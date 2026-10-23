using System;
using Sprocket;
using Sprocket.GameControl;
using Sprocket.Spawning;
using Sprocket.VehicleDesigner;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Serialization;
using Sprocket.Vehicles.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerBehaviour = Sprocket.PlayerControl.Player;

namespace SprocketSpawnMenu;

/// <summary>
/// Read-only scene probe. Answers the open questions from the handover (is there a spawner in
/// the scene, where do blueprints live, which team is the player on, which GameMode runs the
/// sandbox) without changing any game state.
/// </summary>
internal static class Probe
{
    private static string _scene;
    private static float _sceneSeenAt;
    private static bool _autoDumped;

    private static float _nextAutoCheck;

    internal static void MaybeAutoDump()
    {
        // This runs from a per-frame patch, so it must stay cheap. In 0.1.0 it scanned the
        // whole scene with FindObjectsByType every single frame; when the sandbox "start"
        // button loaded the player's tank (thousands of extra objects) that scan became the
        // dominant frame cost and the game appeared to freeze. Now the scene name is checked
        // at most once a second and the expensive scan only happens once per scene.
        var now = Time.realtimeSinceStartup;
        if (now < _nextAutoCheck) return;
        _nextAutoCheck = now + 1f;

        var scene = SceneManager.GetActiveScene().name;
        if (scene != _scene)
        {
            _scene = scene;
            _sceneSeenAt = now;
            _autoDumped = false;
            return;
        }

        if (_autoDumped) return;
        if (now - _sceneSeenAt < 3f) return;

        GameController[] controllers;
        try { controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsSortMode.None); }
        catch { return; }

        if (controllers == null || controllers.Length == 0) return;

        _autoDumped = true;
        Dump("auto");
    }

    internal static void Dump(string trigger)
    {
        Log($"===== probe ({trigger}) =====");
        Try("environment", () =>
        {
            Log($"game={Application.version} unity={Application.unityVersion} " +
                $"scene={SceneManager.GetActiveScene().name} frames={Time.frameCount} " +
                $"realtime={Time.realtimeSinceStartup:0.0}");
            Log($"persistentDataPath={Application.persistentDataPath}");
        });

        Try("GameController", () =>
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsSortMode.None);
            Log($"instances={controllers.Length}");
            foreach (var gc in controllers)
            {
                Log($"  {gc.GetType().Name} go={gc.name} active={gc.isActiveAndEnabled}");
                foreach (var field in new[] { "gamemode", "state", "setupContext", "player" })
                {
                    if (Interop.TryGetValue(typeof(GameController), gc, field, out var value))
                        Log($"    {field} = {Describe(value)}");
                }
            }
        });

        Try("GameMode", () =>
        {
            var modes = UnityEngine.Object.FindObjectsByType<GameMode>(FindObjectsSortMode.None);
            Log($"instances={modes.Length}");
            foreach (var gm in modes)
            {
                Log($"  {gm.GetType().FullName} mode={Safe(() => gm.Mode.ToString())} " +
                    $"state={Safe(() => gm.State.ToString())} active={gm.isActiveAndEnabled}");
                Interop.LogTeamLikeFields(gm.GetType(), gm, s => Log($"    {s}"));
            }
        });

        Try("vehicle register", () => DumpVehicleRegister());

        Try("VehicleSpawner", () =>
        {
            var spawners = UnityEngine.Object.FindObjectsByType<VehicleSpawner>(FindObjectsSortMode.None);
            Log($"instances={spawners.Length}");
            foreach (var s in spawners)
            {
                Log($"  id={s.SpawnerID} team={s.TeamID} group={s.GroupID} " +
                    $"name={s.nameOverride ?? "<null>"} appendID={s.appendNameID} " +
                    $"go={s.name} active={s.gameObject.activeInHierarchy} pos={s.transform.position}");
                Interop.TryGetValue(typeof(VehicleSpawner), s, "assemblyResourcesOverride", out var res);
                Log($"    assemblyResourcesOverride={Describe(res)}" +
                    $" defaultResources={Describe(VehicleAssemblyResources.Default)}");
            }
        });

        Try("VehicleSource", () =>
        {
            var sources = UnityEngine.Object.FindObjectsByType<VehicleSource>(FindObjectsSortMode.None);
            Log($"instances={sources.Length}");
            foreach (var src in sources)
            {
                Log($"  go={src.name} mode={src.Mode} bp={src.BlueprintName ?? "<null>"} " +
                    $"sub={src.Subdirectory ?? "<null>"} source={src.Source} " +
                    $"mass={src.MinMass}..{src.MaxMass} pos={src.transform.position}");
                // Deliberately do NOT call GetBlueprint() here. It loads the blueprint and
                // may have side effects the game relies on when the sandbox "start" button
                // assembles the targets; the BlueprintName field above is enough to identify it.
            }
        });

        Try("SpawnLocator", () =>
        {
            var locators = UnityEngine.Object.FindObjectsByType<SpawnLocator>(FindObjectsSortMode.None);
            Log($"instances={locators.Length}");
            foreach (var loc in locators)
            {
                Log($"  {loc.GetType().Name} maxSpawn={Safe(() => loc.MaxSpawn.ToString())} " +
                    $"next={Safe(() => loc.NextSpawnIndex.ToString())} pos={loc.transform.position}");
            }
        });

        Try("Player", () =>
        {
            var players = UnityEngine.Object.FindObjectsByType<PlayerBehaviour>(FindObjectsSortMode.None);
            Log($"instances={players.Length}");
            foreach (var p in players)
            {
                Log($"  go={p.name} pos={p.transform.position} " +
                    $"state={Safe(() => p.CurrentState == null ? "<null>" : p.CurrentState.name)}");
                Interop.LogTeamLikeFields(p.GetType(), p, s => Log($"    {s}"));
            }
        });

        Try("player vehicles", () =>
        {
            var vehicles = UnityEngine.Object.FindObjectsByType<Sprocket.Gameplay.VehicleControl.VehicleController>(
                FindObjectsSortMode.None);
            Log($"VehicleController instances={vehicles.Length}");
            foreach (var v in vehicles)
                Log($"  go={v.name} pos={v.transform.position} active={v.isActiveAndEnabled}");
        });

        Try("blueprint directories", () =>
        {
            foreach (GameDataDirectory dir in Enum.GetValues(typeof(GameDataDirectory)))
            {
                Try($"dir {dir}", () =>
                {
                    var path = VehicleBlueprintFiler.GetVehicleBlueprintDirectory(dir);
                    var all = VehicleBlueprintFiler.GetAllVehicleBlueprints(path);
                    Log($"  {dir}: path={path} count={(all == null ? -1 : all.Length)}");
                });
            }
        });

        Try("blueprint list", () =>
        {
            BlueprintLibrary.Rescan();
            BlueprintLibrary.LogAll();
        });

        Try("last saved", () =>
        {
            var found = LastSavedVehicleBlueprint.TryGet(out var path);
            Log($"TryGet={found} path={path ?? "<null>"}");
        });

        Try("assembly resources", () =>
        {
            var res = VehicleAssemblyResources.Default;
            Log($"Default={Describe(res)}");
            Log($"IVehicleFactory.Factory={Describe(Sprocket.Vehicles.IVehicleFactory.Factory)}");
            // GetFactories() is intentionally not called: it can create/cache the assembly
            // stage factories, and doing that before the game's own "start" transition is
            // exactly the kind of side effect this diagnostic build must avoid.
        });

        Log("===== end probe =====");
    }

    /// <summary>
    /// Finds the vehicle register that the active GameMode owns and logs every behaviour's
    /// EntityID team. This is the only reliable way to see what team a vehicle really got.
    /// </summary>
    internal static void DumpVehicleRegister()
    {
        var mode = UnityEngine.Object.FindObjectsByType<GameMode>(FindObjectsSortMode.None);
        if (mode == null || mode.Length == 0)
        {
            Log("no GameMode instance - cannot reach the vehicle register.");
            return;
        }

        foreach (var gm in mode)
        {
            var shared = GetMember(gm, "sharedState");
            var register = GetMember(gm, "vehicleRegister") ?? GetMember(shared, "VehicleRegister");
            var playerVehicle = GetMember(shared, "PlayerVehicle");

            if (shared != null) Log($"  {gm.GetType().Name}.sharedState = {shared.GetType().Name}");
            if (playerVehicle != null)
            {
                Log($"  PlayerVehicle = {playerVehicle.GetType().FullName} " +
                    $"BehaviourEnabled={GetMember(playerVehicle, "BehaviourEnabled")}");
            }

            if (register == null)
            {
                Log($"  {gm.GetType().Name}: no register field reachable.");
                continue;
            }

            var vehicles = GetMember(register, "RegisteredVehicles") as System.Collections.IEnumerable;
            var behaviours = GetMember(register, "Behaviours") as System.Collections.IEnumerable;

            var vehicleCount = 0;
            if (vehicles != null)
            {
                foreach (var v in vehicles)
                {
                    vehicleCount++;
                    Log($"    vehicle {v?.GetType().Name} behaviourEnabled={GetMember(v, "BehaviourEnabled")}");
                }
            }

            var behaviourCount = 0;
            if (behaviours != null)
            {
                foreach (var b in behaviours)
                {
                    behaviourCount++;
                    var id = GetMember(b, "ID");
                    var team = GetMember(id, "TeamID");
                    var flags = GetMember(b, "Flags");
                    Log($"    behaviour {b?.GetType().Name} team={team} flags={flags}");
                }
            }

            Log($"  register={register.GetType().Name} vehicles={vehicleCount} behaviours={behaviourCount}");
        }
    }

    private static object GetMember(object instance, string name)
    {
        if (instance == null) return null;
        return Interop.TryGetValue(instance.GetType(), instance, name, out var value) ? value : null;
    }

    internal static string DescribeBlueprint(IVehicleBlueprint bp)
    {
        if (bp == null) return "<null>";

        try
        {
            var header = bp.Header;
            return $"{bp.GetType().Name} valid={bp.Valid} name={header?.Name ?? "<null>"} " +
                   $"mass={header?.Mass} version={bp.LoadedVersion}";
        }
        catch (Exception e)
        {
            return $"{bp.GetType().Name} (header failed: {e.GetType().Name})";
        }
    }

    private static string Describe(object value)
    {
        if (value == null) return "<null>";

        try { return value.ToString(); }
        catch { return value.GetType().Name; }
    }

    private static string Safe(Func<string> get)
    {
        try { return get() ?? "<null>"; }
        catch (Exception e) { return $"<{e.GetType().Name}>"; }
    }

    private static void Try(string label, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log($"[{label}] FAILED: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void Log(string message) => SpawnMenuPlugin.Logger.LogInfo($"[probe] {message}");
}
