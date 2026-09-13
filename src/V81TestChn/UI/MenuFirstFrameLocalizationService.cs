using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace V81TestChn;

// Serialized labels do not pass through text setters when a panel is enabled.
// Handle only fixed menu wording on the component lifecycle, before rendering.
internal static class MenuFirstFrameLocalizationService
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Master volume"] = "主音量", ["CONTROLS"] = "控制设置",
        ["Look sensitivity"] = "视角灵敏度", ["Invert Y-Axis"] = "反转 Y 轴",
        ["Terrain / Grass Detail"] = "地形 / 草地细节", ["Motion blur"] = "动态模糊",
        ["Pixel Resolution"] = "像素分辨率", ["Indirect lighting"] = "间接光照",
        ["Frame rate cap"] = "帧率上限", ["Display mode"] = "显示模式",
        ["Flip Screen Horizontally"] = "水平翻转画面",
        ["Toggle Sprint"] = "切换冲刺", ["Head bobbing"] = "头部晃动",
        ["Arachnophobia Mode"] = "蜘蛛恐惧症模式", ["Resume"] = "继续游戏",
        ["Settings"] = "设置", ["Invite friends"] = "邀请好友", ["Quit"] = "退出",
        ["Confirm changes"] = "确认更改", ["Back"] = "返回",
        ["Change keybinds"] = "修改按键绑定", ["Reset all to default"] = "将全部选项还原为默认",
        ["GRAPHICS"] = "图形画面", ["DISPLAY"] = "画面显示", ["ACCESSIBILITY"] = "辅助功能设置"
    };

    internal static bool TryTranslate(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || source.Length > 96) return false;
        var key = source.Trim();
        var colon = key.EndsWith(":", StringComparison.Ordinal);
        if (!Labels.TryGetValue(colon ? key[..^1].TrimEnd() : key, out var label)) return false;
        translated = label + (colon ? "：" : "");
        return true;
    }

    internal static void ApplyTmp(TMP_Text text)
    {
        if (text == null || !TryTranslate(text.text, out var translated) ||
            text.GetComponentInParent<TMP_InputField>() != null) return;
        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
    }

    internal static void ApplyText(Text text)
    {
        if (text == null || !TryTranslate(text.text, out var translated) ||
            text.GetComponentInParent<InputField>() != null) return;
        text.text = translated;
    }
}
