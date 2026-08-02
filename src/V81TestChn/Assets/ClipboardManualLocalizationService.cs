using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace V81TestChn;

internal static class ClipboardManualLocalizationService
{
    private sealed class TextureSpec
    {
        public TextureSpec(string fileName, int width = 0, int height = 0, bool precompressedBc3 = false)
        {
            FileName = fileName;
            Width = width;
            Height = height;
            PrecompressedBc3 = precompressedBc3;
        }

        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
        public bool PrecompressedBc3 { get; }
    }

    private sealed class RendererState
    {
        public RendererState(Renderer renderer, Material[] originalMaterials, List<Material> localizedMaterials)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            LocalizedMaterials = localizedMaterials;
        }

        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }
        public List<Material> LocalizedMaterials { get; }
    }

    private const int MaxTextureFileBytes = 8 * 1024 * 1024;
    private const int MaxTextureDimension = 4096;
    private static readonly Dictionary<string, TextureSpec> TextureFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Manual1"] = new TextureSpec("ClipboardManualPage1.png"),
        ["Manual2"] = new TextureSpec("ClipboardManualPage2.png"),
        ["Manual3"] = new TextureSpec("ClipboardManualPage3.png"),
        ["Manual4"] = new TextureSpec("ClipboardManualPage4.png"),
        ["CruiserManual1"] = new TextureSpec("CruiserManualPage1.bc3", 900, 1260, precompressedBc3: true),
        ["CruiserManual2"] = new TextureSpec("CruiserManualPage2.bc3", 900, 1260, precompressedBc3: true),
        ["CruiserManual3"] = new TextureSpec("CruiserManualPage3.bc3", 900, 1260, precompressedBc3: true),
        ["CruiserManual4"] = new TextureSpec("CruiserManualPage4.bc3", 900, 1260, precompressedBc3: true)
    };

    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, RendererState> RendererStates = new();
    private static string _textureDirectory = string.Empty;
    private static bool _initialized;

    public static void Initialize(string pluginDir)
    {
        _textureDirectory = Path.Combine(pluginDir, "textures");
        _initialized = true;
        FailedTextures.Clear();
    }

    public static void Apply(GrabbableObject? grabbableObject)
    {
        if (!_initialized || Plugin.IsRuntimeShuttingDown || grabbableObject is not ClipboardItem clipboardItem)
        {
            return;
        }

        var animator = clipboardItem.clipboardAnimator;
        if (animator == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            ApplyToRenderer(renderer);
        }
    }

    public static void ApplyVehicle(VehicleController? vehicle)
    {
        if (!_initialized || Plugin.IsRuntimeShuttingDown || vehicle == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        foreach (var renderer in vehicle.GetComponentsInChildren<Renderer>(true))
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
        foreach (var texture in Textures.Values)
        {
            DestroyUnityObject(texture);
        }

        Textures.Clear();
        FailedTextures.Clear();
        _textureDirectory = string.Empty;
        _initialized = false;
    }

    public static void OnSceneUnloaded()
    {
        PruneDestroyedRendererStates();
    }

    private static void ApplyToRenderer(Renderer? renderer)
    {
        if (renderer == null)
        {
            return;
        }

        var rendererId = renderer.GetInstanceID();
        if (RendererStates.ContainsKey(rendererId))
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
            if (originalMaterial == null || !TryResolveTexture(originalMaterial.name, out var texture))
            {
                continue;
            }

            var localizedMaterial = new Material(originalMaterial)
            {
                name = $"{NormalizeMaterialName(originalMaterial.name)} (zh-CN)"
            };
            SetTexture(localizedMaterial, texture);
            localizedMaterials[index] = localizedMaterial;
            createdMaterials.Add(localizedMaterial);
        }

        if (createdMaterials.Count == 0)
        {
            return;
        }

        renderer.sharedMaterials = localizedMaterials;
        RendererStates[rendererId] = new RendererState(renderer, originalMaterials, createdMaterials);
    }

    private static void PruneDestroyedRendererStates()
    {
        List<int>? destroyedRendererIds = null;
        foreach (var pair in RendererStates)
        {
            if (pair.Value.Renderer != null)
            {
                continue;
            }

            destroyedRendererIds ??= new List<int>();
            destroyedRendererIds.Add(pair.Key);
            foreach (var material in pair.Value.LocalizedMaterials)
            {
                DestroyUnityObject(material);
            }
        }

        if (destroyedRendererIds == null)
        {
            return;
        }

        foreach (var rendererId in destroyedRendererIds)
        {
            RendererStates.Remove(rendererId);
        }
    }

    private static bool TryResolveTexture(string? materialName, out Texture2D texture)
    {
        var normalized = NormalizeMaterialName(materialName);
        if (Textures.TryGetValue(normalized, out texture!))
        {
            return true;
        }

        if (FailedTextures.Contains(normalized) ||
            !TextureFiles.TryGetValue(normalized, out var spec))
        {
            texture = null!;
            return false;
        }

        var loaded = LoadTexture(normalized, spec);
        if (loaded == null)
        {
            FailedTextures.Add(normalized);
            texture = null!;
            return false;
        }

        Textures[normalized] = loaded;
        texture = loaded;
        return true;
    }

    private static string NormalizeMaterialName(string? materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return string.Empty;
        }

        var normalized = materialName.Trim();
        const string instanceSuffix = " (Instance)";
        if (normalized.EndsWith(instanceSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - instanceSuffix.Length);
        }

        const string localizedSuffix = " (zh-CN)";
        if (normalized.EndsWith(localizedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - localizedSuffix.Length);
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

    private static Texture2D? LoadTexture(string materialName, TextureSpec spec)
    {
        var fileName = spec.FileName;
        var filePath = Path.Combine(_textureDirectory, fileName);
        Texture2D? texture = null;
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                Plugin.Log.LogWarning($"Clipboard manual texture is missing: {fileName}");
                return null;
            }

            if (fileInfo.Length <= 0 || (!spec.PrecompressedBc3 && fileInfo.Length > MaxTextureFileBytes))
            {
                Plugin.Log.LogWarning($"Clipboard manual texture has an invalid file size: {fileName} ({fileInfo.Length} bytes)");
                return null;
            }

            if (spec.PrecompressedBc3)
            {
                texture = PrecompressedTextureLoader.LoadBc3(
                    filePath,
                    spec.Width,
                    spec.Height,
                    mipChain: false,
                    out var error);
                if (texture == null)
                {
                    Plugin.Log.LogWarning($"Clipboard manual texture could not be loaded: {fileName} ({error})");
                    return null;
                }
            }
            else
            {
                var data = File.ReadAllBytes(filePath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, data, true))
                {
                    Plugin.Log.LogWarning($"Clipboard manual texture could not be decoded: {fileName}");
                    DestroyUnityObject(texture);
                    return null;
                }

                if (texture.width <= 0 || texture.height <= 0 ||
                    texture.width > MaxTextureDimension || texture.height > MaxTextureDimension)
                {
                    Plugin.Log.LogWarning($"Clipboard manual texture dimensions are invalid: {fileName} ({texture.width}x{texture.height})");
                    DestroyUnityObject(texture);
                    return null;
                }
            }

            texture.name = $"{materialName} (zh-CN)";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
        catch (Exception ex)
        {
            DestroyUnityObject(texture);
            Plugin.Log.LogWarning($"Clipboard manual texture load failed for {fileName}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
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
