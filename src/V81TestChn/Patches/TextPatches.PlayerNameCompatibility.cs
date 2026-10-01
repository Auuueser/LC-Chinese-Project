using System;
using System.Collections;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;

namespace V81TestChn;

internal static partial class TextPatches
{
    private sealed class EmptyPlayerNameUpdate : IEnumerator
    {
        public object? Current => null;
        public bool MoveNext() => false;
        public void Reset() { }
    }
    private static readonly IEnumerator CompletedPlayerNameUpdate = new EmptyPlayerNameUpdate();

    private static void InstallChuxiaPlayerNameCompatibility(Harmony harmony, ref int patched)
    {
        var type = FindLoadedTypeQuiet("Patches.FixPlayerName_Patches");
        if (type == null) return;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var update = type.GetMethod("UpdatePlayerName", flags, null, new[] { typeof(PlayerControllerB), typeof(PlayerListSlot) }, null);
        var persona = type.GetMethod("OnPersonaStateChange", flags, null, new[] { typeof(Friend) }, null);
        if (update?.ReturnType != typeof(IEnumerator) || persona?.ReturnType != typeof(void))
        {
            Plugin.Log.LogWarning("ChuxiaFixes player-name API changed; compatibility adapter skipped.");
            return;
        }
        // Intercept the iterator factory before its first MoveNext can write a
        // stale persona. An OpenQuickMenu postfix alone runs too early.
        PatchPrefix(harmony, update, nameof(ChuxiaPlayerNameUpdatePrefix), ref patched, Priority.First);
        PatchPrefix(harmony, persona, nameof(ChuxiaPersonaNamePrefix), ref patched, Priority.First);
    }

    private static bool ChuxiaPlayerNameUpdatePrefix(PlayerControllerB __0, PlayerListSlot __1, ref IEnumerator __result)
    {
        if (!PlayerNameSourceService.ApplyKnownName(__0, __1)) return true;
        if (__1 != null) __1.playerSteamId = __0.playerSteamId;
        __result = CompletedPlayerNameUpdate;
        return false;
    }

    private static bool ChuxiaPersonaNamePrefix(Friend __0)
    {
        var players = StartOfRound.Instance?.allPlayerScripts;
        if (players == null) return true;
        foreach (var player in players)
            if (player != null && player.playerSteamId == (ulong)__0.Id && PlayerNameSourceService.ApplyKnownName(player)) return false;
        return true;
    }
}
