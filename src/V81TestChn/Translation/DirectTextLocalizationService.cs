using TMPro;

namespace V81TestChn;

internal static class DirectTextLocalizationService
{
    public static void ApplyHangarShipDoor(HangarShipDoor? door, string reason)
    {
        if (door == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateRoot(door.hydraulicsDisplay, reason);
        ApplyComposite(door.doorPowerDisplay, reason);
    }

    public static bool ApplyComposite(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var original = text.text;
        var translated = TranslationService.TranslateComposite(original);
        if (string.Equals(original, translated, System.StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            RuntimeTextCollector.Record(text, original);
            return false;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        Plugin.ReportTranslationHit();
        return true;
    }
}
