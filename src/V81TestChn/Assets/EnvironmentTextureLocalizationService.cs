using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace V81TestChn;

internal static class EnvironmentTextureLocalizationService
{
    private sealed class TextureSpec
    {
        public TextureSpec(
            string originalName,
            string fileName,
            int width,
            int height,
            bool precompressedBc3 = false,
            bool mipChain = true)
        {
            OriginalName = originalName;
            FileName = fileName;
            Width = width;
            Height = height;
            PrecompressedBc3 = precompressedBc3;
            MipChain = mipChain;
        }

        public string OriginalName { get; }
        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
        public bool PrecompressedBc3 { get; }
        public bool MipChain { get; }
    }

    private sealed class RendererState
    {
        public RendererState(Renderer renderer, Material[] originalMaterials, List<int> localizedMaterialIds)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            LocalizedMaterialIds = localizedMaterialIds;
        }

        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }
        public List<int> LocalizedMaterialIds { get; }
    }

    private sealed class MaterialState
    {
        public MaterialState(Material original, Material localized)
        {
            Original = new WeakReference<Material>(original);
            Localized = localized;
        }

        public WeakReference<Material> Original { get; }
        public Material Localized { get; }

        public bool HasOriginal(Material material)
        {
            return Original.TryGetTarget(out var original) && original != null && original == material;
        }

        public bool HasLiveOriginal()
        {
            return Original.TryGetTarget(out var original) && original != null;
        }
    }

    private const int MaxTextureFileBytes = 16 * 1024 * 1024;
    private static readonly string[] TexturePropertyNames = { "_BaseColorMap", "_MainTex" };
    private static readonly List<Renderer> RendererScanBuffer = new();
    private static readonly HashSet<int> ActiveLocalizedMaterialIds = new();
    private static readonly Dictionary<string, TextureSpec> TextureSpecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["posters"] = new TextureSpec("posters", "ShipPostersLocalized.png", 1024, 1024),
        ["TipsPoster2"] = new TextureSpec("TipsPoster2", "ShipTipsPosterLocalized.png", 796, 1024),
        ["ToiletPaperTex"] = new TextureSpec("ToiletPaperTex", "ToiletPaperTexLocalized.png", 1024, 1024),
        ["CashRegisterTex"] = new TextureSpec("CashRegisterTex", "CashRegisterTexLocalized.png", 1024, 1024),
        ["FlashbangBottleTexture"] = new TextureSpec(
            "FlashbangBottleTexture",
            "FlashbangBottleTextureLocalized.png",
            1024,
            1024),
        ["WelcomeMatTex"] = new TextureSpec("WelcomeMatTex", "WelcomeMatTexLocalized.png", 1024, 1024),
        ["AirhornTex"] = new TextureSpec("AirhornTex", "AirhornTexLocalized.png", 512, 512),
        ["TetraChemicalTex"] = new TextureSpec("TetraChemicalTex", "TetraChemicalTexLocalized.png", 2048, 2048),
        ["StopSignTex"] = new TextureSpec("StopSignTex", "StopSignTexLocalized.png", 1024, 1024),
        ["YieldSignTex"] = new TextureSpec("YieldSignTex", "YieldSignTexLocalized.png", 512, 512),
        ["SodaCanTex1"] = new TextureSpec("SodaCanTex1", "SodaCanTex1Localized.png", 1024, 1024),
        ["ToothpasteTex"] = new TextureSpec("ToothpasteTex", "ToothpasteTexLocalized.png", 1024, 1024),
        ["WeedKillerBottleTex"] = new TextureSpec("WeedKillerBottleTex", "WeedKillerBottleTexLocalized.png", 1024, 1024),
        ["ChemicalBottle1"] = new TextureSpec("ChemicalBottle1", "ChemicalBottle1Localized.png", 1024, 1024),
        ["CompanyCruiserCombined4Diffuse 1"] = new TextureSpec(
            "CompanyCruiserCombined4Diffuse 1",
            "CompanyCruiserDiffuseLocalized.bc3",
            4096,
            4096,
            precompressedBc3: true),
        ["CompanyCruiserCombined4DiffuseDirtyV2"] = new TextureSpec(
            "CompanyCruiserCombined4DiffuseDirtyV2",
            "CompanyCruiserDiffuseDirtyLocalized.bc3",
            4096,
            4096,
            precompressedBc3: true)
    };

    private static readonly Dictionary<string, Texture2D> LocalizedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, RendererState> RendererStates = new();
    private static readonly Dictionary<int, MaterialState> MaterialStates = new();
    private static string _textureDirectory = string.Empty;
    private static bool _initialized;

    public static void Initialize(string pluginDir)
    {
        _textureDirectory = Path.Combine(pluginDir, "textures");
        _initialized = true;
    }

    public static void ApplyShipEnvironment(StartOfRound? startOfRound)
    {
        if (!CanApply() || startOfRound == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();
        var scene = startOfRound.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null)
            {
                continue;
            }

            RendererScanBuffer.Clear();
            root.GetComponentsInChildren(true, RendererScanBuffer);
            foreach (var renderer in RendererScanBuffer)
            {
                ApplyToRenderer(renderer);
            }
        }

        RendererScanBuffer.Clear();
    }

    public static void ApplyVehicle(VehicleController? vehicle)
    {
        if (!CanApply() || vehicle == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();
        RendererScanBuffer.Clear();
        vehicle.GetComponentsInChildren(false, RendererScanBuffer);
        foreach (var renderer in RendererScanBuffer)
        {
            ApplyToRenderer(renderer);
        }

        RendererScanBuffer.Clear();
    }

    public static void ApplyGrabbableObject(GrabbableObject? grabbableObject)
    {
        if (!CanApply() || grabbableObject == null || !IsLocalizedScrap(grabbableObject.gameObject.name))
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();
        RendererScanBuffer.Clear();
        grabbableObject.GetComponentsInChildren(true, RendererScanBuffer);
        foreach (var renderer in RendererScanBuffer)
        {
            ApplyToRenderer(renderer);
        }

        RendererScanBuffer.Clear();
    }

    public static void ApplyShipDecoration(AutoParentToShip? shipDecoration)
    {
        if (!CanApply() || shipDecoration == null ||
            !IsLocalizedShipDecoration(shipDecoration.gameObject.name))
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();
        RendererScanBuffer.Clear();
        shipDecoration.GetComponentsInChildren(true, RendererScanBuffer);
        foreach (var renderer in RendererScanBuffer)
        {
            ApplyToRenderer(renderer);
        }

        RendererScanBuffer.Clear();
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
        }

        RendererStates.Clear();
        foreach (var state in MaterialStates.Values)
        {
            DestroyUnityObject(state.Localized);
        }

        MaterialStates.Clear();
        RendererScanBuffer.Clear();
        ActiveLocalizedMaterialIds.Clear();
        foreach (var texture in LocalizedTextures.Values)
        {
            DestroyUnityObject(texture);
        }

        LocalizedTextures.Clear();
        FailedTextures.Clear();
        _textureDirectory = string.Empty;
        _initialized = false;
    }

    public static void OnSceneUnloaded()
    {
        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();
    }

    private static bool CanApply()
    {
        return _initialized && !Plugin.IsRuntimeShuttingDown;
    }

    private static bool IsLocalizedScrap(string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        return objectName.StartsWith("ToiletPaperRolls", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("CashRegisterItem", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("DiyFlashbang", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Airhorn", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("TZPChemical", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("StopSign", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("YieldSign", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("RedSodaCan", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Toothpaste", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("WeedKillerItem", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("ChemicalJug", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalizedShipDecoration(string? objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName) &&
               objectName.StartsWith("WelcomeMatContainer", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyToRenderer(Renderer? renderer)
    {
        if (renderer == null || RendererStates.ContainsKey(renderer.GetInstanceID()))
        {
            return;
        }

        var originalMaterials = renderer.sharedMaterials;
        if (originalMaterials == null || originalMaterials.Length == 0)
        {
            return;
        }

        var localizedMaterials = (Material[])originalMaterials.Clone();
        List<int>? localizedMaterialIds = null;
        var changed = false;
        for (var index = 0; index < originalMaterials.Length; index++)
        {
            var original = originalMaterials[index];
            var localized = GetOrCreateLocalizedMaterial(original);
            if (localized == null || ReferenceEquals(localized, original))
            {
                continue;
            }

            localizedMaterials[index] = localized;
            localizedMaterialIds ??= new List<int>();
            var materialId = original.GetInstanceID();
            if (!localizedMaterialIds.Contains(materialId))
            {
                localizedMaterialIds.Add(materialId);
            }
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        renderer.sharedMaterials = localizedMaterials;
        RendererStates[renderer.GetInstanceID()] = new RendererState(
            renderer,
            originalMaterials,
            localizedMaterialIds ?? new List<int>());
    }

    private static Material? GetOrCreateLocalizedMaterial(Material? original)
    {
        if (original == null)
        {
            return null;
        }

        var materialId = original.GetInstanceID();
        if (MaterialStates.TryGetValue(materialId, out var existing))
        {
            if (existing.HasOriginal(original))
            {
                return existing.Localized;
            }

            DestroyUnityObject(existing.Localized);
            MaterialStates.Remove(materialId);
        }

        Material? localized = null;
        var changed = false;
        foreach (var propertyName in TexturePropertyNames)
        {
            if (!original.HasProperty(propertyName) || !TryResolveSpec(original.GetTexture(propertyName), out var spec))
            {
                continue;
            }

            var texture = EnsureTextureLoaded(spec, original.GetTexture(propertyName));
            if (texture == null)
            {
                continue;
            }

            localized ??= new Material(original);
            localized.SetTexture(propertyName, texture);
            changed = true;
        }

        if (!changed &&
            original.HasProperty("_MainTex") &&
            TryResolveSpec(original.mainTexture, out var mainSpec))
        {
            var texture = EnsureTextureLoaded(mainSpec, original.mainTexture);
            if (texture != null)
            {
                localized = new Material(original)
                {
                    mainTexture = texture
                };
                changed = true;
            }
        }

        if (!changed || localized == null)
        {
            DestroyUnityObject(localized);
            return original;
        }

        localized.name = $"{NormalizeName(original.name)} (zh-CN)";
        if (MaterialStates.TryGetValue(materialId, out var stale))
        {
            DestroyUnityObject(stale.Localized);
        }

        MaterialStates[materialId] = new MaterialState(original, localized);
        return localized;
    }

    private static Texture2D? EnsureTextureLoaded(TextureSpec spec, Texture? originalTexture)
    {
        if (LocalizedTextures.TryGetValue(spec.OriginalName, out var existing))
        {
            return existing;
        }

        if (FailedTextures.Contains(spec.OriginalName))
        {
            return null;
        }

        var path = Path.Combine(_textureDirectory, spec.FileName);
        Texture2D? texture = null;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                throw new FileNotFoundException("localized texture file is missing", path);
            }

            if (file.Length <= 0 || (!spec.PrecompressedBc3 && file.Length > MaxTextureFileBytes))
            {
                throw new InvalidDataException($"invalid file size ({file.Length} bytes)");
            }

            if (spec.PrecompressedBc3)
            {
                texture = PrecompressedTextureLoader.LoadBc3(
                    path,
                    spec.Width,
                    spec.Height,
                    spec.MipChain,
                    out var error);
                if (texture == null)
                {
                    throw new InvalidDataException(error ?? "precompressed BC3 load failed");
                }
            }
            else
            {
                var data = File.ReadAllBytes(path);
                texture = LoadTextureData(data, useRuntimeCompression: true) ?? LoadTextureData(data, useRuntimeCompression: false);
            }
            if (texture == null || texture.width != spec.Width || texture.height != spec.Height)
            {
                var size = texture == null ? "decode failed" : $"{texture.width}x{texture.height}";
                throw new InvalidDataException($"expected {spec.Width}x{spec.Height}, got {size}");
            }

            texture.name = $"{spec.OriginalName} (zh-CN)";
            if (originalTexture != null)
            {
                texture.wrapMode = originalTexture.wrapMode;
                texture.filterMode = originalTexture.filterMode;
                texture.anisoLevel = originalTexture.anisoLevel;
                texture.mipMapBias = originalTexture.mipMapBias;
            }

            LocalizedTextures[spec.OriginalName] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            DestroyUnityObject(texture);
            FailedTextures.Add(spec.OriginalName);
            Plugin.Log.LogWarning(
                $"Environment texture localization failed for {spec.FileName}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static Texture2D? LoadTextureData(byte[] data, bool useRuntimeCompression)
    {
        Texture2D? texture = null;
        try
        {
            var format = useRuntimeCompression && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)
                ? TextureFormat.DXT5
                : TextureFormat.RGBA32;
            var initialSize = format == TextureFormat.DXT5 ? 4 : 2;
            texture = new Texture2D(initialSize, initialSize, format, true);
            if (ImageConversion.LoadImage(texture, data, true))
            {
                return texture;
            }

            DestroyUnityObject(texture);
            return null;
        }
        catch
        {
            DestroyUnityObject(texture);
            return null;
        }
    }

    private static bool TryResolveSpec(Texture? texture, out TextureSpec spec)
    {
        return TextureSpecs.TryGetValue(NormalizeName(texture?.name), out spec!);
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.Trim();
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

    private static void PruneUnusedMaterialStates()
    {
        ActiveLocalizedMaterialIds.Clear();
        foreach (var rendererState in RendererStates.Values)
        {
            if (rendererState.Renderer == null)
            {
                continue;
            }

            foreach (var materialId in rendererState.LocalizedMaterialIds)
            {
                ActiveLocalizedMaterialIds.Add(materialId);
            }
        }

        List<int>? staleIds = null;
        foreach (var pair in MaterialStates)
        {
            if (pair.Value.Localized != null &&
                pair.Value.HasLiveOriginal() &&
                ActiveLocalizedMaterialIds.Contains(pair.Key))
            {
                continue;
            }

            staleIds ??= new List<int>();
            staleIds.Add(pair.Key);
            DestroyUnityObject(pair.Value.Localized);
        }

        if (staleIds != null)
        {
            foreach (var id in staleIds)
            {
                MaterialStates.Remove(id);
            }
        }

        ActiveLocalizedMaterialIds.Clear();
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
