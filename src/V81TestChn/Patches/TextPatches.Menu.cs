using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static QuickMenuManager? _cachedPlayerNameQuickMenuManager;

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
        ChatEmojiSpriteService.ApplyToQuickMenuLobbyHeader(__instance);
        MenuSceneLocalizationService.ApplyQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
        PlayerNameDiagnosticService.LogQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
    }

    private static void LobbyImprovementsUpdatePlayerListHeaderPostfix(QuickMenuManager __instance)
    {
        ChatEmojiSpriteService.ApplyToQuickMenuLobbyHeader(__instance);
        PlayerNameDiagnosticService.LogQuickMenu(__instance, "LobbyImprovements.UpdatePlayerListHeader");
    }

    private static void LobbyImprovementsAddUserToPlayerListPostfix(object __instance, object[] __args)
    {
        if (__args.Length >= 2 && __args[1] is int playerObjectId)
        {
            RestoreLobbyImprovementsPlayerNameFromRadar(playerObjectId);
        }

        PlayerNameDiagnosticService.LogLobbyImprovementsPlayerList(
            __instance,
            __args,
            "LobbyImprovements.AddUserToPlayerList");
    }

    private static void RestoreLobbyImprovementsPlayerNameFromRadar(int playerObjectId)
    {
        var round = StartOfRound.Instance;
        var players = round?.allPlayerScripts;
        var radarTargets = round?.mapScreen?.radarTargets;
        if (players == null ||
            radarTargets == null ||
            playerObjectId < 0 ||
            playerObjectId >= players.Length ||
            playerObjectId >= radarTargets.Count)
        {
            return;
        }

        var player = players[playerObjectId];
        var radarName = radarTargets[playerObjectId]?.name;
        if (player == null ||
            string.IsNullOrWhiteSpace(radarName) ||
            radarName.StartsWith("Player #", System.StringComparison.Ordinal) ||
            !HasSyntheticNumericSuffix(player.playerUsername, radarName))
        {
            return;
        }

        player.playerUsername = radarName;
        if (player.usernameBillboardText != null)
        {
            player.usernameBillboardText.text = radarName;
        }

        var quickMenu = ResolvePlayerNameQuickMenuManager();
        var playerListSlots = quickMenu?.playerListSlots;
        if (playerListSlots != null && playerObjectId < playerListSlots.Length)
        {
            var usernameHeader = playerListSlots[playerObjectId]?.usernameHeader;
            if (usernameHeader != null)
            {
                usernameHeader.text = radarName;
            }
        }
    }

    private static QuickMenuManager? ResolvePlayerNameQuickMenuManager()
    {
        var cached = _cachedPlayerNameQuickMenuManager;
        if (cached != null)
        {
            return cached;
        }

        cached = UnityEngine.Object.FindFirstObjectByType<QuickMenuManager>();
        _cachedPlayerNameQuickMenuManager = cached;
        return cached;
    }

    private static bool HasSyntheticNumericSuffix(string? displayedName, string authoritativeName)
    {
        if (string.IsNullOrEmpty(displayedName) ||
            displayedName.Length <= authoritativeName.Length ||
            !displayedName.StartsWith(authoritativeName, System.StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = authoritativeName.Length; index < displayedName.Length; index++)
        {
            if (!char.IsDigit(displayedName[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void LobbyImprovementsParsePlayerNamePostfix(
        string? playerName,
        int playerClientId,
        ref string __result)
    {
        // Rebuild from the original LAN name because LobbyImprovements calls
        // the vanilla letter-only sanitizer before appending its synthetic 0.
        // Looking only at the result cannot distinguish "小杰0" from "小杰".
        var sanitized = SanitizePlayerNamePreservingDigits(playerName);
        if (sanitized.Length == 0)
        {
            __result = $"Player #{playerClientId}";
            return;
        }

        __result = sanitized.Length > 32
            ? sanitized.Substring(0, 32)
            : sanitized;
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
