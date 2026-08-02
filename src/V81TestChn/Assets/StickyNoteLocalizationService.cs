using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace V81TestChn;

internal static class StickyNoteLocalizationService
{
    private sealed class RendererState
    {
        public RendererState(MeshRenderer renderer, Material[] originalMaterials, List<Material> localizedMaterials)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            LocalizedMaterials = localizedMaterials;
        }

        public MeshRenderer Renderer { get; }
        public Material[] OriginalMaterials { get; }
        public List<Material> LocalizedMaterials { get; }
    }

    private const string ItemName = "StickyNoteItem";
    private const string MaterialName = "StickyNoteMaterial";
    private const string TextureFileName = "StickyNoteLocalized.png";
    private const int MaxTextureFileBytes = 8 * 1024 * 1024;
    private const int ExpectedTextureWidth = 1024;
    private const int ExpectedTextureHeight = 512;

    private static readonly Dictionary<int, RendererState> RendererStates = new();
    private static string _texturePath = string.Empty;
    private static Texture2D? _localizedTexture;
    private static bool _initialized;
    private static bool _loadAttempted;

    public static void Initialize(string pluginDir)
    {
        _texturePath = Path.Combine(pluginDir, "textures", TextureFileName);
        _initialized = true;
        _loadAttempted = false;
    }

    public static void Apply(GrabbableObject? grabbableObject)
    {
        if (!_initialized || Plugin.IsRuntimeShuttingDown || grabbableObject == null ||
            !IsStickyNote(grabbableObject.gameObject.name) || !EnsureTextureLoaded())
        {
            return;
        }

        PruneDestroyedRendererStates();
        foreach (var renderer in grabbableObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            ApplyToRenderer(renderer);
        }
    }

    public static void Shutdown()
    {
        foreach (var state in RendererStates.Values)
        {
            try
            {
                if (state.Renderer != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                }
            }
            catch
            {
                // Unity objects may already be tearing down; restoration is best-effort.
            }

            foreach (var material in state.LocalizedMaterials)
            {
                DestroyUnityObject(material);
            }
        }

        RendererStates.Clear();
        DestroyUnityObject(_localizedTexture);
        _localizedTexture = null;
        _texturePath = string.Empty;
        _initialized = false;
        _loadAttempted = false;
    }

    private static bool IsStickyNote(string? objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               objectName.StartsWith(ItemName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EnsureTextureLoaded()
    {
        if (_loadAttempted)
        {
            return _localizedTexture != null;
        }

        _loadAttempted = true;
        Texture2D? texture = null;
        try
        {
            var fileInfo = new FileInfo(_texturePath);
            if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > MaxTextureFileBytes)
            {
                Plugin.Log.LogWarning($"Sticky-note localization texture is missing or invalid: {TextureFileName}");
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(_texturePath), true) ||
                texture.width != ExpectedTextureWidth || texture.height != ExpectedTextureHeight)
            {
                Plugin.Log.LogWarning(
                    $"Sticky-note localization texture must be {ExpectedTextureWidth}x{ExpectedTextureHeight}: " +
                    $"{texture.width}x{texture.height}");
                DestroyUnityObject(texture);
                return false;
            }

            texture.name = "StickyNoteTex (zh-CN)";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            _localizedTexture = texture;
            return true;
        }
        catch (Exception ex)
        {
            DestroyUnityObject(texture);
            Plugin.Log.LogWarning($"Sticky-note localization texture load failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void ApplyToRenderer(MeshRenderer? renderer)
    {
        if (renderer == null || RendererStates.ContainsKey(renderer.GetInstanceID()) || _localizedTexture == null)
        {
            return;
        }

        var originalMaterials = renderer.sharedMaterials;
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            return;
        }

        var localizedMaterials = (Material[])originalMaterials.Clone();
        var createdMaterials = new List<Material>();
        for (var index = 0; index < originalMaterials.Length; index++)
        {
            var originalMaterial = originalMaterials[index];
            if (originalMaterial == null ||
                !string.Equals(NormalizeMaterialName(originalMaterial.name), MaterialName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localizedMaterial = new Material(originalMaterial)
            {
                name = $"{MaterialName} (zh-CN)"
            };
            SetTexture(localizedMaterial, _localizedTexture);
            localizedMaterials[index] = localizedMaterial;
            createdMaterials.Add(localizedMaterial);
        }

        if (createdMaterials.Count == 0)
        {
            return;
        }

        renderer.sharedMaterials = localizedMaterials;
        RendererStates[renderer.GetInstanceID()] = new RendererState(renderer, originalMaterials, createdMaterials);
    }

    private static void PruneDestroyedRendererStates()
    {
        List<int>? destroyedIds = null;
        foreach (var pair in RendererStates)
        {
            if (pair.Value.Renderer != null)
            {
                continue;
            }

            destroyedIds ??= new List<int>();
            destroyedIds.Add(pair.Key);
            foreach (var material in pair.Value.LocalizedMaterials)
            {
                DestroyUnityObject(material);
            }
        }

        if (destroyedIds == null)
        {
            return;
        }

        foreach (var id in destroyedIds)
        {
            RendererStates.Remove(id);
        }
    }

    private static string NormalizeMaterialName(string? materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return string.Empty;
        }

        var normalized = materialName.Trim();
        foreach (var suffix in new[] { " (Instance)", " (zh-CN)" })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - suffix.Length);
            }
        }

        return normalized;
    }

    private static void SetTexture(Material material, Texture2D texture)
    {
        if (material.HasProperty("_BaseColorMap"))
        {
            material.SetTexture("_BaseColorMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        material.mainTexture = texture;
    }

    private static void DestroyUnityObject(UnityEngine.Object? value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(value);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
