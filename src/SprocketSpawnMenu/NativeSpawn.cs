using System;
using System.IO;
using System.Reflection;
using Il2CppInterop.Runtime.Injection;
using Sprocket;
using Sprocket.Spawning;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Missions;
using Sprocket.Vehicles.Serialization;
using Sprocket.Vehicles.Spawning;
using UnityEngine;

namespace SprocketSpawnMenu;

/// <summary>
/// Spawn locator injected into IL2CPP that always returns the current crosshair point.
/// The game's StageVehicleSpawnEvent asks for a SpawnPointInfo and we feed it our aim point.
/// </summary>
public sealed class AimSpawnLocator : SpawnLocator
{
    public AimSpawnLocator(IntPtr pointer) : base(pointer) { }

    internal static Vector3 Position;
    internal static Quaternion Rotation = Quaternion.identity;

    public override int MaxSpawn => 1;

    public override int NextSpawnIndex => 0;

    public override void Get(out SpawnPointInfo point) =>
        point = new SpawnPointInfo(Position, Rotation);

    public override void Get(int spawnIndex, out SpawnPointInfo point) => Get(out point);
}

/// <summary>
/// Spawns through the game's own StageVehicleSpawnEvent instead of calling Spawner.Spawn and
/// registering by hand. Registration, AI activation, ownership (VehicleSpawn[]) and cleanup
/// (StageReset) are then handled by the game.
/// </summary>
internal static class NativeSpawn
{
    private static bool _locatorRegistered;
    private static GameObject _root;
    private static StageVehicleSpawnEvent _stage;

    internal static bool TrySpawn(VehicleRegister register, string blueprintPath)
    {
        Cleanup();

        if (register == null || string.IsNullOrEmpty(blueprintPath))
        {
            LogError("native spawn needs a vehicle register and a blueprint path.");
            return false;
        }

        if (!File.Exists(blueprintPath))
        {
            LogError($"blueprint does not exist: {blueprintPath}");
            return false;
        }

        try
        {
            var player = SpawnTest.FindPlayer();

            if (!SpawnTest.TryGetAimPoint(player, out var position))
            {
                position = FallbackPosition(player);
                LogWarning("aim ray missed; using the fallback position in front of the player.");
            }

            AimSpawnLocator.Position = position;
            AimSpawnLocator.Rotation = SpawnTest.FacePlayerRotation(
                player != null ? player.transform.position : position, position, player);

            if (!_locatorRegistered)
            {
                ClassInjector.RegisterTypeInIl2Cpp<AimSpawnLocator>();
                _locatorRegistered = true;
            }

            _root = new GameObject("Sprocket.SpawnMenu.Stage");

            var spawner = _root.AddComponent<VehicleSpawner>();
            ApplyTeam(spawner);

            var source = _root.AddComponent<VehicleSource>();
            ConfigureSource(source, blueprintPath);

            var locator = _root.AddComponent<AimSpawnLocator>();
            _stage = _root.AddComponent<StageVehicleSpawnEvent>();

            SetField("spawnLimit", 1);
            SetField("useBudget", false);
            SetField("costBudget", 0f);
            SetField("subtractExisting", false);
            SetField("delay", 0f);
            SetField("blueprintSource", source);
            SetField("spawner", spawner);
            SetField("spawnLocator", locator);

            var interfaceRegister = register.TryCast<IVehicleRegister>();
            if (interfaceRegister == null)
            {
                LogError("could not cast VehicleRegister to IVehicleRegister.");
                Cleanup();
                return false;
            }

            _stage.SetVehicleRegister(interfaceRegister);

            var start = typeof(StageVehicleSpawnEvent).GetMethod(
                "StageStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (start == null)
            {
                LogError("StageVehicleSpawnEvent.StageStart not found.");
                Cleanup();
                return false;
            }

            var task = start.Invoke(_stage, new object[] { Il2CppSystem.Threading.CancellationToken.None });

            Log($"native StageStart invoked at {position} (blueprint={Path.GetFileName(blueprintPath)}, " +
                $"task={task?.GetType().Name})");
            return true;
        }
        catch (Exception e)
        {
            LogError($"native spawn failed: {e}");
            Cleanup();
            return false;
        }
    }

    internal static void Cleanup()
    {
        if (_stage != null)
        {
            try
            {
                var reset = typeof(StageVehicleSpawnEvent).GetMethod(
                    "StageReset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                reset?.Invoke(_stage, null);
                Log("native StageReset invoked.");
            }
            catch (Exception e)
            {
                LogWarning($"StageReset failed: {e.GetType().Name}: {e.Message}");
            }
        }

        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
            Log("native stage GameObject destroyed.");
        }

        _stage = null;
        _root = null;
    }

    private static void ConfigureSource(VehicleSource source, string blueprintPath)
    {
        source.Mode = SpawnMode.Explicit;
        source.BlueprintName = Path.GetFileNameWithoutExtension(blueprintPath);
        source.Subdirectory = string.Empty;
        source.Source = GameDataDirectory.Documents;

        foreach (GameDataDirectory directory in Enum.GetValues(typeof(GameDataDirectory)))
        {
            try
            {
                var baseDirectory = VehicleBlueprintFiler.GetVehicleBlueprintDirectory(directory);
                if (string.IsNullOrEmpty(baseDirectory)) continue;

                var normalized = baseDirectory.TrimEnd('\\', '/');
                if (!blueprintPath.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)) continue;

                source.Source = directory;

                var relative = blueprintPath.Substring(normalized.Length).TrimStart('\\', '/');
                var relativeDirectory = Path.GetDirectoryName(relative);
                if (!string.IsNullOrEmpty(relativeDirectory))
                    source.Subdirectory = relativeDirectory;

                Log($"source={directory} subdirectory='{source.Subdirectory}' " +
                    $"blueprint='{source.BlueprintName}'");
                return;
            }
            catch
            {
                // try the next data directory
            }
        }

        LogWarning("blueprint path did not match a game data directory; using Documents.");
    }

    private static void ApplyTeam(VehicleSpawner spawner)
    {
        var team = (TeamID)(1 << (SpawnMenuPlugin.CfgTeam.Value - 1));
        var group = SpawnMenuPlugin.CfgGroup.Value <= 0
            ? GroupID.None
            : (GroupID)(1 << (SpawnMenuPlugin.CfgGroup.Value - 1));

        Interop.TrySetValue(typeof(VehicleSpawner), spawner, "teamID", team);
        Interop.TrySetValue(typeof(VehicleSpawner), spawner, "groupID", group);
    }

    private static Vector3 FallbackPosition(Sprocket.PlayerControl.Player player)
    {
        if (player == null) return new Vector3(0f, 2f, 150f);

        var transform = player.transform;
        var forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        var flat = transform.position + forward * SpawnMenuPlugin.CfgDistance.Value;
        var ground = transform.position.y;

        if (Physics.Raycast(new Vector3(flat.x, transform.position.y + 300f, flat.z),
                Vector3.down, out var hit, 1000f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            ground = hit.point.y;

        return new Vector3(flat.x, ground + SpawnMenuPlugin.CfgHeight.Value, flat.z);
    }

    private static void SetField(string name, object value)
    {
        if (!Interop.TrySetValue(typeof(StageVehicleSpawnEvent), _stage, name, value))
            LogWarning($"could not set StageVehicleSpawnEvent.{name}");
    }

    private static void Log(string message) => SpawnMenuPlugin.Logger.LogInfo($"[native] {message}");

    private static void LogWarning(string message) => SpawnMenuPlugin.Logger.LogWarning($"[native] {message}");

    private static void LogError(string message) => SpawnMenuPlugin.Logger.LogError($"[native] {message}");
}
