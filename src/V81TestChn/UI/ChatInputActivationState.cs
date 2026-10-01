namespace V81TestChn;

// A successful game chat-open action owns a short activation request. UI mods
// can rebuild/reparent the field between Select and TMP's deferred LateUpdate.
// Never turn that request into a permanent focus watchdog.
internal sealed class ChatInputActivationState
{
    private const int RecoveryFrames = 2;
    private bool _pending;
    private int _openedFrame;
    private int _lastAttemptFrame = -1;

    internal bool Pending => _pending;

    internal void Begin(int frame)
    {
        _pending = true;
        _openedFrame = frame;
        _lastAttemptFrame = -1;
    }

    internal void Reset() => _pending = false;

    internal bool ShouldActivate(int frame, bool typing, bool available,
        bool otherSelected, bool canceled, bool focused, bool activationQueued)
    {
        if (!_pending) return false;
        if (!typing || !available || otherSelected || canceled ||
            frame < _openedFrame || frame - _openedFrame > RecoveryFrames)
        {
            Reset();
            return false;
        }

        if (focused || activationQueued || frame == _lastAttemptFrame) return false;
        _lastAttemptFrame = frame;
        return true;
    }
}
