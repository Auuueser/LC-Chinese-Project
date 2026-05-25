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
}
