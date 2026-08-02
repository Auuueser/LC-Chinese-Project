namespace V81TestChn;

internal static partial class TextPatches
{
    private static void HudManagerBeginDisplayAdPrefix(ref string itemName, ref string saleText)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        AdvertisementLocalizationService.Prepare(ref itemName, ref saleText);
    }

    private static void HudManagerBeginDisplayAdPostfix(HUDManager __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        AdvertisementLocalizationService.ApplyFontFallback(__instance);
    }
}
