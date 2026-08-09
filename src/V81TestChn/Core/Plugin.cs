using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Zaggy1024.OpenBodyCams", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("LCBetterSaves", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.example.Advancedfeatures", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("FlipMods.TooManyEmotes", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "cn.codex.v81testchn";
    public const string PluginName = "V81 TEST CHN";
    public const string PluginVersion = "3.2.5";
    private const string LegacyConfigFileName = "LC Chinese Project.cfg";

    internal static ManualLogSource Log = null!;

    private readonly Harmony _harmony = new(PluginGuid);
    private static int _translationHits;
    private static bool _isShuttingDown;
    private static bool _cleanupInProgress;
    private static bool _runtimeShutDown;
    private static ConfigEntry<bool>? _logRuntimeLocalizationEvents;
    private static bool _logRuntimeLocalizationEventsFast;
    private ConfigFile? _runtimeConfig;
    private bool _cleanupCompleted;

    internal static bool IsRuntimeShuttingDown => _cleanupInProgress || _runtimeShutDown;
    internal static bool RuntimeLocalizationLogsEnabled => _logRuntimeLocalizationEventsFast;

    private void Awake()
    {
        Log = Logger;
        _runtimeShutDown = false;
        _cleanupInProgress = false;
        var runtimeConfig = CreateRuntimeConfig();
        _logRuntimeLocalizationEvents = runtimeConfig.Bind(
            ConfigSections.DiagnosticsGeneral,
            "LogRuntimeLocalizationEvents",
            false,
            "Enable verbose runtime localization event logs. Default off to avoid IO spikes during gameplay.");
        _logRuntimeLocalizationEventsFast = _logRuntimeLocalizationEvents.Value;
        DontDestroyOnLoad(gameObject);
        Application.quitting += OnUnityQuitting;

        var pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? Paths.PluginPath;
        TryInitialize("StartupSplashLocalizationService", () => { StartupSplashLocalizationService.Initialize(this, pluginDir, runtimeConfig); });
        RuntimePerformanceSettings.Initialize(runtimeConfig);
        TranslationGuard.Initialize(runtimeConfig);
        TextPatches.Initialize(runtimeConfig);
        TryInitialize("CustomLocalizationExtensionService", () => { CustomLocalizationExtensionService.Initialize(pluginDir, runtimeConfig); });
        TryInitialize("ItemIdentityCompatibilityService", () => { ItemIdentityCompatibilityService.Initialize(); });
        try
        {
            TranslationService.Initialize(runtimeConfig);
            TranslationService.Load(pluginDir);
        }
        catch (Exception ex)
        {
            Logger.LogError($"TranslationService.Load failed: {ex.GetType().Name}: {ex.Message}");
        }

        TryInitialize("AutomaticTranslationService", () => { AutomaticTranslationService.Initialize(pluginDir, runtimeConfig); });
        TryInitialize("RuntimeTextCollector", () => { RuntimeTextCollector.Initialize(pluginDir, runtimeConfig); });
        TryInitialize("ClipboardManualLocalizationService", () => { ClipboardManualLocalizationService.Initialize(pluginDir); });
        TryInitialize("StickyNoteLocalizationService", () => { StickyNoteLocalizationService.Initialize(pluginDir); });
        TryInitialize("EnvironmentTextureLocalizationService", () => { EnvironmentTextureLocalizationService.Initialize(pluginDir); });
        TryInitialize("ChatEmojiSpriteService", () => { ChatEmojiSpriteService.Initialize(pluginDir, runtimeConfig); });
        TryInitialize("CompanySubtitleService", () => { CompanySubtitleService.Initialize(runtimeConfig); });

        var existingPatchCount = CountOwnHarmonyPatches();
        var manualPatchCount = 0;
        if (existingPatchCount > 0)
        {
            Logger.LogWarning($"Manual patch install skipped; {existingPatchCount} Harmony patches are already owned by {PluginGuid}.");
        }
        else
        {
            manualPatchCount = TextPatches.Install(_harmony);
        }

        if (manualPatchCount == 0 && existingPatchCount == 0)
        {
            Logger.LogWarning("Manual patch count is 0; global text hooks are not installed.");
        }

        TryInitialize("FontFallbackService", () => { FontFallbackService.TryLoadFontAsset(pluginDir); });
        TryInitialize("FontFallbackAuditService", () => { FontFallbackAuditService.Initialize(runtimeConfig); });
        TryInitialize("AlertTextureReplacementService", () => { AlertTextureReplacementService.Initialize(pluginDir); });
        TryInitialize("RadiationWarningAuditService", () => { RadiationWarningAuditService.Initialize(runtimeConfig); });
        TryInitialize("RadiationWarningPlaybackService", () => { RadiationWarningPlaybackService.Initialize(pluginDir, runtimeConfig); });
        TryInitialize("EndGameLocalizationService", () => { EndGameLocalizationService.Initialize(pluginDir); });
        TryInitialize("TargetedUiTranslator", () => { TargetedUiTranslator.Initialize(); });

        // Verbose runtime marker; keep code available for future diagnostics without adding startup log noise.
        // Logger.LogInfo($"Runtime marker: lean-hooks-v72-warning-graft; fontCompatibility=primary-fallback-only; fontAssetAwake=minimal-restored; fallback=relay-only-plus-whitelist; relaySync=hud-start-plus-color-sync-plus-exact-path-watcher; fixedSceneLabels=relay-scene-watcher-plus-exact-text; translationCfg=first-source-wins-no-command-alias-cfg-terminal-zhCN-skipped; translationRegexSafety=known-slow-cfg-fastpath; hostStageMarkers=enabled; roomCreateProbe=diagnostics-suppressed; systemOnlineMode=original-tmp-exact-path-only; terminalInput=untranslated-safe; terminalUiRootTranslation=disabled; terminalLoadNewNodeFallback=disabled; terminalInputFieldGlobalTmpHooks=disabled; terminalOutput=body-cn-command-pages-bilingual-full-structured-safe; endgameLocalization=original-image-sprite-replacement-clean-reference-textures-plus-statsboxes-candidate-fix; spectateDeadLocalization=early-hooked; warningTextureLocalization=animator-following-sprite-substitution; manualPatchCount={manualPatchCount}; harmonyPatchedMethods={CountOwnHarmonyPatches()}");
        Logger.LogInfo($"{PluginName} loaded. Entries: {TranslationService.EntryCount}; manualPatchCount={manualPatchCount}; harmonyPatchedMethods={CountOwnHarmonyPatches()}");
    }

    private ConfigFile CreateRuntimeConfig()
    {
        var legacyConfigPath = Path.Combine(Paths.ConfigPath, LegacyConfigFileName);
        TryMigrateLegacyConfig(legacyConfigPath, Config);
        _runtimeConfig = Config;
        return _runtimeConfig;
    }

    private static void TryMigrateLegacyConfig(string legacyConfigPath, ConfigFile runtimeConfig)
    {
        try
        {
            var configPath = runtimeConfig.ConfigFilePath;
            if (!File.Exists(legacyConfigPath) || PathsReferToSameFile(legacyConfigPath, configPath))
            {
                return;
            }

            var canonicalHasSettings = ConfigFileContainsSettings(configPath);
            var legacyIsNewer = !File.Exists(configPath) ||
                                File.GetLastWriteTimeUtc(legacyConfigPath) > File.GetLastWriteTimeUtc(configPath);
            if (canonicalHasSettings && !legacyIsNewer)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? Paths.ConfigPath);
            if (File.Exists(configPath))
            {
                var backupPath = configPath + ".pre-lethalconfig-migration.bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(configPath, backupPath, overwrite: false);
                }
            }

            File.Copy(legacyConfigPath, configPath, overwrite: true);
            runtimeConfig.Reload();
            Log.LogInfo($"Migrated legacy config '{LegacyConfigFileName}' to the BepInEx plugin config used by LethalConfig.");
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to migrate legacy config '{LegacyConfigFileName}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool ConfigFileContainsSettings(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed[0] == '[')
            {
                continue;
            }

            if (trimmed.IndexOf('=') > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathsReferToSameFile(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
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
        _cleanupInProgress = true;
        _runtimeShutDown = true;
        TryCleanup("Harmony.UnpatchSelf", _harmony.UnpatchSelf);
        TryCleanup("OriginalResourceStateService.RestoreAll", () => { OriginalResourceStateService.RestoreAll(); });
        TryCleanup("ItemIdentityCompatibilityService.Shutdown", () => { ItemIdentityCompatibilityService.Shutdown(); });
        TryCleanup("TextPatches.Clear", () => { TextPatches.Clear(); });
        TryCleanup("TranslationGuard.Clear", () => { TranslationGuard.Clear(); });
        TryCleanup("TargetedUiTranslator.Shutdown", () => { TargetedUiTranslator.Shutdown(); });
        TryCleanup("TranslationService.ClearCaches", () => { TranslationService.ClearCaches(); });
        TryCleanup("CustomLocalizationExtensionService.Shutdown", () => { CustomLocalizationExtensionService.Shutdown(); });
        TryCleanup("CompanySubtitleService.Shutdown", () => { CompanySubtitleService.Shutdown(); });
        TryCleanup("ChatEmojiSpriteService.Shutdown", () => { ChatEmojiSpriteService.Shutdown(); });
        TryCleanup("FontFallbackAuditService.Shutdown", () => { FontFallbackAuditService.Shutdown(); });
        TryCleanup("FontFallbackService.Shutdown", () => { FontFallbackService.Shutdown(); });
        TryCleanup("ClipboardManualLocalizationService.Shutdown", () => { ClipboardManualLocalizationService.Shutdown(); });
        TryCleanup("StickyNoteLocalizationService.Shutdown", () => { StickyNoteLocalizationService.Shutdown(); });
        TryCleanup("StartupSplashLocalizationService.Shutdown", () => { StartupSplashLocalizationService.Shutdown(); });
        TryCleanup("EnvironmentTextureLocalizationService.Shutdown", () => { EnvironmentTextureLocalizationService.Shutdown(); });
        TryCleanup("SpiderSafeModeLocalizationService.Shutdown", () => { SpiderSafeModeLocalizationService.Shutdown(); });
        TryCleanup("AlertTextureReplacementService.Shutdown", () => { AlertTextureReplacementService.Shutdown(); });
        TryCleanup("RadiationWarningAuditService.Shutdown", () => { RadiationWarningAuditService.Shutdown(); });
        TryCleanup("RadiationWarningPlaybackService.Shutdown", () => { RadiationWarningPlaybackService.Shutdown(); });
        TryCleanup("EndGameLocalizationService.Shutdown", () => { EndGameLocalizationService.Shutdown(); });
        TryCleanup("RuntimeTextCollector.Shutdown", () => { RuntimeTextCollector.Shutdown(); });
        TryCleanup("AutomaticTranslationService.Shutdown", () => { AutomaticTranslationService.Shutdown(); });
        _cleanupInProgress = false;
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

