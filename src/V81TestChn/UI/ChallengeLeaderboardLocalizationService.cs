using System;
using System.Collections.Generic;
using TMPro;

namespace V81TestChn;

internal static class ChallengeLeaderboardLocalizationService
{
    private static readonly Dictionary<string, string> FilterValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Friends"] = "\u4ec5\u597d\u53cb",
        ["Rank"] = "\u6392\u540d",
        ["Similar rank"] = "\u76f8\u8fd1\u6392\u540d",
        ["Top 20"] = "\u524d 20 \u540d",
        ["Top20"] = "\u524d 20 \u540d"
    };

    public static void Apply(MenuManager? menu, string reason)
    {
        var root = menu?.leaderboardContainer;
        if (root == null)
        {
            return;
        }

        ExternalEnglishCompatibilityUiService.TranslateRoot(root, includeInactive: true, reason);
        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var changed = false;
            foreach (var option in dropdown.options)
            {
                if (option != null && TryTranslateFilterOption(option.text, out var localized))
                {
                    option.text = localized;
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            dropdown.RefreshShownValue();
            FontFallbackService.ApplyFallback(dropdown.captionText, dropdown.captionText?.text);
            FontFallbackService.ApplyFallback(dropdown.itemText, dropdown.itemText?.text);
        }
    }

    internal static bool TryTranslateFilterOption(string? source, out string localized)
    {
        localized = string.Empty;
        var value = source?.Trim() ?? string.Empty;
        if (value.StartsWith("Sort:", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring("Sort:".Length).Trim();
        }
        else if (value.StartsWith("\u6392\u5e8f:", StringComparison.Ordinal) ||
                 value.StartsWith("\u6392\u5e8f\uff1a", StringComparison.Ordinal))
        {
            value = value.Substring(3).Trim();
        }

        if (!FilterValues.TryGetValue(value, out var translatedValue))
        {
            return false;
        }

        localized = "\u6392\u5e8f\uff1a" + translatedValue;
        return true;
    }
}
