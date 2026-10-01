using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using Steamworks;
using Steamworks.Data;
using System.Threading;

namespace V81TestChn;

// Steam callbacks only enqueue IDs. All Steam reads and Unity writes occur on
// the game thread, and only connected identities may consume a notification.
internal static class SteamPlayerNameService
{
    private static readonly PlayerNameRefreshInbox Inbox = new();
    private static readonly Dictionary<ulong, ulong> Clients = new();
    private static bool _subscribed;
    private static bool _warned;
    private const string MemberNameKey = "Aueser.LCChineseProject.player-name.v1";
    private static ulong _lobbyId;
    private static string? _publishedName;

    private static bool Available => PlayerNameSettings.Enabled && GameNetworkManager.Instance != null &&
        !GameNetworkManager.Instance.disableSteam && SteamClient.IsValid;

    internal static void Clear()
    {
        Volatile.Write(ref _lobbyId, 0);
        _publishedName = null;
        if (_subscribed)
        {
            SteamFriends.OnPersonaStateChange -= OnPersonaChanged;
            SteamMatchmaking.OnLobbyMemberDataChanged -= OnMemberDataChanged;
        }
        _subscribed = false;
        _warned = false;
        Clients.Clear();
        Inbox.Clear();
    }

    internal static void Remove(ulong clientId)
    {
        if (!Clients.TryGetValue(clientId, out var steamId)) return;
        Clients.Remove(clientId);
        if (!Clients.ContainsValue(steamId)) Inbox.Remove(steamId);
    }

    internal static string SelectCaptureName(PlayerControllerB player, string incoming)
    {
        if (!Available || player.playerSteamId == 0) return incoming;
        if (player.playerSteamId != (ulong)SteamClient.SteamId)
        {
            var advertised = ReadMemberName(player.playerSteamId);
            if (advertised.Length == 0) return incoming;
            PlayerNameDiagnosticService.LogSteamSource(player, "member-capture", incoming, advertised, advertised);
            return advertised;
        }
        // Do not let a stale GetFriendPersonaName result replace our own current
        // persona name. Other players always remain keyed by their Steam ID.
        var raw = SteamClient.Name;
        var name = PlayerNamePolicy.Sanitize(raw);
        PlayerNameDiagnosticService.LogSteamSource(player, "local-capture", incoming, raw, name);
        return name.Length == 0 ? incoming : name;
    }

    internal static void RefreshLocalName()
    {
        if (!Available) return;
        var name = PlayerNamePolicy.Sanitize(SteamClient.Name);
        if (name.Length > 0) GameNetworkManager.Instance.username = name;
        try { PublishLocalName(); } catch (Exception ex) { Warn(ex); }
    }

    private static void OnPersonaChanged(Friend friend) => Inbox.Notify((ulong)friend.Id);
    private static void OnMemberDataChanged(Lobby lobby, Friend friend)
    {
        if ((ulong)lobby.Id == Volatile.Read(ref _lobbyId)) Inbox.Notify((ulong)friend.Id);
    }

    private static string ReadMemberName(ulong steamId)
    {
        var lobby = GameNetworkManager.Instance.currentLobby;
        if (!lobby.HasValue) return string.Empty;
        try { return PlayerNamePolicy.Advertised(lobby.Value.GetMemberData(new Friend(steamId), MemberNameKey)); }
        catch (Exception ex) { Warn(ex); return string.Empty; }
    }

    private static void PublishLocalName()
    {
        var lobby = GameNetworkManager.Instance.currentLobby;
        if (!lobby.HasValue) return;
        var id = (ulong)lobby.Value.Id;
        if (_lobbyId != id)
        {
            Clients.Clear();
            Inbox.Clear();
            _publishedName = null;
            Volatile.Write(ref _lobbyId, id);
        }
        var name = PlayerNamePolicy.Advertised(SteamClient.Name);
        if (name.Length == 0 || name == _publishedName) return;
        // SetMemberData can only write this client's own member record.
        // Never overwrite another member or any game-owned lobby key.
        lobby.Value.SetMemberData(MemberNameKey, name);
        _publishedName = name;
        PlayerNameDiagnosticService.LogSteamPublish(id, name);
    }

    internal static void RefreshConnectedPlayers()
    {
        if (!Available) return;
        if (!_subscribed)
        {
            SteamFriends.OnPersonaStateChange += OnPersonaChanged;
            SteamMatchmaking.OnLobbyMemberDataChanged += OnMemberDataChanged;
            _subscribed = true;
        }
        try { PublishLocalName(); } catch (Exception ex) { Warn(ex); }
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players == null) return;
        foreach (var player in players)
        {
            if (player == null || (!player.isPlayerControlled && !player.isPlayerDead) || player.playerSteamId == 0) continue;
            if (Clients.TryGetValue(player.actualClientId, out var previous) && previous != player.playerSteamId)
                Remove(player.actualClientId);
            Clients[player.actualClientId] = player.playerSteamId;
            var first = Inbox.Track(player.playerSteamId);
            try
            {
                // Once per connected identity; not every frame or ESC opening.
                if (first && player.playerSteamId != (ulong)SteamClient.SteamId)
                {
                    var pending = SteamFriends.RequestUserInformation(player.playerSteamId, true);
                    PlayerNameDiagnosticService.LogSteamRequest(player, pending);
                }
                Refresh(player, "rpc-refresh");
            }
            catch (Exception ex) { Warn(ex); }
        }
    }

    internal static void ApplyPending()
    {
        if (!Inbox.HasPending) return;
        if (!Available) { Inbox.ClearPending(); return; }
        try { PublishLocalName(); } catch (Exception ex) { Warn(ex); }
        var players = StartOfRound.Instance?.allPlayerScripts;
        foreach (var steamId in Inbox.Drain())
        {
            if (players == null) continue;
            foreach (var player in players)
            {
                if (player == null || player.playerSteamId != steamId || (!player.isPlayerControlled && !player.isPlayerDead) ||
                    !Clients.TryGetValue(player.actualClientId, out var expected) || expected != steamId) continue;
                try { Refresh(player, "persona-change"); }
                catch (Exception ex) { Warn(ex); }
            }
        }
    }

    private static void Refresh(PlayerControllerB player, string reason)
    {
        var local = player.playerSteamId == (ulong)SteamClient.SteamId;
        var raw = local ? SteamClient.Name : new Friend(player.playerSteamId).Name;
        if (!local)
        {
            var advertised = ReadMemberName(player.playerSteamId);
            if (advertised.Length > 0) { raw = advertised; reason += "/member-data"; }
        }
        var name = PlayerNamePolicy.Sanitize(raw);
        PlayerNameDiagnosticService.LogSteamSource(player, reason, player.playerUsername, raw, name);
        if (name.Length == 0) return; // An incomplete response must not erase a known name.
        if (local) GameNetworkManager.Instance.username = name;
        PlayerNameSourceService.Capture(player, name);
        PlayerNameSourceService.Apply(player);
    }

    private static void Warn(Exception ex)
    {
        if (_warned) return;
        _warned = true;
        Plugin.Log.LogWarning($"Steam player-name refresh failed: {ex.GetType().Name}: {ex.Message}");
    }
}
