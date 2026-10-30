using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using TWG.Shared.Adventure;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CP2MouseControls;

[BepInPlugin("cp2.mousecontrols", "Chicken Police 2 - Mouse Controls", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> CloseUpOnMiddleMouse;
    internal static ConfigEntry<bool> BackOnRightMouse;

    public override void Load()
    {
        Logger = Log;
        CloseUpOnMiddleMouse = Config.Bind("General", "CloseUpOnMiddleMouse", true, "Move the close-up (zoom) from the right mouse button to the middle mouse button.");
        BackOnRightMouse = Config.Bind("General", "BackOnRightMouse", true, "Right mouse button goes back from the current scene, like the on-screen back arrow.");
        AddComponent<MouseControlsBehaviour>();
        Log.LogInfo("Mouse Controls loaded");
    }
}

public class MouseControlsBehaviour : MonoBehaviour
{
    public MouseControlsBehaviour(IntPtr ptr) : base(ptr) { }

    private const string ZoomActionName = "Player/Zoom";
    private const string RightButtonPath = "<Mouse>/rightButton";
    private const string MiddleButtonPath = "<Mouse>/middleButton";
    private const float RebindCheckInterval = 1f;

    private float _nextRebindCheck;

    private void Update()
    {
        try
        {
            if (Plugin.CloseUpOnMiddleMouse.Value && Time.unscaledTime >= _nextRebindCheck)
            {
                _nextRebindCheck = Time.unscaledTime + RebindCheckInterval;
                MoveZoomToMiddleMouse();
            }

            var mouse = Mouse.current;
            if (Plugin.BackOnRightMouse.Value && mouse != null && mouse.rightButton.wasPressedThisFrame)
                TryGoBack();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Mouse controls failed: {e}");
        }
    }

    // Re-checked periodically because the game can reload its input assets / binding overrides.
    private static void MoveZoomToMiddleMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            var zoom = asset.FindAction(ZoomActionName, false);
            if (zoom == null || zoom.GetBindingIndexForControl(mouse.rightButton) < 0) continue;

            zoom.ApplyBindingOverride(MiddleButtonPath, null, RightButtonPath);
            Plugin.Logger.LogInfo($"Close-up moved to middle mouse ({asset.name})");
        }
    }

    // Same as clicking the on-screen back arrow in a location view.
    private static void TryGoBack()
    {
        var menu = FindObjectOfType<LocationMenu>();
        if (menu == null || !menu.CurrentlyActive) return;

        var dialogue = FindObjectOfType<DialogueManager>();
        if (dialogue != null && dialogue.CurrentDialogue != null) return;

        // RMB already cancels a held inventory item; don't also leave the scene.
        var itemUse = FindObjectOfType<ItemUseMenu>();
        if (itemUse != null && itemUse.IsOpen) return;

        var exit = menu.exitNavigationButton;
        if (exit == null || !exit.activeInHierarchy) return;

        var button = exit.GetComponentInChildren<Button>();
        if (button != null)
        {
            if (!button.IsInteractable()) return;
            button.onClick.Invoke();
        }
        else
        {
            menu.OnExitButtonClicked();
        }
        Plugin.Logger.LogInfo("Went back (right mouse)");
    }
}
