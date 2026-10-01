using System.Collections.Generic;
using System.Threading;

namespace V81TestChn;

internal sealed class PlayerNameRefreshInbox
{
    private readonly object _gate = new();
    private readonly HashSet<ulong> _tracked = new();
    private readonly HashSet<ulong> _pending = new();
    private int _hasPending;
    internal bool HasPending => Volatile.Read(ref _hasPending) != 0;
    internal bool Track(ulong id) { lock (_gate) return _tracked.Add(id); }
    internal void Notify(ulong id)
    {
        lock (_gate)
        {
            if (!_tracked.Contains(id)) return;
            _pending.Add(id);
            Volatile.Write(ref _hasPending, 1);
        }
    }
    internal ulong[] Drain()
    {
        lock (_gate)
        {
            var ids = new ulong[_pending.Count];
            _pending.CopyTo(ids);
            _pending.Clear();
            Volatile.Write(ref _hasPending, 0);
            return ids;
        }
    }
    internal void Remove(ulong id)
    {
        lock (_gate)
        {
            _tracked.Remove(id);
            _pending.Remove(id);
            Volatile.Write(ref _hasPending, _pending.Count == 0 ? 0 : 1);
        }
    }
    internal void ClearPending() { lock (_gate) { _pending.Clear(); Volatile.Write(ref _hasPending, 0); } }
    internal void Clear() { lock (_gate) { _tracked.Clear(); ClearPending(); } }
}
