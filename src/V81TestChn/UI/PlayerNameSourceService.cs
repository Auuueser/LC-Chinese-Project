using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class PlayerNameSourceService
{
    private static readonly PlayerNameRegistry Names = new();
    private static readonly Dictionary<ulong, int> RadarIndices = new();
    private static ConditionalWeakTable<Component, Binding> _components = new();
    private sealed class Binding
    {
        internal readonly WeakReference<PlayerControllerB> Player;
        internal readonly ulong ClientId;
        internal readonly Transform? Parent;
        internal Binding(PlayerControllerB player, Component text)
        { Player = new(player); ClientId = player.actualClientId; Parent = text.transform.parent; }
    }

    internal static void Clear()
    {
        SteamPlayerNameService.Clear();
        Names.Clear();
        RadarIndices.Clear();
        _components = new();
    }
    internal static void Remove(ulong clientId)
    {
        SteamPlayerNameService.Remove(clientId);
        Names.Remove(clientId);
        RadarIndices.Remove(clientId);
    }

    internal static bool IsNameComponent(Component text)
    {
        if (!_components.TryGetValue(text, out var binding)) return false;
        if (binding.Player.TryGetTarget(out var player) && player != null && player.actualClientId == binding.ClientId &&
            text.transform.parent == binding.Parent) return true;
        _components.Remove(text);
        return false;
    }

    private static void Register(TMP_Text? text, PlayerControllerB player)
    {
        if (text == null) return;
        if (_components.TryGetValue(text, out var binding) && binding.ClientId == player.actualClientId && text.transform.parent == binding.Parent &&
            binding.Player.TryGetTarget(out var owner) && ReferenceEquals(owner, player)) return;
        _components.Remove(text);
        _components.Add(text, new Binding(player, text));
    }

    internal static void Capture(PlayerControllerB player, string name)
    {
        if (player == null) return;
        name = SteamPlayerNameService.SelectCaptureName(player, name);
        Names.Set(player, player.actualClientId, player.playerSteamId, name);
        Register(player.usernameBillboardText, player);
        player.playerUsername = name;
    }

    internal static void CaptureLan(int slot, string name)
    {
        if (GameNetworkManager.Instance == null || !GameNetworkManager.Instance.disableSteam) return;
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players == null || slot < 0 || slot >= players.Length || players[slot] == null) return;
        var player = players[slot];
        // The LAN parser also runs in a connection prefix, before the game
        // assigns actualClientId. Do not register a remote player as client 0.
        Names.Stage(player, name);
        Register(player.usernameBillboardText, player);
    }

    internal static void BindLanConnection(PlayerControllerB player)
    {
        if (player == null || GameNetworkManager.Instance == null || !GameNetworkManager.Instance.disableSteam) return;
        Names.BindPending(player, player.actualClientId, player.playerSteamId);
        Apply(player);
    }

    internal static void RegisterSlot(QuickMenuManager menu, int slot)
    {
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players == null || slot < 0 || slot >= players.Length || players[slot] == null ||
            menu.playerListSlots == null || slot >= menu.playerListSlots.Length) return;
        Register(menu.playerListSlots[slot]?.usernameHeader, players[slot]);
    }

    internal static void ApplySlot(QuickMenuManager menu, int slot)
    {
        RegisterSlot(menu, slot);
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players != null && slot >= 0 && slot < players.Length) Apply(players[slot], menu);
    }

    internal static void ApplyAll(QuickMenuManager menu)
    {
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players == null) return;
        for (var i = 0; i < players.Length; i++) ApplySlot(menu, i);
    }

    internal static bool ApplyKnownName(PlayerControllerB? player, PlayerListSlot? slot = null)
    {
        if (player == null || (!player.isPlayerControlled && !player.isPlayerDead) ||
            !Names.TryGet(player, player.actualClientId, player.playerSteamId, out var name)) return false;
        if (slot == null)
        {
            var slots = player.quickMenuManager?.playerListSlots;
            if (slots != null && player.playerClientId < (ulong)slots.Length) slot = slots[(int)player.playerClientId];
        }
        // A known, already consistent row needs no fallback font checks,
        // radar lookup, source logging or UI writes when opening ESC.
        if (player.playerUsername == name && player.usernameBillboardText != null &&
            player.usernameBillboardText.text == name && slot?.usernameHeader != null && slot.usernameHeader.text == name) return true;
        PlayerNameDiagnosticService.LogSteamSource(player, "ChuxiaFixes-corrected", player.playerUsername, name, name);
        Apply(player);
        return true;
    }

    internal static void Apply(PlayerControllerB? player, QuickMenuManager? menu = null)
    {
        if (player == null || !Names.TryGet(player, player.actualClientId, player.playerSteamId, out var name)) return;
        if (!string.Equals(player.playerUsername, name, StringComparison.Ordinal)) player.playerUsername = name;
        Set(player.usernameBillboardText, player, name);
        var round = StartOfRound.Instance;
        var players = round?.allPlayerScripts;
        menu ??= player.quickMenuManager;
        if (round != null && players != null && menu?.playerListSlots != null)
        {
            var found = round.ClientPlayerList.TryGetValue(player.actualClientId, out var slot) && slot >= 0 &&
                slot < players.Length && ReferenceEquals(players[slot], player);
            if (!found)
            {
                slot = -1;
                for (var i = 0; i < players.Length; i++)
                    if (ReferenceEquals(players[i], player)) { slot = i; break; }
            }
            if (slot >= 0 && slot < menu.playerListSlots.Length) Set(menu.playerListSlots[slot]?.usernameHeader, player, name);
        }
        var targets = round?.mapScreen?.radarTargets;
        if (targets != null)
        {
            if (!RadarIndices.TryGetValue(player.actualClientId, out var index) || index < 0 || index >= targets.Count ||
                targets[index] == null || targets[index].isNonPlayer || targets[index].transform != player.transform)
            {
                index = -1;
                for (var i = 0; i < targets.Count; i++)
                    if (targets[i] != null && !targets[i].isNonPlayer && targets[i].transform == player.transform) { index = i; break; }
                if (index < 0) { RadarIndices.Remove(player.actualClientId); return; }
                RadarIndices[player.actualClientId] = index;
            }
            if (targets[index].name != name) targets[index].name = name;
        }
    }

    private static void Set(TMP_Text? text, PlayerControllerB player, string name)
    {
        if (text == null) return;
        Register(text, player);
        if (string.Equals(text.text, name, StringComparison.Ordinal)) return;
        text.text = name;
        FontFallbackService.ApplyFallback(text, name);
    }
}
