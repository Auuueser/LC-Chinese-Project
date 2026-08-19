using System;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class MenuSceneLocalizationService
{
    public static void ApplyMenuManager(MenuManager? menu, string reason)
    {
        if (menu == null)
        {
            return;
        }

        Plugin.LogPatchEntry(reason);
        TargetedUiTranslator.TranslateMenuManager(menu, reason);
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes(reason + ".autosave");
    }

    public static void ApplyMenuNotification(ref string notificationText, ref string buttonText)
    {
        notificationText = TargetedUiTranslator.TranslateDynamicTargeted(notificationText, DynamicTextDomain.MenuNotification);
        buttonText = TargetedUiTranslator.TranslateDynamic(buttonText);
    }

    public static void ApplyEnabledPanel(MenuManager? menu, GameObject? enablePanel, string reason)
    {
        if (enablePanel == null)
        {
            return;
        }

        if (!TargetedUiTranslator.ScheduleMenuPanelOnce(menu, enablePanel, reason))
        {
            TargetedUiTranslator.TranslateMenuPanelOnce(enablePanel, reason);
        }
    }

    public static void ApplyDeleteFilePrompt(DeleteFileButton? button, string reason)
    {
        if (button == null)
        {
            return;
        }

        var root = button.transform.parent?.gameObject ?? button.gameObject;
        TargetedUiTranslator.TranslateRoot(root, reason);
    }

    public static void ApplySaveFileSlot(SaveFileUISlot? slot, string reason)
    {
        TargetedUiTranslator.TranslateSaveFileSlot(slot, reason);
    }

    public static void ApplyPreInit(PreInitSceneScript? script, string reason)
    {
        if (script == null)
        {
            return;
        }

        TargetedUiTranslator.TranslatePreInit(script, reason);
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes(reason + ".autosave");
    }

    public static void ApplyQuickMenu(QuickMenuManager? menu, string reason)
    {
        if (menu == null)
        {
            return;
        }

        if (!TargetedUiTranslator.ScheduleQuickMenu(menu, reason))
        {
            TargetedUiTranslator.TranslateQuickMenu(menu, reason);
        }

    }

    public static void ApplyQuickMenuStartup(QuickMenuManager? menu, string reason)
    {
        if (menu == null)
        {
            return;
        }

        // Unity can invoke Start on the same frame that this menu is first
        // activated. Translate only the tiny developer TMP set before render;
        // the full menu hierarchy must remain on the budgeted path.
        TargetedUiTranslator.TranslateDebugMenuBeforeFirstRender(menu, reason + ".debug-before-render");
        TargetedUiTranslator.ScheduleQuickMenu(menu, reason + ".static");
    }

    public static void ApplyQuickMenuPanel(QuickMenuManager? menu, GameObject? enablePanel, string reason)
    {
        if (enablePanel == null)
        {
            return;
        }

        if (!TargetedUiTranslator.ScheduleMenuPanelOnce(menu, enablePanel, reason))
        {
            TargetedUiTranslator.TranslateMenuPanelOnce(enablePanel, reason);
        }

        if (IsRelatedPanel(enablePanel, menu?.ConfirmKickUserPanel))
        {
            ApplyKickConfirmationPanel(menu, reason + ".kick-confirmation");
        }
    }

    public static void ApplyQuickMenuLeaveGamePanel(GameObject? panel, string reason)
    {
        if (panel == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateQuickMenuLeaveGamePanel(panel, reason);
    }

    public static void ApplyAutosaveText(string reason)
    {
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes(reason);
    }

    public static void ApplyKickConfirmationPanel(QuickMenuManager? menu, string reason)
    {
        ApplyKickConfirmationPanel(menu, playerObjId: null, reason);
    }

    public static void ApplyKickConfirmationPanel(QuickMenuManager? menu, int playerObjId, string reason)
    {
        ApplyKickConfirmationPanel(menu, (int?)playerObjId, reason);
    }

    private static void ApplyKickConfirmationPanel(QuickMenuManager? menu, int? playerObjId, string reason)
    {
        if (menu?.ConfirmKickUserPanel == null)
        {
            return;
        }

        TranslateKickHeader(menu.ConfirmKickPlayerText, TryGetPlayerName(playerObjId));

        var panel = menu.ConfirmKickUserPanel.transform.Find("Panel");
        if (panel == null)
        {
            return;
        }

        TranslateExact(
            panel.Find("Reason/Text Area/Placeholder")?.GetComponent<TMP_Text>(),
            "No Reason Specified",
            "\u672a\u6307\u5b9a\u539f\u56e0");

        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<TMP_InputField>(true) != null)
            {
                continue;
            }

            var source = text.text?.Trim();
            if (string.Equals(source, "Ban", StringComparison.OrdinalIgnoreCase))
            {
                TranslateExact(text, "Ban", "\u5c01\u7981");
            }
            else if (string.Equals(source, "Kick", StringComparison.OrdinalIgnoreCase))
            {
                TranslateExact(text, "Kick", "\u8e22\u51fa\u73a9\u5bb6");
            }
            else if (string.Equals(source, "Cancel", StringComparison.OrdinalIgnoreCase))
            {
                TranslateExact(text, "Cancel", "\u53d6\u6d88");
            }
        }

        Plugin.LogPatchEntry(reason);
    }

    private static bool IsRelatedPanel(GameObject panel, GameObject? target)
    {
        if (target == null)
        {
            return false;
        }

        return ReferenceEquals(panel, target) ||
               panel.transform.IsChildOf(target.transform) ||
               target.transform.IsChildOf(panel.transform);
    }

    private static string? TryGetPlayerName(int? playerObjId)
    {
        if (!playerObjId.HasValue)
        {
            return null;
        }

        var players = StartOfRound.Instance?.allPlayerScripts;
        var index = playerObjId.Value;
        if (players == null || index < 0 || index >= players.Length)
        {
            return null;
        }

        return players[index]?.playerUsername;
    }

    private static void TranslateKickHeader(TMP_Text? text, string? actualPlayerName)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        var playerName = actualPlayerName;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            var source = text.text.Trim();
            const string prefix = "Kick out ";
            if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !source.EndsWith("?", StringComparison.Ordinal))
            {
                FontFallbackService.ApplyFallback(text, text.text);
                return;
            }

            playerName = source.Substring(prefix.Length, source.Length - prefix.Length - 1).Trim();
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        var translated = playerName.Equals("Player", StringComparison.OrdinalIgnoreCase)
            ? "\u662f\u5426\u8e22\u51fa\u73a9\u5bb6\uff1f"
            : $"\u662f\u5426\u8e22\u51fa {playerName}\uff1f";
        text.text = translated;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMax = Math.Max(text.fontSizeMax, text.fontSize);
        text.fontSizeMin = Math.Min(text.fontSizeMin, Math.Max(12f, text.fontSize * 0.6f));
        FontFallbackService.ApplyFallback(text, translated);
        Plugin.ReportTranslationHit();
    }

    private static void TranslateExact(TMP_Text? text, string english, string localized)
    {
        if (text == null)
        {
            return;
        }

        if (string.Equals(text.text?.Trim(), english, StringComparison.OrdinalIgnoreCase))
        {
            text.text = localized;
            Plugin.ReportTranslationHit();
        }

        FontFallbackService.ApplyFallback(text, text.text);
    }
}
