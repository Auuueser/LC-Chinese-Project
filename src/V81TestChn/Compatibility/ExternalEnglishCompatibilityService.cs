using System;
using System.Collections.Generic;
using System.Text;

namespace V81TestChn;

internal static class ExternalEnglishCompatibilityService
{
    private const int MaxSourceLength = 512;
    private const int RuntimeCacheLimit = 4096;
    private const string DiscountAlertNoDiscountLocalizedText = "\u6682\u65e0\u6298\u6263\n\u660e\u5929\u518d\u6765\u67e5\u770b";

    private static readonly Dictionary<string, string> ExactEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Emote Menu"] = "\u52a8\u4f5c\u83dc\u5355",
        ["Random Emote"] = "\u968f\u673a\u52a8\u4f5c",
        ["Zoom"] = "\u7f29\u653e",
        ["Freeze"] = "\u51bb\u7ed3",
        ["Swap Page"] = "\u5207\u6362\u9875\u9762",
        ["Favorite Emote"] = "\u6536\u85cf\u52a8\u4f5c",
        ["Set Quick Emote"] = "\u8bbe\u7f6e\u5feb\u6377\u52a8\u4f5c",
        ["Tell autopilot ship to leave early"] = "\u547d\u4ee4\u81ea\u52a8\u9a7e\u9a76\u98de\u8239\u63d0\u524d\u79bb\u5f00",
        ["Spectate Previous Player"] = "\u5207\u6362\u4e0a\u4e00\u540d\u73a9\u5bb6",
        ["Open Admin UI"] = "\u6253\u5f00\u7ba1\u7406\u754c\u9762",
        ["Copy Lobby ID"] = "\u590d\u5236\u623f\u95f4 ID",
        ["Enter tag or id..."] = "\u8f93\u5165\u6807\u7b7e\u6216 ID...",
        ["Enter tag or ip..."] = "\u8f93\u5165\u6807\u7b7e\u6216 IP...",
        ["Search Mods"] = "\u641c\u7d22\u6a21\u7ec4",
        ["Search Configs"] = "\u641c\u7d22\u914d\u7f6e",
        ["Close"] = "\u5173\u95ed",
        ["Apply"] = "\u5e94\u7528",
        ["Delete"] = "\u5220\u9664",
        ["Go back"] = "\u8fd4\u56de",
        ["OK"] = "\u786e\u8ba4",
        ["Server Password"] = "\u623f\u95f4\u5bc6\u7801",
        ["Validate Steam Sessions"] = "\u9a8c\u8bc1 Steam \u4f1a\u8bdd",
        ["Max Players"] = "\u6700\u5927\u4eba\u6570",
        ["Server Access"] = "\u623f\u95f4\u6743\u9650",
        ["Invite-only"] = "\u4ec5\u9650\u9080\u8bf7",
        ["Friends-only"] = "\u4ec5\u9650\u597d\u53cb",
        ["Server Tag"] = "\u623f\u95f4\u6807\u7b7e",
        ["New File"] = "\u65b0\u5b58\u6863",
        ["Players"] = "\u73a9\u5bb6",
        ["Today's discounts"] = "\u4eca\u65e5\u6298\u6263",
        ["Target on ship"] = "\u98de\u8239\u4e0a\u7684\u76ee\u6807",
        ["Signal lost"] = "\u4fe1\u53f7\u4e22\u5931",
        ["Antenna stored"] = "\u5929\u7ebf\u5df2\u5b58\u653e",
        ["PERFORMANCE REPORT"] = "\u7ee9\u6548\u62a5\u544a",
        ["NO SURVIVORS"] = "\u65e0\u4eba\u751f\u8fd8",
        ["NOTES"] = "\u5907\u6ce8",
        ["DECEASED"] = "\u6b7b\u4ea1",
        ["Collected"] = "\u5df2\u6536\u96c6",
        ["Grade"] = "\u8bc4\u7ea7",
        ["Lost 100% scrap"] = "\u635f\u5931 100% \u5e9f\u6599",
        ["* The most paranoid employee."] = "* \u6700\u591a\u7591\u7684\u5458\u5de5",
        ["The most paranoid employee."] = "\u6700\u591a\u7591\u7684\u5458\u5de5",
        ["* Sustained the most injuries."] = "* \u53d7\u4f24\u6700\u591a",
        ["Sustained the most injuries."] = "\u53d7\u4f24\u6700\u591a",
        ["* Dislikes smoke."] = "* \u8ba8\u538c\u70df\u96fe",
        ["Dislikes smoke."] = "\u8ba8\u538c\u70df\u96fe",
        ["* The least likely to die next time."] = "* \u4e0b\u6b21\u6700\u4e0d\u53ef\u80fd\u6b7b\u4ea1",
        ["The least likely to die next time."] = "\u4e0b\u6b21\u6700\u4e0d\u53ef\u80fd\u6b7b\u4ea1",
        ["* I think this one's a serial killer."] = "* \u6211\u89c9\u5f97\u8fd9\u4eba\u50cf\u4e2a\u8fde\u73af\u6740\u624b",
        ["I think this one's a serial killer."] = "\u6211\u89c9\u5f97\u8fd9\u4eba\u50cf\u4e2a\u8fde\u73af\u6740\u624b",
        ["* Go! Freaky on a Friday night."] = "* \u5468\u4e94\u591c\u665a\u5c3d\u60c5\u75af\u72c2",
        ["Go! Freaky on a Friday night."] = "\u5468\u4e94\u591c\u665a\u5c3d\u60c5\u75af\u72c2"
    };

    private static readonly Dictionary<string, string> KeyTokenEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scroll Mouse"] = "\u6eda\u52a8\u9f20\u6807",
        ["keybind #"] = "\u6309\u952e\u7ed1\u5b9a #"
    };

    private static readonly Dictionary<string, bool> CanHandleCache = new(RuntimeCacheLimit, StringComparer.Ordinal);
    private static readonly Dictionary<string, string?> TranslationCache = new(RuntimeCacheLimit, StringComparer.Ordinal);

    public static void ClearRuntimeCaches()
    {
        CanHandleCache.Clear();
        TranslationCache.Clear();
    }

    public static bool CanHandleCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (CanHandleCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var result = CanHandleCheapCore(source);
        CacheCanHandleResult(source, result);
        return result;
    }

    public static bool MightTranslateStatusLikeTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (ContainsLineBreak(source))
        {
            var start = 0;
            while (start <= source.Length)
            {
                var newline = source.IndexOf('\n', start);
                var end = newline < 0 ? source.Length : newline;
                var lineSpan = source.AsSpan(start, end - start);
                if (lineSpan.Length > 0 && lineSpan[^1] == '\r')
                {
                    lineSpan = lineSpan[..^1];
                }

                if (LineMightNeedExternalCompatibilityCheck(lineSpan) &&
                    MightTranslateStatusLikeSingleLineCheap(lineSpan.ToString()))
                {
                    return true;
                }

                if (newline < 0)
                {
                    break;
                }

                start = newline + 1;
            }

            return false;
        }

        return MightTranslateStatusLikeSingleLineCheap(source);
    }

    public static bool MightTranslateStatusLikeLabelCheap(string? label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > 64)
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(label)).Trim());
        if (content.EndsWith(":", StringComparison.Ordinal))
        {
            content = content[..^1].TrimEnd();
        }

        return ExactEntries.ContainsKey(content);
    }

    private static bool CanHandleCheapCore(string source)
    {
        if (MightContainDeleteFilePrompt(source) &&
            LooksLikeDeleteFilePrompt(source))
        {
            return true;
        }

        if (MightContainDiscountAlertNoDiscountText(source) &&
            LooksLikeDiscountAlertNoDiscountText(source))
        {
            return true;
        }

        if (ContainsLineBreak(source))
        {
            return CanHandleAnyLineCheap(source);
        }

        if (ContainsCjk(source))
        {
            return false;
        }

        return CanHandleSingleLineCheap(source);
    }

    private static bool CanHandleAnyLineCheap(string source)
    {
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var lineSpan = source.AsSpan(start, end - start);
            if (lineSpan.Length > 0 && lineSpan[^1] == '\r')
            {
                lineSpan = lineSpan[..^1];
            }

            if (LineMightNeedExternalCompatibilityCheck(lineSpan) &&
                CanHandleSingleLineCheap(lineSpan.ToString()))
            {
                return true;
            }

            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
        }

        return false;
    }

    private static bool LineMightNeedExternalCompatibilityCheck(ReadOnlySpan<char> line)
    {
        line = line.Trim();
        if (line.Length == 0 || line.Length > MaxSourceLength)
        {
            return false;
        }

        var hasAsciiLetter = false;
        foreach (var ch in line)
        {
            if (IsCjk(ch))
            {
                return false;
            }

            if (IsAsciiLetter(ch))
            {
                hasAsciiLetter = true;
            }
        }

        return hasAsciiLetter;
    }

    private static bool MightContainDeleteFilePrompt(string source) =>
        source.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0 &&
        source.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MightContainDiscountAlertNoDiscountText(string source) =>
        source.IndexOf("None", StringComparison.OrdinalIgnoreCase) >= 0 &&
        source.IndexOf("tomorrow", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MightContainDiscountLine(string source) =>
        source.IndexOf('$') >= 0 &&
        (source.IndexOf(" off!", StringComparison.OrdinalIgnoreCase) >= 0 ||
         source.IndexOf(" up!", StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool MightContainCompositeExternalUiShape(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsWhiteSpace(ch) || ch is ':' or '[' or ']' or '#' or '(' or ')' or '$' or '%' or '*' or '!' or '-')
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanHandleSingleLineCheap(string source)
    {
        var text = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(source)).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(text);
        if (ExactEntries.ContainsKey(content) ||
            LooksLikeBracketedKnownExternalUiToken(content))
        {
            return true;
        }

        if (!MightContainCompositeExternalUiShape(content))
        {
            return false;
        }

        return LooksLikeKnownExternalUiLabel(content) ||
               LooksLikeSaveFileLabel(content) ||
               LooksLikeAdvancedFeaturesPlayerLabel(content) ||
               LooksLikeDeleteFilePrompt(content) ||
               text.IndexOf("Emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Admin UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Lobby ID", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Server Access", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Steam Sessions", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("FRIENDS ONLY", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("INVITE ONLY", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("discount", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf(" off!", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf(" up!", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool MightTranslateStatusLikeSingleLineCheap(string source)
    {
        var text = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(source)).Trim();
        if (text.Length == 0 || ContainsCjk(text))
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(text).Trim();
        return LooksLikeKnownExternalUiLabel(content) ||
               LooksLikeBracketedKnownExternalUiToken(content);
    }

    public static bool TryTranslateFast(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (TryGetCachedTranslation(source, out translated, out var hasTranslation))
        {
            return hasTranslation;
        }

        if (!CanHandleCheap(source))
        {
            CacheTranslationResult(source, null);
            return false;
        }

        if (TryTranslateDiscountAlertNoDiscountText(source!, out translated) ||
            TryTranslateDeleteFilePrompt(source!, out translated))
        {
            CacheTranslationResult(source, translated);
            return true;
        }

        if (ContainsLineBreak(source!))
        {
            var changedLines = TryTranslateLines(source!, out translated);
            CacheTranslationResult(source, changedLines ? translated : null);
            return changedLines;
        }

        var changed = TryTranslateSingleLinePreservingWhitespace(source!, out translated);
        CacheTranslationResult(source, changed ? translated : null);
        return changed;
    }

    public static bool TryTranslateDisplayTipText(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (source.Length <= MaxSourceLength)
        {
            return TryTranslateFast(source, out translated);
        }

        return ContainsLineBreak(source) &&
               TryTranslateDisplayTipLines(source, out translated);
    }

    public static bool MightContainDisplayTipCompatibilityText(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (source.Length <= MaxSourceLength)
        {
            return CanHandleCheap(source) ||
                   MightContainDiscountLine(source);
        }

        return MightContainDiscountLine(source);
    }

    private static void CacheCanHandleResult(string source, bool result)
    {
        if (!result && LooksLikeVolatileNegativeCacheSource(source))
        {
            return;
        }

        if (CanHandleCache.Count >= RuntimePerformanceSettings.ExternalCompatibilityCacheLimit)
        {
            return;
        }

        CanHandleCache[source] = result;
    }

    private static bool TryGetCachedTranslation(string source, out string translated, out bool hasTranslation)
    {
        if (!TranslationCache.TryGetValue(source, out var cached))
        {
            translated = source;
            hasTranslation = false;
            return false;
        }

        hasTranslation = cached != null;
        translated = cached ?? source;
        return true;
    }

    private static void CacheTranslationResult(string source, string? translated)
    {
        if (translated == null && LooksLikeVolatileNegativeCacheSource(source))
        {
            return;
        }

        if (TranslationCache.Count >= RuntimePerformanceSettings.ExternalCompatibilityCacheLimit)
        {
            return;
        }

        TranslationCache[source] = translated;
    }

    private static bool LooksLikeVolatileNegativeCacheSource(string source)
    {
        if (source.Length > 128)
        {
            return true;
        }

        foreach (var ch in source)
        {
            if (char.IsDigit(ch) ||
                ch is '\r' or '\n' or ':' or '[' or ']' or '(' or ')' or '<' or '>' or '$' or '%' or '#' or '/' or '\\')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateLines(string source, out string translated)
    {
        var changed = false;
        var builder = new StringBuilder(source.Length + 16);
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var line = source.Substring(start, end - start);
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (TryTranslateSingleLinePreservingWhitespace(content, out var rewrittenLine))
            {
                builder.Append(rewrittenLine);
                if (hasCarriageReturn)
                {
                    builder.Append('\r');
                }

                changed = true;
            }
            else
            {
                builder.Append(line);
            }

            if (newline < 0)
            {
                break;
            }

            builder.Append('\n');
            start = newline + 1;
        }

        translated = changed ? builder.ToString() : source;
        return changed;
    }

    private static bool TryTranslateDisplayTipLines(string source, out string translated)
    {
        var changed = false;
        var builder = new StringBuilder(source.Length + 16);
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var line = source.Substring(start, end - start);
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (content.Length <= MaxSourceLength &&
                LineMightNeedExternalCompatibilityCheck(content.AsSpan()) &&
                TryTranslateSingleLinePreservingWhitespace(content, out var rewrittenLine))
            {
                builder.Append(rewrittenLine);
                if (hasCarriageReturn)
                {
                    builder.Append('\r');
                }

                changed = true;
            }
            else
            {
                builder.Append(line);
            }

            if (newline < 0)
            {
                break;
            }

            builder.Append('\n');
            start = newline + 1;
        }

        translated = changed ? builder.ToString() : source;
        return changed;
    }

    private static bool TryTranslateSingleLinePreservingWhitespace(string source, out string translated)
    {
        translated = source;
        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var coreLength = source.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var leading = leadingLength > 0 ? source[..leadingLength] : string.Empty;
        var trailing = trailingLength > 0 ? source[^trailingLength..] : string.Empty;
        var core = source.Substring(leadingLength, coreLength);
        if (!TryTranslateSingleLineCore(core, out var rewrittenCore))
        {
            return false;
        }

        translated = leading + rewrittenCore + trailing;
        return true;
    }

    private static bool TryTranslateSingleLineCore(string source, out string translated)
    {
        translated = source;
        var text = source.Trim();
        var richPrefix = string.Empty;
        var richSuffix = string.Empty;
        while (TryExtractOuterSimpleRichTextEnvelope(text, out var envelopePrefix, out var inner, out var envelopeSuffix))
        {
            richPrefix += envelopePrefix;
            richSuffix = envelopeSuffix + richSuffix;
            text = inner.Trim();
        }

        richPrefix += ExtractLeadingSimpleRichTextPrefix(ref text);
        var menuPrefix = string.Empty;
        if (text.StartsWith(">", StringComparison.Ordinal))
        {
            menuPrefix = "> ";
            text = StripMenuSelectionPrefix(text);
        }

        if (LooksLikeNonUiName(text))
        {
            return false;
        }

        if (TryTranslateBracketedCommand(text, out translated) ||
            TryTranslateControlTip(text, out translated) ||
            TryTranslateLabelValue(text, out translated) ||
            TryTranslateDiscountLine(text, out translated) ||
            TryTranslateSaveFileLabel(text, out translated) ||
            TryTranslateAdvancedFeaturesPlayerLabel(text, out translated) ||
            TryTranslateDeleteFilePrompt(text, out translated) ||
            TryTranslateExactUiText(text, out translated))
        {
            translated = menuPrefix + richPrefix + translated + richSuffix;
            return true;
        }

        return false;
    }

    private static bool TryTranslateSaveFileLabel(string text, out string translated)
    {
        translated = text;
        var content = StripMenuSelectionPrefix(text).Trim();
        if (!LooksLikeSaveFileLabel(content))
        {
            return false;
        }

        translated = content["File ".Length..].TrimStart() + " \u53f7\u5b58\u6863";
        return true;
    }

    private static bool TryTranslateDeleteFilePrompt(string text, out string translated)
    {
        translated = text;
        if (TryExtractDeleteFileAlias(text, out var alias))
        {
            translated = "\u8981\u5220\u9664 " + alias + " \u5417";
            return true;
        }

        if (!TryExtractDeleteFileNumber(text, out var fileNumber))
        {
            return false;
        }

        translated = "\u8981\u5220\u9664 " + fileNumber + " \u53f7\u5b58\u6863\u5417";
        return true;
    }

    private static bool TryTranslateDiscountAlertNoDiscountText(string text, out string translated)
    {
        translated = text;
        var leadingLength = text.Length - text.TrimStart().Length;
        var trailingLength = text.Length - text.TrimEnd().Length;
        var coreLength = text.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var leading = leadingLength > 0 ? text[..leadingLength] : string.Empty;
        var trailing = trailingLength > 0 ? text[^trailingLength..] : string.Empty;
        var core = text.Substring(leadingLength, coreLength);
        var richPrefix = string.Empty;
        var richSuffix = string.Empty;
        while (TryExtractOuterSimpleRichTextEnvelope(core.Trim(), out var envelopePrefix, out var inner, out var envelopeSuffix))
        {
            richPrefix += envelopePrefix;
            richSuffix = envelopeSuffix + richSuffix;
            core = inner;
        }

        if (!LooksLikeDiscountAlertNoDiscountCore(core))
        {
            return false;
        }

        translated = leading + richPrefix + DiscountAlertNoDiscountLocalizedText + richSuffix + trailing;
        return true;
    }

    private static bool LooksLikeDiscountAlertNoDiscountText(string text)
    {
        var core = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text));
        return LooksLikeDiscountAlertNoDiscountCore(core);
    }

    private static bool LooksLikeDiscountAlertNoDiscountCore(string text)
    {
        var normalized = NormalizeAsciiWhitespace(
            text.Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n'));
        return normalized.Equals("None :( Check back tomorrow!", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryTranslateBetterSavesDeleteFilePrompt(string? source, int fileToDelete, out string translated)
    {
        translated = source ?? string.Empty;
        if (fileToDelete <= 0 || string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (TryExtractDeleteFileAlias(source, out var alias))
        {
            translated = "\u8981\u5220\u9664 " + alias + " \u5417";
            return true;
        }

        if (!TryExtractDeleteFileNumber(source, out _) &&
            !LooksLikeLocalizedDeleteFileNumberPrompt(source))
        {
            return false;
        }

        translated = "\u8981\u5220\u9664 " + fileToDelete.ToString(System.Globalization.CultureInfo.InvariantCulture) + " \u53f7\u5b58\u6863\u5417";
        return true;
    }

    private static bool TryTranslateExactUiText(string text, out string translated)
    {
        translated = text;
        if (ExactEntries.TryGetValue(text, out translated))
        {
            return true;
        }

        if (text.EndsWith(":", StringComparison.Ordinal))
        {
            var label = text[..^1].TrimEnd();
            if (ExactEntries.TryGetValue(label, out var localizedLabel))
            {
                translated = localizedLabel + "\uff1a";
                return true;
            }
        }

        if (string.Equals(text, "FRIENDS ONLY means only friends or invited people can join.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4ec5\u9650\u597d\u53cb\uff1a\u597d\u53cb\u6216\u53d7\u9080\u73a9\u5bb6\u53ef\u4ee5\u52a0\u5165";
            return true;
        }

        if (string.Equals(text, "INVITE ONLY means you must send invites through Steam for players to join.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4ec5\u9650\u9080\u8bf7\uff1a\u5fc5\u987b\u901a\u8fc7 Steam \u9080\u8bf7\u73a9\u5bb6\u52a0\u5165";
            return true;
        }

        return false;
    }

    private static bool TryTranslateBracketedCommand(string text, out string translated)
    {
        translated = text;
        if (text.Length < 3 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        var inner = text.Substring(1, text.Length - 2).Trim();
        if (!ExactEntries.TryGetValue(inner, out var localized))
        {
            return false;
        }

        translated = "[ " + localized + " ]";
        return true;
    }

    private static bool TryTranslateControlTip(string text, out string translated)
    {
        translated = text;
        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var action = text.Substring(0, colon).Trim();
        if (!ExactEntries.TryGetValue(action, out var localizedAction))
        {
            return false;
        }

        var payload = text.Substring(colon + 1).Trim();
        if (!LooksLikeControlPayload(payload))
        {
            return false;
        }

        translated = localizedAction + "\uff1a" + NormalizeControlPayload(payload);
        return true;
    }

    private static bool TryTranslateLabelValue(string text, out string translated)
    {
        translated = text;
        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var label = text.Substring(0, colon).Trim();
        if (!ExactEntries.TryGetValue(label, out var localizedLabel))
        {
            return false;
        }

        var payload = text.Substring(colon + 1).Trim();
        if (payload.Length == 0 || LooksLikeNonUiName(payload))
        {
            return false;
        }

        translated = localizedLabel + "\uff1a" + payload;
        return true;
    }

    private static bool TryTranslateDiscountLine(string text, out string translated)
    {
        translated = text;
        var trimmed = text.Trim();
        var prefix = string.Empty;
        if (trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            prefix = "* ";
            trimmed = trimmed[1..].TrimStart();
        }

        var parenStart = trimmed.LastIndexOf('(');
        if (parenStart <= 0 || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var itemAndPrice = trimmed.Substring(0, parenStart).TrimEnd();
        var discount = trimmed.Substring(parenStart + 1, trimmed.Length - parenStart - 2).Trim();
        var isDiscount = discount.EndsWith("off!", StringComparison.OrdinalIgnoreCase);
        var isPriceUp = discount.EndsWith("up!", StringComparison.OrdinalIgnoreCase);
        if ((!isDiscount && !isPriceUp) || !TryTranslateItemAndPrice(itemAndPrice, out var localizedItemAndPrice))
        {
            return false;
        }

        var suffixLength = isDiscount ? "off!".Length : "up!".Length;
        var percent = discount[..^suffixLength].Trim();
        if (percent.Length == 0 || !ContainsDigit(percent))
        {
            return false;
        }

        translated = prefix + localizedItemAndPrice + " \uff08" + percent + (isDiscount ? " \u6298\u6263\uff09" : " \u6da8\u4ef7\uff09");
        return true;
    }

    private static bool TryTranslateItemAndPrice(string value, out string translated)
    {
        translated = value;
        var dollar = value.LastIndexOf('$');
        if (dollar <= 0)
        {
            return false;
        }

        var item = value.Substring(0, dollar).TrimEnd();
        var price = value[dollar..].TrimStart();
        if (!LooksLikePrice(price))
        {
            return false;
        }

        var localizedItem = TranslationService.BuildTerminalLocalizedItemName(item);
        if (localizedItem.Length == 0 ||
            string.Equals(localizedItem, item, StringComparison.Ordinal))
        {
            return false;
        }

        translated = localizedItem + " " + price;
        return true;
    }

    private static string NormalizeControlPayload(string payload)
    {
        var value = payload.Trim();
        if (value.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase))
        {
            return "\u6309\u4f4f " + NormalizeKeyTokens(value["Hold ".Length..].Trim());
        }

        value = NormalizeKeyTokens(value);
        if (value.IndexOf("(Hold)", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = value.Replace("(Hold)", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd() + "\uff08\u957f\u6309\uff09";
        }

        return value;
    }

    private static string NormalizeKeyTokens(string value)
    {
        var builder = value;
        foreach (var entry in KeyTokenEntries)
        {
            builder = builder.Replace("[" + entry.Key + "]", "[" + entry.Value + "]", StringComparison.OrdinalIgnoreCase);
        }

        return builder;
    }

    private static bool LooksLikeControlPayload(string payload)
    {
        if (payload.Length < 3)
        {
            return false;
        }

        if (payload.StartsWith("Hold [", StringComparison.OrdinalIgnoreCase))
        {
            return payload.EndsWith("]", StringComparison.Ordinal);
        }

        return payload.IndexOf('[') >= 0 && payload.IndexOf(']') > payload.IndexOf('[');
    }

    private static bool LooksLikeKnownExternalUiLabel(string text)
    {
        if (text.EndsWith(":", StringComparison.Ordinal))
        {
            var label = text[..^1].TrimEnd();
            return ExactEntries.ContainsKey(label);
        }

        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon > 64)
        {
            return false;
        }

        var labelBeforeColon = text.Substring(0, colon).Trim();
        return ExactEntries.ContainsKey(labelBeforeColon);
    }

    private static string StripMenuSelectionPrefix(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith(">", StringComparison.Ordinal)
            ? trimmed[1..].TrimStart()
            : text;
    }

    private static bool LooksLikeBracketedKnownExternalUiToken(string text)
    {
        if (text.Length < 3 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        var inner = text.Substring(1, text.Length - 2).Trim();
        return ExactEntries.ContainsKey(inner);
    }

    private static bool LooksLikeSaveFileLabel(string text)
    {
        const string Prefix = "File ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = text[Prefix.Length..].TrimStart();
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryTranslateAdvancedFeaturesPlayerLabel(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeAdvancedFeaturesPlayerLabel(text))
        {
            return false;
        }

        translated = "\u73a9\u5bb6 #" + text.Trim()["Player #".Length..];
        return true;
    }

    private static bool LooksLikeAdvancedFeaturesPlayerLabel(string text)
    {
        const string Prefix = "Player #";
        var trimmed = text.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = trimmed[Prefix.Length..];
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeDeleteFilePrompt(string text) =>
        TryExtractDeleteFileNumber(text, out _) || TryExtractDeleteFileAlias(text, out _);

    private static bool TryExtractDeleteFileNumber(string text, out string fileNumber)
    {
        fileNumber = string.Empty;
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "Do you want to delete File ";
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        var number = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - 1).Trim();
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        fileNumber = number;
        return true;
    }

    private static bool TryExtractDeleteFileAlias(string text, out string alias)
    {
        alias = string.Empty;
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "Do you want to delete file (";
        const string Suffix = ")?";
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        alias = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - Suffix.Length).Trim();
        return alias.Length > 0;
    }

    private static bool LooksLikeLocalizedDeleteFileNumberPrompt(string text)
    {
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "\u8981\u5220\u9664 ";
        const string Suffix = " \u53f7\u5b58\u6863\u5417";
        if (!normalized.StartsWith(Prefix, StringComparison.Ordinal) ||
            !normalized.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var number = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - Suffix.Length);
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeNonUiName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.IndexOf(' ') >= 0)
        {
            return false;
        }

        if (trimmed.IndexOf('-') >= 0 && ContainsDigit(trimmed))
        {
            return true;
        }

        return LooksLikeCamelCaseIdentifier(trimmed);
    }

    private static bool LooksLikeCamelCaseIdentifier(string text)
    {
        if (text.Length < 6 || !IsAsciiLetter(text[0]))
        {
            return false;
        }

        var hasLower = false;
        var uppercaseAfterLower = false;
        foreach (var ch in text)
        {
            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            if (char.IsLower(ch))
            {
                hasLower = true;
            }
            else if (hasLower && char.IsUpper(ch))
            {
                uppercaseAfterLower = true;
            }
        }

        return uppercaseAfterLower;
    }

    private static int FindTopLevelColon(string value)
    {
        var inBracket = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '[')
            {
                inBracket = true;
                continue;
            }

            if (ch == ']' && inBracket)
            {
                inBracket = false;
                continue;
            }

            if (!inBracket && ch == ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static string StripOuterSimpleRichTextEnvelope(string value)
    {
        var text = value.Trim();
        for (var depth = 0; depth < 3; depth++)
        {
            if (text.Length < 7 || text[0] != '<')
            {
                break;
            }

            var tagClose = text.IndexOf('>');
            if (tagClose <= 1 || tagClose > 24)
            {
                break;
            }

            var tagNameLength = 0;
            for (var i = 1; i < tagClose; i++)
            {
                var ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '=')
                {
                    break;
                }

                if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
                {
                    tagNameLength = 0;
                    break;
                }

                tagNameLength++;
            }

            if (tagNameLength == 0)
            {
                break;
            }

            var tagName = text.Substring(1, tagNameLength);
            var closingTag = "</" + tagName + ">";
            if (!text.EndsWith(closingTag, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            text = text.Substring(tagClose + 1, text.Length - tagClose - 1 - closingTag.Length).Trim();
        }

        return text;
    }

    private static bool TryExtractOuterSimpleRichTextEnvelope(
        string value,
        out string prefix,
        out string inner,
        out string suffix)
    {
        prefix = string.Empty;
        inner = string.Empty;
        suffix = string.Empty;

        var text = value.Trim();
        if (!TryReadSimpleOpeningRichTextTag(text, out var tagEnd) ||
            !TryReadSimpleOpeningTagName(text, tagEnd, out var tagName))
        {
            return false;
        }

        var closingTag = "</" + tagName + ">";
        if (!text.EndsWith(closingTag, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        prefix = text.Substring(0, tagEnd + 1);
        suffix = text[^closingTag.Length..];
        inner = text.Substring(tagEnd + 1, text.Length - tagEnd - 1 - closingTag.Length);
        return true;
    }

    private static string StripLeadingSimpleRichTextTags(string value)
    {
        var text = value.Trim();
        while (TryReadSimpleOpeningRichTextTag(text, out var tagEnd))
        {
            text = text[(tagEnd + 1)..].TrimStart();
        }

        return text;
    }

    private static string ExtractLeadingSimpleRichTextPrefix(ref string value)
    {
        var text = value.Trim();
        StringBuilder? prefix = null;
        while (TryReadSimpleOpeningRichTextTag(text, out var tagEnd))
        {
            prefix ??= new StringBuilder();
            prefix.Append(text, 0, tagEnd + 1);
            text = text[(tagEnd + 1)..].TrimStart();
        }

        value = text;
        return prefix?.ToString() ?? string.Empty;
    }

    private static bool TryReadSimpleOpeningRichTextTag(string value, out int tagEnd)
    {
        tagEnd = -1;
        if (value.Length < 4 || value[0] != '<' || value[1] == '/')
        {
            return false;
        }

        var close = value.IndexOf('>');
        if (close <= 1 || close > 40)
        {
            return false;
        }

        var tagNameLength = 0;
        for (var i = 1; i < close; i++)
        {
            var ch = value[i];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            tagNameLength++;
        }

        if (tagNameLength == 0)
        {
            return false;
        }

        tagEnd = close;
        return true;
    }

    private static bool TryReadSimpleOpeningTagName(string value, int tagEnd, out string tagName)
    {
        tagName = string.Empty;
        if (tagEnd <= 1 || tagEnd >= value.Length)
        {
            return false;
        }

        var tagNameLength = 0;
        for (var i = 1; i < tagEnd; i++)
        {
            var ch = value[i];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            tagNameLength++;
        }

        if (tagNameLength == 0)
        {
            return false;
        }

        tagName = value.Substring(1, tagNameLength);
        return true;
    }

    private static bool LooksLikePrice(string value)
    {
        if (value.Length < 2 || value[0] != '$')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]) && value[i] != ',' && value[i] != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsDigit(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeAsciiWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool ContainsLineBreak(string value) =>
        value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjk(char ch) => ch >= 0x3400 && ch <= 0x9FFF;

    private static bool IsAsciiLetter(char ch) => (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z');
}
