using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace V81TestChn;

// Only caches parsed expressions, never input text or translation results. Instance
// methods keep this plugin independent of the process-wide static Regex cache.
internal sealed class BoundedRegexCache
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = new();
    private readonly LinkedList<Entry> _recency = new();
    private long _evictionCount;

    public BoundedRegexCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public int Count
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    public long EvictionCount
    {
        get { lock (_gate) { return _evictionCount; } }
    }

    public Regex Get(string pattern, RegexOptions options, TimeSpan timeout)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        var culture = (options & RegexOptions.CultureInvariant) != 0
            ? null
            : CultureInfo.CurrentCulture;
        var key = new Key(pattern, options, timeout, culture);
        lock (_gate)
        {
            if (TryGetLocked(key, out var cached))
            {
                return cached;
            }
        }

        // Preserve the caller's options and timeout, including culture-sensitive
        // casing. Parsing and all matching run outside the cache lock. Invalid
        // patterns/arguments throw normally and are never retained as entries.
        var created = new Regex(pattern, options, timeout);
        lock (_gate)
        {
            if (TryGetLocked(key, out var cached))
            {
                return cached;
            }

            if (_entries.Count == _capacity)
            {
                var oldest = _recency.First!;
                _entries.Remove(oldest.Value.Key);
                _recency.RemoveFirst();
                _evictionCount++;
            }

            var node = _recency.AddLast(new Entry(key, created));
            _entries.Add(key, node);
            return created;
        }
    }

    private bool TryGetLocked(Key key, out Regex regex)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            if (node != _recency.Last)
            {
                _recency.Remove(node);
                _recency.AddLast(node);
            }

            regex = node.Value.Regex;
            return true;
        }

        regex = null!;
        return false;
    }

    private readonly struct Entry
    {
        public Entry(Key key, Regex regex)
        {
            Key = key;
            Regex = regex;
        }

        public Key Key { get; }
        public Regex Regex { get; }
    }

    private readonly struct Key : IEquatable<Key>
    {
        private readonly string _pattern;
        private readonly RegexOptions _options;
        private readonly TimeSpan _timeout;
        private readonly CultureInfo? _culture;

        public Key(string pattern, RegexOptions options, TimeSpan timeout, CultureInfo? culture)
        {
            _pattern = pattern;
            _options = options;
            _timeout = timeout;
            _culture = culture;
        }

        // Conservatively keep distinct culture objects separate, even if they
        // share a name. The same capacity bounds their lifetime and total count.
        public bool Equals(Key other) =>
            string.Equals(_pattern, other._pattern, StringComparison.Ordinal) &&
            _options == other._options && _timeout == other._timeout &&
            ReferenceEquals(_culture, other._culture);

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(_pattern);
                hash = (hash * 397) ^ (int)_options;
                hash = (hash * 397) ^ _timeout.GetHashCode();
                return (hash * 397) ^ (_culture == null ? 0 : RuntimeHelpers.GetHashCode(_culture));
            }
        }
    }
}
