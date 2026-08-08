using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static bool TmpInputFieldBackspacePrefix(TMP_InputField __instance)
    {
        return !ChatEmojiPasteService.TryHandleBackspace(__instance);
    }

    private static bool TmpInputFieldDeleteKeyPrefix(TMP_InputField __instance)
    {
        return !ChatEmojiPasteService.TryHandleDeleteKey(__instance);
    }

    private static bool TmpInputFieldDeleteSelectionPrefix(TMP_InputField __instance)
    {
        return !ChatEmojiPasteService.TryHandleDeleteSelection(__instance);
    }

    private static bool TmpInputFieldMoveLeftPrefix(TMP_InputField __instance, bool shift, bool ctrl)
    {
        return !ChatEmojiPasteService.TryHandleMove(__instance, false, shift, ctrl);
    }

    private static bool TmpInputFieldMoveRightPrefix(TMP_InputField __instance, bool shift, bool ctrl)
    {
        return !ChatEmojiPasteService.TryHandleMove(__instance, true, shift, ctrl);
    }

    private static void TmpInputFieldNormalizeSelectionPostfix(TMP_InputField __instance)
    {
        ChatEmojiPasteService.NormalizeSelectionToEmojiBoundaries(__instance);
    }

    private static IEnumerable<CodeInstruction> SignalTranslatorSubstringTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var substring = AccessTools.Method(typeof(string), nameof(string.Substring), new[] { typeof(int), typeof(int) });
        var replacement = AccessTools.Method(
            typeof(ChatEmojiPasteService),
            nameof(ChatEmojiPasteService.TruncateSignalTranslatorMessage));
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (substring != null && instruction.Calls(substring))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }

            yield return instruction;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException($"Expected one signal Substring call, replaced {replaced}.");
        }
    }

    private static IEnumerable<CodeInstruction> SignalTranslatorServerLengthTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var stringLength = AccessTools.PropertyGetter(typeof(string), nameof(string.Length));
        var replacement = AccessTools.Method(
            typeof(ChatEmojiPasteService),
            nameof(ChatEmojiPasteService.CountSignalTranslatorCharacters));
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (stringLength != null && instruction.Calls(stringLength))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }

            yield return instruction;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException($"Expected one signal length check, replaced {replaced}.");
        }
    }
}
