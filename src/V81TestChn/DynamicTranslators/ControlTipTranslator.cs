using System;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class ControlTipTranslator
    {
        public static bool CanHandleCheap(string? source) =>
            LooksLikeControlTipTextCheap(source) ||
            LooksLikeSuitChangePromptCheap(source);

        public static bool Translate(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (TranslateSuitChangePrompt(source, out translated))
            {
                return true;
            }

            var trimmed = source.Trim();
            var dropMatch = SafeRegexMatch(
                trimmed,
                @"^Drop\s+(?<item>.+?)\s*[:\uff1a]\s*(?<key>\[[^\]]+\])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dropMatch.Success)
            {
                var item = BuildTerminalLocalizedItemName(dropMatch.Groups["item"].Value.Trim());
                translated = $"\u4e22\u5f03 {item}\uff1a{dropMatch.Groups["key"].Value.Trim()}";
                return true;
            }

            var actionMatch = SafeRegexMatch(
                trimmed,
                @"^(?<action>.+?)\s*[:\uff1a]\s*(?<key>\[[^\]]+\])(?<suffix>.*)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!actionMatch.Success)
            {
                return false;
            }

            var action = NormalizeAction(actionMatch.Groups["action"].Value, out var actionImpliesHold);
            if (!ControlTipActionEntries.TryGetValue(action, out var localizedAction))
            {
                return false;
            }

            var key = actionMatch.Groups["key"].Value.Trim();
            var suffix = NormalizeSuffix(actionMatch.Groups["suffix"].Value, actionImpliesHold);
            translated = $"{localizedAction}\uff1a{key}{suffix}";
            return true;
        }

        private static bool TranslateSuitChangePrompt(string source, out string translated)
        {
            translated = source;
            var match = SafeRegexMatch(
                StripRichTextTags(source).Trim(),
                @"^(?:Change|\u66f4\u6362\u670d\u88c5)\s*[:\uff1a]\s*(?<suit>.+?)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            var suit = match.Groups["suit"].Value.Trim();
            if (suit.Length == 0)
            {
                return false;
            }

            translated = $"\u66f4\u6362\u670d\u88c5\uff1a{BuildTerminalLocalizedItemName(suit)}";
            return true;
        }

        public static bool TranslateStandalone(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var normalized = NormalizeAction(StripRichTextTags(source), out var actionImpliesHold);
            if (!ControlTipActionEntries.TryGetValue(normalized, out translated))
            {
                return false;
            }

            if (actionImpliesHold)
            {
                translated += "\uff08\u957f\u6309\uff09";
            }

            return true;
        }

        public static bool TranslateStandaloneFast(string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var action = StripRichTextTagsCheap(source).Trim();
            if (action.Length == 0)
            {
                return false;
            }

            var actionImpliesHold = false;
            if (action.EndsWith(" hold", StringComparison.OrdinalIgnoreCase))
            {
                actionImpliesHold = true;
                action = action[..^" hold".Length].TrimEnd();
            }

            if (!ControlTipActionEntries.TryGetValue(action, out translated))
            {
                return false;
            }

            if (actionImpliesHold)
            {
                translated += "\uff08\u957f\u6309\uff09";
            }

            return true;
        }

        private static bool LooksLikeSuitChangePromptCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var text = StripRichTextTagsCheap(source).TrimStart();
            return text.StartsWith("Change:", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("Change\uff1a", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("\u66f4\u6362\u670d\u88c5:", StringComparison.Ordinal) ||
                   text.StartsWith("\u66f4\u6362\u670d\u88c5\uff1a", StringComparison.Ordinal);
        }

        private static string NormalizeAction(string action, out bool impliesHold)
        {
            impliesHold = false;
            var normalized = SafeRegexReplace(action, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            if (normalized.EndsWith(" hold", StringComparison.OrdinalIgnoreCase))
            {
                impliesHold = true;
                normalized = normalized[..^" hold".Length].TrimEnd();
            }

            return normalized;
        }

        private static string NormalizeSuffix(string suffix, bool actionImpliesHold)
        {
            var normalized = SafeRegexReplace(suffix ?? string.Empty, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            if (actionImpliesHold ||
                normalized.Contains("Hold", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains("\u957f\u6309", StringComparison.Ordinal))
            {
                return "\uff08\u957f\u6309\uff09";
            }

            return normalized.Length == 0 ? string.Empty : $" {normalized}";
        }
    }
}
