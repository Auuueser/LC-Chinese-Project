using System;
using System.Collections.Generic;
using System.Text;

namespace V81TestChn;

// Display-only lookup. Whitespace differences are ignored, but all words and
// punctuation must match. Never used to resolve commands or change their arguments.
internal sealed class TerminalStaticDescriptionLookup
{
    // Only the audited vanilla INFO descriptions opt into whitespace normalization.
    private static readonly string[] DescriptionPrefixes =
    {
        "The inverse teleporter is a modified teleporter which",
        "Used to communicate with any crew member from",
        "Use the \"transmit\" command to broadcast a text",
        "Press the button to activate the teleporter. It",
        "Lock-pickers will unlock your limitless potential for efficiency",
        "The most affordable light source. It's even waterproof!",
        "The Company's very own tactical belt bags are",
        "These jamming tunes are great for a morale",
        "The Company Cruiser is an entire delivery truck",
        "The extension ladder can reach as high as",
        "With an extra battery life and even brighter",
        "This device will get you around anywhere! Just",
        "The most advanced map device, using light-detection and",
        "Radar boosters come with many uses! Use the",
        "For digging! The Company does not condone violence",
        "The survival kit includes these necessities in a",
        "This safe and legal medicine can be administered",
        "Useful for keeping in touch! Hear other players",
        "Deal with those pesky weeds! Repeated, firm presses",
        "The most specialized self-protective equipment, capable of sending",
    };

    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    internal void Rebuild(IEnumerable<KeyValuePair<string, string>> entries)
    {
        _entries.Clear();
        foreach (var pair in entries)
        {
            if (pair.Key.Length < 40 || pair.Key.Length > 4096 || pair.Key.Contains('<') || !HasChinese(pair.Value)) continue;
            // Dynamic fields must first be expanded by the game. Single-key
            // control hints such as [Q] are literal text and may remain.
            var dynamicField = false;
            for (var i = 0; i < pair.Key.Length; i++)
                if (pair.Key[i] == '[' && (i + 2 >= pair.Key.Length || pair.Key[i + 2] != ']')) { dynamicField = true; break; }
            if (dynamicField) continue;
            var key = Normalize(pair.Key);
            var description = false;
            foreach (var prefix in DescriptionPrefixes)
                if (key.StartsWith(prefix, StringComparison.Ordinal)) { description = true; break; }
            if (!description) continue;
            // Later entries follow the loader precedence; explicit terminal wording is supplied last.
            _entries[key] = pair.Value.Trim();
        }
    }

    internal bool TryTranslate(string source, out string translated)
    {
        translated = source;
        if (source.Length < 40 || source.Length > 4096 || HasChinese(source)) return false;
        if (!_entries.TryGetValue(Normalize(source), out var match)) return false;
        var start = 0;
        while (start < source.Length && char.IsWhiteSpace(source[start])) start++;
        var end = source.Length;
        while (end > start && char.IsWhiteSpace(source[end - 1])) end--;
        translated = source.Substring(0, start) + match + source.Substring(end);
        return true;
    }

    private static bool HasChinese(string text)
    {
        foreach (var c in text) if (c >= '\u3400' && c <= '\u9fff') return true;
        return false;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c)) { space = builder.Length > 0; continue; }
            if (space) builder.Append(' ');
            space = false;
            builder.Append(c);
        }
        return builder.ToString();
    }
}
