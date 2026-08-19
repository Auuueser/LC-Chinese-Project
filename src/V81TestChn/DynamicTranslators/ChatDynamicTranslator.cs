using System;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class ChatDynamicTranslator
    {
        private static readonly (string Suffix, string Replacement)[] SystemSuffixTranslations =
        {
            (" joined the ship.", " \u52a0\u5165\u4e86\u98de\u8239\u3002"),
            (" started the ship.", " \u542f\u52a8\u4e86\u98de\u8239\u3002"),
            (" disconnected.", " \u65ad\u5f00\u4e86\u8fde\u63a5\u3002"),
            (" was left behind.", " \u88ab\u629b\u4e0b\u4e86\u3002"),
            (" was kicked.", " \u88ab\u8e22\u51fa\u4e86\u3002"),
            (" died.", " \u6b7b\u4ea1\u4e86\u3002")
        };

        private static readonly (string Marker, string Replacement)[] ReasonTranslations =
        {
            (" was kicked for ", "\u88ab\u8e22\u51fa"),
            (" was banned for ", "\u88ab\u5c01\u7981"),
            (" was baned for ", "\u88ab\u5c01\u7981")
        };

        public static bool CanHandleCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var (marker, _) in ReasonTranslations)
            {
                if (source.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            foreach (var (suffix, _) in SystemSuffixTranslations)
            {
                if (RenderedTextEndsWith(source, suffix))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (TryTranslateReasonMessage(source, out translated))
            {
                return true;
            }

            foreach (var (suffix, replacement) in SystemSuffixTranslations)
            {
                if (!TryFindRenderedSuffixSpan(source, suffix, out var suffixStart, out var suffixEnd) ||
                    !HasRenderedTextBefore(source, suffixStart))
                {
                    continue;
                }

                translated = source[..suffixStart] + replacement + source[suffixEnd..];
                return true;
            }

            return false;
        }

        private static bool TryTranslateReasonMessage(string source, out string translated)
        {
            translated = source;
            GetOuterColorContentBounds(source, out var contentStart, out var contentEnd);
            var content = source.Substring(contentStart, contentEnd - contentStart);

            foreach (var (marker, replacement) in ReasonTranslations)
            {
                var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex <= 0)
                {
                    continue;
                }

                var playerName = content[..markerIndex].TrimEnd();
                var reason = content[(markerIndex + marker.Length)..].TrimStart();
                if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(reason))
                {
                    continue;
                }

                translated = source[..contentStart] +
                             playerName +
                             " \u56e0 " +
                             reason +
                             " " +
                             replacement +
                             source[contentEnd..];
                return true;
            }

            return false;
        }

        private static void GetOuterColorContentBounds(string source, out int contentStart, out int contentEnd)
        {
            contentStart = 0;
            contentEnd = source.Length;

            var first = 0;
            while (first < source.Length && char.IsWhiteSpace(source[first]))
            {
                first++;
            }

            if (first >= source.Length ||
                source.IndexOf("<color", first, StringComparison.OrdinalIgnoreCase) != first)
            {
                return;
            }

            var openingEnd = source.IndexOf('>', first);
            if (openingEnd < 0)
            {
                return;
            }

            var last = source.Length - 1;
            while (last >= 0 && char.IsWhiteSpace(source[last]))
            {
                last--;
            }

            const string closingTag = "</color>";
            var closingStart = last - closingTag.Length + 1;
            if (closingStart <= openingEnd ||
                string.Compare(source, closingStart, closingTag, 0, closingTag.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return;
            }

            contentStart = openingEnd + 1;
            contentEnd = closingStart;
        }

        private static bool TryFindRenderedSuffixSpan(string source, string suffix, out int suffixStart, out int suffixEnd)
        {
            suffixStart = 0;
            suffixEnd = 0;

            var sourceIndex = source.Length - 1;
            SkipTrailingRenderedWhitespace(source, ref sourceIndex);
            suffixEnd = sourceIndex + 1;

            for (var suffixIndex = suffix.Length - 1; suffixIndex >= 0; suffixIndex--)
            {
                if (!TryReadPreviousRenderedChar(source, ref sourceIndex, out var ch) ||
                    !AsciiEqualsIgnoreCase(ch, suffix[suffixIndex]))
                {
                    return false;
                }
            }

            suffixStart = sourceIndex + 1;
            return true;
        }

        private static bool HasRenderedTextBefore(string source, int endExclusive)
        {
            var sourceIndex = endExclusive - 1;
            while (TryReadPreviousRenderedChar(source, ref sourceIndex, out var ch))
            {
                if (!char.IsWhiteSpace(ch))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RenderedTextEndsWith(string source, string suffix)
        {
            var sourceIndex = source.Length - 1;
            SkipTrailingRenderedWhitespace(source, ref sourceIndex);

            for (var suffixIndex = suffix.Length - 1; suffixIndex >= 0; suffixIndex--)
            {
                if (!TryReadPreviousRenderedChar(source, ref sourceIndex, out var ch) ||
                    !AsciiEqualsIgnoreCase(ch, suffix[suffixIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SkipTrailingRenderedWhitespace(string source, ref int sourceIndex)
        {
            while (TryReadPreviousRenderedChar(source, ref sourceIndex, out var ch))
            {
                if (!char.IsWhiteSpace(ch))
                {
                    sourceIndex++;
                    return;
                }
            }
        }

        private static bool TryReadPreviousRenderedChar(string source, ref int sourceIndex, out char ch)
        {
            while (sourceIndex >= 0)
            {
                ch = source[sourceIndex--];
                if (ch != '>')
                {
                    return true;
                }

                var tagStart = sourceIndex;
                while (tagStart >= 0 && source[tagStart] != '<')
                {
                    tagStart--;
                }

                if (tagStart < 0)
                {
                    return true;
                }

                sourceIndex = tagStart - 1;
            }

            ch = default;
            return false;
        }

        private static bool AsciiEqualsIgnoreCase(char left, char right)
        {
            if (left == right)
            {
                return true;
            }

            if (left is >= 'A' and <= 'Z')
            {
                left = (char)(left + ('a' - 'A'));
            }

            if (right is >= 'A' and <= 'Z')
            {
                right = (char)(right + ('a' - 'A'));
            }

            return left == right;
        }
    }
}
