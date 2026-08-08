namespace V81TestChn;

internal static partial class TextPatches
{
    private static void InputUtilsLoadIntoUiPostfix(KepRemapPanel __0)
    {
        InputUtilsKeybindLocalizationService.Apply(__0, "InputUtils.LcInputActionApi.LoadIntoUI");
    }

    private static void InputUtilsLocaleDataLoadedPostfix()
    {
        InputUtilsKeybindLocalizationService.InstallLocaleOverrides();
    }

    private static void InputUtilsPopOverTextSetPostfix(object __instance)
    {
        InputUtilsKeybindLocalizationService.ApplyPopOverFallback(__instance);
    }
}
