using HarmonyLib;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static readonly Dictionary<string, Type?> OptionalPatchTypeCache = new(StringComparer.Ordinal);

    private static int InstallTextPatches(Harmony harmony)
    {
        var patched = 0;

        PatchPostfix(harmony, typeof(MenuManager), "OnEnable", nameof(MenuManagerOnEnablePostfix), ref patched);
        PatchPostfix(harmony, typeof(MenuManager), "EnableUIPanel", nameof(MenuManagerEnableUIPanelPostfix), ref patched);
        PatchPrefix(harmony, typeof(MenuManager), "DisplayMenuNotification", nameof(MenuManagerDisplayMenuNotificationPrefix), ref patched);
        PatchPostfix(harmony, typeof(DeleteFileButton), "SetFileToDelete", nameof(DeleteFileButtonSetFileToDeletePostfix), ref patched);
        PatchPostfix(harmony, typeof(SaveFileUISlot), "OnEnable", nameof(SaveFileUISlotOnEnablePostfix), ref patched);
        InstallExternalCompatibilityPatches(harmony, ref patched);

        PatchPostfix(harmony, typeof(PreInitSceneScript), "Start", nameof(PreInitSceneScriptStartPostfix), ref patched);
        PatchPostfix(harmony, typeof(PreInitSceneScript), "SetLaunchPanelsEnabled", nameof(PreInitSceneScriptSetLaunchPanelsEnabledPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "OpenQuickMenu", nameof(QuickMenuManagerOpenPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "EnableUIPanel", nameof(QuickMenuManagerEnableUIPanelPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "LeaveGame", nameof(QuickMenuManagerLeaveGamePostfix), ref patched);
        PatchPrefix(harmony, typeof(IngamePlayerSettings), "SetSettingsOptionsText", nameof(IngamePlayerSettingsSetSettingsOptionsTextPrefix), ref patched);
        PatchPostfix(harmony, typeof(IngamePlayerSettings), "DisplayConfirmChangesScreen", nameof(IngamePlayerSettingsDisplayConfirmChangesScreenPostfix), ref patched);
        PatchPostfix(harmony, typeof(SandSpiderAI), "Start", nameof(SandSpiderAIStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "AutoSaveShipData", nameof(StartOfRoundAutoSaveShipDataPrefix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "SetShipReadyToLand", nameof(StartOfRoundSetShipReadyToLandPrefix), ref patched, Priority.First);
        PatchPostfix(harmony, typeof(StartOfRound), "SetShipReadyToLand", nameof(StartOfRoundSetShipReadyToLandPostfix), ref patched, Priority.Last);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangeLevel", nameof(StartOfRoundChangeLevelPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPostfix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SwitchMapMonitorPurpose", nameof(StartOfRoundSwitchMapMonitorPurposePostfix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SceneManager_OnLoadComplete1", nameof(StartOfRoundSceneManagerOnLoadCompletePostfix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "FirePlayersAfterDeadlineClientRpc", nameof(StartOfRoundFirePlayersAfterDeadlineClientRpcPostfix), ref patched);
        PatchPrefix(harmony, typeof(GameNetworkManager), "SaveGame", nameof(GameNetworkManagerSaveGamePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "Start", nameof(HudManagerStartPostfix), ref patched);
        if (AutomaticTranslationService.NeedsMainThreadPump)
        {
            PatchPostfix(harmony, typeof(HUDManager), "Update", nameof(HudManagerAutomaticTranslationUpdatePostfix), ref patched);
        }

        PatchPrefix(harmony, typeof(HUDManager), "BeginDisplayAd", nameof(HudManagerBeginDisplayAdPrefix), ref patched, Priority.First);
        PatchPostfix(harmony, typeof(HUDManager), "BeginDisplayAd", nameof(HudManagerBeginDisplayAdPostfix), ref patched, Priority.Last);
        PatchPostfix(harmony, typeof(HUDManager), "MeteorShowerWarningHUD", nameof(HudManagerMeteorShowerWarningHudPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "RadiationWarningHUD", nameof(HudManagerRadiationWarningHudPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "DisplaySignalTranslatorMessage", nameof(HudManagerDisplaySignalTranslatorMessagePrefix), ref patched, Priority.First);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateScanNodes", nameof(HudManagerUpdateScanNodesPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayCreditsEarning", nameof(HudManagerDisplayCreditsEarningPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayNewScrapFound", nameof(HudManagerDisplayNewScrapFoundPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayNewDeadline", nameof(HudManagerDisplayNewDeadlinePostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayDaysLeft", nameof(HudManagerDisplayDaysLeftPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "SetShipLeaveEarlyVotesText", nameof(HudManagerSetShipLeaveEarlyVotesTextPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ReadDialogue", nameof(HudManagerReadDialoguePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ReadDialogue", nameof(HudManagerReadDialoguePostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateBoxesSpectateUI", nameof(HudManagerUpdateBoxesSpectateUiPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "SetSpectatingTextToPlayer", nameof(HudManagerSetSpectatingTextToPlayerPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "FillEndGameStats", nameof(HudManagerFillEndGameStatsPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "FillEndGameStats", nameof(HudManagerFillEndGameStatsPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "SetPlayerLevel", nameof(HudManagerSetPlayerLevelPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ApplyPenalty", nameof(HudManagerApplyPenaltyPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ShowPlayersFiredScreen", nameof(HudManagerShowPlayersFiredScreenPostfix), ref patched);
        var addChatMessage = AccessTools.Method(typeof(HUDManager), "AddChatMessage", new[] { typeof(string), typeof(string), typeof(int), typeof(bool) });
        PatchPrefix(harmony, addChatMessage, nameof(HudManagerAddChatMessagePrefix), ref patched);
        PatchPostfix(harmony, addChatMessage, nameof(HudManagerAddChatMessagePostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "AddTextToChatOnServer", nameof(HudManagerAddTextToChatOnServerPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "AddTextToChatOnServer", nameof(HudManagerAddTextToChatOnServerPostfix), ref patched);
        PatchPostfix(harmony, typeof(ChallengeLeaderboardSlot), "SetSlotValues", nameof(ChallengeLeaderboardSlotSetSlotValuesPostfix), ref patched);
        PatchPostfix(harmony, typeof(LobbySlot), "SetModdedIcon", nameof(LobbySlotSetModdedIconPostfix), ref patched);
        PatchPostfix(harmony, typeof(HangarShipDoor), "Start", nameof(HangarShipDoorStartPostfix), ref patched);

        PatchPrefix(harmony, typeof(HUDManager), "DisplayTip", nameof(HudManagerDisplayTipPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "DisplayStatusEffect", nameof(HudManagerDisplayStatusEffectPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTip", nameof(HudManagerChangeControlTipPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ChangeControlTip", nameof(HudManagerChangeControlTipPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTipMultiple", nameof(HudManagerChangeControlTipMultiplePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ChangeControlTipMultiple", nameof(HudManagerChangeControlTipMultiplePostfix), ref patched);
        PatchPostfix(harmony, typeof(ShipBuildModeManager), "CreateGhostObjectAndHighlight", nameof(ShipBuildModeManagerCreateGhostObjectAndHighlightPostfix), ref patched, Priority.Last);

        PatchPostfix(harmony, typeof(GrabbableObject), "Start", nameof(GrabbableObjectStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(GrabbableObject), "SetControlTipsForItem", nameof(GrabbableObjectSetControlTipsPrefix), ref patched);
        PatchPostfix(harmony, typeof(GrabbableObject), "SetControlTipsForItem", nameof(GrabbableObjectSetControlTipsPostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(StunGrenadeItem), "SetControlTipForGrenade"), nameof(StunGrenadeItemSetControlTipForGrenadePostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(PlayerControllerB), "SetHoverTipAndCurrentInteractTrigger"), nameof(PlayerControllerBSetHoverTipAndCurrentInteractTriggerPostfix), ref patched);
        PatchPostfix(harmony, typeof(VehicleController), "Start", nameof(VehicleControllerStartPostfix), ref patched);
        PatchPostfix(harmony, typeof(VehicleController), "DestroyCar", nameof(VehicleControllerDestroyCarPostfix), ref patched);
        PatchPostfix(harmony, typeof(RoundManager), "GenerateNewLevelClientRpc", nameof(RoundManagerGenerateNewLevelClientRpcPostfix), ref patched);
        PatchPostfix(harmony, typeof(RoundManager), "FinishGeneratingNewLevelClientRpc", nameof(RoundManagerFinishGeneratingNewLevelClientRpcPostfix), ref patched);
        PatchPostfix(harmony, typeof(RoundManager), "SpawnScrapInLevel", nameof(RoundManagerSpawnScrapInLevelPostfix), ref patched);

        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)), nameof(TmpSetTextPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(TMP_InputField), "OnEnable"), nameof(TmpInputFieldOnEnablePostfix), ref patched);
        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_InputField), "Append", new[] { typeof(string) }),
            nameof(TmpInputFieldAppendStringPrefix),
            ref patched,
            Priority.First);
        if (IsGlobalTmpPostSetRepairEnabled)
        {
            PatchPostfix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)), nameof(TmpSetTextPostfix), ref patched);
        }

        if (IsGlobalTmpColorHookEnabled)
        {
            PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.color)), nameof(TmpSetColorPrefix), ref patched);
        }

        PatchPostfix(harmony, AccessTools.Method(typeof(TMP_FontAsset), "Awake"), nameof(TmpFontAssetAwakePostfix), ref patched);
        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(Text), nameof(Text.text)), nameof(UiTextSetTextPrefix), ref patched);
        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text)), nameof(TextMeshSetTextPrefix), ref patched);
        if (CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            PatchPostfix(harmony, AccessTools.PropertySetter(typeof(Text), nameof(Text.text)), nameof(UiTextSetTextPostfix), ref patched);
            PatchPostfix(harmony, AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text)), nameof(TextMeshSetTextPostfix), ref patched);
        }

        PatchPostfix(harmony, typeof(Terminal), "TextPostProcess", nameof(TerminalTextPostProcessPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "LoadNewNode", nameof(TerminalLoadNewNodePostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "OnSubmit", nameof(TerminalOnSubmitPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "ParsePlayerSentence", nameof(TerminalParsePlayerSentencePostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "PlayBroadcastCodeEffect", nameof(TerminalPlayBroadcastCodeEffectPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "loadTextAnimation", nameof(TerminalLoadTextAnimationPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "BeginUsingTerminal", nameof(TerminalBeginUsingPostfix), ref patched);
        PatchPrefix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPrefix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPostfix), ref patched);
        PatchPrefix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPrefix), ref patched);
        PatchPostfix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPostfix), ref patched);

        // Cover whole text-source SetText overloads only. Numeric formatting, char[] buffer,
        // and range-based StringBuilder overloads stay on the global fast path to avoid dynamic counters and input slices.
        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(bool) }),
            nameof(TmpSetTextStringBoolPrefix),
            ref patched);
        if (IsGlobalTmpPostSetRepairEnabled)
        {
            PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(bool) }),
                nameof(TmpSetTextPostfix),
                ref patched);
        }

        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(float) }),
            nameof(TmpSetTextStringFloatPrefix),
            ref patched);
        if (IsGlobalTmpPostSetRepairEnabled)
        {
            PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(float) }),
                nameof(TmpSetTextPostfix),
                ref patched);
        }

        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(StringBuilder) }),
            nameof(TmpSetTextStringBuilderPrefix),
            ref patched);
        if (IsGlobalTmpPostSetRepairEnabled)
        {
            PatchPostfix(
                harmony,
                AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(StringBuilder) }),
                nameof(TmpSetTextPostfix),
                ref patched);
        }

        return patched;
    }

    private static void InstallExternalCompatibilityPatches(Harmony harmony, ref int patched)
    {
        PatchOptionalPostfix(harmony, "LethalConfig.MonoBehaviours.ConfigMenu", "Open", nameof(LethalConfigConfigMenuOpenPostfix), ref patched);
        PatchOptionalPrefix(harmony, "LethalConfig.MonoBehaviours.ConfigMenuNotification", "SetNotificationContent", nameof(LethalConfigNotificationSetContentPrefix), ref patched);
        PatchOptionalPostfix(harmony, "LethalConfig.MonoBehaviours.ConfigMenuNotification", "Open", nameof(LethalConfigNotificationOpenPostfix), ref patched);
        PatchOptionalPostfix(harmony, "OpenBodyCams.Overlay.OverlayManager", "UpdateText", nameof(OpenBodyCamsOverlayUpdateTextPostfix), ref patched);
        PatchOptionalPostfix(harmony, "LCBetterSaves.Plugin", "InitializeBetterSaves", nameof(BetterSavesInitializeBetterSavesPostfix), ref patched);
        PatchOptionalPostfix(harmony, "DeleteFileButton_BetterSaves", "UpdateFileToDelete", nameof(BetterSavesDeleteFileButtonUpdateFileToDeletePostfix), ref patched);
        PatchOptionalPrefix(harmony, "AdvancedFeatures.Endscreen", "Open", nameof(AdvancedFeaturesEndscreenOpenPrefix), ref patched, Priority.First);
        PatchOptionalPostfix(harmony, "AdvancedFeatures.Endscreen", "Open", nameof(AdvancedFeaturesEndscreenOpenPostfix), ref patched);
        InstallTooManyEmotesCompatibilityPatches(harmony, ref patched);
        PatchOptionalPrefix(harmony, "Steamworks.SteamUtils", "ShowGamepadTextInput", nameof(SteamworksShowGamepadTextInputPrefix), ref patched, Priority.First);
    }

    private static void InstallTooManyEmotesCompatibilityPatches(Harmony harmony, ref int patched)
    {
        if (FindLoadedTypeQuiet("TooManyEmotes.Patches.SyncWithEmoteControllerManager") == null)
        {
            return;
        }

        // Patch the shared player update rather than patching another Harmony patch
        // method. The prefix restores the source string needed by TooManyEmotes'
        // cleanup check; the lowest-priority postfix runs last and localizes display.
        PatchPrefix(harmony, typeof(PlayerControllerB), "LateUpdate", nameof(TooManyEmotesPlayerLateUpdatePrefix), ref patched, Priority.First);
        PatchPostfix(harmony, typeof(PlayerControllerB), "LateUpdate", nameof(TooManyEmotesPlayerLateUpdatePostfix), ref patched, Priority.Last);
    }

    private static void PatchPrefix(Harmony harmony, Type targetType, string targetMethod, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        PatchPrefix(harmony, AccessTools.Method(targetType, targetMethod), patchMethod, ref patched, priority);
    }

    private static void PatchPostfix(Harmony harmony, Type targetType, string targetMethod, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        PatchPostfix(harmony, AccessTools.Method(targetType, targetMethod), patchMethod, ref patched, priority);
    }

    private static void PatchPrefix(Harmony harmony, MethodBase? original, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        Patch(harmony, original, prefixName: patchMethod, postfixName: null, ref patched, priority);
    }

    private static void PatchPostfix(Harmony harmony, MethodBase? original, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        Patch(harmony, original, prefixName: null, postfixName: patchMethod, ref patched, priority);
    }

    private static void PatchOptionalPrefix(Harmony harmony, string targetTypeName, string targetMethod, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        PatchOptional(harmony, targetTypeName, targetMethod, patchMethod, prefix: true, ref patched, priority);
    }

    private static void PatchOptionalPostfix(Harmony harmony, string targetTypeName, string targetMethod, string patchMethod, ref int patched, int priority = Priority.Normal)
    {
        PatchOptional(harmony, targetTypeName, targetMethod, patchMethod, prefix: false, ref patched, priority);
    }

    private static void PatchOptional(Harmony harmony, string targetTypeName, string targetMethod, string patchMethod, bool prefix, ref int patched, int priority)
    {
        var targetType = FindLoadedTypeQuiet(targetTypeName);
        if (targetType == null)
        {
            return;
        }

        var original = AccessTools.Method(targetType, targetMethod);
        if (original == null)
        {
            Plugin.Log.LogWarning($"Optional compatibility patch skipped; target not found: {targetTypeName}.{targetMethod}");
            return;
        }

        if (prefix)
        {
            PatchPrefix(harmony, original, patchMethod, ref patched, priority);
            return;
        }

        PatchPostfix(harmony, original, patchMethod, ref patched, priority);
    }

    private static Type? FindLoadedTypeQuiet(string targetTypeName)
    {
        if (OptionalPatchTypeCache.TryGetValue(targetTypeName, out var cachedType))
        {
            return cachedType;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type;
            try
            {
                type = assembly.GetType(targetTypeName, throwOnError: false, ignoreCase: false);
            }
            catch (ReflectionTypeLoadException)
            {
                type = null;
            }

            if (type == null)
            {
                continue;
            }

            OptionalPatchTypeCache[targetTypeName] = type;
            return type;
        }

        OptionalPatchTypeCache[targetTypeName] = null;
        return null;
    }

    private static void Patch(Harmony harmony, MethodBase? original, string? prefixName, string? postfixName, ref int patched, int priority = Priority.Normal)
    {
        var patchName = prefixName ?? postfixName ?? "";
        try
        {
            if (original == null)
            {
                Plugin.Log.LogWarning($"Manual patch skipped; target not found for {patchName}");
                return;
            }

            var patchMethod = AccessTools.Method(typeof(TextPatches), patchName);
            if (patchMethod == null)
            {
                Plugin.Log.LogWarning($"Manual patch skipped; patch method not found: {patchName}");
                return;
            }

            harmony.Patch(
                original,
                prefixName == null ? null : CreateHarmonyMethod(patchMethod, priority),
                postfixName == null ? null : CreateHarmonyMethod(patchMethod, priority));
            patched++;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Manual patch failed for {patchName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static HarmonyMethod CreateHarmonyMethod(MethodInfo patchMethod, int priority)
    {
        var harmonyMethod = new HarmonyMethod(patchMethod);
        if (priority != Priority.Normal)
        {
            harmonyMethod.priority = priority;
        }

        return harmonyMethod;
    }
}
