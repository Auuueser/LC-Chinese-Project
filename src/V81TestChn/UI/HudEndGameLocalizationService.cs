using System;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudEndGameLocalizationService
{
    private const string PilotComputerLocalizedText = "\u5bfc\u822a\u7535\u8111";

    public static void ApplyPlayersFiredAfterDeadline(string reason)
    {
        ApplyPlayersFiredScreen(HUDManager.Instance, reason);
    }

    public static void ApplyNewDeadline(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        var root = hud.reachedProfitQuotaAnimator == null ? null : hud.reachedProfitQuotaAnimator.gameObject;
        TargetedUiTranslator.TranslateRoot(root, reason);
        DirectTextLocalizationService.ApplyComposite(hud.reachedProfitQuotaBonusText, reason);
        ApplyVoteAndDeadlineText(hud, reason);
    }

    public static void ApplyVoteAndDeadlineText(HUDManager? hud, string reason)
    {
        TargetedUiTranslator.TranslateHudVoteAndDeadlineText(hud, reason);
    }

    public static void ApplyDialogueSegments(DialogueSegment[]? dialogueArray, string reason)
    {
        if (dialogueArray == null)
        {
            return;
        }

        foreach (var segment in dialogueArray)
        {
            if (segment == null)
            {
                continue;
            }

            TryTranslateDialogueSegmentText(ref segment.speakerText);
            TryTranslateDialogueSegmentText(ref segment.bodyText);
        }
    }

    public static void ApplyDialogueHud(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        TryTranslateDialogueText(hud.dialogeBoxHeaderText, reason);
        TryTranslateDialogueText(hud.dialogeBoxText, reason);
        NormalizePilotComputerHeader(hud, reason);
    }

    public static void TryNormalizeDialogueBoxText(TMP_Text? text, string reason)
    {
        var hud = HUDManager.Instance;
        if (hud == null ||
            text == null ||
            (!ReferenceEquals(text, hud.dialogeBoxHeaderText) && !ReferenceEquals(text, hud.dialogeBoxText)))
        {
            return;
        }

        TryTranslateDialogueText(text, reason);
        NormalizePilotComputerHeader(hud, reason);
    }

    private static void TryTranslateDialogueSegmentText(ref string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if ((TranslationService.TryTranslate(value, out var translated) ||
             TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.EndGame, value, out translated)) &&
            !string.Equals(translated, value, StringComparison.Ordinal))
        {
            value = translated;
        }
    }

    private static void TryTranslateDialogueText(TMP_Text? text, string reason)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        if ((TranslationService.TryTranslate(text.text, out var translated) ||
             TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.EndGame, text.text, out translated)) &&
            !string.Equals(translated, text.text, StringComparison.Ordinal))
        {
            text.text = translated;
            FontFallbackService.ApplyFallback(text, translated);
            Plugin.ReportTranslationHit();
            Plugin.Log.LogInfo($"NativeRelay[{reason}] target=DialogueBox action=applied name={text.name} text={translated}");
        }
    }

    private static void NormalizePilotComputerHeader(HUDManager hud, string reason)
    {
        var header = hud.dialogeBoxHeaderText;
        var body = hud.dialogeBoxText?.text;
        if (header == null || !LooksLikePilotComputerBody(body))
        {
            return;
        }

        var current = header.text?.Trim();
        if (!string.IsNullOrWhiteSpace(current) &&
            !string.Equals(current, "PILOT COMPUTER", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(current, PilotComputerLocalizedText, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(header.text, PilotComputerLocalizedText, StringComparison.Ordinal))
        {
            header.text = PilotComputerLocalizedText;
        }

        header.gameObject.SetActive(true);
        header.enabled = true;
        header.enableWordWrapping = false;
        header.overflowMode = TextOverflowModes.Overflow;
        FontFallbackService.ApplyFallback(header, PilotComputerLocalizedText);
        Plugin.Log.LogInfo($"NativeRelay[{reason}] target=DialogueBoxHeader action=pilot-computer text={header.text}");
    }

    private static bool LooksLikePilotComputerBody(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        return (text.IndexOf("No response from crew", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("Emergency code", StringComparison.OrdinalIgnoreCase) >= 0) ||
               (text.IndexOf("\u672a\u6536\u5230\u8fd4\u822a\u8239\u5458\u56de\u5e94", StringComparison.Ordinal) >= 0 &&
                text.IndexOf("\u7d27\u6025\u4ee3\u7801", StringComparison.Ordinal) >= 0) ||
               (text.IndexOf("closest safe spaceport", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("items", StringComparison.OrdinalIgnoreCase) >= 0) ||
               (text.IndexOf("\u6700\u8fd1\u7684\u5b89\u5168\u592a\u7a7a\u6e2f", StringComparison.Ordinal) >= 0 &&
                text.IndexOf("\u7269\u54c1\u5df2\u4e22\u5931", StringComparison.Ordinal) >= 0) ||
               (text.IndexOf("vote", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("autopilot", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("leave early", StringComparison.OrdinalIgnoreCase) >= 0) ||
               (text.IndexOf("\u6295\u7968", StringComparison.Ordinal) >= 0 &&
                text.IndexOf("\u81ea\u52a8\u9a7e\u9a76", StringComparison.Ordinal) >= 0 &&
                (text.IndexOf("\u63d0\u524d\u79bb\u5f00", StringComparison.Ordinal) >= 0 ||
                 text.IndexOf("\u63d0\u65e9\u79bb\u5f00", StringComparison.Ordinal) >= 0));
    }

    public static bool TranslateShipLeaveEarlyWarning(string? source, out string translated)
    {
        return TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.EndGame, source, out translated);
    }

    public static void ApplySpectateUi(HUDManager? hud, string reason)
    {
        EndGameLocalizationService.ApplySpectateUiLocalization(hud, reason);
    }

    public static void ApplyHudEndGame(HUDManager? hud, string reason)
    {
        EndGameLocalizationService.ApplyHudEndGameLocalization(hud, reason);
    }

    public static void ApplyPlayersFiredScreen(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateHudPlayersFiredScreen(hud, reason);
        EndGameLocalizationService.ApplyPlayersFiredStatsLocalization(hud, reason);
    }

    public static void TryNormalizePlayersFiredText(TMP_Text? text, string reason)
    {
        EndGameLocalizationService.TryNormalizePlayersFiredText(text, reason);
    }

    public static void ApplyChallengeSlot(ChallengeLeaderboardSlot? slot, string reason)
    {
        EndGameLocalizationService.ApplyChallengeSlotLocalization(slot, reason);
    }

    public static bool TryTranslateEndOfRunStatsText(TMP_Text text, ref string value, string reason)
    {
        if (!ReferenceEquals(text, HUDManager.Instance?.EndOfRunStatsText))
        {
            return false;
        }

        FontFallbackService.ApplyFallback(text, value);
        if (TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.EndGame, value, out var translated) &&
            !string.Equals(translated, value, StringComparison.Ordinal))
        {
            value = translated;
            FontFallbackService.ApplyFallback(text, translated);
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(text, value);
        }

        return true;
    }

    public static void TryRewriteSpectateDeadValue(TMP_Text? text, ref string value, string reason)
    {
        EndGameLocalizationService.TryRewriteSpectateDeadValue(text, ref value, reason);
    }

    public static void TryLocalizeSpectateDeadLabel(TMP_Text? text, string reason)
    {
        EndGameLocalizationService.TryLocalizeSpectateDeadLabel(text, reason);
    }

}
