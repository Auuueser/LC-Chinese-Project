using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace V81TestChn;

internal static class PlayerNamePolicy
{
    // Versioned lobby member metadata is optional. Bound untrusted input and
    // apply the same display policy; absence/invalid data keeps vanilla fallback.
    internal static string Advertised(string? input)
        => string.IsNullOrEmpty(input) || input.Length > 128 ? string.Empty : Sanitize(input);

    // Preserve ordinary spaces exactly (including repeated spaces), but keep
    // control characters and punctuation out of game/UI names as before.
    internal static string Sanitize(string? input, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (input.Length <= maxLength)
        {
            var unchanged = true;
            var visible = false;
            foreach (var ch in input)
            {
                if (!char.IsLetterOrDigit(ch) && ch != ' ') { unchanged = false; break; }
                visible |= ch != ' ';
            }
            if (unchanged) return visible ? input : string.Empty;
        }
        var buffer = new char[Math.Min(input.Length, maxLength)];
        var count = 0;
        var hasName = false;
        foreach (var ch in input)
        {
            if (count == buffer.Length) break;
            if (!char.IsLetterOrDigit(ch) && ch != ' ') continue;
            buffer[count++] = ch;
            hasName |= ch != ' ';
        }
        return hasName ? new string(buffer, 0, count) : string.Empty;
    }

    internal static string Lan(string? input, int slot)
    {
        var name = Sanitize(input, 32);
        return name.Length == 0 ? $"Player #{slot}" : name;
    }
}

// Display text never becomes a source. Reused slots/client IDs must also match
// their current player object and Steam identity before a stored name is used.
internal sealed class PlayerNameRegistry
{
    private sealed class Entry
    {
        internal readonly WeakReference<object> Player;
        internal readonly ulong SteamId;
        internal readonly string Name;
        internal Entry(object player, ulong steamId, string name)
        { Player = new WeakReference<object>(player); SteamId = steamId; Name = name; }
    }
    private readonly Dictionary<ulong, Entry> _names = new();
    private ConditionalWeakTable<object, Pending> _pending = new();
    private sealed class Pending
    {
        internal readonly string Name;
        internal Pending(string name) => Name = name;
    }
    internal void Stage(object player, string name)
    {
        _pending.Remove(player);
        _pending.Add(player, new Pending(name));
    }
    internal bool BindPending(object player, ulong clientId, ulong steamId)
    {
        if (!_pending.TryGetValue(player, out var pending)) return false;
        Set(player, clientId, steamId, pending.Name);
        return true;
    }
    internal void Set(object player, ulong clientId, ulong steamId, string name)
    {
        _pending.Remove(player);
        if (TryGet(player, clientId, steamId, out var existing) && existing == name) return;
        _names[clientId] = new Entry(player, steamId, name);
    }
    internal bool TryGet(object player, ulong clientId, ulong steamId, out string name)
    {
        name = string.Empty;
        if (!_names.TryGetValue(clientId, out var entry) || entry.SteamId != steamId ||
            !entry.Player.TryGetTarget(out var owner) || !ReferenceEquals(owner, player)) return false;
        name = entry.Name;
        return true;
    }
    internal void Remove(ulong clientId)
    {
        if (_names.TryGetValue(clientId, out var entry) && entry.Player.TryGetTarget(out var player)) _pending.Remove(player);
        _names.Remove(clientId);
    }
    internal void Clear() { _names.Clear(); _pending = new(); }
}
