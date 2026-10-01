using System;

namespace V81TestChn;

// Pure bounded storage; frame intervals are attached to the preceding frame
// at the next Update. Method times are inclusive and must not be summed.
internal sealed class QuickMenuFrameHistory
{
    internal const int Capacity = 96;
    internal const int MethodLimit = 64;
    private readonly int[] _frames = new int[Capacity];
    private readonly double[] _intervals = new double[Capacity];
    private readonly int[] _gc = new int[Capacity];
    private readonly bool[] _diagnosticWork = new bool[Capacity];
    private readonly double[] _milliseconds = new double[Capacity * MethodLimit];
    private readonly double[] _maxMilliseconds = new double[Capacity * MethodLimit];
    private readonly int[] _calls = new int[Capacity * MethodLimit];

    internal QuickMenuFrameHistory() { for (var i = 0; i < Capacity; i++) _frames[i] = -1; }
    private int Slot(int frame)
    {
        var slot = frame % Capacity;
        if (_frames[slot] == frame) return slot;
        _frames[slot] = frame;
        _intervals[slot] = double.NaN;
        _gc[slot] = 0;
        _diagnosticWork[slot] = false;
        Array.Clear(_milliseconds, slot * MethodLimit, MethodLimit);
        Array.Clear(_maxMilliseconds, slot * MethodLimit, MethodLimit);
        Array.Clear(_calls, slot * MethodLimit, MethodLimit);
        return slot;
    }
    internal void RecordInterval(int frame, double milliseconds, int gcCollections)
    {
        if (frame < 0) return;
        var slot = Slot(frame);
        _intervals[slot] = milliseconds;
        _gc[slot] = gcCollections;
    }
    internal void RecordMethod(int frame, int method, double milliseconds)
    {
        if (frame < 0 || method < 0 || method >= MethodLimit) return;
        var index = Slot(frame) * MethodLimit + method;
        _milliseconds[index] += milliseconds;
        _maxMilliseconds[index] = Math.Max(_maxMilliseconds[index], milliseconds);
        _calls[index]++;
    }
    internal bool Contains(int frame) => frame >= 0 && _frames[frame % Capacity] == frame;
    internal void MarkDiagnosticWork(int frame) { if (frame >= 0) _diagnosticWork[Slot(frame)] = true; }
    internal bool HasDiagnosticWork(int frame) => Contains(frame) && _diagnosticWork[frame % Capacity];
    internal double Interval(int frame) => Contains(frame) ? _intervals[frame % Capacity] : double.NaN;
    internal int Collections(int frame) => Contains(frame) ? _gc[frame % Capacity] : 0;
    internal double Milliseconds(int frame, int method) => Contains(frame) ? _milliseconds[frame % Capacity * MethodLimit + method] : 0;
    internal int Calls(int frame, int method) => Contains(frame) ? _calls[frame % Capacity * MethodLimit + method] : 0;
    internal double MaxMilliseconds(int frame, int method) => Contains(frame) ? _maxMilliseconds[frame % Capacity * MethodLimit + method] : 0;
}
