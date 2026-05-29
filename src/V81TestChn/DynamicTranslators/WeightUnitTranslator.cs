using System;
using System.Text;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class WeightUnitTranslator
    {
        public static bool CanHandleCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var stripped = StripRichTextTagsCheap(source).Trim();
            if (stripped.Equals("lb", StringComparison.OrdinalIgnoreCase) ||
                stripped.Equals("kg", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var unitLength = stripped.EndsWith("lb", StringComparison.OrdinalIgnoreCase) ||
                             stripped.EndsWith("kg", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0;
            if (unitLength == 0)
            {
                return false;
            }

            var number = stripped[..^unitLength].Trim();
            return LooksLikeSimpleNumber(number);
        }

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrEmpty(source) || !CanHandleCheap(source))
            {
                return false;
            }

            translated = Normalize(source);
            return !string.Equals(translated, source, StringComparison.Ordinal);
        }

        public static string Normalize(string? source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return source ?? string.Empty;
            }

            StringBuilder? builder = null;
            for (var i = 0; i < source.Length - 1; i++)
            {
                if (!TryGetUnitReplacement(source, i, out var replacement) ||
                    HasAsciiLetterBefore(source, i) ||
                    HasAsciiLetterAfter(source, i + 1))
                {
                    builder?.Append(source[i]);
                    continue;
                }

                builder ??= new StringBuilder(source.Length);
                if (builder.Length == 0 && i > 0)
                {
                    builder.Append(source, 0, i);
                }

                builder.Append(replacement);
                i++;
            }

            if (builder == null)
            {
                return source;
            }

            if (source.Length > 0)
            {
                var last = source[^1];
                if (source.Length < 2)
                {
                    builder.Append(last);
                }
                else if (!((source[^2] == 'l' || source[^2] == 'L') && (last == 'b' || last == 'B')) &&
                         !((source[^2] == 'k' || source[^2] == 'K') && (last == 'g' || last == 'G')))
                {
                    builder.Append(last);
                }
            }

            return builder.ToString();
        }

        private static bool TryGetUnitReplacement(string source, int index, out string replacement)
        {
            replacement = string.Empty;
            if (index + 1 >= source.Length)
            {
                return false;
            }

            var first = source[index];
            var second = source[index + 1];
            if ((first == 'l' || first == 'L') && (second == 'b' || second == 'B'))
            {
                replacement = "\u78c5";
                return true;
            }

            if ((first == 'k' || first == 'K') && (second == 'g' || second == 'G'))
            {
                replacement = "\u5343\u514b";
                return true;
            }

            return false;
        }

        private static bool LooksLikeSimpleNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var sawDigit = false;
            var sawDot = false;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if ((ch == '+' || ch == '-') && i == 0)
                {
                    continue;
                }

                if (ch == '.' && !sawDot)
                {
                    sawDot = true;
                    continue;
                }

                if (!char.IsDigit(ch))
                {
                    return false;
                }

                sawDigit = true;
            }

            return sawDigit;
        }
    }
}
