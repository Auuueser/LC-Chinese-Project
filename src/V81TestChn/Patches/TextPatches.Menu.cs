using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    [HarmonyPatch(typeof(MenuManager), "OnEnable")]
    [HarmonyPostfix]
    private static void MenuManagerOnEnablePostfix(MenuManager __instance)
    {
        MenuSceneLocalizationService.ApplyMenuManager(__instance, "MenuManager.OnEnable");
    }

    [HarmonyPatch(typeof(MenuManager), "DisplayMenuNotification")]
    [HarmonyPrefix]
    private static void MenuManagerDisplayMenuNotificationPrefix(ref string notificationText, ref string buttonText)
    {
        MenuSceneLocalizationService.ApplyMenuNotification(ref notificationText, ref buttonText);
    }

    private static void MenuManagerEnableUIPanelPostfix(MenuManager __instance, GameObject enablePanel)
    {
        MenuSceneLocalizationService.ApplyEnabledPanel(__instance, enablePanel, "MenuManager.EnableUIPanel");
    }

    private static void QuickMenuManagerEnableUIPanelPostfix(QuickMenuManager __instance, GameObject enablePanel)
    {
        MenuSceneLocalizationService.ApplyQuickMenuPanel(__instance, enablePanel, "QuickMenuManager.EnableUIPanel");
    }

    private static void QuickMenuManagerLeaveGamePostfix(QuickMenuManager __instance)
    {
        MenuSceneLocalizationService.ApplyQuickMenuLeaveGamePanel(__instance.leaveGameConfirmPanel, "QuickMenuManager.LeaveGame");
    }

    private static void DeleteFileButtonSetFileToDeletePostfix(DeleteFileButton __instance)
    {
        MenuSceneLocalizationService.ApplyDeleteFilePrompt(__instance, "DeleteFileButton.SetFileToDelete");
    }

    [HarmonyPatch(typeof(SaveFileUISlot), "OnEnable")]
    [HarmonyPostfix]
    private static void SaveFileUISlotOnEnablePostfix(SaveFileUISlot __instance)
    {
        MenuSceneLocalizationService.ApplySaveFileSlot(__instance, "SaveFileUISlot.OnEnable");
    }

    [HarmonyPatch(typeof(PreInitSceneScript), "Start")]
    [HarmonyPostfix]
    private static void PreInitSceneScriptStartPostfix(PreInitSceneScript __instance)
    {
        MenuSceneLocalizationService.ApplyPreInit(__instance, "PreInitSceneScript.Start");
    }

    [HarmonyPatch(typeof(PreInitSceneScript), "SetLaunchPanelsEnabled")]
    [HarmonyPostfix]
    private static void PreInitSceneScriptSetLaunchPanelsEnabledPostfix(PreInitSceneScript __instance)
    {
        MenuSceneLocalizationService.ApplyPreInit(__instance, "PreInitSceneScript.SetLaunchPanelsEnabled");
    }

    [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
    [HarmonyPostfix]
    private static void QuickMenuManagerOpenPostfix(QuickMenuManager __instance)
    {
        MenuSceneLocalizationService.ApplyQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
    }

    private static void StartOfRoundAutoSaveShipDataPrefix()
    {
        MenuSceneLocalizationService.ApplyAutosaveText("StartOfRound.AutoSaveShipData.autosave");
    }

    private static void StartOfRoundSetShipReadyToLandPrefix(StartOfRound __instance)
    {
        RoundTransitionTextThrottle.EnterSetShipReadyToLand(__instance);
    }

    private static void StartOfRoundSetShipReadyToLandPostfix()
    {
        RoundTransitionTextThrottle.ExitSetShipReadyToLand();
        TargetedUiTranslator.FlushHudChatOutputDeferredByRoundTransition(
            HUDManager.Instance,
            "StartOfRound.SetShipReadyToLand.transition-flush");
    }

    private static void GameNetworkManagerSaveGamePrefix()
    {
        MenuSceneLocalizationService.ApplyAutosaveText("GameNetworkManager.SaveGame.autosave");
    }
}
