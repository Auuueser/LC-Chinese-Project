using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<int, ProcessedTextState> TranslationProcessedCache = new();
    private static readonly Dictionary<int, List<int>> TmpDropdownOptionTextCache = new();
    private static readonly Dictionary<int, List<int>> DropdownOptionTextCache = new();
    private static readonly Dictionary<int, ChatOutputState> ChatOutputStates = new();
    private const int ProcessedTextCacheLimit = 8192;
    private const int ChatLineCacheLimit = 256;
    private static bool _sceneUnloadSubscribed;

    private readonly struct ProcessedTextState
    {
        public ProcessedTextState(int parentId, int textHash)
        {
            ParentId = parentId;
            TextHash = textHash;
        }

        public int ParentId { get; }
        public int TextHash { get; }
    }

    private sealed class ChatOutputState
    {
        public int HistoryProcessedCount;
        public int HistoryTailHash;
        public string? LastOriginalText;
        public string? LastTranslatedText;
        public readonly Dictionary<string, string?> LineTranslationCache = new(StringComparer.Ordinal);
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
        TranslationProcessedCache.Clear();
        TmpDropdownOptionTextCache.Clear();
        DropdownOptionTextCache.Clear();
        ChatOutputStates.Clear();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        ClearCaches();
        CustomLocalizationExtensionService.ClearRuntimeCaches();
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
            Add(TranslateGameObject(menu.gameObject, seen));
            Add(TranslateRootOnly(menu.menuContainer, seen));
            Add(TranslateRootOnly(menu.mainButtonsPanel, seen));
            Add(TranslateRootOnly(menu.leaveGameConfirmPanel, seen));
            Add(TranslateRootOnly(menu.settingsPanel, seen));
            Add(TranslateRootOnly(menu.ConfirmKickUserPanel, seen));
            Add(TranslateRootOnly(menu.KeybindsPanel, seen));
            Add(TranslateRootOnly(menu.playerListPanel, seen));
            Add(TranslateRootOnly(menu.debugMenuUI, seen));
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

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
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

        var state = GetChatOutputState(hud);
        var translated = 0;
        var seen = 0;

        translated += TranslateHudChatHistory(hud, state);

        if (hud.chatText != null)
        {
            seen++;
            if (TranslateHudChatText(hud.chatText, state, out var textChanged))
            {
                translated++;
                if (textChanged)
                {
                    Plugin.ReportTranslationHit();
                }
            }
        }

        Plugin.LogTargetedTranslation(reason, translated, seen);
        return (translated, seen);
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

    private static int TranslateHudChatHistory(HUDManager hud, ChatOutputState state)
    {
        if (hud.ChatMessageHistory == null)
        {
            state.HistoryProcessedCount = 0;
            state.HistoryTailHash = 0;
            return 0;
        }

        var count = hud.ChatMessageHistory.Count;
        var currentTailHash = count == 0 ? 0 : GetTextHash(hud.ChatMessageHistory[count - 1]);
        if (count == state.HistoryProcessedCount && currentTailHash == state.HistoryTailHash)
        {
            return 0;
        }

        var start = count > state.HistoryProcessedCount ? state.HistoryProcessedCount : 0;
        var translated = 0;
        for (var i = start; i < count; i++)
        {
            var entry = hud.ChatMessageHistory[i];
            if (!TryTranslateChatLineCached(state, entry, out var rewritten) ||
                string.Equals(entry, rewritten, StringComparison.Ordinal))
            {
                continue;
            }

            hud.ChatMessageHistory[i] = rewritten;
            translated++;
        }

        state.HistoryProcessedCount = count;
        state.HistoryTailHash = count == 0 ? 0 : GetTextHash(hud.ChatMessageHistory[count - 1]);
        return translated;
    }

    private static bool TranslateHudChatText(TMP_Text chatText, ChatOutputState state, out bool changedText)
    {
        changedText = false;
        var current = chatText.text;
        if (string.IsNullOrEmpty(current))
        {
            state.LastOriginalText = current;
            state.LastTranslatedText = current;
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

        state.LastOriginalText = current;
        if (!TryTranslateChatTextCached(state, current, out var rewritten) ||
            string.Equals(current, rewritten, StringComparison.Ordinal))
        {
            state.LastTranslatedText = current;
            FontFallbackService.ApplyFallback(chatText, current);
            return false;
        }

        state.LastTranslatedText = rewritten;
        chatText.text = rewritten;
        FontFallbackService.ApplyFallback(chatText, rewritten);
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
        if (player?.cursorTip == null)
        {
            return (0, 0);
        }

        var seen = new HashSet<int>();
        var translated = 0;
        var totalSeen = 0;
        TranslateTmpTargeted(player.cursorTip, DynamicTextDomain.HudControlTip, seen, ref translated, ref totalSeen);
        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);
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

        foreach (var trigger in vehicle.GetComponentsInChildren<InteractTrigger>(true))
        {
            if (trigger == null)
            {
                continue;
            }

            TranslateInteractTriggerField(trigger.hoverTip, out trigger.hoverTip, ref translated, ref totalSeen);
            TranslateInteractTriggerField(trigger.disabledHoverTip, out trigger.disabledHoverTip, ref translated, ref totalSeen);
            TranslateInteractTriggerField(trigger.holdTip, out trigger.holdTip, ref translated, ref totalSeen);
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
                TranslateAutosaveText(root.GetComponentsInChildren<TMP_Text>(true), seen, ref translated, ref totalSeen);
                TranslateAutosaveText(root.GetComponentsInChildren<Text>(true), seen, ref translated, ref totalSeen);
                TranslateAutosaveText(root.GetComponentsInChildren<TextMesh>(true), seen, ref translated, ref totalSeen);
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
        if (RuntimeIconsCompatibilityService.TryTranslateItemName(item))
        {
            Plugin.ReportTranslationHit();
        }

        if (item.toolTips == null)
        {
            return;
        }

        for (var i = 0; i < item.toolTips.Length; i++)
        {
            var translated = TranslateDynamic(item.toolTips[i]);
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

    private static (int translated, int seen) TranslateTmpRoot(TMP_Text? text, HashSet<int> seenObjects)
    {
        var root = text == null ? null : text.transform.parent?.gameObject;
        return root == null ? (0, 0) : TranslateGameObject(root, seenObjects);
    }

    private static (int translated, int seen) TranslateGameObject(GameObject root, HashSet<int> seenObjects)
    {
        var translated = 0;
        var totalSeen = 0;

        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            TranslateTmpDropdown(dropdown, ref translated);
        }

        foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
        {
            TranslateDropdown(dropdown, ref translated);
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            TranslateTmp(text, seenObjects, ref translated, ref totalSeen);
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            TranslateUiText(text, seenObjects, ref translated, ref totalSeen);
        }

        foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
        {
            TranslateTextMesh(text, seenObjects, ref translated, ref totalSeen);
        }

        return (translated, totalSeen);
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
            ApplyTmpStyleRepairs(text, text.text, "TargetedUiTranslator.TMP.Input");
            MarkTranslationProcessed(text, text.text);
            return;
        }

        if (IsLobbySlotDynamicText(text))
        {
            totalSeen++;
            ApplyTmpStyleRepairs(text, text.text, "TargetedUiTranslator.TMP.Lobby");
            MarkTranslationProcessed(text, text.text);
            return;
        }

        totalSeen++;
        var alreadyTranslated = WasTranslationProcessed(text, text.text);
        if (!alreadyTranslated && TranslationService.TryTranslate(text.text, out var value))
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
            if (!alreadyTranslated)
            {
                RuntimeTextCollector.Record(text, text.text);
            }

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
        var alreadyTranslated = WasTranslationProcessed(text, text.text);
        if (!alreadyTranslated && TranslationService.TryTranslate(text.text, out var value))
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
            if (!alreadyTranslated)
            {
                RuntimeTextCollector.Record(text, text.text);
            }

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
        var alreadyTranslated = WasTranslationProcessed(text, text.text);
        if (!alreadyTranslated && TranslationService.TryTranslate(text.text, out var value))
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

    private static bool WasTranslationProcessed(Component component, string? text)
    {
        var id = component.GetInstanceID();
        var state = new ProcessedTextState(GetParentInstanceId(component), GetTextHash(text));
        return TranslationProcessedCache.TryGetValue(id, out var cached) &&
            cached.ParentId == state.ParentId &&
            cached.TextHash == state.TextHash;
    }

    private static void MarkTranslationProcessed(Component component, string? text)
    {
        if (TranslationProcessedCache.Count >= ProcessedTextCacheLimit)
        {
            TranslationProcessedCache.Clear();
        }

        TranslationProcessedCache[component.GetInstanceID()] = new ProcessedTextState(
            GetParentInstanceId(component),
            GetTextHash(text));
    }

    private static void ApplyTmpStyleRepairs(TMP_Text text, string? value, string stage)
    {
        FontFallbackService.ApplyFallback(text, value);
        FontFallbackService.ApplySystemOnlineProbeFix(text, stage, value);
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static void ApplyUiStyleRepairs(Text text, string stage, string? value)
    {
        FontFallbackService.ApplySystemOnlineProbeFix(text, stage, value);
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static void ApplyTextMeshStyleRepairs(TextMesh text, string stage, string? value)
    {
        FontFallbackService.ApplySystemOnlineProbeFix(text, stage, value);
        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, stage);
        CustomLocalizationExtensionService.ApplyStyle(text, value, allowRegexStyle: true);
    }

    private static int GetParentInstanceId(Component component)
    {
        return component.transform.parent == null ? 0 : component.transform.parent.GetInstanceID();
    }

    private static int GetTextHash(string? text)
    {
        return text == null ? 0 : text.GetHashCode();
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

    private static bool WasDropdownOptionProcessed(Dictionary<int, List<int>> cache, int dropdownId, int optionIndex, string? text)
    {
        return cache.TryGetValue(dropdownId, out var hashes) &&
            optionIndex >= 0 &&
            optionIndex < hashes.Count &&
            hashes[optionIndex] == GetTextHash(text);
    }

    private static void MarkDropdownOptionProcessed(Dictionary<int, List<int>> cache, int dropdownId, int optionIndex, string? text)
    {
        if (cache.Count >= ProcessedTextCacheLimit)
        {
            cache.Clear();
        }

        if (!cache.TryGetValue(dropdownId, out var hashes))
        {
            hashes = new List<int>();
            cache[dropdownId] = hashes;
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
