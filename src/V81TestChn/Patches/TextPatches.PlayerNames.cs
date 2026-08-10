using HarmonyLib;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static void PlayerControllerBSendNewPlayerValuesClientRpcPrefix(
        PlayerControllerB __instance,
        object[] __args,
        out int __state)
    {
        __state = PlayerNameDiagnosticService.BeginRpc(__instance, __args);
    }

    private static void PlayerControllerBSendNewPlayerValuesClientRpcPostfix(
        PlayerControllerB __instance,
        int __state)
    {
        PlayerNameDiagnosticService.EndRpc(__instance, __state);
    }

    private static IEnumerable<CodeInstruction> PlayerControllerBSendNewPlayerValuesClientRpcTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var vanillaSanitizer = AccessTools.Method(
            typeof(PlayerControllerB),
            "NoPunctuation",
            new[] { typeof(string) })
            ?? throw new MissingMethodException(typeof(PlayerControllerB).FullName, "NoPunctuation");
        var preservingSanitizer = AccessTools.Method(
            typeof(TextPatches),
            nameof(PlayerControllerBNoPunctuationPreservingDigits))
            ?? throw new MissingMethodException(typeof(TextPatches).FullName, nameof(PlayerControllerBNoPunctuationPreservingDigits));
        var vanillaDuplicateCounter = AccessTools.Method(
            typeof(PlayerControllerB),
            "GetNumberOfDuplicateNamesInLobby",
            Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(PlayerControllerB).FullName, "GetNumberOfDuplicateNamesInLobby");
        var exactNameDisplayCounter = AccessTools.Method(
            typeof(TextPatches),
            nameof(PlayerControllerBUseExactPlayerName))
            ?? throw new MissingMethodException(typeof(TextPatches).FullName, nameof(PlayerControllerBUseExactPlayerName));

        var sanitizerReplaced = 0;
        var suffixReplaced = 0;
        var duplicateCounterReplaced = 0;
        foreach (var instruction in instructions)
        {
            // The vanilla helper keeps only char.IsLetter, so a legitimate
            // numeric suffix is lost before the short-name branch runs. Keep
            // the same punctuation policy while also retaining real digits.
            if (instruction.Calls(vanillaSanitizer))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = preservingSanitizer;
                sanitizerReplaced++;
            }

            // V81 appends a literal zero to every sanitized Steam name whose
            // UTF-16 length is one or two. Replace only that scoped literal;
            // the later duplicate-name numbering remains unchanged.
            if (instruction.opcode == OpCodes.Ldstr &&
                string.Equals(instruction.operand as string, "0", StringComparison.Ordinal))
            {
                instruction.operand = string.Empty;
                suffixReplaced++;
            }

            // V81 invokes this instance method on the local PlayerControllerB
            // while rebuilding every player's row. One duplicate of the local
            // name therefore appends the same number to all rows and radar
            // targets (for example: 测试, 测试, 测试0 becomes 测试1, 测试1,
            // 测试01). Consume the original instance but return zero so every
            // surface receives the exact synchronized player name. This also
            // avoids guessing whether a real trailing digit is synthetic.
            if (instruction.Calls(vanillaDuplicateCounter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = exactNameDisplayCounter;
                duplicateCounterReplaced++;
            }

            yield return instruction;
        }

        if (sanitizerReplaced != 1 || suffixReplaced != 1 || duplicateCounterReplaced != 1)
        {
            throw new InvalidOperationException(
                "Expected one player-name sanitizer call, one short-name zero suffix literal, and one global duplicate counter call; " +
                $"replaced sanitizer={sanitizerReplaced}, suffix={suffixReplaced}, duplicateCounter={duplicateCounterReplaced}.");
        }
    }

    private static string PlayerControllerBNoPunctuationPreservingDigits(
        PlayerControllerB _,
        string input)
    {
        var output = SanitizePlayerNamePreservingDigits(input);
        PlayerNameDiagnosticService.LogSanitizer(input, output);
        return output;
    }

    private static int PlayerControllerBUseExactPlayerName(PlayerControllerB instance)
    {
        PlayerNameDiagnosticService.LogDuplicateCounter(instance);
        return 0;
    }

    private static string SanitizePlayerNamePreservingDigits(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var buffer = new char[input.Length];
        var count = 0;
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[count++] = ch;
            }
        }

        return count == 0 ? string.Empty : new string(buffer, 0, count);
    }
}
