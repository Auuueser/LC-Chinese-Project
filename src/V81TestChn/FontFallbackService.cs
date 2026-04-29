using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Color32 = UnityEngine.Color32;

namespace V81TestChn;

internal static class FontFallbackService
{
    private static readonly Regex CjkRegex = new(@"[\u3400-\u9FFF]", RegexOptions.Compiled);
    private static readonly Dictionary<int, Color> BaselineColorByInstance = new();
    private static readonly HashSet<int> FinalRenderSubscribedIds = new();
    private static readonly HashSet<int> SpecialCaseTextIds = new();
    private static readonly HashSet<int> FinalRenderRepairLoggedIds = new();
    private static readonly HashSet<int> RenderAuditLoggedIds = new();
    private static int _renderAuditBudget = 80;
    private static int _finalRenderRepairLogBudget = 80;
    private static int _specialCaseLogBudget = 60;
    private static int _focusedAuditBudget = 120;
    private static int _systemOnlineProbeLogBudget = 120;
    private static int _postTranslationProbeLogBudget = 160;
    private static int _runtimeCjkSweepLogBudget = 180;
    private static int _canvasGroupBypassLogBudget = 80;
    private static TMP_FontAsset? _fallbackFont;
    private static string? _pluginDir;
    private static bool _globalFallbackApplied;
    public static bool HasFallbackFont => _fallbackFont != null;

    public static void TryLoadFontAsset(string pluginDir)
    {
        _pluginDir = pluginDir;

        if (TryLoadFontFileAsset(pluginDir))
        {
            ApplyFallbackGlobally();
            return;
        }

        var bundlePath = Path.Combine(pluginDir, "V81TestChn", "fonts", "zh-cn-tmp-font");
        if (!File.Exists(bundlePath))
        {
            bundlePath = Path.Combine(pluginDir, "fonts", "zh-cn-tmp-font");
        }

        if (!File.Exists(bundlePath))
        {
            Plugin.Log.LogWarning($"Chinese TMP font bundle not found: {bundlePath}");
            TryLoadSystemFontAsset();
            return;
        }

        try
        {
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Plugin.Log.LogWarning("Failed to load Chinese TMP font bundle.");
                TryLoadSystemFontAsset();
                return;
            }

            foreach (var assetName in bundle.GetAllAssetNames())
            {
                _fallbackFont = bundle.LoadAsset<TMP_FontAsset>(assetName);
                if (_fallbackFont != null)
                {
                    NormalizeFallbackFontMaterials();
                    Plugin.Log.LogInfo($"Loaded Chinese fallback font: {_fallbackFont.name} from {bundlePath}");
                    ApplyFallbackGlobally();
                    return;
                }
            }

            Plugin.Log.LogWarning($"No TMP_FontAsset found in Chinese font bundle: {bundlePath}");
            TryLoadSystemFontAsset();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to load Chinese font bundle: {ex}");
            TryLoadSystemFontAsset();
        }
    }

    private static bool TryLoadFontFileAsset(string pluginDir)
    {
        foreach (var fontPath in new[]
        {
            Path.Combine(pluginDir, "V81TestChn", "fonts", "NotoSansSC-VF.ttf"),
            Path.Combine(pluginDir, "fonts", "NotoSansSC-VF.ttf"),
            @"C:\Windows\Fonts\NotoSansSC-VF.ttf",
            @"C:\Windows\Fonts\msyh.ttc",
            @"C:\Windows\Fonts\msyhbd.ttc",
            @"C:\Windows\Fonts\simsun.ttc"
        })
        {
            if (!File.Exists(fontPath))
            {
                continue;
            }

            try
            {
                if (TryCreateTmpFontAsset(new Font(fontPath), Path.GetFileNameWithoutExtension(fontPath)))
                {
                    Plugin.Log.LogInfo($"Loaded Chinese fallback font from font file: {fontPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Font file fallback failed for {fontPath}: {ex.Message}");
            }
        }

        return false;
    }

    private static void TryLoadSystemFontAsset()
    {
        if (_fallbackFont != null)
        {
            return;
        }

        foreach (var fontName in new[] { "Noto Sans SC", "Source Han Sans SC", "Microsoft YaHei UI", "Microsoft YaHei" })
        {
            try
            {
                if (TryCreateTmpFontAsset(Font.CreateDynamicFontFromOSFont(fontName, 18), fontName))
                {
                Plugin.Log.LogInfo($"Loaded Chinese fallback font from system font: {fontName}");
                    ApplyFallbackGlobally();
                    return;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"System font fallback failed for {fontName}: {ex.Message}");
            }
        }

        Plugin.Log.LogWarning("No compatible Chinese system font fallback was loaded.");
    }

    private static bool TryCreateTmpFontAsset(Font? font, string label)
    {
        if (font == null)
        {
            return false;
        }

        _fallbackFont = TMP_FontAsset.CreateFontAsset(
            font,
            90,
            9,
            GlyphRenderMode.SDFAA,
            4096,
            4096,
            AtlasPopulationMode.Dynamic,
            true);

        if (_fallbackFont == null)
        {
            return false;
        }

        _fallbackFont.name = $"V81TestChn_SystemFallback_{label}";
        _fallbackFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        NormalizeFallbackFontMaterials();
        WarmFallbackCharacters();
        return true;
    }

    public static void ApplyFallback(TMP_Text? text, string? candidateText = null)
    {
        if (_fallbackFont == null || text?.font == null)
        {
            return;
        }
        
        CaptureHealthyBaseline(text);
        ApplyFallbackToFont(text.font);
    }

    public static void RegisterTextInstance(TMP_Text? text, string stage)
    {
        if (text == null)
        {
            return;
        }

        if (IsCriticalTextForRepair(text, text.text))
        {
            EnsureFinalRenderRepair(text);
        }

        if (ShouldFocusAudit(text.text))
        {
            LogFocusedAudit(stage, text, text.textInfo);
        }
    }

    public static void ReconcileRenderedText(TMP_Text? text)
    {
        return;
    }

    public static void ApplySystemOnlineProbeFix(TMP_Text? text, string stage, string? candidateText = null)
    {
        return;
    }

    public static void RepairPostTranslationText(TMP_Text? text, string stage)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        if (IsAtmosphereRelayObject(text))
        {
            ApplyFallbackToFont(text.font);
            return;
        }

        if (IsSystemOnlineRelayObject(text))
        {
            return;
        }

        if (!CjkRegex.IsMatch(text.text))
        {
            return;
        }

        ApplyFallbackToFont(text.font);
        if (!ShouldRepairReadableCjkTextLight(text))
        {
            return;
        }

        RepairReadableCjkTextLight(text, stage);
    }

    public static void RepairLoadedCjkTextObjects(string stage)
    {
        return;
    }

    public static void ApplySystemOnlineProbeFix(UnityEngine.UI.Text? text, string stage, string? candidateText = null)
    {
        return;
    }

    public static void ApplySystemOnlineProbeFix(TextMesh? text, string stage, string? candidateText = null)
    {
        return;
    }

    public static void SanitizeSystemOnlineAssignedColor(TMP_Text? text, ref Color value, string stage)
    {
        return;
    }

    public static void RepairSystemOnlineRelayAppearance(TMP_Text? text)
    {
        return;
    }

    public static void OnFontAssetAwake(TMP_FontAsset? fontAsset)
    {
        if (fontAsset == null)
        {
            return;
        }

        ApplyFallbackToFont(fontAsset);
        NormalizeMaterial(fontAsset.material);
    }

    public static void ReconcileSubMeshMaterial(TMP_SubMeshUI? subMesh)
    {
        if (subMesh == null)
        {
            return;
        }

        var owner = subMesh.textComponent;
        if (owner == null || string.IsNullOrWhiteSpace(owner.text) || !CjkRegex.IsMatch(owner.text))
        {
            return;
        }

        EnsureFinalRenderRepair(owner);
        var primary = owner.fontSharedMaterial;
        var shared = subMesh.sharedMaterial;
        var instance = TryGetSubMeshUiMaterial(subMesh, "ReconcileSubMeshMaterial(UI)");

        if (primary != null)
        {
            SyncMaterialWithPrimary(shared, primary);
            SyncMaterialWithPrimary(instance, primary);
        }

        ApplyFaceColorFromOwner(shared, owner);
        ApplyFaceColorFromOwner(instance, owner);
        subMesh.color = owner.color;
        AuditIfStillDark("ReconcileSubMeshMaterial(UI)", owner);
    }

    public static void ReconcileSubMeshMaterial(TMP_SubMesh? subMesh)
    {
        if (subMesh == null)
        {
            return;
        }

        var owner = subMesh.textComponent;
        if (owner == null || string.IsNullOrWhiteSpace(owner.text) || !CjkRegex.IsMatch(owner.text))
        {
            return;
        }

        EnsureFinalRenderRepair(owner);
        var primary = owner.fontSharedMaterial;
        if (primary != null)
        {
            SyncMaterialWithPrimary(subMesh.sharedMaterial, primary);
            SyncMaterialWithPrimary(subMesh.material, primary);
        }

        ApplyFaceColorFromOwner(subMesh.sharedMaterial, owner);
        ApplyFaceColorFromOwner(subMesh.material, owner);
        AuditIfStillDark("ReconcileSubMeshMaterial(3D)", owner);
    }

    public static void SanitizeAssignedColor(TMP_Text? text, ref Color value, string? candidateText = null)
    {
        if (text == null)
        {
            return;
        }

        var displayedText = string.IsNullOrWhiteSpace(candidateText) ? text.text : candidateText;
        if (string.IsNullOrWhiteSpace(displayedText))
        {
            return;
        }

        if (IsAtmosphereRelayObject(text))
        {
            return;
        }

        var textId = text.GetInstanceID();
        if (!BaselineColorByInstance.ContainsKey(textId))
        {
            TryStoreHealthyBaseline(textId, text.color);
        }

        var containsCjk = CjkRegex.IsMatch(displayedText);
        if (!containsCjk)
        {
            TryStoreHealthyBaseline(textId, value);
            return;
        }

        if (!ShouldRepairReadableCjkTextLight(text))
        {
            return;
        }

        EnsureFinalRenderRepair(text);
        if (TryGetHealthyBaseline(textId, out var baselineColor))
        {
            if (value.a < 0.999f || IsNearlyBlack(value))
            {
                value = new Color(baselineColor.r, baselineColor.g, baselineColor.b, baselineColor.a);
            }
            return;
        }

        if (value.a < 0.35f || IsNearlyBlack(value))
        {
            value = new Color(1f, 1f, 1f, 1f);
        }
    }

    public static void ApplyFallbackGlobally()
    {
        if (_fallbackFont == null || _globalFallbackApplied)
        {
            return;
        }

        var globalFallbacks = TMP_Settings.fallbackFontAssets;
        if (globalFallbacks != null && !globalFallbacks.Contains(_fallbackFont))
        {
            globalFallbacks.Add(_fallbackFont);
        }

        foreach (var fontAsset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            ApplyFallbackToFont(fontAsset);
        }

        _globalFallbackApplied = true;
        Plugin.Log.LogInfo($"Applied Chinese fallback font globally: {_fallbackFont.name}");
    }

    private static void ApplyFallbackToFont(TMP_FontAsset? fontAsset)
    {
        if (_fallbackFont == null || fontAsset == null || ReferenceEquals(fontAsset, _fallbackFont))
        {
            return;
        }

        if (fontAsset.fallbackFontAssetTable == null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (!fontAsset.fallbackFontAssetTable.Contains(_fallbackFont))
        {
            fontAsset.fallbackFontAssetTable.Add(_fallbackFont);
        }
    }

    private static void WarmFallbackCharacters()
    {
        if (_fallbackFont == null)
        {
            return;
        }

        var charsetPath = _pluginDir == null
            ? null
            : Path.Combine(_pluginDir, "V81TestChn", "fonts", "zh-cn-charset.txt");
        if (charsetPath == null || !File.Exists(charsetPath))
        {
            charsetPath = Path.Combine(PathsRelativePluginRoot(), "fonts", "zh-cn-charset.txt");
        }

        if (!File.Exists(charsetPath))
        {
            return;
        }

        try
        {
            var characters = File.ReadAllText(charsetPath);
            characters = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (_fallbackFont.TryAddCharacters(characters, out var missingCharacters) && string.IsNullOrEmpty(missingCharacters))
            {
                Plugin.Log.LogInfo($"Prewarmed Chinese fallback font characters: {characters.Length}");
            }
            else if (!string.IsNullOrEmpty(missingCharacters))
            {
                Plugin.Log.LogWarning($"Chinese fallback font missing {missingCharacters.Length} prewarm characters.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"Failed to prewarm Chinese fallback characters: {ex.Message}");
        }
    }

    private static string PathsRelativePluginRoot()
    {
        return _pluginDir ?? BepInEx.Paths.PluginPath;
    }

    private static void NormalizeFallbackFontMaterials()
    {
        if (_fallbackFont == null)
        {
            return;
        }

        NormalizeMaterial(_fallbackFont.material);
    }

    private static void NormalizeMaterial(Material? material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        }

        if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
        {
            var outline = material.GetColor(ShaderUtilities.ID_OutlineColor);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(outline.r, outline.g, outline.b, 0f));
        }

        if (material.HasProperty(ShaderUtilities.ID_FaceDilate))
        {
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            var underlay = material.GetColor(ShaderUtilities.ID_UnderlayColor);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(underlay.r, underlay.g, underlay.b, 0f));
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayDilate))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
        }

        material.DisableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        material.DisableKeyword("OUTLINE_ON");
    }

    private static void ImproveReadabilityForOverlayChinese(TMP_Text text, string? candidateText)
    {
        if (text == null)
        {
            return;
        }

        var displayedText = string.IsNullOrWhiteSpace(candidateText) ? text.text : candidateText;
        if (string.IsNullOrWhiteSpace(displayedText))
        {
            return;
        }

        var textId = text.GetInstanceID();
        if (!BaselineColorByInstance.ContainsKey(textId))
        {
            // Capture the component's original style color as early as possible.
            TryStoreHealthyBaseline(textId, text.color);
        }

        var containsCjk = CjkRegex.IsMatch(displayedText);
        if (!containsCjk)
        {
            TryStoreHealthyBaseline(textId, text.color);
            return;
        }

        if (IsAtmosphereHeader(displayedText))
        {
            ApplyAtmosphereHeaderStyle(text);
            return;
        }

        SyncSubMeshMaterials(text);

        var color = text.color;
        var shouldRepair = color.a < 0.999f || IsNearlyBlack(color);
        if (!shouldRepair)
        {
            return;
        }

        if (TryGetHealthyBaseline(textId, out var baselineColor))
        {
            text.color = new Color(baselineColor.r, baselineColor.g, baselineColor.b, baselineColor.a);
            return;
        }

        // Final fallback: avoid black/transparent Chinese text if no baseline is available.
        text.color = color.a < 0.999f || IsNearlyBlack(color)
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(color.r, color.g, color.b, 1f);
    }

    private static void RepairReadableCjkTextLight(TMP_Text text, string stage)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text) || !CjkRegex.IsMatch(text.text))
        {
            return;
        }

        CaptureHealthyBaseline(text);
        var target = ResolveExpectedColor(text);
        if (target.a < 0.999f)
        {
            target.a = 1f;
        }

        var colorChanged = false;
        if (NeedsGraphicColorRepair(text.color, target))
        {
            text.color = target;
            colorChanged = true;
        }

        var materialsChanged = RepairReadableFaceMaterials(text, target);
        if (HasAnomalousCjkVertex(text))
        {
            FixCjkVertexColors(text);
        }

        if ((colorChanged || materialsChanged) && _postTranslationProbeLogBudget > 0)
        {
            _postTranslationProbeLogBudget--;
            var fontName = text.font != null ? text.font.name : string.Empty;
            Plugin.Log.LogWarning(
                $"PostTranslationCjkRepairLight[{stage}] name={text.name}, font='{fontName}', after={text.color}, text='{TrimAuditText(text.text)}'");
        }
    }

    private static bool ShouldRepairReadableCjkTextLight(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (IsSystemOnlineRelayObject(text) || IsAtmosphereRelayObject(text))
        {
            return false;
        }

        return false;
    }

    private static bool IsNearlyBlack(Color color)
    {
        return color.r < 0.08f && color.g < 0.08f && color.b < 0.08f;
    }

    private static bool IsHealthyColor(Color color)
    {
        return !IsNearlyBlack(color) && color.a >= 0.35f;
    }

    private static bool TryGetHealthyBaseline(int textId, out Color baseline)
    {
        if (BaselineColorByInstance.TryGetValue(textId, out baseline) && IsHealthyColor(baseline))
        {
            return true;
        }

        baseline = default;
        return false;
    }

    private static void TryStoreHealthyBaseline(int textId, Color candidate)
    {
        if (IsHealthyColor(candidate))
        {
            BaselineColorByInstance[textId] = candidate;
        }
    }

    private static void SyncSubMeshMaterials(TMP_Text text)
    {
        var primary = text.fontSharedMaterial;
        if (primary == null)
        {
            return;
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
        {
            SyncMaterialWithPrimary(subMesh.sharedMaterial, primary);
            SyncMaterialWithPrimary(TryGetSubMeshUiMaterial(subMesh, "SyncSubMeshMaterials"), primary);
            subMesh.color = text.color;
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
        {
            SyncMaterialWithPrimary(subMesh.sharedMaterial, primary);
            SyncMaterialWithPrimary(subMesh.material, primary);
        }
    }

    private static void FixCjkVertexColors(TMP_Text text)
    {
        if (text.textInfo == null || string.IsNullOrEmpty(text.text))
        {
            return;
        }

        if (!CjkRegex.IsMatch(text.text))
        {
            return;
        }

        var expected = ResolveExpectedColor(text);
        var expected32 = (Color32)expected;
        var info = text.textInfo;
        var changed = false;

        for (var i = 0; i < info.characterCount; i++)
        {
            var charInfo = info.characterInfo[i];
            if (!charInfo.isVisible || !IsCjk(charInfo.character))
            {
                continue;
            }

            var materialIndex = charInfo.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= info.meshInfo.Length)
            {
                continue;
            }

            var colors = info.meshInfo[materialIndex].colors32;
            var vi = charInfo.vertexIndex;
            if (colors == null || vi < 0 || vi + 3 >= colors.Length)
            {
                continue;
            }

            if (NeedsVertexRepair(colors[vi], expected32) ||
                NeedsVertexRepair(colors[vi + 1], expected32) ||
                NeedsVertexRepair(colors[vi + 2], expected32) ||
                NeedsVertexRepair(colors[vi + 3], expected32))
            {
                colors[vi] = expected32;
                colors[vi + 1] = expected32;
                colors[vi + 2] = expected32;
                colors[vi + 3] = expected32;
                changed = true;
            }
        }

        if (changed)
        {
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    private static void EnsureFinalRenderRepair(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        var id = text.GetInstanceID();
        if (!FinalRenderSubscribedIds.Add(id))
        {
            return;
        }

        text.OnPreRenderText += OnPreRenderText;
    }

    private static void OnPreRenderText(TMP_TextInfo textInfo)
    {
        try
        {
            ReconcileFinalRenderText(textInfo?.textComponent, textInfo);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"Final TMP render repair failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ReconcileFinalRenderText(TMP_Text? text, TMP_TextInfo? textInfo)
    {
        if (_fallbackFont == null || text?.font == null || textInfo == null)
        {
            return;
        }

        var displayedText = text.text;
        if (string.IsNullOrWhiteSpace(displayedText))
        {
            return;
        }

        var focusedAudit = ShouldFocusAudit(displayedText);
        var isCritical = IsCriticalTextForRepair(text, displayedText);
        if (!focusedAudit && !isCritical)
        {
            return;
        }

        ApplyFallbackToFont(text.font);
        if (focusedAudit)
        {
            LogFocusedAudit("OnPreRenderText.BeforeRepair", text, textInfo);
        }

        if (IsAtmosphereHeader(displayedText))
        {
            ApplyAtmosphereHeaderStyle(text);
            LogFinalRenderRepair(text, "atmosphere-header");
            return;
        }

        CaptureHealthyBaseline(text);
        var expected = ResolveExpectedColor(text);
        if (expected.a < 0.85f)
        {
            expected.a = 1f;
        }

        var changedVertices = RepairCjkVertexColors(textInfo, (Color32)expected);
        var changedMaterials = RepairFinalMaterials(text, expected);

        if (changedVertices)
        {
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        if (changedVertices || changedMaterials)
        {
            LogFinalRenderRepair(text, $"vertices={changedVertices}, materials={changedMaterials}, expected={expected}");
        }

        if (focusedAudit)
        {
            LogFocusedAudit("OnPreRenderText.AfterRepair", text, textInfo);
        }
    }

    private static void CaptureHealthyBaseline(TMP_Text text)
    {
        var id = text.GetInstanceID();
        var color = text.color;
        if (IsHealthyColor(color))
        {
            BaselineColorByInstance[id] = color;
            return;
        }

        if (TryGetHealthyBaseline(id, out _))
        {
            return;
        }

        var primary = text.fontSharedMaterial;
        if (primary != null && primary.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            var face = primary.GetColor(ShaderUtilities.ID_FaceColor);
            if (IsHealthyColor(face))
            {
                BaselineColorByInstance[id] = face;
            }
        }
    }

    private static bool RepairCjkVertexColors(TMP_TextInfo info, Color32 expected)
    {
        var changed = false;
        for (var i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible || !IsCjk(ch.character))
            {
                continue;
            }

            var materialIndex = ch.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= info.meshInfo.Length)
            {
                continue;
            }

            var colors = info.meshInfo[materialIndex].colors32;
            var vi = ch.vertexIndex;
            if (colors == null || vi < 0 || vi + 3 >= colors.Length)
            {
                continue;
            }

            if (!NeedsFinalVertexRepair(colors[vi], expected) &&
                !NeedsFinalVertexRepair(colors[vi + 1], expected) &&
                !NeedsFinalVertexRepair(colors[vi + 2], expected) &&
                !NeedsFinalVertexRepair(colors[vi + 3], expected))
            {
                continue;
            }

            colors[vi] = expected;
            colors[vi + 1] = expected;
            colors[vi + 2] = expected;
            colors[vi + 3] = expected;
            changed = true;
        }

        return changed;
    }

    private static bool RepairFinalMaterials(TMP_Text text, Color expected)
    {
        var changed = false;
        changed |= RepairMaterialFaceColor(text.fontSharedMaterial, expected);
        changed |= RepairMaterialFaceColor(text.fontMaterial, expected);

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
        {
            changed |= RepairMaterialFaceColor(subMesh.sharedMaterial, expected);
            changed |= RepairMaterialFaceColor(TryGetSubMeshUiMaterial(subMesh, "RepairFinalMaterials"), expected);
            if (NeedsGraphicColorRepair(subMesh.color, expected))
            {
                subMesh.color = expected;
                changed = true;
            }
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
        {
            changed |= RepairMaterialFaceColor(subMesh.sharedMaterial, expected);
            changed |= RepairMaterialFaceColor(subMesh.material, expected);
        }

        return changed;
    }

    private static bool RepairMaterialFaceColor(Material? material, Color expected)
    {
        if (material == null || !material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            return false;
        }

        var face = material.GetColor(ShaderUtilities.ID_FaceColor);
        if (!NeedsGraphicColorRepair(face, expected))
        {
            return false;
        }

        ForceMaterialFaceColor(material, expected);
        return true;
    }

    private static bool NeedsFinalVertexRepair(Color32 actual, Color32 expected)
    {
        var nearBlack = actual.r < 20 && actual.g < 20 && actual.b < 20;
        var lowAlpha = actual.a < 90 && expected.a >= 90;
        if (nearBlack || lowAlpha)
        {
            return true;
        }

        var actualBrightness = actual.r + actual.g + actual.b;
        var expectedBrightness = expected.r + expected.g + expected.b;
        return expectedBrightness > 120 &&
               actualBrightness + 120 < expectedBrightness &&
               actual.a + 40 < expected.a;
    }

    private static bool NeedsGraphicColorRepair(Color actual, Color expected)
    {
        var nearBlack = IsNearlyBlack(actual);
        var lowAlpha = actual.a < 0.35f && expected.a >= 0.35f;
        if (nearBlack || lowAlpha)
        {
            return true;
        }

        var actualBrightness = actual.r + actual.g + actual.b;
        var expectedBrightness = expected.r + expected.g + expected.b;
        return expectedBrightness > 0.45f &&
               actualBrightness + 0.45f < expectedBrightness &&
               actual.a + 0.15f < expected.a;
    }

    private static void LogFinalRenderRepair(TMP_Text text, string reason)
    {
        if (_finalRenderRepairLogBudget <= 0)
        {
            return;
        }

        if (!FinalRenderRepairLoggedIds.Add(text.GetInstanceID()))
        {
            return;
        }

        _finalRenderRepairLogBudget--;
        Plugin.Log.LogWarning(
            $"FinalRenderRepair name={text.name}, type={text.GetType().Name}, color={text.color}, reason={reason}, text='{TrimAuditText(text.text)}'");
    }

    private static bool ShouldFocusAudit(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsOrdinalIgnoreCase(text, "SYSTEMS ONLINE") ||
               ContainsOrdinalIgnoreCase(text, "joined the ship") ||
               ContainsOrdinalIgnoreCase(text, "started the ship") ||
               ContainsOrdinalIgnoreCase(text, "ENTERING THE ATMOSPHERE") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u5728\u7ebf") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u4e0a\u7ebf") ||
               ContainsOrdinalIgnoreCase(text, "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42") ||
               ContainsOrdinalIgnoreCase(text, "\u8fdb\u5165\u5927\u6c14\u5c42");
    }

    private static bool IsSystemOnlineProbeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsOrdinalIgnoreCase(text, "SYSTEMS ONLINE") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u5728\u7ebf") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u4e0a\u7ebf");
    }

    private static void LogFocusedAudit(string stage, TMP_Text text, TMP_TextInfo? info)
    {
        if (_focusedAuditBudget <= 0 || text == null)
        {
            return;
        }

        _focusedAuditBudget--;
        var sharedMat = text.fontSharedMaterial;
        var sharedFace = sharedMat != null && sharedMat.HasProperty(ShaderUtilities.ID_FaceColor)
            ? sharedMat.GetColor(ShaderUtilities.ID_FaceColor).ToString()
            : "N/A";
        var materialFace = text.fontMaterial != null && text.fontMaterial.HasProperty(ShaderUtilities.ID_FaceColor)
            ? text.fontMaterial.GetColor(ShaderUtilities.ID_FaceColor).ToString()
            : "N/A";

        var cjkVisible = 0;
        var lowAlphaVertices = 0;
        var blackVertices = 0;
        if (info != null)
        {
            for (var i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || !IsCjk(ch.character))
                {
                    continue;
                }

                cjkVisible++;
                var mi = ch.materialReferenceIndex;
                var vi = ch.vertexIndex;
                if (mi < 0 || mi >= info.meshInfo.Length)
                {
                    continue;
                }

                var colors = info.meshInfo[mi].colors32;
                if (colors == null || vi < 0 || vi >= colors.Length)
                {
                    continue;
                }

                var c = colors[vi];
                if (c.a < 90)
                {
                    lowAlphaVertices++;
                }

                if (c.r < 20 && c.g < 20 && c.b < 20)
                {
                    blackVertices++;
                }
            }
        }

        Plugin.Log.LogWarning(
            $"FocusedAudit[{stage}] name={text.name}, type={text.GetType().Name}, color={text.color}, sharedFace={sharedFace}, fontMatFace={materialFace}, cjkVisible={cjkVisible}, cjkLowAlpha={lowAlphaVertices}, cjkBlack={blackVertices}, text='{TrimAuditText(text.text)}'");
    }

    private static void LogSystemOnlineProbe(string stage, string type, Component component, Color before, Color after, string text)
    {
        if (_systemOnlineProbeLogBudget <= 0)
        {
            return;
        }

        _systemOnlineProbeLogBudget--;
        Plugin.Log.LogWarning(
            $"SystemOnlineProbe[{stage}] type={type}, name={component.name}, path={GetObjectPath(component)}, before={before}, after={after}, text='{TrimAuditText(text)}'");
    }

    private static void LogRuntimeCjkSweepCandidate(
        string stage,
        string type,
        Component component,
        string fontName,
        bool isSystemOnline,
        bool isBFont,
        Color color,
        float parentCanvasGroupAlpha,
        string text)
    {
        if (_runtimeCjkSweepLogBudget <= 0)
        {
            return;
        }

        _runtimeCjkSweepLogBudget--;
        Plugin.Log.LogWarning(
            $"RuntimeCjkCandidate[{stage}] type={type}, name={component.name}, path={GetObjectPath(component)}, font='{fontName}', color={color}, parentCanvasGroupAlpha={parentCanvasGroupAlpha:0.###}, systemOnline={isSystemOnline}, bFont={isBFont}, text='{TrimAuditText(text)}'");
    }

    private static float GetParentCanvasGroupAlpha(Component component)
    {
        var alpha = 1f;
        var current = component.transform;
        while (current != null)
        {
            var group = current.GetComponent<CanvasGroup>();
            if (group != null)
            {
                alpha *= group.alpha;
            }

            current = current.parent;
        }

        return alpha;
    }

    private static void BypassParentCanvasGroups(Component component, string stage, string? text)
    {
        if (!IsCriticalComponentForRepair(component, text))
        {
            return;
        }

        if (ShouldPreserveOriginalParentAlpha(component))
        {
            LogCanvasGroupDecision(stage, component, "preserve-original", GetParentCanvasGroupAlpha(component), text);
            return;
        }

        var parentAlpha = GetParentCanvasGroupAlpha(component);
        if (parentAlpha >= 0.999f)
        {
            return;
        }

        var group = component.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = component.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 1f;
        group.ignoreParentGroups = true;
        group.blocksRaycasts = false;
        group.interactable = false;

        LogCanvasGroupDecision(stage, component, "bypass", parentAlpha, text);
    }

    private static string GetObjectPath(Component component)
    {
        var parts = new Stack<string>();
        var current = component.transform;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts.ToArray());
    }

    private static bool IsCjk(uint codePoint)
    {
        return codePoint >= 0x3400 && codePoint <= 0x9FFF;
    }

    private static bool ShouldPreserveOriginalParentAlpha(Component? component)
    {
        return IsSystemOnlineRelayObject(component) || IsAtmosphereRelayObject(component);
    }

    private static bool IsSystemOnlineRelayObject(Component? component)
    {
        var transform = component?.transform;
        if (transform == null || !string.Equals(transform.name, "TipLeft1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parent = transform.parent;
        return parent != null && string.Equals(parent.name, "SystemsOnline", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAtmosphereRelayObject(Component? component)
    {
        var transform = component?.transform;
        if (transform == null || !string.Equals(transform.name, "LoadText", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parent = transform.parent;
        return parent != null && string.Equals(parent.name, "LoadingText", StringComparison.OrdinalIgnoreCase);
    }

    private static Color ResolveExpectedColor(TMP_Text text)
    {
        if (TryGetHealthyBaseline(text.GetInstanceID(), out var baseline))
        {
            return baseline;
        }

        var primary = text.fontSharedMaterial;
        if (primary != null && primary.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            var face = primary.GetColor(ShaderUtilities.ID_FaceColor);
            if (IsHealthyColor(face))
            {
                return face;
            }
        }

        var current = text.color;
        if (IsHealthyColor(current))
        {
            return current;
        }

        return new Color(1f, 1f, 1f, 1f);
    }

    private static Color ResolveAtmosphereExpectedColor(TMP_Text text)
    {
        if (TryGetHealthyBaseline(text.GetInstanceID(), out var baseline))
        {
            return new Color(baseline.r, baseline.g, baseline.b, 1f);
        }

        var primary = text.fontSharedMaterial;
        if (primary != null && primary.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            var face = primary.GetColor(ShaderUtilities.ID_FaceColor);
            if (IsHealthyColor(face))
            {
                return new Color(face.r, face.g, face.b, 1f);
            }
        }

        var current = text.color;
        if (IsHealthyColor(current))
        {
            return new Color(current.r, current.g, current.b, 1f);
        }

        return new Color(0.63f, 0.82f, 0.90f, 1f);
    }

    private static bool NeedsVertexRepair(Color32 actual, Color32 expected)
    {
        var nearBlack = actual.r < 20 && actual.g < 20 && actual.b < 20;
        var lowAlpha = actual.a < 90;
        if (nearBlack || lowAlpha)
        {
            return true;
        }

        // If very close to expected style color, keep as-is.
        var dr = Mathf.Abs(actual.r - expected.r);
        var dg = Mathf.Abs(actual.g - expected.g);
        var db = Mathf.Abs(actual.b - expected.b);
        var da = Mathf.Abs(actual.a - expected.a);
        return dr + dg + db + da > 200;
    }

    private static bool IsAtmosphereHeader(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsOrdinalIgnoreCase(text, "ENTERING THE ATMOSPHERE") ||
               ContainsOrdinalIgnoreCase(text, "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42") ||
               ContainsOrdinalIgnoreCase(text, "\u8fdb\u5165\u5927\u6c14\u5c42");
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string needle)
    {
        return source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ApplyAtmosphereHeaderStyle(TMP_Text text)
    {
        // Preserve the original relay animation chain and only repair the local face/vertex color.
        var target = ResolveAtmosphereExpectedColor(text);
        BaselineColorByInstance[text.GetInstanceID()] = target;
        text.color = target;
        ForceMaterialFaceColor(text.fontSharedMaterial, target);
        ForceMaterialFaceColor(text.fontMaterial, target);
        ForceCjkVertexColors(text, (Color32)target);
        ForceSubMeshFaceColors(text, target);
        text.SetAllDirty();
    }

    private static void RepairUiTextAppearance(UnityEngine.UI.Text text, string stage, bool isSystemOnline, bool isAtmosphere)
    {
        var before = text.color;
        text.color = ResolveGraphicRepairColor(text.text, text.color, isAtmosphere);
        BypassParentCanvasGroups(text, stage, text.text);
        if (isSystemOnline)
        {
            LogSystemOnlineProbe(stage, "UI.Text", text, before, text.color, text.text);
        }
    }

    private static void RepairTextMeshAppearance(TextMesh text, string stage, bool isSystemOnline, bool isAtmosphere)
    {
        var before = text.color;
        text.color = ResolveGraphicRepairColor(text.text, text.color, isAtmosphere);
        if (isSystemOnline)
        {
            LogSystemOnlineProbe(stage, "TextMesh", text, before, text.color, text.text);
        }
    }

    private static Color ResolveGraphicRepairColor(string? text, Color current, bool isAtmosphere)
    {
        if (isAtmosphere || IsAtmosphereHeader(text ?? string.Empty))
        {
            return IsHealthyColor(current)
                ? new Color(current.r, current.g, current.b, 1f)
                : new Color(0.63f, 0.82f, 0.90f, 1f);
        }

        if (IsNearlyBlack(current))
        {
            return Color.white;
        }

        return new Color(current.r, current.g, current.b, 1f);
    }

    private static void LogCanvasGroupDecision(string stage, Component component, string action, float parentAlpha, string? text)
    {
        if (_canvasGroupBypassLogBudget <= 0)
        {
            return;
        }

        _canvasGroupBypassLogBudget--;
        Plugin.Log.LogWarning(
            $"CanvasGroupBypass[{stage}] action={action}, type={component.GetType().Name}, name={component.name}, path={GetObjectPath(component)}, parentAlpha={parentAlpha:0.###}, text='{TrimAuditText(text ?? string.Empty)}'");
    }

    private static void DetectAndRegisterSpecialCase(TMP_Text text)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text) || !IsCriticalTextForRepair(text, text.text))
        {
            return;
        }

        var id = text.GetInstanceID();
        if (SpecialCaseTextIds.Contains(id))
        {
            return;
        }

        var anomaly = IsNearlyBlack(text.color) || text.color.a < 0.35f || HasAnomalousCjkVertex(text) || HasAnomalousSubMeshFace(text);
        if (!anomaly)
        {
            return;
        }

        SpecialCaseTextIds.Add(id);
        if (_specialCaseLogBudget > 0)
        {
            _specialCaseLogBudget--;
            Plugin.Log.LogWarning($"SpecialCaseText registered: name={text.name}, type={text.GetType().Name}, color={text.color}, text='{TrimAuditText(text.text)}'");
        }
    }

    private static bool HasAnomalousSubMeshFace(TMP_Text text)
    {
        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
        {
            if (HasAnomalousFaceColor(subMesh.sharedMaterial) || HasAnomalousFaceColor(TryGetSubMeshUiMaterial(subMesh, "HasAnomalousSubMeshFace")))
            {
                return true;
            }
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
        {
            if (HasAnomalousFaceColor(subMesh.sharedMaterial) || HasAnomalousFaceColor(subMesh.material))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnomalousFaceColor(Material? material)
    {
        if (material == null || !material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            return false;
        }

        var face = material.GetColor(ShaderUtilities.ID_FaceColor);
        return IsNearlyBlack(face) || face.a < 0.35f;
    }

    private static bool HasAnomalousCjkVertex(TMP_Text text)
    {
        var info = text.textInfo;
        if (info == null)
        {
            return false;
        }

        for (var i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible || !IsCjk(ch.character))
            {
                continue;
            }

            var materialIndex = ch.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= info.meshInfo.Length)
            {
                continue;
            }

            var colors = info.meshInfo[materialIndex].colors32;
            var vi = ch.vertexIndex;
            if (colors == null || vi < 0 || vi >= colors.Length)
            {
                continue;
            }

            var c = colors[vi];
            var nearBlack = c.r < 20 && c.g < 20 && c.b < 20;
            var lowAlpha = c.a < 90;
            if (nearBlack || lowAlpha)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplySpecialCaseFixIfNeeded(TMP_Text text)
    {
        if (!SpecialCaseTextIds.Contains(text.GetInstanceID()))
        {
            return;
        }

        var expected = ResolveExpectedColor(text);
        if (expected.a < 0.85f)
        {
            expected.a = 1f;
        }

        text.color = expected;
        ForceCjkVertexColors(text, (Color32)expected);
        ForceSubMeshFaceColors(text, expected);
    }

    private static void ForceCjkVertexColors(TMP_Text text, Color32 expected)
    {
        var info = text.textInfo;
        if (info == null)
        {
            return;
        }

        var changed = false;
        for (var i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible || !IsCjk(ch.character))
            {
                continue;
            }

            var materialIndex = ch.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= info.meshInfo.Length)
            {
                continue;
            }

            var colors = info.meshInfo[materialIndex].colors32;
            var vi = ch.vertexIndex;
            if (colors == null || vi < 0 || vi + 3 >= colors.Length)
            {
                continue;
            }

            colors[vi] = expected;
            colors[vi + 1] = expected;
            colors[vi + 2] = expected;
            colors[vi + 3] = expected;
            changed = true;
        }

        if (changed)
        {
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    private static void ForceSubMeshFaceColors(TMP_Text text, Color expected)
    {
        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
        {
            ForceMaterialFaceColor(subMesh.sharedMaterial, expected);
            ForceMaterialFaceColor(TryGetSubMeshUiMaterial(subMesh, "ForceSubMeshFaceColors"), expected);
            subMesh.color = expected;
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
        {
            ForceMaterialFaceColor(subMesh.sharedMaterial, expected);
            ForceMaterialFaceColor(subMesh.material, expected);
        }
    }

    private static bool RepairReadableFaceMaterials(TMP_Text text, Color expected)
    {
        var changed = false;
        changed |= RepairMaterialFaceColor(text.fontSharedMaterial, expected);
        changed |= RepairMaterialFaceColor(text.fontMaterial, expected);

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMeshUI>(true))
        {
            changed |= RepairMaterialFaceColor(subMesh.sharedMaterial, expected);
            changed |= RepairMaterialFaceColor(TryGetSubMeshUiMaterial(subMesh, "RepairReadableFaceMaterials"), expected);
            if (NeedsGraphicColorRepair(subMesh.color, expected))
            {
                subMesh.color = expected;
                changed = true;
            }
        }

        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
        {
            changed |= RepairMaterialFaceColor(subMesh.sharedMaterial, expected);
        }

        return changed;
    }

    private static void ForceMaterialFaceColor(Material? material, Color expected)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            material.SetColor(ShaderUtilities.ID_FaceColor, expected);
        }

        if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
        {
            var outline = material.GetColor(ShaderUtilities.ID_OutlineColor);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(outline.r, outline.g, outline.b, 0f));
        }

        if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
        {
            var underlay = material.GetColor(ShaderUtilities.ID_UnderlayColor);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(underlay.r, underlay.g, underlay.b, 0f));
        }
    }

    private static void SyncMaterialWithPrimary(Material? target, Material primary)
    {
        if (target == null || primary == null)
        {
            return;
        }

        CopyColorProperty(target, primary, ShaderUtilities.ID_FaceColor);
        CopyColorProperty(target, primary, ShaderUtilities.ID_OutlineColor);
        CopyColorProperty(target, primary, ShaderUtilities.ID_UnderlayColor);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_OutlineWidth);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_FaceDilate);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_UnderlayDilate);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_UnderlaySoftness);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_UnderlayOffsetX);
        CopyFloatProperty(target, primary, ShaderUtilities.ID_UnderlayOffsetY);

        CopyKeyword(target, primary, "UNDERLAY_ON");
        CopyKeyword(target, primary, "UNDERLAY_INNER");
        CopyKeyword(target, primary, "OUTLINE_ON");
    }

    private static Material? TryGetSubMeshUiMaterial(TMP_SubMeshUI? subMesh, string stage)
    {
        if (subMesh == null || subMesh.sharedMaterial == null)
        {
            return null;
        }

        try
        {
            return subMesh.material;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"{stage} skipped instance material for {subMesh.name}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static void CopyColorProperty(Material target, Material primary, int propertyId)
    {
        if (!target.HasProperty(propertyId) || !primary.HasProperty(propertyId))
        {
            return;
        }

        target.SetColor(propertyId, primary.GetColor(propertyId));
    }

    private static void CopyFloatProperty(Material target, Material primary, int propertyId)
    {
        if (!target.HasProperty(propertyId) || !primary.HasProperty(propertyId))
        {
            return;
        }

        target.SetFloat(propertyId, primary.GetFloat(propertyId));
    }

    private static void CopyKeyword(Material target, Material primary, string keyword)
    {
        if (primary.IsKeywordEnabled(keyword))
        {
            target.EnableKeyword(keyword);
        }
        else
        {
            target.DisableKeyword(keyword);
        }
    }

    private static void ApplyFaceColorFromOwner(Material? material, TMP_Text owner)
    {
        if (material == null || !material.HasProperty(ShaderUtilities.ID_FaceColor))
        {
            return;
        }

        var ownerColor = owner.color;
        if (BaselineColorByInstance.TryGetValue(owner.GetInstanceID(), out var baseline) && !IsNearlyBlack(baseline))
        {
            ownerColor = baseline;
        }

        material.SetColor(ShaderUtilities.ID_FaceColor, ownerColor);
    }

    private static void AuditIfStillDark(string stage, TMP_Text text)
    {
        if (_renderAuditBudget <= 0 || text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        if (!IsCriticalTextForRepair(text, text.text))
        {
            return;
        }

        var c = text.color;
        var anomalousVertex = HasAnomalousCjkVertex(text);
        var anomalousSubMesh = HasAnomalousSubMeshFace(text);
        if (!IsNearlyBlack(c) && c.a >= 0.35f && !anomalousVertex && !anomalousSubMesh)
        {
            return;
        }

        if (string.Equals(stage, "OnPreRenderText", StringComparison.Ordinal) &&
            !RenderAuditLoggedIds.Add(text.GetInstanceID()))
        {
            return;
        }

        _renderAuditBudget--;
        var primaryFace = Color.white;
        var primaryMaterial = text.fontSharedMaterial;
        var hasPrimaryFace = primaryMaterial != null && primaryMaterial.HasProperty(ShaderUtilities.ID_FaceColor);
        if (hasPrimaryFace)
        {
            primaryFace = primaryMaterial!.GetColor(ShaderUtilities.ID_FaceColor);
        }

        var baseline = BaselineColorByInstance.TryGetValue(text.GetInstanceID(), out var b) ? b : new Color(-1, -1, -1, -1);
        Plugin.Log.LogWarning(
            $"RenderAudit[{stage}] name={text.name}, type={text.GetType().Name}, color={c}, primaryFace={(hasPrimaryFace ? primaryFace.ToString() : "N/A")}, baseline={baseline}, anomalousVertex={anomalousVertex}, anomalousSubMesh={anomalousSubMesh}, text='{TrimAuditText(text.text)}'");
    }

    private static bool IsCriticalTextForRepair(TMP_Text? text, string? candidateText = null)
    {
        return false;
    }

    private static bool IsCriticalComponentForRepair(Component? component, string? text = null)
    {
        return false;
    }

    private static string TrimAuditText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        input = input.Replace('\n', ' ');
        return input.Length <= 64 ? input : input.Substring(0, 64);
    }
}



