using System;
using System.Collections.Generic;

namespace V81TestChn;

internal sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private const int MaximumInitialCapacity = 512;
    private const int MaximumRecycledOrderNodes = 512;
    private readonly Dictionary<TKey, CacheEntry> _entries;
    private readonly LinkedList<TKey> _insertionOrder = new();
    private readonly Stack<LinkedListNode<TKey>> _recycledOrderNodes = new();

    private readonly struct CacheEntry
    {
        public CacheEntry(TValue value, LinkedListNode<TKey> orderNode)
        {
            Value = value;
            OrderNode = orderNode;
        }

        public TValue Value { get; }
        public LinkedListNode<TKey> OrderNode { get; }
    }

    public BoundedCache(int expectedLimit, IEqualityComparer<TKey>? comparer = null)
    {
        var initialCapacity = Math.Max(0, Math.Min(expectedLimit, MaximumInitialCapacity));
        _entries = new Dictionary<TKey, CacheEntry>(initialCapacity, comparer);
    }

    public int Count => _entries.Count;
    public long EvictionCount { get; private set; }

    public bool ContainsKey(TKey key) => _entries.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value, int limit)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            _entries[key] = new CacheEntry(value, existing.OrderNode);
            return;
        }

        TrimForInsert(Math.Max(1, limit));
        var orderNode = RentOrderNode(key);
        try
        {
            _entries.Add(key, new CacheEntry(value, orderNode));
        }
        catch
        {
            RecycleOrderNode(orderNode);
            throw;
        }

        _insertionOrder.AddLast(orderNode);
    }

    public bool Remove(TKey key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        _entries.Remove(key);
        _insertionOrder.Remove(entry.OrderNode);
        RecycleOrderNode(entry.OrderNode);
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _insertionOrder.Clear();
        _recycledOrderNodes.Clear();
        EvictionCount = 0;
    }

    private void TrimForInsert(int limit)
    {
        while (_entries.Count >= limit && _insertionOrder.First is { } oldest)
        {
            // Dictionary removal can execute a user comparer and throw. Keep the order node linked
            // until the authoritative entry has been removed so both structures remain consistent.
            if (!_entries.Remove(oldest.Value))
            {
                _entries.Clear();
                _insertionOrder.Clear();
                _recycledOrderNodes.Clear();
                EvictionCount++;
                return;
            }

            _insertionOrder.RemoveFirst();
            RecycleOrderNode(oldest);
            EvictionCount++;
        }

        if (_entries.Count >= limit)
        {
            _entries.Clear();
            _insertionOrder.Clear();
            EvictionCount++;
        }
    }

    private LinkedListNode<TKey> RentOrderNode(TKey key)
    {
        if (_recycledOrderNodes.Count == 0)
        {
            return new LinkedListNode<TKey>(key);
        }

        var node = _recycledOrderNodes.Pop();
        node.Value = key;
        return node;
    }

    private void RecycleOrderNode(LinkedListNode<TKey> node)
    {
        if (_recycledOrderNodes.Count >= MaximumRecycledOrderNodes)
        {
            return;
        }

        node.Value = default!;
        _recycledOrderNodes.Push(node);
    }
}

internal sealed class BoundedSet<T> where T : notnull
{
    private readonly BoundedCache<T, byte> _cache;

    public BoundedSet(int expectedLimit, IEqualityComparer<T>? comparer = null)
    {
        _cache = new BoundedCache<T, byte>(expectedLimit, comparer);
    }

    public int Count => _cache.Count;
    public long EvictionCount => _cache.EvictionCount;
    public bool Contains(T value) => _cache.ContainsKey(value);

    public bool Add(T value, int limit)
    {
        if (_cache.ContainsKey(value))
        {
            return false;
        }

        _cache.Set(value, 0, limit);
        return true;
    }

    public bool Remove(T value) => _cache.Remove(value);
    public void Clear() => _cache.Clear();
}
