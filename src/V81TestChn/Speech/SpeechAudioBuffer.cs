using System;

namespace V81TestChn;

// Dissonance owns incoming arrays. Copy into a fixed buffer before returning.
internal sealed class SpeechAudioBuffer
{
    internal const int SampleRate = 48000;
    internal const int Capacity = SampleRate * SpeechSession.MaximumSeconds;
    private readonly object _gate = new();
    internal readonly float[] Samples = new float[Capacity];
    internal readonly SpeechSession Session = new();
    private bool _active;
    private int _count;
    private volatile bool _faulted;
    internal bool Faulted => _faulted;

    internal long Begin(double now)
    {
        lock (_gate) { _count = 0; _faulted = false; _active = true; return Session.Begin(now); }
    }

    internal void Receive(ArraySegment<float> data, int rate, int channels)
    {
        lock (_gate)
        {
            if (!_active) return;
            if (rate != SampleRate || channels != 1 || data.Array == null) { _faulted = true; return; }
            var count = Math.Min(data.Count, Capacity - _count);
            Array.Copy(data.Array, data.Offset, Samples, _count, count);
            _count += count;
        }
    }

    internal void Voice(bool active, double now) { lock (_gate) Session.ObserveVoice(active, now); }
    internal void Reset() { lock (_gate) { if (_active) _faulted = true; } }
    internal SpeechStopReason Poll(double now, double silence) { lock (_gate) return Session.Poll(now, silence); }
    internal int End(out bool spoken)
    {
        lock (_gate) { _active = false; spoken = Session.End(); return _count; }
    }
    internal void Cancel() { lock (_gate) { _active = false; Session.Cancel(); _count = 0; } }
}
