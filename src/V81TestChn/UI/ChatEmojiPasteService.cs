using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;

namespace V81TestChn;

internal static class ChatEmojiPasteService
{
    private static readonly FieldInfo? IsStringPositionDirtyField =
        AccessTools.Field(typeof(TMP_InputField), "m_IsStringPositionDirty");
    private static Terminal? _terminal;
    private static TMP_InputField? _terminalInputField;

    public static void RegisterTerminalInput(Terminal? terminal)
    {
        _terminal = terminal;
        _terminalInputField = terminal?.screenText;
    }

    public static void Clear()
    {
        _terminal = null;
        _terminalInputField = null;
    }

    public static bool TryHandlePaste(TMP_InputField? inputField, string? input)
    {
        var isTerminalInput = IsTerminalInput(inputField);
        if (Plugin.IsRuntimeShuttingDown ||
            inputField == null ||
            !IsSupportedInputField(inputField) ||
            string.IsNullOrEmpty(input) ||
            (!isTerminalInput && HasCharacterValidation(inputField)) ||
            !ContainsSupportedEmoji(input))
        {
            return false;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var paste = FilterPasteCharacters(input);
        var current = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, current.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, current.Length);
        var selectionStart = Math.Min(anchor, focus);
        var selectionEnd = Math.Max(anchor, focus);
        ExpandSelectionToEmojiBoundaries(current, ref selectionStart, ref selectionEnd);
        var withoutSelection = current.Remove(selectionStart, selectionEnd - selectionStart);

        if (!isTerminalInput && inputField.characterLimit > 0)
        {
            var available = Math.Max(0, inputField.characterLimit - withoutSelection.Length);
            paste = TruncateWithoutSplittingSurrogatePair(paste, available);
        }

        var updated = withoutSelection.Insert(selectionStart, paste);
        inputField.text = updated;

        var newPosition = selectionStart + paste.Length;
        SetStringSelection(inputField, newPosition, newPosition);
        return true;
    }

    public static bool TryHandleTerminalTextChanged(Terminal? terminal, string? newText)
    {
        if (Plugin.IsRuntimeShuttingDown ||
            terminal == null ||
            !ReferenceEquals(terminal, _terminal) ||
            !ReferenceEquals(terminal.screenText, _terminalInputField) ||
            terminal.currentNode == null ||
            newText == null)
        {
            return false;
        }

        if (terminal.modifyingText)
        {
            terminal.modifyingText = false;
            return true;
        }

        var currentText = terminal.currentText ?? string.Empty;
        var previousTextAdded = Math.Clamp(terminal.textAdded, 0, currentText.Length);
        var baseLength = currentText.Length - previousTextAdded;
        if (newText.Length < baseLength ||
            !newText.AsSpan(0, baseLength).SequenceEqual(currentText.AsSpan(0, baseLength)))
        {
            return false;
        }

        var appendedText = newText.Substring(baseLength);
        if (!ContainsSupportedEmoji(appendedText))
        {
            return false;
        }

        if (CountTerminalInputCharacters(appendedText) > terminal.currentNode.maxCharactersToType)
        {
            terminal.screenText.text = currentText;
            return true;
        }

        terminal.textAdded = appendedText.Length;
        terminal.currentText = newText;
        return true;
    }

    public static bool TryGetSelectedText(TMP_InputField? inputField, out string selectedText)
    {
        selectedText = string.Empty;
        if (Plugin.IsRuntimeShuttingDown || inputField == null || !IsSupportedInputField(inputField))
        {
            return false;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var value = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        var selectionStart = Math.Min(anchor, focus);
        var selectionEnd = Math.Max(anchor, focus);
        ExpandSelectionToEmojiBoundaries(value, ref selectionStart, ref selectionEnd);
        selectedText = value.Substring(selectionStart, selectionEnd - selectionStart);
        return ContainsSupportedEmoji(selectedText);
    }

    public static bool TryHandleBackspace(TMP_InputField? inputField)
    {
        if (TryHandleDeleteSelection(inputField))
        {
            return true;
        }

        if (!TryGetEditableCollapsedSelection(inputField, out var value, out var position) ||
            !TryFindEmojiForBackspace(value, position, out var start, out var end))
        {
            return false;
        }

        CommitInputEdit(inputField!, value.Remove(start, end - start), start);
        return true;
    }

    public static bool TryHandleDeleteKey(TMP_InputField? inputField)
    {
        if (TryHandleDeleteSelection(inputField))
        {
            return true;
        }

        if (!TryGetEditableCollapsedSelection(inputField, out var value, out var position) ||
            !TryFindEmojiForDelete(value, position, out var start, out var end))
        {
            return false;
        }

        CommitInputEdit(inputField!, value.Remove(start, end - start), start);
        return true;
    }

    public static bool TryHandleDeleteSelection(TMP_InputField? inputField)
    {
        if (Plugin.IsRuntimeShuttingDown ||
            inputField == null ||
            inputField.readOnly ||
            !IsSupportedInputField(inputField))
        {
            return false;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var value = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        if (anchor == focus)
        {
            return false;
        }

        var start = Math.Min(anchor, focus);
        var end = Math.Max(anchor, focus);
        ExpandSelectionToEmojiBoundaries(value, ref start, ref end);
        if (!ContainsSupportedEmoji(value.Substring(start, end - start)))
        {
            return false;
        }

        CommitInputEdit(inputField, value.Remove(start, end - start), start);
        return true;
    }

    public static void ExpandSelectionForDeletion(TMP_InputField? inputField)
    {
        if (Plugin.IsRuntimeShuttingDown || inputField == null || !IsSupportedInputField(inputField))
        {
            return;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var value = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        if (anchor == focus)
        {
            return;
        }

        var start = Math.Min(anchor, focus);
        var end = Math.Max(anchor, focus);
        var originalStart = start;
        var originalEnd = end;
        ExpandSelectionToEmojiBoundaries(value, ref start, ref end);
        if (start == originalStart && end == originalEnd)
        {
            return;
        }

        if (anchor <= focus)
        {
            SetStringSelection(inputField, start, end);
        }
        else
        {
            SetStringSelection(inputField, end, start);
        }
    }

    public static bool TryHandleMove(TMP_InputField? inputField, bool moveRight, bool shift, bool ctrl)
    {
        if (Plugin.IsRuntimeShuttingDown ||
            inputField == null ||
            !IsSupportedInputField(inputField) ||
            ctrl)
        {
            return false;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var value = inputField.text ?? string.Empty;
        if (!ContainsSupportedEmoji(value))
        {
            return false;
        }

        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        int target;
        if (anchor != focus && !shift)
        {
            target = moveRight ? Math.Max(anchor, focus) : Math.Min(anchor, focus);
            target = SnapCollapsedCaret(value, target, preferEnd: moveRight);
        }
        else if (moveRight)
        {
            target = TryFindEmojiForDelete(value, focus, out _, out var end)
                ? end
                : NextCodePointBoundary(value, focus);
        }
        else
        {
            target = TryFindEmojiForBackspace(value, focus, out var start, out _)
                ? start
                : PreviousCodePointBoundary(value, focus);
        }

        var targetAnchor = inputField.selectionStringAnchorPosition;
        if (!shift)
        {
            targetAnchor = target;
        }

        SetStringSelection(inputField, targetAnchor, target);
        return true;
    }

    public static void NormalizeSelectionToEmojiBoundaries(TMP_InputField? inputField)
    {
        if (Plugin.IsRuntimeShuttingDown || inputField == null || !IsSupportedInputField(inputField))
        {
            return;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        var value = inputField.text ?? string.Empty;
        if (!ContainsSupportedEmoji(value))
        {
            return;
        }

        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        if (anchor == focus)
        {
            var normalized = SnapCollapsedCaret(value, focus, preferEnd: false);
            if (normalized != focus)
            {
                SetStringSelection(inputField, normalized, normalized);
            }

            return;
        }

        ExpandSelectionForDeletion(inputField);
    }

    private static bool IsSupportedInputField(TMP_InputField inputField)
    {
        return ReferenceEquals(inputField, HUDManager.Instance?.chatTextField) ||
               ReferenceEquals(inputField, _terminalInputField) ||
               ChatEmojiSpriteService.OwnsTextBinding(inputField.textComponent);
    }

    private static bool IsTerminalInput(TMP_InputField? inputField)
    {
        return inputField != null &&
               ReferenceEquals(inputField, _terminalInputField) &&
               ReferenceEquals(_terminal?.screenText, inputField);
    }

    private static bool HasCharacterValidation(TMP_InputField inputField)
    {
        return inputField.characterValidation != TMP_InputField.CharacterValidation.None ||
               inputField.onValidateInput != null;
    }

    internal static int CountTerminalInputCharacters(string value)
    {
        var count = 0;
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                count++;
                index += consumedLength;
                continue;
            }

            var codeUnitLength = ChatEmojiCatalog.ReadCodePoint(value, index, out _);
            index += codeUnitLength;
            count += codeUnitLength;
        }

        return count;
    }

    internal static int CountSignalTranslatorCharacters(string value)
    {
        var count = 0;
        var start = SkipLeadingWhiteSpace(value, 0);
        for (var index = start; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                count++;
                index += consumedLength;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
            count++;
        }

        return count;
    }

    internal static string TruncateSignalTranslatorMessage(string value, int startIndex, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(value) || maximumCharacters <= 0)
        {
            return string.Empty;
        }

        var start = SkipLeadingWhiteSpace(value, Math.Clamp(startIndex, 0, value.Length));
        var end = start;
        var count = 0;
        while (end < value.Length && count < maximumCharacters)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, end, out var consumedLength, out _))
            {
                end += consumedLength;
            }
            else
            {
                end += ChatEmojiCatalog.ReadCodePoint(value, end, out _);
            }

            count++;
        }

        return value.Substring(start, end - start);
    }

    private static void ExpandSelectionToEmojiBoundaries(string value, ref int selectionStart, ref int selectionEnd)
    {
        if (selectionStart == selectionEnd || value.Length == 0)
        {
            return;
        }

        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                var matchEnd = index + consumedLength;
                if (index < selectionEnd && matchEnd > selectionStart)
                {
                    selectionStart = Math.Min(selectionStart, index);
                    selectionEnd = Math.Max(selectionEnd, matchEnd);
                }

                index = matchEnd;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }
    }

    private static bool TryGetEditableCollapsedSelection(TMP_InputField? inputField, out string value, out int position)
    {
        value = string.Empty;
        position = 0;
        if (Plugin.IsRuntimeShuttingDown ||
            inputField == null ||
            inputField.readOnly ||
            !IsSupportedInputField(inputField))
        {
            return false;
        }

        SynchronizeStringSelectionFromCaret(inputField);
        value = inputField.text ?? string.Empty;
        var anchor = Math.Clamp(inputField.selectionStringAnchorPosition, 0, value.Length);
        var focus = Math.Clamp(inputField.selectionStringFocusPosition, 0, value.Length);
        if (anchor != focus)
        {
            return false;
        }

        position = focus;
        return true;
    }

    private static bool TryFindEmojiForBackspace(string value, int position, out int start, out int end)
    {
        start = 0;
        end = 0;
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                var matchEnd = index + consumedLength;
                if (index < position && position <= matchEnd)
                {
                    start = index;
                    end = matchEnd;
                    return true;
                }

                if (index >= position)
                {
                    return false;
                }

                index = matchEnd;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        return false;
    }

    private static bool TryFindEmojiForDelete(string value, int position, out int start, out int end)
    {
        start = 0;
        end = 0;
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                var matchEnd = index + consumedLength;
                if (index <= position && position < matchEnd)
                {
                    start = index;
                    end = matchEnd;
                    return true;
                }

                if (index > position)
                {
                    return false;
                }

                index = matchEnd;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        return false;
    }

    private static int SnapCollapsedCaret(string value, int position, bool preferEnd)
    {
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out _))
            {
                var matchEnd = index + consumedLength;
                if (index < position && position < matchEnd)
                {
                    if (preferEnd)
                    {
                        return matchEnd;
                    }

                    return position - index <= matchEnd - position ? index : matchEnd;
                }

                index = matchEnd;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        return position;
    }

    private static int PreviousCodePointBoundary(string value, int position)
    {
        position = Math.Clamp(position, 0, value.Length);
        if (position == 0)
        {
            return 0;
        }

        var target = position - 1;
        if (target > 0 && char.IsLowSurrogate(value[target]) && char.IsHighSurrogate(value[target - 1]))
        {
            target--;
        }

        return target;
    }

    private static int NextCodePointBoundary(string value, int position)
    {
        position = Math.Clamp(position, 0, value.Length);
        if (position >= value.Length)
        {
            return value.Length;
        }

        return position + ChatEmojiCatalog.ReadCodePoint(value, position, out _);
    }

    private static int SkipLeadingWhiteSpace(string value, int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position]))
        {
            position++;
        }

        return position;
    }

    private static void CommitInputEdit(TMP_InputField inputField, string value, int position)
    {
        inputField.text = value;
        SetStringSelection(inputField, position, position);
    }

    private static void SynchronizeStringSelectionFromCaret(TMP_InputField inputField)
    {
        if (IsStringPositionDirtyField?.GetValue(inputField) is not true)
        {
            return;
        }

        var textInfo = inputField.textComponent?.textInfo;
        if (textInfo == null || textInfo.characterCount <= 0)
        {
            return;
        }

        var valueLength = (inputField.text ?? string.Empty).Length;
        var anchor = GetStringIndexFromCaretPosition(textInfo, inputField.selectionAnchorPosition, valueLength);
        var focus = GetStringIndexFromCaretPosition(textInfo, inputField.selectionFocusPosition, valueLength);
        SetStringSelection(inputField, anchor, focus);
    }

    private static int GetStringIndexFromCaretPosition(TMP_TextInfo textInfo, int caretPosition, int valueLength)
    {
        var characterIndex = Math.Clamp(caretPosition, 0, textInfo.characterCount - 1);
        return Math.Clamp(textInfo.characterInfo[characterIndex].index, 0, valueLength);
    }

    private static void SetStringSelection(TMP_InputField inputField, int anchor, int focus)
    {
        inputField.selectionStringAnchorPosition = anchor;
        inputField.selectionStringFocusPosition = focus;
        IsStringPositionDirtyField?.SetValue(inputField, false);
    }

    internal static bool ContainsSupportedEmoji(string value)
    {
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out _, out _))
            {
                return true;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
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
