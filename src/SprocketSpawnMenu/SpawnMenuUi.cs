using System;
using System.IO;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace SprocketSpawnMenu;

/// <summary>
/// Minimal IMGUI overlay for blueprint selection, injected with ClassInjector.
///
/// This game's IL2CPP build has stripped nearly all IMGUI: GUILayout.Window, BeginArea,
/// Button, ScrollView etc. all throw "Method unstripping failed". dump.cs only lists
/// GUI.Box(Rect, string) and GUI.Label(Rect, string), so the panel is drawn with just those
/// two. Selection stays on the keyboard (F8/F6), spawning on Z.
/// </summary>
public sealed class SpawnMenuUi : MonoBehaviour
{
    public SpawnMenuUi(IntPtr pointer) : base(pointer) { }

    private static bool _visible = false;
    private static bool _created;

    internal static void Initialize()
    {
        if (_created) return;
        _created = true;

        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<SpawnMenuUi>();

            var go = new GameObject("Sprocket.SpawnMenu.UI");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SpawnMenuUi>();

            SpawnMenuPlugin.Logger.LogInfo("[ui] IMGUI overlay created (B toggles).");
        }
        catch (Exception e)
        {
            SpawnMenuPlugin.Logger.LogError($"[ui] creation failed: {e}");
        }
    }

    internal static void Toggle()
    {
        _visible = !_visible;
        SpawnMenuPlugin.Logger.LogInfo($"[ui] {(_visible ? "shown" : "hidden")}.");
    }

    internal static void Hide()
    {
        if (!_visible) return;
        _visible = false;
        SpawnMenuPlugin.Logger.LogInfo("[ui] hidden (spawn menu is sandbox-only).");
    }

    private void OnGUI()
    {
        if (!SandboxGate.Allowed)
        {
            _visible = false;
            return;
        }

        if (!_visible) return;

        try
        {
            BlueprintLibrary.EnsureScanned();

            var count = BlueprintLibrary.Count;
            var index = BlueprintLibrary.Index;
            var panel = new Rect(40f, 40f, 520f, 540f);

            // Box() and Label(Rect, string) are the only IMGUI calls that survived stripping.
            GUI.Box(panel, "Spawn Menu  (B)");

            var x = panel.x + 12f;
            var width = panel.width - 24f;
            var y = panel.y + 28f;

            GUI.Label(new Rect(x, y, width, 22f), $"Selected {index + 1}/{count}");
            y += 24f;
            GUI.Label(new Rect(x, y, width, 22f), BlueprintLibrary.DescribeCurrent());
            y += 30f;

            if (count > 0)
            {
                var from = Math.Max(0, index - 8);
                var to = Math.Min(count - 1, index + 8);

                for (var i = from; i <= to; i++)
                {
                    var name = Path.GetFileNameWithoutExtension(BlueprintLibrary.All[i]);
                    var text = (i == index ? "> " : "   ") + $"[{i + 1}] {name}";

                    var oldColour = GUI.color;
                    if (i == index) GUI.color = Color.yellow;
                    GUI.Label(new Rect(x, y, width, 20f), text);
                    GUI.color = oldColour;

                    y += 20f;
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, width, 20f), "no blueprints found");
                y += 20f;
            }

            y += 8f;
            GUI.Label(new Rect(x, y, width, 20f),
                "[ / ] = select    Z = spawn at aim    F9 = probe    B = hide");
        }
        catch (Exception e)
        {
            SpawnMenuPlugin.Logger.LogError($"[ui] OnGUI failed: {e}");
            _visible = false;
        }
    }
}
