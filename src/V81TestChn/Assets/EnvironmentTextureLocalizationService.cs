using System;
using System.Collections.Generic;
using System.IO;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
            bool precompressedBc1 = false,
            bool precompressedBc3 = false,
            bool precompressedBc7 = false,
            bool mipChain = true)
        {
            OriginalName = originalName;
            FileName = fileName;
            Width = width;
            Height = height;
            PrecompressedBc1 = precompressedBc1;
            PrecompressedBc3 = precompressedBc3;
            PrecompressedBc7 = precompressedBc7;
            MipChain = mipChain;
        }

        public string OriginalName { get; }
        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
        public bool PrecompressedBc1 { get; }
        public bool PrecompressedBc3 { get; }
        public bool PrecompressedBc7 { get; }
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

    private sealed class FireExitState
    {
        public FireExitState(Renderer renderer, Material[] originalMaterials, GameObject overlay)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials;
            Overlay = overlay;
        }

        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }
        public GameObject Overlay { get; }
    }

    private const int MaxTextureFileBytes = 16 * 1024 * 1024;
    private const string FireExitLabelFileName = "FireExitDoorLabelLocalized.png";
    private static readonly string[] TexturePropertyNames = { "_BaseColorMap", "_MainTex" };
    private static readonly List<Renderer> RendererScanBuffer = new();
    private static readonly HashSet<int> ActiveLocalizedMaterialIds = new();
    private static readonly HashSet<int> UnchangedMaterialIdsThisPass = new();
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
        ["SprayCanTex"] = new TextureSpec("SprayCanTex", "SprayCanTexLocalized.png", 1024, 1024),
        ["StopSignTex"] = new TextureSpec("StopSignTex", "StopSignTexLocalized.png", 1024, 1024),
        ["YieldSignTex"] = new TextureSpec("YieldSignTex", "YieldSignTexLocalized.png", 512, 512),
        ["SodaCanTex1"] = new TextureSpec("SodaCanTex1", "SodaCanTex1Localized.png", 1024, 1024),
        ["ToothpasteTex"] = new TextureSpec("ToothpasteTex", "ToothpasteTexLocalized.png", 1024, 1024),
        ["WeedKillerBottleTex"] = new TextureSpec("WeedKillerBottleTex", "WeedKillerBottleTexLocalized.png", 1024, 1024),
        ["WhoopieCushionTex"] = new TextureSpec("WhoopieCushionTex", "WhoopieCushionTexLocalized.png", 1024, 1024),
        ["YellowMineDoorTex"] = new TextureSpec("YellowMineDoorTex", "YellowMineDoorTexLocalized.png", 1024, 1024),
        ["ChemicalBottle1"] = new TextureSpec("ChemicalBottle1", "ChemicalBottle1Localized.png", 1024, 1024),
        ["powerBoxTextures"] = new TextureSpec("powerBoxTextures", "powerBoxTexturesLocalized.png", 1024, 820),
        ["PlayerLevelStickers"] = new TextureSpec(
            "PlayerLevelStickers",
            "PlayerLevelStickersLocalized.png",
            256,
            512),
        ["PlayerLevelStickers 1"] = new TextureSpec(
            "PlayerLevelStickers 1",
            "PlayerVipEmployeeStickerLocalized.png",
            512,
            128),
        ["CompanyCruiserCombined4Diffuse 1"] = new TextureSpec(
            "CompanyCruiserCombined4Diffuse 1",
            "CompanyCruiserDestroyedDiffuseLocalized.dxt1",
            4096,
            4096,
            precompressedBc1: true),
        ["CompanyCruiserCombined4DiffuseDirtyV2"] = new TextureSpec(
            "CompanyCruiserCombined4DiffuseDirtyV2",
            "CompanyCruiserIntactDiffuseLocalized.bc7",
            4096,
            4096,
            precompressedBc7: true)
    };

    private static readonly Dictionary<string, Texture2D> LocalizedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedTextures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, RendererState> RendererStates = new();
    private static readonly Dictionary<int, MaterialState> MaterialStates = new();
    private static readonly Dictionary<int, FireExitState> FireExitStates = new();
    private static Texture2D? _fireExitLabelTexture;
    private static Material? _fireExitLabelMaterial;
    private static Material? _fireExitHiddenMaterial;
    private static Mesh? _fireExitOverlayMesh;
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

    public static void ApplyLevelEnvironment(RoundManager? roundManager)
    {
        if (!CanApply() || roundManager == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneDestroyedFireExitStates();
        PruneUnusedMaterialStates();
        UnchangedMaterialIdsThisPass.Clear();
        try
        {
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
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
                        ApplyToRenderer(renderer, cacheUnchangedMaterials: true);
                        TryApplyFireExitDoor(renderer);
                    }
                }
            }
        }
        finally
        {
            RendererScanBuffer.Clear();
            UnchangedMaterialIdsThisPass.Clear();
        }
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

    public static void ApplyPlayerBadges(PlayerControllerB? playerController)
    {
        if (!CanApply() || playerController == null)
        {
            return;
        }

        PruneDestroyedRendererStates();
        PruneUnusedMaterialStates();

        var levelBadgeRenderer = playerController.playerBadgeMesh != null
            ? playerController.playerBadgeMesh.GetComponent<Renderer>()
            : null;
        ApplyToRenderer(levelBadgeRenderer);
        ApplyToRenderer(playerController.playerBetaBadgeMesh);
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
        foreach (var state in FireExitStates.Values)
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

            DestroyUnityObject(state.Overlay);
        }

        FireExitStates.Clear();
        foreach (var state in MaterialStates.Values)
        {
            DestroyUnityObject(state.Localized);
        }

        MaterialStates.Clear();
        RendererScanBuffer.Clear();
        ActiveLocalizedMaterialIds.Clear();
        UnchangedMaterialIdsThisPass.Clear();
        foreach (var texture in LocalizedTextures.Values)
        {
            DestroyUnityObject(texture);
        }

        LocalizedTextures.Clear();
        FailedTextures.Clear();
        DestroyUnityObject(_fireExitLabelMaterial);
        DestroyUnityObject(_fireExitHiddenMaterial);
        DestroyUnityObject(_fireExitOverlayMesh);
        DestroyUnityObject(_fireExitLabelTexture);
        _fireExitLabelMaterial = null;
        _fireExitHiddenMaterial = null;
        _fireExitOverlayMesh = null;
        _fireExitLabelTexture = null;
        _textureDirectory = string.Empty;
        _initialized = false;
    }

    public static void OnSceneUnloaded()
    {
        PruneDestroyedRendererStates();
        PruneDestroyedFireExitStates();
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
               objectName.StartsWith("SprayPaint", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("StopSign", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("YieldSign", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("RedSodaCan", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Toothpaste", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("WeedKillerItem", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("WhoopieCushion", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("ChemicalJug", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalizedShipDecoration(string? objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName) &&
               objectName.StartsWith("WelcomeMatContainer", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryApplyFireExitDoor(Renderer? renderer)
    {
        if (renderer == null || FireExitStates.ContainsKey(renderer.GetInstanceID()) ||
            !string.Equals(renderer.gameObject.name, "Cube.001", StringComparison.Ordinal) ||
            renderer.transform.parent == null ||
            !renderer.transform.parent.gameObject.name.StartsWith("FireExitDoor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var meshFilter = renderer.GetComponent<MeshFilter>();
        var mesh = meshFilter?.sharedMesh;
        var originalMaterials = renderer.sharedMaterials;
        if (mesh == null || !string.Equals(mesh.name, "Cube.001", StringComparison.Ordinal) ||
            mesh.subMeshCount != 3 || originalMaterials == null || originalMaterials.Length != 3)
        {
            return;
        }

        var hiddenMaterial = EnsureFireExitHiddenMaterial();
        var labelMaterial = EnsureFireExitLabelMaterial();
        var overlayMesh = EnsureFireExitOverlayMesh();
        if (hiddenMaterial == null || labelMaterial == null || overlayMesh == null)
        {
            return;
        }

        GameObject? overlay = null;
        try
        {
            var localizedMaterials = (Material[])originalMaterials.Clone();
            localizedMaterials[2] = hiddenMaterial;
            renderer.sharedMaterials = localizedMaterials;

            overlay = new GameObject("FireExitDoorLabel (zh-CN)")
            {
                layer = renderer.gameObject.layer
            };
            overlay.transform.SetParent(renderer.transform, false);
            var overlayFilter = overlay.AddComponent<MeshFilter>();
            overlayFilter.sharedMesh = overlayMesh;
            var overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = labelMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
            overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            FireExitStates[renderer.GetInstanceID()] = new FireExitState(renderer, originalMaterials, overlay);
        }
        catch (Exception ex)
        {
            renderer.sharedMaterials = originalMaterials;
            DestroyUnityObject(overlay);
            Plugin.Log.LogWarning($"Fire-exit localization failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Texture2D? EnsureFireExitLabelTexture()
    {
        if (_fireExitLabelTexture != null)
        {
            return _fireExitLabelTexture;
        }

        var path = Path.Combine(_textureDirectory, FireExitLabelFileName);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0 || file.Length > 1024 * 1024)
            {
                throw new InvalidDataException("missing or invalid fire-exit label texture");
            }

            var texture = LoadTextureData(File.ReadAllBytes(path), useRuntimeCompression: true) ??
                          LoadTextureData(File.ReadAllBytes(path), useRuntimeCompression: false);
            if (texture == null || texture.width != 512 || texture.height != 128)
            {
                DestroyUnityObject(texture);
                throw new InvalidDataException("fire-exit label texture must be 512x128");
            }

            texture.name = "FireExitDoorLabel (zh-CN)";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            _fireExitLabelTexture = texture;
            return texture;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Fire-exit label load failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static Shader? FindTransparentShader()
    {
        return Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
    }

    private static Material? EnsureFireExitLabelMaterial()
    {
        if (_fireExitLabelMaterial != null)
        {
            return _fireExitLabelMaterial;
        }

        var shader = FindTransparentShader();
        var texture = EnsureFireExitLabelTexture();
        if (shader == null || texture == null)
        {
            return null;
        }

        _fireExitLabelMaterial = new Material(shader)
        {
            name = "FireExitDoorLabelMaterial (zh-CN)",
            mainTexture = texture,
            color = Color.white,
            renderQueue = 3000
        };
        return _fireExitLabelMaterial;
    }

    private static Material? EnsureFireExitHiddenMaterial()
    {
        if (_fireExitHiddenMaterial != null)
        {
            return _fireExitHiddenMaterial;
        }

        var shader = FindTransparentShader();
        if (shader == null)
        {
            return null;
        }

        _fireExitHiddenMaterial = new Material(shader)
        {
            name = "FireExitDoorOriginalTextHidden (zh-CN)",
            color = Color.clear,
            renderQueue = 3000
        };
        return _fireExitHiddenMaterial;
    }

    private static Mesh EnsureFireExitOverlayMesh()
    {
        if (_fireExitOverlayMesh != null)
        {
            return _fireExitOverlayMesh;
        }

        var mesh = new Mesh { name = "FireExitDoorLabelQuad (zh-CN)" };
        mesh.vertices = new[]
        {
            new Vector3(1.08f, -34f, -43.6f),
            new Vector3(1.08f, 34f, -43.6f),
            new Vector3(1.08f, 34f, -27.8f),
            new Vector3(1.08f, -34f, -27.8f)
        };
        // Texture2D.LoadImage and the door mesh disagree on both image axes.
        // Flip U and V once on the shared quad so the authored source remains
        // readable and upright on both entrance and exit instances.
        mesh.uv = new[]
        {
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        _fireExitOverlayMesh = mesh;
        return mesh;
    }

    private static void ApplyToRenderer(Renderer? renderer, bool cacheUnchangedMaterials = false)
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
            var localized = GetOrCreateLocalizedMaterial(original, cacheUnchangedMaterials);
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

    private static Material? GetOrCreateLocalizedMaterial(Material? original, bool cacheUnchangedMaterials)
    {
        if (original == null)
        {
            return null;
        }

        var materialId = original.GetInstanceID();
        if (cacheUnchangedMaterials && UnchangedMaterialIdsThisPass.Contains(materialId))
        {
            return original;
        }

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
            if (cacheUnchangedMaterials)
            {
                UnchangedMaterialIdsThisPass.Add(materialId);
            }

            return original;
        }

        UnchangedMaterialIdsThisPass.Remove(materialId);
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

            if (file.Length <= 0 ||
                (!spec.PrecompressedBc1 && !spec.PrecompressedBc3 && !spec.PrecompressedBc7 &&
                 file.Length > MaxTextureFileBytes))
            {
                throw new InvalidDataException($"invalid file size ({file.Length} bytes)");
            }

            if (spec.PrecompressedBc1)
            {
                texture = PrecompressedTextureLoader.LoadBc1(
                    path,
                    spec.Width,
                    spec.Height,
                    spec.MipChain,
                    out var error);
                if (texture == null)
                {
                    throw new InvalidDataException(error ?? "precompressed BC1 load failed");
                }
            }
            else if (spec.PrecompressedBc3)
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
            else if (spec.PrecompressedBc7)
            {
                texture = PrecompressedTextureLoader.LoadBc7(
                    path,
                    spec.Width,
                    spec.Height,
                    spec.MipChain,
                    out var error);
                if (texture == null)
                {
                    throw new InvalidDataException(error ?? "precompressed BC7 load failed");
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

    private static void PruneDestroyedFireExitStates()
    {
        List<int>? destroyedIds = null;
        foreach (var pair in FireExitStates)
        {
            if (pair.Value.Renderer != null && pair.Value.Overlay != null)
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
            FireExitStates.Remove(id);
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
