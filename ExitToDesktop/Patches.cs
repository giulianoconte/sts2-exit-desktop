using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.addons.mega_text;

namespace ExitToDesktop;

[HarmonyPatch(typeof(NPauseMenu), "_Ready")]
public static class PauseMenuPatch
{
    static void Postfix(NPauseMenu __instance)
    {
        MainFile.Logger.Info("Injecting 'Save and Exit to Desktop' button into pause menu");

        var saveAndQuitButton = ((Node)__instance).GetNodeOrNull<NPauseMenuButton>("%ButtonContainer/SaveAndQuit");
        if (saveAndQuitButton == null)
        {
            MainFile.Logger.Warn("Could not find SaveAndQuit button");
            return;
        }

        var exitButton = (NPauseMenuButton)((Node)saveAndQuitButton).Duplicate();
        ((Node)exitButton).Name = "ExitToDesktop";

        var buttonContainer = ((Node)__instance).GetNodeOrNull<Control>("%ButtonContainer");
        if (buttonContainer == null) return;
        buttonContainer.AddChild((Node)(object)exitButton);

        var imageNode = ((Node)exitButton).GetNodeOrNull<TextureRect>("ButtonImage");
        if (imageNode != null && ((CanvasItem)imageNode).Material is ShaderMaterial sharedMat)
        {
            var ownMat = (ShaderMaterial)sharedMat.Duplicate(true);
            ((CanvasItem)imageNode).Material = ownMat;
            var hsvField = AccessTools.Field(typeof(NPauseMenuButton), "_hsv");
            hsvField?.SetValue(exitButton, ownMat);
        }

        var label = ((Node)exitButton).GetNodeOrNull<MegaLabel>("Label");
        label?.SetTextAutoSize(LocalizedButtonLabel());

        ((CanvasItem)exitButton).Visible =
            RunManager.Instance.NetService.Type != NetGameType.Client;

        // Wire up keyboard/controller focus navigation
        var exitPath = ((Node)exitButton).GetPath();
        var saveAndQuitPath = ((Node)saveAndQuitButton).GetPath();
        ((Control)exitButton).FocusNeighborLeft = exitPath;
        ((Control)exitButton).FocusNeighborRight = exitPath;
        ((Control)exitButton).FocusNeighborTop = saveAndQuitPath;
        ((Control)exitButton).FocusNeighborBottom = exitPath;
        ((Control)saveAndQuitButton).FocusNeighborBottom = exitPath;

        ((GodotObject)exitButton).Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => OnExitToDesktopPressed()),
            0u);

        MainFile.Logger.Info("'Save and Exit to Desktop' button injected");
    }

    private static string LocalizedButtonLabel()
    {
        const string fallback = "Save and Exit to Desktop";
        try
        {
            return LocManager.Instance?.GetTable("gameplay_ui")
                .GetRawText("EXITTODESKTOP.button_label") ?? fallback;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"Falling back to English button label: {e.GetType().Name}: {e.Message}");
            return fallback;
        }
    }

    private static void OnExitToDesktopPressed()
    {
        MainFile.Logger.Info("Save and Exit to Desktop pressed");
        TaskHelper.RunSafely(ConfirmAndExit());
    }

    private static async Task ConfirmAndExit()
    {
        NGenericPopup popup = NGenericPopup.Create()!;
        NModalContainer.Instance.Add((Node)(object)popup!);

        if (await popup.WaitForConfirmation(
                new LocString("main_menu_ui", "QUIT_CONFIRM_POPUP.body"),
                new LocString("main_menu_ui", "QUIT_CONFIRM_POPUP.header"),
                new LocString("main_menu_ui", "GENERIC_POPUP.cancel"),
                new LocString("main_menu_ui", "GENERIC_POPUP.confirm")))
        {
            MainFile.Logger.Info("Exit confirmed — waiting for save and quitting");

            Task? saveTask = SaveManager.Instance.CurrentRunSaveTask;
            if (saveTask != null)
            {
                MainFile.Logger.Info("Waiting for in-progress run save...");
                await saveTask;
            }

            NGame.Instance?.Quit();
        }
    }
}
