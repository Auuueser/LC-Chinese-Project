using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class TranslationGuard
{
    private const int MaxGlobalSetterTextLength = 1024;
    private static readonly HashSet<int> LoggedSkipComponents = new();
    private static ConfigEntry<bool>? _logTranslationGuardSkips;

    private static readonly string[] ExcludedNameTokens =
    {
        "RuntimeIcons",
        "HoneeItemIcons",
        "BetterRotations"
    };

    public static void Initialize(ConfigFile config)
    {
        _logTranslationGuardSkips = config.Bind(
            "Diagnostics",
            "LogTranslationGuardSkips",
            false,
            "Log one TranslationGuard skip per component with path, reason, and text summary.");
    }

    public static void Clear()
    {
        LoggedSkipComponents.Clear();
        _logTranslationGuardSkips = null;
    }

    public static bool ShouldTranslateGlobalText(TMP_Text? text, string? value)
    {
        return ShouldTranslateComponent(text, value);
    }

    public static bool ShouldTranslateGlobalText(Text? text, string? value)
    {
        return ShouldTranslateComponent(text, value);
    }

    public static bool ShouldTranslateGlobalText(TextMesh? text, string? value)
    {
        return ShouldTranslateComponent(text, value);
    }

    public static bool ShouldTouchGlobalTextStyle(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (TryGetComponentSkipReason(text, text.text, out var reason))
        {
            LogSkipOnce(text, text.text, reason);
            return false;
        }

        return true;
    }

    private static bool ShouldTranslateComponent(Component? component, string? value)
    {
        if (component == null)
        {
            return false;
        }

        if (TryGetSkipReason(component, value, out var reason))
        {
            LogSkipOnce(component, value, reason);
            return false;
        }

        return true;
    }

    public static bool TryGetSkipReason(Component component, string? value, out string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "empty";
            return true;
        }

        if (value.Length > MaxGlobalSetterTextLength)
        {
            reason = "too long";
            return true;
        }

        if (TryGetComponentSkipReason(component, value, out reason))
        {
            return true;
        }

        if (MaybeKnownDynamicTextCheap(value))
        {
            return false;
        }

        var hasAsciiLetter = false;
        var asciiLetterCount = 0;
        var digitCount = 0;
        foreach (var ch in value)
        {
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                hasAsciiLetter = true;
                asciiLetterCount++;
                continue;
            }

            if (char.IsDigit(ch))
            {
                digitCount++;
            }
        }

        if (!hasAsciiLetter)
        {
            reason = digitCount > 0 ? "pure dynamic value" : "no ascii letters";
            return true;
        }

        if (digitCount > 0 && asciiLetterCount <= 2)
        {
            reason = "pure dynamic value";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetComponentSkipReason(Component component, string? value, out string reason)
    {
        if (IsChatInput(component))
        {
            reason = "Chat input";
            return true;
        }

        if (IsTerminalInput(component))
        {
            reason = "Terminal input";
            return true;
        }

        if (component.GetComponentInParent<TMP_InputField>(true) != null ||
            component.GetComponentInParent<InputField>(true) != null)
        {
            reason = "InputField";
            return true;
        }

        if (IsLobbyDynamicText(component))
        {
            reason = "LobbySlot dynamic text";
            return true;
        }

        // Chat output is deliberately allowed; only the input field is protected above.
        if (IsChatOutput(component))
        {
            reason = string.Empty;
            return false;
        }

        if (IsPlayerNameOnly(component, value))
        {
            reason = "player name";
            return true;
        }

        if (TryGetExcludedNameToken(component.transform, out var token))
        {
            reason = $"name token: {token}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetExcludedNameToken(Transform? transform, out string matchedToken)
    {
        var current = transform;
        while (current != null)
        {
            foreach (var token in ExcludedNameTokens)
            {
                if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedToken = token;
                    return true;
                }
            }

            current = current.parent;
        }

        matchedToken = string.Empty;
        return false;
    }

    private static bool IsChatInput(Component component)
    {
        if (component.GetComponentInParent<TMP_InputField>(true) == null &&
            component.GetComponentInParent<InputField>(true) == null)
        {
            return false;
        }

        return TransformPathContains(component.transform, "Chat") ||
               TransformPathContains(component.transform, "ChatInput");
    }

    private static bool IsChatOutput(Component component)
    {
        return TransformPathContains(component.transform, "Chat") && !IsChatInput(component);
    }

    private static bool IsTerminalInput(Component component)
    {
        return TransformPathContains(component.transform, "CommandInput") ||
               TransformPathContains(component.transform, "TerminalInput") ||
               TransformPathContains(component.transform, "terminalTextField");
    }

    private static bool IsPlayerNameOnly(Component component, string? value)
    {
        if (!TransformPathContains(component.transform, "PlayerName") &&
            !TransformPathContains(component.transform, "Player Name") &&
            !TransformPathContains(component.transform, "Username") &&
            !TransformPathContains(component.transform, "UserName") &&
            !TransformPathContains(component.transform, "SteamName"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (LooksLikePlayerStatusTextCheap(trimmed))
        {
            return false;
        }

        return trimmed.Length <= 64 && trimmed.IndexOf('\n') < 0 && trimmed.IndexOf('\r') < 0;
    }

    private static bool IsLobbyDynamicText(Component component)
    {
        var slot = component.GetComponentInParent<LobbySlot>(true);
        if (slot == null)
        {
            return false;
        }

        if (component is TMP_Text tmp)
        {
            try
            {
                if (ReferenceEquals(slot.LobbyName, tmp) || ReferenceEquals(slot.playerCount, tmp))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        return TransformPathContains(component.transform, "LobbyName") ||
               TransformPathContains(component.transform, "playerCount");
    }

    private static bool IsWeightUnitText(string? value)
    {
        return TranslationService.LooksLikeWeightUnitTextCheap(value);
    }

    private static bool IsVoteText(string? value)
    {
        return TranslationService.LooksLikeVoteTextCheap(value);
    }

    private static bool IsDaysLeftText(string? value)
    {
        return TranslationService.LooksLikeDaysLeftTextCheap(value);
    }

    private static bool IsRandomSeedText(string? value)
    {
        return TranslationService.LooksLikeRandomSeedTextCheap(value);
    }

    private static bool IsEndgameStatText(string? value)
    {
        return TranslationService.LooksLikeEndgameStatTextCheap(value);
    }

    private static bool IsControlTipText(string? value)
    {
        return TranslationService.LooksLikeControlTipTextCheap(value);
    }

    public static bool MaybeKnownDynamicTextCheap(string? value)
    {
        // A weight unit like "0 lb" is dynamic text with a translatable unit, not a pure number.
        return IsWeightUnitText(value) ||
               IsVoteText(value) ||
               IsDaysLeftText(value) ||
               IsRandomSeedText(value) ||
               IsEndgameStatText(value) ||
               IsControlTipText(value) ||
               IsClockText(value) ||
               IsShipMonitorText(value) ||
               IsPlanetInfoText(value) ||
               IsHostModWarningText(value) ||
               IsChatSystemMessageCheap(value);
    }

    private static bool IsHostModWarningText(string? value)
    {
        return TranslationService.LooksLikeHostModWarningTextCheap(value);
    }

    private static bool IsChatSystemMessageCheap(string? value)
    {
        return TranslationService.LooksLikeChatSystemMessageCheap(value);
    }

    private static bool IsClockText(string? value)
    {
        return TranslationService.LooksLikeClockTextCheap(value);
    }

    private static bool IsShipMonitorText(string? value)
    {
        return TranslationService.LooksLikeShipMonitorTextCheap(value);
    }

    private static bool IsPlanetInfoText(string? value)
    {
        return TranslationService.LooksLikePlanetInfoTextCheap(value);
    }

    private static bool LooksLikePlayerStatusTextCheap(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Equals("(Dead)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Dead", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Deceased", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("(Deceased)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("Missing", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("(Missing)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("(Dead)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(" Dead", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("(Deceased)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(" Deceased", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("(Missing)", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(" Missing", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogSkipOnce(Component component, string? value, string reason)
    {
        if (_logTranslationGuardSkips?.Value != true)
        {
            return;
        }

        try
        {
            if (!LoggedSkipComponents.Add(component.GetInstanceID()))
            {
                return;
            }

            Plugin.Log.LogInfo(
                $"TranslationGuard skipped {component.GetType().Name} at '{BuildPath(component.transform)}': reason={reason}, text='{TrimLogText(value)}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"TranslationGuard skip logging failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildPath(Transform? transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var parts = new List<string>();
        var current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool TransformPathContains(Transform? transform, string token)
    {
        var current = transform;
        while (current != null)
        {
            if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string TrimLogText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return normalized.Length <= 120 ? normalized : normalized.Substring(0, 120) + "...";
    }
}
