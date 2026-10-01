using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace V81TestChn;

// Use only counters actually exposed by this player build. Never infer a
// pause duration from collection counts or treat an unavailable counter as zero.
internal static class QuickMenuGcCounters
{
    private sealed class Counter
    {
        internal string Label = "";
        internal ProfilerRecorder Recorder;
    }
    private static readonly List<Counter> Counters = new();
    private static QuickMenuFrameHistory _history = new();

    internal static void Initialize()
    {
        Dispose();
        _history = new();
        try
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                var name = description.Name;
                if (Counters.Count >= 8 || !Wanted(name)) continue;
                var recorder = new ProfilerRecorder(handle, 1);
                if (!recorder.Valid) { recorder.Dispose(); continue; }
                recorder.Start();
                var counter = new Counter { Label = name + "[" + description.UnitType + "]", Recorder = recorder };
                Counters.Add(counter);
                Plugin.Log.LogInfo("[EscTiming] nativeGcCounter=" + counter.Label);
            }
            if (Counters.Count == 0)
                Plugin.Log.LogWarning("[EscTiming] native GC pause/allocation counters unavailable in this player; collection counts cannot establish pause duration.");
            else
                Plugin.Log.LogInfo("[EscTiming] only listed native GC counters are available; NA means no sample, not a measured zero. Values keep native units and may include multiple threads.");
        }
        catch (Exception ex)
        {
            Dispose();
            Plugin.Log.LogWarning($"[EscTiming] native GC counters unavailable: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static bool Wanted(string name) => name == "GC.Collect" || name == "GC.CollectIncremental" ||
        name == "GarbageCollector.CollectIncremental" || name == "GC Allocated In Frame" || name == "GC.Alloc";

    internal static void Tick(int precedingFrame)
    {
        if (precedingFrame < 0) return;
        for (var i = 0; i < Counters.Count; i++)
        {
            var counter = Counters[i];
            if (!counter.Recorder.Valid || !counter.Recorder.IsRunning) continue;
            if (counter.Recorder.Count > 0)
            {
                var sample = counter.Recorder.GetSample(counter.Recorder.Count - 1);
                _history.RecordInterval(precedingFrame, 0, 0);
                _history.RecordMethod(precedingFrame, i, sample.Value);
                // Count is marker/counter sample count, never claimed to be GC count.
            }
            counter.Recorder.Reset();
        }
    }

    internal static string Snapshot(int frame)
    {
        var result = new StringBuilder();
        for (var i = 0; i < Counters.Count; i++)
        {
            result.Append("; nativeGcCounter=").Append(Counters[i].Label).Append(" offsets=-30..30 values=");
            for (var offset = -30; offset <= 30; offset++)
            {
                if (offset != -30) result.Append(',');
                result.Append(_history.Calls(frame + offset, i) == 0 ? "NA" :
                    _history.Milliseconds(frame + offset, i).ToString("F0", CultureInfo.InvariantCulture));
            }
        }
        return result.ToString();
    }

    internal static void Dispose()
    {
        foreach (var counter in Counters) counter.Recorder.Dispose();
        Counters.Clear();
    }
}
