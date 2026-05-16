using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

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
    private static readonly List<AssetBundle> LoadedAssetBundles = new();
    private static readonly HashSet<int> PatchedFontIds = new();
    private static readonly Dictionary<int, FontFallbackPatchState> AddedBundleFallbacks = new();
    private static readonly Dictionary<int, FontMutationState> DisabledFontStates = new();
    private static readonly HashSet<string> ScannedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(25);

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

    private sealed class FontFallbackPatchState
    {
        public FontFallbackPatchState(TMP_FontAsset fontAsset)
        {
            FontAsset = fontAsset;
        }

        public TMP_FontAsset FontAsset { get; }
        public List<TMP_FontAsset> AddedFallbacks { get; } = new();
    }

    private sealed class FontMutationState
    {
        public FontMutationState(
            TMP_FontAsset fontAsset,
            Dictionary<uint, TMP_Character>? characterLookupTable,
            Dictionary<uint, Glyph>? glyphLookupTable,
            AtlasPopulationMode atlasPopulationMode)
        {
            FontAsset = fontAsset;
            CharacterLookupTable = characterLookupTable;
            GlyphLookupTable = glyphLookupTable;
            AtlasPopulationMode = atlasPopulationMode;
        }

        public TMP_FontAsset FontAsset { get; }
        public Dictionary<uint, TMP_Character>? CharacterLookupTable { get; }
        public Dictionary<uint, Glyph>? GlyphLookupTable { get; }
        public AtlasPopulationMode AtlasPopulationMode { get; }
    }

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
        var isNormal = IsRegexMatch(_normalRegex, fontName, "NormalFontNameRegex");
        var isTransmit = !isNormal && IsRegexMatch(_transmitRegex, fontName, "TransmitFontNameRegex");
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

        if (isNormal && !_useVanillaNormal!.Value)
        {
            DisableVanillaFont(fontAsset);
        }

        if (isTransmit && !_useVanillaTransmit!.Value)
        {
            DisableVanillaFont(fontAsset);
        }

        var added = 0;
        foreach (var bundle in LoadedBundles)
        {
            if (isNormal && !ShouldAddNormalFallbacks(bundle))
            {
                continue;
            }

            if (isTransmit && !ShouldAddTransmitFallbacks(bundle))
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
            TrackAddedFallback(fontAsset, fallback);
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
        RemoveAddedBundleFallbacks();
        UnloadAssetBundles();
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

                if (_bundleNameRegex != null && !IsRegexMatch(_bundleNameRegex, file.Name, "BundleNameRegex"))
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
                        bundle.Unload(false);
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
                    LoadedAssetBundles.Add(bundle);

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
        var relative = ValidateFontAssetsPath(_fontAssetsPath!.Value);
        if (!string.IsNullOrWhiteSpace(relative))
        {
            foreach (var allowedRoot in new[]
            {
                Path.Combine(pluginDir, "V81TestChn"),
                pluginDir,
                Paths.ConfigPath
            })
            {
                var candidate = Path.GetFullPath(Path.Combine(allowedRoot, relative));
                var root = Path.GetFullPath(allowedRoot);
                if (IsPathUnderRoot(candidate, root))
                {
                    yield return candidate;
                }
            }
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
        CaptureDisabledFontState(font);
        if (font.characterLookupTable != null)
        {
            font.characterLookupTable.Clear();
        }

        font.atlasPopulationMode = AtlasPopulationMode.Static;
    }

    private static void CaptureDisabledFontState(TMP_FontAsset font)
    {
        var fontId = font.GetInstanceID();
        if (DisabledFontStates.ContainsKey(fontId))
        {
            return;
        }

        var characterLookupTable = font.characterLookupTable == null
            ? null
            : new Dictionary<uint, TMP_Character>(font.characterLookupTable);
        var glyphLookupTable = font.glyphLookupTable == null
            ? null
            : new Dictionary<uint, Glyph>(font.glyphLookupTable);

        DisabledFontStates[fontId] = new FontMutationState(
            font,
            characterLookupTable,
            glyphLookupTable,
            font.atlasPopulationMode);
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
            return new Regex(source, RegexOptions.Compiled, RegexTimeout);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"EmbeddedFontPatcher invalid regex '{source}', fallback to '{fallback}'. Error: {ex.Message}");
            return new Regex(fallback, RegexOptions.Compiled, RegexTimeout);
        }
    }

    private static string ValidateFontAssetsPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
        if (Path.IsPathRooted(normalized))
        {
            Plugin.Log.LogWarning($"EmbeddedFontPatcher rejected rooted FontAssetsPath: {value}");
            return string.Empty;
        }

        foreach (var segment in normalized.Split(Path.DirectorySeparatorChar))
        {
            if (segment == "..")
            {
                Plugin.Log.LogWarning($"EmbeddedFontPatcher rejected traversing FontAssetsPath: {value}");
                return string.Empty;
            }
        }

        return normalized;
    }

    private static bool IsPathUnderRoot(string candidate, string root)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAddNormalFallbacks(FontBundlePair bundle)
    {
        return bundle.Normal != null;
    }

    private static bool ShouldAddTransmitFallbacks(FontBundlePair bundle)
    {
        return bundle.Transmit != null;
    }

    private static bool IsRegexMatch(Regex regex, string value, string label)
    {
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException ex)
        {
            Plugin.Log.LogWarning($"EmbeddedFontPatcher regex timeout in {label} for '{value}': {ex.Message}");
            return false;
        }
    }

    public static void Shutdown()
    {
        RemoveAddedBundleFallbacks();
        RestoreDisabledFontStates();
        UnloadAssetBundles();
        LoadedBundles.Clear();
        PatchedFontIds.Clear();
        ScannedDirectories.Clear();
        _normalRegex = null;
        _transmitRegex = null;
        _bundleNameRegex = null;
        _initialized = false;
    }

    private static void RestoreDisabledFontStates()
    {
        foreach (var state in DisabledFontStates.Values)
        {
            try
            {
                var font = state.FontAsset;
                if (font == null)
                {
                    continue;
                }

                if (state.CharacterLookupTable != null)
                {
                    // Current TMP exposes lookup dictionaries as read-only properties; older TMP builds allowed characterLookupTable = new Dictionary<uint, TMP_Character>(...).
                    RestoreLookupTable(font.characterLookupTable, state.CharacterLookupTable);
                }

                if (state.GlyphLookupTable != null)
                {
                    // Current TMP uses UnityEngine.TextCore.Glyph here; older TMP builds exposed glyphLookupTable = new Dictionary<uint, TMP_Glyph>(...).
                    RestoreLookupTable(font.glyphLookupTable, state.GlyphLookupTable);
                }

                font.atlasPopulationMode = state.AtlasPopulationMode;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"EmbeddedFontPatcher failed restoring disabled font state: {ex.GetType().Name}: {ex.Message}");
            }
        }

        DisabledFontStates.Clear();
    }

    private static void RestoreLookupTable<T>(Dictionary<uint, T>? target, Dictionary<uint, T> original)
    {
        if (target == null)
        {
            return;
        }

        target.Clear();
        foreach (var entry in original)
        {
            target[entry.Key] = entry.Value;
        }
    }

    private static void UnloadAssetBundles()
    {
        RemoveAddedBundleFallbacks();
        foreach (var bundle in LoadedAssetBundles)
        {
            if (bundle != null)
            {
                bundle.Unload(false);
            }
        }

        LoadedAssetBundles.Clear();
    }

    private static void TrackAddedFallback(TMP_FontAsset fontAsset, TMP_FontAsset fallback)
    {
        var fontId = fontAsset.GetInstanceID();
        if (!AddedBundleFallbacks.TryGetValue(fontId, out var state))
        {
            state = new FontFallbackPatchState(fontAsset);
            AddedBundleFallbacks[fontId] = state;
        }

        if (!state.AddedFallbacks.Contains(fallback))
        {
            state.AddedFallbacks.Add(fallback);
        }
    }

    private static void RemoveAddedBundleFallbacks()
    {
        foreach (var state in AddedBundleFallbacks.Values)
        {
            var fontAsset = state.FontAsset;
            if (fontAsset == null || fontAsset.fallbackFontAssetTable == null)
            {
                continue;
            }

            foreach (var fallback in state.AddedFallbacks)
            {
                if (fallback != null)
                {
                    fontAsset.fallbackFontAssetTable.Remove(fallback);
                }
            }
        }

        AddedBundleFallbacks.Clear();
    }
}
