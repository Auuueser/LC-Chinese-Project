using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

// Serialized labels do not pass through text setters when a panel is enabled.
// Handle only fixed menu wording on the component lifecycle, before rendering.
internal static class MenuFirstFrameLocalizationService
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Master volume"] = "主音量", ["CONTROLS"] = "控制设置",
        ["Voice volume"] = "语音播放音量", ["CREW"] = "船员",
        ["Gamma/Brightness"] = "伽马值/画面亮度",
        ["MODE: Push to talk"] = "模式：按键发言", ["MODE: Voice activation"] = "模式：语音触发",
        ["Look sensitivity"] = "视角灵敏度", ["Invert Y-Axis"] = "反转 Y 轴",
        ["Terrain / Grass Detail"] = "地形 / 草地细节", ["Motion blur"] = "动态模糊",
        ["Pixel Resolution"] = "像素分辨率", ["Indirect lighting"] = "间接光照",
        ["Frame rate cap"] = "帧率上限", ["Display mode"] = "显示模式",
        ["Flip Screen Horizontally"] = "水平翻转画面",
        ["Toggle Sprint"] = "切换冲刺", ["Head bobbing"] = "头部晃动",
        ["Arachnophobia Mode"] = "蜘蛛恐惧症模式", ["Resume"] = "继续游戏",
        ["Settings"] = "基础设置", ["Invite friends"] = "邀请好友", ["Quit"] = "退出游戏",
        ["Confirm changes"] = "确认更改", ["Back"] = "返回",
        ["Change keybinds"] = "修改按键绑定", ["Reset all to default"] = "将全部选项还原为默认",
        ["GRAPHICS"] = "图形画面", ["DISPLAY"] = "画面显示", ["ACCESSIBILITY"] = "辅助功能设置"
    };

    internal static bool TryTranslate(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source) || source.Length > 96) return false;
        var key = source.Trim();
        var arrow = key.StartsWith(">", StringComparison.Ordinal);
        if (arrow) key = key.Substring(1).TrimStart();
        var colon = key.EndsWith(":", StringComparison.Ordinal);
        if (!Labels.TryGetValue(colon ? key[..^1].TrimEnd() : key, out var label)) return false;
        translated = (arrow ? "> " : "") + label + (colon ? "：" : "");
        return true;
    }

    internal static void ApplyTmp(TMP_Text text)
    {
        if (text == null || !TryTranslate(text.text, out var translated) || IsProtectedText(text)) return;
        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
    }

    internal static void ApplyText(Text text)
    {
        if (text == null || !TryTranslate(text.text, out var translated) ||
            IsProtectedText(text)) return;
        text.text = translated;
    }

    // A room name is deliberately never passed through the fixed-label dictionary.
    internal static bool TryTranslateLobbyHeader(string source, out string translated)
    {
        translated = source;
        if (string.IsNullOrEmpty(source)) return false;
        var start = source.LastIndexOf('\n') + 1;
        var line = source.Substring(start);
        if (line.StartsWith("Players: ", StringComparison.Ordinal) && IsPlayerCount(line.Substring(9)))
            translated = source.Substring(0, start) + "玩家：" + line.Substring(9);
        else if (start == 0 && line.StartsWith("CREW (", StringComparison.Ordinal) &&
                 line.EndsWith("):", StringComparison.Ordinal) && IsPlayerCount(line.Substring(6, line.Length - 8)))
            translated = "船员（" + line.Substring(6, line.Length - 8) + "）：";
        else if (start == 0 && string.Equals(line, "CREW:", StringComparison.OrdinalIgnoreCase))
            translated = "船员：";
        return !string.Equals(source, translated, StringComparison.Ordinal);
    }

    private static bool IsPlayerCount(string value)
    {
        var slash = value.IndexOf('/');
        if (slash <= 0 || slash == value.Length - 1) return false;
        for (var i = 0; i < value.Length; i++)
            if (i != slash && (value[i] < '0' || value[i] > '9')) return false;
        return true;
    }

    internal static void ApplyLobbyHeader(QuickMenuManager? menu)
    {
        var header = menu?.playerListPanel?.transform.Find("Image/Header")?.GetComponent<TMP_Text>();
        if (header == null || !TryTranslateLobbyHeader(header.text, out var translated)) return;
        header.text = translated;
        FontFallbackService.ApplyFallback(header, translated);
    }

    internal static bool IsProtectedText(Component text)
    {
        if (text.GetComponentInParent<TMP_InputField>(true) != null ||
            text.GetComponentInParent<InputField>(true) != null) return true;
        return IsPlayerNameText(text);
    }

    internal static bool IsPlayerNameText(Component text)
    {
        // MoreCompany's serialized player-name field can enable before its slot
        // has been assigned to QuickMenuManager.playerListSlots.
        if (PlayerNameSourceService.IsNameComponent(text) || TranslationGuard.IsPlayerNamePath(text)) return true;
        var lobby = text.GetComponentInParent<LobbySlot>(true);
        if (lobby != null && (ReferenceEquals(lobby.LobbyName, text) || ReferenceEquals(lobby.playerCount, text))) return true;
        return false;
    }
}
