using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace V81TestChn;

internal static class TargetedUiTranslator
{
    [System.ThreadStatic]
    private static int _dropdownRefreshDepth;

    private static readonly HashSet<int> QuickMenuTranslated = new();
    private static readonly HashSet<int> MenuPanelTranslated = new();
    private static readonly HashSet<int> QuickMenuTranslationRunning = new();
    private static readonly HashSet<int> MenuPanelTranslationRunning = new();
    private static readonly HashSet<int> HudChatOutputTranslationPending = new();
    private static readonly HashSet<int> HudChatOutputTranslationRunning = new();
    private static readonly Dictionary<int, int> HudChatOutputTranslationGenerations = new();
    private static readonly BoundedCache<int, ProcessedTextState> TranslationProcessedCache = new(16384);
    private static readonly BoundedCache<int, List<int>> TmpDropdownOptionTextCache = new(16384);
    private static readonly BoundedCache<int, List<int>> DropdownOptionTextCache = new(16384);
    private static readonly Dictionary<int, ChatOutputState> ChatOutputStates = new();
    private static readonly Dictionary<int, CursorTipState> CursorTipStates = new();
    private static readonly Dictionary<int, PlanetRiskTextPair> PlanetRiskTextPairCache = new();
    private static readonly List<TMP_Dropdown> TmpDropdownScanBuffer = new();
    private static readonly List<Dropdown> DropdownScanBuffer = new();
    private static readonly List<TMP_Text> TmpTextScanBuffer = new();
    private static readonly List<Text> UiTextScanBuffer = new();
    private static readonly List<TextMesh> TextMeshScanBuffer = new();
    private static readonly List<InteractTrigger> InteractTriggerScanBuffer = new();
    private static readonly List<TMP_Text> PlanetRiskTmpTextScanBuffer = new();
    private const int ChatLineCacheLimit = 256;
    private const int ChatHistoryReferenceLimit = 1024;
    private const float PlanetInfoSummaryMiddleThreshold = 0.68f;
    private const float PlanetInfoSummaryAutoSizeMinScale = 0.84f;
    private const string PlanetRiskTitleObjectName = "HazardLevel";
    private const string PlanetRiskTitleLocalizedText = "\u98ce\u9669\u7ea7\u522b\uff1a";
    private const string PlanetRiskValueObjectName = "HazardLevelLetter";
    private const int PlanetRiskMergeLogLimit = 6;
    private static bool _sceneUnloadSubscribed;
    private static bool _hudChatOutputDeferredByRoundTransition;
    private static int _planetRiskMergeLogRemaining = PlanetRiskMergeLogLimit;
    [System.ThreadStatic]
    private static int _planetRiskTextMutationDepth;

    private readonly struct ProcessedTextState
    {
        public ProcessedTextState(int parentId, int textHash, int styleHash)
        {
            ParentId = parentId;
            TextHash = textHash;
            StyleHash = styleHash;
        }

        public int ParentId { get; }
        public int TextHash { get; }
        public int StyleHash { get; }
    }

    private sealed class ChatOutputState
    {
        public int HistoryValidationCursor;
        public int HistoryKnownCount;
        public readonly BoundedCache<int, string?> HistoryEntryReferences = new(ChatHistoryReferenceLimit);
        public string? LastOriginalText;
        public string? LastTranslatedText;
        public readonly Dictionary<string, string?> LineTranslationCache = new(StringComparer.Ordinal);
    }

    private sealed class CursorTipState
    {
        public int ParentId;
        public string? LastOriginalText;
        public string? LastTranslatedText;
    }

    private sealed class PlanetRiskTextPair
    {
        public TMP_Text? HudRiskText;
        public TMP_Text? Title;
        public TMP_Text? Value;
        public int HudRiskTextId;
        public int TitleParentId;
        public int ValueParentId;
    }

    private sealed class TranslationCounts
    {
        public int Translated;
        public int Seen;
        public int WorkThisFrame;
    }

    public static void Initialize()
    {
        if (_sceneUnloadSubscribed)
        {
            return;
        }

        SceneManager.sceneUnloaded += OnSceneUnloaded;
        _sceneUnloadSubscribed = true;
    }

    public static void Shutdown()
    {
        if (_sceneUnloadSubscribed)
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneUnloadSubscribed = false;
        }

        ClearCaches();
    }

    public static void ClearCaches()
    {
        QuickMenuTranslated.Clear();
        MenuPanelTranslated.Clear();
        QuickMenuTranslationRunning.Clear();
        MenuPanelTranslationRunning.Clear();
        HudChatOutputTranslationPending.Clear();
        HudChatOutputTranslationRunning.Clear();
        HudChatOutputTranslationGenerations.Clear();
        _hudChatOutputDeferredByRoundTransition = false;
        TranslationProcessedCache.Clear();
        TmpDropdownOptionTextCache.Clear();
        DropdownOptionTextCache.Clear();
        ChatOutputStates.Clear();
        CursorTipStates.Clear();
        PlanetRiskTextPairCache.Clear();
        RoundTransitionTextThrottle.Reset();
        ClearScanBuffers();
    }

    private static void FillScanBuffer<T>(GameObject root, bool includeInactive, List<T> buffer)
        where T : Component
    {
        buffer.Clear();
        root.GetComponentsInChildren(includeInactive, buffer);
    }

    private static void ClearScanBuffers()
    {
        TmpDropdownScanBuffer.Clear();
        DropdownScanBuffer.Clear();
        TmpTextScanBuffer.Clear();
        UiTextScanBuffer.Clear();
        TextMeshScanBuffer.Clear();
        InteractTriggerScanBuffer.Clear();
        PlanetRiskTmpTextScanBuffer.Clear();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        ClearCaches();
        CustomLocalizationExtensionService.ClearRuntimeCaches();
        TextPatches.ClearSceneRuntimeCaches();
    }

    public static (int translated, int seen) TranslateRoot(GameObject? root, string reason)
    {
        if (root == null)
        {
            return (0, 0);
        }

        var seen = new HashSet<int>();
        var result = TranslateGameObject(root, seen);
        Plugin.LogTargetedTranslation(reason, result.translated, result.seen);
        return result;
    }

    public static (int translated, int seen) TranslateMenuPanelOnce(GameObject? root, string reason)
    {
        if (root == null)
        {
            return (0, 0);
        }

        var instanceId = root.GetInstanceID();
        if (!MenuPanelTranslated.Add(instanceId))
        {
            return (0, 0);
        }

        var seen = new HashSet<int>();
        var result = TranslateGameObject(root, seen, includeInactive: false);
        Plugin.LogTargetedTranslation(reason, result.translated, result.seen);
        return result;
    }

    public static bool ScheduleMenuPanelOnce(MonoBehaviour? owner, GameObject? root, string reason)
    {
        if (root == null)
        {
            return true;
        }

        var instanceId = root.GetInstanceID();
        if (MenuPanelTranslated.Contains(instanceId) || MenuPanelTranslationRunning.Contains(instanceId))
        {
            return true;
        }

        if (owner == null || !owner.isActiveAndEnabled)
        {
            return false;
        }

        TranslateGameObjectOpenFrameFast(root, includeInactive: false);

        try
        {
            MenuPanelTranslationRunning.Add(instanceId);
            owner.StartCoroutine(TranslateMenuPanelOnceBudgeted(root, reason, instanceId));
            return true;
        }
        catch (Exception ex)
        {
            MenuPanelTranslationRunning.Remove(instanceId);
            Plugin.Log.LogWarning($"Menu panel translation scheduling failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static (int translated, int seen) TranslateQuickMenuLeaveGamePanel(GameObject? panel, string reason)
    {
        if (panel == null)
        {
            return (0, 0);
        }

        return TranslateMenuPanelOnce(panel, reason);
    }

    public static (int translated, int seen) TranslateMenuManager(MenuManager menu, string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        Add(TranslateGameObject(menu.gameObject, seen));
        Add(TranslateRootOnly(menu.menuButtons, seen));
        Add(TranslateRootOnly(menu.menuNotification, seen));
        Add(TranslateRootOnly(menu.loadingScreen, seen));
        Add(TranslateRootOnly(menu.lanButtonContainer, seen));
        Add(TranslateRootOnly(menu.lanWarningContainer, seen));
        Add(TranslateRootOnly(menu.joinCrewButtonContainer, seen));
        Add(TranslateRootOnly(menu.serverListUIContainer, seen));
        Add(TranslateRootOnly(menu.NewsPanel, seen));
        Add(TranslateRootOnly(menu.HostSettingsScreen, seen));
        Add(TranslateRootOnly(menu.HostSettingsOptionsLAN, seen));
        Add(TranslateRootOnly(menu.HostSettingsOptionsNormal, seen));
        Add(TranslateRootOnly(menu.hostSettingsPanel, seen));
        Add(TranslateRootOnly(menu.PleaseConfirmChangesSettingsPanel, seen));
        Add(TranslateRootOnly(menu.KeybindsPanel, seen));
        Add(TranslateRootOnly(menu.leaderboardContainer, seen));
        Add(TranslateRootOnly(menu.inputFieldGameObject, seen));

        TranslateTmp(menu.menuNotificationText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.menuNotificationButtonText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.loadingText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.launchedInLanModeText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.tipTextHostSettings, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.logText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.privatePublicDescription, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.currentMicrophoneText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.changesNotAppliedText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.settingsBackButton, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.submittedRankText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.leaderboardHeaderText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.leaderboardLoadingText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.HoverTipText, seen, ref translated, ref totalSeen);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    public static (int translated, int seen) TranslatePreInit(PreInitSceneScript script, string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        Add(TranslateGameObject(script.gameObject, seen));
        Add(TranslateRootOnly(script.continueButton, seen));
        Add(TranslateRootOnly(script.OnlineModeButton, seen));
        Add(TranslateRootOnly(script.FileCorruptedPanel, seen));
        Add(TranslateRootOnly(script.FileCorruptedDialoguePanel, seen));
        Add(TranslateRootOnly(script.FileCorruptedRestartButton, seen));
        Add(TranslateRootOnly(script.restartingGameText, seen));
        Add(TranslateRootOnly(script.launchSettingsPanelsContainer, seen));

        if (script.LaunchSettingsPanels != null)
        {
            foreach (var panel in script.LaunchSettingsPanels)
            {
                Add(TranslateRootOnly(panel, seen));
            }
        }

        TranslateTmp(script.headerText, seen, ref translated, ref totalSeen);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    public static (int translated, int seen) TranslateQuickMenu(QuickMenuManager menu, string reason)
    {
        var firstPass = QuickMenuTranslated.Add(menu.GetInstanceID());
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        if (firstPass)
        {
            Add(TranslateRootOnly(menu.mainButtonsPanel, seen));
            if (menu.playerListPanel != null && menu.playerListPanel.activeInHierarchy)
            {
                Add(TranslateRootOnly(menu.playerListPanel, seen));
            }

            if (menu.debugMenuUI != null && menu.debugMenuUI.activeInHierarchy)
            {
                Add(TranslateRootOnly(menu.debugMenuUI, seen));
            }
        }

        TranslateTmp(menu.interactTipText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.leaveGameClarificationText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.ConfirmKickPlayerText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.currentMicrophoneText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.changesNotAppliedText, seen, ref translated, ref totalSeen);
        TranslateTmp(menu.settingsBackButton, seen, ref translated, ref totalSeen);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    public static bool ScheduleQuickMenu(QuickMenuManager? menu, string reason)
    {
        if (menu == null)
        {
            return true;
        }

        var instanceId = menu.GetInstanceID();
        if (QuickMenuTranslationRunning.Contains(instanceId))
        {
            return true;
        }

        if (!menu.isActiveAndEnabled)
        {
            return false;
        }

        var firstPass = QuickMenuTranslated.Add(instanceId);
        if (firstPass)
        {
            TranslateGameObjectOpenFrameFast(menu.mainButtonsPanel, includeInactive: true);
            if (menu.debugMenuUI != null && menu.debugMenuUI.activeInHierarchy)
            {
                TranslateGameObjectOpenFrameFast(menu.debugMenuUI, includeInactive: true);
            }
        }

        try
        {
            QuickMenuTranslationRunning.Add(instanceId);
            menu.StartCoroutine(TranslateQuickMenuBudgeted(menu, reason, instanceId, firstPass));
            return true;
        }
        catch (Exception ex)
        {
            QuickMenuTranslationRunning.Remove(instanceId);
            Plugin.Log.LogWarning($"Quick menu translation scheduling failed: {ex.GetType().Name}: {ex.Message}");
            if (firstPass)
            {
                // Re-enable the synchronous first pass fallback if coroutine scheduling failed.
                QuickMenuTranslated.Remove(instanceId);
            }

            return false;
        }
    }

    public static (int translated, int seen) TranslateHud(HUDManager hud, string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        Add(TranslateGameObject(hud.gameObject, seen));
        Add(TranslateRootOnly(hud.HUDContainer, seen));
        Add(TranslateHudElement(hud.Inventory, seen));
        Add(TranslateHudElement(hud.Chat, seen));
        Add(TranslateHudElement(hud.PlayerInfo, seen));
        Add(TranslateHudElement(hud.Tooltips, seen));
        Add(TranslateHudElement(hud.Clock, seen));

        TranslateTmpArray(hud.controlTipLines, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.buildModeControlTip, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.loadingText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.planetInfoSummaryText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.planetInfoHeaderText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.planetRiskLevelText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.tipsPanelBody, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.tipsPanelHeader, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.globalNotificationText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.dialogeBoxHeaderText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.dialogeBoxText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.spectatingPlayerText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.spectatorTipText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.holdButtonToEndGameEarlyText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.holdButtonToEndGameEarlyVotesText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.EndOfRunStatsText, seen, ref translated, ref totalSeen);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    public static (int translated, int seen) TranslateHudPlanetInfo(HUDManager hud, string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        TranslateTmpTargeted(hud.planetInfoHeaderText, DynamicTextDomain.PlanetInfo, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.planetInfoSummaryText, DynamicTextDomain.PlanetInfo, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.planetRiskLevelText, DynamicTextDomain.PlanetInfo, seen, ref translated, ref totalSeen);
        ApplyPlanetInfoPresentation(hud);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    private static void ApplyPlanetInfoPresentation(HUDManager hud)
    {
        ApplyPlanetInfoSummaryPresentation(hud.planetInfoSummaryText);
        ApplyPlanetRiskPresentation(hud.planetRiskLevelText, FindPlanetRiskTitle(hud));
    }

    private static void ApplyPlanetInfoSummaryPresentation(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        var maxFontSize = text.fontSize;
        if (maxFontSize > 0f)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = maxFontSize;
            text.fontSizeMin = Mathf.Min(text.fontSizeMin > 0f ? text.fontSizeMin : maxFontSize, maxFontSize * PlanetInfoSummaryAutoSizeMinScale);
        }

        var rect = text.rectTransform == null ? default : text.rectTransform.rect;
        if (rect.width <= 1f || rect.height <= 1f)
        {
            text.alignment = TextAlignmentOptions.TopLeft;
            return;
        }

        var preferredHeight = text.GetPreferredValues(text.text, rect.width, 0f).y;
        text.alignment = preferredHeight > 0f && preferredHeight <= rect.height * PlanetInfoSummaryMiddleThreshold
            ? TextAlignmentOptions.MidlineLeft
            : TextAlignmentOptions.TopLeft;
    }

    private static void ApplyPlanetRiskPresentation(TMP_Text? text, TMP_Text? title)
    {
        if (text == null)
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        ApplyPlanetRiskValuePresentation(text, title, text.text, assignRiskText: true);
    }

    public static bool TryPrepareHudPlanetRiskValue(HUDManager? hud, TMP_Text? riskText, string? sourceValue, string reason, out string preparedValue)
    {
        preparedValue = string.Empty;
        if (hud == null || riskText == null || string.IsNullOrWhiteSpace(sourceValue))
        {
            return false;
        }

        return ApplyPlanetRiskValuePresentation(riskText, null, sourceValue, false, reason, out preparedValue);
    }

    public static bool FinalizeHudPlanetRiskValue(HUDManager? hud, TMP_Text? riskText, string reason)
    {
        if (hud == null || riskText == null || string.IsNullOrWhiteSpace(riskText.text))
        {
            return false;
        }

        var title = FindPlanetRiskTitle(hud);
        return ApplyPlanetRiskValuePresentation(riskText, title, riskText.text, assignRiskText: true, reason, out _);
    }

    public static bool TryMergeHudPlanetRiskValue(HUDManager? hud, TMP_Text? riskText, string? sourceValue, string reason, out string preparedValue)
    {
        return TryHandleHudPlanetRiskText(hud, riskText, sourceValue, reason, out preparedValue);
    }

    public static bool TryHandleHudPlanetRiskText(HUDManager? hud, TMP_Text? text, string? sourceValue, string reason, out string preparedValue)
    {
        preparedValue = string.Empty;
        if (_planetRiskTextMutationDepth > 0 ||
            hud == null ||
            text == null ||
            string.IsNullOrWhiteSpace(sourceValue) ||
            !IsHudPlanetRiskTextCandidate(hud, text))
        {
            return false;
        }

        ResolvePlanetRiskTextPair(hud, text, out var title, out var valueText);
        var isValueText = ReferenceEquals(text, valueText) || IsPlanetRiskValueObject(text);
        var isTitleText = ReferenceEquals(text, title) || IsPlanetRiskTitleObject(text) || IsPlanetRiskTitle(sourceValue);
        var sourceRiskValue = NormalizePlanetRiskValue(sourceValue);
        if (isTitleText && !isValueText && string.IsNullOrWhiteSpace(sourceRiskValue))
        {
            preparedValue = PlanetRiskTitleLocalizedText;
            var titleText = title ?? text;
            ApplyPlanetRiskTitlePresentation(titleText);
            FontFallbackService.ApplyFallback(titleText, preparedValue);
            if (!ReferenceEquals(text, titleText))
            {
                SetPlanetRiskTitleOnlyText(titleText);
            }

            CachePlanetRiskTextPair(hud, titleText, valueText);
            return true;
        }

        var valueSource = isValueText ? sourceValue : valueText?.text;
        if (string.IsNullOrWhiteSpace(valueSource))
        {
            if (isTitleText && valueText != null)
            {
                preparedValue = PlanetRiskTitleLocalizedText;
                return true;
            }

            valueSource = sourceValue;
        }

        var value = isValueText ? sourceRiskValue : NormalizePlanetRiskValue(valueSource);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (title != null)
        {
            ApplyPlanetRiskTitlePresentation(title);
            var combinedText = PlanetRiskTitleLocalizedText + value;
            if (ReferenceEquals(text, title))
            {
                preparedValue = combinedText;
                FontFallbackService.ApplyFallback(title, combinedText);
            }
            else
            {
                preparedValue = isValueText ? string.Empty : combinedText;
                SetPlanetRiskTitleText(title, value);
            }

            if (valueText != null &&
                !ReferenceEquals(text, valueText) &&
                !string.IsNullOrEmpty(valueText.text))
            {
                SetPlanetRiskValueText(valueText, string.Empty);
            }

            LogPlanetRiskMerge(reason, isValueText ? text : valueText ?? text, title, value);
            return true;
        }

        preparedValue = value;
        FontFallbackService.ApplyFallback(text, preparedValue);
        return true;
    }

    public static bool IsHudPlanetRiskTextCandidate(HUDManager? hud, TMP_Text? text)
    {
        if (hud == null || text == null)
        {
            return false;
        }

        return ReferenceEquals(text, hud.planetRiskLevelText) || HasPlanetRiskObjectName(text);
    }

    private static bool ApplyPlanetRiskValuePresentation(TMP_Text riskText, TMP_Text? title, string? sourceValue, bool assignRiskText)
    {
        return ApplyPlanetRiskValuePresentation(riskText, title, sourceValue, assignRiskText, "planet-risk.presentation", out _);
    }

    private static bool ApplyPlanetRiskValuePresentation(TMP_Text riskText, TMP_Text? title, string? sourceValue, bool assignRiskText, string reason, out string preparedValue)
    {
        var value = NormalizePlanetRiskValue(sourceValue);
        if (string.IsNullOrWhiteSpace(value))
        {
            preparedValue = string.Empty;
            return false;
        }

        riskText.richText = true;
        riskText.enableWordWrapping = false;
        riskText.overflowMode = TextOverflowModes.Overflow;
        riskText.alignment = TextAlignmentOptions.MidlineLeft;

        if (!assignRiskText || title == null)
        {
            preparedValue = value;
            FontFallbackService.ApplyFallback(riskText, preparedValue);
            if (assignRiskText && !string.Equals(riskText.text, preparedValue, StringComparison.Ordinal))
            {
                riskText.text = preparedValue;
            }

            return true;
        }

        preparedValue = string.Empty;
        ApplyPlanetRiskTitlePresentation(title);
        SetPlanetRiskTitleText(title, value);
        if (assignRiskText && !string.IsNullOrEmpty(riskText.text))
        {
            riskText.text = string.Empty;
        }

        LogPlanetRiskMerge(reason, riskText, title, value);
        return true;
    }

    private static void ResolvePlanetRiskTextPair(HUDManager hud, TMP_Text text, out TMP_Text? title, out TMP_Text? value)
    {
        if (TryGetCachedPlanetRiskTextPair(hud, out title, out value) &&
            (ReferenceEquals(text, title) ||
             ReferenceEquals(text, value) ||
             (title != null && IsPlanetRiskTitleObject(text)) ||
             (value != null && IsPlanetRiskValueObject(text))))
        {
            return;
        }

        title = FindPlanetRiskTitleUncached(hud);
        value = FindPlanetRiskValue(hud, text, title);
        if (value == null && title != null)
        {
            value = FindPlanetRiskValueSibling(title);
        }

        if (title == null && value != null)
        {
            title = FindPlanetRiskTitleSibling(value) ?? FindPlanetRiskTitleUncached(hud);
        }

        CachePlanetRiskTextPair(hud, title, value);
    }

    private static bool TryGetCachedPlanetRiskTextPair(HUDManager hud, out TMP_Text? title, out TMP_Text? value)
    {
        title = null;
        value = null;
        var hudId = hud.GetInstanceID();
        if (!PlanetRiskTextPairCache.TryGetValue(hudId, out var cached))
        {
            return false;
        }

        var riskText = hud.planetRiskLevelText;
        if (!ReferenceEquals(cached.HudRiskText, riskText) ||
            cached.HudRiskTextId != GetComponentInstanceId(riskText))
        {
            PlanetRiskTextPairCache.Remove(hudId);
            return false;
        }

        title = IsCachedPlanetRiskTextValid(cached.Title, cached.TitleParentId) ? cached.Title : null;
        value = IsCachedPlanetRiskTextValid(cached.Value, cached.ValueParentId) ? cached.Value : null;
        if (title != null || value != null)
        {
            return true;
        }

        PlanetRiskTextPairCache.Remove(hudId);
        return false;
    }

    private static void CachePlanetRiskTextPair(HUDManager hud, TMP_Text? title, TMP_Text? value)
    {
        if (title == null && value == null)
        {
            return;
        }

        var riskText = hud.planetRiskLevelText;
        PlanetRiskTextPairCache[hud.GetInstanceID()] = new PlanetRiskTextPair
        {
            HudRiskText = riskText,
            HudRiskTextId = GetComponentInstanceId(riskText),
            Title = title,
            Value = value,
            TitleParentId = GetParentInstanceId(title),
            ValueParentId = GetParentInstanceId(value)
        };
    }

    private static bool IsCachedPlanetRiskTextValid(TMP_Text? text, int parentId)
    {
        return text != null && GetParentInstanceId(text) == parentId;
    }

    private static int GetComponentInstanceId(Component? component)
    {
        return component == null ? 0 : component.GetInstanceID();
    }

    private static TMP_Text? FindPlanetRiskTitle(HUDManager hud)
    {
        if (TryGetCachedPlanetRiskTextPair(hud, out var title, out _) && title != null)
        {
            return title;
        }

        title = FindPlanetRiskTitleUncached(hud);
        if (title != null)
        {
            CachePlanetRiskTextPair(hud, title, FindPlanetRiskValueSibling(title));
        }

        return title;
    }

    private static TMP_Text? FindPlanetRiskTitleUncached(HUDManager hud)
    {
        TMP_Text? best = null;
        var bestScore = float.MaxValue;
        var riskText = hud.planetRiskLevelText;
        if (IsPlanetRiskTitleObject(riskText) || IsPlanetRiskTitle(riskText?.text))
        {
            return riskText;
        }

        var sibling = FindPlanetRiskTitleSibling(riskText);
        if (sibling != null)
        {
            return sibling;
        }

        try
        {
            FillScanBuffer(hud.gameObject, includeInactive: true, PlanetRiskTmpTextScanBuffer);
            foreach (var text in PlanetRiskTmpTextScanBuffer)
            {
                if (ReferenceEquals(text, riskText))
                {
                    continue;
                }

                if (IsPlanetRiskTitle(text.text))
                {
                    if (riskText == null)
                    {
                        return text;
                    }

                    var score = ScorePlanetRiskTitleCandidate(text, riskText);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = text;
                    }
                }
            }
        }
        finally
        {
            PlanetRiskTmpTextScanBuffer.Clear();
        }

        return best;
    }

    private static TMP_Text? FindPlanetRiskValue(HUDManager hud, TMP_Text text, TMP_Text? title)
    {
        if (IsPlanetRiskValueObject(text))
        {
            return text;
        }

        var value = FindPlanetRiskValueSibling(text) ?? FindPlanetRiskValueSibling(title);
        if (value != null)
        {
            return value;
        }

        var riskText = hud.planetRiskLevelText;
        if (riskText != null && !ReferenceEquals(riskText, title) && !IsPlanetRiskTitleObject(riskText))
        {
            return riskText;
        }

        return null;
    }

    private static TMP_Text? FindPlanetRiskTitleSibling(TMP_Text? riskText)
    {
        var parent = riskText?.transform.parent;
        if (parent == null)
        {
            return null;
        }

        var title = parent.Find(PlanetRiskTitleObjectName)?.GetComponent<TMP_Text>();
        if (title != null && !ReferenceEquals(title, riskText))
        {
            return title;
        }

        try
        {
            FillScanBuffer(parent.gameObject, includeInactive: true, PlanetRiskTmpTextScanBuffer);
            foreach (var text in PlanetRiskTmpTextScanBuffer)
            {
                if (text == null || ReferenceEquals(text, riskText))
                {
                    continue;
                }

                if (string.Equals(text.name, PlanetRiskTitleObjectName, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
        }
        finally
        {
            PlanetRiskTmpTextScanBuffer.Clear();
        }

        return null;
    }

    private static TMP_Text? FindPlanetRiskValueSibling(TMP_Text? text)
    {
        var parent = text?.transform.parent;
        if (parent == null)
        {
            return null;
        }

        var value = parent.Find(PlanetRiskValueObjectName)?.GetComponent<TMP_Text>();
        if (value != null && !ReferenceEquals(value, text))
        {
            return value;
        }

        try
        {
            FillScanBuffer(parent.gameObject, includeInactive: true, PlanetRiskTmpTextScanBuffer);
            foreach (var childText in PlanetRiskTmpTextScanBuffer)
            {
                if (childText == null || ReferenceEquals(childText, text))
                {
                    continue;
                }

                if (IsPlanetRiskValueObject(childText))
                {
                    return childText;
                }
            }
        }
        finally
        {
            PlanetRiskTmpTextScanBuffer.Clear();
        }

        return null;
    }

    private static float ScorePlanetRiskTitleCandidate(TMP_Text title, TMP_Text riskText)
    {
        var titlePosition = title.transform.position;
        var riskPosition = riskText.transform.position;
        var score = Mathf.Abs(titlePosition.y - riskPosition.y) * 1000f + Mathf.Abs(titlePosition.x - riskPosition.x);
        if (title.transform.parent == riskText.transform.parent)
        {
            score -= 250f;
        }

        if (title.gameObject.activeInHierarchy)
        {
            score -= 100f;
        }

        return score;
    }

    private static bool IsPlanetRiskTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = StripSimpleRichTextTags(text).Trim();
        return string.Equals(trimmed, "HAZARD LEVEL:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("HAZARD LEVEL:", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Risk level:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Risk level:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(PlanetRiskTitleLocalizedText, StringComparison.Ordinal) ||
               trimmed.StartsWith("\u98ce\u9669\u7ea7\u522b:", StringComparison.Ordinal) ||
               string.Equals(trimmed, "\u98ce\u9669\u7ea7\u522b\uff1a", StringComparison.Ordinal) ||
               string.Equals(trimmed, "\u98ce\u9669\u7ea7\u522b:", StringComparison.Ordinal) ||
               string.Equals(trimmed, "\u5371\u9669\u7b49\u7ea7\uff1a", StringComparison.Ordinal) ||
               string.Equals(trimmed, "\u5371\u9669\u7b49\u7ea7:", StringComparison.Ordinal);
    }

    private static bool HasPlanetRiskObjectName(TMP_Text? text)
    {
        return IsPlanetRiskTitleObject(text) || IsPlanetRiskValueObject(text);
    }

    private static bool IsPlanetRiskTitleObject(TMP_Text? text)
    {
        return text != null && string.Equals(text.name, PlanetRiskTitleObjectName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlanetRiskValueObject(TMP_Text? text)
    {
        return text != null && string.Equals(text.name, PlanetRiskValueObjectName, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyPlanetRiskTitlePresentation(TMP_Text title)
    {
        title.richText = true;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Overflow;
        title.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void SetPlanetRiskTitleText(TMP_Text title, string value)
    {
        var combinedText = PlanetRiskTitleLocalizedText + value;
        if (!string.Equals(title.text, combinedText, StringComparison.Ordinal))
        {
            _planetRiskTextMutationDepth++;
            try
            {
                title.text = combinedText;
            }
            finally
            {
                _planetRiskTextMutationDepth--;
            }
        }

        FontFallbackService.ApplyFallback(title, combinedText);
    }

    private static void SetPlanetRiskTitleOnlyText(TMP_Text title)
    {
        if (!string.Equals(title.text, PlanetRiskTitleLocalizedText, StringComparison.Ordinal))
        {
            _planetRiskTextMutationDepth++;
            try
            {
                title.text = PlanetRiskTitleLocalizedText;
            }
            finally
            {
                _planetRiskTextMutationDepth--;
            }
        }

        FontFallbackService.ApplyFallback(title, PlanetRiskTitleLocalizedText);
    }

    private static void SetPlanetRiskValueText(TMP_Text valueText, string value)
    {
        _planetRiskTextMutationDepth++;
        try
        {
            valueText.text = value;
        }
        finally
        {
            _planetRiskTextMutationDepth--;
        }
    }

    private static void LogPlanetRiskMerge(string reason, TMP_Text riskText, TMP_Text title, string value)
    {
        if (!Plugin.RuntimeLocalizationLogsEnabled)
        {
            return;
        }

        if (_planetRiskMergeLogRemaining <= 0)
        {
            return;
        }

        _planetRiskMergeLogRemaining--;
        Plugin.Log.LogInfo(
            $"PlanetRiskMerge[{reason}] value={value} titlePath={BuildTransformPath(title.transform)} titleText={title.text} valuePath={BuildTransformPath(riskText.transform)} valueObject={riskText.name} expectedValueObject={PlanetRiskValueObjectName}");
    }

    private static string BuildTransformPath(Transform? transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var parts = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts);
    }

    private static string NormalizePlanetRiskValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = ExtractPlanetRiskValue(StripSimpleRichTextTags(value).Trim());
        if (TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.PlanetInfo, trimmed, out var translated))
        {
            return ExtractPlanetRiskValue(translated.Trim());
        }

        return trimmed;
    }

    private static string ExtractPlanetRiskValue(string value)
    {
        foreach (var label in new[]
                 {
                     PlanetRiskTitleLocalizedText,
                     "\u98ce\u9669\u7ea7\u522b:",
                     "\u5371\u9669\u7b49\u7ea7\uff1a",
                     "\u5371\u9669\u7b49\u7ea7:",
                     "HAZARD LEVEL:",
                     "Risk level:"
                 })
        {
            if (value.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                return value[label.Length..].Trim();
            }
        }

        return value;
    }

    private static string StripSimpleRichTextTags(string value)
    {
        if (value.IndexOf('<') < 0)
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length);
        var inTag = false;
        foreach (var ch in value)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (inTag)
            {
                if (ch == '>')
                {
                    inTag = false;
                }

                continue;
            }

            result.Append(ch);
        }

        return result.ToString();
    }

    public static (int translated, int seen) TranslateHudChatPrompts(HUDManager hud, string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        TranslateTmp(hud.typingIndicator, seen, ref translated, ref totalSeen);
        if (hud.chatTextField?.placeholder is TMP_Text placeholder)
        {
            TranslateTmp(placeholder, seen, ref translated, ref totalSeen);
        }

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateHudChatOutput(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return (0, 0);
        }

        var needsContinuation = TranslateHudChatOutputPass(hud, out var translated, out var seen);

        Plugin.LogTargetedTranslation(reason, translated, seen);
        if (needsContinuation)
        {
            ScheduleHudChatOutput(hud, reason + ".continuation");
        }

        return (translated, seen);
    }

    private static bool TranslateHudChatOutputPass(HUDManager hud, out int translated, out int seen)
    {
        var state = GetChatOutputState(hud);
        translated = TranslateHudChatHistory(hud, state, out var historyPending);
        seen = 0;
        var visiblePending = false;

        if (hud.chatText != null)
        {
            seen++;
            if (TranslateHudChatTextBudgeted(hud.chatText, state, out var textChanged, out visiblePending))
            {
                translated++;
                if (textChanged)
                {
                    Plugin.ReportTranslationHit();
                }
            }
        }

        return historyPending || visiblePending;
    }

    public static bool IsHudChatOutputTranslationPending(HUDManager? hud)
    {
        return hud != null && HudChatOutputTranslationPending.Contains(hud.GetInstanceID());
    }

    public static bool ShouldBypassHudChatOutputTmpText(TMP_Text? text, string? value, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var hud = HUDManager.Instance;
        if (hud == null || !ReferenceEquals(text, hud.chatText))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(value) &&
            !ChatWorkBudgetPolicy.ExceedsCharacterBudget(
                value,
                RuntimePerformanceSettings.ChatTranslationMaxCharactersPerFrame))
        {
            FontFallbackService.ApplyFallback(text, value);
        }

        if (RoundTransitionTextThrottle.ShouldDeferHudChatOutput())
        {
            MarkHudChatOutputTranslationPending(hud);
            return true;
        }

        if (IsHudChatOutputTranslationPending(hud))
        {
            return true;
        }

        MarkHudChatOutputTranslationPending(hud);
        ScheduleHudChatOutput(hud, reason);
        return true;
    }

    public static bool ShouldBypassPendingHudChatOutputTmpText(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        var hud = HUDManager.Instance;
        return hud != null &&
               ReferenceEquals(text, hud.chatText) &&
               (RoundTransitionTextThrottle.ShouldDeferHudChatOutput() ||
                IsHudChatOutputTranslationPending(hud));
    }

    public static void MarkHudChatOutputTranslationPending(HUDManager? hud)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown || !hud.isActiveAndEnabled)
        {
            return;
        }

        if (RoundTransitionTextThrottle.ShouldDeferHudChatOutput())
        {
            _hudChatOutputDeferredByRoundTransition = true;
            return;
        }

        var instanceId = hud.GetInstanceID();
        HudChatOutputTranslationPending.Add(instanceId);
        IncrementHudChatOutputTranslationGeneration(instanceId);
    }

    public static void ScheduleHudChatOutput(HUDManager? hud, string reason)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        if (RoundTransitionTextThrottle.ShouldDeferHudChatOutput())
        {
            _hudChatOutputDeferredByRoundTransition = true;
            return;
        }

        var instanceId = hud.GetInstanceID();
        HudChatOutputTranslationPending.Add(instanceId);
        if (HudChatOutputTranslationRunning.Contains(instanceId))
        {
            return;
        }

        if (!hud.isActiveAndEnabled)
        {
            HudChatOutputTranslationPending.Remove(instanceId);
            HudChatOutputTranslationGenerations.Remove(instanceId);
            return;
        }

        try
        {
            var scheduledGeneration = GetHudChatOutputTranslationGeneration(instanceId);
            HudChatOutputTranslationRunning.Add(instanceId);
            hud.StartCoroutine(TranslateHudChatOutputDeferred(hud, reason, instanceId, scheduledGeneration));
        }
        catch (Exception ex)
        {
            HudChatOutputTranslationPending.Remove(instanceId);
            HudChatOutputTranslationRunning.Remove(instanceId);
            HudChatOutputTranslationGenerations.Remove(instanceId);
            Plugin.Log.LogWarning($"HUD chat output translation scheduling failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static IEnumerator TranslateHudChatOutputDeferred(HUDManager hud, string reason, int instanceId, int scheduledGeneration)
    {
        var needsContinuation = false;
        try
        {
            yield return null;

            if (Plugin.IsRuntimeShuttingDown || hud == null || !hud.isActiveAndEnabled)
            {
                yield break;
            }

            needsContinuation = TranslateHudChatOutputPass(hud, out var translated, out var seen);
            Plugin.LogTargetedTranslation(reason + ".deferred", translated, seen);
        }
        finally
        {
            HudChatOutputTranslationRunning.Remove(instanceId);
            var currentGeneration = GetHudChatOutputTranslationGeneration(instanceId);
            if (!Plugin.IsRuntimeShuttingDown &&
                hud != null &&
                hud.isActiveAndEnabled &&
                (currentGeneration != scheduledGeneration || needsContinuation))
            {
                HudChatOutputTranslationPending.Add(instanceId);
                ScheduleHudChatOutput(hud, reason + ".coalesced");
            }
            else
            {
                HudChatOutputTranslationPending.Remove(instanceId);
                HudChatOutputTranslationGenerations.Remove(instanceId);
            }
        }
    }

    private static int IncrementHudChatOutputTranslationGeneration(int instanceId)
    {
        HudChatOutputTranslationGenerations.TryGetValue(instanceId, out var current);
        var next = current == int.MaxValue ? 1 : current + 1;
        HudChatOutputTranslationGenerations[instanceId] = next;
        return next;
    }

    private static int GetHudChatOutputTranslationGeneration(int instanceId)
    {
        return HudChatOutputTranslationGenerations.TryGetValue(instanceId, out var generation)
            ? generation
            : 0;
    }

    public static void FlushHudChatOutputDeferredByRoundTransition(HUDManager? hud, string reason)
    {
        if (!_hudChatOutputDeferredByRoundTransition)
        {
            return;
        }

        _hudChatOutputDeferredByRoundTransition = false;
        ScheduleHudChatOutput(hud, reason);
    }

    private static ChatOutputState GetChatOutputState(HUDManager hud)
    {
        var id = hud.GetInstanceID();
        if (ChatOutputStates.TryGetValue(id, out var state))
        {
            return state;
        }

        if (ChatOutputStates.Count >= 8)
        {
            ChatOutputStates.Clear();
        }

        state = new ChatOutputState();
        ChatOutputStates[id] = state;
        return state;
    }

    private static int TranslateHudChatHistory(HUDManager hud, ChatOutputState state, out bool needsContinuation)
    {
        needsContinuation = false;
        if (hud.ChatMessageHistory == null)
        {
            state.HistoryValidationCursor = 0;
            state.HistoryKnownCount = 0;
            state.HistoryEntryReferences.Clear();
            return 0;
        }

        var count = hud.ChatMessageHistory.Count;
        if (count == 0)
        {
            state.HistoryValidationCursor = 0;
            state.HistoryKnownCount = 0;
            state.HistoryEntryReferences.Clear();
            return 0;
        }

        if (count < state.HistoryKnownCount)
        {
            state.HistoryEntryReferences.Clear();
            state.HistoryValidationCursor = 0;
        }
        state.HistoryKnownCount = count;

        var trackedStart = Math.Max(0, count - ChatHistoryReferenceLimit);
        if (state.HistoryValidationCursor < trackedStart || state.HistoryValidationCursor >= count)
        {
            state.HistoryValidationCursor = trackedStart;
        }

        var start = state.HistoryValidationCursor;
        var end = Math.Min(count, start + RuntimePerformanceSettings.ChatTranslationMaxEntriesPerFrame);
        var translated = 0;
        for (var i = start; i < end; i++)
        {
            var entry = hud.ChatMessageHistory[i];
            if (state.HistoryEntryReferences.TryGetValue(i, out var previousEntry) &&
                ReferenceEquals(entry, previousEntry))
            {
                continue;
            }

            if (ChatWorkBudgetPolicy.ExceedsCharacterBudget(
                    entry,
                    RuntimePerformanceSettings.ChatTranslationMaxCharactersPerFrame))
            {
                state.HistoryEntryReferences.Set(i, entry, ChatHistoryReferenceLimit);
                continue;
            }

            if (TryTranslateChatLineCached(state, entry, out var rewritten) &&
                !string.Equals(entry, rewritten, StringComparison.Ordinal))
            {
                hud.ChatMessageHistory[i] = rewritten;
                translated++;
            }

            state.HistoryEntryReferences.Set(i, hud.ChatMessageHistory[i], ChatHistoryReferenceLimit);
        }

        if (end < count)
        {
            state.HistoryValidationCursor = end;
            needsContinuation = true;
        }
        else
        {
            state.HistoryValidationCursor = trackedStart;
        }

        return translated;
    }

    private static bool TranslateHudChatTextBudgeted(
        TMP_Text chatText,
        ChatOutputState state,
        out bool changedText,
        out bool needsContinuation)
    {
        changedText = false;
        needsContinuation = false;
        var current = chatText.text;
        if (string.IsNullOrEmpty(current))
        {
            state.LastOriginalText = current;
            state.LastTranslatedText = current;
            return false;
        }

        var characterBudget = RuntimePerformanceSettings.ChatTranslationMaxCharactersPerFrame;
        var visibleLineLimit = Math.Max(1, RuntimePerformanceSettings.ChatTranslationMaxEntriesPerFrame * 4);
        if (ChatWorkBudgetPolicy.ExceedsCharacterBudget(current, characterBudget) ||
            ChatWorkBudgetPolicy.ExceedsLineBudget(current, visibleLineLimit))
        {
            // One-shot translation avoids stale-snapshot starvation. Hard character and line caps
            // keep pathological third-party bulk output byte-for-byte unchanged with bounded work.
            state.LastOriginalText = null;
            state.LastTranslatedText = null;
            return false;
        }

        if (string.Equals(current, state.LastTranslatedText, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(current, state.LastOriginalText, StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(state.LastTranslatedText) &&
                !string.Equals(current, state.LastTranslatedText, StringComparison.Ordinal))
            {
                chatText.text = state.LastTranslatedText;
                FontFallbackService.ApplyFallback(chatText, state.LastTranslatedText);
                changedText = true;
                return true;
            }

            return false;
        }

        var changed = TryTranslateChatTextCached(state, current, out var rewritten) &&
                      !string.Equals(current, rewritten, StringComparison.Ordinal);
        state.LastOriginalText = current;
        state.LastTranslatedText = rewritten;
        FontFallbackService.ApplyFallback(chatText, rewritten);
        if (!changed)
        {
            return false;
        }

        chatText.text = rewritten;
        changedText = true;
        return true;
    }

    private static bool TryTranslateChatTextCached(ChatOutputState state, string source, out string translated)
    {
        translated = source;
        var newlineIndex = source.IndexOf('\n');
        if (newlineIndex < 0)
        {
            return TryTranslateChatLineCached(state, source, out translated);
        }

        var changed = false;
        var lineStart = 0;
        System.Text.StringBuilder? builder = null;
        while (lineStart <= source.Length)
        {
            var lineEnd = source.IndexOf('\n', lineStart);
            var hasNewline = lineEnd >= 0;
            if (!hasNewline)
            {
                lineEnd = source.Length;
            }

            var lineLength = lineEnd - lineStart;
            var line = lineLength == 0 ? string.Empty : source.Substring(lineStart, lineLength);
            if (TryTranslateChatLineCached(state, line, out var rewrittenLine) &&
                !string.Equals(line, rewrittenLine, StringComparison.Ordinal))
            {
                builder ??= new System.Text.StringBuilder(source.Length + 16);
                if (!changed && lineStart > 0)
                {
                    builder.Append(source, 0, lineStart);
                }

                builder.Append(rewrittenLine);
                changed = true;
            }
            else if (changed)
            {
                builder!.Append(line);
            }

            if (!hasNewline)
            {
                break;
            }

            if (changed)
            {
                builder!.Append('\n');
            }

            lineStart = lineEnd + 1;
        }

        if (!changed)
        {
            return false;
        }

        translated = builder!.ToString();
        return true;
    }

    private static bool TryTranslateChatLineCached(ChatOutputState state, string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (ContainsCjk(source) && !TranslationService.ChatDynamicTranslator.CanHandleCheap(source))
        {
            return false;
        }

        if (state.LineTranslationCache.TryGetValue(source, out var cached))
        {
            if (cached == null)
            {
                return false;
            }

            translated = cached;
            return true;
        }

        if (state.LineTranslationCache.Count >= ChatLineCacheLimit)
        {
            state.LineTranslationCache.Clear();
        }

        if (!TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.ChatOutput, source, out translated) ||
            string.Equals(source, translated, StringComparison.Ordinal))
        {
            state.LineTranslationCache[source] = null;
            translated = source;
            return false;
        }

        state.LineTranslationCache[source] = translated;
        return true;
    }

    public static (int translated, int seen) TranslateHudControlTips(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return (0, 0);
        }

        var seenObjects = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        TranslateTmpArrayTargeted(hud.controlTipLines, DynamicTextDomain.HudControlTip, seenObjects, ref translated, ref totalSeen);
        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateHudScrapItemBoxes(HUDManager? hud, string reason)
    {
        if (hud?.ScrapItemBoxes == null)
        {
            return (0, 0);
        }

        var seenObjects = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        foreach (var box in hud.ScrapItemBoxes)
        {
            if (box == null)
            {
                continue;
            }

            TranslateTmpTargeted(box.headerText, DynamicTextDomain.HudRewards, seenObjects, ref translated, ref totalSeen);
            TranslateTmpTargeted(box.valueText, DynamicTextDomain.HudRewards, seenObjects, ref translated, ref totalSeen);
        }

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateStunGrenadeControlTip(StunGrenadeItem? grenade, string reason)
    {
        var hud = HUDManager.Instance;
        if (grenade == null || hud?.controlTipLines == null)
        {
            return (0, 0);
        }

        var translated = 0;
        var seen = 0;
        foreach (var text in hud.controlTipLines)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            seen++;
            var rewritten = TranslationService.TranslateStunGrenadeControlTip(text.text, grenade.pinPulled);
            if (!string.Equals(rewritten, text.text, StringComparison.Ordinal))
            {
                text.text = rewritten;
                translated++;
                Plugin.ReportTranslationHit();
            }

            FontFallbackService.ApplyFallback(text, text.text);
        }

        Plugin.LogTargetedTranslation(reason, translated, seen);
        return (translated, seen);
    }

    public static (int translated, int seen) TranslatePlayerCursorTip(PlayerControllerB? player, string reason)
    {
        var text = player?.cursorTip;
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return (0, 0);
        }

        var source = text.text;
        var id = text.GetInstanceID();
        var parentId = GetParentInstanceId(text);
        if (!CursorTipStates.TryGetValue(id, out var state) || state.ParentId != parentId)
        {
            state = new CursorTipState { ParentId = parentId };
            CursorTipStates[id] = state;
        }

        if (string.Equals(source, state.LastTranslatedText, StringComparison.Ordinal))
        {
            if (!WasTranslationProcessed(text, source))
            {
                ApplyTmpStyleRepairs(text, source, "TargetedUiTranslator.TMP.HudControlTip");
                MarkTranslationProcessed(text, source);
            }

            Plugin.LogTargetedTranslation(reason, 0, 1);
            return (0, 1);
        }

        if (state.LastTranslatedText != null &&
            string.Equals(source, state.LastOriginalText, StringComparison.Ordinal) &&
            !string.Equals(source, state.LastTranslatedText, StringComparison.Ordinal))
        {
            text.text = state.LastTranslatedText;
            if (!WasTranslationProcessed(text, state.LastTranslatedText))
            {
                ApplyTmpStyleRepairs(text, state.LastTranslatedText, "TargetedUiTranslator.TMP.HudControlTip");
                MarkTranslationProcessed(text, state.LastTranslatedText);
            }

            Plugin.ReportTranslationHit();
            Plugin.LogTargetedTranslation(reason, 1, 1);
            return (1, 1);
        }

        if (TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.HudControlTip, source, out var value) &&
            !string.Equals(source, value, StringComparison.Ordinal))
        {
            text.text = value;
            state.LastOriginalText = source;
            state.LastTranslatedText = value;
            ApplyTmpStyleRepairs(text, value, "TargetedUiTranslator.TMP.HudControlTip");
            Plugin.ReportTranslationHit();
            MarkTranslationProcessed(text, value);
            Plugin.LogTargetedTranslation(reason, 1, 1);
            return (1, 1);
        }

        state.LastOriginalText = source;
        state.LastTranslatedText = source;
        ApplyTmpStyleRepairs(text, source, "TargetedUiTranslator.TMP.HudControlTip");
        MarkTranslationProcessed(text, source);
        Plugin.LogTargetedTranslation(reason, 0, 1);
        return (0, 1);
    }

    public static (int translated, int seen) TranslateVehicleStaticTexts(VehicleController? vehicle, string reason)
    {
        if (vehicle == null)
        {
            return (0, 0);
        }

        var seenObjects = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        Add(TranslateGameObject(vehicle.gameObject, seenObjects));

        try
        {
            FillScanBuffer(vehicle.gameObject, includeInactive: true, InteractTriggerScanBuffer);
            foreach (var trigger in InteractTriggerScanBuffer)
            {
                if (trigger == null)
                {
                    continue;
                }

                TranslateInteractTriggerField(trigger.hoverTip, out trigger.hoverTip, ref translated, ref totalSeen);
                TranslateInteractTriggerField(trigger.disabledHoverTip, out trigger.disabledHoverTip, ref translated, ref totalSeen);
                TranslateInteractTriggerField(trigger.holdTip, out trigger.holdTip, ref translated, ref totalSeen);
            }
        }
        finally
        {
            InteractTriggerScanBuffer.Clear();
        }

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    private static void TranslateInteractTriggerField(string? source, out string? translatedValue, ref int translated, ref int totalSeen)
    {
        translatedValue = source;
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        totalSeen++;
        var value = TranslateDynamicTargeted(source, DynamicTextDomain.HudControlTip);
        if (string.Equals(source, value, StringComparison.Ordinal))
        {
            return;
        }

        translatedValue = value;
        translated++;
        Plugin.ReportTranslationHit();
    }

    public static (int translated, int seen) TranslateHudPlayersFiredScreen(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return (0, 0);
        }

        var seenObjects = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        TranslateTmpTargeted(hud.EndOfRunStatsText, DynamicTextDomain.EndGame, seenObjects, ref translated, ref totalSeen);
        if (hud.playersFiredAnimator != null)
        {
            Add(TranslateGameObject(hud.playersFiredAnimator.gameObject, seenObjects));
        }

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
    }

    public static (int translated, int seen) TranslateHudVoteAndDeadlineText(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return (0, 0);
        }

        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        TranslateTmpTargeted(hud.holdButtonToEndGameEarlyText, DynamicTextDomain.EndGame, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.holdButtonToEndGameEarlyVotesText, DynamicTextDomain.EndGame, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.profitQuotaDaysLeftText, DynamicTextDomain.EndGame, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.profitQuotaDaysLeftText2, DynamicTextDomain.EndGame, seen, ref translated, ref totalSeen);
        TranslateTmpTargeted(hud.reachedProfitQuotaBonusText, DynamicTextDomain.EndGame, seen, ref translated, ref totalSeen);
        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateSaveFileSlot(SaveFileUISlot? slot, string reason)
    {
        if (slot == null)
        {
            return (0, 0);
        }

        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        TranslateTmpTargeted(slot.fileStatsText, DynamicTextDomain.GeneralFast, seen, ref translated, ref totalSeen);
        TranslateTmp(slot.fileNotCompatibleAlert, seen, ref translated, ref totalSeen);
        TranslateTmp(slot.specialTipText, seen, ref translated, ref totalSeen);
        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateAutosaveTextInLoadedScenes(string reason)
    {
        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;

        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                try
                {
                    FillScanBuffer(root, includeInactive: true, TmpTextScanBuffer);
                    TranslateAutosaveText(TmpTextScanBuffer, seen, ref translated, ref totalSeen);

                    FillScanBuffer(root, includeInactive: true, UiTextScanBuffer);
                    TranslateAutosaveText(UiTextScanBuffer, seen, ref translated, ref totalSeen);

                    FillScanBuffer(root, includeInactive: true, TextMeshScanBuffer);
                    TranslateAutosaveText(TextMeshScanBuffer, seen, ref translated, ref totalSeen);
                }
                finally
                {
                    ClearScanBuffers();
                }
            }
        }

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
    }

    public static (int translated, int seen) TranslateLobbySlotStatic(LobbySlot slot, string reason)
    {
        var seen = new HashSet<int>();
        var result = TranslateGameObject(slot.gameObject, seen);
        Plugin.LogTargetedTranslation(reason, result.translated, result.seen);
        return result;
    }

    public static string TranslateDynamic(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        return TranslationService.TranslateComposite(source);
    }

    public static string TranslateDynamicTargeted(string? source, DynamicTextDomain domain)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        return TranslationService.TryTranslateKnownDynamicTextTargeted(domain, source, out var translated)
            ? translated
            : TranslationService.TranslateComposite(source);
    }

    public static void TranslateItem(Item? item)
    {
        if (item == null)
        {
            return;
        }

        OriginalResourceStateService.CaptureItem(item);
        if (ItemIdentityCompatibilityService.TryTranslateItemName(item))
        {
            Plugin.ReportTranslationHit();
        }

        if (item.toolTips == null)
        {
            return;
        }

        for (var i = 0; i < item.toolTips.Length; i++)
        {
            var translated = TranslationService.TranslateHeldItemControlTip(item.toolTips[i]);
            if (translated != item.toolTips[i])
            {
                item.toolTips[i] = translated;
                Plugin.ReportTranslationHit();
            }
        }
    }

    private static void TranslateAutosaveText(IEnumerable<TMP_Text> texts, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        foreach (var text in texts)
        {
            if (text == null || !seenObjects.Add(text.GetInstanceID()) || !IsAutosavingText(text.text))
            {
                continue;
            }

            totalSeen++;
            if (TranslationService.TryTranslate(text.text, out var value))
            {
                text.text = value;
                FontFallbackService.ApplyFallback(text, value);
                translated++;
                Plugin.ReportTranslationHit();
            }
        }
    }

    private static void TranslateAutosaveText(IEnumerable<Text> texts, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        foreach (var text in texts)
        {
            if (text == null || !seenObjects.Add(text.GetInstanceID()) || !IsAutosavingText(text.text))
            {
                continue;
            }

            totalSeen++;
            if (TranslationService.TryTranslate(text.text, out var value))
            {
                text.text = value;
                translated++;
                Plugin.ReportTranslationHit();
            }
        }
    }

    private static void TranslateAutosaveText(IEnumerable<TextMesh> texts, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        foreach (var text in texts)
        {
            if (text == null || !seenObjects.Add(text.GetInstanceID()) || !IsAutosavingText(text.text))
            {
                continue;
            }

            totalSeen++;
            if (TranslationService.TryTranslate(text.text, out var value))
            {
                text.text = value;
                translated++;
                Plugin.ReportTranslationHit();
            }
        }
    }

    private static bool IsAutosavingText(string? text)
    {
        return string.Equals(text?.Trim(), "Autosaving...", StringComparison.OrdinalIgnoreCase);
    }

    private static (int translated, int seen) TranslateHudElement(HUDElement element, HashSet<int> seenObjects)
    {
        return element.canvasGroup == null ? (0, 0) : TranslateGameObject(element.canvasGroup.gameObject, seenObjects);
    }

    private static (int translated, int seen) TranslateRootOnly(GameObject? root, HashSet<int> seenObjects)
    {
        return root == null ? (0, 0) : TranslateGameObject(root, seenObjects);
    }

    private static (int translated, int seen) TranslateRootOnly(GameObject? root, HashSet<int> seenObjects, bool includeInactive)
    {
        return root == null ? (0, 0) : TranslateGameObject(root, seenObjects, includeInactive);
    }

    private static (int translated, int seen) TranslateTmpRoot(TMP_Text? text, HashSet<int> seenObjects)
    {
        var root = text == null ? null : text.transform.parent?.gameObject;
        return root == null ? (0, 0) : TranslateGameObject(root, seenObjects);
    }

    private static (int translated, int seen) TranslateGameObject(GameObject root, HashSet<int> seenObjects)
    {
        return TranslateGameObject(root, seenObjects, includeInactive: true);
    }

    private static (int translated, int seen) TranslateGameObject(GameObject root, HashSet<int> seenObjects, bool includeInactive)
    {
        var translated = 0;
        var totalSeen = 0;

        try
        {
            FillScanBuffer(root, includeInactive, TmpDropdownScanBuffer);
            foreach (var dropdown in TmpDropdownScanBuffer)
            {
                TranslateTmpDropdown(dropdown, ref translated);
            }

            FillScanBuffer(root, includeInactive, DropdownScanBuffer);
            foreach (var dropdown in DropdownScanBuffer)
            {
                TranslateDropdown(dropdown, ref translated);
            }

            FillScanBuffer(root, includeInactive, TmpTextScanBuffer);
            foreach (var text in TmpTextScanBuffer)
            {
                TranslateTmp(text, seenObjects, ref translated, ref totalSeen);
            }

            FillScanBuffer(root, includeInactive, UiTextScanBuffer);
            foreach (var text in UiTextScanBuffer)
            {
                TranslateUiText(text, seenObjects, ref translated, ref totalSeen);
            }

            FillScanBuffer(root, includeInactive, TextMeshScanBuffer);
            foreach (var text in TextMeshScanBuffer)
            {
                TranslateTextMesh(text, seenObjects, ref translated, ref totalSeen);
            }
        }
        finally
        {
            ClearScanBuffers();
        }

        return (translated, totalSeen);
    }

    private static IEnumerator TranslateMenuPanelOnceBudgeted(GameObject root, string reason, int instanceId)
    {
        var counts = new TranslationCounts();
        try
        {
            if (!MenuPanelTranslated.Add(instanceId))
            {
                yield break;
            }

            var seen = new HashSet<int>();
            yield return TranslateGameObjectBudgeted(root, seen, includeInactive: false, counts);
            Plugin.LogTargetedTranslation(reason, counts.Translated, counts.Seen);
        }
        finally
        {
            MenuPanelTranslationRunning.Remove(instanceId);
        }
    }

    private static IEnumerator TranslateQuickMenuBudgeted(
        QuickMenuManager menu,
        string reason,
        int instanceId,
        bool firstPass)
    {
        var counts = new TranslationCounts();
        try
        {
            var seen = new HashSet<int>();
            if (firstPass)
            {
                if (menu.playerListPanel != null && menu.playerListPanel.activeInHierarchy)
                {
                    yield return TranslateRootOnlyBudgeted(menu.playerListPanel, seen, counts);
                }
            }

            TranslateTmp(menu.interactTipText, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            TranslateTmp(menu.leaveGameClarificationText, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            TranslateTmp(menu.ConfirmKickPlayerText, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            TranslateTmp(menu.currentMicrophoneText, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            TranslateTmp(menu.changesNotAppliedText, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            TranslateTmp(menu.settingsBackButton, seen, ref counts.Translated, ref counts.Seen);
            if (AdvanceMenuBudget(counts))
            {
                yield return null;
            }

            Plugin.LogTargetedTranslation(reason, counts.Translated, counts.Seen);
        }
        finally
        {
            QuickMenuTranslationRunning.Remove(instanceId);
        }
    }

    private static IEnumerator TranslateRootOnlyBudgeted(GameObject? root, HashSet<int> seenObjects, TranslationCounts counts)
    {
        if (root == null)
        {
            yield break;
        }

        yield return TranslateGameObjectBudgeted(root, seenObjects, includeInactive: true, counts);
    }

    private static IEnumerator TranslateGameObjectBudgeted(
        GameObject root,
        HashSet<int> seenObjects,
        bool includeInactive,
        TranslationCounts counts)
    {
        var pending = new Stack<Transform>(64);
        pending.Push(root.transform);
        var traversedThisFrame = 0;
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == null || (!includeInactive && !current.gameObject.activeInHierarchy))
            {
                continue;
            }

            for (var childIndex = current.childCount - 1; childIndex >= 0; childIndex--)
            {
                pending.Push(current.GetChild(childIndex));
            }

            if (current.TryGetComponent<TMP_Dropdown>(out var tmpDropdown))
            {
                TranslateTmpDropdown(tmpDropdown, ref counts.Translated);
                if (AdvanceMenuBudget(counts))
                {
                    traversedThisFrame = 0;
                    yield return null;
                }
            }

            if (current.TryGetComponent<Dropdown>(out var dropdown))
            {
                TranslateDropdown(dropdown, ref counts.Translated);
                if (AdvanceMenuBudget(counts))
                {
                    traversedThisFrame = 0;
                    yield return null;
                }
            }

            if (current.TryGetComponent<TMP_Text>(out var tmpText))
            {
                TranslateTmp(tmpText, seenObjects, ref counts.Translated, ref counts.Seen);
                if (AdvanceMenuBudget(counts))
                {
                    traversedThisFrame = 0;
                    yield return null;
                }
            }

            if (current.TryGetComponent<Text>(out var uiText))
            {
                TranslateUiText(uiText, seenObjects, ref counts.Translated, ref counts.Seen);
                if (AdvanceMenuBudget(counts))
                {
                    traversedThisFrame = 0;
                    yield return null;
                }
            }

            if (current.TryGetComponent<TextMesh>(out var textMesh))
            {
                TranslateTextMesh(textMesh, seenObjects, ref counts.Translated, ref counts.Seen);
                if (AdvanceMenuBudget(counts))
                {
                    traversedThisFrame = 0;
                    yield return null;
                }
            }

            traversedThisFrame++;
            if (traversedThisFrame >= RuntimePerformanceSettings.MenuTranslationWorkBudgetPerFrame * 4)
            {
                traversedThisFrame = 0;
                yield return null;
            }
        }
    }

    private static bool AdvanceMenuBudget(TranslationCounts counts)
    {
        counts.WorkThisFrame++;
        if (counts.WorkThisFrame < RuntimePerformanceSettings.MenuTranslationWorkBudgetPerFrame)
        {
            return false;
        }

        counts.WorkThisFrame = 0;
        return true;
    }

    private static void TranslateGameObjectOpenFrameFast(GameObject? root, bool includeInactive)
    {
        if (root == null)
        {
            return;
        }

        try
        {
            FillScanBuffer(root, includeInactive, TmpDropdownScanBuffer);
            foreach (var dropdown in TmpDropdownScanBuffer)
            {
                TranslateTmpDropdownOptionsFastExact(dropdown);
            }

            FillScanBuffer(root, includeInactive, DropdownScanBuffer);
            foreach (var dropdown in DropdownScanBuffer)
            {
                TranslateDropdownOptionsFastExact(dropdown);
            }

            FillScanBuffer(root, includeInactive, TmpTextScanBuffer);
            foreach (var text in TmpTextScanBuffer)
            {
                TranslateTmpOpenFrameFast(text);
            }

            FillScanBuffer(root, includeInactive, UiTextScanBuffer);
            foreach (var text in UiTextScanBuffer)
            {
                TranslateUiTextOpenFrameFast(text);
            }

            FillScanBuffer(root, includeInactive, TextMeshScanBuffer);
            foreach (var text in TextMeshScanBuffer)
            {
                TranslateTextMeshOpenFrameFast(text);
            }
        }
        finally
        {
            ClearScanBuffers();
        }
    }

    private static void TranslateTmpOpenFrameFast(TMP_Text? text)
    {
        if (text == null || IsInputFieldTextComponent(text) || IsLobbySlotDynamicText(text))
        {
            return;
        }

        if (!TryTranslateFastUiText(text.text, out var translated) ||
            string.Equals(text.text, translated, StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        MarkTranslationProcessed(text, translated);
        Plugin.ReportTranslationHit();
    }

    private static void TranslateUiTextOpenFrameFast(Text? text)
    {
        if (text == null ||
            !TryTranslateFastUiText(text.text, out var translated) ||
            string.Equals(text.text, translated, StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        MarkTranslationProcessed(text, translated);
        Plugin.ReportTranslationHit();
    }

    private static void TranslateTextMeshOpenFrameFast(TextMesh? text)
    {
        if (text == null ||
            !TryTranslateFastUiText(text.text, out var translated) ||
            string.Equals(text.text, translated, StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        MarkTranslationProcessed(text, translated);
        Plugin.ReportTranslationHit();
    }

    private static void TranslateTmpDropdownOptionsFastExact(TMP_Dropdown? dropdown)
    {
        if (dropdown?.options == null)
        {
            return;
        }

        var changed = false;
        for (var i = 0; i < dropdown.options.Count; i++)
        {
            var option = dropdown.options[i];
            if (option == null ||
                !TryTranslateFastUiText(option.text, out var translated) ||
                string.Equals(option.text, translated, StringComparison.Ordinal))
            {
                continue;
            }

            option.text = translated;
            changed = true;
            Plugin.ReportTranslationHit();
        }

        if (changed)
        {
            SafeRefreshShownValue(dropdown);
        }
    }

    private static void TranslateDropdownOptionsFastExact(Dropdown? dropdown)
    {
        if (dropdown?.options == null)
        {
            return;
        }

        var changed = false;
        for (var i = 0; i < dropdown.options.Count; i++)
        {
            var option = dropdown.options[i];
            if (option == null ||
                !TryTranslateFastUiText(option.text, out var translated) ||
                string.Equals(option.text, translated, StringComparison.Ordinal))
            {
                continue;
            }

            option.text = translated;
            changed = true;
            Plugin.ReportTranslationHit();
        }

        if (changed)
        {
            SafeRefreshShownValue(dropdown);
        }
    }

    private static void TranslateTmpArray(TMP_Text[]? texts, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (texts == null)
        {
            return;
        }

        foreach (var text in texts)
        {
            TranslateTmp(text, seenObjects, ref translated, ref totalSeen);
        }
    }

    private static void TranslateTmpArrayTargeted(
        TMP_Text[]? texts,
        DynamicTextDomain domain,
        HashSet<int> seenObjects,
        ref int translated,
        ref int totalSeen)
    {
        if (texts == null)
        {
            return;
        }

        foreach (var text in texts)
        {
            TranslateTmpTargeted(text, domain, seenObjects, ref translated, ref totalSeen);
        }
    }

    private static void TranslateTmpTargeted(
        TMP_Text? text,
        DynamicTextDomain domain,
        HashSet<int> seenObjects,
        ref int translated,
        ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        totalSeen++;
        var alreadyProcessed = WasTranslationProcessed(text, text.text);
        if (TranslationService.TryTranslateKnownDynamicTextTargeted(domain, text.text, out var value) &&
            !string.Equals(text.text, value, StringComparison.Ordinal))
        {
            text.text = value;
            ApplyTmpStyleRepairs(text, value, $"TargetedUiTranslator.TMP.{domain}");
            translated++;
            Plugin.ReportTranslationHit();
            MarkTranslationProcessed(text, value);
            return;
        }

        if (alreadyProcessed)
        {
            return;
        }

        ApplyTmpStyleRepairs(text, text.text, $"TargetedUiTranslator.TMP.{domain}");
        MarkTranslationProcessed(text, text.text);
    }

    private static void TranslateTmp(TMP_Text? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        if (IsInputFieldTextComponent(text))
        {
            totalSeen++;
            if (WasTranslationProcessed(text, text.text))
            {
                return;
            }

            ApplyTmpStyleRepairs(text, text.text, "TargetedUiTranslator.TMP.Input");
            MarkTranslationProcessed(text, text.text);
            return;
        }

        if (IsLobbySlotDynamicText(text))
        {
            totalSeen++;
            if (WasTranslationProcessed(text, text.text))
            {
                return;
            }

            ApplyTmpStyleRepairs(text, text.text, "TargetedUiTranslator.TMP.Lobby");
            MarkTranslationProcessed(text, text.text);
            return;
        }

        totalSeen++;
        if (WasTranslationProcessed(text, text.text))
        {
            return;
        }

        if (TryTranslateStaticUiText(text.text, out var value))
        {
            text.text = value;
            ApplyTmpStyleRepairs(text, value, "TargetedUiTranslator.TMP");
            translated++;
            Plugin.ReportTranslationHit();
            MarkTranslationProcessed(text, value);
        }
        else
        {
            ApplyTmpStyleRepairs(text, text.text, "TargetedUiTranslator.TMP");
            RuntimeTextCollector.Record(text, text.text);
            MarkTranslationProcessed(text, text.text);
        }
    }

    private static void TranslateUiText(Text? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        totalSeen++;
        if (WasTranslationProcessed(text, text.text))
        {
            return;
        }

        if (TryTranslateStaticUiText(text.text, out var value))
        {
            text.text = value;
            ApplyUiStyleRepairs(text, "TargetedUiTranslator.UI.Text", value);
            translated++;
            Plugin.ReportTranslationHit();
            MarkTranslationProcessed(text, value);
        }
        else
        {
            ApplyUiStyleRepairs(text, "TargetedUiTranslator.UI.Text", text.text);
            RuntimeTextCollector.Record(text, text.text);
            MarkTranslationProcessed(text, text.text);
        }
    }

    private static void TranslateTextMesh(TextMesh? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        totalSeen++;
        if (WasTranslationProcessed(text, text.text))
        {
            return;
        }

        if (TryTranslateStaticUiText(text.text, out var value))
        {
            text.text = value;
            ApplyTextMeshStyleRepairs(text, "TargetedUiTranslator.TextMesh", value);
            translated++;
            Plugin.ReportTranslationHit();
            MarkTranslationProcessed(text, value);
        }
        else
        {
            ApplyTextMeshStyleRepairs(text, "TargetedUiTranslator.TextMesh", text.text);
            MarkTranslationProcessed(text, text.text);
        }
    }

    private static bool TryTranslateStaticUiText(string? source, out string translated)
    {
        if (TryTranslateFastUiText(source, out translated))
        {
            return true;
        }

        if (TranslationService.TryTranslate(source, out translated))
        {
            return true;
        }

        return AutomaticTranslationService.TryTranslateOrQueue(source, out translated);
    }

    private static bool TryTranslateFastUiText(string? source, out string translated)
    {
        if (TranslationService.TryTranslateKnownDynamicTextFast(source, out translated))
        {
            return true;
        }

        return TranslationService.TryTranslateFastExact(source, out translated);
    }

    private static bool WasTranslationProcessed(Component component, string? text)
    {
        if (!RuntimePerformanceSettings.EnableTargetedUiStyleRepairFastGate)
        {
            return false;
        }

        var id = component.GetInstanceID();
        var state = new ProcessedTextState(GetParentInstanceId(component), GetTextHash(text), GetStyleHash(component));
        return TranslationProcessedCache.TryGetValue(id, out var cached) &&
            cached.ParentId == state.ParentId &&
            cached.TextHash == state.TextHash &&
            cached.StyleHash == state.StyleHash;
    }

    private static void MarkTranslationProcessed(Component component, string? text)
    {
        var componentId = component.GetInstanceID();
        TranslationProcessedCache.Set(
            componentId,
            new ProcessedTextState(GetParentInstanceId(component), GetTextHash(text), GetStyleHash(component)),
            RuntimePerformanceSettings.ComponentTextCacheLimit);
    }

    private static int GetStyleHash(Component component)
    {
        unchecked
        {
            if (component is TMP_Text tmp)
            {
                var hash = 17;
                hash = (hash * 31) + (tmp.font == null ? 0 : tmp.font.GetInstanceID());
                hash = (hash * 31) + tmp.color.GetHashCode();
                hash = (hash * 31) + tmp.fontSize.GetHashCode();
                return hash;
            }

            if (component is Text uiText)
            {
                var hash = 23;
                hash = (hash * 31) + (uiText.font == null ? 0 : uiText.font.GetInstanceID());
                hash = (hash * 31) + uiText.color.GetHashCode();
                hash = (hash * 31) + uiText.fontSize;
                return hash;
            }

            if (component is TextMesh textMesh)
            {
                var hash = 29;
                hash = (hash * 31) + (textMesh.font == null ? 0 : textMesh.font.GetInstanceID());
                hash = (hash * 31) + textMesh.color.GetHashCode();
                hash = (hash * 31) + textMesh.fontSize.GetHashCode();
                return hash;
            }

            return 0;
        }
    }

    private static void ApplyTmpStyleRepairs(TMP_Text text, string? value, string stage)
    {
        FontFallbackService.ApplyFallback(text, value);
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static void ApplyUiStyleRepairs(Text text, string stage, string? value)
    {
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static void ApplyTextMeshStyleRepairs(TextMesh text, string stage, string? value)
    {
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static int GetParentInstanceId(Component? component)
    {
        var parent = component == null ? null : component.transform.parent;
        return parent == null ? 0 : parent.GetInstanceID();
    }

    private static int GetTextHash(string? text)
    {
        return text == null ? 0 : text.GetHashCode();
    }

    private static bool ContainsCjk(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if ((ch >= '\u3400' && ch <= '\u9fff') ||
                (ch >= '\uf900' && ch <= '\ufaff'))
            {
                return true;
            }
        }

        return false;
    }

    private static void TranslateTmpDropdown(TMP_Dropdown? dropdown, ref int translated)
    {
        if (dropdown?.options == null)
        {
            return;
        }

        var changed = false;
        var dropdownId = dropdown.GetInstanceID();
        for (var i = 0; i < dropdown.options.Count; i++)
        {
            var option = dropdown.options[i];
            if (option == null)
            {
                continue;
            }

            if (WasDropdownOptionProcessed(TmpDropdownOptionTextCache, dropdownId, i, option.text))
            {
                continue;
            }

            if (TranslationService.TryTranslate(option.text, out var value))
            {
                option.text = value;
                translated++;
                changed = true;
                Plugin.ReportTranslationHit();
            }
            else if (AutomaticTranslationService.TryTranslateOrQueue(option.text, out value))
            {
                option.text = value;
                translated++;
                changed = true;
                Plugin.ReportTranslationHit();
            }

            MarkDropdownOptionProcessed(TmpDropdownOptionTextCache, dropdownId, i, option.text);
        }

        if (changed)
        {
            SafeRefreshShownValue(dropdown);
        }
    }

    private static void TranslateDropdown(Dropdown? dropdown, ref int translated)
    {
        if (dropdown?.options == null)
        {
            return;
        }

        var changed = false;
        var dropdownId = dropdown.GetInstanceID();
        for (var i = 0; i < dropdown.options.Count; i++)
        {
            var option = dropdown.options[i];
            if (option == null)
            {
                continue;
            }

            if (WasDropdownOptionProcessed(DropdownOptionTextCache, dropdownId, i, option.text))
            {
                continue;
            }

            if (TranslationService.TryTranslate(option.text, out var value))
            {
                option.text = value;
                translated++;
                changed = true;
                Plugin.ReportTranslationHit();
            }
            else if (AutomaticTranslationService.TryTranslateOrQueue(option.text, out value))
            {
                option.text = value;
                translated++;
                changed = true;
                Plugin.ReportTranslationHit();
            }

            MarkDropdownOptionProcessed(DropdownOptionTextCache, dropdownId, i, option.text);
        }

        if (changed)
        {
            SafeRefreshShownValue(dropdown);
        }
    }

    internal static bool IsDropdownRefreshActive()
    {
        return _dropdownRefreshDepth > 0;
    }

    private static bool WasDropdownOptionProcessed(BoundedCache<int, List<int>> cache, int dropdownId, int optionIndex, string? text)
    {
        return cache.TryGetValue(dropdownId, out var hashes) &&
            optionIndex >= 0 &&
            optionIndex < hashes.Count &&
            hashes[optionIndex] == GetTextHash(text);
    }

    private static void MarkDropdownOptionProcessed(BoundedCache<int, List<int>> cache, int dropdownId, int optionIndex, string? text)
    {
        if (!cache.TryGetValue(dropdownId, out var hashes))
        {
            hashes = new List<int>();
            cache.Set(dropdownId, hashes, RuntimePerformanceSettings.ComponentTextCacheLimit);
        }

        while (hashes.Count <= optionIndex)
        {
            hashes.Add(0);
        }

        hashes[optionIndex] = GetTextHash(text);
    }

    internal static void SafeRefreshShownValue(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        _dropdownRefreshDepth++;
        try
        {
            dropdown.RefreshShownValue();
        }
        finally
        {
            _dropdownRefreshDepth--;
        }
    }

    internal static void SafeRefreshShownValue(Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        _dropdownRefreshDepth++;
        try
        {
            dropdown.RefreshShownValue();
        }
        finally
        {
            _dropdownRefreshDepth--;
        }
    }

    private static bool IsInputFieldTextComponent(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        var inputField = text.GetComponentInParent<TMP_InputField>(true);
        if (inputField == null)
        {
            return false;
        }

        return ReferenceEquals(inputField.textComponent, text);
    }

    private static bool IsLobbySlotDynamicText(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        var slot = text.GetComponentInParent<LobbySlot>(true);
        return slot != null && (ReferenceEquals(slot.LobbyName, text) || ReferenceEquals(slot.playerCount, text));
    }
}
