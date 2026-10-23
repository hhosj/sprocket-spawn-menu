using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Sprocket;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Serialization;
using Sprocket.Vehicles.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SprocketSpawnMenu;

/// <summary>
/// One-shot spawn attempt. Everything is logged step by step so a single Windows test run
/// answers whether the scene spawner / blueprint loading / team override path actually works.
/// </summary>
internal static class SpawnTest
{
    private static object _pending;
    private static float _startedAt;
    private static VehicleRegister _register;
    private static int _registerCountBefore = -1;

    // Keep the spawn result and the vehicle alive, and log its transform for a few seconds so
    // a "spawns then vanishes" report can be split into falling / destroyed / deregistered.
    private static object _spawnResult;
    private static Transform _trackedTransform;
    private static float _nextTrack;
    private static int _trackSeconds;
    private static float _spawnGroundY;

    private static readonly List<VehicleSpawn> Spawned = new();
    private static Sprocket.PlayerControl.Player _player;
    private static string _lastPlayerState;
    private static string _lastScene;
    private static float _nextStatePoll;

    internal static void Poll()
    {
        if (_pending == null) return;

        try
        {
            if (!GetBool(_pending, "IsCompleted"))
            {
                if (Time.realtimeSinceStartup - _startedAt > 60f)
                {
                    LogError("spawn task still not completed after 60 s - giving up on it.");
                    _pending = null;
                }

                return;
            }

            var status = Get(_pending, "Status");
            Log($"spawn task completed: status={status}");

            var exception = Get(_pending, "Exception");
            if (exception != null)
            {
                LogError($"spawn task faulted: {exception}");
                _pending = null;
                return;
            }

            var result = Get(_pending, "Result");
            Log($"spawn result = {result}");

            if (result is VehicleSpawn spawn)
            {
                Spawned.Add(spawn);
                Log($"[spawn] active spawned vehicles = {Spawned.Count}");
            }

            if (result != null)
            {
                Log($"  Spawned={SafeGet(result, "Spawned")} Vehicle={SafeGet(result, "Vehicle")}");
                var vehicle = Get(result, "Vehicle");
                if (vehicle != null)
                {
                    Log($"  Vehicle type={vehicle.GetType().FullName} " +
                        $"BehaviourEnabled={SafeGet(vehicle, "BehaviourEnabled")}");

                    RegisterIfNeeded(vehicle);

                    var root = GetVehicleTransform(vehicle);
                    if (root == null)
                    {
                        LogWarning("could not resolve the spawned vehicle's transform; " +
                                   "skipping ground snap / physics / tracking.");
                    }
                    else
                    {
                        SnapToGround(root, _spawnGroundY);
                        EnablePhysics(root);
                        StartTracking(result, root);
                    }
                }

                Probe.DumpVehicleRegister();
            }

            _pending = null;
        }
        catch (Exception e)
        {
            LogError($"poll failed: {e}");
            _pending = null;
        }
    }

    internal static void PollPlayerState()
    {
        var now = Time.realtimeSinceStartup;
        if (now < _nextStatePoll) return;
        _nextStatePoll = now + 0.25f;

        try
        {
            var scene = SceneManager.GetActiveScene().name;
            if (scene != _lastScene)
            {
                _lastScene = scene;
                _lastPlayerState = null;
                ClearSpawned($"scene changed to {scene}", dispose: false);
            }

            if (ReferenceEquals(_player, null)) _player = FindPlayer();
            if (ReferenceEquals(_player, null)) return;

            var state = _player.CurrentState;
            if (state == null) return;

            var stateName = state.StateName;
            if (string.IsNullOrEmpty(stateName)) return;
            if (stateName == _lastPlayerState) return;

            Log($"[state] player state: {_lastPlayerState ?? "<none>"} -> {stateName}");
            _lastPlayerState = stateName;

            if (SpawnMenuPlugin.CfgClearOnEditorReturn.Value &&
                stateName.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ClearSpawned("returned to the vehicle editor");
            }

            // Catch a spawn that completed after the state transition.
            if (SpawnMenuPlugin.CfgClearOnEditorReturn.Value &&
                stateName.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0 &&
                Spawned.Count > 0)
            {
                ClearSpawned("still in the vehicle editor");
            }
        }
        catch
        {
            // The player object is rebuilt on scene changes; re-find it next poll.
            _player = null;
        }
    }

    private static int CountAIs()
    {
        try
        {
            var registerType = Interop.FindType("Sprocket.ArtificialIntelligence.AIRegister");
            if (registerType == null) return -1;

            object list = null;

            if (Interop.TryGetValue(registerType, null, "register", out var field)) list = field;
            if (list == null)
            {
                var getter = registerType.GetMethod("get_RegisteredAIs", Interop.StaticAny);
                list = getter?.Invoke(null, null);
            }

            if (list == null) return -1;

            var listType = list.GetType();
            var countProperty = listType.GetProperty("Count", Interop.InstanceAny);
            if (countProperty != null) return (int)countProperty.GetValue(list);

            var getCount = listType.GetMethod("get_Count", Interop.InstanceAny);
            if (getCount != null) return (int)getCount.Invoke(list, null);
        }
        catch
        {
            // best effort only
        }

        return -1;
    }

    internal static void CleanupForSceneExit() => ClearSpawned("match exit");

    private static void ClearSpawned(string reason, bool dispose = true)
    {
        if (Spawned.Count == 0) return;

        Log($"[cleanup] {reason}: {(dispose ? "disposing" : "forgetting")} {Spawned.Count} spawned vehicle(s)");

        if (!dispose)
        {
            Spawned.Clear();
            _trackedTransform = null;
            _spawnResult = null;
            _register = null;
            _registerCountBefore = -1;
            return;
        }

        var aisBefore = CountAIs();

        foreach (var spawn in Spawned)
        {
            try
            {
                // Use the game's own disposal path. VehicleSpawn.Dispose() -> factory.Destroy()
                // is what StageVehicleSpawnEvent uses, and it also removes the vehicle's AIs
                // from the STATIC AIRegister/AvoidanceEntityRegister. Deactivating the GameObject
                // or manually deregistering first leaves those static entries behind, which then
                // throw every frame in AIRegister.Update (and look like missing tracks / loss of
                // control in the next match).
                spawn.Dispose();
            }
            catch (Exception e)
            {
                LogWarning($"[cleanup] one vehicle could not be disposed: {e.GetType().Name}");
            }
        }

        var aisAfter = CountAIs();
        Spawned.Clear();
        _trackedTransform = null;
        _spawnResult = null;
        _registerCountBefore = _register?.TotalRegisteredCount ?? -1;
        Log($"[cleanup] done (AIs {aisBefore} -> {aisAfter})");
    }

    internal static void Track()
    {
        if (ReferenceEquals(_trackedTransform, null)) return;

        var now = Time.realtimeSinceStartup;
        if (now < _nextTrack) return;
        _nextTrack = now + 1f;

        try
        {
            var position = _trackedTransform.position;
            var active = _trackedTransform.gameObject.activeInHierarchy;
            var registered = _register?.TotalRegisteredCount ?? -1;

            _trackSeconds++;
            Log($"[track] +{_trackSeconds}s pos={position} active={active} registered={registered}");

            if (_trackSeconds >= 10) _trackedTransform = null;
        }
        catch (Exception e)
        {
            LogError($"[track] lost the spawned vehicle after {_trackSeconds}s: {e.GetType().Name}");
            _trackedTransform = null;
            _spawnResult = null;
        }
    }

    private static void StartTracking(object result, Transform root)
    {
        try
        {
            _spawnResult = result;
            _trackedTransform = root;
            _trackSeconds = 0;
            _nextTrack = Time.realtimeSinceStartup + 1f;

            Log($"[track] spawned go={root.gameObject.name} pos={root.position} " +
                $"active={root.gameObject.activeInHierarchy}");
        }
        catch (Exception e)
        {
            LogError($"tracking setup failed: {e}");
        }
    }

    /// <summary>
    /// The interop proxy you get from VehicleSpawn.Vehicle is typed IVehicleEditGateway and
    /// does NOT satisfy an `as IVehicleGateway` cast (interface identity differs). TryCast goes
    /// through the native type check, and the concrete Vehicle class is the fallback.
    /// </summary>
    private static Transform GetVehicleTransform(object vehicleObject)
    {
        try
        {
            if (!(vehicleObject is Il2CppObjectBase proxy)) return null;

            var gateway = proxy.TryCast<IVehicleGateway>();
            if (gateway != null) return gateway.transform;

            var vehicle = proxy.TryCast<Vehicle>();
            if (vehicle != null) return vehicle.transform;
        }
        catch (Exception e)
        {
            LogWarning($"resolving vehicle transform failed: {e.Message}");
        }

        return null;
    }

    private static void SnapToGround(Transform root, float groundY)
    {
        try
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                LogWarning("no renderers on the spawned vehicle - cannot snap it to the ground.");
                return;
            }

            // Only visible mesh geometry: trail/particle/shadow renderers would poison the bounds.
            Bounds? meshBounds = null;
            foreach (var renderer in renderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                if (meshBounds == null)
                {
                    meshBounds = renderer.bounds;
                }
                else
                {
                    var union = meshBounds.Value;
                    union.Encapsulate(renderer.bounds);
                    meshBounds = union;
                }
            }

            if (meshBounds == null) meshBounds = renderers[0].bounds;
            var bounds = meshBounds.Value;

            var delta = groundY + 0.02f - bounds.min.y;
            var position = root.position;

            Log($"vehicle bounds min={bounds.min} max={bounds.max} groundY={groundY:0.00} " +
                $"delta={delta:0.00}");

            if (Math.Abs(delta) > 0.005f)
            {
                root.position = new Vector3(position.x, position.y + delta, position.z);
                Log($"snapped to ground, new pos={root.position}");
            }
        }
        catch (Exception e)
        {
            LogError($"ground snap failed: {e}");
        }
    }

    internal static void TrySpawn()
    {
        if (_pending != null)
        {
            LogError("a spawn is already in flight; wait for it to finish.");
            return;
        }

        try
        {
            var controllers = UnityEngine.Object.FindObjectsByType<Sprocket.GameControl.GameController>(
                FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                LogError("no GameController in the scene - spawn only works inside a battle/sandbox. " +
                         "Press F9 first and check the probe output.");
                return;
            }

            BlueprintLibrary.EnsureScanned();
            var path = BlueprintLibrary.Current;
            if (path == null)
            {
                LogError("no blueprint to spawn - the library is empty. " +
                         "Press F9 to list what was found, or save a vehicle in the designer once.");
                return;
            }

            Log($"selected blueprint {BlueprintLibrary.Index + 1}/{BlueprintLibrary.Count}: {path}");

            Log($"loading blueprint: {path}");
            var serializer = new VehicleBlueprintSerializer();
            var blueprint = serializer.LoadFromPath(path);
            if (blueprint == null)
            {
                LogError($"LoadFromPath('{path}') returned null.");
                return;
            }

            Log($"blueprint: {Probe.DescribeBlueprint(blueprint)}");

            var spawner = FindOrCreateSpawner();
            if (spawner == null) return;

            _register = FindVehicleRegister();
            _registerCountBefore = _register?.TotalRegisteredCount ?? -1;
            Log($"vehicle register = {(_register == null ? "<null>" : _register.GetType().Name)} " +
                $"countBefore={_registerCountBefore}");

            var player = FindPlayer();
            var playerPosition = player != null ? player.transform.position : Vector3.zero;

            Vector3 position;
            Quaternion rotation;
            float groundY;

            if (SpawnMenuPlugin.CfgSpawnAtAim.Value && TryGetAimPoint(player, out var aimPoint))
            {
                groundY = aimPoint.y;
                position = new Vector3(aimPoint.x, groundY + SpawnMenuPlugin.CfgHeight.Value, aimPoint.z);
                rotation = FacePlayerRotation(playerPosition, position, player);

                Log($"aim spawn: point={aimPoint} groundY={groundY:0.00} spawn={position}");
            }
            else if (player != null)
            {
                var t = player.transform;

                // Horizontal direction only: the player is usually looking slightly down.
                var forward = t.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                var flat = playerPosition + forward * SpawnMenuPlugin.CfgDistance.Value;
                groundY = playerPosition.y;

                if (SpawnMenuPlugin.CfgGroundSnap.Value)
                {
                    var origin = new Vector3(flat.x, playerPosition.y + 300f, flat.z);

                    // Ignore triggers: the sandbox map has trigger volumes (spawn zones, water
                    // markers) and hitting one puts the vehicle in mid-air, where it falls out
                    // of the world and disappears.
                    if (Physics.Raycast(origin, Vector3.down, out var hit, 1000f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        groundY = hit.point.y;
                        var collider = hit.collider;
                        Log($"ground raycast hit at {hit.point} " +
                            $"collider={collider.name} layer={collider.gameObject.layer} " +
                            $"trigger={collider.isTrigger}");
                    }
                    else
                    {
                        LogWarning("ground raycast missed everything; using the player's height.");
                    }
                }

                position = new Vector3(flat.x, groundY + SpawnMenuPlugin.CfgHeight.Value, flat.z);
                rotation = FacePlayerRotation(playerPosition, position, player);

                Log($"player={player.name} pos={playerPosition} forward={t.forward} " +
                    $"flatForward={forward} distance={SpawnMenuPlugin.CfgDistance.Value:0.#} " +
                    $"groundY={groundY:0.00}");
            }
            else
            {
                position = new Vector3(0f, 5f, 150f);
                rotation = Quaternion.identity;
                groundY = 5f;
                LogWarning("no Player found - spawning at a fixed guess position instead.");
            }

            _spawnGroundY = groundY;

            var team = (TeamID)(1 << (SpawnMenuPlugin.CfgTeam.Value - 1));
            var group = SpawnMenuPlugin.CfgGroup.Value <= 0
                ? GroupID.None
                : (GroupID)(1 << (SpawnMenuPlugin.CfgGroup.Value - 1));

            var instance = new VehicleSpawnInstance
            {
                position = position,
                rotation = rotation,
                teamIdOverride = team,
                groupIdOverride = group,
            };

            Log($"spawn at pos={position} rot={rotation} team={team} group={group} " +
                $"options={VehicleSpawnOptions.None}");

            // The interop wrapper for this overload takes
            // Il2CppSystem.Threading.CancellationToken (a reference type), so `default` is
            // null and Il2CppObjectBaseToPtrNotNull throws before the game is even called.
            var cancellationToken = Il2CppSystem.Threading.CancellationToken.None;

            // Do NOT write `cancellationToken == null`: the interop type overloads op_Equality,
            // and that operator calls Il2CppObjectBaseToPtrNotNull, which throws on null.
            if (ReferenceEquals(cancellationToken, null))
            {
                LogWarning("CancellationToken.None was null; building an un-cancelled token instead.");
                cancellationToken = new Il2CppSystem.Threading.CancellationToken(false);
            }

            if (ReferenceEquals(cancellationToken, null))
            {
                LogError("could not build a CancellationToken - cannot call Spawn.");
                return;
            }

            Log($"cancellationToken type={cancellationToken.GetType().Name}");

            _startedAt = Time.realtimeSinceStartup;
            var task = spawner.Spawn(blueprint, instance, VehicleSpawnOptions.None, cancellationToken);
            _pending = task;

            if (task == null)
            {
                LogError("Spawn() returned null.");
                return;
            }

            Log($"Spawn() accepted, task type = {task.GetType().FullName}");
        }
        catch (Exception e)
        {
            LogError($"spawn failed: {e}");
        }
    }

    // --- helpers ------------------------------------------------------------------------

    private static VehicleSpawner FindOrCreateSpawner()
    {
        var existing = UnityEngine.Object.FindObjectsByType<VehicleSpawner>(FindObjectsSortMode.None);
        Log($"scene spawners = {existing?.Length ?? -1}");
        if (existing != null && existing.Length > 0)
        {
            foreach (var candidate in existing)
            {
                if (!candidate.gameObject.activeInHierarchy) continue;
                Log($"using scene spawner '{candidate.name}' at {candidate.transform.position}.");
                return candidate;
            }

            Log($"all {existing.Length} scene spawner(s) are inactive; using the first one anyway.");
            return existing[0];
        }

        if (!SpawnMenuPlugin.CfgCreateSpawner.Value)
        {
            LogError("scene has no VehicleSpawner and CreateSpawnerIfMissing is false.");
            return null;
        }

        Log("no scene spawner - creating a GameObject with a VehicleSpawner component.");
        var go = new GameObject("SpawnMenuSpawner");
        var spawner = go.AddComponent<VehicleSpawner>();
        if (spawner == null)
        {
            LogError("AddComponent<VehicleSpawner>() returned null.");
            return null;
        }

        var resources = VehicleAssemblyResources.Default;
        if (resources == null)
        {
            LogError("VehicleAssemblyResources.Default is null - the game has not initialised its " +
                     "assembly pipeline yet. Try again a few seconds after the battle starts.");
            return null;
        }

        var applied = Interop.TrySetValue(
            typeof(VehicleSpawner), spawner, "assemblyResourcesOverride", resources);
        Log($"created spawner; default resources={resources}; override set={applied}");

        return spawner;
    }

    private static void EnablePhysics(Transform root)
    {
        try
        {
            if (root == null)
            {
                LogWarning("no vehicle transform - cannot enable physics.");
                return;
            }

            var bodies = root.GetComponentsInChildren<Rigidbody>(true);
            Rigidbody body = null;

            if (bodies != null)
            {
                // The main hull body is by far the heaviest; wheels/turrets are light.
                foreach (var candidate in bodies)
                {
                    if (body == null || candidate.mass > body.mass) body = candidate;
                }
            }

            if (body == null)
            {
                LogWarning("no Rigidbody found on the spawned vehicle - it will not settle.");
                return;
            }

            var bodyCount = bodies == null ? 0 : bodies.Length;
            Log($"rigidbody: go={body.gameObject.name} isKinematic={body.isKinematic} " +
                $"useGravity={body.useGravity} mass={body.mass} (of {bodyCount})");

            // StageVehicleSpawnEvent does exactly this after Spawn(): the factory leaves the
            // body kinematic during assembly, and without this the vehicle hangs in the air.
            body.isKinematic = false;
            body.useGravity = true;

            Log("rigidbody.isKinematic = false (vehicle can fall/settle)");
        }
        catch (Exception e)
        {
            LogError($"enabling vehicle physics failed: {e}");
        }
    }

    private static void RegisterIfNeeded(object vehicleObject)
    {
        if (_register == null)
        {
            LogWarning("no vehicle register found - the spawned vehicle may not be ticked by the game.");
            return;
        }

        if (!(vehicleObject is IVehicleEditGateway vehicle))
        {
            LogWarning($"spawned vehicle is {vehicleObject.GetType().FullName}, not IVehicleEditGateway.");
            return;
        }

        try
        {
            var countNow = _register.TotalRegisteredCount;
            Log($"register count after spawn = {countNow} (before {_registerCountBefore})");
            if (countNow > _registerCountBefore) return;

            _register.Register(vehicle);
            Log("spawned vehicle was not registered - Register() called manually.");
        }
        catch (Exception e)
        {
            LogError($"manual register failed: {e}");
        }
    }

    private static VehicleRegister FindVehicleRegister()
    {
        try
        {
            var modes = UnityEngine.Object.FindObjectsByType<GameMode>(FindObjectsSortMode.None);
            foreach (var mode in modes)
            {
                try
                {
                    var scenario = mode.TryCast<ScenarioGameMode>();
                    if (scenario != null &&
                        Interop.TryGetValue(typeof(ScenarioGameMode), scenario, "sharedState", out var shared) &&
                        shared is GameModeSharedState state && state.VehicleRegister != null)
                    {
                        return state.VehicleRegister.TryCast<VehicleRegister>();
                    }
                }
                catch (Exception e)
                {
                    LogWarning($"ScenarioGameMode register lookup failed: {e.Message}");
                }

                try
                {
                    var deathmatch = mode.TryCast<DeathmatchGameMode>();
                    if (deathmatch != null &&
                        Interop.TryGetValue(typeof(DeathmatchGameMode), deathmatch, "vehicleRegister",
                            out var register) && register is IVehicleRegister typed)
                    {
                        return typed.TryCast<VehicleRegister>();
                    }
                }
                catch (Exception e)
                {
                    LogWarning($"DeathmatchGameMode register lookup failed: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            LogWarning($"vehicle register lookup failed: {e.Message}");
        }

        return null;
    }

    private static bool TryGetAimPoint(Sprocket.PlayerControl.Player player, out Vector3 point)
    {
        point = default;

        Camera camera = null;
        try
        {
            if (player != null && player.Cameras != null) camera = player.Cameras.ActiveCamera;
        }
        catch
        {
            // fall back to Camera.main below
        }

        if (camera == null) camera = Camera.main;
        if (camera == null)
        {
            var cameras = Camera.allCameras;
            if (cameras != null && cameras.Length > 0) camera = cameras[0];
        }

        if (camera == null)
        {
            LogWarning("no camera found for the aim ray; falling back to horizontal spawn.");
            return false;
        }

        var centre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        var ray = camera.ScreenPointToRay(centre);

        var hits = Physics.RaycastAll(ray, SpawnMenuPlugin.CfgMaxAimDistance.Value,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            Log("aim ray hit nothing; falling back to horizontal spawn.");
            return false;
        }

        var playerVehicleRoot = FindPlayerVehicleRoot();
        var bestDistance = float.MaxValue;
        RaycastHit? best = null;

        foreach (var hit in hits)
        {
            // The third-person camera looks through the player's own tank.
            if (hit.distance < 2f) continue;
            if (playerVehicleRoot != null && hit.collider.transform.IsChildOf(playerVehicleRoot))
                continue;
            if (hit.distance >= bestDistance) continue;

            bestDistance = hit.distance;
            best = hit;
        }

        if (best == null)
        {
            Log("aim ray only hit things closer than 5 m; falling back to horizontal spawn.");
            return false;
        }

        point = best.Value.point;
        Log($"aim ray hit {best.Value.collider.name} at {point} distance={bestDistance:0.0}");
        return true;
    }

    private static Transform FindPlayerVehicleRoot()
    {
        try
        {
            var controllers = UnityEngine.Object.FindObjectsByType<
                Sprocket.Gameplay.VehicleControl.VehicleController>(FindObjectsSortMode.None);

            if (controllers == null || controllers.Length == 0) return null;

            if (Interop.TryGetValue(typeof(Sprocket.Gameplay.VehicleControl.VehicleController),
                    controllers[0], "controlTarget", out var target) &&
                target is Il2CppObjectBase proxy)
            {
                var behaviour = proxy.TryCast<Sprocket.Vehicles.Control.IAIControllableVehicle>();
                if (behaviour != null) return behaviour.transform.root;
            }
        }
        catch (Exception e)
        {
            LogWarning($"player vehicle lookup failed: {e.Message}");
        }

        return null;
    }

    private static Quaternion FacePlayerRotation(Vector3 playerPosition, Vector3 spawnPosition,
        Sprocket.PlayerControl.Player player)
    {
        if (!SpawnMenuPlugin.CfgFacePlayer.Value)
            return player != null ? player.transform.rotation : Quaternion.identity;

        var toPlayer = playerPosition - spawnPosition;
        toPlayer.y = 0f;

        var rotation = toPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
            : Quaternion.identity;
        rotation *= Quaternion.Euler(0f, SpawnMenuPlugin.CfgFacingOffset.Value, 0f);

        Log($"facing player: dir={toPlayer.normalized} " +
            $"offset={SpawnMenuPlugin.CfgFacingOffset.Value:0.#} deg");
        return rotation;
    }

    private static Sprocket.PlayerControl.Player FindPlayer()
    {
        try
        {
            var players = UnityEngine.Object.FindObjectsByType<Sprocket.PlayerControl.Player>(
                FindObjectsSortMode.None);
            return players != null && players.Length > 0 ? players[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool GetBool(object target, string property)
    {
        var value = Get(target, property);
        return value is bool b && b;
    }

    private static object Get(object target, string property)
    {
        try
        {
            var prop = target.GetType().GetProperty(property,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeGet(object target, string property)
    {
        var value = Get(target, property);
        if (value == null) return "<null>";
        try { return value.ToString(); }
        catch { return value.GetType().Name; }
    }

    private static void Log(string message) => SpawnMenuPlugin.Logger.LogInfo($"[spawn] {message}");

    private static void LogWarning(string message) => SpawnMenuPlugin.Logger.LogWarning($"[spawn] {message}");

    private static void LogError(string message) => SpawnMenuPlugin.Logger.LogError($"[spawn] {message}");
}
