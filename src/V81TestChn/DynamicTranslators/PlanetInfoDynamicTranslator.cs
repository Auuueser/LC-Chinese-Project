using System;
using System.Text;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class PlanetInfoDynamicTranslator
    {
        public static bool CanHandleCheap(string? source) => LooksLikePlanetInfoTextCheap(source);

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            return !string.IsNullOrWhiteSpace(source) && TranslateLine(source, out translated);
        }

        public static bool TranslateFast(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source) || !CanHandleCheap(source))
            {
                return false;
            }

            var newlineIndex = source.IndexOf('\n');
            if (newlineIndex < 0)
            {
                return TranslateLineFast(source, out translated);
            }

            var changed = false;
            var builder = new StringBuilder(source.Length + 16);
            var start = 0;
            while (start <= source.Length)
            {
                var nextNewline = source.IndexOf('\n', start);
                var end = nextNewline < 0 ? source.Length : nextNewline;
                var line = source[start..end];
                var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
                var content = hasCarriageReturn ? line[..^1] : line;
                if (TranslateLineFast(content, out var rewrittenLine))
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

                if (nextNewline < 0)
                {
                    break;
                }

                builder.Append('\n');
                start = nextNewline + 1;
            }

            if (!changed)
            {
                return false;
            }

            translated = builder.ToString();
            return true;
        }

        public static bool TranslateLine(string source, out string translated)
        {
            translated = source;
            var trimmed = StripRichTextTags(source).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (TryTranslateRiskLevelText(trimmed, out var riskLevel))
            {
                var leadingLength = source.Length - source.TrimStart().Length;
                translated = (leadingLength > 0 ? source[..leadingLength] : string.Empty) + riskLevel;
                return true;
            }

            foreach (var (englishLabel, chineseLabel, translateValue) in new[]
                     {
                         ("CELESTIAL BODY:", "\u5929\u4f53\uff1a", false),
                         ("CELESTIAL_BODY:", "\u5929\u4f53\uff1a", false),
                         ("\u5929\u4f53:", "\u5929\u4f53\uff1a", false),
                         ("\u5929\u4f53\uff1a", "\u5929\u4f53\uff1a", false),
                         ("POPULATION:", "\u4eba\u53e3\uff1a", true),
                         ("\u4eba\u53e3:", "\u4eba\u53e3\uff1a", true),
                         ("\u4eba\u53e3\uff1a", "\u4eba\u53e3\uff1a", true),
                         ("CONDITIONS:", "\u73af\u5883\uff1a", true),
                         ("\u73af\u5883:", "\u73af\u5883\uff1a", true),
                         ("\u73af\u5883\uff1a", "\u73af\u5883\uff1a", true),
                         ("FAUNA:", "\u751f\u6001\uff1a", true),
                         ("\u751f\u6001:", "\u751f\u6001\uff1a", true),
                         ("\u751f\u6001\uff1a", "\u751f\u6001\uff1a", true)
                     })
            {
                if (!trimmed.StartsWith(englishLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = trimmed[englishLabel.Length..].Trim();
                translated = value.Length == 0
                    ? chineseLabel
                    : $"{chineseLabel}{(translateValue ? TranslateKnownValue(value) : value)}";
                return true;
            }

            var knownValue = TranslateKnownStandaloneValue(trimmed);
            if (!string.Equals(knownValue, trimmed, StringComparison.Ordinal))
            {
                var leadingLength = source.Length - source.TrimStart().Length;
                translated = leadingLength > 0 ? source[..leadingLength] + knownValue : knownValue;
                return true;
            }

            return false;
        }

        public static string TranslateKnownValue(string value)
        {
            var normalized = NormalizeLoose(value);
            var core = TranslateKnownValueCore(normalized);
            if (core.Length != 0)
            {
                return core;
            }

            if (TryTranslateExact(value, out var exact) &&
                !string.Equals(exact, value, StringComparison.Ordinal))
            {
                return SanitizeTranslatedText(exact);
            }

            if (TryTranslateRegex(value, out var regex) &&
                !string.Equals(regex, value, StringComparison.Ordinal))
            {
                return SanitizeTranslatedText(regex);
            }

            if (TryTranslateKnownPlanetName(value, out var planetName))
            {
                return planetName;
            }

            return value;
        }

        private static bool TranslateLineFast(string source, out string translated)
        {
            translated = source;
            var leadingLength = source.Length - source.TrimStart().Length;
            var leading = leadingLength > 0 ? source[..leadingLength] : string.Empty;
            var trimmed = StripRichTextTagsCheap(source).Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (TryTranslateRiskLevelText(trimmed, out var riskLevel))
            {
                translated = leading + riskLevel;
                return true;
            }

            var rewritten = source;
            var matched =
                TryRewrite("CELESTIAL BODY:", "\u5929\u4f53\uff1a", translateValue: false) ||
                TryRewrite("CELESTIAL_BODY:", "\u5929\u4f53\uff1a", translateValue: false) ||
                TryRewrite("\u5929\u4f53:", "\u5929\u4f53\uff1a", translateValue: false) ||
                TryRewrite("\u5929\u4f53\uff1a", "\u5929\u4f53\uff1a", translateValue: false) ||
                TryRewrite("POPULATION:", "\u4eba\u53e3\uff1a", translateValue: true) ||
                TryRewrite("\u4eba\u53e3:", "\u4eba\u53e3\uff1a", translateValue: true) ||
                TryRewrite("\u4eba\u53e3\uff1a", "\u4eba\u53e3\uff1a", translateValue: true) ||
                TryRewrite("CONDITIONS:", "\u73af\u5883\uff1a", translateValue: true) ||
                TryRewrite("\u73af\u5883:", "\u73af\u5883\uff1a", translateValue: true) ||
                TryRewrite("\u73af\u5883\uff1a", "\u73af\u5883\uff1a", translateValue: true) ||
                TryRewrite("FAUNA:", "\u751f\u6001\uff1a", translateValue: true) ||
                TryRewrite("\u751f\u6001:", "\u751f\u6001\uff1a", translateValue: true) ||
                TryRewrite("\u751f\u6001\uff1a", "\u751f\u6001\uff1a", translateValue: true);
            if (!matched && TryTranslateKnownValueFastLine())
            {
                translated = rewritten;
                return true;
            }

            translated = rewritten;
            return matched;

            bool TryTranslateKnownValueFastLine()
            {
                var value = TranslateKnownStandaloneValue(trimmed);
                if (string.Equals(value, trimmed, StringComparison.Ordinal))
                {
                    return false;
                }

                rewritten = leading + value;
                return true;
            }

            bool TryRewrite(string label, string localizedLabel, bool translateValue)
            {
                if (!trimmed.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var value = trimmed[label.Length..].Trim();
                rewritten = value.Length == 0
                    ? leading + localizedLabel
                    : leading + localizedLabel + (translateValue ? TranslateKnownValueFast(value) : value);
                return true;
            }
        }

        private static string TranslateKnownValueFast(string value)
        {
            var normalized = NormalizeLoose(value);
            var core = TranslateKnownValueCore(normalized);
            if (core.Length != 0)
            {
                return core;
            }

            if (TryTranslateExact(value, out var exact) &&
                !string.Equals(exact, value, StringComparison.Ordinal))
            {
                return SanitizeTranslatedText(exact);
            }

            return value;
        }

        private static bool TryTranslateRiskLevelText(string text, out string translated)
        {
            translated = text;
            foreach (var label in new[]
                     {
                         "HAZARD LEVEL:",
                         "Hazard level:",
                         "RISK LEVEL:",
                         "Risk level:",
                         "\u5371\u9669\u7b49\u7ea7:",
                         "\u5371\u9669\u7b49\u7ea7\uff1a",
                         "\u98ce\u9669\u7ea7\u522b:",
                         "\u98ce\u9669\u7ea7\u522b\uff1a"
                     })
            {
                if (!text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = text[label.Length..].Trim();
                translated = value.Length == 0
                    ? "\u98ce\u9669\u7ea7\u522b\uff1a"
                    : "\u98ce\u9669\u7ea7\u522b\uff1a" + FormatRiskLevelValue(value);
                return true;
            }

            return TryFormatStandaloneRiskLevel(text, out translated);
        }

        private static bool TryFormatStandaloneRiskLevel(string text, out string translated)
        {
            translated = text;
            if (text.Length != 1)
            {
                return false;
            }

            translated = FormatRiskLevelValue(text);
            return !string.Equals(translated, text, StringComparison.Ordinal);
        }

        private static string FormatRiskLevelValue(string value)
        {
            var normalized = NormalizeLoose(value);
            if (string.Equals(normalized, "Safe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "\u5b89\u5168", StringComparison.Ordinal) ||
                string.Equals(normalized, "\u4fdd\u9669\u67dc", StringComparison.Ordinal))
            {
                return "\u5b89\u5168";
            }

            return normalized.ToUpperInvariant() switch
            {
                "A" => "\uff21",
                "B" => "\uff22",
                "C" => "\uff23",
                "D" => "\uff24",
                "S" => "\uff33",
                _ => value
            };
        }

        private static string TranslateKnownStandaloneValue(string value)
        {
            var normalized = NormalizeLoose(value);
            var core = TranslateKnownValueCore(normalized);
            return core.Length == 0 ? value : core;
        }

        private static string TranslateKnownValueCore(string normalized)
        {
            return normalized switch
            {
                "None" => "\u65e0",
                "Unknown" => "\u672a\u77e5",
                "Abandoned" => "\u5e9f\u5f03",
                "Safe" => "\u5b89\u5168",
                "\u4fdd\u9669\u67dc" => "\u5b89\u5168",
                "Diverse" => "\u591a\u6837",
                "Humid." => "\u6f6e\u6e7f\u3002",
                "Where the Company resides." => "\u516c\u53f8\u6240\u5728\u5730",
                "Where the Company resides" => "\u516c\u53f8\u6240\u5728\u5730",
                "Where the Company resices." => "\u516c\u53f8\u6240\u5728\u5730",
                "Where the Company resices" => "\u516c\u53f8\u6240\u5728\u5730",
                "Arid. Thick haze, worsened by industrial artifacts." => "\u5e72\u65f1\u3002\u6d53\u96fe\u56e0\u5de5\u4e1a\u5e9f\u5f03\u7269\u800c\u52a0\u91cd\u3002",
                "Arid. Low habitability, worsened by industrial artifacts." => "\u5e72\u65f1\u3002\u5b9c\u5c45\u6027\u4f4e\uff0c\u5e76\u56e0\u5de5\u4e1a\u5e9f\u5f03\u7269\u800c\u52a0\u91cd\u3002",
                "Humid. Rough terrain. Teeming with plant-life." => "\u6f6e\u6e7f\uff0c\u5730\u5f62\u5d0e\u5c96\uff0c\u690d\u7269\u7e41\u8302\u3002",
                "Expansive. Constant rain." => "\u5730\u5f62\u5f00\u9614\uff0c\u5e38\u5e74\u964d\u96e8\u3002",
                "No land masses. Continual storms." => "\u6ca1\u6709\u9646\u5730\uff0c\u98ce\u66b4\u6301\u7eed\u4e0d\u65ad\u3002",
                "Frozen, rocky. Its planet orbits a white dwarf star." => "\u51b0\u51b7\u591a\u5ca9\u3002\u5b83\u56f4\u7ed5\u4e00\u9897\u767d\u77ee\u661f\u8fd0\u884c\u3002",
                "Frozen, rocky. This moon was mined for resources. It's easy to get lost here." => "\u51b0\u51b7\u591a\u5ca9\u3002\u8fd9\u9897\u536b\u661f\u66fe\u88ab\u7528\u4e8e\u8d44\u6e90\u5f00\u91c7\uff0c\u5f88\u5bb9\u6613\u5728\u8fd9\u91cc\u8ff7\u8def\u3002",
                "Waning forests. Abandoned facilities littered across the landscape." => "\u8870\u8d25\u68ee\u6797\uff0c\u5e9f\u5f03\u8bbe\u65bd\u904d\u5e03\u5730\u8868\u3002",
                "Rumored active machinery left behind." => "\u4f20\u95fb\u6709\u6d3b\u8dc3\u673a\u68b0\u9057\u7559\u3002",
                "Dangerous entities have been rumored to take residence in the vast network of tunnels." => "\u636e\u4f20\u5371\u9669\u5b9e\u4f53\u6816\u606f\u5728\u5e9e\u5927\u7684\u96a7\u9053\u7f51\u7edc\u4e2d\u3002",
                "A competitive ecosystem supports aggressive lifeforms." => "\u7ade\u4e89\u6fc0\u70c8\u7684\u751f\u6001\u7cfb\u7edf\u652f\u6301\u653b\u51fb\u6027\u751f\u547d\u5f62\u6001\u5b58\u7eed\u3002",
                "A competitive and toughened ecosystem supports aggressive lifeforms. Travellers to 21-Offense should know it's not for the faint of heart." => "\u7ade\u4e89\u6fc0\u70c8\u4e14\u987d\u5f3a\u7684\u751f\u6001\u7cfb\u7edf\u5b55\u80b2\u4e86\u653b\u51fb\u6027\u751f\u547d\u3002\u524d\u5f80 21-Offense \u7684\u65c5\u884c\u8005\u5e94\u77e5\u9053\uff0c\u8fd9\u91cc\u5e76\u4e0d\u9002\u5408\u80c6\u5c0f\u8005\u3002",
                "It's highly unlikely for complex life to exist here." => "\u8fd9\u91cc\u6781\u4e0d\u53ef\u80fd\u5b58\u5728\u590d\u6742\u751f\u547d\u3002",
                "Unlikely for complex life to exist" => "\u4e0d\u592a\u53ef\u80fd\u5b58\u5728\u590d\u6742\u751f\u547d",
                "A landscape of deep valleys and mountains." => "\u7531\u6df1\u8c37\u4e0e\u7fa4\u5c71\u6784\u6210\u7684\u5730\u8c8c\u3002",
                "Home to a lively, diverse ecosystem of smaller-sized omnivores." => "\u62e5\u6709\u6d3b\u8dc3\u800c\u591a\u6837\u7684\u5c0f\u578b\u6742\u98df\u52a8\u7269\u751f\u6001\u7cfb\u7edf\u3002",
                "Previously mined for its rich industrial resources, Liquidation is now largely an ocean moon." => "Liquidation \u66fe\u56e0\u4e30\u5bcc\u7684\u5de5\u4e1a\u8d44\u6e90\u800c\u88ab\u5f00\u91c7\uff0c\u5982\u4eca\u5927\u591a\u88ab\u6d77\u6d0b\u8986\u76d6\u3002",
                "Desolate, made of amethyst." => "\u8352\u51c9\uff0c\u7531\u7d2b\u6c34\u6676\u6784\u6210\u3002",
                "Embrion is devoid of biological life." => "Embrion \u6ca1\u6709\u751f\u7269\u751f\u547d\u3002",
                "Dominated by a few species." => "\u7531\u5c11\u6570\u7269\u79cd\u4e3b\u5bfc\u3002",
                "Jagged and weathered terrain." => "\u5d0e\u5c96\u4e14\u98ce\u5316\u7684\u5730\u5f62\u3002",
                "Ecosystem supports territorial behaviour." => "\u751f\u6001\u7cfb\u7edf\u652f\u6301\u9886\u5730\u884c\u4e3a\u3002",
                "Ecosystem supports territorial behavior." => "\u751f\u6001\u7cfb\u7edf\u652f\u6301\u9886\u5730\u884c\u4e3a\u3002",
                _ => string.Empty
            };
        }
    }
}
