using System;
using Sprocket;
using Sprocket.GameControl;
using UnityEngine;

namespace SprocketSpawnMenu;

/// <summary>
/// Spawn menu is sandbox-only. GameMode.Mode is the set of modes a scenario supports
/// (the sandbox reports Sandbox|Mission), so the active mode is read from
/// GameController.setupContext.Mode instead. No GameController (main menu) = denied.
/// </summary>
internal static class SandboxGate
{
    private static float _nextCheck;
    private static bool _allowed;
    private static bool _warned;
    private static string _lastState;

    internal static bool Allowed
    {
        get
        {
            var now = Time.realtimeSinceStartup;
            if (now < _nextCheck) return _allowed;

            _nextCheck = now + 1f;
            _allowed = Compute();
            return _allowed;
        }
    }

    private static bool Compute()
    {
        try
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(
                FindObjectsSortMode.None);

            if (controllers == null || controllers.Length == 0)
            {
                LogState("no GameController (main menu / not in a battle)");
                return false;
            }

            foreach (var controller in controllers)
            {
                if (!Interop.TryGetValue(typeof(GameController), controller,
                        "setupContext", out var raw) || !(raw is GameSetupContext setup))
                    continue;

                var allowed = setup.Mode == SupportedGameModes.Sandbox;
                LogState(setup.Mode.ToString());
                return allowed;
            }
        }
        catch (Exception e)
        {
            if (!_warned)
            {
                _warned = true;
                SpawnMenuPlugin.Logger.LogWarning(
                    $"[gate] sandbox check failed: {e.GetType().Name}: {e.Message}");
            }
        }

        return false;
    }

    private static void LogState(string state)
    {
        if (_lastState == state) return;
        _lastState = state;
        SpawnMenuPlugin.Logger.LogInfo($"[gate] mode={state}; spawn menu=" +
                                       $"{(state == "Sandbox" ? "enabled" : "disabled")}");
    }
}
