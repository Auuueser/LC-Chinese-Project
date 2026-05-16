using System;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class ChatDynamicTranslator
    {
        public static bool CanHandleCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var text = StripRichTextTagsCheap(source).Trim();
            return text.EndsWith(" joined the ship.", StringComparison.OrdinalIgnoreCase) ||
                   text.EndsWith(" started the ship.", StringComparison.OrdinalIgnoreCase) ||
                   text.EndsWith(" disconnected.", StringComparison.OrdinalIgnoreCase) ||
                   text.EndsWith(" was left behind.", StringComparison.OrdinalIgnoreCase) ||
                   text.EndsWith(" was kicked.", StringComparison.OrdinalIgnoreCase) ||
                   text.EndsWith(" died.", StringComparison.OrdinalIgnoreCase);
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

            foreach (var (suffix, replacement) in new[]
                     {
                         (" joined the ship.", " \u52a0\u5165\u4e86\u98de\u8239\u3002"),
                         (" started the ship.", " \u542f\u52a8\u4e86\u98de\u8239\u3002"),
                         (" disconnected.", " \u65ad\u5f00\u4e86\u8fde\u63a5\u3002"),
                         (" was left behind.", " \u88ab\u629b\u4e0b\u4e86\u3002"),
                         (" was kicked.", " \u88ab\u8e22\u51fa\u4e86\u3002"),
                         (" died.", " \u6b7b\u4ea1\u4e86\u3002")
                     })
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
    }
}
