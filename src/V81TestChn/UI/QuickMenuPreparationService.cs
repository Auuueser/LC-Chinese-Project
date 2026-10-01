using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace V81TestChn;

// Prepare each real menu once after player initialization, without enabling UI
// or invoking any of the game's/third-party OpenQuickMenu callbacks.
internal static class QuickMenuPreparationService
{
    private static MonoBehaviour? _host;
    private static ConditionalWeakTable<QuickMenuManager, Preparation> _menus = new();
    private sealed class Preparation
    {
        internal MonoBehaviour? Host;
        internal bool Started;
    }
    internal static void AttachHost(MonoBehaviour host)
    {
        _host = host;
        MenuTranslationScan.ResetEpoch();
        foreach (var pair in _menus)
        {
            pair.Value.Host = null;
            pair.Value.Started = false;
            Request(pair.Key);
        }
    }
    internal static void Clear() { _menus = new(); _host = null; }
    internal static void OnTranslationCachesCleared()
    {
        // Additive scene unloads can invalidate scans while the room survives.
        foreach (var pair in _menus)
        {
            pair.Value.Started = false;
            Request(pair.Key);
        }
    }
    internal static string Describe(QuickMenuManager menu) => !_menus.TryGetValue(menu, out var state)
        ? "not-requested" : state.Started ? "dispatched" : "queued";

    internal static void Request(QuickMenuManager? menu)
    {
        // Player objects can be disabled or have their coroutines stopped after
        // Start. An independent runtime host must own preparation and scans.
        var host = _host;
        if (menu == null || host == null || !host.isActiveAndEnabled || Plugin.IsRuntimeShuttingDown) return;
        var state = _menus.GetValue(menu, _ => new Preparation());
        if (state.Started || state.Host != null && state.Host.isActiveAndEnabled) return;
        state.Host = host;
        host.StartCoroutine(Prepare(menu, state));
    }

    private static IEnumerator Prepare(QuickMenuManager menu, Preparation state)
    {
        // Let other mods finish Start and build their player-list rows first.
        yield return null;
        yield return null;
        if (menu == null || state.Host == null || Plugin.IsRuntimeShuttingDown ||
            !_menus.TryGetValue(menu, out var current) || !ReferenceEquals(current, state)) yield break;
        var host = state.Host;
        state.Started = true;
        MenuFirstFrameLocalizationService.ApplyLobbyHeader(menu);
        ChatEmojiSpriteService.ApplyToQuickMenuLobbyHeader(menu);
        TargetedUiTranslator.ScheduleQuickMenu(menu, "QuickMenu.Prepare");
        if (menu.mainButtonsPanel != null)
            MenuTranslationScan.For(menu.mainButtonsPanel).Prewarm(host, "QuickMenu.Prepare.buttons");
        if (menu.playerListPanel != null)
            MenuTranslationScan.For(menu.playerListPanel).Prewarm(host, "QuickMenu.Prepare.players");
        if (menu.debugMenuUI != null)
            MenuTranslationScan.For(menu.debugMenuUI).Prewarm(host, "QuickMenu.Prepare.debug", menu.debugMenuUI.GetInstanceID());
        if (menu.settingsPanel != null)
            MenuTranslationScan.For(menu.settingsPanel).Prewarm(host, "QuickMenu.Prepare.settings");
        if (menu.leaveGameConfirmPanel != null)
            MenuTranslationScan.For(menu.leaveGameConfirmPanel).Prewarm(host, "QuickMenu.Prepare.leave");
        // Dynamically created third-party dialogs keep their on-demand lifecycle.
        // All root traversals share the existing MenuFrameBudget.
        state.Host = null;
    }
}
