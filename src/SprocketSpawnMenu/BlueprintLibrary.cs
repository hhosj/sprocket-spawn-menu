using System;
using System.Collections.Generic;
using System.IO;
using Sprocket;
using Sprocket.Vehicles.Serialization;
using Sprocket.VehicleDesigner;

namespace SprocketSpawnMenu;

/// <summary>
/// All vehicle blueprints the game knows about, in one ordered list, with a moving cursor.
/// F9 prints the whole list with indices, F8/F6 move the cursor, F10 spawns the current entry.
/// No UI: the list and the current selection go to the log.
/// </summary>
internal static class BlueprintLibrary
{
    private static readonly List<string> Paths = new();
    private static int _index;
    private static bool _scanned;

    internal static int Count => Paths.Count;

    internal static IReadOnlyList<string> All => Paths;

    internal static int Index => _index;

    internal static string Current => Paths.Count == 0 ? null : Paths[_index];

    internal static void EnsureScanned()
    {
        if (_scanned) return;
        Rescan();
    }

    internal static void Rescan()
    {
        var previous = Paths.Count == 0 ? null : Paths[_index];
        Paths.Clear();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameDataDirectory dir in Enum.GetValues(typeof(GameDataDirectory)))
        {
            try
            {
                var directory = VehicleBlueprintFiler.GetVehicleBlueprintDirectory(dir);
                var all = VehicleBlueprintFiler.GetAllVehicleBlueprints(directory);
                if (all == null) continue;

                foreach (var path in all)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    if (seen.Add(path)) Paths.Add(path);
                }
            }
            catch (Exception e)
            {
                LogWarning($"scanning {dir} failed: {e.Message}");
            }
        }

        Paths.Sort(StringComparer.OrdinalIgnoreCase);
        _scanned = true;

        if (previous != null)
        {
            var kept = Paths.FindIndex(p => string.Equals(p, previous, StringComparison.OrdinalIgnoreCase));
            if (kept >= 0)
            {
                _index = kept;
                Log($"blueprint list reloaded: {Paths.Count} entries, current = {DescribeCurrent()}");
                return;
            }
        }

        ChooseInitialSelection();
        Log($"blueprint list reloaded: {Paths.Count} entries, current = {DescribeCurrent()}");
    }

    internal static void Select(int index)
    {
        if (Paths.Count == 0) return;

        _index = ((index % Paths.Count) + Paths.Count) % Paths.Count;
        Log($"selected {_index + 1}/{Paths.Count}: {DescribeCurrent()}");
    }

    internal static void Cycle(int delta)
    {
        EnsureScanned();
        if (Paths.Count == 0)
        {
            LogWarning("no blueprints found - nothing to cycle through.");
            return;
        }

        _index = ((_index + delta) % Paths.Count + Paths.Count) % Paths.Count;
        Log($"selected {_index + 1}/{Paths.Count}: {DescribeCurrent()}");
    }

    internal static void LogAll()
    {
        EnsureScanned();
        if (Paths.Count == 0)
        {
            LogWarning("no blueprints found in any game data directory.");
            return;
        }

        Log($"all blueprints ({Paths.Count}):");
        for (var i = 0; i < Paths.Count; i++)
        {
            var marker = i == _index ? "  <== current" : "";
            Log($"  [{i + 1}] {Paths[i]}{marker}");
        }
    }

    private static void ChooseInitialSelection()
    {
        _index = 0;
        if (Paths.Count == 0) return;

        var configured = SpawnMenuPlugin.CfgBlueprintName.Value?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            var match = FindMatch(configured);
            if (match >= 0)
            {
                _index = match;
                return;
            }

            LogWarning($"configured BlueprintName '{configured}' does not match any blueprint; " +
                       "falling back to the last saved vehicle.");
        }

        if (!SpawnMenuPlugin.CfgUseLastSaved.Value) return;

        try
        {
            if (!LastSavedVehicleBlueprint.TryGet(out var last) || string.IsNullOrEmpty(last)) return;

            for (var i = 0; i < Paths.Count; i++)
            {
                if (string.Equals(Paths[i], last, StringComparison.OrdinalIgnoreCase))
                {
                    _index = i;
                    return;
                }
            }
        }
        catch (Exception e)
        {
            LogWarning($"LastSavedVehicleBlueprint.TryGet failed: {e.Message}");
        }
    }

    private static int FindMatch(string configured)
    {
        if (configured.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < Paths.Count; i++)
                if (string.Equals(Paths[i], configured, StringComparison.OrdinalIgnoreCase))
                    return i;
        }

        var bare = Path.GetFileNameWithoutExtension(configured);
        for (var i = 0; i < Paths.Count; i++)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(Paths[i]), bare,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < Paths.Count; i++)
        {
            if (Paths[i].IndexOf(configured, StringComparison.OrdinalIgnoreCase) >= 0)
                return i;
        }

        return -1;
    }

    internal static string DescribeCurrent()
    {
        if (Paths.Count == 0) return "<none>";
        return $"{Path.GetFileNameWithoutExtension(Paths[_index])}  ({Paths[_index]})";
    }

    private static void Log(string message) => SpawnMenuPlugin.Logger.LogInfo($"[select] {message}");

    private static void LogWarning(string message) => SpawnMenuPlugin.Logger.LogWarning($"[select] {message}");
}
