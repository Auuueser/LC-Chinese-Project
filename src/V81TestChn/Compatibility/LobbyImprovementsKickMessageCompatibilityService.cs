using System;
using System.Reflection;
using HarmonyLib;

namespace V81TestChn;

internal static class LobbyImprovementsKickMessageCompatibilityService
{
    private const string BannedMarker = " was banned for ";
    private const string KickedMarker = " was kicked for ";
    private const string KickReasonPrefix = "<size=12><color=red>Kicked From Lobby:<color=white>\n";
    private static FieldInfo? _kickReasonField;
    private static bool _fieldLookupAttempted;

    public static void CorrectMisclassifiedKickMessage(ref string message)
    {
        if (string.IsNullOrEmpty(message) ||
            message.IndexOf(BannedMarker, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        var reason = GetCurrentKickReason();
        message = CorrectMisclassifiedKickMessage(message, reason);
    }

    internal static string CorrectMisclassifiedKickMessage(string message, string? currentReason)
    {
        if (string.IsNullOrEmpty(message) ||
            string.IsNullOrEmpty(currentReason) ||
            !currentReason.StartsWith(KickReasonPrefix, StringComparison.Ordinal) ||
            message.IndexOf(BannedMarker, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return message;
        }

        var markerIndex = message.IndexOf(BannedMarker, StringComparison.OrdinalIgnoreCase);
        return message.Substring(0, markerIndex) + KickedMarker + message.Substring(markerIndex + BannedMarker.Length);
    }

    private static string? GetCurrentKickReason()
    {
        if (!_fieldLookupAttempted)
        {
            _fieldLookupAttempted = true;
            var generalPatches = AccessTools.TypeByName("LobbyImprovements.General_Patches");
            _kickReasonField = generalPatches == null ? null : AccessTools.Field(generalPatches, "kickReason");
        }

        return _kickReasonField?.GetValue(null) as string;
    }
}
