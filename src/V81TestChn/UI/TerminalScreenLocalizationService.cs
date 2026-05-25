using HarmonyLib;
using System;
using System.Reflection;

namespace V81TestChn;

internal static class TerminalScreenLocalizationService
{
    private static readonly FieldInfo? TerminalModifyingTextField = AccessTools.Field(typeof(Terminal), "modifyingText");

    public static void ApplyTextPostProcess(TerminalNode? node, ref string result)
    {
        var translated = TranslationService.TranslateTerminalOutputForNode(result, node != null && node.clearPreviousText);
        if (translated != result)
        {
            result = translated;
            Plugin.ReportTranslationHit();
        }
    }

    public static void ApplyScreenFallback(Terminal? terminal, string reason)
    {
        if (terminal?.screenText?.textComponent == null)
        {
            return;
        }

        var original = terminal.screenText.text ?? string.Empty;
        terminal.screenText.richText = true;
        terminal.screenText.textComponent.richText = true;
        var clearPreviousText = terminal.currentNode == null || terminal.currentNode.clearPreviousText;
        var translated = TranslationService.TranslateTerminalOutputForNode(original, clearPreviousText);
        if (!string.Equals(original, translated, StringComparison.Ordinal))
        {
            var hadModifyingText = TryGetTerminalModifyingText(terminal, out var previousModifyingText);
            if (hadModifyingText)
            {
                SetTerminalModifyingText(terminal, true);
            }

            try
            {
                terminal.currentText = translated;
                terminal.textAdded = 0;
                terminal.screenText.text = translated;
                terminal.currentText = terminal.screenText.text;
                terminal.textAdded = 0;
            }
            finally
            {
                if (hadModifyingText)
                {
                    SetTerminalModifyingText(terminal, previousModifyingText);
                }
            }

            Plugin.ReportTranslationHit();
        }

        terminal.screenText.richText = true;
        terminal.screenText.textComponent.richText = true;
        FontFallbackService.ApplyFallback(terminal.screenText.textComponent, terminal.screenText.text);
    }

    public static void ApplyFontFallback(Terminal? terminal)
    {
        if (terminal?.screenText?.textComponent == null)
        {
            return;
        }

        FontFallbackService.ApplyFallback(terminal.screenText.textComponent, terminal.screenText.text);
    }

    private static bool TryGetTerminalModifyingText(Terminal terminal, out bool value)
    {
        value = false;
        if (TerminalModifyingTextField == null)
        {
            return false;
        }

        try
        {
            if (TerminalModifyingTextField.GetValue(terminal) is bool current)
            {
                value = current;
                return true;
            }
        }
        catch
        {
            // Best-effort guard only; fallback translation still works without this private field.
        }

        return false;
    }

    private static void SetTerminalModifyingText(Terminal terminal, bool value)
    {
        if (TerminalModifyingTextField == null)
        {
            return;
        }

        try
        {
            TerminalModifyingTextField.SetValue(terminal, value);
        }
        catch
        {
            // Best-effort guard only; avoid breaking terminal output if the private field changes.
        }
    }
}
