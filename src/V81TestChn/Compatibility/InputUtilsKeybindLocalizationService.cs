using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class InputUtilsKeybindLocalizationService
{
    private const string RebindButtonTypeName = "LethalCompanyInputUtils.Components.RebindButton";

    private static readonly Dictionary<string, string> LocaleTokenEntries = new(StringComparer.Ordinal)
    {
        ["Context.Global"] = "全局范围",
        ["Context.Local"] = "本地模式",
        ["OverridePriority.PreferGlobal"] = "优先使用全局绑定",
        ["OverridePriority.PreferLocal"] = "优先使用本地绑定",
        ["OverridePriority.GlobalOnly"] = "仅使用全局绑定",
        ["OverridePriority.LocalOnly"] = "仅使用本地绑定",
        ["RebindButton.ResetToDefault.PopOver"] = "恢复默认绑定",
        ["RebindButton.RemoveBind.PopOver"] = "移除绑定",
        ["RebindButton.NotSupportedDeviceBind.PopOver"] = "此设备不支持该绑定",
        ["RebindButton.DisabledByOverride.PopOver"] =
            "此按键绑定正被[OppositeContext]设置覆盖。\n\n" +
            "点击左上角的[OppositeContext]按钮可以查看对应设置。\n\n" +
            "也可以通过该按钮下方显示“[BindingOverridePriorityConfigValue]”的下拉框调整加载优先级。[OptionalPreferPriority]",
        ["RebindButton.DisabledByOverride.PopOver.Optional"] =
            "\n\n当前优先级为“[BindingOverridePriorityConfigValue]”。重新绑定此项（相同或不同按键）即可解决。",
        ["BindingOverrideContextSwitch.Info.PopOver"] =
            "你可以在这里切换当前查看或编辑的按键设置。\n\n" +
            "全局范围 - 这些按键设置独立于当前模组包或配置文件保存。\n\n" +
            "本地模式 - 这些按键设置随当前模组包、配置文件或手动安装保存，更新模组包或配置文件时可能被覆盖。\n\n" +
            "原版《致命公司》的按键设置始终按全局设置保存。",
        ["BindingOverridePriorityDropdown.Info.PopOver"] =
            "决定加载按键设置时的优先级。\n\n" +
            "优先使用全局绑定 - 全局按键设置优先于模组包、配置文件或手动安装中的设置\n\n" +
            "优先使用本地绑定 - 模组包、配置文件或手动安装中的按键设置优先于全局设置\n\n" +
            "仅使用全局绑定 - 只使用全局按键设置\n\n" +
            "仅使用本地绑定 - 只使用模组包、配置文件或手动安装中的按键设置",
        ["LegacyControls.Button.Label"] = "显示旧版按键（{0} 项）"
    };

    private static readonly Dictionary<string, string> ExactEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["REMAP CONTROLS"] = "按键重映射",
        ["Lethal Company"] = "致命公司",
        ["Keyboard/Mouse"] = "键盘/鼠标",
        ["Keyboard & Mouse"] = "键盘/鼠标",
        ["Controller"] = "手柄",
        ["Global"] = "全局范围",
        ["Local"] = "本地模式",
        ["Prefer Global"] = "优先使用全局绑定",
        ["Prefer Local"] = "优先使用本地绑定",
        ["Global Only"] = "仅使用全局绑定",
        ["Local Only"] = "仅使用本地绑定",
        ["Set to defaults"] = "恢复默认设置",
        ["> Set to defaults"] = "> 恢复默认设置",
        ["Back"] = "返回",
        ["> Back"] = "> 返回"
    };

    public static bool ShouldPreservePhysicalKeyLabel(TMP_Text? text, string? value)
    {
        // "Enter" is also a valid standalone control-tip action in the base game. Only bypass
        // that translation when InputUtils is writing the physical keyboard binding label.
        if (text == null || !string.Equals(value, "Enter", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parents = text.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var parent in parents)
        {
            if (parent != null && string.Equals(parent.GetType().FullName, RebindButtonTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static void Apply(KepRemapPanel? panel, string reason)
    {
        if (panel == null)
        {
            return;
        }

        InstallLocaleOverrides();

        var translated = 0;
        var seen = 0;
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null)
            {
                continue;
            }

            seen++;
            var source = text.text;
            if (!TryTranslate(source, out var localized) || string.Equals(source, localized, StringComparison.Ordinal))
            {
                continue;
            }

            text.SetText(localized, true);
            FontFallbackService.ApplyFallback(text, localized);
            translated++;
            Plugin.ReportTranslationHit();
        }

        foreach (var dropdown in panel.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (dropdown == null)
            {
                continue;
            }

            var changed = false;
            foreach (var option in dropdown.options)
            {
                if (option == null || !TryTranslate(option.text, out var localized) ||
                    string.Equals(option.text, localized, StringComparison.Ordinal))
                {
                    continue;
                }

                option.text = localized;
                changed = true;
                translated++;
                Plugin.ReportTranslationHit();
            }

            if (!changed)
            {
                continue;
            }

            dropdown.RefreshShownValue();
            if (dropdown.captionText != null)
            {
                FontFallbackService.ApplyFallback(dropdown.captionText, dropdown.captionText.text);
            }
        }

        Plugin.LogTargetedTranslation(reason, translated, seen);
    }

    public static void InstallLocaleOverrides()
    {
        var localeManagerType = AccessTools.TypeByName("LethalCompanyInputUtils.Localization.LocaleManager");
        var localeEntriesField = localeManagerType == null ? null : AccessTools.Field(localeManagerType, "LocaleEntries");
        if (localeEntriesField?.GetValue(null) is not IDictionary<string, string> localeEntries)
        {
            return;
        }

        foreach (var entry in LocaleTokenEntries)
        {
            localeEntries[entry.Key] = entry.Value;
        }
    }

    public static void ApplyPopOverFallback(object? instance)
    {
        if (instance is not Component component)
        {
            return;
        }

        var label = component.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            FontFallbackService.ApplyFallback(label, label.text);
        }
    }

    private static bool TryTranslate(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var trimmed = source.Trim();
        if (ExactEntries.TryGetValue(trimmed, out var exact))
        {
            translated = source.Length == trimmed.Length ? exact : source.Replace(trimmed, exact);
            return true;
        }

        return TryTranslateLegacyControls(trimmed, out translated);
    }

    private static bool TryTranslateLegacyControls(string source, out string translated)
    {
        translated = source;
        const string prefix = "> Show Legacy Controls (";
        const string suffix = " present)";
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var countText = source.Substring(prefix.Length, source.Length - prefix.Length - suffix.Length).Trim();
        if (!int.TryParse(countText, out var count))
        {
            return false;
        }

        translated = $"> 显示旧版按键（{count} 项）";
        return true;
    }
}
