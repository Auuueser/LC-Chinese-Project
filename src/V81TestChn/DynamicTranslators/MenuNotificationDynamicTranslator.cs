using System;
using System.Collections.Generic;

namespace V81TestChn;

internal static class MenuNotificationDynamicTranslator
{
    private static readonly Dictionary<string, string> ExactEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Game has already started!"] = "游戏已经开始！",
        ["Unknown; please verify your game files."] = "未知错误；请验证游戏文件完整性。",
        ["You tried to bypass the kick!"] = "检测到你试图绕过踢出限制！",
        ["Failed to get SteamID"] = "无法获取 Steam ID",
        ["You disconnected!"] = "你已断开连接！",
        ["Ship has landed!"] = "飞船已经着陆！",
        ["Host has disconnected!"] = "主机已断开连接！",
        ["Ship has already landed!"] = "飞船已经着陆！",
        ["Lobby has been closed!"] = "房间已经关闭！",
        ["This lobby requires steam authentication."] = "此房间要求进行 Steam 身份验证。"
    };

    private static readonly (string English, string Localized)[] KnownPhraseEntries =
    {
        ("Kicked From Lobby:", "已被踢出房间："),
        ("Banned From Lobby:", "已被房间封禁："),
        ("LobbyImprovements:", "LobbyImprovements："),
        ("Password Protection:", "密码保护："),
        ("Invalid Steam Ticket:", "Steam 票据无效："),
        ("Missing Steam Ticket:", "缺少 Steam 票据："),
        ("InvalidTicket", "票据无效"),
        ("DuplicateRequest", "重复验证请求"),
        ("InvalidVersion", "票据版本无效"),
        ("GameMismatch", "游戏不匹配"),
        ("ExpiredTicket", "票据已过期"),
        ("This lobby requires you to have the LobbyImprovements mod.", "此房间要求安装 LobbyImprovements 模组。"),
        ("You have entered an incorrect password.", "输入的房间密码不正确。"),
        ("This lobby is password protected which requires you to have the LobbyImprovements mod.", "此房间受密码保护，需要安装 LobbyImprovements 模组。"),
        ("You cannot rejoin after being kicked.", "被踢出后无法重新加入。"),
        ("You are banned from this lobby:", "你已被此房间封禁：")
    };

    public static bool Translate(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var trimmed = source.Trim();
        if (ExactEntries.TryGetValue(trimmed, out var exact))
        {
            translated = PreserveOuterWhitespace(source, trimmed, exact);
            return true;
        }

        if (TryTranslateGameVersionMismatch(trimmed, out var dynamicTranslation) ||
            TryTranslateSaveCompatibilityWarning(trimmed, out dynamicTranslation) ||
            TryTranslateCorruptedSaveWarning(trimmed, out dynamicTranslation) ||
            TryTranslateLobbyControlQueue(trimmed, out dynamicTranslation) ||
            TryTranslateCrewSizeMismatch(trimmed, out dynamicTranslation))
        {
            translated = PreserveOuterWhitespace(source, trimmed, dynamicTranslation);
            return true;
        }

        var rewritten = source;
        var changed = false;
        foreach (var entry in KnownPhraseEntries)
        {
            rewritten = ReplaceOrdinalIgnoreCase(rewritten, entry.English, entry.Localized, out var phraseChanged);
            changed |= phraseChanged;
        }

        if (!changed)
        {
            return false;
        }

        translated = rewritten;
        return true;
    }

    private static bool TryTranslateGameVersionMismatch(string source, out string translated)
    {
        const string prefix = "Game version mismatch! Their version: ";
        const string separator = ". Your version: ";
        translated = source;
        if (!TrySplitDynamicPair(source, prefix, separator, out var theirVersion, out var yourVersion))
        {
            return false;
        }

        translated = $"游戏版本不匹配！主机版本：{theirVersion}；你的版本：{yourVersion}";
        return true;
    }

    private static bool TryTranslateSaveCompatibilityWarning(string source, out string translated)
    {
        const string prefix = "Some of your save files may not be compatible with version ";
        const string suffix = " and may be corrupted if you play them.";
        translated = source;
        if (!TryExtractBetween(source, prefix, suffix, out var version))
        {
            return false;
        }

        translated = $"部分存档可能与版本 {version} 不兼容，继续使用可能会导致存档损坏。";
        return true;
    }

    private static bool TryTranslateCorruptedSaveWarning(string source, out string translated)
    {
        const string prefix = "Error loading file #";
        const string separator = "! Deleting file since it's likely corrupted. Error: ";
        translated = source;
        if (!TrySplitDynamicPair(source, prefix, separator, out var fileNumber, out var error))
        {
            return false;
        }

        translated = $"读取存档 #{fileNumber} 时出错！该存档可能已损坏，正在删除。错误：{error}";
        return true;
    }

    private static bool TryTranslateLobbyControlQueue(string source, out string translated)
    {
        const string connectingPrefix = "Another player is connecting\n";
        const string queuePrefix = "Join Queue is Full !\nQueued connections: ";
        const string queueSuffix = "\nPlease Wait a bit before retrying";
        translated = source;

        if (source.Equals(connectingPrefix + "Please Wait a bit before retrying", StringComparison.OrdinalIgnoreCase))
        {
            translated = "另一名玩家正在连接\n请稍后重试";
            return true;
        }

        if (!TryExtractBetween(source, queuePrefix, queueSuffix, out var count))
        {
            return false;
        }

        translated = $"加入队列已满！\n排队中的连接：{count}\n请稍后重试";
        return true;
    }

    private static bool TryTranslateCrewSizeMismatch(string source, out string translated)
    {
        const string prefix = "Crew size mismatch! Their size: ";
        const string separator = ". Your size: ";
        translated = source;
        if (!TrySplitDynamicPair(source, prefix, separator, out var theirSize, out var yourSize))
        {
            return false;
        }

        translated = $"玩家容量不一致！主机容量：{theirSize}；你的容量：{yourSize}";
        return true;
    }

    private static bool TrySplitDynamicPair(
        string source,
        string prefix,
        string separator,
        out string first,
        out string second)
    {
        first = string.Empty;
        second = string.Empty;
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = source.IndexOf(separator, prefix.Length, StringComparison.OrdinalIgnoreCase);
        if (separatorIndex < prefix.Length)
        {
            return false;
        }

        first = source.Substring(prefix.Length, separatorIndex - prefix.Length).Trim();
        second = source[(separatorIndex + separator.Length)..].Trim();
        return first.Length > 0 && second.Length > 0;
    }

    private static bool TryExtractBetween(string source, string prefix, string suffix, out string value)
    {
        value = string.Empty;
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            source.Length <= prefix.Length + suffix.Length)
        {
            return false;
        }

        value = source.Substring(prefix.Length, source.Length - prefix.Length - suffix.Length).Trim();
        return value.Length > 0;
    }

    private static string ReplaceOrdinalIgnoreCase(string source, string oldValue, string newValue, out bool changed)
    {
        changed = false;
        var startIndex = 0;
        while (true)
        {
            var matchIndex = source.IndexOf(oldValue, startIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                return source;
            }

            source = source.Substring(0, matchIndex) + newValue + source[(matchIndex + oldValue.Length)..];
            startIndex = matchIndex + newValue.Length;
            changed = true;
        }
    }

    private static string PreserveOuterWhitespace(string source, string trimmed, string translated)
    {
        var start = source.IndexOf(trimmed, StringComparison.Ordinal);
        if (start < 0)
        {
            return translated;
        }

        return source.Substring(0, start) + translated + source[(start + trimmed.Length)..];
    }
}
