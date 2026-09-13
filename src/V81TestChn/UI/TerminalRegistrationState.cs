using System;
using System.Collections.Generic;

namespace V81TestChn;

// Exact structural comparison, without hashes or allocations on an unchanged pass.
// Only used on terminal registration/open events, never per frame or per keystroke.
internal sealed class TerminalRegistrationState
{
    private readonly List<(object? Reference, string? Text, int A, int B, int C, int D)> _entries = new();
    private int _cursor;
    private bool _changed;
    internal void Begin() { _cursor = 0; _changed = false; }
    internal void Observe(object? reference, string? text = null, int a = 0, int b = 0, int c = 0, int d = 0)
    {
        var entry = (reference, text, a, b, c, d);
        if (_cursor == _entries.Count) { _entries.Add(entry); _changed = true; }
        else
        {
            var old = _entries[_cursor];
            if (!ReferenceEquals(old.Reference, reference) || !string.Equals(old.Text, text, StringComparison.Ordinal) ||
                old.A != a || old.B != b || old.C != c || old.D != d)
            {
                _entries[_cursor] = entry;
                _changed = true;
            }
        }
        _cursor++;
    }
    internal bool End()
    {
        if (_cursor < _entries.Count)
        {
            _entries.RemoveRange(_cursor, _entries.Count - _cursor);
            _changed = true;
        }
        return _changed;
    }
}
