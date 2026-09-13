using System;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static class StoryLogNameLocalizationService
{
    private sealed class Marker { }
    private static System.Runtime.CompilerServices.ConditionalWeakTable<TerminalNode, Marker> _logs = new();
    internal static void Register(System.Collections.Generic.IList<TerminalNode>? nodes)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
            if (node != null && !_logs.TryGetValue(node, out _)) _logs.Add(node, new Marker());
    }
    internal static bool IsLog(TerminalNode? node) => node != null && _logs.TryGetValue(node, out _);
    internal static void Clear() => _logs = new();
    private static readonly Regex Names = new(@"(?<![A-Za-z])(Sigurd|Richard|Rich|Desmond|Jess)(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30));
    // Called only on story-log display text, never player names or chat input.
    internal static string Normalize(string text) => Names.Replace(text, m => m.Value.ToLowerInvariant() switch
    {
        "sigurd" => "西格德", "richard" or "rich" => "理查德", "desmond" => "德斯蒙德", "jess" => "杰丝", _ => m.Value
    }).Replace("里奇", "理查德");
}
