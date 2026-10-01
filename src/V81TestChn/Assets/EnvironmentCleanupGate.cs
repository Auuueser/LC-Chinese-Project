namespace V81TestChn;

// Only coalesces event-driven cleanup; it never schedules work or delays localization.
// Record completion after the sweep succeeds, so an interrupted sweep can be retried.
internal struct EnvironmentCleanupGate
{
    private bool _hasCompletedFrame;
    private int _completedFrame;

    internal bool NeedsCleanup(int frame) => !_hasCompletedFrame || _completedFrame != frame;

    internal void Complete(int frame)
    {
        _completedFrame = frame;
        _hasCompletedFrame = true;
    }

    internal void Reset()
    {
        _hasCompletedFrame = false;
        _completedFrame = 0;
    }
}
