using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Sprocket;
using Sprocket.GameControl;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SprocketSpawnMenu;

/// <summary>
/// Restores a way to spawn vehicles in Sprocket 0.2, where the sandbox spawn menu was removed.
///
/// This first build is a probe: F9 writes everything we need to know about the running scene
/// to BepInEx/LogOutput.log, F10 attempts one real spawn (team override = AI side) and logs
/// every step. Nothing is patched except a per-frame tick.
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class SpawnMenuPlugin : BasePlugin
{
    public const string PluginGuid = "sprocket.spawn.menu";
    public const string PluginName = "Sprocket Spawn Menu";
    public const string PluginVersion = "0.5.1";

    internal static ManualLogSource Logger;

    internal static ConfigEntry<bool> CfgEnabled;
    internal static ConfigEntry<bool> CfgDebug;
    internal static ConfigEntry<bool> CfgHeartbeat;

    internal static ConfigEntry<string> CfgProbeKey;
    internal static ConfigEntry<string> CfgSpawnKey;
    internal static ConfigEntry<string> CfgSpawnFallbackKey;
    internal static ConfigEntry<string> CfgNextKey;
    internal static ConfigEntry<string> CfgPrevKey;
    internal static ConfigEntry<string> CfgUiKey;
    internal static ConfigEntry<bool> CfgAutoProbe;

    internal static ConfigEntry<string> CfgBlueprintName;
    internal static ConfigEntry<bool> CfgUseLastSaved;
    internal static ConfigEntry<int> CfgTeam;
    internal static ConfigEntry<int> CfgGroup;
    internal static ConfigEntry<float> CfgDistance;
    internal static ConfigEntry<float> CfgHeight;
    internal static ConfigEntry<bool> CfgGroundSnap;
    internal static ConfigEntry<bool> CfgFacePlayer;
    internal static ConfigEntry<float> CfgFacingOffset;
    internal static ConfigEntry<bool> CfgSpawnAtAim;
    internal static ConfigEntry<float> CfgMaxAimDistance;
    internal static ConfigEntry<bool> CfgCreateSpawner;
    internal static ConfigEntry<bool> CfgClearOnEditorReturn;
    internal static ConfigEntry<int> CfgConfigVersion;

    public override void Load()
    {
        Logger = Log;

        CfgEnabled = Config.Bind(
            "General", "Enabled", true,
            "Master switch. When false the plugin loads but does nothing.");

        CfgDebug = Config.Bind(
            "General", "DebugLogging", true,
            "Also log the per-frame keys the plugin sees (noisy).");

        CfgHeartbeat = Config.Bind(
            "General", "Heartbeat", true,
            "Write a [tick] line every 10 s. If the game hangs, the last line tells us whether " +
            "the main thread was still running.");

        CfgProbeKey = Config.Bind(
            "Keys", "ProbeKey", "F9",
            "Key that dumps the scene/blueprint probe to the log.");

        CfgSpawnKey = Config.Bind(
            "Keys", "SpawnKey", "F10",
            "Key that attempts one spawn (team override from [Spawn] Team). Some laptop " +
            "keyboards send F10 as a media key and the game never sees it - use the fallback " +
            "key in that case.");

        CfgSpawnFallbackKey = Config.Bind(
            "Keys", "SpawnFallbackKey", "Z",
            "Second key that also attempts a spawn. Z is the key the removed sandbox spawn " +
            "menu used to be bound to, and it is a normal letter key so it always reaches the game.");

        CfgNextKey = Config.Bind(
            "Keys", "NextBlueprintKey", "]",
            "Key that moves the blueprint cursor to the next entry (current selection is logged).");

        CfgPrevKey = Config.Bind(
            "Keys", "PrevBlueprintKey", "[",
            "Key that moves the blueprint cursor to the previous entry.");

        CfgUiKey = Config.Bind(
            "Keys", "UiToggleKey", "B",
            "Key that shows/hides the blueprint overlay.");

        CfgAutoProbe = Config.Bind(
            "Keys", "AutoProbeOnSceneLoad", false,
            "Dump the probe automatically ~3 s after a battle scene loads. Off by default in " +
            "0.1.1 so the freeze investigation is not influenced by the probe; press F9 instead.");

        CfgBlueprintName = Config.Bind(
            "Spawn", "BlueprintName", "",
            "Blueprint to spawn. Empty = use the last vehicle saved in the designer, or the " +
            "first blueprint in Documents/Vehicles. A bare name is looked up as " +
            "Documents/Vehicles/<name>.blueprint; a full path is used as-is.");

        CfgUseLastSaved = Config.Bind(
            "Spawn", "UseLastSavedIfEmpty", true,
            "When BlueprintName is empty, prefer the designer's last saved vehicle.");

        CfgTeam = Config.Bind(
            "Spawn", "Team", 2,
            new ConfigDescription(
                "1-8. The spawned vehicle gets this team override. The player is normally team 1, " +
                "so 2 makes it an enemy (and, hopefully, AI-controlled).",
                new AcceptableValueRange<int>(1, 8)));

        CfgGroup = Config.Bind(
            "Spawn", "Group", 1,
            new ConfigDescription(
                "1-8. Group override for the spawned vehicle. 0 = none.",
                new AcceptableValueRange<int>(0, 8)));

        CfgDistance = Config.Bind(
            "Spawn", "Distance", 150f,
            new ConfigDescription(
                "Metres in front of the player to spawn (horizontal distance).",
                new AcceptableValueRange<float>(5f, 500f)));

        CfgHeight = Config.Bind(
            "Spawn", "Height", 2f,
            new ConfigDescription(
                "Initial metres above the surface found by GroundSnap. The vehicle is placed here " +
                "first (so it is never buried during assembly), then snapped down by its rendered " +
                "bounds once it exists.",
                new AcceptableValueRange<float>(-10f, 50f)));

        CfgGroundSnap = Config.Bind(
            "Spawn", "GroundSnap", true,
            "Raycast down at the spawn point and place the vehicle on the first surface hit " +
            "(terrain, road, building). Off = use the player's height.");

        CfgFacePlayer = Config.Bind(
            "Spawn", "FacePlayer", true,
            "Rotate the spawned vehicle so its forward axis points at the player.");

        CfgSpawnAtAim = Config.Bind(
            "Spawn", "SpawnAtAim", true,
            "Spawn at the point the player's crosshair is looking at (raycast from the active " +
            "camera through the screen centre). Falls back to Distance in front of the player " +
            "when the ray hits nothing.");

        CfgMaxAimDistance = Config.Bind(
            "Spawn", "MaxAimDistance", 800f,
            new ConfigDescription(
                "Maximum crosshair raycast distance in metres.",
                new AcceptableValueRange<float>(10f, 2000f)));

        CfgFacingOffset = Config.Bind(
            "Spawn", "FacingOffsetDegrees", 0f,
            new ConfigDescription(
                "Extra yaw applied after FacePlayer, if the blueprint's forward axis is reversed " +
                "set this to 180.",
                new AcceptableValueRange<float>(-180f, 180f)));

        CfgConfigVersion = Config.Bind(
            "General", "ConfigVersion", 0,
            "Internal: used to migrate old default values once.");

        CfgCreateSpawner = Config.Bind(
            "Spawn", "CreateSpawnerIfMissing", true,
            "If the scene has no VehicleSpawner, create one on a new GameObject and spawn through it.");

        CfgClearOnEditorReturn = Config.Bind(
            "Spawn", "ClearOnEditorReturn", true,
            "Dispose every vehicle this plugin spawned when the player returns to the vehicle " +
            "editor (Space). Without this they stay in the world after switching back to play.");

        if (CfgConfigVersion.Value < 5)
        {
            if (Math.Abs(CfgDistance.Value - 40f) < 0.01f) CfgDistance.Value = 150f;
            if (Math.Abs(CfgHeight.Value - 0.2f) < 0.01f ||
                Math.Abs(CfgHeight.Value - 1f) < 0.01f) CfgHeight.Value = 2f;
            CfgConfigVersion.Value = 5;
            Logger.LogInfo("[config] migrated to v5: Distance=150, Height=2 (initial height, then snap to ground).");
        }

        if (CfgConfigVersion.Value < 6)
        {
            if (string.Equals(CfgNextKey.Value.Trim(), "F8", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(CfgPrevKey.Value.Trim(), "F6", StringComparison.OrdinalIgnoreCase))
            {
                CfgNextKey.Value = "]";
                CfgPrevKey.Value = "[";
            }

            CfgConfigVersion.Value = 6;
            Logger.LogInfo("[config] migrated to v6: blueprint keys -> [ and ] " +
                           "(J is AbandonVehicle, K is Dissolve in the game bindings).");
        }

        try
        {
            var harmony = new Harmony(PluginGuid);
            Ticks.Apply(harmony);
            SpawnMenuUi.Initialize();

            Logger.LogInfo(
                $"{PluginName} {PluginVersion} loaded. " +
                $"probe={CfgProbeKey.Value}, spawn={CfgSpawnKey.Value}/{CfgSpawnFallbackKey.Value}, " +
                $"next={CfgNextKey.Value}, prev={CfgPrevKey.Value}. " +
                $"Key names resolve to: {Hotkey.Describe(CfgProbeKey.Value)}, " +
                $"{Hotkey.Describe(CfgSpawnKey.Value)}, {Hotkey.Describe(CfgSpawnFallbackKey.Value)}.");
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to apply patches: {e}");
        }
    }

    // --- per-frame tick -----------------------------------------------------------------
    //
    // GameController.SceneUpdate runs every frame while a battle scene is loaded and is a
    // large method, so it is the safest Harmony target. GameMode.GameModeUpdate is only a
    // three-instruction tail-jump to the virtual ModeUpdate; it is patched as a fallback in
    // case SceneUpdate is not hit (e.g. a mode that swaps the scene root).

    internal static class Ticks
    {
        private static int _lastFrame = -1;
        private static float _nextHeartbeat;
        private static float _lastHeartbeatAt;
        private static int _lastHeartbeatFrame;

        internal static void Apply(Harmony harmony)
        {
            // Only one tick patch. GameMode.GameModeUpdate is a three-instruction tail-jump
            // to the virtual ModeUpdate; detouring that stub from a second patch is extra
            // risk at state transitions (the sandbox switches from design to play mode), so
            // it is used only if SceneUpdate cannot be patched.
            var patched = PatchOne(harmony, typeof(GameController), "SceneUpdate",
                nameof(SceneUpdatePrefix), "GameController.SceneUpdate (per-frame tick)");

            PatchOne(harmony, typeof(GameController), "Release",
                nameof(ReleasePrefix), "GameController.Release (cleanup before leaving the match)");

            if (!patched)
                PatchOne(harmony, typeof(GameMode), "GameModeUpdate",
                    nameof(GameModeUpdatePrefix), "GameMode.GameModeUpdate (fallback tick)");
        }

        private static bool PatchOne(Harmony harmony, Type type, string targetName,
            string prefixName, string label)
        {
            try
            {
                var target = AccessTools.Method(type, targetName);
                var prefix = AccessTools.Method(typeof(Ticks), prefixName);
                if (target == null || prefix == null)
                {
                    Logger.LogWarning($"{label} not found.");
                    return false;
                }

                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Logger.LogInfo($"Patched {label}.");
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to patch {label}: {e}");
                return false;
            }
        }

        private static void SceneUpdatePrefix() => Tick();

        private static void ReleasePrefix()
        {
            try
            {
                SpawnTest.CleanupForSceneExit();
            }
            catch (Exception e)
            {
                Logger.LogError($"cleanup on match exit failed: {e}");
            }
        }

        private static void KeyLog(string key, string action)
        {
            if (!SpawnMenuPlugin.CfgDebug.Value) return;
            Logger.LogInfo($"[key] {key} -> {action}");
        }

        private static void GameModeUpdatePrefix() => Tick();

        private static void Tick()
        {
            var frame = Time.frameCount;
            if (frame == _lastFrame) return;
            _lastFrame = frame;

            if (!SpawnMenuPlugin.CfgEnabled.Value) return;

            if (SpawnMenuPlugin.CfgHeartbeat.Value)
            {
                var now = Time.realtimeSinceStartup;
                if (now >= _nextHeartbeat)
                {
                    var dt = now - _lastHeartbeatAt;
                    var df = frame - _lastHeartbeatFrame;
                    var fps = dt > 0.1f ? df / dt : 0f;

                    _nextHeartbeat = now + 10f;
                    _lastHeartbeatAt = now;
                    _lastHeartbeatFrame = frame;

                    Logger.LogInfo(
                        $"[tick] alive frame={frame} realtime={now:0.0} fps={fps:0.0}");
                }
            }

            try
            {
                if (Hotkey.WasPressed(SpawnMenuPlugin.CfgUiKey.Value))
                {
                    KeyLog(SpawnMenuPlugin.CfgUiKey.Value, "toggle UI");
                    SpawnMenuUi.Toggle();
                }

                if (Hotkey.WasPressed(SpawnMenuPlugin.CfgProbeKey.Value))
                {
                    KeyLog(SpawnMenuPlugin.CfgProbeKey.Value, "probe");
                    Probe.Dump("manual");
                }

                if (Hotkey.WasPressed(SpawnMenuPlugin.CfgPrevKey.Value))
                {
                    KeyLog(SpawnMenuPlugin.CfgPrevKey.Value, "previous blueprint");
                    BlueprintLibrary.Cycle(-1);
                }

                if (Hotkey.WasPressed(SpawnMenuPlugin.CfgNextKey.Value))
                {
                    KeyLog(SpawnMenuPlugin.CfgNextKey.Value, "next blueprint");
                    BlueprintLibrary.Cycle(1);
                }

                var spawnPressed = Hotkey.WasPressed(SpawnMenuPlugin.CfgSpawnKey.Value);
                var fallbackPressed = !spawnPressed &&
                                      Hotkey.WasPressed(SpawnMenuPlugin.CfgSpawnFallbackKey.Value);
                if (spawnPressed || fallbackPressed)
                {
                    KeyLog(spawnPressed
                        ? SpawnMenuPlugin.CfgSpawnKey.Value
                        : SpawnMenuPlugin.CfgSpawnFallbackKey.Value, "spawn");
                    SpawnTest.TrySpawn();
                }

                if (SpawnMenuPlugin.CfgAutoProbe.Value)
                    Probe.MaybeAutoDump();

                SpawnTest.Poll();
                SpawnTest.Track();
                SpawnTest.PollPlayerState();
            }
            catch (Exception e)
            {
                Logger.LogError($"tick failed: {e}");
            }
        }
    }

    internal static class Hotkey
    {
        // 0.1.0 called Input.GetKeyDown() for every key on every frame whenever the
        // InputSystem did not report the key. Under Proton that either throws or does a
        // native lookup 3x per frame; the exceptions/allocations are a plausible part of
        // the hang. Cache the enum values and stick to one backend.
        private static readonly System.Collections.Generic.Dictionary<string, Key> KeyCache = new();
        private static readonly System.Collections.Generic.Dictionary<string, KeyCode> LegacyCache = new();
        private static bool _inputSystemBroken;
        private static bool _legacyBroken;

        internal static bool WasPressed(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            if (!_inputSystemBroken)
            {
                try
                {
                    var keyboard = Keyboard.current;
                    if (keyboard != null)
                    {
                        var key = ResolveKey(name);
                        if (key == Key.None) return false;

                        var control = keyboard[key];
                        return control != null && control.wasPressedThisFrame;
                    }
                }
                catch (Exception e)
                {
                    _inputSystemBroken = true;
                    SpawnMenuPlugin.Logger.LogWarning(
                        $"[hotkey] InputSystem unavailable ({e.GetType().Name}); using legacy input.");
                }
            }

            if (_legacyBroken) return false;

            try
            {
                var code = ResolveLegacyKey(name);
                return code != KeyCode.None && Input.GetKeyDown(code);
            }
            catch (Exception e)
            {
                _legacyBroken = true;
                SpawnMenuPlugin.Logger.LogWarning(
                    $"[hotkey] legacy input unavailable ({e.GetType().Name}); hotkeys disabled.");
                return false;
            }
        }

        internal static string Describe(string name)
        {
            var key = ResolveKey(name);
            return key == Key.None ? $"'{name}' -> <unparsed>" : $"'{name}' -> {key}";
        }

        private static Key ResolveKey(string name)
        {
            if (KeyCache.TryGetValue(name, out var cached)) return cached;

            var key = Key.None;
            try
            {
                // Friendly aliases for keys whose InputSystem enum names are long.
                switch (name.Trim())
                {
                    case "[": key = Key.LeftBracket; break;
                    case "]": key = Key.RightBracket; break;
                    default: Enum.TryParse(name, true, out key); break;
                }
            }
            catch
            {
                key = Key.None;
            }

            KeyCache[name] = key;
            return key;
        }

        private static KeyCode ResolveLegacyKey(string name)
        {
            if (LegacyCache.TryGetValue(name, out var cached)) return cached;

            var code = KeyCode.None;
            try
            {
                switch (name.Trim())
                {
                    case "[": code = KeyCode.LeftBracket; break;
                    case "]": code = KeyCode.RightBracket; break;
                    default: Enum.TryParse(name, true, out code); break;
                }
            }
            catch
            {
                code = KeyCode.None;
            }

            LegacyCache[name] = code;
            return code;
        }
    }
}
