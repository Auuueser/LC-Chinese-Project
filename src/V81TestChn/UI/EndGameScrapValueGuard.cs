using System;
using UnityEngine;

namespace V81TestChn;

internal static class EndGameScrapValueGuard
{
    private static int _lastRepairLogFrame = -1;

    public static void EnsureSafeScrapDenominator(string reason, int observedScrapCollected = 0)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        EnsureSafeScrapDenominator(RoundManager.Instance, reason, observedScrapCollected);
    }

    public static void EnsureSafeScrapDenominator(RoundManager? round, string reason, int observedScrapCollected = 0)
    {
        if (round == null)
        {
            return;
        }

        var current = round.totalScrapValueInLevel;
        if (current > 0f && !float.IsNaN(current) && !float.IsInfinity(current))
        {
            return;
        }

        var repaired = Math.Max(1, Math.Max(round.scrapCollectedInLevel, observedScrapCollected));
        round.totalScrapValueInLevel = repaired;

        var frame = Time.frameCount;
        if (_lastRepairLogFrame == frame)
        {
            return;
        }

        _lastRepairLogFrame = frame;
        Plugin.Log.LogWarning(
            $"Repaired invalid endgame scrap denominator at {reason}: " +
            $"old={current}; repaired={repaired}; collected={round.scrapCollectedInLevel}; observed={observedScrapCollected}");
    }
}
