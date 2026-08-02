using System;
using UnityEngine;

namespace V81TestChn;

internal static class SettingsLocalizationService
{
    private const string NoDeviceFoundSource = "No device found \n (click to refresh)";
    private const string NoDeviceFoundLocalized = "未找到设备\n（点击刷新）";
    private const string CurrentDevicePrefix = "Current input device: \n ";
    private const string CurrentDeviceLocalizedPrefix = "当前输入设备：\n";

    public static void LocalizeOptionText(SettingsOptionType optionType, ref string setToText)
    {
        if (optionType != SettingsOptionType.MicDevice || string.IsNullOrEmpty(setToText))
        {
            return;
        }

        if (string.Equals(setToText, NoDeviceFoundSource, StringComparison.Ordinal))
        {
            setToText = NoDeviceFoundLocalized;
            return;
        }

        if (setToText.StartsWith(CurrentDevicePrefix, StringComparison.Ordinal))
        {
            setToText = CurrentDeviceLocalizedPrefix + setToText.Substring(CurrentDevicePrefix.Length);
        }
    }

    public static void ApplyConfirmChangesPanel(bool visible, string reason)
    {
        if (!visible || Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        var menuManager = UnityEngine.Object.FindObjectOfType<MenuManager>();
        if (menuManager != null && menuManager.PleaseConfirmChangesSettingsPanel != null)
        {
            TargetedUiTranslator.TranslateRoot(menuManager.PleaseConfirmChangesSettingsPanel, reason + ".menu");
            return;
        }

        var quickMenuManager = UnityEngine.Object.FindObjectOfType<QuickMenuManager>();
        if (quickMenuManager != null && quickMenuManager.PleaseConfirmChangesSettingsPanel != null)
        {
            TargetedUiTranslator.TranslateRoot(quickMenuManager.PleaseConfirmChangesSettingsPanel, reason + ".quick-menu");
        }
    }
}
