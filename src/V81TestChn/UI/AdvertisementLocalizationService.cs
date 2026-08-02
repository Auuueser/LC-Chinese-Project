namespace V81TestChn;

internal static class AdvertisementLocalizationService
{
    public static void Prepare(ref string itemName, ref string saleText)
    {
        itemName = TranslateItemName(itemName);
        saleText = TranslateSaleText(saleText);
    }

    public static string TranslateItemName(string? itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return itemName ?? string.Empty;
        }

        return TranslationService.BuildTerminalLocalizedItemName(itemName);
    }

    public static string TranslateSaleText(string? saleText)
    {
        if (string.IsNullOrWhiteSpace(saleText))
        {
            return saleText ?? string.Empty;
        }

        return ExternalEnglishCompatibilityService.TryTranslateFast(saleText, out var translated)
            ? translated
            : saleText;
    }

    public static void ApplyFontFallback(HUDManager? hudManager)
    {
        if (hudManager == null)
        {
            return;
        }

        FontFallbackService.ApplyFallback(hudManager.advertTopText, hudManager.advertTopText?.text);
        FontFallbackService.ApplyFallback(hudManager.advertBottomText, hudManager.advertBottomText?.text);
    }
}
