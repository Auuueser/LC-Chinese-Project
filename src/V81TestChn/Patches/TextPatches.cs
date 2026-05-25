using HarmonyLib;
using GameNetcodeStuff;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Text;

namespace V81TestChn;

internal static partial class TextPatches
{
    private const int TextClassificationCacheLimit = 4096;
    private const int GlobalStringBuilderTranslationLengthLimit = 1024;
    private static readonly Dictionary<int, CachedTextClassification> InputFieldTextCache = new();
    private static readonly Dictionary<int, CachedTextClassification> LobbySlotTextCache = new();

    private readonly struct CachedTextClassification
    {
        public CachedTextClassification(int parentId, bool value)
        {
            ParentId = parentId;
            Value = value;
        }

        public int ParentId { get; }
        public bool Value { get; }
    }

    public static void Initialize(ConfigFile config)
    {
        HudScannerLocalizationService.Initialize(config);
    }

    public static int Install(Harmony harmony) => InstallTextPatches(harmony);

    public static void Clear()
    {
        HudScannerLocalizationService.Clear();
        SignalTranslatorLocalizationService.Clear();
        ClearHudRuntimeCaches();
        InputFieldTextCache.Clear();
        LobbySlotTextCache.Clear();
        CustomLocalizationExtensionService.ClearRuntimeCaches();
    }

    [HarmonyPatch(typeof(StartOfRound), "FirePlayersAfterDeadlineClientRpc")]
    [HarmonyPostfix]
    private static void StartOfRoundFirePlayersAfterDeadlineClientRpcPostfix()
    {
        HudEndGameLocalizationService.ApplyPlayersFiredAfterDeadline("StartOfRound.FirePlayersAfterDeadlineClientRpc");
    }

    [HarmonyPatch(typeof(HUDManager), "Start")]
    [HarmonyPostfix]
    private static void HudManagerStartPostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (__instance == null)
        {
            return;
        }

        ClearHudRuntimeCaches();
        RadiationWarningPlaybackService.ResetForHudLifecycle(__instance, "HUDManager.Start");
        AlertTextureReplacementService.ForceApplySystemOnlineOverlay(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginSystemOnlineExactPathWatcher(__instance, "HUDManager.Start");
        AlertTextureReplacementService.SyncFixedSceneLabels(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginFixedSceneLabelWatcher(__instance, "HUDManager.Start");
        TargetedUiTranslator.TranslateHud(__instance, "HUDManager.Start.hud");
        HudScannerLocalizationService.ApplyHudScannerLocalization(__instance, "HUDManager.Start.scanner");
        TargetedUiTranslator.TranslateHudPlanetInfo(__instance, "HUDManager.Start.planet-info");
        TargetedUiTranslator.TranslateHudChatPrompts(__instance, "HUDManager.Start.chat-prompts");
        // Plugin.Log.LogInfo($"Patch entry HUDManager.Start loadingText={__instance.loadingText?.name ?? "<null>"} riskText={__instance.planetRiskLevelText?.name ?? "<null>"}");
    }

    private static void ClearHudRuntimeCaches()
    {
        HudScannerLocalizationService.ClearRuntimeCaches();
        TranslationGuard.ClearRuntimeCaches();
        SignalTranslatorLocalizationService.ClearCaches();
    }

    [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
    [HarmonyPrefix]
    private static void HudManagerUseSignalTranslatorClientRpcPrefix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        SignalTranslatorLocalizationService.BeginLocalizationWindow(__instance, "HUDManager.UseSignalTranslatorClientRpc.prefix");
    }

    [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
    [HarmonyPostfix]
    private static void HudManagerUseSignalTranslatorClientRpcPostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        SignalTranslatorLocalizationService.BeginLocalizationWindow(__instance, "HUDManager.UseSignalTranslatorClientRpc.postfix");
    }

    [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
    [HarmonyPostfix]
    private static void HudManagerUpdateScanNodesPostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudScannerLocalizationService.ApplyHudScannerSourceNodesIfDue(__instance, "HUDManager.UpdateScanNodes.scan-node-source");

        if (SignalTranslatorLocalizationService.ShouldRetryLocalization())
        {
            SignalTranslatorLocalizationService.ApplyHudLocalization(__instance, "HUDManager.UpdateScanNodes.signal-translator-window");
        }

        HudScannerLocalizationService.ApplyHudScannerLocalization(__instance, "HUDManager.UpdateScanNodes");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayCreditsEarning")]
    [HarmonyPostfix]
    private static void HudManagerDisplayCreditsEarningPostfix(HUDManager __instance)
    {
        HudRewardsLocalizationService.ApplyCreditsEarning(__instance, "HUDManager.DisplayCreditsEarning");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayNewScrapFound")]
    [HarmonyPostfix]
    private static void HudManagerDisplayNewScrapFoundPostfix(HUDManager __instance)
    {
        HudRewardsLocalizationService.ApplyNewScrapFound(__instance, "HUDManager.DisplayNewScrapFound");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayNewDeadline")]
    [HarmonyPostfix]
    private static void HudManagerDisplayNewDeadlinePostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyNewDeadline(__instance, "HUDManager.DisplayNewDeadline");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayDaysLeft")]
    [HarmonyPostfix]
    private static void HudManagerDisplayDaysLeftPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyVoteAndDeadlineText(__instance, "HUDManager.DisplayDaysLeft");
    }

    [HarmonyPatch(typeof(HUDManager), "SetShipLeaveEarlyVotesText")]
    [HarmonyPostfix]
    private static void HudManagerSetShipLeaveEarlyVotesTextPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyVoteAndDeadlineText(__instance, "HUDManager.SetShipLeaveEarlyVotesText");
    }

    private static void HudManagerReadDialoguePrefix(DialogueSegment[] dialogueArray)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudEndGameLocalizationService.ApplyDialogueSegments(dialogueArray, "HUDManager.ReadDialogue");
    }

    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    [HarmonyPostfix]
    private static void HudManagerAddChatMessagePostfix(HUDManager __instance)
    {
        TargetedUiTranslator.TranslateHudChatOutput(__instance, "HUDManager.AddChatMessage");
    }

    [HarmonyPatch(typeof(HUDManager), "AddTextToChatOnServer")]
    [HarmonyPostfix]
    private static void HudManagerAddTextToChatOnServerPostfix(HUDManager __instance, int playerId = -1)
    {
        if (playerId == -1)
        {
            TargetedUiTranslator.TranslateHudChatOutput(__instance, "HUDManager.AddTextToChatOnServer.system");
        }
    }

    [HarmonyPatch(typeof(HangarShipDoor), "Start")]
    [HarmonyPostfix]
    private static void HangarShipDoorStartPostfix(HangarShipDoor __instance)
    {
        DirectTextLocalizationService.ApplyHangarShipDoor(__instance, "HangarShipDoor.Start");
    }

    private static void LobbySlotSetModdedIconPostfix(LobbySlot __instance)
    {
        if (__instance == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateLobbySlotStatic(__instance, "LobbySlot.SetModdedIcon");
        FontFallbackAuditService.RecordLobbySlot(__instance, "LobbySlot.SetModdedIcon");
    }

    [HarmonyPatch(typeof(HUDManager), "UpdateBoxesSpectateUI")]
    [HarmonyPostfix]
    private static void HudManagerUpdateBoxesSpectateUiPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplySpectateUi(__instance, "HUDManager.UpdateBoxesSpectateUI");
    }

    private static void HudManagerSetSpectatingTextToPlayerPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplySpectateUi(__instance, "HUDManager.SetSpectatingTextToPlayer");
    }

    [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
    [HarmonyPostfix]
    private static void HudManagerFillEndGameStatsPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyHudEndGame(__instance, "HUDManager.FillEndGameStats");
    }

    [HarmonyPatch(typeof(HUDManager), "ApplyPenalty")]
    [HarmonyPostfix]
    private static void HudManagerApplyPenaltyPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyHudEndGame(__instance, "HUDManager.ApplyPenalty");
    }

    [HarmonyPatch(typeof(HUDManager), "ShowPlayersFiredScreen")]
    [HarmonyPostfix]
    private static void HudManagerShowPlayersFiredScreenPostfix(HUDManager __instance, bool show)
    {
        if (!show)
        {
            return;
        }

        HudEndGameLocalizationService.ApplyPlayersFiredScreen(__instance, "HUDManager.ShowPlayersFiredScreen");
    }

    [HarmonyPatch(typeof(ChallengeLeaderboardSlot), "SetSlotValues")]
    [HarmonyPostfix]
    private static void ChallengeLeaderboardSlotSetSlotValuesPostfix(ChallengeLeaderboardSlot __instance)
    {
        HudEndGameLocalizationService.ApplyChallengeSlot(__instance, "ChallengeLeaderboardSlot.SetSlotValues");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayTip")]
    [HarmonyPrefix]
    private static void HudManagerDisplayTipPrefix(ref string headerText, ref string bodyText)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyDisplayTip(ref headerText, ref bodyText);
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayStatusEffect")]
    [HarmonyPrefix]
    private static void HudManagerDisplayStatusEffectPrefix(ref string statusEffect)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyDisplayStatusEffect(ref statusEffect);
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTip")]
    [HarmonyPrefix]
    private static void HudManagerChangeControlTipPrefix(ref string changeTo)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyControlTipPrefix(ref changeTo);
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTip")]
    [HarmonyPostfix]
    private static void HudManagerChangeControlTipPostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyControlTipPostfix(__instance, "HUDManager.ChangeControlTip");
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTipMultiple")]
    [HarmonyPrefix]
    private static void HudManagerChangeControlTipMultiplePrefix(ref string[] allLines, bool holdingItem, Item itemProperties)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyControlTipMultiplePrefix(ref allLines, itemProperties);
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTipMultiple")]
    [HarmonyPostfix]
    private static void HudManagerChangeControlTipMultiplePostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyControlTipPostfix(__instance, "HUDManager.ChangeControlTipMultiple");
    }

    [HarmonyPatch(typeof(GrabbableObject), "Start")]
    [HarmonyPostfix]
    private static void GrabbableObjectStartPostfix(GrabbableObject __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyGrabbableItem(__instance);
    }

    [HarmonyPatch(typeof(GrabbableObject), "SetControlTipsForItem")]
    [HarmonyPrefix]
    private static void GrabbableObjectSetControlTipsPrefix(GrabbableObject __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyGrabbableItem(__instance);
    }

    [HarmonyPatch(typeof(GrabbableObject), "SetControlTipsForItem")]
    [HarmonyPostfix]
    private static void GrabbableObjectSetControlTipsPostfix(GrabbableObject __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyGrabbableControlTips(__instance, "GrabbableObject.SetControlTipsForItem");
    }

    [HarmonyPatch(typeof(StunGrenadeItem), "SetControlTipForGrenade")]
    [HarmonyPostfix]
    private static void StunGrenadeItemSetControlTipForGrenadePostfix(StunGrenadeItem __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyStunGrenadeControlTip(__instance, "StunGrenadeItem.SetControlTipForGrenade");
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SetHoverTipAndCurrentInteractTrigger")]
    [HarmonyPostfix]
    private static void PlayerControllerBSetHoverTipAndCurrentInteractTriggerPostfix(PlayerControllerB __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyPlayerCursorTip(__instance, "PlayerControllerB.SetHoverTipAndCurrentInteractTrigger");
    }

    [HarmonyPatch(typeof(VehicleController), "Start")]
    [HarmonyPostfix]
    private static void VehicleControllerStartPostfix(VehicleController __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudInteractionLocalizationService.ApplyVehicleStaticTexts(__instance, "VehicleController.Start");
    }

    [HarmonyPatch(typeof(RoundManager), "GenerateNewLevelClientRpc")]
    [HarmonyPostfix]
    private static void RoundManagerGenerateNewLevelClientRpcPostfix()
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        DirectTextLocalizationService.ApplyComposite(HUDManager.Instance?.loadingText, "RoundManager.GenerateNewLevelClientRpc");
    }

    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    [HarmonyPrefix]
    private static void TmpSetTextPrefix(TMP_Text __instance, ref string value)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        FontFallbackAuditService.RecordTextSnapshot(__instance, "TMP_Text.set_text.prefix", value);
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        if (ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelDescription))
        {
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText prefix enter len={value?.Length ?? -1}");
            MapScreenLocalizationService.ApplySetTextPrefix(__instance, ref value, "TMP_Text.set_text.map-screen");
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText prefix exit ms={(DateTime.UtcNow - start).TotalMilliseconds:0.0} len={value?.Length ?? -1}");
            return;
        }

        if (HudEndGameLocalizationService.TryTranslateEndOfRunStatsText(__instance, ref value, "TMP_Text.set_text.end-of-run-stats"))
        {
            return;
        }

        HudEndGameLocalizationService.TryRewriteSpectateDeadValue(__instance, ref value, "TMP_Text.set_text");
        TranslateTmpText(__instance, ref value);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void TmpSetTextPostfix(TMP_Text __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        FontFallbackAuditService.RecordTextSnapshot(__instance, "TMP_Text.set_text.postfix");
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        var isMapScreenText = ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelDescription);
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText postfix enter len={__instance.text?.Length ?? -1}");
        }

        HudEndGameLocalizationService.TryLocalizeSpectateDeadLabel(__instance, "TMP_Text.post_set_text");
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after spectate-dead");
        }

        FontFallbackService.RepairPostTranslationText(__instance, "TMP_Text.post_set_text");
        ApplyBootSplashTypography(__instance, __instance.text);
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after font-repair");
        }

        SyncRelayTitleHooks(__instance, "TMP_Text.post_set_text");
        if (CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            CustomLocalizationExtensionService.ApplyStyle(__instance, __instance.text);
        }

        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after relay-sync");
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix exit");
            return;
        }

        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TMP_Text.post_set_text");
    }

    [HarmonyPatch(typeof(TMP_Text), "set_color")]
    [HarmonyPrefix]
    private static void TmpSetColorPrefix(TMP_Text __instance, ref Color value)
    {
        if (!TranslationGuard.ShouldTouchGlobalTextStyle(__instance))
        {
            return;
        }

        FontFallbackService.SanitizeAssignedColor(__instance, ref value, __instance.text);
        FontFallbackService.SanitizeSystemOnlineAssignedColor(__instance, ref value, "TMP_Text.set_color");
    }

    [HarmonyPriority(Priority.Last)]
    private static void TmpSetColorPostfix(TMP_Text __instance)
    {
        if (!TranslationGuard.ShouldTouchGlobalTextStyle(__instance))
        {
            return;
        }

        SyncRelayTitleHooks(__instance, "TMP_Text.post_set_color");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TMP_Text.post_set_color");
    }

    [HarmonyPatch(typeof(TMP_FontAsset), "Awake")]
    [HarmonyPostfix]
    private static void TmpFontAssetAwakePostfix(TMP_FontAsset __instance)
    {
        FontFallbackAuditService.RecordFontAssetSnapshot(__instance, "TMP_FontAsset.Awake.before-fallback");
        FontFallbackService.OnFontAssetAwake(__instance);
        EmbeddedFontPatcherService.PatchFontAsset(__instance, "TMP_FontAsset.Awake");
        FontFallbackAuditService.RecordFontAssetSnapshot(__instance, "TMP_FontAsset.Awake.after-fallback");
    }

    private static void AnimatorSetTriggerPostfix(Animator __instance, string name)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name == "RadiationWarning" && ReferenceEquals(__instance, HUDManager.Instance.radiationGraphicAnimator))
        {
            RadiationWarningAuditService.OnRadiationWarningTriggered(HUDManager.Instance, "Animator.SetTrigger.RadiationWarning");
            RadiationWarningPlaybackService.OnRadiationWarningTriggered(HUDManager.Instance, "Animator.SetTrigger.RadiationWarning");
        }
    }

    private static void AnimatorSetBoolPrefix(Animator __instance, string name, bool value)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name != "IsLoading" || !ReferenceEquals(__instance, HUDManager.Instance.LoadingScreen))
        {
            return;
        }

        if (value)
        {
            AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(HUDManager.Instance, "Animator.SetBool.IsLoading.prefix.true");
        }
    }

    private static void AnimatorSetBoolPostfix(Animator __instance, string name, bool value)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name == "transmitting" && ReferenceEquals(__instance, HUDManager.Instance.signalTranslatorAnimator))
        {
            if (value)
            {
                SignalTranslatorLocalizationService.BeginLocalizationWindow(HUDManager.Instance, "Animator.SetBool.transmitting.true");
            }
            else
            {
                SignalTranslatorLocalizationService.EndLocalizationWindow();
            }
        }

        if (name != "IsLoading" || !ReferenceEquals(__instance, HUDManager.Instance.LoadingScreen))
        {
            return;
        }

        if (value)
        {
            AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(HUDManager.Instance, "Animator.SetBool.IsLoading.postfix.true");
        }
        else
        {
            AlertTextureReplacementService.HideEnteringAtmosphereOverlayForHud(HUDManager.Instance, "Animator.SetBool.IsLoading.false");
        }
    }

    private static bool TranslateTmpText(TMP_Text __instance, ref string value)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return false;
        }

        if (IsLobbySlotDynamicText(__instance))
        {
            FontFallbackService.ApplyFallback(__instance, value);
            return false;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, value))
        {
            FontFallbackService.ApplyFallback(__instance, value);
            return false;
        }

        FontFallbackService.ApplyFallback(__instance, value);
        ApplyBootSplashTypography(__instance, value);
        if (TranslationService.TryTranslateKnownDynamicTextFast(value, out var translated) ||
            TranslationService.TryTranslateFastExact(value, out translated))
        {
            value = translated;
            FontFallbackService.ApplyFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            Plugin.ReportTranslationHit();
            return true;
        }

        RuntimeTextCollector.Record(__instance, value);
        return false;
    }

    [HarmonyPatch(typeof(Text), "set_text")]
    [HarmonyPrefix]
    private static void UiTextSetTextPrefix(Text __instance, ref string value)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TranslateUiText(__instance, ref value);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "UI.Text.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void UiTextSetTextPostfix(Text __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "UI.Text.post_set_text");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "UI.Text.post_set_text");
        if (CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            CustomLocalizationExtensionService.ApplyStyle(__instance, __instance.text);
        }
    }

    private static bool TranslateUiText(Text __instance, ref string value)
    {
        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, value))
        {
            return false;
        }

        if (TranslationService.TryTranslateKnownDynamicTextFast(value, out var translated) ||
            TranslationService.TryTranslateFastExact(value, out translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
            return true;
        }

        RuntimeTextCollector.Record(__instance, value);
        return false;
    }

    [HarmonyPatch(typeof(TextMesh), "set_text")]
    [HarmonyPrefix]
    private static void TextMeshSetTextPrefix(TextMesh __instance, ref string value)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, value))
        {
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TextMesh.set_text", value);
            return;
        }

        if (TranslationService.TryTranslateKnownDynamicTextFast(value, out var translated) ||
            TranslationService.TryTranslateFastExact(value, out translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(__instance, value);
        }

        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TextMesh.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void TextMeshSetTextPostfix(TextMesh __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TextMesh.post_set_text");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TextMesh.post_set_text");
        if (CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            CustomLocalizationExtensionService.ApplyStyle(__instance, __instance.text);
        }
    }

    private static bool IsInputFieldTextComponent(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (TryGetCachedTextClassification(text, InputFieldTextCache, out var cached))
        {
            return cached;
        }

        var inputField = text.GetComponentInParent<TMP_InputField>(true);
        if (inputField == null)
        {
            CacheTextClassification(text, InputFieldTextCache, false);
            return false;
        }

        var result = ReferenceEquals(inputField.textComponent, text);
        CacheTextClassification(text, InputFieldTextCache, result);
        return result;
    }

    private static bool IsLobbySlotDynamicText(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (TryGetCachedTextClassification(text, LobbySlotTextCache, out var cached))
        {
            return cached;
        }

        var slot = text.GetComponentInParent<LobbySlot>(true);
        var result = slot != null && (ReferenceEquals(slot.LobbyName, text) || ReferenceEquals(slot.playerCount, text));
        CacheTextClassification(text, LobbySlotTextCache, result);
        return result;
    }

    private static bool TryGetCachedTextClassification(
        TMP_Text text,
        Dictionary<int, CachedTextClassification> cache,
        out bool value)
    {
        var parentId = GetParentInstanceId(text);
        if (cache.TryGetValue(text.GetInstanceID(), out var cached) && cached.ParentId == parentId)
        {
            value = cached.Value;
            return true;
        }

        value = false;
        return false;
    }

    private static void CacheTextClassification(TMP_Text text, Dictionary<int, CachedTextClassification> cache, bool value)
    {
        if (cache.Count >= TextClassificationCacheLimit)
        {
            cache.Clear();
        }

        cache[text.GetInstanceID()] = new CachedTextClassification(GetParentInstanceId(text), value);
    }

    private static int GetParentInstanceId(TMP_Text text)
    {
        return text.transform.parent == null ? 0 : text.transform.parent.GetInstanceID();
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBoolPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        TmpSetTextPrefix(__instance, ref sourceText);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(string,bool)", sourceText);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(float))]
    [HarmonyPrefix]
    private static void TmpSetTextStringFloatPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        TmpSetTextPrefix(__instance, ref sourceText);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(string,float)", sourceText);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(StringBuilder))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBuilderPrefix(TMP_Text __instance, ref StringBuilder sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        if (sourceText == null)
        {
            return;
        }

        if (sourceText.Length > GlobalStringBuilderTranslationLengthLimit)
        {
            return;
        }

        if (!MightContainTranslatableStringBuilderText(sourceText))
        {
            return;
        }

        var rawText = sourceText.ToString();
        if (IsLobbySlotDynamicText(__instance))
        {
            FontFallbackService.ApplyFallback(__instance, rawText);
            return;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, rawText))
        {
            FontFallbackService.ApplyFallback(__instance, rawText);
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(StringBuilder)", rawText);
            return;
        }

        FontFallbackService.ApplyFallback(__instance, rawText);
        ApplyBootSplashTypography(__instance, rawText);
        if (TranslationService.TryTranslateKnownDynamicTextFast(rawText, out var translated) ||
            TranslationService.TryTranslateFastExact(rawText, out translated))
        {
            sourceText = new StringBuilder(translated);
            FontFallbackService.ApplyFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(StringBuilder)", translated);
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(__instance, rawText);
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(StringBuilder)", rawText);
        }
    }

    private static bool MightContainTranslatableStringBuilderText(StringBuilder sourceText)
    {
        for (var i = 0; i < sourceText.Length; i++)
        {
            var ch = sourceText[i];
            if (ch > 127 || char.IsLetter(ch) || ch == '<' || ch == '[' || ch == '(')
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyBootSplashTypography(TMP_Text? text, string? value)
    {
        if (text == null || !TranslationService.IsBootSplashText(value))
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void SyncRelayTitleHooks(TMP_Text text, string stage)
    {
        if (text == null || HUDManager.Instance == null)
        {
            return;
        }

        AlertTextureReplacementService.SyncEnteringAtmosphereOverlayState(text, stage);
    }

}
