using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

internal static class PlayerNameDiagnosticService
{
    private const string ConfigSection = "90 诊断 - 玩家名称";
    private const string LogPrefix = "[PlayerNameDiag]";
    private static bool _enabled;
    private static int _remainingLogs;
    private static int _rpcSequence;
    private static bool _patchOwnersLogged;
    private static string _lastDisplaySignature = string.Empty;

    internal static void Initialize(ConfigFile config)
    {
        var enabled = config.Bind(
            ConfigSection,
            "EnablePlayerNameDiagnostics",
            false,
            "记录玩家名称同步、清洗、重名计数、ESC 列表和雷达显示的限量诊断。仅排查名称尾随数字时开启。");
        var budget = config.Bind(
            ConfigSection,
            "PlayerNameDiagnosticLogBudget",
            160,
            new ConfigDescription(
                "单次游戏运行最多写入的玩家名称诊断日志条数。达到上限后自动停止。",
                new AcceptableValueRange<int>(20, 1000)));

        _enabled = enabled.Value;
        _remainingLogs = Math.Max(20, budget.Value);
        _rpcSequence = 0;
        _patchOwnersLogged = false;
        _lastDisplaySignature = string.Empty;
        if (_enabled)
        {
            TryLog($"enabled budget={_remainingLogs}");
        }
    }

    internal static void Clear()
    {
        _enabled = false;
        _remainingLogs = 0;
        _rpcSequence = 0;
        _patchOwnersLogged = false;
        _lastDisplaySignature = string.Empty;
    }

    internal static int BeginRpc(PlayerControllerB instance, object[] args)
    {
        if (!_enabled || !IsRemoteClientSession())
        {
            return 0;
        }

        var sequence = Interlocked.Increment(ref _rpcSequence);
        LogPatchOwnersOnce();
        TryLog($"rpc={sequence} phase=enter instanceSlot={FindPlayerSlot(instance)} args={FormatArguments(args)}");
        LogSnapshot(sequence, "enter");
        return sequence;
    }

    internal static void EndRpc(PlayerControllerB instance, int sequence)
    {
        if (!_enabled || sequence <= 0)
        {
            return;
        }

        LogSnapshot(sequence, "postfix");
        instance.StartCoroutine(CaptureNextFrame(sequence));
    }

    internal static void LogSanitizer(string? input, string output)
    {
        if (_enabled && IsRemoteClientSession())
        {
            TryLog($"sanitizer input={Quote(input)} output={Quote(output)}");
        }
    }

    internal static void LogDuplicateCounter(PlayerControllerB instance)
    {
        if (_enabled && IsRemoteClientSession())
        {
            TryLog($"duplicate-counter instanceSlot={FindPlayerSlot(instance)} username={Quote(instance?.playerUsername)} forcedResult=0");
        }
    }

    internal static void LogQuickMenu(QuickMenuManager? manager, string reason)
    {
        if (!_enabled || manager == null || !IsRemoteClientSession())
        {
            return;
        }

        LogDisplaySnapshot(reason, manager);
        manager.StartCoroutine(CaptureQuickMenuNextFrame(manager, reason));
    }

    internal static void LogLobbyImprovementsPlayerList(object? instance, object[] args, string reason)
    {
        if (!_enabled)
        {
            return;
        }

        var manager = UnityEngine.Object.FindObjectOfType<QuickMenuManager>();
        if (manager == null)
        {
            return;
        }

        if (IsRemoteClientSession())
        {
            TryLog($"display-event={reason} instance={instance?.GetType().FullName ?? "<null>"} args={FormatArguments(args)}");
            LogDisplaySnapshot(reason, manager);
        }

        manager.StartCoroutine(CaptureQuickMenuNextFrame(manager, reason));
    }

    private static IEnumerator CaptureNextFrame(int sequence)
    {
        yield return null;
        if (_enabled)
        {
            LogSnapshot(sequence, "next-frame");
        }
    }

    private static IEnumerator CaptureQuickMenuNextFrame(QuickMenuManager manager, string reason)
    {
        yield return null;
        if (_enabled && manager != null)
        {
            LogDisplaySnapshot(reason + ".next-frame", manager);
            yield return null;
            if (_enabled && manager != null)
            {
                LogDisplaySnapshot(reason + ".second-frame", manager);
            }
        }
    }

    private static void LogSnapshot(int sequence, string phase)
    {
        if (!IsRemoteClientSession())
        {
            return;
        }

        LogDisplaySnapshot(
            $"rpc={sequence} phase={phase}",
            UnityEngine.Object.FindObjectOfType<QuickMenuManager>());
    }

    private static void LogDisplaySnapshot(string reason, QuickMenuManager? manager)
    {
        if (!_enabled || !IsRemoteClientSession())
        {
            return;
        }

        var activeSlots = CollectSynchronizedPlayers(out var synchronizedSummary);
        var playerListSummary = CollectPlayerListSlots(manager, activeSlots);
        var radarSummary = CollectRadarTargets(activeSlots);
        var signature = synchronizedSummary + "\n" + playerListSummary + "\n" + radarSummary;
        if (string.Equals(signature, _lastDisplaySignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastDisplaySignature = signature;
        TryLog(
            $"display-event={reason} role=[{DescribeNetworkRole()}] connected={GetConnectedPlayerCount()} frame={Time.frameCount} " +
            $"synchronized=[{synchronizedSummary}] playerList=[{playerListSummary}] radar=[{radarSummary}]");
    }

    private static HashSet<int> CollectSynchronizedPlayers(out string summary)
    {
        var activeSlots = new HashSet<int>();
        var rows = new List<string>();
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players != null)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var username = player?.playerUsername;
                if (player == null || string.IsNullOrEmpty(username) ||
                    (!player.isPlayerControlled && string.Equals(username, $"Player #{i}", StringComparison.Ordinal)))
                {
                    continue;
                }

                activeSlots.Add(i);
                rows.Add($"{i}:{Quote(username)}:controlled={player.isPlayerControlled}:dead={player.isPlayerDead}");
            }
        }

        summary = string.Join(" | ", rows);
        return activeSlots;
    }

    private static string CollectPlayerListSlots(QuickMenuManager? manager, HashSet<int> activeSlots)
    {
        var slots = manager?.playerListSlots;
        if (slots == null)
        {
            return "<unavailable>";
        }

        var rows = new List<string>();
        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var header = slot?.usernameHeader;
            var value = header?.text;
            var visible = slot?.slotContainer?.activeInHierarchy == true;
            if (!activeSlots.Contains(i) && !visible)
            {
                continue;
            }

            rows.Add($"{i}:{Quote(value)}:visible={visible}");
        }

        return string.Join(" | ", rows);
    }

    private static string CollectRadarTargets(HashSet<int> activeSlots)
    {
        var mapScreen = StartOfRound.Instance?.mapScreen;
        if (mapScreen == null)
        {
            return "<unavailable>";
        }

        var field = AccessTools.Field(mapScreen.GetType(), "radarTargets");
        if (field?.GetValue(mapScreen) is not IEnumerable targets)
        {
            return "<unavailable>";
        }

        var rows = new List<string>();
        var index = 0;
        foreach (var target in targets)
        {
            if (target != null && activeSlots.Contains(index))
            {
                var nameField = AccessTools.Field(target.GetType(), "name");
                rows.Add($"{index}:{Quote(nameField?.GetValue(target) as string)}");
            }

            index++;
        }

        return string.Join(" | ", rows);
    }

    private static int GetConnectedPlayerCount()
    {
        return Math.Max(0, (StartOfRound.Instance?.connectedPlayersAmount ?? -1) + 1);
    }

    private static bool IsRemoteClientSession()
    {
        var round = StartOfRound.Instance;
        return round != null &&
               GetConnectedPlayerCount() >= 2 &&
               ReadNetworkFlag(round, "IsClient") &&
               !ReadNetworkFlag(round, "IsHost") &&
               !ReadNetworkFlag(round, "IsServer");
    }

    private static bool ReadNetworkFlag(object instance, string propertyName)
    {
        return AccessTools.Property(instance.GetType(), propertyName)?.GetValue(instance) is true;
    }

    private static string DescribeNetworkRole()
    {
        var round = StartOfRound.Instance;
        if (round == null)
        {
            return "unavailable";
        }

        var type = round.GetType();
        var networkManager = AccessTools.Property(type, "NetworkManager")?.GetValue(round);
        var localClientId = networkManager == null
            ? null
            : AccessTools.Property(networkManager.GetType(), "LocalClientId")?.GetValue(networkManager);
        return
            $"client={AccessTools.Property(type, "IsClient")?.GetValue(round) ?? "?"}," +
            $"server={AccessTools.Property(type, "IsServer")?.GetValue(round) ?? "?"}," +
            $"host={AccessTools.Property(type, "IsHost")?.GetValue(round) ?? "?"}," +
            $"localClientId={localClientId ?? "?"}";
    }

    private static void LogPatchOwnersOnce()
    {
        if (_patchOwnersLogged)
        {
            return;
        }

        _patchOwnersLogged = true;
        var target = AccessTools.Method(typeof(PlayerControllerB), "SendNewPlayerValuesClientRpc");
        var info = target == null ? null : Harmony.GetPatchInfo(target);
        if (target == null || info == null)
        {
            TryLog("patches target-or-info=<unavailable>");
            return;
        }

        TryLog(
            $"patches prefixes=[{FormatPatches(info.Prefixes)}] " +
            $"postfixes=[{FormatPatches(info.Postfixes)}] " +
            $"transpilers=[{FormatPatches(info.Transpilers)}] " +
            $"finalizers=[{FormatPatches(info.Finalizers)}]");
    }

    private static string FormatPatches(IEnumerable<Patch> patches)
    {
        var rows = new List<string>();
        foreach (var patch in patches)
        {
            rows.Add($"{patch.owner}:priority={patch.priority}:index={patch.index}");
        }

        return string.Join(",", rows);
    }

    private static string FormatArguments(object[] args)
    {
        if (args == null || args.Length == 0)
        {
            return "[]";
        }

        var rows = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            rows[i] = $"{i}:{FormatArgument(args[i])}";
        }

        return "[" + string.Join(",", rows) + "]";
    }

    private static string FormatArgument(object? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        if (value is string text)
        {
            return Quote(text);
        }

        if (value is ulong)
        {
            return "<UInt64-redacted>";
        }

        if (value is Array array)
        {
            return $"<{value.GetType().Name}:length={array.Length}>";
        }

        return value is bool or byte or sbyte or short or ushort or int or uint or long or float or double
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? value.GetType().Name
            : $"<{value.GetType().Name}>";
    }

    private static int FindPlayerSlot(PlayerControllerB? target)
    {
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (target == null || players == null)
        {
            return -1;
        }

        for (var i = 0; i < players.Length; i++)
        {
            if (ReferenceEquals(players[i], target))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Quote(string? value)
    {
        if (value == null)
        {
            return "<null>";
        }

        return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n") + "'";
    }

    private static void TryLog(string message)
    {
        if (!_enabled || Interlocked.Decrement(ref _remainingLogs) < 0)
        {
            _enabled = false;
            return;
        }

        Plugin.Log.LogWarning($"{LogPrefix} {message}");
    }
}
