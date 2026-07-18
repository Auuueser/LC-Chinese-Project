using System;

namespace V81TestChn;

internal static class TerminalScreenLocalizationService
{
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
            var canRestoreModifyingText = false;
            var previousModifyingText = false;
            try
            {
                previousModifyingText = terminal.modifyingText;
                terminal.modifyingText = true;
                canRestoreModifyingText = true;
            }
            catch (Exception)
            {
                // Keep terminal localization best-effort if a future game build changes this private field.
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
                if (canRestoreModifyingText)
                {
                    try
                    {
                        terminal.modifyingText = previousModifyingText;
                    }
                    catch (Exception)
                    {
                        // Do not break terminal output during a game-version transition.
                    }
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

}
