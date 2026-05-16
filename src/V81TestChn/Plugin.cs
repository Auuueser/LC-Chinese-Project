using System;
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
    public const string PluginVersion = "0.2.0";

    internal static ManualLogSource Log = null!;

    private readonly Harmony _harmony = new(PluginGuid);
    private static int _translationHits;
    private static bool _isShuttingDown;
    private bool _cleanupCompleted;

    private void Awake()
    {
        Log = Logger;
        DontDestroyOnLoad(gameObject);
        Application.quitting += OnUnityQuitting;

        var pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? Paths.PluginPath;
        TranslationGuard.Initialize(Config);
        TextPatches.Initialize(Config);
        TryInitialize("CustomLocalizationExtensionService", () => { CustomLocalizationExtensionService.Initialize(pluginDir, Config); });
        try
        {
            TranslationService.Initialize(Config);
            TranslationService.Load(pluginDir);
        }
        catch (Exception ex)
        {
            Logger.LogError($"TranslationService.Load failed: {ex.GetType().Name}: {ex.Message}");
        }

        var manualPatchCount = TextPatches.Install(_harmony);
        if (manualPatchCount == 0)
        {
            Logger.LogWarning("Manual patch count is 0; global text hooks are not installed.");
        }

        TryInitialize("FontFallbackService", () => { FontFallbackService.TryLoadFontAsset(pluginDir); });
        TryInitialize("EmbeddedFontPatcherService", () => { EmbeddedFontPatcherService.Initialize(pluginDir, Config); });
        TryInitialize("AlertTextureReplacementService", () => { AlertTextureReplacementService.Initialize(pluginDir); });
        TryInitialize("RadiationWarningAuditService", () => { RadiationWarningAuditService.Initialize(Config); });
        TryInitialize("RadiationWarningPlaybackService", () => { RadiationWarningPlaybackService.Initialize(pluginDir, Config); });
        TryInitialize("EndGameLocalizationService", () => { EndGameLocalizationService.Initialize(pluginDir); });
        TryInitialize("RuntimeTextCollector", () => { RuntimeTextCollector.Initialize(pluginDir, Config); });
        TryInitialize("TargetedUiTranslator", () => { TargetedUiTranslator.Initialize(); });

        // Verbose runtime marker; keep code available for future diagnostics without adding startup log noise.
        // Logger.LogInfo($"Runtime marker: lean-hooks-v72-warning-graft; embeddedFontPatcher=startup-only; fontAssetAwake=minimal-restored; fallback=relay-only-plus-whitelist; relaySync=hud-start-plus-color-sync-plus-exact-path-watcher; fixedSceneLabels=relay-scene-watcher-plus-exact-text; translationCfg=first-source-wins-no-command-alias-cfg-terminal-zhCN-skipped; translationRegexSafety=known-slow-cfg-fastpath; hostStageMarkers=enabled; roomCreateProbe=diagnostics-suppressed; systemOnlineMode=original-tmp-exact-path-only; terminalInput=untranslated-safe; terminalUiRootTranslation=disabled; terminalLoadNewNodeFallback=disabled; terminalInputFieldGlobalTmpHooks=disabled; terminalOutput=body-cn-command-pages-bilingual-full-structured-safe; endgameLocalization=original-image-sprite-replacement-clean-reference-textures-plus-statsboxes-candidate-fix; spectateDeadLocalization=early-hooked; warningTextureLocalization=animator-following-sprite-substitution; manualPatchCount={manualPatchCount}; harmonyPatchedMethods={CountOwnHarmonyPatches()}");
        Logger.LogInfo($"{PluginName} loaded. Entries: {TranslationService.EntryCount}; manualPatchCount={manualPatchCount}; harmonyPatchedMethods={CountOwnHarmonyPatches()}");
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
        Log.LogInfo("Plugin OnApplicationQuit");
        CleanupPlugin();
    }

    private void OnDestroy()
    {
        Log.LogInfo($"Plugin OnDestroy; shuttingDown={_isShuttingDown}");
        if (!_isShuttingDown && Application.isPlaying)
        {
            Log.LogWarning("Preserving Harmony patches after early OnDestroy because the application is still running.");
            return;
        }

        CleanupPlugin();
    }

    private void OnUnityQuitting()
    {
        _isShuttingDown = true;
        Log.LogInfo("Plugin Application.quitting");
        CleanupPlugin();
    }

    private void CleanupPlugin()
    {
        if (_cleanupCompleted)
        {
            return;
        }

        _cleanupCompleted = true;
        Application.quitting -= OnUnityQuitting;
        TryCleanup("OriginalResourceStateService.RestoreAll", () => { OriginalResourceStateService.RestoreAll(); });
        TryCleanup("RuntimeIconsCompatibilityService.Clear", () => { RuntimeIconsCompatibilityService.Clear(); });
        TryCleanup("TextPatches.Clear", () => { TextPatches.Clear(); });
        TryCleanup("TranslationGuard.Clear", () => { TranslationGuard.Clear(); });
        TryCleanup("TargetedUiTranslator.Shutdown", () => { TargetedUiTranslator.Shutdown(); });
        TryCleanup("TranslationService.ClearCaches", () => { TranslationService.ClearCaches(); });
        TryCleanup("CustomLocalizationExtensionService.Shutdown", () => { CustomLocalizationExtensionService.Shutdown(); });
        TryCleanup("FontFallbackService.Shutdown", () => { FontFallbackService.Shutdown(); });
        TryCleanup("EmbeddedFontPatcherService.Shutdown", () => { EmbeddedFontPatcherService.Shutdown(); });
        TryCleanup("AlertTextureReplacementService.Shutdown", () => { AlertTextureReplacementService.Shutdown(); });
        TryCleanup("RadiationWarningPlaybackService.Shutdown", () => { RadiationWarningPlaybackService.Shutdown(); });
        TryCleanup("EndGameLocalizationService.Shutdown", () => { EndGameLocalizationService.Shutdown(); });
        TryCleanup("RuntimeTextCollector.Shutdown", () => { RuntimeTextCollector.Shutdown(); });
        TryCleanup("Harmony.UnpatchSelf", _harmony.UnpatchSelf);
    }

    private static void TryInitialize(string name, Action initialize)
    {
        try
        {
            initialize();
        }
        catch (Exception ex)
        {
            Log.LogError($"{name} initialization failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TryCleanup(string name, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"{name} failed during plugin cleanup: {ex.GetType().Name}: {ex.Message}");
        }
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

