using System;
using TMPro;

namespace V81TestChn;

internal static class MapScreenLocalizationService
{
    public static bool ApplyDescriptionTranslation(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var original = text.text;
        ApplyTypography(text);
        if (!TranslationService.TryTranslateMapScreenDescription(original, out var translated) ||
            string.Equals(original, translated, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            RuntimeTextCollector.Record(text, original);
            return false;
        }

        text.text = translated;
        ApplyTypography(text);
        FontFallbackService.ApplyFallback(text, translated);
        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    public static void ApplySetTextPrefix(TMP_Text? text, ref string value, string reason)
    {
        if (text == null)
        {
            return;
        }

        ApplyTypography(text);
        FontFallbackService.ApplyFallback(text, value);
        if (TranslationService.TryTranslateMapScreenDescription(value, out var translated))
        {
            value = translated;
            FontFallbackService.ApplyFallback(text, translated);
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(text, value);
        }

        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, value);
    }

    public static void ApplyTypography(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.characterSpacing = 0f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.paragraphSpacing = 0f;
        if (text.fontSize > 26f)
        {
            text.fontSize = 26f;
        }
    }
}
