using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TWG.Shared;
using TWG.Shared.Adventure;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CP2FastMode;

[BepInPlugin("cp2.fastmode", "Chicken Police 2 - Fast Mode", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<Key> SkipKey;

    public override void Load()
    {
        Logger = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Skip the current dialogue when SkipKey is pressed; hold it to skip cutscenes.");
        SkipKey = Config.Bind("General", "SkipKey", Key.Escape, "Key that skips dialogue / holds to skip cutscenes (Unity InputSystem Key name, e.g. Escape, Space, Tab).");
        AddComponent<FastModeBehaviour>();
        Log.LogInfo("Fast Mode loaded");
    }
}

public class FastModeBehaviour : MonoBehaviour
{
    public FastModeBehaviour(IntPtr ptr) : base(ptr) { }

    // Cutscene skipper we started a hold-skip on, so the key release cancels the same one.
    private CutsceneSkipper _heldSkipper;

    private void Update()
    {
        if (!Plugin.Enabled.Value) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        var key = keyboard[Plugin.SkipKey.Value];

        try
        {
            if (key.wasPressedThisFrame)
            {
                if (!TryStartCutsceneSkip())
                    TrySkip();
            }
            else if (key.wasReleasedThisFrame)
            {
                CancelCutsceneSkip();
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Skip failed: {e}");
        }
    }

    // Cutscenes use a hold-to-skip widget; drive it exactly like holding the mouse button does.
    private bool TryStartCutsceneSkip()
    {
        var skipper = FindObjectOfType<CutsceneSkipper>();
        if (skipper == null || skipper.skipped) return false;
        if (skipper.cutscenePlayer != null && !skipper.cutscenePlayer.IsPlaying) return false;

        skipper.StartSkip();
        _heldSkipper = skipper;
        return true;
    }

    private void CancelCutsceneSkip()
    {
        var skipper = _heldSkipper;
        _heldSkipper = null;
        if (skipper == null || skipper.skipped) return;

        // Don't interrupt the hold if the game's own skip button is still down.
        if (skipper.skipAction != null && skipper.skipAction.IsPressed()) return;

        skipper.CancelSkip();
    }

    private static void TrySkip()
    {
        var manager = FindObjectOfType<DialogueManager>();
        if (manager == null || manager.CurrentDialogue == null) return;

        // Same path as the in-game "skip dialogue" button when the dialogue UI is up.
        var menu = FindObjectOfType<DialogueMenu>();
        if (menu != null)
        {
            menu.OnSkipDialogueButtonClicked();
            Plugin.Logger.LogInfo("Dialogue skipped (menu)");
            return;
        }

        manager.SkipDialog();
        Plugin.Logger.LogInfo("Dialogue skipped (manager)");
    }
}
