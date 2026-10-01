using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class QuickMenuFrameProbe
{
    private static readonly Dictionary<MethodBase, int> Methods = new();
    private static readonly List<string> Labels = new();
    private static QuickMenuFrameHistory _history = new();
    private static QuickMenuFrameTimeline _timeline = new();
    internal static bool Recording => _recording && !Plugin.IsRuntimeShuttingDown;
    private static bool _recording;
    private static int _scanDepth;
    internal static readonly string[] TranslationStages =
    {
        "TryTranslateKnownDynamicText", "TryTranslateKnownDynamicTextFast", "TryTranslateExact",
        "TryTranslateRegex", "TranslateCompositeCore", "TryApplyBuiltInPhraseRegexes",
        "TryApplyRegexEntry", "TryTranslateControlTipItemName", "TryTranslateMapScreenDescription",
        "ReplaceIgnoreCase"
    };
    private static int _lastFrame = -1, _lastGc, _lastOpen = -100, _opens;
    internal static bool WindowActive => Time.frameCount <= _lastOpen + 33;
    internal static void MarkDiagnosticWork() => _history.MarkDiagnosticWork(Time.frameCount);

    internal static void Initialize(MonoBehaviour host, Harmony harmony, IEnumerable<MethodInfo> callbacks)
    {
        Methods.Clear(); Labels.Clear(); _history = new();
        _timeline = new();
        _scanDepth = 0;
        _opens = 0; _lastOpen = -100; _lastFrame = -1; _lastGc = GC.CollectionCount(0);
        Add(harmony, AccessTools.Method(typeof(QuickMenuManager), "Start"));
        Add(harmony, AccessTools.Method(typeof(MenuScanEnumerator), "MoveNext"));
        Add(harmony, AccessTools.Method(typeof(TextMeshProUGUI), "GenerateTextMesh"));
        Add(harmony, AccessTools.Method(typeof(TextMeshPro), "GenerateTextMesh"));
        Add(harmony, AccessTools.Method(typeof(Text), "OnPopulateMesh", new[] { typeof(VertexHelper) }));
        Add(harmony, AccessTools.Method(typeof(LayoutRebuilder), "Rebuild"));
        Add(harmony, AccessTools.Method(typeof(CanvasUpdateRegistry), "PerformUpdate"));
        foreach (var method in typeof(TMP_FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            if (method.Name == "TryAddCharacters") Add(harmony, method);
        foreach (var method in callbacks)
            if (method.DeclaringType != typeof(QuickMenuTimingService)) Add(harmony, method);
        // LethalAdmin creates this behaviour on first ESC, then prepares its UI
        // in a later OnGUI. Timing only the open postfix misses that work.
        var adminUi = AccessTools.TypeByName("LethalAdmin.UI.LethalAdminUI");
        if (adminUi != null)
        {
            Add(harmony, AccessTools.Method(adminUi, "OnGUI"));
            Add(harmony, AccessTools.Method(adminUi, "PrepareGui"));
            Add(harmony, AccessTools.Method(adminUi, "SetMenuForAll"));
            Add(harmony, AccessTools.Method(adminUi, "Awake"));
        }
        var gui = Type.GetType("UnityEngine.GUI, UnityEngine.IMGUIModule");
        var guiUtility = Type.GetType("UnityEngine.GUIUtility, UnityEngine.IMGUIModule");
        if (gui != null) Add(harmony, AccessTools.PropertyGetter(gui, "skin"));
        if (guiUtility != null)
        {
            Add(harmony, AccessTools.Method(guiUtility, "BeginGUI"));
            Add(harmony, AccessTools.Method(guiUtility, "EndGUI"));
        }
        // Detail probes only record inside an actual menu scan. No text values,
        // formatting or file I/O on the measured path. Totals include children.
        AddScanDetails(harmony, typeof(TargetedUiTranslator), "TranslateTmp", "TranslateUiText", "TranslateTextMesh",
            "TryTranslateStaticUiText", "ApplyTmpStyleRepairs", "ApplyUiStyleRepairs", "ApplyTextMeshStyleRepairs",
            "WasTranslationProcessed", "MarkTranslationProcessed", "SafeRefreshShownValue");
        AddScanDetails(harmony, typeof(FontFallbackService), "ApplyFallback");
        AddScanDetails(harmony, typeof(CustomLocalizationExtensionService), "ApplyStyle");
        AddScanDetails(harmony, typeof(AlertTextureReplacementService), "TryReplaceSystemOnlineText");
        AddScanDetails(harmony, typeof(RuntimeTextCollector), "Record");
        AddScanDetails(harmony, typeof(TranslationService), "TryTranslate");
        AddScanDetails(harmony, typeof(AutomaticTranslationService), "TryTranslateOrQueue");
        AddScanDetails(harmony, typeof(TranslationService), TranslationStages);
        AddScanDetails(harmony, typeof(CustomLocalizationExtensionService), "TryTranslate");
        QuickMenuGcCounters.Initialize();
        _recording = true;
        host.gameObject.AddComponent<QuickMenuFrameProbeDriver>();
        Plugin.Log.LogInfo("[EscTiming] frame probe enabled: 30 before/30 after; global UI inclusive method times, not additive; GC=gen0 count delta, not pause duration.");
    }

    private static void AddScanDetails(Harmony harmony, Type type, params string[] names)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            if (Array.IndexOf(names, method.Name) >= 0) Add(harmony, method, scanOnly: true);
    }

    private static void Add(Harmony harmony, MethodInfo? method, bool scanOnly = false)
    {
        if (method == null || Methods.ContainsKey(method)) return;
        if (Methods.Count >= QuickMenuFrameHistory.MethodLimit)
        {
            Plugin.Log.LogWarning("[EscTiming] probe capacity exceeded: " + method);
            return;
        }
        try
        {
            if (method.GetMethodBody() == null) return;
            var scanRoot = method.DeclaringType == typeof(MenuScanEnumerator) && method.Name == "MoveNext";
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(QuickMenuFrameProbe), scanRoot ? nameof(BeginScan) : scanOnly ? nameof(BeginScanDetail) : nameof(Begin)) { priority = Priority.First },
                finalizer: new HarmonyMethod(typeof(QuickMenuFrameProbe), scanRoot ? nameof(EndScan) : nameof(End)) { priority = Priority.Last });
            Methods.Add(method, Labels.Count);
            Labels.Add((scanOnly ? "scanOnly:" : "") + method.DeclaringType?.FullName + "." + method.Name + "#" + method.MetadataToken);
            Plugin.Log.LogInfo("[EscTiming] probe=" + Labels[Labels.Count - 1]);
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[EscTiming] probe unavailable {method}: {ex.GetType().Name}: {ex.Message}"); }
    }

    internal static void Reattach(MonoBehaviour host)
    {
        // Do not reinstall probes or reset the per-process sample budget.
        // Interrupted windows are discarded, never stitched across host loss.
        _history = new(); _lastFrame = -1; _lastGc = GC.CollectionCount(0);
        _timeline = new();
        if (_recording) QuickMenuGcCounters.Initialize();
        host.gameObject.AddComponent<QuickMenuFrameProbeDriver>();
        Plugin.Log.LogWarning("[EscTiming] runtime host recovered; interrupted windows discarded, probe registrations/sample budget preserved.");
    }

    internal static void Opened()
    {
        MarkTimeline(QuickMenuFrameTimeline.OpenBegin);
        _lastOpen = Time.frameCount;
        _opens++;
    }

    internal static void Tick()
    {
        if (!_recording || Plugin.IsRuntimeShuttingDown) return;
        MarkTimeline(QuickMenuFrameTimeline.Update);
        var frame = Time.frameCount;
        var gc = GC.CollectionCount(0);
        if (_lastFrame >= 0 && frame == _lastFrame + 1)
        {
            _history.RecordInterval(_lastFrame, Time.unscaledDeltaTime * 1000.0, Math.Max(0, gc - _lastGc));
            QuickMenuGcCounters.Tick(_lastFrame);
        }
        _lastFrame = frame; _lastGc = gc;
        if (_opens >= 4 && frame > _lastOpen + 33) { _recording = false; QuickMenuGcCounters.Dispose(); }
    }

    internal static void MarkTimeline(int stage)
    {
        if (Recording) _timeline.Mark(Time.frameCount, stage, Stopwatch.GetTimestamp());
    }

    private static void Begin(out long __state) => __state = _recording && !Plugin.IsRuntimeShuttingDown ? Stopwatch.GetTimestamp() : 0;
    private static void BeginScan(out long __state)
    {
        Begin(out __state);
        if (__state != 0) _scanDepth++;
    }
    private static void BeginScanDetail(out long __state)
        => __state = _scanDepth > 0 && _recording && !Plugin.IsRuntimeShuttingDown ? Stopwatch.GetTimestamp() : 0;
    private static void EndScan(MethodBase __originalMethod, long __state)
    {
        try { End(__originalMethod, __state); }
        finally { if (__state != 0) _scanDepth--; }
    }
    private static void End(MethodBase __originalMethod, long __state)
    {
        if (__state == 0 || !Methods.TryGetValue(__originalMethod, out var index)) return;
        _history.RecordMethod(Time.frameCount, index, (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency);
    }

    internal static string Snapshot(int openingFrame)
    {
        var output = new StringBuilder(4096);
        output.Append("frameWindow openingFrame=").Append(openingFrame).Append(" offsets=-30..30 followingIntervalMs=");
        for (var offset = -30; offset <= 30; offset++)
        {
            if (offset != -30) output.Append(',');
            output.Append(_history.Interval(openingFrame + offset).ToString("F2", CultureInfo.InvariantCulture));
        }
        output.Append("; gcGen0Deltas=");
        for (var offset = -30; offset <= 30; offset++)
        {
            if (offset != -30) output.Append(',');
            output.Append(_history.Contains(openingFrame + offset) ? _history.Collections(openingFrame + offset).ToString() : "NA");
        }
        output.Append("; diagnosticWorkOffsets=");
        for (var offset = -30; offset <= 30; offset++)
            if (_history.HasDiagnosticWork(openingFrame + offset)) output.Append(offset).Append(',');
        // At most 61 * MethodLimit records; omit sub-ms totals. No text values or player names.
        output.Append("; inclusiveMethodTotalsAbove1ms=");
        for (var offset = -30; offset <= 30; offset++)
            for (var method = 0; method < Labels.Count; method++)
            {
                var ms = _history.Milliseconds(openingFrame + offset, method);
                if (ms < 1) continue;
                output.Append(" [offset=").Append(offset).Append(' ').Append(Labels[method])
                    .Append(" ms=").Append(ms.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" maxMs=").Append(_history.MaxMilliseconds(openingFrame + offset, method).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" calls=").Append(_history.Calls(openingFrame + offset, method)).Append(']');
            }
        output.Append(_timeline.Snapshot(openingFrame, Stopwatch.Frequency));
        return output.ToString();
    }
}

internal sealed class QuickMenuFrameProbeDriver : MonoBehaviour
{
    private static QuickMenuFrameProbeDriver? _owner;
    private void Awake() => _owner = this;
    private void Update() => QuickMenuFrameProbe.Tick();
    private void LateUpdate() => QuickMenuFrameProbe.MarkTimeline(QuickMenuFrameTimeline.LateUpdate);
    private IEnumerator Start()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (QuickMenuFrameProbe.Recording)
        {
            yield return endOfFrame;
            QuickMenuFrameProbe.MarkTimeline(QuickMenuFrameTimeline.EndOfFrame);
        }
    }
    private void OnDestroy()
    {
        if (!ReferenceEquals(_owner, this)) return;
        _owner = null;
        QuickMenuGcCounters.Dispose();
    }
}
