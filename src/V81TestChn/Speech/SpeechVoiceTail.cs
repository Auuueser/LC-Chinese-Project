namespace V81TestChn;

// VAD callbacks can run on the preprocessing thread. This tracks only voice
// transitions; stopped recordings keep no PCM subscription or growing buffer.
internal sealed class SpeechVoiceTail
{
    internal const double SilenceSeconds = 0.35;
    internal const double ManualMuteSeconds = 1;
    private readonly object _gate = new();
    private bool _observing, _speaking, _holding, _manual;
    private double _quietSince, _manualDeadline;

    internal void Begin(double now)
    {
        lock (_gate)
        {
            _observing = true;
            _speaking = _holding = _manual = false;
            _quietSince = now;
        }
    }

    internal void Voice(bool speaking, double now)
    {
        lock (_gate)
        {
            if (!_observing) return;
            if (speaking || _speaking) _quietSince = now;
            _speaking = speaking;
        }
    }

    internal void Hold(double now, bool manual = false)
    {
        lock (_gate)
        {
            _holding = _observing;
            _manual = manual;
            // Manual stop uses wall time, not a quiet interval. VAD events
            // cannot prolong it; the microphone itself is never muted.
            _manualDeadline = now + ManualMuteSeconds;
            // Also drain a short tail when VAD went quiet immediately before
            // stop, or has not yet reported a late voice activation callback.
            _quietSince = now;
        }
    }

    internal bool ReadyToRelease(double now)
    {
        lock (_gate) return _holding && (_manual
            ? now >= _manualDeadline
            : !_speaking && now - _quietSince >= SilenceSeconds);
    }

    internal void Cancel()
    {
        lock (_gate) _observing = _speaking = _holding = _manual = false;
    }
}
