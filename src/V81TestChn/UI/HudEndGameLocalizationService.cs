using System;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudEndGameLocalizationService
{
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

            if (TranslationService.TryTranslateKnownDynamicTextTargeted(
                    DynamicTextDomain.EndGame,
                    segment.bodyText,
                    out var translated) &&
                !string.Equals(translated, segment.bodyText, StringComparison.Ordinal))
            {
                segment.bodyText = translated;
            }
        }
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

        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, value);
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
