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

        public static bool CanHandleCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
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

            var trimmed = source.Trim();
            if (TryUnwrapColorTag(trimmed, out var openTag, out var body, out var closeTag) &&
                Translate(body, out var bodyTranslation))
            {
                translated = openTag + bodyTranslation + closeTag;
                return true;
            }

            foreach (var (suffix, replacement) in SystemSuffixTranslations)
            {
                if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || trimmed.Length <= suffix.Length)
                {
                    continue;
                }

                translated = trimmed[..^suffix.Length] + replacement;
                return true;
            }

            return false;
        }

        private static bool TryUnwrapColorTag(string source, out string openTag, out string body, out string closeTag)
        {
            openTag = string.Empty;
            body = source;
            closeTag = string.Empty;

            const string close = "</color>";
            if (!source.StartsWith("<color=", StringComparison.OrdinalIgnoreCase) ||
                !source.EndsWith(close, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var openEnd = source.IndexOf('>');
            if (openEnd <= 0)
            {
                return false;
            }

            openTag = source[..(openEnd + 1)];
            closeTag = source.Substring(source.Length - close.Length, close.Length);
            body = source.Substring(openEnd + 1, source.Length - openEnd - 1 - close.Length);
            return true;
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
