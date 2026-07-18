using HarmonyLib;
using GameNetcodeStuff;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Text;

namespace V81TestChn;

internal static partial class TextPatches
{
    private const int TextClassificationCacheLimit = 16384;
    private const int GlobalStringBuilderTranslationLengthLimit = 1024;
    private const int ShortInertDynamicTextLengthLimit = 8;
    private const int GenericStatusTextLengthLimit = 96;
    private const int CompactMetricStatusTextLengthLimit = 32;
    private const int UntranslatedNoopCacheLengthLimit = 160;
    private const int TmpHookNoopCacheLimit = 16384;
    private const int TmpHookTranslationCacheLimit = 16384;
    private const int TmpHookSourceNoopCacheLimit = 16384;
    private const int TmpColorHookEligibilityCacheLimit = 16384;
    private const int TmpColorHookCandidateCacheLimit = 16384;
    private const int TmpHookShapeCacheWarmupCount = 2;
    private const int TmpHookComponentBypassWarmupCount = 4;
    private const int TmpHookComponentBypassTextLengthLimit = 160;
    private const int TmpNumericFormatTextLengthLimit = 96;
    private const float LobbyNameFallbackGlyphMinFontScale = 0.82f;
    private static readonly BoundedCache<int, CachedTextClassification> InputFieldTextCache = new(TextClassificationCacheLimit);
    private static readonly BoundedCache<int, CachedTextClassification> LobbySlotTextCache = new(TextClassificationCacheLimit);
    private static readonly BoundedCache<int, CachedLobbyNameTypography> LobbyNameTypographyCache = new(TextClassificationCacheLimit);
    private static readonly BoundedCache<int, CachedTmpHookNoop> TmpHookNoopCache = new(TmpHookNoopCacheLimit);
    private static readonly BoundedCache<int, CachedTmpHookTranslation> TmpHookTranslationCache = new(TmpHookTranslationCacheLimit);
    private static readonly BoundedCache<int, CachedTmpColorHookEligibility> TmpColorHookEligibilityCache = new(TmpColorHookEligibilityCacheLimit);
    private static readonly BoundedSet<int> TmpColorHookCandidateTextIds = new(TmpColorHookCandidateCacheLimit);
    private static readonly BoundedSet<string> TmpHookSourceNoopCache = new(TmpHookSourceNoopCacheLimit, StringComparer.Ordinal);
    [ThreadStatic]
    private static List<TmpSetTextPostfixSkipEntry>? _tmpSetTextPostfixSkipStack;
    [ThreadStatic]
    private static bool _restoringLateWriterCursorTipSource;
    private static ConfigEntry<bool>? _enableTmpHookPerfCounters;
    private static ConfigEntry<int>? _tmpHookPerfLogIntervalSeconds;
    private static ConfigEntry<bool>? _enableGlobalTmpColorHook;
    private static ConfigEntry<bool>? _enableGlobalTmpPostSetRepair;
    private static bool _tmpHookPerfCountersEnabledFast;
    private static long _tmpHookPerfLogIntervalTicksFast;
    private static bool _enableGlobalTmpColorHookFast;
    private static bool _enableGlobalTmpPostSetRepairFast;
    private static long _tmpPerfNextLogTimestamp;
    private static long _tmpPerfPrefixCalls;
    private static long _tmpPerfPostfixCalls;
    private static long _tmpPerfSkipHits;
    private static long _tmpPerfSkipMisses;
    private static long _tmpPerfExactCacheHits;
    private static long _tmpPerfShapeCacheHits;
    private static long _tmpPerfComponentBypassHits;
    private static long _tmpPerfTranslationCacheHits;
    private static long _tmpPerfGenericStatusHits;
    private static long _tmpPerfGenericStatusFailNoMarker;
    private static long _tmpPerfGenericStatusFailControlTip;
    private static long _tmpPerfGenericStatusFailCjk;
    private static long _tmpPerfGenericStatusFailShape;
    private static long _tmpPerfTranslateCalls;
    private static long _tmpPerfFallbackCalls;
    private static long _tmpPerfCollectorCalls;
    private static long _tmpPerfPrefixTicks;
    private static long _tmpPerfPostfixTicks;
    private static long _tmpPerfTranslateTicks;

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

    private readonly struct CachedLobbyNameTypography
    {
        public CachedLobbyNameTypography(int parentId, bool enableAutoSizing, float fontSize, float fontSizeMin, float fontSizeMax)
        {
            ParentId = parentId;
            EnableAutoSizing = enableAutoSizing;
            FontSize = fontSize;
            FontSizeMin = fontSizeMin;
            FontSizeMax = fontSizeMax;
        }

        public int ParentId { get; }
        public bool EnableAutoSizing { get; }
        public float FontSize { get; }
        public float FontSizeMin { get; }
        public float FontSizeMax { get; }
    }

    private readonly struct CachedTmpHookNoop
    {
        public CachedTmpHookNoop(
            string source,
            ulong shapeHash,
            int shapeLength,
            int shapeCount,
            bool shapeActive,
            int componentMissCount,
            bool componentBypassActive)
        {
            Source = source;
            ShapeHash = shapeHash;
            ShapeLength = shapeLength;
            ShapeCount = shapeCount;
            ShapeActive = shapeActive;
            ComponentMissCount = componentMissCount;
            ComponentBypassActive = componentBypassActive;
        }

        public string Source { get; }
        public ulong ShapeHash { get; }
        public int ShapeLength { get; }
        public int ShapeCount { get; }
        public bool ShapeActive { get; }
        public int ComponentMissCount { get; }
        public bool ComponentBypassActive { get; }
    }

    private readonly struct CachedTmpHookTranslation
    {
        public CachedTmpHookTranslation(string? source, string translated, int fontId)
        {
            Source = source;
            Translated = translated;
            FontId = fontId;
        }

        public string? Source { get; }
        public string Translated { get; }
        public int FontId { get; }
    }

    private readonly struct TmpSetTextPostfixSkipEntry
    {
        public TmpSetTextPostfixSkipEntry(int textId, string? value, bool requiresValueMatch)
        {
            TextId = textId;
            Value = value;
            RequiresValueMatch = requiresValueMatch;
        }

        public int TextId { get; }
        public string? Value { get; }
        public bool RequiresValueMatch { get; }
    }

    private readonly struct CachedTmpColorHookEligibility
    {
        public CachedTmpColorHookEligibility(string? source, bool value)
        {
            Source = source;
            Value = value;
        }

        public string? Source { get; }
        public bool Value { get; }
    }

    private readonly struct TmpTextShape
    {
        private TmpTextShape(
            int length,
            bool hasNonWhiteSpace,
            bool hasCjk,
            bool hasAsciiLetter,
            bool hasDigit,
            bool hasNewline,
            bool hasRichTextMarker,
            bool hasGenericStatusMarker)
        {
            Length = length;
            HasNonWhiteSpace = hasNonWhiteSpace;
            HasCjk = hasCjk;
            HasAsciiLetter = hasAsciiLetter;
            HasDigit = hasDigit;
            HasNewline = hasNewline;
            HasRichTextMarker = hasRichTextMarker;
            HasGenericStatusMarker = hasGenericStatusMarker;
        }

        public int Length { get; }
        public bool HasNonWhiteSpace { get; }
        public bool HasCjk { get; }
        public bool HasAsciiLetter { get; }
        public bool HasDigit { get; }
        public bool HasNewline { get; }
        public bool HasRichTextMarker { get; }
        public bool HasGenericStatusMarker { get; }
        public bool IsNullOrEmpty => Length == 0;
        public bool IsNullOrWhiteSpace => Length == 0 || !HasNonWhiteSpace;

        public static TmpTextShape From(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            if (value.Length > UntranslatedNoopCacheLengthLimit)
            {
                return new TmpTextShape(
                    value.Length,
                    hasNonWhiteSpace: true,
                    hasCjk: false,
                    hasAsciiLetter: false,
                    hasDigit: false,
                    hasNewline: false,
                    hasRichTextMarker: false,
                    hasGenericStatusMarker: false);
            }

            var hasNonWhiteSpace = false;
            var hasCjk = false;
            var hasAsciiLetter = false;
            var hasDigit = false;
            var hasNewline = false;
            var hasRichTextMarker = false;
            var hasGenericStatusMarker = false;
            foreach (var ch in value)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    hasNonWhiteSpace = true;
                }

                if (IsCjk(ch))
                {
                    hasCjk = true;
                    continue;
                }

                if (IsAsciiLetter(ch))
                {
                    hasAsciiLetter = true;
                    continue;
                }

                if (char.IsDigit(ch))
                {
                    hasDigit = true;
                    hasGenericStatusMarker = true;
                    continue;
                }

                if (ch is '\r' or '\n')
                {
                    hasNewline = true;
                    continue;
                }

                if (ch == '<')
                {
                    hasRichTextMarker = true;
                    hasGenericStatusMarker = true;
                    continue;
                }

                if (ch == '>')
                {
                    hasGenericStatusMarker = true;
                    continue;
                }

                if (ch is '$' or '%' or '/' or ':' or '[' or ']' or '(' or ')')
                {
                    hasGenericStatusMarker = true;
                }
            }

            return new TmpTextShape(
                value.Length,
                hasNonWhiteSpace,
                hasCjk,
                hasAsciiLetter,
                hasDigit,
                hasNewline,
                hasRichTextMarker,
                hasGenericStatusMarker);
        }
    }

    public static void Initialize(ConfigFile config)
    {
        HudScannerLocalizationService.Initialize(config);
        _enableTmpHookPerfCounters = config.Bind(
            ConfigSections.DiagnosticsGeneral,
            "EnableTmpHookPerfCounters",
            false,
            "Enable temporary TMP hook performance counters. Default off.");
        _tmpHookPerfLogIntervalSeconds = config.Bind(
            ConfigSections.DiagnosticsGeneral,
            "TmpHookPerfLogIntervalSeconds",
            10,
            "TMP hook performance counter log interval in seconds when enabled.");
        _enableGlobalTmpColorHook = config.Bind(
            ConfigSections.Performance,
            "EnableGlobalTmpColorHook",
            false,
            "Enable the legacy global TMP_Text.color hook. Default off to avoid per-color-update overhead in large modpacks.");
        _enableGlobalTmpPostSetRepair = config.Bind(
            ConfigSections.Performance,
            "EnableGlobalTmpPostSetRepair",
            false,
            "Enable legacy global TMP post-set repair. Default off to avoid one extra Harmony callback for every TMP text assignment.");
        _tmpHookPerfCountersEnabledFast = _enableTmpHookPerfCounters.Value;
        _tmpHookPerfLogIntervalTicksFast = Math.Max(1, _tmpHookPerfLogIntervalSeconds.Value) * Stopwatch.Frequency;
        _enableGlobalTmpColorHookFast = _enableGlobalTmpColorHook.Value;
        _enableGlobalTmpPostSetRepairFast = _enableGlobalTmpPostSetRepair.Value;
    }

    public static int Install(Harmony harmony) => InstallTextPatches(harmony);
    private static bool IsGlobalTmpColorHookEnabled => _enableGlobalTmpColorHookFast;
    private static bool IsGlobalTmpPostSetRepairEnabled =>
        _enableGlobalTmpPostSetRepairFast || CustomLocalizationExtensionService.HasGlobalStyleRules;

    public static void Clear()
    {
        HudScannerLocalizationService.Clear();
        SignalTranslatorLocalizationService.Clear();
        ClearHudRuntimeCaches();
        InputFieldTextCache.Clear();
        LobbySlotTextCache.Clear();
        LobbyNameTypographyCache.Clear();
        TmpHookNoopCache.Clear();
        TmpHookTranslationCache.Clear();
        TmpColorHookEligibilityCache.Clear();
        TmpColorHookCandidateTextIds.Clear();
        TmpHookSourceNoopCache.Clear();
        AdvancedFeaturesGradeTextIds.Clear();
        ExternalEnglishCompatibilityService.ClearRuntimeCaches();
        ExternalEnglishCompatibilityUiService.ClearRuntimeCaches();
        ResetTmpHookPerfCounters();
        CustomLocalizationExtensionService.ClearRuntimeCaches();
    }

    internal static void ClearSceneRuntimeCaches()
    {
        InputFieldTextCache.Clear();
        LobbySlotTextCache.Clear();
        LobbyNameTypographyCache.Clear();
        TmpHookNoopCache.Clear();
        TmpHookTranslationCache.Clear();
        TmpColorHookEligibilityCache.Clear();
        TmpColorHookCandidateTextIds.Clear();
        AdvancedFeaturesGradeTextIds.Clear();
        ClearHudRuntimeCaches();
        ExternalEnglishCompatibilityUiService.ClearRuntimeCaches();
        FontFallbackService.ClearSceneComponentCaches();
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
        MeteorShowerWarningLocalizationService.ResetForHudLifecycle(__instance);
        AlertTextureReplacementService.ForceApplySystemOnlineOverlay(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginSystemOnlineExactPathWatcher(__instance, "HUDManager.Start");
        AlertTextureReplacementService.SyncFixedSceneLabels(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginFixedSceneLabelWatcher(__instance, "HUDManager.Start");
        TargetedUiTranslator.TranslateHud(__instance, "HUDManager.Start.hud");
        HudScannerLocalizationService.ApplyHudScannerLocalization(__instance, "HUDManager.Start.scanner");
        TargetedUiTranslator.TranslateHudPlanetInfo(__instance, "HUDManager.Start.planet-info");
        TargetedUiTranslator.TranslateHudChatPrompts(__instance, "HUDManager.Start.chat-prompts");
        TranslateTooManyEmotesMenu(__instance);
        // Plugin.Log.LogInfo($"Patch entry HUDManager.Start loadingText={__instance.loadingText?.name ?? "<null>"} riskText={__instance.planetRiskLevelText?.name ?? "<null>"}");
    }

    [HarmonyPatch(typeof(HUDManager), "MeteorShowerWarningHUD")]
    [HarmonyPostfix]
    private static void HudManagerMeteorShowerWarningHudPostfix(HUDManager __instance)
    {
        MeteorShowerWarningLocalizationService.Apply(__instance, "HUDManager.MeteorShowerWarningHUD");
    }

    private static void ClearHudRuntimeCaches()
    {
        TargetedUiTranslator.ClearCaches();
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

        var hasActiveScanner = HudScannerLocalizationService.HasActiveHudScannerElement(__instance);
        var immediatePass = HudScannerLocalizationService.ShouldRunImmediateHudScannerLocalizationPass(
            __instance,
            hasActiveScanner,
            out hasActiveScanner);
        HudScannerLocalizationService.ApplyHudScannerBoundNodesIfDue(
            __instance,
            "HUDManager.UpdateScanNodes.bound-nodes",
            hasActiveScanner,
            immediatePass);

        if (SignalTranslatorLocalizationService.ShouldRetryLocalization())
        {
            SignalTranslatorLocalizationService.ApplyHudLocalization(__instance, "HUDManager.UpdateScanNodes.signal-translator-window");
        }

        HudScannerLocalizationService.ApplyHudScannerLocalization(
            __instance,
            "HUDManager.UpdateScanNodes",
            hasActiveScanner,
            immediatePass);
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

    private static void HudManagerReadDialoguePostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        HudEndGameLocalizationService.ApplyDialogueHud(__instance, "HUDManager.ReadDialogue.postfix");
    }

    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    [HarmonyPrefix]
    private static void HudManagerAddChatMessagePrefix(HUDManager __instance)
    {
        TargetedUiTranslator.MarkHudChatOutputTranslationPending(__instance);
    }

    [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
    [HarmonyPostfix]
    private static void HudManagerAddChatMessagePostfix(HUDManager __instance)
    {
        TargetedUiTranslator.ScheduleHudChatOutput(__instance, "HUDManager.AddChatMessage");
    }

    [HarmonyPatch(typeof(HUDManager), "AddTextToChatOnServer")]
    [HarmonyPrefix]
    private static void HudManagerAddTextToChatOnServerPrefix(HUDManager __instance, int playerId = -1)
    {
        if (playerId == -1)
        {
            TargetedUiTranslator.MarkHudChatOutputTranslationPending(__instance);
        }
    }

    [HarmonyPatch(typeof(HUDManager), "AddTextToChatOnServer")]
    [HarmonyPostfix]
    private static void HudManagerAddTextToChatOnServerPostfix(HUDManager __instance, int playerId = -1)
    {
        if (playerId == -1)
        {
            TargetedUiTranslator.ScheduleHudChatOutput(__instance, "HUDManager.AddTextToChatOnServer.system");
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

    private static void HudManagerFillEndGameStatsPrefix(int scrapCollected = 0)
    {
        EndGameScrapValueGuard.EnsureSafeScrapDenominator("HUDManager.FillEndGameStats", scrapCollected);
    }

    [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
    [HarmonyPostfix]
    private static void HudManagerFillEndGameStatsPostfix(HUDManager __instance)
    {
        HudEndGameLocalizationService.ApplyHudEndGame(__instance, "HUDManager.FillEndGameStats");
    }

    private static void HudManagerSetPlayerLevelPrefix()
    {
        EndGameScrapValueGuard.EnsureSafeScrapDenominator("HUDManager.SetPlayerLevel");
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

    private static void ShipBuildModeManagerCreateGhostObjectAndHighlightPostfix()
    {
        var text = HUDManager.Instance?.buildModeControlTip;
        if (Plugin.IsRuntimeShuttingDown || text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        var source = text.text;
        var translated = TargetedUiTranslator.TranslateDynamicTargeted(source, DynamicTextDomain.HudControlTip);
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        Plugin.ReportTranslationHit();
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

    private static void RoundManagerSpawnScrapInLevelPostfix(RoundManager __instance)
    {
        EndGameScrapValueGuard.EnsureSafeScrapDenominator(__instance, "RoundManager.SpawnScrapInLevel");
    }

    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    [HarmonyPrefix]
    private static void TmpSetTextPrefix(TMP_Text __instance, ref string value)
    {
        if (!TmpHookPerfCountersEnabled)
        {
            TmpSetTextPrefixCore(__instance, ref value, skipAlreadyChecked: false);
            return;
        }

        CountTmpPerf(ref _tmpPerfPrefixCalls);
        var perfStart = StartTmpPerfTimer();
        try
        {
            TmpSetTextPrefixCore(__instance, ref value, skipAlreadyChecked: false);
        }
        finally
        {
            AddTmpPerfElapsed(ref _tmpPerfPrefixTicks, perfStart);
            MaybeLogTmpHookPerfCounters();
        }
    }

    private static void TmpSetTextPrefixCore(TMP_Text __instance, ref string value, bool skipAlreadyChecked)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (_restoringLateWriterCursorTipSource)
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
            return;
        }

        if (SignalTranslatorLocalizationService.IsPlayerMessageText(__instance))
        {
            SignalTranslatorLocalizationService.PreservePlayerMessageText(__instance, value);
            MarkTmpSetTextPostfixSkip(__instance, value);
            return;
        }

        if (TargetedUiTranslator.ShouldBypassHudChatOutputTmpText(__instance, value, "TMP_Text.set_text.chat-output"))
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
            return;
        }

        if (TryNormalizeHudEndGameGradeLetterText(__instance, ref value))
        {
            return;
        }

        if (TryNormalizeAdvancedFeaturesGradeText(__instance, ref value))
        {
            return;
        }

        if (TryTranslateHudPlanetRiskText(__instance, ref value))
        {
            return;
        }

        if (!skipAlreadyChecked && ShouldSkipGlobalTmpTextHook(__instance, value))
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
            return;
        }

        if (TryApplyCachedTmpHookTranslation(__instance, ref value))
        {
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, value);
            return;
        }

        FontFallbackAuditService.RecordTextSnapshot(__instance, "TMP_Text.set_text.prefix", value);
        if (IsInputFieldTextComponent(__instance))
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
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
    }

    private static bool TryNormalizeHudEndGameGradeLetterText(TMP_Text text, ref string value)
    {
        if (!ReferenceEquals(text, HUDManager.Instance?.statsUIElements?.gradeLetter) ||
            !TryNormalizeHudEndGameGradeLetterValue(value, out var normalized))
        {
            return false;
        }

        value = normalized;
        MarkTmpSetTextPostfixSkip(text, value);
        return true;
    }

    private static bool TryNormalizeHudEndGameGradeLetterValue(string? value, out string normalized)
    {
        return TryNormalizeVanillaEndgameGradeLetter(value, out normalized);
    }

    private static bool TryNormalizeAdvancedFeaturesGradeText(TMP_Text text, ref string value)
    {
        if (!TryNormalizeAdvancedFeaturesGradeTextValue(text, value, out var normalized))
        {
            return false;
        }

        value = normalized;
        MarkTmpSetTextPostfixSkip(text, value);
        return true;
    }

    private static bool TryNormalizeAdvancedFeaturesGradeText(TMP_Text text, ref StringBuilder sourceText)
    {
        if (sourceText == null ||
            sourceText.Length != 1 ||
            !TryNormalizeAdvancedFeaturesGradeTextValue(text, sourceText.ToString(), out var normalized))
        {
            return false;
        }

        sourceText = new StringBuilder(normalized);
        MarkTmpSetTextPostfixSkip(text);
        return true;
    }

    private static void NormalizeHudEndGameGradeLetterSource(TMP_Text? text)
    {
        if (text == null ||
            !TryNormalizeHudEndGameGradeLetterValue(text.text, out var normalized) ||
            string.Equals(text.text?.Trim(), normalized, StringComparison.Ordinal))
        {
            return;
        }

        text.text = normalized;
    }

    private static bool TryTranslateHudPlanetRiskText(TMP_Text? text, ref string value)
    {
        if (text == null ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (TargetedUiTranslator.TryHandleHudPlanetRiskText(HUDManager.Instance, text, value, "TMP_Text.set_text.planet-risk", out var preparedValue))
        {
            value = preparedValue;
            ApplyTmpHookFallback(text, value);
            Plugin.ReportTranslationHit();
            return true;
        }

        if (!TranslationService.LooksLikePlanetInfoTextCheap(value) ||
            !TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.PlanetInfo, value, out var translated) ||
            string.Equals(value, translated, StringComparison.Ordinal))
        {
            return false;
        }

        value = translated;
        ApplyTmpHookFallback(text, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    private static bool TryTranslateHudPlanetRiskText(TMP_Text? text, ref StringBuilder sourceText)
    {
        if (text == null ||
            sourceText == null ||
            sourceText.Length == 0 ||
            !TargetedUiTranslator.IsHudPlanetRiskTextCandidate(HUDManager.Instance, text))
        {
            return false;
        }

        var rawText = sourceText.ToString();
        if (!TargetedUiTranslator.TryHandleHudPlanetRiskText(HUDManager.Instance, text, rawText, "TMP_Text.SetText(StringBuilder).planet-risk", out var preparedValue))
        {
            return false;
        }

        sourceText = new StringBuilder(preparedValue);
        ApplyTmpHookFallback(text, preparedValue);
        Plugin.ReportTranslationHit();
        return true;
    }

    [HarmonyPriority(Priority.Last)]
    private static void TmpSetTextPostfix(TMP_Text __instance)
    {
        if (!TmpHookPerfCountersEnabled)
        {
            TmpSetTextPostfixCore(__instance);
            return;
        }

        CountTmpPerf(ref _tmpPerfPostfixCalls);
        var perfStart = StartTmpPerfTimer();
        try
        {
            TmpSetTextPostfixCore(__instance);
        }
        finally
        {
            AddTmpPerfElapsed(ref _tmpPerfPostfixTicks, perfStart);
            MaybeLogTmpHookPerfCounters();
        }
    }

    private static void TmpSetTextPostfixCore(TMP_Text __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            ClearTmpSetTextPostfixSkip();
            return;
        }

        if (__instance == null)
        {
            ClearTmpSetTextPostfixSkip();
            return;
        }

        if (TrySkipTmpSetTextPostfixFromPrefix(__instance))
        {
            return;
        }

        var currentText = __instance.text;
        if (TrySkipTmpSetTextPostfixFromPrefix(__instance, currentText))
        {
            return;
        }

        if (FinalizeHudPlanetRiskText(__instance))
        {
            ClearTmpSetTextPostfixSkip();
            return;
        }

        if (ShouldSkipGlobalTmpTextHook(__instance, currentText))
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
        HudEndGameLocalizationService.TryNormalizePlayersFiredText(__instance, "TMP_Text.post_set_text.players-fired");
        HudEndGameLocalizationService.TryNormalizeDialogueBoxText(__instance, "TMP_Text.post_set_text.dialogue");
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

    private static void ClearTmpSetTextPostfixSkip()
    {
        if (!IsGlobalTmpPostSetRepairEnabled)
        {
            return;
        }

        _tmpSetTextPostfixSkipStack?.Clear();
    }

    private static void MarkTmpSetTextPostfixSkip(TMP_Text? text, string? value)
    {
        if (!IsGlobalTmpPostSetRepairEnabled)
        {
            return;
        }

        if (text == null || value == null)
        {
            return;
        }

        AddTmpSetTextPostfixSkip(text.GetInstanceID(), value, requiresValueMatch: true);
    }

    private static void MarkTmpSetTextPostfixSkip(TMP_Text? text)
    {
        if (!IsGlobalTmpPostSetRepairEnabled)
        {
            return;
        }

        if (text == null)
        {
            return;
        }

        AddTmpSetTextPostfixSkip(text.GetInstanceID(), null, requiresValueMatch: false);
    }

    private static void MarkTmpSetTextPostfixSkipForTranslatedOutput(TMP_Text? text, string? translated)
    {
        if (!IsGlobalTmpPostSetRepairEnabled)
        {
            return;
        }

        if (!CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            MarkTmpSetTextPostfixSkip(text, translated);
        }
    }

    private static bool TrySkipTmpSetTextPostfixFromPrefix(TMP_Text text, string? value)
    {
        var stack = _tmpSetTextPostfixSkipStack;
        if (stack == null || stack.Count == 0)
        {
            return false;
        }

        var textId = text.GetInstanceID();
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var entry = stack[i];
            if (entry.TextId != textId)
            {
                continue;
            }

            if (!entry.RequiresValueMatch || TextEquals(entry.Value, value))
            {
                stack.RemoveAt(i);
                return true;
            }

            stack.RemoveAt(i);
            return false;
        }

        return false;
    }

    private static bool TrySkipTmpSetTextPostfixFromPrefix(TMP_Text text)
    {
        var stack = _tmpSetTextPostfixSkipStack;
        if (stack == null || stack.Count == 0)
        {
            return false;
        }

        var textId = text.GetInstanceID();
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var entry = stack[i];
            if (entry.TextId != textId)
            {
                continue;
            }

            if (entry.RequiresValueMatch)
            {
                return false;
            }

            stack.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static void AddTmpSetTextPostfixSkip(int textId, string? value, bool requiresValueMatch)
    {
        var stack = _tmpSetTextPostfixSkipStack ??= new List<TmpSetTextPostfixSkipEntry>(4);
        if (stack.Count >= 8)
        {
            stack.RemoveAt(0);
        }

        stack.Add(new TmpSetTextPostfixSkipEntry(textId, value, requiresValueMatch));
    }

    private static bool TextEquals(string? expected, string? value)
    {
        return ReferenceEquals(expected, value) ||
               (expected != null &&
                value != null &&
                expected.Length == value.Length &&
                string.Equals(expected, value, StringComparison.Ordinal));
    }

    private static bool FinalizeHudPlanetRiskText(TMP_Text? text)
    {
        if (text == null || !ReferenceEquals(text, HUDManager.Instance?.planetRiskLevelText))
        {
            return false;
        }

        TargetedUiTranslator.FinalizeHudPlanetRiskValue(HUDManager.Instance, text, "TMP_Text.post_set_text.planet-risk");
        return true;
    }

    private static bool ShouldProcessTmpColorHookFast(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        var textId = text.GetInstanceID();
        var isSpecialNativeText = IsNativeRelayColorHookCandidate(text);
        if (!isSpecialNativeText && !TmpColorHookCandidateTextIds.Contains(textId))
        {
            return false;
        }

        var value = text.text;
        if (TmpColorHookEligibilityCache.TryGetValue(textId, out var cached) &&
            TextEquals(cached.Source, value))
        {
            return cached.Value;
        }

        bool result;
        if (!string.IsNullOrEmpty(value) && ContainsCjk(value))
        {
            result = true;
        }
        else
        {
            result = isSpecialNativeText;
            if (!result)
            {
                TmpColorHookCandidateTextIds.Remove(textId);
            }
        }

        CacheTmpColorHookEligibility(textId, value, result);
        return result;
    }

    private static bool IsNativeRelayColorHookCandidate(TMP_Text text)
    {
        var name = text.name;
        return string.Equals(name, "LoadText", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "TipLeft1", StringComparison.OrdinalIgnoreCase);
    }

    internal static void RegisterTmpColorHookCandidate(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        var id = text.GetInstanceID();
        TmpColorHookCandidateTextIds.Add(id, RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    [HarmonyPatch(typeof(TMP_Text), "set_color")]
    [HarmonyPrefix]
    private static void TmpSetColorPrefix(TMP_Text __instance, ref Color value)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        var shouldProcess = ShouldProcessTmpColorHookFast(__instance);
        if (!shouldProcess)
        {
            return;
        }

        if (!TranslationGuard.ShouldTouchGlobalTextStyle(__instance))
        {
            return;
        }

        FontFallbackService.SanitizeAssignedColor(__instance, ref value, __instance.text);
    }

    private static void CacheTmpColorHookEligibility(int textId, string? source, bool value)
    {
        TmpColorHookEligibilityCache.Set(
            textId,
            new CachedTmpColorHookEligibility(source, value),
            RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    [HarmonyPatch(typeof(TMP_FontAsset), "Awake")]
    [HarmonyPostfix]
    private static void TmpFontAssetAwakePostfix(TMP_FontAsset __instance)
    {
        FontFallbackAuditService.RecordFontAssetSnapshot(__instance, "TMP_FontAsset.Awake.before-fallback");
        FontFallbackService.OnFontAssetAwake(__instance);
        FontFallbackAuditService.RecordFontAssetSnapshot(__instance, "TMP_FontAsset.Awake.after-fallback");
    }

    private static void AnimatorSetTriggerPrefix(Animator __instance, string name)
    {
        if (__instance == null || name != "displayStats")
        {
            return;
        }

        var hud = HUDManager.Instance;
        if (hud == null || !ReferenceEquals(__instance, hud.endgameStatsAnimator))
        {
            return;
        }

        NormalizeHudEndGameGradeLetterSource(hud.statsUIElements?.gradeLetter);
    }

    private static void AnimatorSetTriggerPostfix(Animator __instance, string name)
    {
        if (__instance == null || name != "RadiationWarning")
        {
            return;
        }

        var hud = HUDManager.Instance;
        if (hud != null && ReferenceEquals(__instance, hud.radiationGraphicAnimator))
        {
            RadiationWarningAuditService.OnRadiationWarningTriggered(hud, "Animator.SetTrigger.RadiationWarning");
            RadiationWarningPlaybackService.OnRadiationWarningTriggered(hud, "Animator.SetTrigger.RadiationWarning");
        }
    }

    private static void AnimatorSetBoolPrefix(Animator __instance, string name, bool value)
    {
        if (__instance == null || name != "IsLoading" || !value)
        {
            return;
        }

        var hud = HUDManager.Instance;
        if (hud == null || !ReferenceEquals(__instance, hud.LoadingScreen))
        {
            return;
        }

        AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(hud, "Animator.SetBool.IsLoading.prefix.true");
    }

    private static void AnimatorSetBoolPostfix(Animator __instance, string name, bool value)
    {
        if (__instance == null ||
            (name != "transmitting" && name != "IsLoading"))
        {
            return;
        }

        var hud = HUDManager.Instance;
        if (hud == null)
        {
            return;
        }

        if (name == "transmitting" && ReferenceEquals(__instance, hud.signalTranslatorAnimator))
        {
            if (value)
            {
                SignalTranslatorLocalizationService.BeginLocalizationWindow(hud, "Animator.SetBool.transmitting.true");
            }
            else
            {
                SignalTranslatorLocalizationService.EndLocalizationWindow();
            }
        }

        if (name != "IsLoading" || !ReferenceEquals(__instance, hud.LoadingScreen))
        {
            return;
        }

        if (value)
        {
            AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(hud, "Animator.SetBool.IsLoading.postfix.true");
        }
        else
        {
            AlertTextureReplacementService.HideEnteringAtmosphereOverlayForHud(hud, "Animator.SetBool.IsLoading.false");
        }
    }

    private static bool TranslateTmpText(TMP_Text __instance, ref string value)
    {
        if (!TmpHookPerfCountersEnabled)
        {
            return TranslateTmpTextCore(__instance, ref value);
        }

        CountTmpPerf(ref _tmpPerfTranslateCalls);
        var perfStart = StartTmpPerfTimer();
        try
        {
            return TranslateTmpTextCore(__instance, ref value);
        }
        finally
        {
            AddTmpPerfElapsed(ref _tmpPerfTranslateTicks, perfStart);
        }
    }

    private static bool TranslateTmpTextCore(TMP_Text __instance, ref string value)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
            return false;
        }

        if (IsLobbySlotDynamicText(__instance))
        {
            ApplyTmpHookFallback(__instance, value);
            TryApplyLobbyNameFallbackGlyphSizing(__instance, value);
            MarkTmpSetTextPostfixSkip(__instance, value);
            return false;
        }

        var eastAsianClass = ClassifyEastAsianDisplayText(value);
        if (eastAsianClass == EastAsianDisplayTextClass.HanOnly && CanTreatCjkTextAsAlreadyLocalized(value))
        {
            ApplyTmpHookFallback(__instance, value, eastAsianClass);
            ApplyBootSplashTypography(__instance, value);
            MarkTmpSetTextPostfixSkip(__instance, value);
            return false;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, value))
        {
            ApplyTmpHookFallback(__instance, value, eastAsianClass);
            MarkTmpSetTextPostfixSkip(__instance, value);
            return false;
        }

        if (ShouldSkipTranslationForEastAsianDisplayText(eastAsianClass))
        {
            ApplyTmpHookFallback(__instance, value, eastAsianClass);
            MarkTmpSetTextPostfixSkip(__instance, value);
            return false;
        }

        if (eastAsianClass != EastAsianDisplayTextClass.None)
        {
            ApplyTmpHookFallback(__instance, value, eastAsianClass);
        }

        ApplyBootSplashTypography(__instance, value);
        if (TranslationService.TryTranslateKnownDynamicTextFast(value, out var translated) ||
            TranslationService.TryTranslateFastExact(value, out translated))
        {
            var source = value;
            value = translated;
            CacheTmpHookTranslation(__instance, source, translated);
            ApplyTmpHookFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, translated);
            Plugin.ReportTranslationHit();
            return true;
        }

        if (AutomaticTranslationService.IsEnabled &&
            AutomaticTranslationService.TryTranslateOrQueue(value, out translated))
        {
            var source = value;
            value = translated;
            CacheTmpHookTranslation(__instance, source, translated);
            ApplyTmpHookFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, translated);
            Plugin.ReportTranslationHit();
            return true;
        }

        RecordTmpRuntimeText(__instance, value);
        if (CacheUntranslatedTmpHookNoop(__instance, value))
        {
            MarkTmpSetTextPostfixSkip(__instance, value);
        }

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
    }

    [HarmonyPriority(Priority.Last)]
    private static void UiTextSetTextPostfix(Text __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (!CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            return;
        }

        CustomLocalizationExtensionService.ApplyStyle(__instance, __instance.text);
    }

    private static bool TranslateUiText(Text __instance, ref string value)
    {
        if (ShouldSkipGlobalNonTmpTextHook(value))
        {
            return false;
        }

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

        if (AutomaticTranslationService.IsEnabled &&
            AutomaticTranslationService.TryTranslateOrQueue(value, out translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
            return true;
        }

        if (RuntimeTextCollector.IsEnabled)
        {
            RuntimeTextCollector.Record(__instance, value);
        }

        CacheTmpHookSourceNoop(value);
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

        if (ShouldSkipGlobalNonTmpTextHook(value))
        {
            return;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, value))
        {
            return;
        }

        if (TranslationService.TryTranslateKnownDynamicTextFast(value, out var translated) ||
            TranslationService.TryTranslateFastExact(value, out translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
        }
        else if (AutomaticTranslationService.IsEnabled &&
                 AutomaticTranslationService.TryTranslateOrQueue(value, out translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
        }
        else
        {
            if (RuntimeTextCollector.IsEnabled)
            {
                RuntimeTextCollector.Record(__instance, value);
            }

            CacheTmpHookSourceNoop(value);
        }

    }

    [HarmonyPriority(Priority.Last)]
    private static void TextMeshSetTextPostfix(TextMesh __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (!CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            return;
        }

        CustomLocalizationExtensionService.ApplyStyle(__instance, __instance.text);
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

    private static void TryApplyLobbyNameFallbackGlyphSizing(TMP_Text? text, string? value)
    {
        if (text == null)
        {
            return;
        }

        var slot = text.GetComponentInParent<LobbySlot>(true);
        if (slot == null || !ReferenceEquals(slot.LobbyName, text))
        {
            return;
        }

        var id = text.GetInstanceID();
        var parentId = GetParentInstanceId(text);
        if (!LobbyNameTypographyCache.TryGetValue(id, out var baseline) || baseline.ParentId != parentId)
        {
            baseline = new CachedLobbyNameTypography(parentId, text.enableAutoSizing, text.fontSize, text.fontSizeMin, text.fontSizeMax);
            LobbyNameTypographyCache.Set(id, baseline, RuntimePerformanceSettings.TmpHookCacheLimit);
        }

        if (!ContainsLobbyNameFallbackGlyphCandidate(value))
        {
            RestoreLobbyNameTypography(text, baseline);
            return;
        }

        if (!baseline.EnableAutoSizing && !text.enableAutoSizing)
        {
            return;
        }

        var maxFontSize = Mathf.Max(baseline.FontSizeMax, baseline.FontSize, text.fontSizeMax, text.fontSize);
        if (maxFontSize <= 0f)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(text.fontSizeMax, maxFontSize);
        text.fontSizeMin = Mathf.Min(text.fontSizeMax, Mathf.Max(text.fontSizeMin, baseline.FontSizeMin, maxFontSize * LobbyNameFallbackGlyphMinFontScale));
        if (text.fontSize < text.fontSizeMin)
        {
            text.fontSize = text.fontSizeMin;
        }
    }

    private static void RestoreLobbyNameTypography(TMP_Text text, CachedLobbyNameTypography baseline)
    {
        text.enableAutoSizing = baseline.EnableAutoSizing;
        text.fontSizeMin = baseline.FontSizeMin;
        text.fontSizeMax = baseline.FontSizeMax;
        text.fontSize = baseline.FontSize;
    }

    private static bool ContainsLobbyNameFallbackGlyphCandidate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch > '\u007f' && !char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCachedTextClassification(
        TMP_Text text,
        BoundedCache<int, CachedTextClassification> cache,
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

    private static void CacheTextClassification(TMP_Text text, BoundedCache<int, CachedTextClassification> cache, bool value)
    {
        var id = text.GetInstanceID();
        cache.Set(id, new CachedTextClassification(GetParentInstanceId(text), value), RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    private static int GetParentInstanceId(TMP_Text text)
    {
        var parent = text.transform == null ? null : text.transform.parent;
        return parent == null ? 0 : parent.GetInstanceID();
    }

    private static int GetFontInstanceId(TMP_Text text)
    {
        return text.font == null ? 0 : text.font.GetInstanceID();
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBoolPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (TryNormalizeAdvancedFeaturesGradeText(__instance, ref sourceText))
        {
            return;
        }

        if (TryTranslateHudPlanetRiskText(__instance, ref sourceText))
        {
            return;
        }

        if (CanSkipSetTextNumericFormatFast(sourceText))
        {
            CacheTmpHookSourceNoop(sourceText);
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        if (ShouldSkipGlobalTmpTextHook(__instance, sourceText))
        {
            MarkTmpSetTextPostfixSkip(__instance, sourceText);
            return;
        }

        TmpSetTextPrefixCore(__instance, ref sourceText, skipAlreadyChecked: true);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(float))]
    [HarmonyPrefix]
    private static void TmpSetTextStringFloatPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (TryNormalizeAdvancedFeaturesGradeText(__instance, ref sourceText))
        {
            return;
        }

        if (TryTranslateHudPlanetRiskText(__instance, ref sourceText))
        {
            return;
        }

        if (CanSkipSetTextNumericFormatFast(sourceText))
        {
            CacheTmpHookSourceNoop(sourceText);
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        if (ShouldSkipGlobalTmpTextHook(__instance, sourceText))
        {
            MarkTmpSetTextPostfixSkip(__instance, sourceText);
            return;
        }

        TmpSetTextPrefixCore(__instance, ref sourceText, skipAlreadyChecked: true);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(StringBuilder))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBuilderPrefix(TMP_Text __instance, ref StringBuilder sourceText)
    {
        if (TryNormalizeAdvancedFeaturesGradeText(__instance, ref sourceText))
        {
            return;
        }

        if (TryTranslateHudPlanetRiskText(__instance, ref sourceText))
        {
            return;
        }

        if (CanSkipGlobalTmpTextHookFast(sourceText))
        {
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        if (CanSkipStrongStatusStringBuilderFast(sourceText))
        {
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        if (IsInputFieldTextComponent(__instance))
        {
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        if (sourceText.Length > GlobalStringBuilderTranslationLengthLimit)
        {
            return;
        }

        if (!MightContainTranslatableStringBuilderText(sourceText))
        {
            MarkTmpSetTextPostfixSkip(__instance);
            return;
        }

        var rawText = sourceText.ToString();
        if (ShouldSkipGlobalTmpTextHook(__instance, rawText))
        {
            MarkTmpSetTextPostfixSkip(__instance, rawText);
            return;
        }

        if (TryApplyCachedTmpHookTranslation(__instance, ref rawText))
        {
            sourceText = new StringBuilder(rawText);
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, rawText);
            return;
        }

        var eastAsianClass = ClassifyEastAsianDisplayText(rawText);
        if (eastAsianClass == EastAsianDisplayTextClass.HanOnly && CanTreatCjkTextAsAlreadyLocalized(rawText))
        {
            ApplyTmpHookFallback(__instance, rawText, eastAsianClass);
            ApplyBootSplashTypography(__instance, rawText);
            MarkTmpSetTextPostfixSkip(__instance, rawText);
            return;
        }

        if (IsLobbySlotDynamicText(__instance))
        {
            ApplyTmpHookFallback(__instance, rawText, eastAsianClass);
            TryApplyLobbyNameFallbackGlyphSizing(__instance, rawText);
            MarkTmpSetTextPostfixSkip(__instance, rawText);
            return;
        }

        if (!TranslationGuard.ShouldTranslateGlobalText(__instance, rawText))
        {
            ApplyTmpHookFallback(__instance, rawText, eastAsianClass);
            MarkTmpSetTextPostfixSkip(__instance, rawText);
            return;
        }

        if (ShouldSkipTranslationForEastAsianDisplayText(eastAsianClass))
        {
            ApplyTmpHookFallback(__instance, rawText, eastAsianClass);
            MarkTmpSetTextPostfixSkip(__instance, rawText);
            return;
        }

        if (eastAsianClass != EastAsianDisplayTextClass.None)
        {
            ApplyTmpHookFallback(__instance, rawText, eastAsianClass);
        }

        ApplyBootSplashTypography(__instance, rawText);
        if (TranslationService.TryTranslateKnownDynamicTextFast(rawText, out var translated) ||
            TranslationService.TryTranslateFastExact(rawText, out translated))
        {
            var source = rawText;
            sourceText = new StringBuilder(translated);
            CacheTmpHookTranslation(__instance, source, translated);
            ApplyTmpHookFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, translated);
            Plugin.ReportTranslationHit();
        }
        else if (AutomaticTranslationService.IsEnabled &&
                 AutomaticTranslationService.TryTranslateOrQueue(rawText, out translated))
        {
            var source = rawText;
            sourceText = new StringBuilder(translated);
            CacheTmpHookTranslation(__instance, source, translated);
            ApplyTmpHookFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            MarkTmpSetTextPostfixSkipForTranslatedOutput(__instance, translated);
            Plugin.ReportTranslationHit();
        }
        else
        {
            RecordTmpRuntimeText(__instance, rawText);
            if (CacheUntranslatedTmpHookNoop(__instance, rawText))
            {
                MarkTmpSetTextPostfixSkip(__instance, rawText);
            }
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

    private static bool CanSkipStrongStatusStringBuilderFast(StringBuilder value)
    {
        if (value.Length == 0 || value.Length > GenericStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasStrongMarker = false;
        var colon = -1;
        var bracketDepth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsCjk(ch) || ch == '<' || ch == '>')
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (ch is '$' or '%')
            {
                hasStrongMarker = true;
                continue;
            }

            if (ch == '[')
            {
                bracketDepth++;
                continue;
            }

            if (ch == ']')
            {
                bracketDepth = Math.Max(0, bracketDepth - 1);
                continue;
            }

            if (ch == ':' && colon < 0 && bracketDepth == 0)
            {
                colon = i;
                continue;
            }
        }

        if (!hasDigit)
        {
            return false;
        }

        if (LooksLikeCompactMetricStatusStringBuilderShape(value))
        {
            return true;
        }

        if (LooksLikeAcronymMetricStatusStringBuilderShape(value))
        {
            return true;
        }

        return colon > 0 &&
               LooksLikeStringBuilderStatusLabelValue(value, colon, hasStrongMarker);
    }

    private static bool LooksLikeGenericStatusStringBuilderShape(StringBuilder value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsAsciiLetter(ch) ||
                char.IsDigit(ch) ||
                char.IsWhiteSpace(ch) ||
                ch is ':' or '$' or '%' or '/' or '(' or ')' or '-' or '+' or '.' or ',' or '[' or ']' or '<' or '>' or '=' or '#' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool LooksLikeCompactMetricStatusStringBuilderShape(StringBuilder value)
    {
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasLetter = false;
        var hasMarker = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsCjk(ch) || ch == ':' || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (ch is '$' or '%' or '#')
            {
                hasMarker = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '+' or '.' or ',' or '/' or '(' or ')' or '[' or ']' or '_')
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasLetter && hasMarker;
    }

    private static bool LooksLikeStringBuilderStatusLabelValue(StringBuilder value, int colon, bool hasStrongMarker)
    {
        if (colon <= 0 || colon > 24 || colon >= value.Length - 1)
        {
            return false;
        }

        if (!LooksLikeShortStatusLabel(value, 0, colon))
        {
            return false;
        }

        var label = value.ToString(0, colon).Trim();
        if (ExternalEnglishCompatibilityService.MightTranslateStatusLikeLabelCheap(label))
        {
            return false;
        }

        var hasPayloadDigit = false;
        var hasPayloadMarker = hasStrongMarker;
        for (var i = colon + 1; i < value.Length; i++)
        {
            var ch = value[i];
            if (char.IsDigit(ch))
            {
                hasPayloadDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch) || char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (ch is '$' or '%' or '/' or '(' or ')' or '[' or ']' or '#')
            {
                hasPayloadMarker = true;
                continue;
            }

            if (ch == '-')
            {
                hasPayloadMarker = true;
                continue;
            }

            if (ch is '+' or '.' or ',')
            {
                continue;
            }

            return false;
        }

        return hasPayloadDigit && hasPayloadMarker && LooksLikeGenericStatusStringBuilderShape(value);
    }

    private static bool LooksLikeAcronymMetricStatusStringBuilderShape(StringBuilder value)
    {
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasAcronymLetter = false;
        var acronymLetters = 0;
        var separatorSeen = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsCjk(ch) || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (!separatorSeen)
            {
                if (ch is >= 'A' and <= 'Z')
                {
                    hasAcronymLetter = true;
                    acronymLetters++;
                    if (acronymLetters > 8)
                    {
                        return false;
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch) || ch == ':' || ch == '-' || ch == '_')
                {
                    separatorSeen = hasAcronymLetter;
                    continue;
                }

                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '$' or '%' or '#' or '/' or ':' or '-' or '+' or '.' or ',' or '(' or ')' or '[' or ']')
            {
                continue;
            }

            return false;
        }

        return hasAcronymLetter && separatorSeen && hasDigit;
    }

    private static bool CanSkipGlobalTmpTextHookFast(string? value)
    {
        return CanSkipGlobalTmpTextHookFast(value, TmpTextShape.From(value));
    }

    private static bool CanSkipGlobalTmpTextHookFast(string? value, TmpTextShape shape)
    {
        if (shape.IsNullOrEmpty)
        {
            return true;
        }

        if (shape.Length == 1)
        {
            return value != null && IsSingleInertDynamicChar(value[0]);
        }

        if (shape.Length > ShortInertDynamicTextLengthLimit)
        {
            return false;
        }

        if (value == null)
        {
            return true;
        }

        if (LooksLikeStandaloneKeyToken(value))
        {
            return true;
        }

        if (shape.HasAsciiLetter || shape.HasCjk || !shape.HasDigit)
        {
            return false;
        }

        return LooksLikeShortInertDynamicValue(value);
    }

    private static bool CanSkipSetTextNumericFormatFast(string? value)
    {
        if (TryGetCachedTmpHookSourceNoop(value))
        {
            CountTmpPerf(ref _tmpPerfSkipHits);
            return true;
        }

        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > TmpNumericFormatTextLengthLimit ||
            ContainsCjk(value))
        {
            return false;
        }

        var hasPlaceholder = false;
        var hasAsciiLetter = false;
        var hasLowercase = false;
        var hasUppercase = false;
        var hasStrongMetricMarker = false;
        var hasColon = false;
        var firstPlaceholder = value.Length;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '\r' || ch == '\n' || ch == '<' || ch == '>')
            {
                return false;
            }

            if (ch == '{')
            {
                if (!TrySkipNumericFormatPlaceholder(value, i, out var close))
                {
                    return false;
                }

                hasPlaceholder = true;
                firstPlaceholder = Math.Min(firstPlaceholder, i);
                i = close;
                continue;
            }

            if (ch == '}')
            {
                return false;
            }

            if (IsAsciiLetter(ch))
            {
                hasAsciiLetter = true;
                if (ch is >= 'a' and <= 'z')
                {
                    hasLowercase = true;
                }
                else
                {
                    hasUppercase = true;
                }

                continue;
            }

            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '-' or '+' or '.' or ',' or '_' or '[' or ']')
            {
                continue;
            }

            if (ch is '$' or '%' or '/' or '(' or ')')
            {
                hasStrongMetricMarker = true;
                continue;
            }

            if (ch == ':')
            {
                hasColon = true;
                continue;
            }

            return false;
        }

        if (!hasPlaceholder || !hasAsciiLetter)
        {
            return false;
        }

        if (!hasStrongMetricMarker && !hasColon && (!hasUppercase || hasLowercase))
        {
            return false;
        }

        if (TranslationService.LooksLikeControlTipTextCheap(value) ||
            TranslationService.MaybeKnownDynamicTextCheap(value) ||
            TranslationService.TryTranslateKnownDynamicTextFast(value, out _) ||
            ExternalEnglishCompatibilityService.MightTranslateStatusLikeTextCheap(value) ||
            (hasColon && LooksLikeKnownTranslatableFormatLabel(value, firstPlaceholder)))
        {
            return false;
        }

        return true;
    }

    private static bool TrySkipNumericFormatPlaceholder(string value, int open, out int close)
    {
        close = -1;
        var i = open + 1;
        if (i >= value.Length || !char.IsDigit(value[i]))
        {
            return false;
        }

        i++;
        while (i < value.Length && char.IsDigit(value[i]))
        {
            i++;
        }

        for (; i < value.Length && i <= open + 24; i++)
        {
            var ch = value[i];
            if (ch == '}')
            {
                close = i;
                return true;
            }

            if (ch == '{' || ch == '<' || ch == '>' || ch == '\r' || ch == '\n')
            {
                return false;
            }
        }

        return false;
    }

    private static bool LooksLikeKnownTranslatableFormatLabel(string value, int beforeIndex)
    {
        var end = Math.Min(beforeIndex, value.Length);
        while (end > 0 && (char.IsWhiteSpace(value[end - 1]) || value[end - 1] is ':' or '-' or '/' or '#' or '(' or '['))
        {
            end--;
        }

        if (end <= 0)
        {
            return false;
        }

        var start = end - 1;
        while (start >= 0)
        {
            var ch = value[start];
            if (!IsAsciiLetter(ch) && !char.IsWhiteSpace(ch) && ch != '-' && ch != '_')
            {
                break;
            }

            start--;
        }

        var label = value.Substring(start + 1, end - start - 1).Trim();
        return label.Length > 0 && ExternalEnglishCompatibilityService.MightTranslateStatusLikeLabelCheap(label);
    }

    private static bool ShouldSkipGlobalNonTmpTextHook(string? value)
    {
        var shape = TmpTextShape.From(value);
        if (CanSkipGlobalTmpTextHookFast(value, shape))
        {
            return true;
        }

        if (TryGetCachedTmpHookSourceNoop(value, shape))
        {
            return true;
        }

        return CanSkipGenericStatusTextFast(value, shape, out _);
    }

    private static bool CanTreatCjkTextAsAlreadyLocalized(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            RuntimeTextCollector.IsEnabled ||
            AutomaticTranslationService.IsEnabled ||
            CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            return false;
        }

        return !HasAsciiLetter(value) || !TranslationService.MaybeKnownDynamicTextCheap(value);
    }

    private static bool ShouldSkipGlobalTmpTextHook(TMP_Text? text, string? value)
    {
        var shape = TmpTextShape.From(value);
        if (CanSkipGlobalTmpTextHookFast(value, shape))
        {
            return true;
        }

        if (TryGetCachedTmpHookSourceNoop(value, shape))
        {
            CountTmpPerf(ref _tmpPerfSkipHits);
            return true;
        }

        if (text != null && TryGetCachedTmpHookNoop(text, value))
        {
            CountTmpPerf(ref _tmpPerfSkipHits);
            return true;
        }

        if (text != null && TryGetCachedTmpHookTranslatedOutput(text, value))
        {
            CountTmpPerf(ref _tmpPerfSkipHits);
            return true;
        }

        if (CanSkipGenericStatusTextFast(value, shape, out var statusFailReason))
        {
            CountTmpPerf(ref _tmpPerfGenericStatusHits);
            CacheTmpHookNoop(text, value, activateShape: true);
            CountTmpPerf(ref _tmpPerfSkipHits);
            return true;
        }

        CountGenericStatusFailure(statusFailReason);
        CountTmpPerf(ref _tmpPerfSkipMisses);
        return false;
    }

    internal static void MarkTmpHookTranslatedOutput(TMP_Text? text, string? translated)
    {
        if (text == null || string.IsNullOrEmpty(translated))
        {
            return;
        }

        CacheTmpHookTranslation(text, source: null, translated);
    }

    internal static void MarkTmpHookTranslation(TMP_Text? text, string? source, string? translated)
    {
        CacheTmpHookTranslation(text, source, translated);
    }

    private static bool TryApplyCachedTmpHookTranslation(TMP_Text text, ref string value)
    {
        if (string.IsNullOrEmpty(value) ||
            !TmpHookTranslationCache.TryGetValue(text.GetInstanceID(), out var cached) ||
            string.IsNullOrEmpty(cached.Source) ||
            !string.Equals(cached.Source, value, StringComparison.Ordinal))
        {
            return false;
        }

        value = cached.Translated;
        CountTmpPerf(ref _tmpPerfTranslationCacheHits);
        RefreshCachedTmpHookTranslationFallback(text, cached);
        return true;
    }

    private static bool TryGetCachedTmpHookTranslatedOutput(TMP_Text text, string? value)
    {
        return !CustomLocalizationExtensionService.HasGlobalStyleRules &&
               !string.IsNullOrEmpty(value) &&
               TmpHookTranslationCache.TryGetValue(text.GetInstanceID(), out var cached) &&
               string.Equals(cached.Translated, value, StringComparison.Ordinal);
    }

    private static void CacheTmpHookTranslation(TMP_Text? text, string? source, string? translated)
    {
        if (text == null ||
            string.IsNullOrEmpty(translated) ||
            (!string.IsNullOrEmpty(source) && string.Equals(source, translated, StringComparison.Ordinal)))
        {
            return;
        }

        TmpHookTranslationCache.Set(text.GetInstanceID(), new CachedTmpHookTranslation(
            source,
            translated,
            GetFontInstanceId(text)), RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    private static void RefreshCachedTmpHookTranslationFallback(TMP_Text text, CachedTmpHookTranslation cached)
    {
        var eastAsianClass = ClassifyEastAsianDisplayText(cached.Translated);
        if (cached.FontId == GetFontInstanceId(text) || eastAsianClass == EastAsianDisplayTextClass.None)
        {
            return;
        }

        ApplyTmpHookFallback(text, cached.Translated, eastAsianClass);
        CacheTmpHookTranslation(text, cached.Source, cached.Translated);
    }

    private static bool TryGetCachedTmpHookNoop(TMP_Text text, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (!TmpHookNoopCache.TryGetValue(text.GetInstanceID(), out var cached))
        {
            return false;
        }

        if (string.Equals(cached.Source, value, StringComparison.Ordinal))
        {
            CountTmpPerf(ref _tmpPerfExactCacheHits);
            return true;
        }

        if (cached.ShapeActive &&
            cached.ShapeHash != 0UL &&
            TryBuildTmpHookNoopShapeHash(value, out var shapeHash, out var shapeLength) &&
            cached.ShapeHash == shapeHash &&
            cached.ShapeLength == shapeLength)
        {
            CountTmpPerf(ref _tmpPerfShapeCacheHits);
            return true;
        }

        if (cached.ComponentBypassActive && CanSkipComponentBypassText(value))
        {
            CountTmpPerf(ref _tmpPerfComponentBypassHits);
            return true;
        }

        return false;
    }

    private static void CacheTmpHookNoop(
        TMP_Text? text,
        string? value,
        bool activateShape = false,
        bool countComponentMiss = false)
    {
        if (text == null || string.IsNullOrEmpty(value))
        {
            return;
        }

        var textId = text.GetInstanceID();
        var hasShape = TryBuildTmpHookNoopShapeHash(value, out var shapeHash, out var shapeLength);
        var shapeCount = hasShape ? 1 : 0;
        var shapeActive = activateShape && hasShape;
        var componentMissCount = countComponentMiss ? 1 : 0;
        var componentBypassActive = false;

        if (TmpHookNoopCache.TryGetValue(textId, out var cached))
        {
            if (hasShape &&
                cached.ShapeHash == shapeHash &&
                cached.ShapeLength == shapeLength)
            {
                shapeCount = Math.Min(cached.ShapeCount + 1, TmpHookShapeCacheWarmupCount);
                shapeActive = shapeActive || cached.ShapeActive || shapeCount >= TmpHookShapeCacheWarmupCount;
            }
            else if (!hasShape)
            {
                shapeCount = 0;
                shapeActive = false;
            }

            if (countComponentMiss)
            {
                componentMissCount = Math.Min(cached.ComponentMissCount + 1, TmpHookComponentBypassWarmupCount);
            }
            else
            {
                componentMissCount = cached.ComponentMissCount;
            }

            componentBypassActive = cached.ComponentBypassActive ||
                                    (countComponentMiss && componentMissCount >= TmpHookComponentBypassWarmupCount);
        }

        TmpHookNoopCache.Set(textId, new CachedTmpHookNoop(
            value,
            hasShape ? shapeHash : 0UL,
            hasShape ? shapeLength : 0,
            shapeCount,
            shapeActive,
            componentMissCount,
            componentBypassActive), RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    private static bool CacheUntranslatedTmpHookNoop(TMP_Text? text, string? value)
    {
        if (!CanCacheUntranslatedTmpHookNoop(value))
        {
            return false;
        }

        CacheTmpHookSourceNoop(value!);
        CacheTmpHookNoop(text, value, countComponentMiss: CanCountComponentBypassMiss(value!));
        return true;
    }

    private static bool TryGetCachedTmpHookSourceNoop(string? value)
    {
        return TryGetCachedTmpHookSourceNoop(value, TmpTextShape.From(value));
    }

    private static bool TryGetCachedTmpHookSourceNoop(string? value, TmpTextShape shape)
    {
        if (!CanLookupTmpHookSourceNoopCache(value, shape))
        {
            return false;
        }

        return TmpHookSourceNoopCache.Contains(value!);
    }

    private static void CacheTmpHookSourceNoop(string value)
    {
        if (!CanUseTmpHookSourceNoopCache(value, TmpTextShape.From(value)))
        {
            return;
        }

        TmpHookSourceNoopCache.Add(value, RuntimePerformanceSettings.TmpHookCacheLimit);
    }

    private static bool CanLookupTmpHookSourceNoopCache(string? value)
    {
        return CanLookupTmpHookSourceNoopCache(value, TmpTextShape.From(value));
    }

    private static bool CanLookupTmpHookSourceNoopCache(string? value, TmpTextShape shape)
    {
        return !shape.IsNullOrWhiteSpace &&
               shape.Length <= UntranslatedNoopCacheLengthLimit &&
               !RuntimeTextCollector.IsEnabled &&
               !AutomaticTranslationService.IsEnabled &&
               !CustomLocalizationExtensionService.HasGlobalStyleRules;
    }

    private static bool CanUseTmpHookSourceNoopCache(string? value)
    {
        return CanUseTmpHookSourceNoopCache(value, TmpTextShape.From(value));
    }

    private static bool CanUseTmpHookSourceNoopCache(string? value, TmpTextShape shape)
    {
        return CanLookupTmpHookSourceNoopCache(value, shape) &&
               !shape.HasCjk &&
               shape.HasAsciiLetter &&
               !TranslationService.MaybeKnownDynamicTextCheap(value);
    }

    private static bool CanCacheUntranslatedTmpHookNoop(string? value)
    {
        return CanCacheUntranslatedTmpHookNoop(value, TmpTextShape.From(value));
    }

    private static bool CanCacheUntranslatedTmpHookNoop(string? value, TmpTextShape shape)
    {
        if (shape.IsNullOrWhiteSpace ||
            shape.Length > UntranslatedNoopCacheLengthLimit ||
            AutomaticTranslationService.IsEnabled ||
            CustomLocalizationExtensionService.HasGlobalStyleRules)
        {
            return false;
        }

        return !shape.HasCjk &&
               !TranslationService.MaybeKnownDynamicTextCheap(value) &&
               shape.HasAsciiLetter &&
               (!shape.HasRichTextMarker || TryBuildTmpHookNoopShapeHash(value, out _, out _));
    }

    private static bool CanCountComponentBypassMiss(string value)
    {
        return !RuntimeTextCollector.IsEnabled &&
               value.Length <= TmpHookComponentBypassTextLengthLimit &&
               value.IndexOf('<') < 0;
    }

    private static bool CanSkipComponentBypassText(string value)
    {
        var shape = TmpTextShape.From(value);
        if (RuntimeTextCollector.IsEnabled ||
            shape.IsNullOrWhiteSpace ||
            shape.Length > TmpHookComponentBypassTextLengthLimit ||
            shape.HasRichTextMarker ||
            AutomaticTranslationService.IsEnabled ||
            CustomLocalizationExtensionService.HasGlobalStyleRules ||
            shape.HasCjk ||
            !shape.HasAsciiLetter ||
            TranslationService.MaybeKnownDynamicTextCheap(value))
        {
            return false;
        }

        if (TryBuildTmpHookNoopShapeHash(value, out _, out _))
        {
            return true;
        }

        return !TranslationService.TryTranslateFastExact(value, out _);
    }

    private enum GenericStatusSkipFailure
    {
        None,
        NoMarker,
        ControlTip,
        CjkOrInvalid,
        Shape
    }

    private enum NoAllocGenericStatusKind
    {
        None,
        LabelOrBracket,
        Numeric,
        Metric
    }

    private static bool TryClassifyGenericStatusTextNoAllocFast(string value, out NoAllocGenericStatusKind kind)
    {
        return TryClassifyGenericStatusTextNoAllocFast(value, TmpTextShape.From(value), out kind);
    }

    private static bool TryClassifyGenericStatusTextNoAllocFast(string value, TmpTextShape shape, out NoAllocGenericStatusKind kind)
    {
        kind = NoAllocGenericStatusKind.None;
        var text = StripOuterSimpleRichTextEnvelope(value.AsSpan()).Trim();
        if (text.Length == 0 ||
            text.Length > GenericStatusTextLengthLimit ||
            (shape.HasCjk && ContainsCjk(text)))
        {
            return false;
        }

        if (LooksLikeLabelStatusLine(text) || LooksLikeBracketedStatusToken(text))
        {
            kind = NoAllocGenericStatusKind.LabelOrBracket;
            return true;
        }

        if (LooksLikeNumericStatusValue(text))
        {
            kind = NoAllocGenericStatusKind.Numeric;
            return true;
        }

        if (LooksLikeCompactMetricStatusText(text) ||
            LooksLikeAcronymMetricStatusText(text) ||
            LooksLikeVolatileMetricShape(text))
        {
            kind = NoAllocGenericStatusKind.Metric;
            return true;
        }

        return false;
    }

    private static bool CanSkipGenericStatusTextFast(string? value) =>
        CanSkipGenericStatusTextFast(value, out _);

    private static bool CanSkipGenericStatusTextFast(string? value, out GenericStatusSkipFailure failReason)
    {
        return CanSkipGenericStatusTextFast(value, TmpTextShape.From(value), out failReason);
    }

    private static bool CanSkipGenericStatusTextFast(string? value, TmpTextShape shape, out GenericStatusSkipFailure failReason)
    {
        if (shape.IsNullOrWhiteSpace || shape.Length > GenericStatusTextLengthLimit)
        {
            failReason = GenericStatusSkipFailure.Shape;
            return false;
        }

        if (!shape.HasGenericStatusMarker)
        {
            failReason = GenericStatusSkipFailure.NoMarker;
            return false;
        }

        if (TranslationService.LooksLikeControlTipTextCheap(value))
        {
            failReason = GenericStatusSkipFailure.ControlTip;
            return false;
        }

        if (value == null)
        {
            failReason = GenericStatusSkipFailure.Shape;
            return false;
        }

        if (TryClassifyGenericStatusTextNoAllocFast(value, shape, out var noAllocKind))
        {
            if (noAllocKind == NoAllocGenericStatusKind.LabelOrBracket &&
                ExternalEnglishCompatibilityService.MightTranslateStatusLikeTextCheap(value))
            {
                failReason = GenericStatusSkipFailure.Shape;
                return false;
            }

            if (noAllocKind == NoAllocGenericStatusKind.Metric &&
                TranslationService.MaybeKnownDynamicTextCheap(value))
            {
                failReason = GenericStatusSkipFailure.Shape;
                return false;
            }

            failReason = GenericStatusSkipFailure.None;
            return true;
        }

        var text = StripOuterSimpleRichTextEnvelope(value);
        if (text.Length == 0 ||
            text.Length > GenericStatusTextLengthLimit ||
            (shape.HasCjk && ContainsCjk(text)))
        {
            failReason = GenericStatusSkipFailure.CjkOrInvalid;
            return false;
        }

        if (LooksLikeLabelStatusLine(text) || LooksLikeBracketedStatusToken(text))
        {
            if (ExternalEnglishCompatibilityService.MightTranslateStatusLikeTextCheap(value))
            {
                failReason = GenericStatusSkipFailure.Shape;
                return false;
            }

            failReason = GenericStatusSkipFailure.None;
            return true;
        }

        if (TranslationService.MaybeKnownDynamicTextCheap(value))
        {
            failReason = GenericStatusSkipFailure.Shape;
            return false;
        }

        if (LooksLikeCompactMetricStatusText(text))
        {
            failReason = GenericStatusSkipFailure.None;
            return true;
        }

        if (LooksLikeAcronymMetricStatusText(text))
        {
            failReason = GenericStatusSkipFailure.None;
            return true;
        }

        var result = LooksLikeNumericStatusValue(text);
        failReason = result ? GenericStatusSkipFailure.None : GenericStatusSkipFailure.Shape;
        return result;
    }

    private static bool HasGenericStatusMarker(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is '$' or '%' or '/' or ':' or '[' or ']' or '(' or ')' or '<' or '>')
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<char> StripOuterSimpleRichTextEnvelope(ReadOnlySpan<char> value)
    {
        var text = value.Trim();
        for (var depth = 0; depth < 3; depth++)
        {
            if (text.Length < 7 || text[0] != '<')
            {
                break;
            }

            var tagClose = text.IndexOf('>');
            if (tagClose <= 1 || tagClose > 24)
            {
                break;
            }

            var tagNameLength = 0;
            for (var i = 1; i < tagClose; i++)
            {
                var ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '=')
                {
                    break;
                }

                if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
                {
                    tagNameLength = 0;
                    break;
                }

                tagNameLength++;
            }

            var closingLength = tagNameLength + 3;
            if (tagNameLength == 0 || text.Length <= tagClose + closingLength)
            {
                break;
            }

            var closingStart = text.Length - closingLength;
            if (text[closingStart] != '<' || text[closingStart + 1] != '/' || text[^1] != '>')
            {
                break;
            }

            var matchesClosingTag = true;
            for (var i = 0; i < tagNameLength; i++)
            {
                var open = text[1 + i];
                var close = text[closingStart + 2 + i];
                if (char.ToUpperInvariant(open) != char.ToUpperInvariant(close))
                {
                    matchesClosingTag = false;
                    break;
                }
            }

            if (!matchesClosingTag)
            {
                break;
            }

            text = text.Slice(tagClose + 1, closingStart - tagClose - 1).Trim();
        }

        return text;
    }

    private static bool LooksLikeNumericStatusValue(ReadOnlySpan<char> value)
    {
        var hasDigit = false;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch) || IsCjk(ch) || !IsNumericStatusPunctuation(ch))
            {
                return false;
            }
        }

        return hasDigit;
    }

    private static bool LooksLikeCompactMetricStatusText(ReadOnlySpan<char> value)
    {
        value = StripOuterSimpleRichTextEnvelope(value).Trim();
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasLetter = false;
        var hasMarker = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch) || ch == ':' || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (ch is '$' or '%' or '#')
            {
                hasMarker = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '+' or '.' or ',' or '/' or '(' or ')' or '[' or ']' or '_')
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasLetter && hasMarker;
    }

    private static bool LooksLikeAcronymMetricStatusText(ReadOnlySpan<char> value)
    {
        value = StripOuterSimpleRichTextEnvelope(value).Trim();
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasAcronymLetter = false;
        var acronymLetters = 0;
        var separatorSeen = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch) || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (!separatorSeen)
            {
                if (ch is >= 'A' and <= 'Z')
                {
                    hasAcronymLetter = true;
                    acronymLetters++;
                    if (acronymLetters > 8)
                    {
                        return false;
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch) || ch == ':' || ch == '-' || ch == '_')
                {
                    separatorSeen = hasAcronymLetter;
                    continue;
                }

                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '$' or '%' or '#' or '/' or ':' or '-' or '+' or '.' or ',' or '(' or ')' or '[' or ']')
            {
                continue;
            }

            return false;
        }

        return hasAcronymLetter && separatorSeen && hasDigit;
    }

    private static bool LooksLikeVolatileMetricShape(ReadOnlySpan<char> value)
    {
        if (value.Length == 0 || value.Length > 64)
        {
            return false;
        }

        var hasDigit = false;
        var hasLetter = false;
        var hasSeparator = false;
        var wordCount = 0;
        var inWord = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch) || ch == '\n' || ch == '\r')
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                inWord = false;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                if (!inWord)
                {
                    wordCount++;
                    inWord = true;
                    if (wordCount > 5)
                    {
                        return false;
                    }
                }

                continue;
            }

            inWord = false;
            if (char.IsWhiteSpace(ch) || ch is '%' or '$' or '#' or '/' or ':' or '-' or '+' or '.' or ',' or '(' or ')' or '[' or ']' or '_')
            {
                hasSeparator = true;
                continue;
            }

            return false;
        }

        return hasDigit && hasLetter && hasSeparator;
    }

    private static bool LooksLikeBracketedStatusToken(ReadOnlySpan<char> value)
    {
        value = StripOuterSimpleRichTextEnvelope(value).Trim();
        if (value.Length < 5 || value.Length > 64 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        var colon = value.IndexOf(':');
        if (colon <= 1 || colon >= value.Length - 2)
        {
            colon = value.IndexOf('\uff1a');
        }

        if (colon <= 1 || colon >= value.Length - 2)
        {
            return false;
        }

        for (var i = 1; i < value.Length - 1; i++)
        {
            if (i != value.Length - 1 && value[i] == ']' && i < value.Length - 1)
            {
                return false;
            }
        }

        return LooksLikeShortStatusLabel(value.Slice(1, colon - 1)) &&
               LooksLikeShortStatusTokenValue(value.Slice(colon + 1, value.Length - colon - 2));
    }

    private static bool LooksLikeLabelStatusLine(ReadOnlySpan<char> value)
    {
        var colon = FindTopLevelColon(value);
        if (colon <= 0 || colon > 24 || colon >= value.Length - 1)
        {
            return false;
        }

        var label = value.Slice(0, colon).Trim();
        var payload = value.Slice(colon + 1).Trim();
        if (!LooksLikeShortStatusLabel(label) ||
            !HasLowercaseAsciiLetter(label) ||
            payload.Length == 0 ||
            LooksLikeStandaloneKeyToken(payload))
        {
            return false;
        }

        return LooksLikeStatusPayload(payload);
    }

    private static bool LooksLikeStatusPayload(ReadOnlySpan<char> payload)
    {
        var hasDigit = false;
        var hasStatusMarker = false;
        var hasAsciiToken = false;
        for (var i = 0; i < payload.Length; i++)
        {
            var ch = payload[i];
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (ch == '<' && TrySkipSimpleRichTextTag(payload, i, out var tagEnd))
            {
                i = tagEnd;
                hasStatusMarker = true;
                continue;
            }

            if (ch == '[')
            {
                var close = payload.Slice(i + 1).IndexOf(']');
                if (close < 0)
                {
                    return false;
                }

                close += i + 1;
                if (!LooksLikeBracketedStatusToken(payload.Slice(i, close - i + 1)))
                {
                    return false;
                }

                i = close;
                hasStatusMarker = true;
                continue;
            }

            if (ch is '$' or '%' or '/' or '(' or ')' or '-' or '+' or '.' or ',')
            {
                hasStatusMarker = true;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasAsciiToken = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return false;
        }

        return hasDigit && (hasStatusMarker || hasAsciiToken || payload.Length <= 24);
    }

    private static int FindTopLevelColon(ReadOnlySpan<char> value)
    {
        var inBracket = false;
        var inTag = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>' && inTag)
            {
                inTag = false;
                continue;
            }

            if (inTag)
            {
                continue;
            }

            if (ch == '[')
            {
                inBracket = true;
                continue;
            }

            if (ch == ']' && inBracket)
            {
                inBracket = false;
                continue;
            }

            if (!inBracket && ch == ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool LooksLikeShortStatusLabel(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > 24)
        {
            return false;
        }

        var hasLetter = false;
        foreach (var ch in value)
        {
            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '.')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static bool LooksLikeShortStatusTokenValue(ReadOnlySpan<char> value)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > 32)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (IsAsciiLetter(ch) || char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '.' or '/' or '$' or '%')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool ContainsCjk(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLowercaseAsciiLetter(ReadOnlySpan<char> value)
    {
        foreach (var ch in value)
        {
            if (ch is >= 'a' and <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeStandaloneKeyToken(ReadOnlySpan<char> value)
    {
        if (value.Length < 3 || value.Length > 6 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        for (var i = 1; i < value.Length - 1; i++)
        {
            var ch = value[i];
            if (!IsAsciiLetter(ch) && !char.IsDigit(ch) && ch != ' ' && ch != '/' && ch != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static string StripOuterSimpleRichTextEnvelope(string value)
    {
        var text = value.Trim();
        for (var depth = 0; depth < 3; depth++)
        {
            if (text.Length < 7 || text[0] != '<')
            {
                break;
            }

            var tagClose = text.IndexOf('>');
            if (tagClose <= 1 || tagClose > 24)
            {
                break;
            }

            var tagNameLength = 0;
            for (var i = 1; i < tagClose; i++)
            {
                var ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '=')
                {
                    break;
                }

                if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
                {
                    tagNameLength = 0;
                    break;
                }

                tagNameLength++;
            }

            if (tagNameLength == 0)
            {
                break;
            }

            var tagName = text.Substring(1, tagNameLength);
            var closingTag = "</" + tagName + ">";
            if (!text.EndsWith(closingTag, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            text = text.Substring(tagClose + 1, text.Length - tagClose - 1 - closingTag.Length).Trim();
        }

        return text;
    }

    private static bool LooksLikeNumericStatusValue(string value)
    {
        var hasDigit = false;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch) || IsCjk(ch))
            {
                return false;
            }

            if (!IsNumericStatusPunctuation(ch))
            {
                return false;
            }
        }

        return hasDigit;
    }

    private static bool LooksLikeCompactMetricStatusText(string value)
    {
        value = StripOuterSimpleRichTextEnvelope(value).Trim();
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasLetter = false;
        var hasMarker = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch) || ch == ':' || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (ch is '$' or '%' or '#')
            {
                hasMarker = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '+' or '.' or ',' or '/' or '(' or ')' or '[' or ']' or '_')
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasLetter && hasMarker;
    }

    private static bool LooksLikeAcronymMetricStatusText(string value)
    {
        value = StripOuterSimpleRichTextEnvelope(value).Trim();
        if (value.Length == 0 || value.Length > CompactMetricStatusTextLengthLimit)
        {
            return false;
        }

        var hasDigit = false;
        var hasAcronymLetter = false;
        var acronymLetters = 0;
        var separatorSeen = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch) || ch == '<' || ch == '>' || ch == '!' || ch == '*')
            {
                return false;
            }

            if (!separatorSeen)
            {
                if (ch is >= 'A' and <= 'Z')
                {
                    hasAcronymLetter = true;
                    acronymLetters++;
                    if (acronymLetters > 8)
                    {
                        return false;
                    }

                    continue;
                }

                if (char.IsWhiteSpace(ch) || ch == ':' || ch == '-' || ch == '_')
                {
                    separatorSeen = hasAcronymLetter;
                    continue;
                }

                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '$' or '%' or '#' or '/' or ':' or '-' or '+' or '.' or ',' or '(' or ')' or '[' or ']')
            {
                continue;
            }

            return false;
        }

        return hasAcronymLetter && separatorSeen && hasDigit;
    }

    private static bool LooksLikeBracketedStatusToken(string value)
    {
        value = StripOuterSimpleRichTextEnvelope(value);
        if (value.Length < 5 || value.Length > 64 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        var colon = value.IndexOf(':');
        if (colon <= 1 || colon >= value.Length - 2)
        {
            colon = value.IndexOf('\uff1a');
        }

        if (colon <= 1 || colon >= value.Length - 2 || value.IndexOf(']', 1, value.Length - 2) >= 0)
        {
            return false;
        }

        return LooksLikeShortStatusLabel(value.Substring(1, colon - 1)) &&
               LooksLikeShortStatusTokenValue(value.Substring(colon + 1, value.Length - colon - 2));
    }

    private static bool LooksLikeLabelStatusLine(string value)
    {
        var colon = FindTopLevelColon(value);
        if (colon <= 0 || colon > 24 || colon >= value.Length - 1)
        {
            return false;
        }

        var label = value.Substring(0, colon).Trim();
        var payload = value.Substring(colon + 1).Trim();
        if (!LooksLikeShortStatusLabel(label) ||
            !HasLowercaseAsciiLetter(label) ||
            payload.Length == 0 ||
            LooksLikeStandaloneKeyToken(payload))
        {
            return false;
        }

        return LooksLikeStatusPayload(payload);
    }

    private static bool LooksLikeStatusPayload(string payload)
    {
        var hasDigit = false;
        var hasStatusMarker = false;
        var hasAsciiToken = false;
        for (var i = 0; i < payload.Length; i++)
        {
            var ch = payload[i];
            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (ch == '<' && TrySkipSimpleRichTextTag(payload, i, out var tagEnd))
            {
                i = tagEnd;
                hasStatusMarker = true;
                continue;
            }

            if (ch == '[')
            {
                var close = payload.IndexOf(']', i + 1);
                if (close <= i + 1)
                {
                    return false;
                }

                var token = payload.Substring(i, close - i + 1);
                if (!LooksLikeBracketedStatusToken(token))
                {
                    return false;
                }

                i = close;
                hasStatusMarker = true;
                continue;
            }

            if (ch is '$' or '%' or '/' or '(' or ')' or '-' or '+' or '.' or ',')
            {
                hasStatusMarker = true;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasAsciiToken = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return false;
        }

        return hasDigit && (hasStatusMarker || hasAsciiToken || payload.Length <= 24);
    }

    private static int FindTopLevelColon(string value)
    {
        var inBracket = false;
        var inTag = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>' && inTag)
            {
                inTag = false;
                continue;
            }

            if (inTag)
            {
                continue;
            }

            if (ch == '[')
            {
                inBracket = true;
                continue;
            }

            if (ch == ']' && inBracket)
            {
                inBracket = false;
                continue;
            }

            if (!inBracket && ch == ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool LooksLikeShortStatusLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 24)
        {
            return false;
        }

        var hasLetter = false;
        foreach (var ch in value.Trim())
        {
            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '.')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static bool LooksLikeShortStatusLabel(StringBuilder value, int start, int length)
    {
        if (length <= 0 || length > 24)
        {
            return false;
        }

        var hasLetter = false;
        var end = start + length;
        while (start < end && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        if (start >= end)
        {
            return false;
        }

        for (var i = start; i < end; i++)
        {
            var ch = value[i];
            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '.')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static bool LooksLikeShortStatusTokenValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 32)
        {
            return false;
        }

        foreach (var ch in value.Trim())
        {
            if (IsAsciiLetter(ch) || char.IsDigit(ch) || char.IsWhiteSpace(ch) || ch is '_' or '-' or '.' or '/' or '$' or '%')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAsciiLetter(string value)
    {
        foreach (var ch in value)
        {
            if (IsAsciiLetter(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLowercaseAsciiLetter(string value)
    {
        foreach (var ch in value)
        {
            if (ch is >= 'a' and <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildTmpHookNoopShapeHash(string? value, out ulong shapeHash, out int shapeLength)
    {
        shapeHash = 0UL;
        shapeLength = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length > UntranslatedNoopCacheLengthLimit)
        {
            return false;
        }

        if (TranslationService.LooksLikeControlTipTextCheap(value))
        {
            return false;
        }

        var text = StripOuterSimpleRichTextEnvelope(value.AsSpan()).Trim();
        if (text.Length == 0 || text.Length > UntranslatedNoopCacheLengthLimit || ContainsCjk(text))
        {
            return false;
        }

        if (!LooksLikeGenericStatusShape(text))
        {
            return false;
        }

        const ulong offsetBasis = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        shapeHash = offsetBasis;
        var previous = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '<' && TrySkipSimpleRichTextTag(text, i, out var tagEnd))
            {
                i = tagEnd;
                continue;
            }

            char next;
            if (char.IsDigit(ch))
            {
                next = '#';
            }
            else if (IsAsciiLetter(ch))
            {
                next = char.ToLowerInvariant(ch);
            }
            else if (char.IsWhiteSpace(ch))
            {
                next = ' ';
            }
            else
            {
                next = ch;
            }

            if ((next == '#' || next == ' ') && previous == next)
            {
                continue;
            }

            shapeHash ^= next;
            shapeHash *= prime;
            shapeLength++;
            previous = next;
        }

        if (shapeLength == 0)
        {
            shapeHash = 0UL;
            return false;
        }

        if (shapeHash == 0UL)
        {
            shapeHash = 1UL;
        }

        return true;
    }

    private static bool LooksLikeGenericStatusShape(ReadOnlySpan<char> value)
    {
        if (LooksLikeLabelStatusLine(value) || LooksLikeBracketedStatusToken(value) || LooksLikeNumericStatusValue(value))
        {
            return true;
        }

        var hasDigit = false;
        var hasMarker = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '<' && TrySkipSimpleRichTextTag(value, i, out var tagEnd))
            {
                hasMarker = true;
                i = tagEnd;
                continue;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (ch is '$' or '%' or '/' or ':' or '[' or ']' or '(' or ')' or '-' or '+' or '.' or ',')
            {
                hasMarker = true;
                continue;
            }

            if (IsAsciiLetter(ch) || char.IsWhiteSpace(ch))
            {
                continue;
            }

            return false;
        }

        return hasDigit && hasMarker ||
               LooksLikeAcronymMetricStatusText(value) ||
               LooksLikeVolatileMetricShape(value);
    }

    private static bool LooksLikeVolatileMetricShape(string value)
    {
        if (value.Length == 0 || value.Length > 64 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
        {
            return false;
        }

        var hasDigit = false;
        var hasLetter = false;
        var hasSeparator = false;
        var wordCount = 0;
        var inWord = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                inWord = false;
                continue;
            }

            if (IsAsciiLetter(ch))
            {
                hasLetter = true;
                if (!inWord)
                {
                    wordCount++;
                    inWord = true;
                    if (wordCount > 5)
                    {
                        return false;
                    }
                }

                continue;
            }

            inWord = false;
            if (char.IsWhiteSpace(ch))
            {
                hasSeparator = true;
                continue;
            }

            if (ch is '%' or '$' or '#' or '/' or ':' or '-' or '+' or '.' or ',' or '(' or ')' or '[' or ']' or '_')
            {
                hasSeparator = true;
                continue;
            }

            return false;
        }

        return hasDigit && hasLetter && hasSeparator;
    }

    private static bool TrySkipSimpleRichTextTag(ReadOnlySpan<char> value, int start, out int tagEnd)
    {
        tagEnd = start;
        if (start < 0 || start >= value.Length || value[start] != '<')
        {
            return false;
        }

        var close = value.Slice(start + 1).IndexOf('>');
        if (close < 0)
        {
            return false;
        }

        close += start + 1;
        if (close <= start + 1 || close - start > 32)
        {
            return false;
        }

        var cursor = start + 1;
        if (cursor < close && value[cursor] == '/')
        {
            cursor++;
        }

        var hasTagName = false;
        for (; cursor < close; cursor++)
        {
            var ch = value[cursor];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            hasTagName = true;
        }

        tagEnd = close;
        return hasTagName;
    }

    private static bool TrySkipSimpleRichTextTag(string value, int start, out int tagEnd)
    {
        tagEnd = start;
        if (start < 0 || start >= value.Length || value[start] != '<')
        {
            return false;
        }

        var close = value.IndexOf('>', start + 1);
        if (close <= start + 1 || close - start > 32)
        {
            return false;
        }

        var cursor = start + 1;
        if (cursor < close && value[cursor] == '/')
        {
            cursor++;
        }

        var hasTagName = false;
        for (; cursor < close; cursor++)
        {
            var ch = value[cursor];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            hasTagName = true;
        }

        tagEnd = close;
        return hasTagName;
    }

    private static bool IsNumericStatusPunctuation(char ch)
    {
        return char.IsWhiteSpace(ch) ||
               ch is '-' or '+' or '.' or ',' or '/' or ':' or '$' or '%' or '#' or '[' or ']' or '(' or ')';
    }

    private static bool CanSkipGlobalTmpTextHookFast(StringBuilder? value)
    {
        if (value == null || value.Length == 0)
        {
            return true;
        }

        if (value.Length > ShortInertDynamicTextLengthLimit)
        {
            return false;
        }

        if (value.Length == 1)
        {
            return IsSingleInertDynamicChar(value[0]);
        }

        if (LooksLikeStandaloneKeyToken(value))
        {
            return true;
        }

        return LooksLikeShortInertDynamicValue(value);
    }

    private static bool LooksLikeStandaloneKeyToken(string value)
    {
        if (value.Length < 3 || value.Length > 6 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        for (var i = 1; i < value.Length - 1; i++)
        {
            var ch = value[i];
            if (!IsAsciiLetter(ch) && !char.IsDigit(ch) && ch != ' ' && ch != '/' && ch != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeStandaloneKeyToken(StringBuilder value)
    {
        if (value.Length < 3 || value.Length > 6 || value[0] != '[' || value[value.Length - 1] != ']')
        {
            return false;
        }

        for (var i = 1; i < value.Length - 1; i++)
        {
            var ch = value[i];
            if (!IsAsciiLetter(ch) && !char.IsDigit(ch) && ch != ' ' && ch != '/' && ch != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeShortInertDynamicValue(string value)
    {
        var hasDigit = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsAsciiLetter(ch) || IsCjk(ch))
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (!IsInertDynamicPunctuation(ch))
            {
                return false;
            }
        }

        return hasDigit;
    }

    private static bool LooksLikeShortInertDynamicValue(StringBuilder value)
    {
        var hasDigit = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (IsAsciiLetter(ch) || IsCjk(ch))
            {
                return false;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            if (!IsInertDynamicPunctuation(ch))
            {
                return false;
            }
        }

        return hasDigit;
    }

    private static bool IsSingleInertDynamicChar(char ch)
    {
        return !IsCjk(ch) && !IsNonHanEastAsianDisplayGlyph(ch) && (char.IsLetterOrDigit(ch) || IsInertDynamicPunctuation(ch));
    }

    private static bool IsInertDynamicPunctuation(char ch)
    {
        return char.IsWhiteSpace(ch) ||
               ch is '-' or '+' or '.' or ',' or '/' or ':' or '$' or '%' or '#' or
                   '[' or ']' or '(' or ')' or '<' or '>' or '\u25a0';
    }

    private static bool IsCjk(char ch)
    {
        return ch is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF';
    }

    private static bool IsAsciiLetter(char ch)
    {
        return ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static void RecordTmpRuntimeText(TMP_Text component, string? value)
    {
        if (!RuntimeTextCollector.IsEnabled)
        {
            return;
        }

        CountTmpPerf(ref _tmpPerfCollectorCalls);
        RuntimeTextCollector.Record(component, value);
    }

    private static void ApplyTmpHookFallback(TMP_Text text, string value)
    {
        ApplyTmpHookFallback(text, value, ClassifyEastAsianDisplayText(value));
    }

    private static void ApplyTmpHookFallback(TMP_Text text, string value, EastAsianDisplayTextClass displayClass)
    {
        if (displayClass == EastAsianDisplayTextClass.None)
        {
            return;
        }

        CountTmpPerf(ref _tmpPerfFallbackCalls);
        FontFallbackService.ApplyFallback(text, value, candidateContainsEastAsianGlyph: true);
    }

    private enum EastAsianDisplayTextClass
    {
        None,
        HanOnly,
        NonHanEastAsianOnly,
        MixedHanAndNonHanEastAsian
    }

    private static EastAsianDisplayTextClass ClassifyEastAsianDisplayText(string value)
    {
        var hasHan = false;
        var hasNonHanEastAsianGlyph = false;
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                hasHan = true;
                continue;
            }

            if (IsNonHanEastAsianDisplayGlyph(ch))
            {
                hasNonHanEastAsianGlyph = true;
            }
        }

        if (hasHan && hasNonHanEastAsianGlyph)
        {
            return EastAsianDisplayTextClass.MixedHanAndNonHanEastAsian;
        }

        if (hasHan)
        {
            return EastAsianDisplayTextClass.HanOnly;
        }

        return hasNonHanEastAsianGlyph
            ? EastAsianDisplayTextClass.NonHanEastAsianOnly
            : EastAsianDisplayTextClass.None;
    }

    private static bool ShouldSkipTranslationForEastAsianDisplayText(EastAsianDisplayTextClass displayClass)
    {
        return displayClass is EastAsianDisplayTextClass.NonHanEastAsianOnly or
            EastAsianDisplayTextClass.MixedHanAndNonHanEastAsian;
    }

    private static bool IsNonHanEastAsianDisplayGlyph(char ch)
    {
        return ch is >= '\u3040' and <= '\u30FF' or
               >= '\u31F0' and <= '\u31FF' or
               >= '\uFF66' and <= '\uFF9F' or
               >= '\u1100' and <= '\u11FF' or
               >= '\u3130' and <= '\u318F' or
               >= '\uA960' and <= '\uA97F' or
               >= '\uAC00' and <= '\uD7AF' or
               >= '\uD7B0' and <= '\uD7FF';
    }

    private static bool TmpHookPerfCountersEnabled => _tmpHookPerfCountersEnabledFast;

    private static void CountTmpPerf(ref long counter)
    {
        if (TmpHookPerfCountersEnabled)
        {
            counter++;
        }
    }

    private static long StartTmpPerfTimer()
    {
        return TmpHookPerfCountersEnabled ? Stopwatch.GetTimestamp() : 0L;
    }

    private static void AddTmpPerfElapsed(ref long ticks, long start)
    {
        if (start != 0L)
        {
            ticks += Stopwatch.GetTimestamp() - start;
        }
    }

    private static void CountGenericStatusFailure(GenericStatusSkipFailure reason)
    {
        if (!TmpHookPerfCountersEnabled)
        {
            return;
        }

        switch (reason)
        {
            case GenericStatusSkipFailure.NoMarker:
                _tmpPerfGenericStatusFailNoMarker++;
                break;
            case GenericStatusSkipFailure.ControlTip:
                _tmpPerfGenericStatusFailControlTip++;
                break;
            case GenericStatusSkipFailure.CjkOrInvalid:
                _tmpPerfGenericStatusFailCjk++;
                break;
            case GenericStatusSkipFailure.Shape:
                _tmpPerfGenericStatusFailShape++;
                break;
        }
    }

    private static void MaybeLogTmpHookPerfCounters()
    {
        if (!TmpHookPerfCountersEnabled)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_tmpPerfNextLogTimestamp == 0L)
        {
            _tmpPerfNextLogTimestamp = now + GetTmpPerfLogIntervalTicks();
            return;
        }

        if (now < _tmpPerfNextLogTimestamp)
        {
            return;
        }

        var prefixMs = TicksToMilliseconds(_tmpPerfPrefixTicks);
        var postfixMs = TicksToMilliseconds(_tmpPerfPostfixTicks);
        var translateMs = TicksToMilliseconds(_tmpPerfTranslateTicks);
        Plugin.Log.LogInfo(
            "TMP hook perf " +
            $"prefix={_tmpPerfPrefixCalls} postfix={_tmpPerfPostfixCalls} " +
            $"skipHit={_tmpPerfSkipHits} skipMiss={_tmpPerfSkipMisses} " +
            $"exactCache={_tmpPerfExactCacheHits} shapeCache={_tmpPerfShapeCacheHits} componentBypass={_tmpPerfComponentBypassHits} translationCache={_tmpPerfTranslationCacheHits} statusHit={_tmpPerfGenericStatusHits} " +
            $"statusFail(noMarker/control/cjk/shape)={_tmpPerfGenericStatusFailNoMarker}/{_tmpPerfGenericStatusFailControlTip}/{_tmpPerfGenericStatusFailCjk}/{_tmpPerfGenericStatusFailShape} " +
            $"translate={_tmpPerfTranslateCalls} fallback={_tmpPerfFallbackCalls} collector={_tmpPerfCollectorCalls} " +
            $"cache(noop/translated/source)={TmpHookNoopCache.Count}/{TmpHookTranslationCache.Count}/{TmpHookSourceNoopCache.Count} " +
            $"evicted(noop/translated/source)={TmpHookNoopCache.EvictionCount}/{TmpHookTranslationCache.EvictionCount}/{TmpHookSourceNoopCache.EvictionCount} " +
            $"ms(prefix/postfix/translate)={prefixMs:0.###}/{postfixMs:0.###}/{translateMs:0.###}");
        ResetTmpHookPerfCounters();
        _tmpPerfNextLogTimestamp = now + GetTmpPerfLogIntervalTicks();
    }

    private static long GetTmpPerfLogIntervalTicks()
    {
        return _tmpHookPerfLogIntervalTicksFast != 0L
            ? _tmpHookPerfLogIntervalTicksFast
            : 10L * Stopwatch.Frequency;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static void ResetTmpHookPerfCounters()
    {
        _tmpPerfPrefixCalls = 0;
        _tmpPerfPostfixCalls = 0;
        _tmpPerfSkipHits = 0;
        _tmpPerfSkipMisses = 0;
        _tmpPerfExactCacheHits = 0;
        _tmpPerfShapeCacheHits = 0;
        _tmpPerfComponentBypassHits = 0;
        _tmpPerfTranslationCacheHits = 0;
        _tmpPerfGenericStatusHits = 0;
        _tmpPerfGenericStatusFailNoMarker = 0;
        _tmpPerfGenericStatusFailControlTip = 0;
        _tmpPerfGenericStatusFailCjk = 0;
        _tmpPerfGenericStatusFailShape = 0;
        _tmpPerfTranslateCalls = 0;
        _tmpPerfFallbackCalls = 0;
        _tmpPerfCollectorCalls = 0;
        _tmpPerfPrefixTicks = 0;
        _tmpPerfPostfixTicks = 0;
        _tmpPerfTranslateTicks = 0;
        _tmpPerfNextLogTimestamp = 0;
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
