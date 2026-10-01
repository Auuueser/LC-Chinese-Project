using System;
using System.Collections.Generic;
using System.Text;

namespace V81TestChn;

internal static class SpeechText
{
    internal const int MessageLimit = 49;
    internal const int MaximumTextLength = 4096;
    internal const double SendInterval = 0.35;

    internal static string Clean(string text)
    {
        if (text.Length > MaximumTextLength) throw new InvalidOperationException("Speech result exceeds its limit.");
        var output = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<' && i + 1 < text.Length && text[i + 1] == '|')
            {
                var end = text.IndexOf("|>", i + 2, StringComparison.Ordinal);
                if (end >= 0) { i = end + 1; continue; }
            }
            var c = text[i];
            if (char.IsControl(c)) { if (char.IsWhiteSpace(c)) output.Append(' '); continue; }
            output.Append(c == '<' ? '〈' : c == '>' ? '〉' : c);
        }
        return output.ToString().Trim();
    }

    internal static bool IsCommand(string text)
        => text.Length != 0 && (text[0] == '/' || text[0] == '!');

    internal static List<string> Split(string text)
    {
        var messages = new List<string>();
        for (var start = 0; start < text.Length;)
        {
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            if (start == text.Length) break;
            var end = Math.Min(start + MessageLimit, text.Length);
            if (end < text.Length)
            {
                if (char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
                for (var i = end - 1; i > start + MessageLimit / 2; i--)
                    if (char.IsWhiteSpace(text[i]) || "。！？；，.!?;,".IndexOf(text[i]) >= 0) { end = i + 1; break; }
            }
            var message = text.Substring(start, end - start).Trim();
            // Splitting must not turn ordinary text into a command on the receiver.
            if (IsCommand(message)) throw new InvalidOperationException("Speech result contains a command segment.");
            if (message.Length != 0) messages.Add(message);
            start = end;
        }
        return messages;
    }
}
