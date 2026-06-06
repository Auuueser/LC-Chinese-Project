namespace V81TestChn;

internal static class RoundTransitionTextThrottle
{
    private static int _setShipReadyToLandDepth;

    public static bool ShouldDeferHudChatOutput()
    {
        return _setShipReadyToLandDepth > 0;
    }

    public static void EnterSetShipReadyToLand(StartOfRound? round)
    {
        if (round == null || Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        _setShipReadyToLandDepth++;
    }

    public static void ExitSetShipReadyToLand()
    {
        if (_setShipReadyToLandDepth > 0)
        {
            _setShipReadyToLandDepth--;
        }
    }

    public static void Reset()
    {
        _setShipReadyToLandDepth = 0;
    }
}
