using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static void MenuLabelTmpOnEnablePostfix(TMPro.TextMeshProUGUI __instance)
    {
        HandsFullLayoutService.OnTextEnabled(__instance);
        MenuFirstFrameLocalizationService.ApplyTmp(__instance);
        MenuTranslationScan.NotifyTextEnabled(__instance);
    }

    private static void MenuLabelTextOnEnablePostfix(UnityEngine.UI.Text __instance)
    {
        MenuFirstFrameLocalizationService.ApplyText(__instance);
        MenuTranslationScan.NotifyTextEnabled(__instance);
    }

    private static void HudGlobalNotificationPrefix(ref string displayText)
    {
        if (TranslationService.HudDynamicTranslator.TranslateHudNotificationFast(displayText, out var translated))
            displayText = translated;
    }

    private static void LobbyImprovementsConfirmHostButtonPostfix(object[] __args)
    {
        if (__args.Length == 0 || __args[0] is not MenuManager menuManager)
        {
            return;
        }

        ExternalEnglishCompatibilityUiService.TranslateTmpTextKnownNonInput(
            menuManager.tipTextHostSettings,
            "LobbyImprovements.HostingUI.MM_ConfirmHostButton");
    }

    private static void LobbyImprovementsAddTextToChatOnServerPostfix(ref string __0)
    {
        LobbyImprovementsKickMessageCompatibilityService.CorrectMisclassifiedKickMessage(ref __0);
    }

    private static void MoreCompanyCreateCrewCountInputPostfix()
    {
        var crewCountRoot = GameObject.Find("MC_CrewCount");
        ExternalEnglishCompatibilityUiService.TranslateRoot(
            crewCountRoot,
            includeInactive: true,
            "MoreCompany.MenuManagerHost.CreateCrewCountInput");
    }

    [HarmonyPatch(typeof(MenuManager), "OnEnable")]
    [HarmonyPostfix]
    private static void MenuManagerOnEnablePostfix(MenuManager __instance)
    {
        ChatEmojiSpriteService.ApplyToText(__instance?.lobbyNameInputField?.textComponent);
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
        ChatEmojiSpriteService.ApplyToText(__instance?.lobbyNameInputField?.textComponent);
        MenuSceneLocalizationService.ApplyEnabledPanel(__instance, enablePanel, "MenuManager.EnableUIPanel");
    }

    private static void QuickMenuManagerEnableUIPanelPostfix(QuickMenuManager __instance, GameObject enablePanel)
    {
        MenuSceneLocalizationService.ApplyQuickMenuPanel(__instance, enablePanel, "QuickMenuManager.EnableUIPanel");
        PlayerNameDiagnosticService.LogQuickMenu(__instance, $"QuickMenuManager.EnableUIPanel:{enablePanel?.name ?? "<null>"}");
    }

    private static void QuickMenuManagerLeaveGamePostfix(QuickMenuManager __instance)
    {
        MenuSceneLocalizationService.ApplyQuickMenuLeaveGamePanel(__instance.leaveGameConfirmPanel, "QuickMenuManager.LeaveGame");
    }

    private static void MenuManagerEnableLeaderboardDisplayPostfix(MenuManager __instance, bool enable)
    {
        if (enable)
        {
            ChallengeLeaderboardLocalizationService.Apply(__instance, "MenuManager.EnableLeaderboardDisplay");
        }
    }

    private static void MenuManagerSetLeaderboardFilterPostfix(MenuManager __instance)
    {
        ChallengeLeaderboardLocalizationService.Apply(__instance, "MenuManager.SetLeaderboardFilter");
    }

    private static void IngamePlayerSettingsSetSettingsOptionsTextPrefix(
        SettingsOptionType optionType,
        ref string setToText)
    {
        SettingsLocalizationService.LocalizeOptionText(optionType, ref setToText);
    }

    private static void IngamePlayerSettingsDisplayConfirmChangesScreenPostfix(bool visible)
    {
        SettingsLocalizationService.ApplyConfirmChangesPanel(
            visible,
            "IngamePlayerSettings.DisplayConfirmChangesScreen");
    }

    private static void SandSpiderAIStartPostfix(SandSpiderAI __instance)
    {
        SpiderSafeModeLocalizationService.Apply(__instance);
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
        var timing = QuickMenuTimingService.BeginLocalization();
        try
        {
        MenuFirstFrameLocalizationService.ApplyLobbyHeader(__instance);
        ChatEmojiSpriteService.ApplyToQuickMenuLobbyHeader(__instance);
        MenuSceneLocalizationService.ApplyQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
        PlayerNameDiagnosticService.LogQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
        }
        finally { QuickMenuTimingService.EndLocalization(timing); }
    }

    private static void QuickMenuManagerStartPostfix(QuickMenuManager __instance)
    {
        if (PlayerNameSettings.Enabled) PlayerNameSourceService.ApplyAll(__instance);
        MenuSceneLocalizationService.ApplyQuickMenuStartup(__instance, "QuickMenuManager.Start");
    }

    private static void QuickMenuManagerKickUserFromServerPostfix(QuickMenuManager __instance, int playerObjId)
    {
        MenuSceneLocalizationService.ApplyKickConfirmationPanel(__instance, playerObjId, "QuickMenuManager.KickUserFromServer");
    }

    private static void LobbyImprovementsUpdatePlayerListHeaderPostfix(QuickMenuManager __instance)
    {
        MenuFirstFrameLocalizationService.ApplyLobbyHeader(__instance);
        ChatEmojiSpriteService.ApplyToQuickMenuLobbyHeader(__instance);
        PlayerNameDiagnosticService.LogQuickMenu(__instance, "LobbyImprovements.UpdatePlayerListHeader");
    }

    private static void LobbyImprovementsParsePlayerNamePostfix(string? playerName, int playerClientId, ref string __result)
    {
        __result = PlayerNamePolicy.Lan(playerName, playerClientId);
        PlayerNameSourceService.CaptureLan(playerClientId, __result);
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
