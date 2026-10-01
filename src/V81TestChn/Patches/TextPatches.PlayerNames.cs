using HarmonyLib;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static FieldInfo? _lanClientIdField;
    private static FieldInfo? _lanPlayerNameField;

    private static void InstallLobbyImprovementsNameSources(Harmony harmony, ref int patched)
    {
        var type = FindLoadedTypeQuiet("LobbyImprovements.SessionTickets_Client");
        if (type == null) return;
        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var parameters = method.GetParameters();
            if (method.Name != "UpdatedPlayerInfo" || parameters.Length != 1 || parameters[0].ParameterType.Name != "CL_LANPlayer") continue;
            var infoType = parameters[0].ParameterType;
            _lanClientIdField = AccessTools.Field(infoType, "actualClientId");
            _lanPlayerNameField = AccessTools.Field(infoType, "playerName");
            if (_lanClientIdField?.FieldType != typeof(ulong) || _lanPlayerNameField?.FieldType != typeof(string))
            {
                Plugin.Log.LogWarning("LobbyImprovements LAN name schema changed; source adapter skipped.");
                return;
            }
            PatchPostfix(harmony, method, nameof(LobbyImprovementsLanUpdatePostfix), ref patched, Priority.Last);
            PatchOptionalPostfix(harmony, type.FullName!, "ConnectClientToPlayerObject_Postfix", nameof(LobbyImprovementsLanConnectPostfix), ref patched, Priority.Last);
            return;
        }
        Plugin.Log.LogWarning("LobbyImprovements LAN name update overload missing; source adapter skipped.");
    }
    private static void PlayerControllerBSendNewPlayerValuesClientRpcPrefix(
        PlayerControllerB __instance,
        ulong[] playerSteamIds,
        out int __state)
    {
        __state = PlayerNameDiagnosticService.BeginRpc(__instance, playerSteamIds);
    }

    private static void PlayerControllerBSendNewPlayerValuesClientRpcPostfix(
        PlayerControllerB __instance,
        int __state)
    {
        SteamPlayerNameService.RefreshConnectedPlayers();
        PlayerNameDiagnosticService.EndRpc(__instance, __state);
    }

    private static void PlayerNameSteamConnectPrefix() => SteamPlayerNameService.RefreshLocalName();

    private static IEnumerable<CodeInstruction> PlayerControllerBSendNewPlayerValuesClientRpcTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var vanillaSanitizer = NameMethod(typeof(PlayerControllerB), "NoPunctuation", new[] { typeof(string) });
        var preservingSanitizer = NameMethod(typeof(TextPatches), nameof(PlayerControllerBNoPunctuationPreservingDigits));
        var vanillaDuplicateCounter = NameMethod(typeof(PlayerControllerB), "GetNumberOfDuplicateNamesInLobby", Type.EmptyTypes);
        var exactNameDisplayCounter = NameMethod(typeof(TextPatches), nameof(PlayerControllerBUseExactPlayerName));

        var code = new List<CodeInstruction>();
        foreach (var instruction in instructions) code.Add(new CodeInstruction(instruction));
        var sanitizer = new List<int>();
        var suffix = new List<int>();
        var duplicate = new List<int>();
        var regex = new List<int>();
        var stores = new List<int>();
        var replace = NameMethod(typeof(Regex), nameof(Regex.Replace), new[] { typeof(string), typeof(string), typeof(string) });
        var length = typeof(string).GetProperty(nameof(string.Length))!.GetMethod!;
        var concat = NameMethod(typeof(string), nameof(string.Concat), new[] { typeof(string), typeof(string) });
        for (var i = 0; i < code.Count; i++)
        {
            if (code[i].Calls(vanillaSanitizer)) sanitizer.Add(i);
            if (code[i].Calls(vanillaDuplicateCounter)) duplicate.Add(i);
            if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo field &&
                field.DeclaringType == typeof(PlayerControllerB) && field.Name == nameof(PlayerControllerB.playerUsername)) stores.Add(i);
            if (i >= 2 && code[i].Calls(replace) && code[i - 2].opcode == OpCodes.Ldstr &&
                Equals(code[i - 2].operand, @"[^\w\._]") && code[i - 1].opcode == OpCodes.Ldstr && Equals(code[i - 1].operand, "")) regex.Add(i);
            // Match the length <= 2 branch and its same-local append/store,
            // never an unrelated literal zero elsewhere in this RPC.
            if (i >= 5 && i + 2 < code.Count && code[i].opcode == OpCodes.Ldstr && Equals(code[i].operand, "0") &&
                code[i - 4].Calls(length) && code[i - 3].opcode == OpCodes.Ldc_I4_2 &&
                (code[i - 2].opcode == OpCodes.Bgt || code[i - 2].opcode == OpCodes.Bgt_S) &&
                code[i + 1].Calls(concat) && LoadLocal(code[i - 5]) >= 0 &&
                LoadLocal(code[i - 5]) == LoadLocal(code[i - 1]) && LoadLocal(code[i - 1]) == StoreLocal(code[i + 2])) suffix.Add(i);
        }
        if (sanitizer.Count != 1 || suffix.Count != 1 || duplicate.Count != 1 || regex.Count != 1 || stores.Count != 1 ||
            !(sanitizer[0] < regex[0] && regex[0] < suffix[0] && suffix[0] < stores[0] && stores[0] < duplicate[0]))
            throw new InvalidOperationException("Player-name RPC layout changed; no partial name rewrite is allowed.");

        code[sanitizer[0]].opcode = OpCodes.Call;
        code[sanitizer[0]].operand = preservingSanitizer;
        code[suffix[0]].operand = string.Empty;
        code[duplicate[0]].opcode = OpCodes.Call;
        code[duplicate[0]].operand = exactNameDisplayCounter;
        code[regex[0]].opcode = OpCodes.Call;
        code[regex[0]].operand = NameMethod(typeof(TextPatches), nameof(PreserveSanitizedPlayerName));
        code[stores[0]].opcode = OpCodes.Call;
        code[stores[0]].operand = NameMethod(typeof(PlayerNameSourceService), nameof(PlayerNameSourceService.Capture));
        return code;
    }

    private static MethodInfo NameMethod(Type type, string name, Type[]? parameters = null)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return (parameters == null ? type.GetMethod(name, flags) : type.GetMethod(name, flags, null, parameters, null))
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private static int LoadLocal(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Ldloc_0) return 0;
        if (code.opcode == OpCodes.Ldloc_1) return 1;
        if (code.opcode == OpCodes.Ldloc_2) return 2;
        if (code.opcode == OpCodes.Ldloc_3) return 3;
        return code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloc_S ? LocalOperandIndex(code.operand) : -1;
    }
    private static int StoreLocal(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Stloc_0) return 0;
        if (code.opcode == OpCodes.Stloc_1) return 1;
        if (code.opcode == OpCodes.Stloc_2) return 2;
        if (code.opcode == OpCodes.Stloc_3) return 3;
        return code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S ? LocalOperandIndex(code.operand) : -1;
    }
    private static int LocalOperandIndex(object operand) => operand is LocalBuilder local ? local.LocalIndex :
        operand is LocalVariableInfo variable ? variable.LocalIndex : Convert.ToInt32(operand);
    private static string PreserveSanitizedPlayerName(string input, string pattern, string replacement) => input;

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

    private static string SanitizePlayerNamePreservingDigits(string? input) => PlayerNamePolicy.Sanitize(input);

    private static void PlayerNameSlotPrefix(QuickMenuManager __instance, int playerObjectId)
        => PlayerNameSourceService.RegisterSlot(__instance, playerObjectId);
    private static void PlayerNameSlotPostfix(QuickMenuManager __instance, int playerObjectId)
        => PlayerNameSourceService.ApplySlot(__instance, playerObjectId);
    private static void PlayerNamesDisconnectedPostfix(ulong clientId) => PlayerNameSourceService.Remove(clientId);
    private static void PlayerNamesResetPostfix() => PlayerNameSourceService.Clear();

    private static void LobbyImprovementsLanUpdatePostfix(object __0)
    {
        // This overload is installed only for CL_LANPlayer. Its original raw
        // name, not the radar or a suffixed UI string, is the trusted source.
        if (GameNetworkManager.Instance == null || !GameNetworkManager.Instance.disableSteam ||
            __0 == null || _lanClientIdField == null || _lanPlayerNameField == null) return;
        var clientId = (ulong)_lanClientIdField.GetValue(__0);
        var rawName = _lanPlayerNameField.GetValue(__0) as string;
        var round = StartOfRound.Instance;
        var players = round?.allPlayerScripts;
        if (round == null || players == null || !round.ClientPlayerList.TryGetValue(clientId, out var slot) ||
            slot < 0 || slot >= players.Length) return;
        var player = players[slot];
        if (player == null || player.actualClientId != clientId) return;
        PlayerNameSourceService.Capture(player, PlayerNamePolicy.Lan(rawName, slot));
        PlayerNameSourceService.Apply(player);
        PlayerNameDiagnosticService.LogQuickMenu(player.quickMenuManager, "LobbyImprovements.UpdatedPlayerInfo.LAN");
    }
    private static void LobbyImprovementsLanConnectPostfix(PlayerControllerB __0)
    {
        if (__0 == null || GameNetworkManager.Instance == null || !GameNetworkManager.Instance.disableSteam) return;
        PlayerNameSourceService.BindLanConnection(__0);
        PlayerNameDiagnosticService.LogQuickMenu(__0.quickMenuManager, "LobbyImprovements.ConnectClient.LAN");
    }
}
