using System;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    public static string TranslateHeldItemControlTip(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source ?? string.Empty;
        }

        var normalized = NormalizeHeldItemControlTipBinding(source);
        if (ControlTipTranslator.Translate(normalized, out var translated))
        {
            return translated;
        }

        return normalized;
    }

    private static string NormalizeHeldItemControlTipBinding(string source)
    {
        var normalized = SafeRegexReplace(
            source,
            @"\[\s*\[?\s*(?:RMB|\u9f20\u6807\u53f3\u952e|\u53f3\u952e)\s*\]",
            "[LMB]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return normalized;
    }

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
            if (TryTranslateControlTipLines(trimmed, out translated))
            {
                return true;
            }

            var cooldownMatch = SafeRegexMatch(
                trimmed,
                @"^\[\s*Cooldown\s*[:\uff1a]\s*(?<seconds>\d+)\s*sec\.?\s*\]$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (cooldownMatch.Success)
            {
                translated = $"[\u51b7\u5374\uff1a{cooldownMatch.Groups["seconds"].Value} \u79d2]";
                return true;
            }

            var lockPickingMatch = SafeRegexMatch(
                trimmed,
                @"^Picking\s+lock\s*:\s*(?<seconds>\d+(?:\.\d+)?)\s+sec(?:ond)?s?\.?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (lockPickingMatch.Success)
            {
                translated = $"\u6b63\u5728\u5f00\u9501\uff1a{lockPickingMatch.Groups["seconds"].Value} \u79d2";
                return true;
            }

            if (TryTranslateBracketedControlStatus(trimmed, out translated))
            {
                return true;
            }

            if (TryTranslateKeyFirstControlTip(trimmed, out translated))
            {
                return true;
            }

            if (TryTranslateControlTipSegments(trimmed, out translated))
            {
                return true;
            }

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

            var throwMatch = SafeRegexMatch(
                trimmed,
                @"^Throw\s+(?<item>.+?)\s*[:\uff1a]\s*(?<key>\[[^\]]+\])$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (throwMatch.Success)
            {
                var item = BuildTerminalLocalizedItemName(throwMatch.Groups["item"].Value.Trim());
                translated = $"\u6254\u51fa {item}\uff1a{throwMatch.Groups["key"].Value.Trim()}";
                return true;
            }

            var actionMatch = SafeRegexMatch(
                trimmed,
                @"^(?<action>.+?)\s*[:\uff1a]\s*(?<key>\[[^\]]+\])(?<suffix>.*)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!actionMatch.Success)
            {
                return TranslateLooseControlAction(trimmed, out translated);
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

        private static bool TryTranslateControlTipSegments(string trimmed, out string translated)
        {
            translated = trimmed;
            if (trimmed.IndexOf('|') < 0)
            {
                return false;
            }

            var parts = trimmed.Split('|');
            var changed = false;
            for (var i = 0; i < parts.Length; i++)
            {
                var segment = parts[i].Trim();
                if (segment.Length == 0 || segment.IndexOf('|') >= 0)
                {
                    continue;
                }

                if (!Translate(segment, out var translatedSegment) ||
                    string.Equals(segment, translatedSegment, StringComparison.Ordinal))
                {
                    continue;
                }

                parts[i] = translatedSegment;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            translated = string.Join("   |   ", parts);
            return true;
        }

        private static bool TryTranslateControlTipLines(string trimmed, out string translated)
        {
            translated = trimmed;
            if (trimmed.IndexOf('\n') < 0)
            {
                return false;
            }

            var lines = trimmed.Split('\n');
            var changed = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (!Translate(line, out var translatedLine) ||
                    string.Equals(line, translatedLine, StringComparison.Ordinal))
                {
                    continue;
                }

                lines[i] = translatedLine;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            translated = string.Join("\n", lines);
            return true;
        }

        private static bool TryTranslateBracketedControlStatus(string trimmed, out string translated)
        {
            translated = trimmed;
            if (trimmed.Length < 3 || trimmed[0] != '[' || trimmed[^1] != ']')
            {
                return false;
            }

            var body = trimmed[1..^1].Trim();
            if (!ControlTipActionEntries.TryGetValue(body, out var localized))
            {
                return false;
            }

            translated = $"[{localized}]";
            return true;
        }

        private static bool TryTranslateKeyFirstControlTip(string trimmed, out string translated)
        {
            translated = trimmed;
            var match = SafeRegexMatch(
                trimmed,
                @"^(?<key>\[[^\]]+\])\s+(?<action>[^:\uff1a]+?)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            var action = NormalizeAction(match.Groups["action"].Value, out var actionImpliesHold);
            if (!ControlTipActionEntries.TryGetValue(action, out var localizedAction))
            {
                return false;
            }

            translated = match.Groups["key"].Value.Trim() + " " + localizedAction + (actionImpliesHold ? "\uff08\u957f\u6309\uff09" : string.Empty);
            return true;
        }

        private static bool TranslateLooseControlAction(string trimmed, out string translated)
        {
            translated = trimmed;
            var actionMatch = SafeRegexMatch(
                trimmed,
                @"^(?<action>[^:\uff1a]+)\s*[:\uff1a]\s*(?<value>.+?)$",
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

            var value = NormalizeLooseControlValue(actionMatch.Groups["value"].Value, actionImpliesHold);
            if (value.Length == 0)
            {
                return false;
            }

            translated = $"{localizedAction}\uff1a{value}";
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

        private static string NormalizeLooseControlValue(string value, bool actionImpliesHold)
        {
            var normalized = SafeRegexReplace(value ?? string.Empty, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            if (normalized.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase))
            {
                return "\u6309\u4f4f " + normalized["Hold ".Length..].Trim();
            }

            if (normalized.StartsWith("Toggle ", StringComparison.OrdinalIgnoreCase))
            {
                return "\u5207\u6362 " + normalized["Toggle ".Length..].Trim();
            }

            return actionImpliesHold ? "\u6309\u4f4f " + normalized : normalized;
        }
    }
}
