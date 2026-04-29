using System;
using System.Collections.Generic;
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

        Add(TranslateTmpRoot(hud.planetInfoHeaderText, seen));
        Add(TranslateTmpRoot(hud.planetInfoSummaryText, seen));
        Add(TranslateTmpRoot(hud.planetRiskLevelText, seen));
        TranslateTmp(hud.planetInfoHeaderText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.planetInfoSummaryText, seen, ref translated, ref totalSeen);
        TranslateTmp(hud.planetRiskLevelText, seen, ref translated, ref totalSeen);

        Plugin.LogTargetedTranslation(reason, translated, totalSeen);
        return (translated, totalSeen);

        void Add((int translated, int seen) part)
        {
            translated += part.translated;
            totalSeen += part.seen;
        }
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

    public static void TranslateItem(Item? item)
    {
        if (item == null)
        {
            return;
        }

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

    private static void TranslateTmp(TMP_Text? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        if (IsInputFieldTextComponent(text))
        {
            totalSeen++;
            FontFallbackService.ApplyFallback(text, text.text);
            return;
        }

        if (IsLobbySlotDynamicText(text))
        {
            totalSeen++;
            FontFallbackService.ApplyFallback(text, text.text);
            return;
        }

        totalSeen++;
        if (TranslationService.TryTranslate(text.text, out var value))
        {
            text.text = value;
            FontFallbackService.ApplyFallback(text, value);
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.TMP", value);
            translated++;
            Plugin.ReportTranslationHit();
        }
        else
        {
            FontFallbackService.ApplyFallback(text, text.text);
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.TMP", text.text);
            RuntimeTextCollector.Record(text, text.text);
        }
    }

    private static void TranslateUiText(Text? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        totalSeen++;
        if (TranslationService.TryTranslate(text.text, out var value))
        {
            text.text = value;
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.UI.Text", value);
            translated++;
            Plugin.ReportTranslationHit();
        }
        else
        {
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.UI.Text", text.text);
            RuntimeTextCollector.Record(text, text.text);
        }
    }

    private static void TranslateTextMesh(TextMesh? text, HashSet<int> seenObjects, ref int translated, ref int totalSeen)
    {
        if (text == null || !seenObjects.Add(text.GetInstanceID()))
        {
            return;
        }

        totalSeen++;
        if (TranslationService.TryTranslate(text.text, out var value))
        {
            text.text = value;
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.TextMesh", value);
            translated++;
            Plugin.ReportTranslationHit();
        }
        else
        {
            FontFallbackService.ApplySystemOnlineProbeFix(text, "TargetedUiTranslator.TextMesh", text.text);
        }
    }

    private static void TranslateTmpDropdown(TMP_Dropdown? dropdown, ref int translated)
    {
        if (dropdown?.options == null)
        {
            return;
        }

        var changed = false;
        foreach (var option in dropdown.options)
        {
            if (option == null)
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
        foreach (var option in dropdown.options)
        {
            if (option == null)
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
