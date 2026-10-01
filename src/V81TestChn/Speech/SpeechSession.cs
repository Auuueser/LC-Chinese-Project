using System;

namespace V81TestChn;

internal enum SpeechStopReason { None, Silence, NoSpeech, DurationLimit }

// The clock is supplied by the caller; no Unity API is used on audio threads.
internal sealed class SpeechSession
{
    internal const int MaximumSeconds = 30;
    internal const double InitialSilenceSeconds = 5;
    internal bool Recording { get; private set; }
    internal long Id { get; private set; }
    private double _started, _lastSpeech;
    private bool _spoken, _speaking;

    internal long Begin(double now)
    {
        Id++;
        Recording = true;
        _started = _lastSpeech = now;
        _spoken = _speaking = false;
        return Id;
    }

    internal void ObserveVoice(bool speaking, double now)
    {
        if (!Recording) return;
        if (speaking || _speaking) _lastSpeech = now;
        _speaking = speaking;
        _spoken |= speaking;
    }

    internal SpeechStopReason Poll(double now, double silenceSeconds)
    {
        if (!Recording) return SpeechStopReason.None;
        if (now - _started >= MaximumSeconds) return SpeechStopReason.DurationLimit;
        if (!_spoken && now - _started >= InitialSilenceSeconds) return SpeechStopReason.NoSpeech;
        if (_spoken && !_speaking && now - _lastSpeech >= silenceSeconds) return SpeechStopReason.Silence;
        return SpeechStopReason.None;
    }

    internal bool End() { Recording = false; return _spoken; }
    internal void Cancel() { Recording = false; Id++; }
    internal bool Accepts(long id) => id == Id && !Recording;
}
