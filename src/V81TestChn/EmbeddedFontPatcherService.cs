using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class EmbeddedFontPatcherService
{
    private sealed class FontBundlePair
    {
        public string BundleName { get; set; } = string.Empty;
        public TMP_FontAsset? Normal { get; set; }
        public TMP_FontAsset? Transmit { get; set; }
    }

    private static readonly List<FontBundlePair> LoadedBundles = new();
    private static readonly HashSet<int> PatchedFontIds = new();
    private static readonly HashSet<string> ScannedDirectories = new(StringComparer.OrdinalIgnoreCase);

    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<string>? _fontAssetsPath;
    private static ConfigEntry<string>? _normalRegexPattern;
    private static ConfigEntry<string>? _transmitRegexPattern;
    private static ConfigEntry<bool>? _useVanillaNormal;
    private static ConfigEntry<bool>? _useVanillaTransmit;
    private static ConfigEntry<bool>? _debugLog;
    private static ConfigEntry<bool>? _applyMaterialTweaks;
    private static ConfigEntry<string>? _bundleNameRegexPattern;

    private static Regex? _normalRegex;
    private static Regex? _transmitRegex;
    private static Regex? _bundleNameRegex;
    private static bool _initialized;
    private static int _patchLogBudget = 160;
    private static int _scanLogBudget = 60;
    private static bool _loggedNormalBundleBypass;

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        if (_initialized)
        {
            return;
        }

        _enabled = config.Bind("EmbeddedFontPatcher", "Enable", true, "Enable embedded FontPatcher compatibility behavior.");
        _fontAssetsPath = config.Bind("EmbeddedFontPatcher", "FontAssetsPath", @"fontpatcher\default", "Relative directory for font bundles.");
        _normalRegexPattern = config.Bind("EmbeddedFontPatcher", "NormalFontNameRegex", @"^(b|DialogueText).*$", "Regex used to detect normal UI fonts.");
        _transmitRegexPattern = config.Bind("EmbeddedFontPatcher", "TransmitFontNameRegex", @"^.*$", "Regex used to detect signal/transmit fonts.");
        _useVanillaNormal = config.Bind("EmbeddedFontPatcher", "UseVanillaNormalFont", true, "Keep vanilla normal font characters in addition to bundle fallback fonts.");
        _useVanillaTransmit = config.Bind("EmbeddedFontPatcher", "UseVanillaTransmitFont", true, "Keep vanilla transmit font characters in addition to bundle fallback fonts.");
        _debugLog = config.Bind("EmbeddedFontPatcher", "DebugLog", false, "Enable verbose embedded FontPatcher logs.");
        _applyMaterialTweaks = config.Bind("EmbeddedFontPatcher", "ApplyMaterialTweaks", false, "Apply FontPatcher-like material underlay tweaks.");
        _bundleNameRegexPattern = config.Bind(
            "EmbeddedFontPatcher",
            "BundleNameRegex",
            @"^(00 default|cn|zh.*)$",
            "Only bundles whose file name matches this regex will be loaded. Prevents unintended style drift from unrelated language bundles.");

        UpgradeLegacyRegexDefaultsIfNeeded();
        _normalRegex = CreateRegex(_normalRegexPattern.Value, @"^(b|DialogueText).*$");
        _transmitRegex = CreateRegex(_transmitRegexPattern.Value, @"^.*$");
        _bundleNameRegex = CreateRegex(_bundleNameRegexPattern.Value, @"^(00 default|cn|zh.*)$");

        _initialized = true;

        if (_enabled.Value)
        {
            LoadBundles(pluginDir);
            PatchLoadedFontAssets();
        }

        Plugin.Log.LogInfo($"EmbeddedFontPatcher initialized: enabled={_enabled.Value}, bundles={LoadedBundles.Count}, assetsPath={_fontAssetsPath.Value}");
    }

    public static void PatchFontAsset(TMP_FontAsset? fontAsset, string stage = "unknown")
    {
        if (!_initialized || !_enabled!.Value || fontAsset == null)
        {
            return;
        }

        if (_normalRegex == null || _transmitRegex == null)
        {
            return;
        }

        var fontId = fontAsset.GetInstanceID();
        var fontName = fontAsset.name ?? string.Empty;
        var isNormal = _normalRegex.IsMatch(fontName);
        var isTransmit = _transmitRegex.IsMatch(fontName);
        if (!isNormal && !isTransmit)
        {
            return;
        }

        if (!PatchedFontIds.Add(fontId))
        {
            return;
        }

        if (fontAsset.fallbackFontAssetTable == null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (isNormal)
        {
            DisableVanillaFont(fontAsset);
            if (!_loggedNormalBundleBypass)
            {
                _loggedNormalBundleBypass = true;
                Plugin.Log.LogWarning("EmbeddedFontPatcher bypassed normal bundle fallbacks; relying on V81 dynamic Chinese fallback to avoid unreadable atlas stalls.");
            }
        }
        if (isTransmit && !_useVanillaTransmit!.Value)
        {
            DisableVanillaFont(fontAsset);
        }

        var added = 0;
        foreach (var bundle in LoadedBundles)
        {
            if (isNormal)
            {
                continue;
            }

            var fallback = isNormal ? bundle.Normal : bundle.Transmit;
            if (fallback == null)
            {
                continue;
            }

            if (fontAsset.fallbackFontAssetTable.Contains(fallback))
            {
                continue;
            }

            fontAsset.fallbackFontAssetTable.Add(fallback);
            added++;
        }

        if (_applyMaterialTweaks!.Value)
        {
            ApplyMaterialTweaks(fontAsset.material);
        }

        if (added > 0 && _patchLogBudget > 0)
        {
            _patchLogBudget--;
            Plugin.Log.LogWarning($"EmbeddedFontPatcher patched '{fontName}' at {stage}: addedFallbacks={added}, mode={(isNormal ? "Normal" : "Transmit")}");
        }
        else if (_debugLog!.Value)
        {
            Plugin.Log.LogInfo($"EmbeddedFontPatcher visited '{fontName}' at {stage}: addedFallbacks={added}, mode={(isNormal ? "Normal" : "Transmit")}");
        }
    }

    public static void PatchTextComponent(TMP_Text? text, string stage = "unknown")
    {
        if (text?.font == null)
        {
            return;
        }

        PatchFontAsset(text.font, stage);
    }

    private static void LoadBundles(string pluginDir)
    {
        LoadedBundles.Clear();
        ScannedDirectories.Clear();
        var loadedBundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ResolveCandidateDirectories(pluginDir))
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            if (!ScannedDirectories.Add(path))
            {
                continue;
            }

            var loadedFromDir = 0;
            foreach (var file in new DirectoryInfo(path).GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (!loadedBundleNames.Add(file.Name))
                {
                    continue;
                }

                if (_bundleNameRegex != null && !_bundleNameRegex.IsMatch(file.Name))
                {
                    if (_debugLog!.Value)
                    {
                        Plugin.Log.LogInfo($"EmbeddedFontPatcher skipped bundle '{file.Name}' by BundleNameRegex.");
                    }

                    continue;
                }

                try
                {
                    var bundle = AssetBundle.LoadFromFile(file.FullName);
                    if (bundle == null)
                    {
                        continue;
                    }

                    var normal = bundle.LoadAsset<TMP_FontAsset>("Normal");
                    var transmit = bundle.LoadAsset<TMP_FontAsset>("Transmit");
                    if (normal == null && transmit == null)
                    {
                        continue;
                    }

                    if (normal != null)
                    {
                        normal.name = $"{file.Name}(Normal)";
                    }

                    if (transmit != null)
                    {
                        transmit.name = $"{file.Name}(Transmit)";
                    }

                    LoadedBundles.Add(new FontBundlePair
                    {
                        BundleName = file.Name,
                        Normal = normal,
                        Transmit = transmit
                    });

                    loadedFromDir++;
                    if (_debugLog!.Value)
                    {
                        Plugin.Log.LogInfo($"EmbeddedFontPatcher loaded bundle '{file.Name}' from '{path}': normal={(normal != null)}, transmit={(transmit != null)}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"EmbeddedFontPatcher failed loading '{file.FullName}': {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (loadedFromDir > 0 && _scanLogBudget > 0)
            {
                _scanLogBudget--;
                Plugin.Log.LogInfo($"EmbeddedFontPatcher loaded {loadedFromDir} bundles from '{path}'.");
            }
        }
    }

    private static IEnumerable<string> ResolveCandidateDirectories(string pluginDir)
    {
        var relative = NormalizePathSegment(_fontAssetsPath!.Value);
        if (!string.IsNullOrWhiteSpace(relative))
        {
            yield return Path.Combine(pluginDir, "V81TestChn", relative);
            yield return Path.Combine(pluginDir, relative);
            yield return Path.Combine(Paths.ConfigPath, relative);
        }

        yield return Path.Combine(pluginDir, "V81TestChn", "fontpatcher", "default");
        yield return Path.Combine(pluginDir, "V81TestChn", "fonts", "fontpatcher", "default");
        yield return Path.Combine(Paths.ConfigPath, "FontPatcher", "default");
    }

    private static void PatchLoadedFontAssets()
    {
        foreach (var fontAsset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            PatchFontAsset(fontAsset, "Resources.Scan");
        }
    }

    private static void DisableVanillaFont(TMP_FontAsset font)
    {
        if (font.characterLookupTable != null)
        {
            font.characterLookupTable.Clear();
        }

        font.atlasPopulationMode = AtlasPopulationMode.Static;
    }

    private static void ApplyMaterialTweaks(Material? material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayDilate))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 1f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.1f);
        }
    }

    private static void UpgradeLegacyRegexDefaultsIfNeeded()
    {
        if (_normalRegexPattern == null || _transmitRegexPattern == null)
        {
            return;
        }

        var legacyNormal = @"^(b|DialogueText|3270.*)$";
        var legacyTransmit = @"^edunline.*$";
        var recommendedNormal = @"^(b|DialogueText).*$";
        var recommendedTransmit = @"^.*$";

        if (string.Equals(_normalRegexPattern.Value, legacyNormal, StringComparison.Ordinal))
        {
            _normalRegexPattern.Value = recommendedNormal;
            Plugin.Log.LogWarning(
                $"EmbeddedFontPatcher auto-upgraded NormalFontNameRegex from legacy '{legacyNormal}' to '{recommendedNormal}' to match FontPatcher behavior.");
        }

        if (string.Equals(_transmitRegexPattern.Value, legacyTransmit, StringComparison.Ordinal))
        {
            _transmitRegexPattern.Value = recommendedTransmit;
            Plugin.Log.LogWarning(
                $"EmbeddedFontPatcher auto-upgraded TransmitFontNameRegex from legacy '{legacyTransmit}' to '{recommendedTransmit}' to match FontPatcher behavior.");
        }
    }

    private static Regex CreateRegex(string source, string fallback)
    {
        try
        {
            return new Regex(source, RegexOptions.Compiled);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"EmbeddedFontPatcher invalid regex '{source}', fallback to '{fallback}'. Error: {ex.Message}");
            return new Regex(fallback, RegexOptions.Compiled);
        }
    }

    private static string NormalizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
    }
}
