using System;
using System.Globalization;
using System.Text;

namespace V81TestChn;

// Observed callback timestamps, not PlayerLoop boundaries or GPU/present times.
internal sealed class QuickMenuFrameTimeline
{
    internal const int Update = 0, OpenBegin = 1, OpenEnd = 2, LateUpdate = 3, EndOfFrame = 4;
    private const int Capacity = 96, Stages = 5;
    private readonly int[] _frames = new int[Capacity];
    private readonly long[] _ticks = new long[Capacity * Stages];
    private readonly bool[] _present = new bool[Capacity * Stages];

    internal QuickMenuFrameTimeline() { for (var i = 0; i < Capacity; i++) _frames[i] = -1; }

    internal void Mark(int frame, int stage, long timestamp)
    {
        if (frame < 0 || stage < 0 || stage >= Stages) return;
        var slot = frame % Capacity;
        if (_frames[slot] != frame)
        {
            _frames[slot] = frame;
            Array.Clear(_present, slot * Stages, Stages);
        }
        var index = slot * Stages + stage;
        if (_present[index]) return; // First observation per stage/frame.
        _ticks[index] = timestamp;
        _present[index] = true;
    }

    private bool Has(int frame, int stage) => frame >= 0 && stage >= 0 && stage < Stages &&
        _frames[frame % Capacity] == frame && _present[frame % Capacity * Stages + stage];

    internal double Between(int frame, int stage, int targetFrame, int targetStage, long frequency)
    {
        if (frequency <= 0 || !Has(frame, stage) || !Has(targetFrame, targetStage)) return double.NaN;
        // Negative offsets are valid when another script opens ESC before our Update.
        return (_ticks[targetFrame % Capacity * Stages + targetStage] -
            _ticks[frame % Capacity * Stages + stage]) * 1000.0 / frequency;
    }

    internal string Snapshot(int openingFrame, long frequency)
    {
        var output = new StringBuilder(1024);
        output.Append("; observedTimelineMs(update-relative; first observation; not CPU/GPU attribution)=");
        for (var offset = -1; offset <= 5; offset++)
        {
            var frame = openingFrame + offset;
            output.Append(" [offset=").Append(offset);
            Append(" openBegin=", Between(frame, Update, frame, OpenBegin, frequency));
            Append(" openEnd=", Between(frame, Update, frame, OpenEnd, frequency));
            Append(" lateUpdate=", Between(frame, Update, frame, LateUpdate, frequency));
            Append(" endOfFrame=", Between(frame, Update, frame, EndOfFrame, frequency));
            Append(" nextUpdate=", Between(frame, Update, frame + 1, Update, frequency));
            Append(" openToNextUpdate=", Between(frame, OpenBegin, frame + 1, Update, frequency));
            output.Append(']');
        }
        return output.ToString();

        void Append(string label, double value) => output.Append(label).Append(double.IsNaN(value)
            ? "NA" : value.ToString("F3", CultureInfo.InvariantCulture));
    }
}
