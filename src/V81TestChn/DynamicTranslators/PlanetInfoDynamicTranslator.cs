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

            foreach (var (englishLabel, chineseLabel, translateValue) in new[]
                     {
                         ("CELESTIAL BODY:", "\u5929\u4f53:", false),
                         ("CELESTIAL_BODY:", "\u5929\u4f53:", false),
                         ("\u5929\u4f53:", "\u5929\u4f53:", false),
                         ("\u5929\u4f53\uff1a", "\u5929\u4f53:", false),
                         ("POPULATION:", "\u4eba\u53e3:", true),
                         ("\u4eba\u53e3:", "\u4eba\u53e3:", true),
                         ("\u4eba\u53e3\uff1a", "\u4eba\u53e3:", true),
                         ("CONDITIONS:", "\u73af\u5883:", true),
                         ("\u73af\u5883:", "\u73af\u5883:", true),
                         ("\u73af\u5883\uff1a", "\u73af\u5883:", true),
                         ("FAUNA:", "\u751f\u6001:", true),
                         ("\u751f\u6001:", "\u751f\u6001:", true),
                         ("\u751f\u6001\uff1a", "\u751f\u6001:", true)
                     })
            {
                if (!trimmed.StartsWith(englishLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = trimmed[englishLabel.Length..].Trim();
                translated = value.Length == 0
                    ? chineseLabel
                    : $"{chineseLabel} {(translateValue ? TranslateKnownValue(value) : value)}";
                return true;
            }

            return false;
        }

        public static string TranslateKnownValue(string value)
        {
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

            var normalized = NormalizeLoose(value);
            var core = TranslateKnownValueCore(normalized);
            return core.Length == 0 ? value : core;
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

            var rewritten = source;
            var matched =
                TryRewrite("CELESTIAL BODY:", "\u5929\u4f53:", translateValue: false) ||
                TryRewrite("CELESTIAL_BODY:", "\u5929\u4f53:", translateValue: false) ||
                TryRewrite("\u5929\u4f53:", "\u5929\u4f53:", translateValue: false) ||
                TryRewrite("\u5929\u4f53\uff1a", "\u5929\u4f53:", translateValue: false) ||
                TryRewrite("POPULATION:", "\u4eba\u53e3:", translateValue: true) ||
                TryRewrite("\u4eba\u53e3:", "\u4eba\u53e3:", translateValue: true) ||
                TryRewrite("\u4eba\u53e3\uff1a", "\u4eba\u53e3:", translateValue: true) ||
                TryRewrite("CONDITIONS:", "\u73af\u5883:", translateValue: true) ||
                TryRewrite("\u73af\u5883:", "\u73af\u5883:", translateValue: true) ||
                TryRewrite("\u73af\u5883\uff1a", "\u73af\u5883:", translateValue: true) ||
                TryRewrite("FAUNA:", "\u751f\u6001:", translateValue: true) ||
                TryRewrite("\u751f\u6001:", "\u751f\u6001:", translateValue: true) ||
                TryRewrite("\u751f\u6001\uff1a", "\u751f\u6001:", translateValue: true);
            translated = rewritten;
            return matched;

            bool TryRewrite(string label, string localizedLabel, bool translateValue)
            {
                if (!trimmed.StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var value = trimmed[label.Length..].Trim();
                rewritten = value.Length == 0
                    ? leading + localizedLabel
                    : leading + localizedLabel + " " + (translateValue ? TranslateKnownValueFast(value) : value);
                return true;
            }
        }

        private static string TranslateKnownValueFast(string value)
        {
            if (TryTranslateExact(value, out var exact) &&
                !string.Equals(exact, value, StringComparison.Ordinal))
            {
                return SanitizeTranslatedText(exact);
            }

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
                "Arid. Thick haze, worsened by industrial artifacts." => "\u5e72\u65f1\u3002\u6d53\u96fe\u56e0\u5de5\u4e1a\u5e9f\u5f03\u7269\u800c\u52a0\u91cd\u3002",
                "Arid. Low habitability, worsened by industrial artifacts." => "\u5e72\u65f1\u3002\u5b9c\u5c45\u6027\u4f4e\uff0c\u5e76\u56e0\u5de5\u4e1a\u5e9f\u5f03\u7269\u800c\u52a0\u91cd\u3002",
                "Waning forests. Abandoned facilities littered across the landscape." => "\u8870\u8d25\u68ee\u6797\uff0c\u5e9f\u5f03\u8bbe\u65bd\u904d\u5e03\u5730\u8868\u3002",
                "Rumored active machinery left behind." => "\u4f20\u95fb\u6709\u6d3b\u8dc3\u673a\u68b0\u9057\u7559\u3002",
                "Dominated by a few species." => "\u7531\u5c11\u6570\u7269\u79cd\u4e3b\u5bfc\u3002",
                "Jagged and weathered terrain." => "\u5d0e\u5c96\u4e14\u98ce\u5316\u7684\u5730\u5f62\u3002",
                "Ecosystem supports territorial behaviour." => "\u751f\u6001\u7cfb\u7edf\u652f\u6301\u9886\u5730\u884c\u4e3a\u3002",
                "Ecosystem supports territorial behavior." => "\u751f\u6001\u7cfb\u7edf\u652f\u6301\u9886\u5730\u884c\u4e3a\u3002",
                _ => string.Empty
            };
        }
    }
}
