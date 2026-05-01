using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "cn.codex.v81testchn";
    public const string PluginName = "V81 TEST CHN";
    public const string PluginVersion = "0.1.7";

    internal static ManualLogSource Log = null!;

    private readonly Harmony _harmony = new(PluginGuid);
    private static int _translationHits;
    private static bool _isShuttingDown;

    private void Awake()
    {
        Log = Logger;
        DontDestroyOnLoad(gameObject);

        var pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? Paths.PluginPath;
        TranslationService.Initialize(Config);
        TranslationService.Load(pluginDir);
        FontFallbackService.TryLoadFontAsset(pluginDir);
        EmbeddedFontPatcherService.Initialize(pluginDir, Config);
        AlertTextureReplacementService.Initialize(pluginDir);
        RadiationWarningAuditService.Initialize(Config);
        RadiationWarningPlaybackService.Initialize(pluginDir, Config);
        EndGameLocalizationService.Initialize(pluginDir);
        RuntimeTextCollector.Initialize(pluginDir, Config);

        var manualPatchCount = TextPatches.Install(_harmony);
        // Verbose runtime marker; keep code available for future diagnostics without adding startup log noise.
        // Logger.LogInfo($"Runtime marker: lean-hooks-v72-warning-graft; embeddedFontPatcher=startup-only; fontAssetAwake=minimal-restored; fallback=relay-only-plus-whitelist; relaySync=hud-start-plus-color-sync-plus-exact-path-watcher; fixedSceneLabels=relay-scene-watcher-plus-exact-text; translationCfg=first-source-wins-no-command-alias-cfg-terminal-zhCN-skipped; translationRegexSafety=known-slow-cfg-fastpath; hostStageMarkers=enabled; roomCreateProbe=diagnostics-suppressed; systemOnlineMode=original-tmp-exact-path-only; terminalInput=untranslated-safe; terminalUiRootTranslation=disabled; terminalLoadNewNodeFallback=disabled; terminalInputFieldGlobalTmpHooks=disabled; terminalOutput=body-cn-command-pages-bilingual-full-structured-safe; endgameLocalization=original-image-sprite-replacement-clean-reference-textures-plus-statsboxes-candidate-fix; spectateDeadLocalization=early-hooked; warningTextureLocalization=animator-following-sprite-substitution; manualPatchCount={manualPatchCount}; harmonyPatchedMethods={CountOwnHarmonyPatches()}");
        Logger.LogInfo($"{PluginName} loaded. Entries: {TranslationService.EntryCount}");
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
        Log.LogInfo("Plugin OnApplicationQuit");
    }

    private void OnDestroy()
    {
        Log.LogInfo($"Plugin OnDestroy; shuttingDown={_isShuttingDown}");
        if (!_isShuttingDown)
        {
            return;
        }

        RadiationWarningPlaybackService.Shutdown();
        _harmony.UnpatchSelf();
    }

    internal static void ReportTranslationHit()
    {
        _translationHits++;
    }

    internal static void LogTargetedTranslation(string reason, int translated, int seen)
    {
        if (reason == "HUDManager.UpdateScanNodes")
        {
            // High-frequency scanner refresh log; keep this exact log available for future diagnostics.
            // Log.LogInfo($"Targeted translation {reason}: {translated}/{seen}, totalHits={_translationHits}, untranslated={RuntimeTextCollector.Count}");
            return;
        }

        // Low-value summary log; keep code available for future diagnostics without flooding LogOutput.log.
        // Log.LogInfo($"Targeted translation {reason}: {translated}/{seen}, totalHits={_translationHits}, untranslated={RuntimeTextCollector.Count}");
    }

    internal static void LogPatchEntry(string reason)
    {
        // Low-value patch-entry log; keep code available for future diagnostics without flooding LogOutput.log.
        // Log.LogInfo($"Patch entry {reason}");
    }

    private static int CountOwnHarmonyPatches()
    {
        return Harmony.GetAllPatchedMethods()
            .Count(method => Harmony.GetPatchInfo(method)?.Owners.Contains(PluginGuid) == true);
    }
}

