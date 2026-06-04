using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static void LethalConfigConfigMenuOpenPostfix(object __instance)
    {
        if (__instance is Component component)
        {
            ExternalEnglishCompatibilityUiService.TranslateRoot(component.gameObject, includeInactive: true, "LethalConfig.ConfigMenu.Open");
        }
    }

    private static void LethalConfigNotificationSetContentPrefix(ref string text, ref string button)
    {
        if (ExternalEnglishCompatibilityService.TryTranslateFast(text, out var translatedText))
        {
            text = translatedText;
        }

        if (ExternalEnglishCompatibilityService.TryTranslateFast(button, out var translatedButton))
        {
            button = translatedButton;
        }
    }

    private static void LethalConfigNotificationOpenPostfix(object __instance)
    {
        if (__instance is Component component)
        {
            ExternalEnglishCompatibilityUiService.TranslateRoot(component.gameObject, includeInactive: true, "LethalConfig.ConfigMenuNotification.Open");
        }
    }

    private static void OpenBodyCamsOverlayUpdateTextPostfix(TMP_Text ___textRenderer)
    {
        ExternalEnglishCompatibilityUiService.TranslateTmpTextKnownNonInput(___textRenderer, "OpenBodyCams.OverlayManager.UpdateText");
    }

    private static void TmpInputFieldOnEnablePostfix(TMP_InputField __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        ExternalEnglishCompatibilityUiService.TranslateTmpInputPlaceholder(__instance, "TMP_InputField.OnEnable");
    }

    private static void BetterSavesInitializeBetterSavesPostfix()
    {
        var filesPanel = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel");
        ExternalEnglishCompatibilityUiService.TranslateRoot(filesPanel, includeInactive: true, "BetterSaves.InitializeBetterSaves");
    }

    private static void BetterSavesDeleteFileButtonUpdateFileToDeletePostfix(int ___fileToDelete, TMP_Text ___deleteFileText)
    {
        if (___fileToDelete <= 0 || ___deleteFileText == null)
        {
            return;
        }

        if (ExternalEnglishCompatibilityService.TryTranslateBetterSavesDeleteFilePrompt(___deleteFileText.text, ___fileToDelete, out var translated))
        {
            ApplyTranslatedTmpText(___deleteFileText, translated, "BetterSaves.DeleteFileButton.UpdateFileToDelete");
        }
        else
        {
            ExternalEnglishCompatibilityUiService.TranslateTmpText(___deleteFileText, "BetterSaves.DeleteFileButton.UpdateFileToDelete");
        }

        var confirmationRoot = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation");
        ExternalEnglishCompatibilityUiService.TranslateRoot(confirmationRoot, includeInactive: true, "BetterSaves.DeleteFileConfirmation");
    }

    private static void AdvancedFeaturesEndscreenOpenPostfix(GameObject ___Container)
    {
        ExternalEnglishCompatibilityUiService.TranslateRoot(___Container, includeInactive: true, "AdvancedFeatures.Endscreen.Open");
    }

    private static void ApplyTranslatedTmpText(TMP_Text text, string translated, string reason)
    {
        if (string.Equals(text.text, translated, System.StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        Plugin.ReportTranslationHit();
    }
}
