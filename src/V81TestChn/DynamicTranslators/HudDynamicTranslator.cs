using System;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class HudDynamicTranslator
    {
        public static bool CanHandleCheap(string? source) =>
            LooksLikeRandomSeedTextCheap(source) ||
            LooksLikeVoteTextCheap(source) ||
            LooksLikeDaysLeftTextCheap(source);

        public static bool Translate(DynamicTextDomain domain, string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return domain switch
            {
                DynamicTextDomain.HudScanner => TranslateScanValue(source, out translated) ||
                                                TranslateFast(source, out translated),
                DynamicTextDomain.HudRewards => TranslateRewardLine(source, out translated) ||
                                                TranslateFast(source, out translated),
                _ => TranslateFast(source, out translated)
            };
        }

        public static bool TranslateFast(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return TranslateRandomSeedFast(source, out translated) ||
                   TranslateVotesFast(source, out translated) ||
                   TranslateDaysLeftFast(source, out translated);
        }

        public static bool TranslateRandomSeed(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTags(source).Trim(),
                @"^Random\s+seed\s*:\s*(?<seed>[+-]?\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            translated = $"\u968f\u673a\u79cd\u5b50\uff1a{match.Groups["seed"].Value}";
            return true;
        }

        public static bool TranslateVotes(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTags(source).Trim(),
                @"^(?<open>[\(\uff08]?)(?<votes>\d+\s*/\s*\d+)\s+Votes?(?<close>[\)\uff09]?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            var votes = SafeRegexReplace(match.Groups["votes"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
            var hasParens = match.Groups["open"].Value.Length > 0 || match.Groups["close"].Value.Length > 0;
            translated = hasParens ? $"\uff08{votes} \u7968\uff09" : $"{votes} \u7968";
            return true;
        }

        public static bool TranslateDaysLeft(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTags(source).Trim(),
                @"^(?<days>\d+)\s+Days?\s+Left$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            translated = $"\u5269\u4f59 {match.Groups["days"].Value} \u5929";
            return true;
        }

        public static bool TranslateScanValue(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTags(source).Trim(),
                @"^VALUE\s*:\s*(?<value>.+?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            translated = $"\u4ef7\u503c\uff1a{match.Groups["value"].Value.Trim()}";
            return true;
        }

        public static bool TranslateRewardLine(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            var totalMatch = SafeRegexMatch(
                trimmed,
                @"^TOTAL\s*[:\uff1a]\s*(?<amount>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (totalMatch.Success)
            {
                translated = $"\u603b\u8ba1\uff1a{totalMatch.Groups["amount"].Value.Trim()}";
                return true;
            }

            var valueMatch = SafeRegexMatch(
                StripRichTextTags(trimmed),
                @"^Value\s*[:\uff1a]\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (valueMatch.Success)
            {
                translated = $"\u4ef7\u503c\uff1a{valueMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var itemCollectedMatch = SafeRegexMatch(
                trimmed,
                @"^(?<item>.+?)\s+collected!$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (itemCollectedMatch.Success)
            {
                var collectedItem = BuildTerminalLocalizedItemName(itemCollectedMatch.Groups["item"].Value.Trim());
                translated = $"{collectedItem}\u5df2\u6536\u96c6\uff01";
                return true;
            }

            var collectedMatch = SafeRegexMatch(
                trimmed,
                @"^(?<amount>\$?\s*[+-]?\d+(?:\.\d+)?)\s+Collected$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (collectedMatch.Success)
            {
                translated = $"{SafeRegexReplace(collectedMatch.Groups["amount"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant)} \u5df2\u6536\u96c6";
                return true;
            }

            var itemValueMatch = SafeRegexMatch(
                trimmed,
                @"^(?<item>.+?)\s*(?<count>\(x\d+\))?\s*[:\uff1a]\s*(?<value>\$?\s*[+-]?\d+(?:\.\d+)?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!itemValueMatch.Success)
            {
                return false;
            }

            var itemSource = itemValueMatch.Groups["item"].Value.Trim();
            if (itemSource.Equals("DUE", StringComparison.OrdinalIgnoreCase) ||
                itemSource.Equals("VALUE", StringComparison.OrdinalIgnoreCase) ||
                itemSource.Equals("Random seed", StringComparison.OrdinalIgnoreCase) ||
                itemSource.Equals("DEADLINE", StringComparison.OrdinalIgnoreCase) ||
                itemSource.Equals("PROFIT QUOTA", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var item = BuildTerminalLocalizedItemName(itemSource);
            var count = itemValueMatch.Groups["count"].Value.Trim();
            var value = SafeRegexReplace(itemValueMatch.Groups["value"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
            translated = count.Length == 0 ? $"{item}\uff1a{value}" : $"{item} {count}\uff1a{value}";
            return true;
        }

        private static bool TranslateRandomSeedFast(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            const string prefix = "Random seed:";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var seed = trimmed[prefix.Length..].Trim();
            if (!LooksLikeSignedInteger(seed))
            {
                return false;
            }

            translated = $"\u968f\u673a\u79cd\u5b50\uff1a{seed}";
            return true;
        }

        private static bool TranslateVotesFast(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            var hasParens = trimmed.Length >= 2 &&
                            ((trimmed[0] == '(' && trimmed[^1] == ')') ||
                             (trimmed[0] == '\uff08' && trimmed[^1] == '\uff09'));
            var body = hasParens ? trimmed[1..^1].Trim() : trimmed;
            var voteSuffixLength = body.EndsWith(" Votes", StringComparison.OrdinalIgnoreCase)
                ? " Votes".Length
                : body.EndsWith(" Vote", StringComparison.OrdinalIgnoreCase)
                    ? " Vote".Length
                    : 0;
            if (voteSuffixLength == 0)
            {
                return false;
            }

            var votes = RemoveAsciiWhitespace(body[..^voteSuffixLength]);
            var slash = votes.IndexOf('/');
            if (slash <= 0 || slash >= votes.Length - 1 ||
                !AllDigits(votes[..slash]) ||
                !AllDigits(votes[(slash + 1)..]))
            {
                return false;
            }

            translated = hasParens ? $"\uff08{votes} \u7968\uff09" : $"{votes} \u7968";
            return true;
        }

        private static bool TranslateDaysLeftFast(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            var suffixLength = trimmed.EndsWith(" Days Left", StringComparison.OrdinalIgnoreCase)
                ? " Days Left".Length
                : trimmed.EndsWith(" Day Left", StringComparison.OrdinalIgnoreCase)
                    ? " Day Left".Length
                    : 0;
            if (suffixLength == 0)
            {
                return false;
            }

            var days = trimmed[..^suffixLength].Trim();
            if (!AllDigits(days))
            {
                return false;
            }

            translated = $"\u5269\u4f59 {days} \u5929";
            return true;
        }
    }
}
