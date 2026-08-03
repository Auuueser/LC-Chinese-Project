using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class HudDynamicTranslator
    {
        private static readonly Dictionary<string, string> DeathCauseEntries = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Unknown"] = "\u672a\u77e5",
            ["Bludgeoning"] = "\u949d\u51fb",
            ["Gravity"] = "\u5760\u843d\u51b2\u51fb",
            ["Blast"] = "\u7206\u70b8",
            ["Strangulation"] = "\u52d2\u9888",
            ["Suffocation"] = "\u7a92\u606f",
            ["Mauling"] = "\u6495\u54ac",
            ["Gunshot"] = "\u67aa\u51fb",
            ["Gunshots"] = "\u67aa\u51fb",
            ["Crushing"] = "\u538b\u788e",
            ["Drowning"] = "\u6eba\u6c34",
            ["Abandoned"] = "\u88ab\u9057\u5f03",
            ["Electrocution"] = "\u89e6\u7535",
            ["Kicking"] = "\u8e22\u51fb",
            ["Burning"] = "\u70e7\u4f24",
            ["Stabbing"] = "\u523a\u4f24",
            ["Fan"] = "\u98ce\u6247\u5207\u5272",
            ["Inertia"] = "\u60ef\u6027\u51b2\u51fb",
            ["Snipping"] = "\u526a\u5207",
            ["Scratching"] = "\u6293\u4f24"
        };

        public static bool CanHandleCheap(string? source) =>
            LooksLikeLoadingInfoTextCheap(source) ||
            LooksLikeRandomSeedTextCheap(source) ||
            LooksLikeVoteTextCheap(source) ||
            LooksLikeDaysLeftTextCheap(source) ||
            LooksLikeShipLeaveEarlyWarningTextCheap(source) ||
            LooksLikeCombatNotificationTextCheap(source) ||
            LooksLikeHudStatusLineCheap(source) ||
            LooksLikeHudNotificationTextCheap(source);

        public static bool Translate(DynamicTextDomain domain, string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return domain switch
            {
                DynamicTextDomain.HudScanner => TranslateScannerSubText(source, out translated) ||
                                                TranslateScanValue(source, out translated) ||
                                                TryTranslateFastExact(source, out translated) ||
                                                TranslateScannerLabel(source, out translated) ||
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

            return TranslateLoadingInfoFast(source, out translated) ||
                   TranslateRandomSeedFast(source, out translated) ||
                   TranslateVotesFast(source, out translated) ||
                   TranslateDaysLeftFast(source, out translated) ||
                   TranslateCombatNotificationFast(source, out translated) ||
                   TranslateHudStatusLineFast(source, out translated) ||
                   TranslateShipLeaveEarlyWarning(source, out translated) ||
                   TranslateHudNotificationFast(source, out translated);
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
                @"^VALUE\s*:\s*(?<value>.*?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            translated = $"\u4ef7\u503c\uff1a{match.Groups["value"].Value.Trim()}";
            return true;
        }

        public static bool TranslateShipLeaveEarlyWarning(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTagsCheap(source).Trim(),
                @"^WARNING!\s*Please\s+return\s+by\s+(?<time>.+?)\.\s*A\s+vote\s+has\s+been\s+cast,\s+and\s+the\s+autopilot\s*ship\s+will\s+leave\s+early\.$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            var leaveTime = LocalizeClockPeriod(match.Groups["time"].Value.Trim());
            translated = $"\u8b66\u544a\uff01\u8bf7\u5728 {leaveTime} \u4e4b\u524d\u8fd4\u56de\u3002\u6295\u7968\u5df2\u7ecf\u5b8c\u6210\uff0c\u81ea\u52a8\u9a7e\u9a76\u98de\u8239\u5c06\u63d0\u65e9\u79bb\u5f00\u3002";
            return true;
        }

        private static bool TranslateScannerLabel(string source, out string translated)
        {
            translated = source;
            var stripped = StripRichTextTagsCheap(source).Trim();
            if (stripped.Length == 0 ||
                stripped.IndexOf('\n') >= 0 ||
                stripped.IndexOf(':') >= 0 ||
                LooksLikeSimpleNumber(stripped))
            {
                return false;
            }

            var localized = BuildTerminalLocalizedItemName(stripped);
            if (string.Equals(localized, stripped, StringComparison.Ordinal))
            {
                return false;
            }

            translated = localized;
            return true;
        }

        private static bool TranslateScannerSubText(string source, out string translated)
        {
            translated = source;
            var stripped = StripRichTextTagsCheap(source).Trim();
            var deathCauseMatch = SafeRegexMatch(
                stripped,
                @"^Cause\s+of\s+death\s*[:\uff1a]\s*(?<cause>.+?)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (deathCauseMatch.Success)
            {
                var cause = TranslateDeathCauseName(deathCauseMatch.Groups["cause"].Value.Trim());
                translated = $"\u6b7b\u56e0\uff1a{cause}";
                return true;
            }

            if (!stripped.Equals("You've got work to do.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            translated = "\u4f60\u8fd8\u6709\u5de5\u4f5c\u8981\u505a\u3002";
            return true;
        }

        private static string TranslateDeathCauseName(string cause)
        {
            if (DeathCauseEntries.TryGetValue(cause, out var translated))
            {
                return translated;
            }

            var localized = BuildTerminalLocalizedItemName(cause);
            return string.IsNullOrWhiteSpace(localized) ? cause : localized;
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

        private static bool TranslateLoadingInfoFast(string source, out string translated)
        {
            translated = source;
            if (!LooksLikeLoadingInfoTextCheap(source))
            {
                return false;
            }

            var normalized = StripRichTextTagsCheap(source).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var separator = source.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            var firstLineEnd = normalized.IndexOf('\n');
            if (firstLineEnd <= 0 || firstLineEnd >= normalized.Length - 1)
            {
                return false;
            }

            var firstLine = normalized[..firstLineEnd].Trim();
            var remainder = normalized[(firstLineEnd + 1)..];
            var secondLineEnd = remainder.IndexOf('\n');
            var secondLine = secondLineEnd < 0 ? remainder.Trim() : remainder[..secondLineEnd].Trim();
            var tail = secondLineEnd < 0 ? string.Empty : remainder[(secondLineEnd + 1)..];

            if (!TryTranslateLoadingInfoHeader(firstLine, out var translatedFirstLine) ||
                !TryTranslateLoadingInfoState(secondLine, out var translatedSecondLine))
            {
                return false;
            }

            translated = translatedFirstLine + separator + translatedSecondLine;
            if (tail.Length > 0)
            {
                translated += separator + tail.Replace("\n", separator, StringComparison.Ordinal);
            }

            return true;
        }

        private static bool TryTranslateLoadingInfoHeader(string line, out string translated)
        {
            translated = line;
            const string randomSeedPrefix = "Random seed:";
            if (line.StartsWith(randomSeedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var seed = line[randomSeedPrefix.Length..].Trim();
                if (!LooksLikeSignedInteger(seed))
                {
                    return false;
                }

                translated = "\u968f\u673a\u79cd\u5b50\uff1a" + seed;
                return true;
            }

            if (line.Equals("Waiting for crew...", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u7b49\u5f85\u8239\u5458\u52a0\u8f7d\u4e2d";
                return true;
            }

            if (line.Equals("Waiting for Client...", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u7b49\u5f85\u5ba2\u6237\u7aef\u52a0\u8f7d\u4e2d";
                return true;
            }

            return false;
        }

        private static bool TryTranslateLoadingInfoState(string line, out string translated)
        {
            translated = line;
            if (line.Equals("All players loaded!", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u6240\u6709\u73a9\u5bb6\u5df2\u52a0\u8f7d";
                return true;
            }

            const string playersLoadedPrefix = "Players loaded:";
            if (!line.StartsWith(playersLoadedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var count = RemoveAsciiWhitespace(line[playersLoadedPrefix.Length..]);
            var slash = count.IndexOf('/');
            if (slash <= 0 ||
                slash >= count.Length - 1 ||
                !AllDigits(count[..slash]) ||
                !AllDigits(count[(slash + 1)..]))
            {
                return false;
            }

            translated = "\u73a9\u5bb6\u52a0\u8f7d\uff1a" + count;
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

        private static bool TranslateHudStatusLineFast(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            var match = SafeRegexMatch(trimmed, @"^Page\s+(?<page>[^/]+)/(?<total>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u7b2c {match.Groups["page"].Value.Trim()}/{match.Groups["total"].Value.Trim()} \u9875";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Seed\s*:\s*(?<seed>[0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u79cd\u5b50\uff1a{match.Groups["seed"].Value}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Power\s*:\s*(?<current>\d+)\s*/\s*(?<max>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u7535\u91cf\uff1a{match.Groups["current"].Value} / {match.Groups["max"].Value}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<volume>\d+)%\s+volume$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u97f3\u91cf {match.Groups["volume"].Value}%";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^\((?<percent>.+?)%\s+Battery\s+Life\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\uff08{match.Groups["percent"].Value.Trim()}% \u7535\u6c60\u7535\u91cf\uff09";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<percent>.+?)%\s*\((?<remaining>.+?)\s+remaining\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{match.Groups["percent"].Value.Trim()}%\uff08\u5269\u4f59 {match.Groups["remaining"].Value.Trim()}\uff09";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Found\s+(?<count>\d+)\s+items\s+with\s+a\s+total\s+value\s+of\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u627e\u5230 {match.Groups["count"].Value} \u4ef6\u7269\u54c1\uff0c\u603b\u4ef7\u503c {match.Groups["value"].Value.Trim()}";
                return true;
            }

            if (TryTranslateMultilineStatus(trimmed, out translated))
            {
                return true;
            }

            if (TryTranslateScannerTotalStatus(source, out translated) ||
                TryTranslateSimpleStatusLine(trimmed, out translated))
            {
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Player\s+(?<player>.+?)\s+is\s+now\s+connecting$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u73a9\u5bb6 {match.Groups["player"].Value.Trim()} \u6b63\u5728\u8fde\u63a5";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<count>\d+)\s+Players\s+Connecting!!$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{match.Groups["count"].Value} \u540d\u73a9\u5bb6\u6b63\u5728\u8fde\u63a5\uff01\uff01";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^there\s+are\s+still\s+(?<count>\d+)\s+Players\s+connecting!!$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u4ecd\u6709 {match.Groups["count"].Value} \u540d\u73a9\u5bb6\u6b63\u5728\u8fde\u63a5\uff01\uff01";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Press\s+(?<key>.+?)\s+to\s+stop\s+teleport$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6309 {match.Groups["key"].Value.Trim()} \u505c\u6b62\u4f20\u9001";
                return true;
            }

            return false;
        }

        private static bool TranslateCombatNotificationFast(string source, out string translated)
        {
            translated = source;
            if (!LooksLikeCombatNotificationTextCheap(source))
            {
                return false;
            }

            var trimmed = StripRichTextTagsCheap(source).Trim();
            const string localKilledPrefix = "Killed ";
            if (trimmed.StartsWith(localKilledPrefix, StringComparison.Ordinal))
            {
                var target = trimmed[localKilledPrefix.Length..].Trim();
                if (target.Length == 0)
                {
                    return false;
                }

                translated = "\u51fb\u6740 " + BuildTerminalLocalizedItemName(target);
                return true;
            }

            const string remoteKilledToken = " Killed ";
            var killedIndex = trimmed.IndexOf(remoteKilledToken, StringComparison.Ordinal);
            if (killedIndex > 0)
            {
                var actor = trimmed[..killedIndex].Trim();
                var target = trimmed[(killedIndex + remoteKilledToken.Length)..].Trim();
                if (actor.Length == 0 || target.Length == 0)
                {
                    return false;
                }

                translated = actor + " \u51fb\u6740 " + BuildTerminalLocalizedItemName(target);
                return true;
            }

            if (!trimmed.EndsWith(" HP", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var dash = trimmed.LastIndexOf('-');
            if (dash <= 0 || dash >= trimmed.Length - " HP".Length - 1)
            {
                return false;
            }

            var targetName = trimmed[..dash].Trim();
            var amount = trimmed.Substring(dash + 1, trimmed.Length - dash - 1 - " HP".Length).Trim();
            if (targetName.Length == 0 || amount.Length == 0 || !AllDigits(amount))
            {
                return false;
            }

            translated = BuildTerminalLocalizedItemName(targetName) + " -" + amount + " \u751f\u547d\u503c";
            return true;
        }

        private static bool TryTranslateMultilineStatus(string trimmed, out string translated)
        {
            translated = trimmed;
            var normalized = trimmed.Replace("\r\n", "\n");

            foreach (var (label, localizedLabel) in new[]
                     {
                         ("Dance", "\u8df3\u821e"),
                         ("EMPTY", "\u7a7a"),
                         ("Point", "\u6307\u5411")
                     })
            {
                if (!normalized.StartsWith(label + "\n", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rest = normalized[(label.Length + 1)..];
                if (rest.Length == 0)
                {
                    return false;
                }

                translated = localizedLabel + "\n" + rest;
                return true;
            }

            if (SafeRegexMatch(normalized, @"^Just\s+do\s+what\s+she\s+says\.\.\.\.\n*[|]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Success)
            {
                translated = "\u7167\u5979\u8bf4\u7684\u505a\u2026\u2026\n||||";
                return true;
            }

            if (SafeRegexMatch(normalized, @"^KEEP\s+YOUR\s+TOP\s+SPEED\n*[|]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Success)
            {
                translated = "\u4fdd\u6301\u6700\u9ad8\u901f\u5ea6\n||||";
                return true;
            }

            return false;
        }

        private static bool TryTranslateScannerTotalStatus(string source, out string translated)
        {
            translated = source;
            var normalized = StripRichTextTagsCheap(source).Replace("\r\n", "\n");
            var leadingNewline = normalized.StartsWith("\n", StringComparison.Ordinal);
            var match = SafeRegexMatch(
                normalized.Trim(),
                @"^Total\s+Scanned\s*:\s*(?<scanned>.+?)\s+Ship\s+Total\s*:\s*(?<ship>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            translated = (leadingNewline ? "\n" : string.Empty) +
                         $"\u626b\u63cf\u603b\u8ba1\uff1a{match.Groups["scanned"].Value.Trim()} \u98de\u8239\u603b\u503c\uff1a{match.Groups["ship"].Value.Trim()}";
            return true;
        }

        private static bool TryTranslateSimpleStatusLine(string trimmed, out string translated)
        {
            translated = trimmed;
            if (trimmed.Length == 0)
            {
                return false;
            }

            var match = SafeRegexMatch(trimmed, @"^(?<label>[^:\r\n]+):\s*(?<mode>ALL|NONE)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                var label = BuildTerminalLocalizedItemName(match.Groups["label"].Value.Trim());
                var mode = match.Groups["mode"].Value.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? "\u5168\u90e8" : "\u65e0";
                translated = $"{label}\uff1a{mode}";
                return true;
            }

            foreach (var (label, localizedLabel) in new[]
                     {
                         ("Credits", "\u4fe1\u7528\u70b9"),
                         ("CREDITS", "\u4fe1\u7528\u70b9"),
                         ("DAY", "\u65e5\u671f"),
                         ("TIME", "\u65f6\u95f4"),
                         ("WEATHER", "\u5929\u6c14"),
                         ("SCRAP IN SHIP", "\u98de\u8239\u5185\u5e9f\u6599"),
                         ("SHIP", "\u98de\u8239"),
                         ("MOON", "\u536b\u661f"),
                         ("Value", "\u4ef7\u503c"),
                         ("Default", "\u9ed8\u8ba4\u503c"),
                         ("LOCKED", "\u9501\u5b9a\u72b6\u6001"),
                         ("Ping", "\u5ef6\u8fdf"),
                         ("FILTER", "\u8fc7\u6ee4\u5668"),
                         ("SORT", "\u6392\u5e8f"),
                         ("CHARACTERS LEFT", "\u5269\u4f59\u5b57\u7b26\u6570")
                     })
            {
                if (TryTranslateColonStatus(trimmed, label, localizedLabel, out translated))
                {
                    return true;
                }
            }

            match = SafeRegexMatch(trimmed, @"^Inventory\s+slot\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u7269\u54c1\u680f\u69fd\u4f4d {match.Groups["value"].Value.Trim()}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Favorites\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6536\u85cf {match.Groups["value"].Value.Trim()}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Loading\s+(?<value>.+?)\s+server\s+list\.\.\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6b63\u5728\u52a0\u8f7d {match.Groups["value"].Value.Trim()} \u670d\u52a1\u5668\u5217\u8868\u2026\u2026";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<name>.+?)\s+\((?<count>.+?)\s+uses\s+remaining\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{BuildTerminalLocalizedItemName(match.Groups["name"].Value.Trim())}\uff08\u5269\u4f59 {match.Groups["count"].Value.Trim()} \u6b21\u4f7f\u7528\uff09";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<name>.+?)\s+\((?<label>.+?)\s*:\s*(?<value>.+?)\s+remaining\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{BuildTerminalLocalizedItemName(match.Groups["name"].Value.Trim())}\uff08{match.Groups["label"].Value.Trim()}\uff1a\u5269\u4f59 {match.Groups["value"].Value.Trim()}\uff09";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Lost\s+(?<value>.+?)\s+scrap$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u635f\u5931 {match.Groups["value"].Value.Trim()} \u5e9f\u6599";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Flat\s+Body\s+Of\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{BuildTerminalLocalizedItemName(match.Groups["value"].Value.Trim())} \u7684\u538b\u6241\u9057\u4f53";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Received\s+from\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6765\u81ea {match.Groups["value"].Value.Trim()}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<value>.+?)'s\s+Crew$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{match.Groups["value"].Value.Trim()} \u7684\u5c0f\u961f";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<value>.+?)\s+has\s+been\s+disabled!$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{match.Groups["value"].Value.Trim()} \u5df2\u88ab\u7981\u7528\uff01";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^(?<value>.+?)\s+Submit$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"{match.Groups["value"].Value.Trim()}\uff1a\u63d0\u4ea4\u5185\u5bb9";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Line\s+Break\s+(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6362\u884c\u7b26 {match.Groups["value"].Value.Trim()}";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^Amount\s*:\s*(?<value>\d{1,2})\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u6570\u91cf\uff1a{match.Groups["value"].Value}\u3002";
                return true;
            }

            match = SafeRegexMatch(trimmed, @"^You've made\.\.\s*(?<value>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                translated = $"\u4f60\u5df2\u83b7\u5f97\u2026\u2026{match.Groups["value"].Value.Trim()}";
                return true;
            }

            if (SafeRegexMatch(trimmed, @"^Disconnected\s+due\s+to\s+host\s+shutting\s+down\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Success)
            {
                translated = "\u623f\u4e3b\u5173\u95ed\u623f\u95f4";
                return true;
            }

            return false;
        }

        private static bool TryTranslateColonStatus(string trimmed, string label, string localizedLabel, out string translated)
        {
            translated = trimmed;
            if (!trimmed.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var index = label.Length;
            if (index >= trimmed.Length || trimmed[index] != ':')
            {
                return false;
            }

            var value = trimmed[(index + 1)..].TrimStart(' ', '\t');
            translated = value.StartsWith("\n", StringComparison.Ordinal)
                ? localizedLabel + "\uff1a" + value
                : localizedLabel + "\uff1a" + value.Trim();
            return true;
        }

        private static bool LooksLikeHudStatusLineCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var text = StripRichTextTagsCheap(source).TrimStart();
            return text.StartsWith("Credits:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("CREDITS:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("DAY:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("TIME:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("WEATHER:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("SCRAP IN SHIP:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("Total Scanned:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("Found ", StringComparison.OrdinalIgnoreCase) ||
                   text.IndexOf("Battery Life", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeHudNotificationTextCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var text = StripRichTextTagsCheap(source).TrimStart();
            return text.StartsWith("Found journal entry:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("New creature data sent to terminal", StringComparison.OrdinalIgnoreCase);
        }

        private static string LocalizeClockPeriod(string value)
        {
            if (value.EndsWith("AM", StringComparison.OrdinalIgnoreCase))
            {
                return value[..^2].TrimEnd() + " \u4e0a\u5348";
            }

            if (value.EndsWith("PM", StringComparison.OrdinalIgnoreCase))
            {
                return value[..^2].TrimEnd() + " \u4e0b\u5348";
            }

            return value;
        }

        private static bool TranslateHudNotificationFast(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            if (trimmed.Equals("New creature data sent to terminal!", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("New creature data sent to terminal.", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u65b0\u7684\u751f\u7269\u6570\u636e\u5df2\u53d1\u9001\u81f3\u7ec8\u7aef\uff01";
                return true;
            }

            const string prefix = "Found journal entry:";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var title = trimmed[prefix.Length..].Trim();
            if (title.Length >= 2 &&
                ((title[0] == '\'' && title[^1] == '\'') ||
                 (title[0] == '"' && title[^1] == '"')))
            {
                title = title[1..^1].Trim();
            }

            translated = title.Length == 0
                ? "\u627e\u5230\u65e5\u5fd7"
                : $"\u627e\u5230\u65e5\u5fd7\uff1a{BuildTerminalLocalizedItemName(title)}";
            return true;
        }
    }
}
