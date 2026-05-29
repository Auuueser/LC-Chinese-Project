using System;
using System.Text;
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
            LooksLikeShipLeaveEarlyWarningTextCheap(source) ||
            source?.IndexOf("Notes", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source?.IndexOf("Spectating", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source?.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0 ||
            source?.IndexOf("YOU ARE FIRED", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return TranslatePlayerNotes(source, out translated) ||
                   TranslateStatLine(source, out translated) ||
                   TranslatePlayersFired(source, out translated) ||
                   HudDynamicTranslator.TranslateShipLeaveEarlyWarning(source, out translated) ||
                   HudDynamicTranslator.TranslateVotes(source, out translated) ||
                   HudDynamicTranslator.TranslateDaysLeft(source, out translated) ||
                   TranslatePlayerStatus(source, out translated);
        }

        public static bool TranslatePlayerNotes(string source, out string translated)
        {
            translated = source;
            if (source.IndexOf("Notes", StringComparison.OrdinalIgnoreCase) < 0 &&
                source.IndexOf("\u5907\u6ce8", StringComparison.Ordinal) < 0 &&
                source.IndexOf("\u7b14\u8bb0", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            var lines = source.Split('\n');
            var changed = false;
            var builder = new StringBuilder(source.Length + 16);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                var line = lines[i];
                var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
                var content = hasCarriageReturn ? line[..^1] : line;
                var rewritten = TranslatePlayerNoteLine(content);
                if (!string.Equals(content, rewritten, StringComparison.Ordinal))
                {
                    changed = true;
                }

                builder.Append(rewritten);
                if (hasCarriageReturn)
                {
                    builder.Append('\r');
                }
            }

            if (!changed)
            {
                return false;
            }

            translated = builder.ToString();
            return true;
        }

        private static string TranslatePlayerNoteLine(string line)
        {
            var trimmedStart = line.TrimStart();
            var leading = line[..(line.Length - trimmedStart.Length)];
            var header = trimmedStart.Trim();
            if (string.Equals(header, "Notes:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header, "Notes\uff1a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header, "\u5907\u6ce8:", StringComparison.Ordinal) ||
                string.Equals(header, "\u5907\u6ce8\uff1a", StringComparison.Ordinal) ||
                string.Equals(header, "\u7b14\u8bb0:", StringComparison.Ordinal) ||
                string.Equals(header, "\u7b14\u8bb0\uff1a", StringComparison.Ordinal))
            {
                return leading + "\u5907\u6ce8\uff1a";
            }

            var bulletPrefix = string.Empty;
            var note = trimmedStart;
            if (note.StartsWith("*", StringComparison.Ordinal))
            {
                var afterBullet = note[1..].TrimStart();
                bulletPrefix = "* ";
                note = afterBullet;
            }

            return TranslatePlayerNoteToken(note, out var localized)
                ? leading + bulletPrefix + localized
                : line;
        }

        private static bool TranslatePlayerNoteToken(string source, out string translated)
        {
            translated = source;
            var normalized = source.Trim().TrimEnd('.').Trim();
            translated = normalized.ToLowerInvariant() switch
            {
                "most lazy employee" => "\u6700\u61d2\u60f0\u7684\u5458\u5de5",
                "the laziest employee" => "\u6700\u61d2\u60f0\u7684\u5458\u5de5",
                "most profitable" => "\u6700\u4f1a\u8d5a\u94b1\u7684\u5458\u5de5",
                "most paranoid employee" => "\u6700\u504f\u6267\u7684\u5458\u5de5",
                "the most paranoid employee" => "\u6700\u504f\u6267\u7684\u5458\u5de5",
                "sustained the most injuries" => "\u53d7\u4f24\u4e25\u91cd\u7684\u5458\u5de5",
                "sustained the most injurie" => "\u53d7\u4f24\u4e25\u91cd\u7684\u5458\u5de5",
                _ => string.Empty
            };
            return translated.Length > 0;
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

            var overtimeMatch = SafeRegexMatch(
                trimmed,
                @"^Your\s+current\s+overtime\s+bonus\s+is\s+(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (overtimeMatch.Success)
            {
                translated = $"\u5f53\u524d\u52a0\u73ed\u5956\u52b1\u4e3a {overtimeMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            var totalCreditsMatch = SafeRegexMatch(
                trimmed,
                @"^Your\s+new\s+total\s+credits\s+will\s+be\s+(?<value>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (totalCreditsMatch.Success)
            {
                translated = $"\u65b0\u7684\u603b\u4fe1\u7528\u70b9\u5c06\u4e3a {totalCreditsMatch.Groups["value"].Value.Trim()}";
                return true;
            }

            return false;
        }

        public static bool TranslatePlayersFired(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (string.Equals(trimmed, "YOU ARE FIRED.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "YOU ARE FIRED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "\u4f60\u88ab\u89e3\u96c7\u4e86\uff01", StringComparison.Ordinal) ||
                string.Equals(trimmed, "\u4f60\u5df2\u88ab\u516c\u53f8\u89e3\u96c7\u3002", StringComparison.Ordinal))
            {
                translated = "\u4f60\u5df2\u88ab\u516c\u53f8\u89e3\u96c7";
                return true;
            }

            if (string.Equals(trimmed, "You did not meet the profit quota before the deadline.", StringComparison.OrdinalIgnoreCase) ||
                trimmed.IndexOf("\u5229\u6da6\u6307\u6807", StringComparison.Ordinal) >= 0 ||
                trimmed.IndexOf("\u76ee\u6807\u91d1\u989d", StringComparison.Ordinal) >= 0)
            {
                translated = "\u672a\u80fd\u6309\u671f\u5b8c\u6210\u5229\u6da6\u6307\u6807";
                return true;
            }

            return false;
        }

        public static bool TranslatePlayerStatus(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (TranslateSpectatingStatus(trimmed, out translated))
            {
                return true;
            }

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

        private static bool TranslateSpectatingStatus(string source, out string translated)
        {
            translated = source;
            var suffixMatch = SafeRegexMatch(
                source,
                @"^(?<open>[<\(\uff08])?\s*Spectating\s*[:\uff1a]\s*(?<name>.+?)\s*(?<close>[>\)\uff09])?(?:\s*(?<suffix>\[[^\]]+\]))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (suffixMatch.Success)
            {
                var suffixName = suffixMatch.Groups["name"].Value.Trim();
                if (suffixName.Length == 0)
                {
                    return false;
                }

                var suffixOpen = suffixMatch.Groups["open"].Value;
                var suffixClose = suffixMatch.Groups["close"].Value;
                var suffixValue = $"\u6b63\u5728\u65c1\u89c2\uff1a{suffixName}";
                translated = suffixOpen == "<" || suffixClose == ">"
                    ? $"<{suffixValue}>"
                    : suffixOpen.Length > 0 || suffixClose.Length > 0
                        ? $"\uff08{suffixValue}\uff09"
                        : suffixValue;
                var suffix = suffixMatch.Groups["suffix"].Value;
                if (suffix.Length > 0)
                {
                    translated += " " + suffix;
                }

                return true;
            }

            var match = SafeRegexMatch(
                source,
                @"^(?<open>[<\(（])?\s*Spectating\s*[:：]\s*(?<name>.+?)\s*(?<close>[>\)）])?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            var name = match.Groups["name"].Value.Trim();
            if (name.Length == 0)
            {
                return false;
            }

            var open = match.Groups["open"].Value;
            var close = match.Groups["close"].Value;
            var value = $"\u6b63\u5728\u65c1\u89c2\uff1a{name}";
            translated = open == "<" || close == ">"
                ? $"<{value}>"
                : open.Length > 0 || close.Length > 0
                    ? $"\uff08{value}\uff09"
                    : value;
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
