using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class TerminalBroadcastLocalizationService
{
    private const string TerminalBroadcastCodeNodeSource = "Entered broadcast code.";
    private const string TerminalBroadcastCodeOverlaySource = "BROADCASTED SPECIAL CODE";

    public static void ApplyToAnimator(Animator? animator)
    {
        if (animator == null)
        {
            return;
        }

        foreach (var text in animator.GetComponentsInChildren<TMP_Text>(true) ?? Array.Empty<TMP_Text>())
        {
            if (TranslateTerminalBroadcastCodeText(text.text, out var translated))
            {
                text.text = translated;
                FontFallbackService.ApplyFallback(text, translated);
                Plugin.ReportTranslationHit();
            }
        }

        foreach (var text in animator.GetComponentsInChildren<Text>(true) ?? Array.Empty<Text>())
        {
            if (TranslateTerminalBroadcastCodeText(text.text, out var translated))
            {
                text.text = translated;
                Plugin.ReportTranslationHit();
            }
        }

        foreach (var text in animator.GetComponentsInChildren<TextMesh>(true) ?? Array.Empty<TextMesh>())
        {
            if (TranslateTerminalBroadcastCodeText(text.text, out var translated))
            {
                text.text = translated;
                Plugin.ReportTranslationHit();
            }
        }
    }

    private static bool TranslateTerminalBroadcastCodeText(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (!ContainsTerminalBroadcastCodeStatus(source))
        {
            return false;
        }

        var raw = source!;
        if (TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.Terminal, raw, out translated) ||
            TranslationService.TryTranslateFastExact(raw, out translated))
        {
            return true;
        }

        if (raw.IndexOf(TerminalBroadcastCodeOverlaySource, StringComparison.OrdinalIgnoreCase) >= 0 &&
            TranslationService.TryTranslateFastExact(TerminalBroadcastCodeOverlaySource, out var overlayTranslated))
        {
            translated = ReplaceTerminalBroadcastCodeOverlayText(raw, TerminalBroadcastCodeOverlaySource, overlayTranslated);
            return !string.Equals(raw, translated, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool ContainsTerminalBroadcastCodeStatus(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               (source.IndexOf(TerminalBroadcastCodeNodeSource, StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf(TerminalBroadcastCodeOverlaySource, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string ReplaceTerminalBroadcastCodeOverlayText(string source, string oldValue, string newValue)
    {
        var index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length - oldValue.Length + newValue.Length);
        var start = 0;
        while (index >= 0)
        {
            builder.Append(source, start, index - start);
            builder.Append(newValue);
            start = index + oldValue.Length;
            index = source.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
        }

        builder.Append(source, start, source.Length - start);
        return builder.ToString();
    }
}
