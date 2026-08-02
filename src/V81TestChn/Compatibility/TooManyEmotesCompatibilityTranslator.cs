using System;
using System.Collections.Generic;

namespace V81TestChn;

internal static class TooManyEmotesCompatibilityTranslator
{
    private const int MaxUiTextLength = 160;

    private static readonly Dictionary<string, string> LoadoutLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Favorites"] = "\u6536\u85cf",
        ["Legendary"] = "\u4f20\u8bf4",
        ["Epic"] = "\u53f2\u8bd7",
        ["Rare"] = "\u7a00\u6709",
        ["Common"] = "\u666e\u901a",
        ["Complementary"] = "\u514d\u8d39",
        ["All"] = "\u5168\u90e8"
    };

    private static readonly Dictionary<string, string> ExactUiEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hide complementary"] = "\u9690\u85cf\u514d\u8d39\u52a8\u4f5c",
        ["Hide common"] = "\u9690\u85cf\u666e\u901a\u52a8\u4f5c",
        ["Hide rare"] = "\u9690\u85cf\u7a00\u6709\u52a8\u4f5c",
        ["Hide epic"] = "\u9690\u85cf\u53f2\u8bd7\u52a8\u4f5c",
        ["Hide legendary"] = "\u9690\u85cf\u4f20\u8bf4\u52a8\u4f5c"
    };

    private static readonly Dictionary<string, string> ExactTerminalLines = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Type \"Emotes\" for a list of commands."] = "\u8f93\u5165 EMOTES \u67e5\u770b\u52a8\u4f5c\u5546\u5e97\u3002",
        ["For a list of Emote commands."] = "\u67e5\u770b\u52a8\u4f5c\u5546\u5e97\u4e0e\u547d\u4ee4\u3002",
        ["Store"] = "\u52a8\u4f5c\u5546\u5e97",
        ["Every emote is already unlocked!"] = "\u6240\u6709\u52a8\u4f5c\u5747\u5df2\u89e3\u9501\uff01",
        ["Canceled order."] = "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002",
        ["Rotated emotes."] = "\u5df2\u8f6e\u6362\u52a8\u4f5c\u5546\u5e97\u3002",
        ["Reset ship emotes."] = "\u5df2\u91cd\u7f6e\u672c\u5b58\u6863\u7684\u52a8\u4f5c\u3002",
        ["You have requested to order a new emote."] = "\u4f60\u7533\u8bf7\u8d2d\u4e70\u4e86\u4e00\u4e2a\u65b0\u52a8\u4f5c\u3002",
        ["Please CONFIRM or DENY."] = "\u8bf7\u8f93\u5165 CONFIRM \u786e\u8ba4\uff0c\u6216\u8f93\u5165 DENY \u53d6\u6d88\u3002",
        ["You have successfully purchased a new emote!"] = "\u65b0\u52a8\u4f5c\u8d2d\u4e70\u6210\u529f\uff01",
        ["Your new emote has been added to the emote menu!"] = "\u65b0\u52a8\u4f5c\u5df2\u6dfb\u52a0\u5230\u52a8\u4f5c\u83dc\u5355\uff01",
        ["You have already purchased this emote!"] = "\u4f60\u5df2\u7ecf\u8d2d\u4e70\u8fc7\u8fd9\u4e2a\u52a8\u4f5c\uff01",
        ["You could not afford this emote!"] = "\u4f60\u6ca1\u6709\u8db3\u591f\u7684\u70b9\u6570\u8d2d\u4e70\u8fd9\u4e2a\u52a8\u4f5c\uff01",
        ["Emote does not exist, or is not available in the current rotation."] = "\u8be5\u52a8\u4f5c\u4e0d\u5b58\u5728\uff0c\u6216\u672a\u52a0\u5165\u5f53\u524d\u8f6e\u6362\u3002",
        ["You cannot use the emote commands menu until you are synced with the host."] = "\u4e0e\u4e3b\u673a\u540c\u6b65\u524d\uff0c\u65e0\u6cd5\u4f7f\u7528\u52a8\u4f5c\u547d\u4ee4\u83dc\u5355\u3002",
        ["You may also be seeing this because the host does not have this mod."] = "\u4e5f\u53ef\u80fd\u662f\u56e0\u4e3a\u4e3b\u673a\u672a\u5b89\u88c5\u6b64\u6a21\u7ec4\u3002",
        ["If this is the case, you will already have access to every emote in your emote wheel. Enjoy!"] = "\u5982\u679c\u662f\u8fd9\u79cd\u60c5\u51b5\uff0c\u52a8\u4f5c\u8f6e\u76d8\u4e2d\u7684\u6240\u6709\u52a8\u4f5c\u90fd\u5df2\u53ef\u7528\u3002\u5c3d\u60c5\u4f7f\u7528\u5427\uff01"
    };

    private static readonly (string Prefix, string Replacement)[] TerminalValuePrefixes =
    {
        ("Remaining emote credit balance: $", "\u5269\u4f59\u52a8\u4f5c\u70b9\u6570\uff1a$"),
        ("Remaining group credit balance: $", "\u5269\u4f59\u56e2\u961f\u4f59\u989d\uff1a$"),
        ("New emote credit balance: $", "\u65b0\u52a8\u4f5c\u70b9\u6570\uff1a$"),
        ("New group credit balance: $", "\u65b0\u56e2\u961f\u4f59\u989d\uff1a$"),
        ("New emote credit balance: ", "\u65b0\u52a8\u4f5c\u70b9\u6570\uff1a"),
        ("Emote credit balance is $", "\u52a8\u4f5c\u70b9\u6570\uff1a$"),
        ("Group credit balance is $", "\u56e2\u961f\u4f59\u989d\uff1a$"),
        ("Emote credit balance: $", "\u52a8\u4f5c\u70b9\u6570\uff1a$"),
        ("Group credit balance: $", "\u56e2\u961f\u4f59\u989d\uff1a$"),
        ("Cost of emote is $", "\u52a8\u4f5c\u4ef7\u683c\uff1a$")
    };

    public static bool MightTranslateUiTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxUiTextLength)
        {
            return false;
        }

        var text = source.TrimStart();
        if (text.StartsWith("Hide ", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("Page [", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.IndexOf('[') < 0 || text.IndexOf(']') < 0)
        {
            return false;
        }

        return text.StartsWith("Favorites ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Legendary ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Epic ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Rare ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Common ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Complementary ", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("All ", StringComparison.OrdinalIgnoreCase) ||
               (text.StartsWith("<color", StringComparison.OrdinalIgnoreCase) &&
                text.IndexOf("</color>", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static bool TryTranslateUiText(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (!MightTranslateUiTextCheap(source))
        {
            return false;
        }

        var leadingLength = source!.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var coreLength = source.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var core = source.Substring(leadingLength, coreLength);
        if (!TryTranslateUiCore(core, out var localized))
        {
            return false;
        }

        translated = (leadingLength > 0 ? source[..leadingLength] : string.Empty) +
                     localized +
                     (trailingLength > 0 ? source[^trailingLength..] : string.Empty);
        return true;
    }

    public static string TranslateTerminalOutput(string source)
    {
        if (string.IsNullOrEmpty(source) || !MightContainTooManyEmotesTerminalText(source))
        {
            return source;
        }

        var changed = false;
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (!TryTranslateTerminalLine(content, out var localized))
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? localized + "\r" : localized;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static bool TryTranslateUiCore(string text, out string translated)
    {
        translated = text;
        if (ExactUiEntries.TryGetValue(text, out translated) ||
            TryTranslatePageLabel(text, out translated) ||
            TryTranslateLoadoutCount(text, out translated))
        {
            return true;
        }

        return false;
    }

    private static bool TryTranslatePageLabel(string text, out string translated)
    {
        translated = text;
        const string Prefix = "Page ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = text[Prefix.Length..];
        if (!LooksLikePageCount(payload))
        {
            return false;
        }

        translated = "\u9875\u7801 " + payload;
        return true;
    }

    private static bool TryTranslateLoadoutCount(string text, out string translated)
    {
        translated = text;
        if (text.Length < 4 || text[^1] != ']')
        {
            return false;
        }

        var countStart = text.LastIndexOf('[');
        if (countStart <= 0 || !AllDigits(text.AsSpan(countStart + 1, text.Length - countStart - 2)))
        {
            return false;
        }

        var labelMarkup = text[..countStart].TrimEnd();
        var label = labelMarkup;
        var richPrefix = string.Empty;
        var richSuffix = string.Empty;
        if (TryUnwrapColorTag(labelMarkup, out var prefix, out var inner, out var suffix))
        {
            richPrefix = prefix;
            richSuffix = suffix;
            label = inner.Trim();
        }

        if (!LoadoutLabels.TryGetValue(label, out var localizedLabel))
        {
            return false;
        }

        translated = richPrefix + localizedLabel + richSuffix + " " + text[countStart..];
        return true;
    }

    private static bool TryUnwrapColorTag(string text, out string prefix, out string inner, out string suffix)
    {
        prefix = string.Empty;
        inner = string.Empty;
        suffix = string.Empty;
        if (!text.StartsWith("<color", StringComparison.OrdinalIgnoreCase) ||
            !text.EndsWith("</color>", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var openingEnd = text.IndexOf('>');
        if (openingEnd <= 1 || openingEnd >= text.Length - "</color>".Length)
        {
            return false;
        }

        prefix = text[..(openingEnd + 1)];
        suffix = text[^"</color>".Length..];
        inner = text.Substring(openingEnd + 1, text.Length - openingEnd - 1 - suffix.Length);
        return true;
    }

    private static bool LooksLikePageCount(string value)
    {
        if (value.Length < 5 || value[0] != '[' || value[^1] != ']')
        {
            return false;
        }

        var slash = value.IndexOf('/');
        if (slash <= 1 || slash >= value.Length - 2)
        {
            return false;
        }

        return AllDigits(value.AsSpan(1, slash - 1).Trim()) &&
               AllDigits(value.AsSpan(slash + 1, value.Length - slash - 2).Trim());
    }

    private static bool MightContainTooManyEmotesTerminalText(string source)
    {
        return source.IndexOf("[TooManyEmotes]", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("emote credit", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Emote commands", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("new emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("this emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Every emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Cost of emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("emote menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Rotated emotes", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Reset ship emotes", StringComparison.OrdinalIgnoreCase) >= 0 ||
               source.IndexOf("Canceled order.", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryTranslateTerminalLine(string source, out string translated)
    {
        translated = source;
        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var coreLength = source.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var core = source.Substring(leadingLength, coreLength);
        if (!TryTranslateTerminalCore(core, out var localized))
        {
            return false;
        }

        translated = (leadingLength > 0 ? source[..leadingLength] : string.Empty) +
                     localized +
                     (trailingLength > 0 ? source[^trailingLength..] : string.Empty);
        return true;
    }

    private static bool TryTranslateTerminalCore(string text, out string translated)
    {
        translated = text;
        if (ExactTerminalLines.TryGetValue(text, out translated))
        {
            return true;
        }

        foreach (var (prefix, replacement) in TerminalValuePrefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || text.Length <= prefix.Length)
            {
                continue;
            }

            var value = text[prefix.Length..];
            translated = replacement + NormalizeTerminalValuePunctuation(value);
            return true;
        }

        var purchasedIndex = text.IndexOf("[Purchased]", StringComparison.OrdinalIgnoreCase);
        if (purchasedIndex >= 0 && text.StartsWith("* ", StringComparison.Ordinal))
        {
            translated = text[..purchasedIndex] + "[\u5df2\u8d2d\u4e70]" + text[(purchasedIndex + "[Purchased]".Length)..];
            return true;
        }

        return false;
    }

    private static string NormalizeTerminalValuePunctuation(string value)
    {
        return value.EndsWith(".", StringComparison.Ordinal)
            ? value[..^1] + "\u3002"
            : value;
    }

    private static bool AllDigits(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }
}
