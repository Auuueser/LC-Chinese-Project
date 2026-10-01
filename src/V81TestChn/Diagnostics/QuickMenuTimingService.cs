using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

// Opt-in, four samples per process. No synchronous log IO in the open callback.
internal static class QuickMenuTimingService
{
    private static MonoBehaviour? _host;
    private static int _count;
    private static Sample? _current;
    private static Harmony? _harmony;
    private static bool _probesReady;
    private sealed class Sample
    {
        internal long Start;
        internal double CallMs;
        internal double ActivationMs, CursorMs, LocalizationMs;
        internal string Preparation = "";
        internal int Frame;
        internal bool ButtonsReady, PlayersReady, DebugReady, SettingsReady, Failed;
    }

    internal static void Initialize(ConfigFile config, Harmony harmony)
    {
        if (!config.Bind("91 诊断 - ESC 性能", "EnableQuickMenuTiming", false,
            "仅排查首次 ESC 卡顿时开启，重启生效。每次运行最多记录四次打开前后各30帧、菜单/UI/第三方回调计时和GC次数变化；延后写日志，不采集玩家姓名。诊断会增加测量开销，正常游玩请关闭。").Value) return;
        _host = MenuRuntimeHost.Ensure();
        _harmony = harmony;
        _count = 0;
        harmony.Patch(AccessTools.Method(typeof(QuickMenuManager), "OpenQuickMenu"),
            prefix: new HarmonyMethod(typeof(QuickMenuTimingService), nameof(Begin)) { priority = Priority.First },
            transpiler: new HarmonyMethod(typeof(QuickMenuTimingService), nameof(MeasureNativeCalls)),
            finalizer: new HarmonyMethod(typeof(QuickMenuTimingService), nameof(End)) { priority = Priority.Last });
        if (_host != null) _host.StartCoroutine(LogOwners(harmony));
    }

    internal static void AttachHost(MonoBehaviour host)
    {
        _host = host;
        _current = null;
        if (_harmony == null) return; // Diagnostics are disabled/not installed yet.
        if (_probesReady) QuickMenuFrameProbe.Reattach(host);
        else host.StartCoroutine(LogOwners(_harmony));
    }

    private static IEnumerator LogOwners(Harmony harmony)
    {
        yield return null;
        var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(QuickMenuManager), "OpenQuickMenu"));
        if (info == null) yield break;
        Plugin.Log.LogInfo("[EscTiming] enabled; build=" + typeof(QuickMenuTimingService).Assembly.ManifestModule.ModuleVersionId);
        foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
            Plugin.Log.LogInfo($"[EscTiming] patch={patch.owner}:{patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}");
        if (_host != null && !_probesReady)
        {
            QuickMenuFrameProbe.Initialize(_host, harmony, info.Prefixes.Concat(info.Postfixes).Select(p => p.PatchMethod));
            _probesReady = true;
        }
    }

    private static void Begin(QuickMenuManager __instance, out Sample? __state)
    {
        __state = null;
        if (_count >= 4 || _host == null || Plugin.IsRuntimeShuttingDown) return;
        _count++;
        QuickMenuFrameProbe.Opened();
        __state = new Sample { Frame = Time.frameCount,
            ButtonsReady = Ready(__instance.mainButtonsPanel), PlayersReady = Ready(__instance.playerListPanel),
            DebugReady = Ready(__instance.debugMenuUI), SettingsReady = Ready(__instance.settingsPanel),
            Preparation = QuickMenuPreparationService.Describe(__instance) };
        _current = __state;
        __state.Start = Stopwatch.GetTimestamp();
    }

    private static bool Ready(GameObject root) => root != null &&
        root.TryGetComponent<MenuTranslationScan>(out var scan) && scan.IsPrepared;

    // Only replace these exact calls inside OpenQuickMenu; preserve IL labels
    // and exception blocks. The original Unity operation still runs exactly once.
    private static IEnumerable<CodeInstruction> MeasureNativeCalls(IEnumerable<CodeInstruction> instructions)
    {
        var activation = typeof(GameObject).GetMethod("SetActive", new[] { typeof(bool) });
        var cursor = typeof(Cursor).GetMethod("SetCursor", new[] { typeof(Texture2D), typeof(Vector2), typeof(CursorMode) });
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(activation))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(QuickMenuTimingService).GetMethod(nameof(SetActive), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
            else if (instruction.Calls(cursor))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = typeof(QuickMenuTimingService).GetMethod(nameof(SetCursor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
            yield return instruction;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        var sample = _current;
        if (sample == null) { target.SetActive(active); return; }
        var start = Stopwatch.GetTimestamp();
        try { target.SetActive(active); }
        finally { sample.ActivationMs += Elapsed(start); }
    }

    private static void SetCursor(Texture2D texture, Vector2 hotspot, CursorMode mode)
    {
        var sample = _current;
        if (sample == null) { Cursor.SetCursor(texture, hotspot, mode); return; }
        var start = Stopwatch.GetTimestamp();
        try { Cursor.SetCursor(texture, hotspot, mode); }
        finally { sample.CursorMs += Elapsed(start); }
    }

    internal static long BeginLocalization() => _current == null ? 0 : Stopwatch.GetTimestamp();
    internal static void EndLocalization(long start)
    {
        if (start != 0 && _current != null) _current.LocalizationMs += Elapsed(start);
    }
    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    // A void finalizer observes but never swallows/replaces game exceptions.
    private static void End(Sample? __state, Exception? __exception)
    {
        if (__state == null) return;
        QuickMenuFrameProbe.MarkTimeline(QuickMenuFrameTimeline.OpenEnd);
        __state.CallMs = (Stopwatch.GetTimestamp() - __state.Start) * 1000.0 / Stopwatch.Frequency;
        __state.Failed = __exception != null;
        _current = null;
        if (_host != null && !Plugin.IsRuntimeShuttingDown) _host.StartCoroutine(Report(__state, _count));
    }

    private static IEnumerator Report(Sample sample, int number)
    {
        while (Time.frameCount <= sample.Frame + 32)
        {
            yield return null;
            if (Plugin.IsRuntimeShuttingDown) yield break;
        }
        QuickMenuFrameProbe.MarkDiagnosticWork();
        var snapshot = QuickMenuFrameProbe.Snapshot(sample.Frame) + QuickMenuGcCounters.Snapshot(sample.Frame);
        // Preserve each window before ring wrap; delay disk IO if another open overlaps.
        while (QuickMenuFrameProbe.WindowActive) yield return null;
        if (Plugin.IsRuntimeShuttingDown) yield break;
        QuickMenuFrameProbe.MarkDiagnosticWork();
        // Frame intervals include rendering/other mods and are not CPU attribution.
        Plugin.Log.LogInfo($"[EscTiming] open={number} frame={sample.Frame} callMs={sample.CallMs:F3} " +
            $"preparedButtons={sample.ButtonsReady} preparedPlayers={sample.PlayersReady} preparedDebug={sample.DebugReady} preparedSettings={sample.SettingsReady} exception={sample.Failed} " +
            $"activationMs={sample.ActivationMs:F3} cursorMs={sample.CursorMs:F3} localizationPostfixMs={sample.LocalizationMs:F3} preparation={sample.Preparation} " +
            snapshot);
    }
}
