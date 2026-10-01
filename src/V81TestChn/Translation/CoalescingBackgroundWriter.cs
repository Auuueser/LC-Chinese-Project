using System;
using System.Threading.Tasks;

namespace V81TestChn;

// One active write and at most one pending full snapshot. A newer pending
// snapshot supersedes the older one; an active write always finishes first.
internal sealed class CoalescingBackgroundWriter<T> where T : class
{
    private readonly object _gate = new();
    private readonly Action<T> _write;
    private T? _pending;
    private bool _running;
    private bool _closed;
    private Task _completion = Task.CompletedTask;
    private Exception? _failure;

    internal CoalescingBackgroundWriter(Action<T> write) => _write = write;

    internal bool TryEnqueue(T snapshot)
    {
        lock (_gate)
        {
            if (_closed) return false;
            _pending = snapshot;
            if (!_running)
            {
                _running = true;
                _completion = Task.Run(Run);
            }
            return true;
        }
    }

    internal Exception? TakeFailure()
    {
        lock (_gate)
        {
            var failure = _failure;
            _failure = null;
            return failure;
        }
    }

    // Caller may wait on shutdown, never during the normal frame pump. Closing
    // rejects late submissions and drains the last accepted full snapshot.
    internal Task Complete()
    {
        lock (_gate)
        {
            _closed = true;
            return _completion;
        }
    }

    private void Run()
    {
        while (true)
        {
            T snapshot;
            lock (_gate)
            {
                if (_pending == null)
                {
                    _running = false;
                    return;
                }
                snapshot = _pending;
                _pending = null;
            }

            Exception? failure = null;
            try { _write(snapshot); }
            catch (Exception ex) { failure = ex; }
            lock (_gate) _failure = failure;
        }
    }
}
