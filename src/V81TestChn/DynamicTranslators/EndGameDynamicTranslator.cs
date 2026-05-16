using System;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class EndGameDynamicTranslator
    {
        public static bool CanHandleCheap(string? source) =>
            LooksLikeEndgameStatTextCheap(source) ||
            LooksLikeVoteTextCheap(source) ||
            LooksLikeDaysLeftTextCheap(source) ||
            source?.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source?.IndexOf("YOU ARE FIRED", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return TranslateStatLine(source, out translated) ||
                   TranslatePlayersFired(source, out translated) ||
                   HudDynamicTranslator.TranslateVotes(source, out translated) ||
                   HudDynamicTranslator.TranslateDaysLeft(source, out translated) ||
                   TranslatePlayerStatus(source, out translated);
        }

        public static bool TranslateStatLine(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();

            var casualtiesMatch = SafeRegexMatch(
                trimmed,
                @"^(?<count>\d+)\s+(?:casualties|\u4eba\u4f24\u4ea1)\s*[:\uff1a]\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (casualtiesMatch.Success)
            {
                translated = $"{casualtiesMatch.Groups["count"].Value} \u4eba\u4f24\u4ea1\uff1a{casualtiesMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var bodiesMatch = SafeRegexMatch(
                trimmed,
                @"^(?<open>[<\(])(?<count>\d+)\s+bodies recovered(?<close>[>\)])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (bodiesMatch.Success)
            {
                translated = $"{bodiesMatch.Groups["open"].Value}{bodiesMatch.Groups["count"].Value} \u5177\u5c38\u4f53\u5df2\u56de\u6536{bodiesMatch.Groups["close"].Value}";
                return true;
            }

            var bareBodiesMatch = SafeRegexMatch(
                trimmed,
                @"^(?<count>\d+)\s+bodies recovered$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (bareBodiesMatch.Success)
            {
                translated = $"{bareBodiesMatch.Groups["count"].Value} \u5177\u5c38\u4f53\u5df2\u56de\u6536";
                return true;
            }

            var dueMatch = SafeRegexMatch(
                trimmed,
                @"^DUE:\s*(?<amount>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dueMatch.Success)
            {
                translated = $"\u5e94\u4ed8\uff1a{dueMatch.Groups["amount"].Value.Trim()}";
                return true;
            }

            var daysWorkedMatch = SafeRegexMatch(
                trimmed,
                @"^Days on the job\s*:\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (daysWorkedMatch.Success)
            {
                translated = $"\u5de5\u4f5c\u5929\u6570\uff1a{daysWorkedMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var scrapValueMatch = SafeRegexMatch(
                trimmed,
                @"^Scrap value collected\s*:\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (scrapValueMatch.Success)
            {
                translated = $"\u6536\u96c6\u5e9f\u6599\u4ef7\u503c\uff1a{scrapValueMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var deathsMatch = SafeRegexMatch(
                trimmed,
                @"^Deaths\s*:\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (deathsMatch.Success)
            {
                translated = $"\u6b7b\u4ea1\u6b21\u6570\uff1a{deathsMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var stepsMatch = SafeRegexMatch(
                trimmed,
                @"^Steps taken\s*:\s*(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (stepsMatch.Success)
            {
                translated = $"\u884c\u8d70\u6b65\u6570\uff1a{stepsMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            return false;
        }

        public static bool TranslatePlayersFired(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (string.Equals(trimmed, "YOU ARE FIRED.", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u4f60\u88ab\u89e3\u96c7\u4e86\uff01";
                return true;
            }

            if (string.Equals(trimmed, "You did not meet the profit quota before the deadline.", StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u4f60\u672a\u80fd\u5728\u622a\u6b62\u65e5\u671f\u524d\u8fbe\u5230\u76ee\u6807\u91d1\u989d";
                return true;
            }

            return false;
        }

        public static bool TranslatePlayerStatus(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (TranslateStatusToken(trimmed, out translated))
            {
                return true;
            }

            var match = SafeRegexMatch(
                trimmed,
                @"^(?<name>[\s\S]+?)(?<sep>\r?\n|\s+)\(?(?<status>Dead|Deceased|Missing)\)?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success || !TranslateStatusToken(match.Groups["status"].Value, out var status))
            {
                return false;
            }

            translated = match.Groups["name"].Value.TrimEnd() + match.Groups["sep"].Value + status;
            return true;
        }

        private static bool TranslateStatusToken(string source, out string translated)
        {
            translated = source.Trim() switch
            {
                "(Dead)" => "\uff08\u6b7b\u4ea1\uff09",
                "Dead" => "\uff08\u6b7b\u4ea1\uff09",
                "Deceased" => "\uff08\u6b7b\u4ea1\uff09",
                "(Deceased)" => "\uff08\u6b7b\u4ea1\uff09",
                "Missing" => "\uff08\u5931\u8e2a\uff09",
                "(Missing)" => "\uff08\u5931\u8e2a\uff09",
                _ => string.Empty
            };
            return translated.Length > 0;
        }
    }
}
