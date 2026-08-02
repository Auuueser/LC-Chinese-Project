using System;
using System.Collections.Generic;
using System.Text;
using TMPro;

namespace V81TestChn;

internal static class ChatEmojiPasteService
{
    private static readonly HashSet<uint> SupportedUnicodes = new(ChatEmojiCatalog.UnicodeValues);
    private static TMP_InputField? _terminalInputField;

    public static void RegisterTerminalInput(Terminal? terminal)
    {
        _terminalInputField = terminal?.screenText;
    }

    public static void Clear()
    {
        _terminalInputField = null;
    }

    public static bool TryHandlePaste(TMP_InputField? inputField, string? input)
    {
        if (Plugin.IsRuntimeShuttingDown ||
            inputField == null ||
            !IsSupportedInputField(inputField) ||
            string.IsNullOrEmpty(input) ||
            inputField.characterValidation != TMP_InputField.CharacterValidation.None ||
            inputField.onValidateInput != null ||
            !ContainsSupportedEmoji(input))
        {
            return false;
        }

        var paste = FilterPasteCharacters(input);
        var current = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, current.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, current.Length);
        var selectionStart = Math.Min(anchor, focus);
        var selectionEnd = Math.Max(anchor, focus);
        var withoutSelection = current.Remove(selectionStart, selectionEnd - selectionStart);

        if (inputField.characterLimit > 0)
        {
            var available = Math.Max(0, inputField.characterLimit - withoutSelection.Length);
            paste = TruncateWithoutSplittingSurrogatePair(paste, available);
        }

        var updated = withoutSelection.Insert(selectionStart, paste);
        inputField.text = updated;

        var newPosition = selectionStart + paste.Length;
        inputField.selectionStringAnchorPosition = newPosition;
        inputField.selectionStringFocusPosition = newPosition;
        return true;
    }

    private static bool IsSupportedInputField(TMP_InputField inputField)
    {
        return ReferenceEquals(inputField, HUDManager.Instance?.chatTextField) ||
               ReferenceEquals(inputField, _terminalInputField);
    }

    internal static bool ContainsSupportedEmoji(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            uint unicode;
            var ch = value[index];
            if (char.IsHighSurrogate(ch) &&
                index + 1 < value.Length &&
                char.IsLowSurrogate(value[index + 1]))
            {
                unicode = (uint)char.ConvertToUtf32(ch, value[++index]);
            }
            else if (!char.IsSurrogate(ch))
            {
                unicode = ch;
            }
            else
            {
                continue;
            }

            if (SupportedUnicodes.Contains(unicode))
            {
                return true;
            }
        }

        return false;
    }

    internal static string FilterPasteCharacters(string value)
    {
        var filtered = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (char.IsHighSurrogate(ch))
            {
                if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    filtered.Append(ch);
                    filtered.Append(value[++index]);
                }

                continue;
            }

            if (char.IsLowSurrogate(ch))
            {
                continue;
            }

            if (ch >= ' ' || ch is '\t' or '\r' or '\n')
            {
                filtered.Append(ch);
            }
        }

        return filtered.ToString();
    }

    internal static string TruncateWithoutSplittingSurrogatePair(string value, int maximumLength)
    {
        if (maximumLength <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maximumLength)
        {
            return value;
        }

        var length = maximumLength;
        if (char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value.Substring(0, length);
    }
}
