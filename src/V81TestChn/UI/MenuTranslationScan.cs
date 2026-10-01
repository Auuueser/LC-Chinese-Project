using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace V81TestChn;

// State belongs to the actual panel, not its manager or a reusable Unity instance ID.
// Disabling a panel explicitly cancels an incomplete pass; enabling it retries.
internal sealed class MenuTranslationScan : MonoBehaviour
{
    private static int _epoch;
    private int _observedEpoch = -1;
    private readonly MenuScanState _state = new();
    private readonly HashSet<int> _knownTexts = new();
    private string? _reason;
    private int _debugRootId;
    internal bool IsPrepared => _observedEpoch == _epoch && _state.Completed;

    internal static void ResetEpoch() => _epoch++;

    internal static MenuTranslationScan For(GameObject root)
        => root.GetComponent<MenuTranslationScan>() ?? root.AddComponent<MenuTranslationScan>();

    internal void RecordText(Component text) => _knownTexts.Add(text.GetInstanceID());

    internal void Prewarm(MonoBehaviour host, string reason, int debugRootId = 0)
    {
        // Configure epoch/lifecycle normally. An active root already schedules
        // itself; a hidden root borrows the active room/player coroutine host.
        Request(reason, debugRootId);
        if (isActiveAndEnabled || host == null || !host.isActiveAndEnabled ||
            Plugin.IsRuntimeShuttingDown || !_state.TryBegin(out var ticket)) return;
        try { host.StartCoroutine(Run(ticket)); }
        catch (Exception ex)
        {
            _state.Cancel();
            Plugin.Log.LogWarning($"Hidden menu preparation failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void NotifyTextEnabled(Component text)
    {
        if (Plugin.IsRuntimeShuttingDown) return;
        var scan = text.GetComponentInParent<MenuTranslationScan>(true);
        if (scan == null || scan._reason == null || !scan._knownTexts.Add(text.GetInstanceID())) return;
        // A deep descendant can be created after completion without changing
        // the root's immediate children. Handle this text, then queue one pass.
        TargetedUiTranslator.TranslateMenuComponentBeforeRender(text);
        scan._state.Invalidate();
        scan.Request(scan._reason, scan._debugRootId);
    }

    internal void Request(string reason, int debugRootId = 0)
    {
        _reason = reason;
        _debugRootId = debugRootId;
        if (_observedEpoch != _epoch)
        {
            StopAllCoroutines();
            _state.Cancel();
            _knownTexts.Clear();
            _observedEpoch = _epoch;
        }
        if (!isActiveAndEnabled || Plugin.IsRuntimeShuttingDown || !_state.TryBegin(out var ticket)) return;
        try { StartCoroutine(Run(ticket)); }
        catch (Exception ex)
        {
            _state.Cancel();
            Plugin.Log.LogWarning($"Menu translation scheduling failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        if (_reason != null) Request(_reason, _debugRootId);
    }

    private void OnDisable()
    {
        // Unity does not promise to dispose stopped coroutine iterators.
        StopAllCoroutines();
        if (_state.Running) _state.Cancel();
    }

    private void OnDestroy() => TargetedUiTranslator.ForgetMenuRoot(gameObject.GetInstanceID());

    private void OnTransformChildrenChanged()
    {
        _state.Invalidate();
        if (_reason != null) Request(_reason, _debugRootId);
    }

    private IEnumerator Run(int ticket)
    {
        // StartCoroutine executes synchronously until its first yield. Keep full
        // scans out of SetActive/OnEnable and batch row creation before walking.
        // Fixed labels still use the independent before-render localization path.
        yield return null;
        if (this == null || Plugin.IsRuntimeShuttingDown || !_state.IsCurrent(ticket)) yield break;
        // Flatten nested enumerators so a child traversal exception cannot be
        // mistaken for successful completion by Unity's coroutine scheduler.
        using var traversal = new MenuScanEnumerator(TargetedUiTranslator.TranslateMenuRootBudgeted(gameObject, _reason!, _debugRootId), TargetedUiTranslator.HasMenuTime);
        while (this != null && !Plugin.IsRuntimeShuttingDown && _state.IsCurrent(ticket))
        {
            bool more;
            try { more = traversal.MoveNext(); }
            catch (Exception ex)
            {
                _state.Cancel();
                Plugin.Log.LogWarning($"Menu translation interrupted: {ex.GetType().Name}: {ex.Message}");
                yield break;
            }
            if (!more) break;
            yield return traversal.Current;
        }
        if (this == null || Plugin.IsRuntimeShuttingDown || !_state.IsCurrent(ticket)) yield break;
        _state.Complete(ticket);
        if (!_state.Completed && isActiveAndEnabled) Request(_reason!, _debugRootId);
    }
}

internal sealed class MenuScanState
{
    private int _ticket;
    private bool _invalidated;
    internal bool Running { get; private set; }
    internal bool Completed { get; private set; }

    internal bool IsCurrent(int ticket) => Running && _ticket == ticket;

    internal bool TryBegin(out int ticket)
    {
        ticket = _ticket;
        if (Running || Completed) return false;
        ticket = ++_ticket;
        Running = true;
        _invalidated = false;
        return true;
    }

    internal void Complete(int ticket)
    {
        if (!Running || ticket != _ticket) return;
        Running = false;
        Completed = !_invalidated;
    }

    internal void Invalidate()
    {
        Completed = false;
        _invalidated = true;
    }

    internal void Cancel()
    {
        Running = false;
        Completed = false;
        _ticket++;
    }
}

internal sealed class MenuScanEnumerator : IEnumerator, IDisposable
{
    private readonly Stack<IEnumerator> _pending = new();
    private readonly Func<bool>? _canAdvance;
    public object? Current { get; private set; }
    internal MenuScanEnumerator(IEnumerator root) => _pending.Push(root);
    internal MenuScanEnumerator(IEnumerator root, Func<bool> canAdvance) : this(root)
        => _canAdvance = canAdvance;

    public bool MoveNext()
    {
        while (_pending.Count > 0)
        {
            // Child entry, completion and return to a parent are work too.
            // All roots use the same deadline; do not restart it per iterator.
            if (_canAdvance != null && !_canAdvance()) { Current = null; return true; }
            var current = _pending.Peek();
            if (!current.MoveNext())
            {
                _pending.Pop();
                (current as IDisposable)?.Dispose();
                continue;
            }
            if (current.Current is IEnumerator child) { _pending.Push(child); continue; }
            Current = current.Current;
            return true;
        }
        return false;
    }

    public void Reset() => throw new NotSupportedException();
    public void Dispose()
    {
        while (_pending.Count > 0) (_pending.Pop() as IDisposable)?.Dispose();
    }
}
