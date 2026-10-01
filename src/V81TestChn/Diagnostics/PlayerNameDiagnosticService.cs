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
    private static bool CanLog => _enabled && Volatile.Read(ref _remainingLogs) > 0;

    internal static void Initialize(ConfigFile config)
    {
        var enabled = config.Bind(
            ConfigSection,
            "EnablePlayerNameDiagnostics",
            false,
            "记录房主与客户端的 Steam 名称来源、更新事件、清洗、ESC 列表和雷达显示。仅排查名称异常时开启。");
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
        if (CanLog)
        {
            TryLog($"enabled budget={_remainingLogs} build={typeof(PlayerNameDiagnosticService).Assembly.ManifestModule.ModuleVersionId}");
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

    internal static void LogSteamSource(PlayerControllerB player, string reason, string? before, string? raw, string after)
    {
        if (!CanLog) return;
        var spaces = 0;
        if (raw != null) foreach (var ch in raw) if (ch == ' ') spaces++;
        TryLog($"steam-source reason={reason} client={player.actualClientId} steam={player.playerSteamId} " +
            $"before={Quote(before)} raw={Quote(raw)} length={raw?.Length ?? 0} spaces={spaces} after={Quote(after)}");
    }

    internal static void LogSteamRequest(PlayerControllerB player, bool pending)
    {
        if (CanLog) TryLog($"steam-request client={player.actualClientId} steam={player.playerSteamId} pending={pending}");
    }

    internal static void LogSteamPublish(ulong lobbyId, string name)
    {
        if (CanLog) TryLog($"steam-member-publish lobby={lobbyId} name={Quote(name)} length={name.Length}");
    }

    internal static int BeginRpc(PlayerControllerB instance, ulong[] steamIds)
    {
        if (!CanLog || !IsRemoteClientSession())
        {
            return 0;
        }

        var sequence = Interlocked.Increment(ref _rpcSequence);
        LogPatchOwnersOnce();
        if (!CanLog) return sequence;
        TryLog($"rpc={sequence} phase=enter instanceSlot={FindPlayerSlot(instance)} steamIdCount={steamIds?.Length ?? 0}");
        LogSnapshot(sequence, "enter");
        return sequence;
    }

    internal static void EndRpc(PlayerControllerB instance, int sequence)
    {
        if (!CanLog || sequence <= 0)
        {
            return;
        }

        LogSnapshot(sequence, "postfix");
        if (CanLog) instance.StartCoroutine(CaptureNextFrame(sequence));
    }

    internal static void LogSanitizer(string? input, string output)
    {
        if (CanLog && IsRemoteClientSession())
        {
            TryLog($"sanitizer input={Quote(input)} output={Quote(output)}");
        }
    }

    internal static void LogDuplicateCounter(PlayerControllerB instance)
    {
        if (CanLog && IsRemoteClientSession())
        {
            TryLog($"duplicate-counter instanceSlot={FindPlayerSlot(instance)} username={Quote(instance?.playerUsername)} forcedResult=0");
        }
    }

    internal static void LogQuickMenu(QuickMenuManager? manager, string reason)
    {
        if (!CanLog || manager == null || !IsRemoteClientSession())
        {
            return;
        }

        // Opening ESC should not synchronously allocate/format every player's
        // diagnostic snapshot. Diagnostic-only sampling begins next frame.
        manager.StartCoroutine(CaptureQuickMenuNextFrame(manager, reason));
    }

    private static IEnumerator CaptureNextFrame(int sequence)
    {
        yield return null;
        if (CanLog)
        {
            LogSnapshot(sequence, "next-frame");
        }
    }

    private static IEnumerator CaptureQuickMenuNextFrame(QuickMenuManager manager, string reason)
    {
        yield return null;
        if (CanLog && manager != null)
        {
            LogDisplaySnapshot(reason + ".next-frame", manager);
            if (!CanLog) yield break;
            yield return null;
            if (CanLog && manager != null)
            {
                LogDisplaySnapshot(reason + ".second-frame", manager);
            }
        }
    }

    private static void LogSnapshot(int sequence, string phase)
    {
        if (!CanLog || !IsRemoteClientSession())
        {
            return;
        }

        LogDisplaySnapshot(
            $"rpc={sequence} phase={phase}",
            UnityEngine.Object.FindObjectOfType<QuickMenuManager>());
    }

    private static void LogDisplaySnapshot(string reason, QuickMenuManager? manager)
    {
        if (!CanLog || !IsRemoteClientSession())
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
               ReadNetworkFlag(round, "IsClient");
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
        if (!CanLog || _patchOwnersLogged)
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
        if (!CanLog)
        {
            return;
        }

        var remaining = Interlocked.Decrement(ref _remainingLogs);
        if (remaining <= 0) _enabled = false;
        if (remaining < 0)
        {
            return;
        }

        Plugin.Log.LogWarning($"{LogPrefix} {message}");
    }
}
