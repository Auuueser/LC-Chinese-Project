using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class RadiationWarningAuditService
{
    internal const string WarningRootPathSuffix = "IngamePlayerHUD/SpecialHUDGraphics/RadiationIncrease";

    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<int>? _sampleCount;
    private static ConfigEntry<float>? _sampleIntervalSeconds;
    private static HUDManager? _activeHudManager;
    private static Coroutine? _activeAuditCoroutine;

    public static void Initialize(ConfigFile config)
    {
        _enabled = config.Bind(
            "RadiationWarningAudit",
            "Enabled",
            false,
            "Enable bounded audit sampling for the original radiation warning subtree. Keep disabled outside diagnostics to avoid runtime subtree enumeration.");
        _sampleCount = config.Bind(
            "RadiationWarningAudit",
            "SampleCount",
            5,
            "Number of bounded subtree samples to capture after the radiation warning trigger.");
        _sampleIntervalSeconds = config.Bind(
            "RadiationWarningAudit",
            "SampleIntervalSeconds",
            0.2f,
            "Seconds between bounded subtree audit samples after the radiation warning trigger.");
    }

    public static void OnRadiationWarningTriggered(HUDManager hudManager, string stage)
    {
        if (hudManager == null || _enabled?.Value != true)
        {
            return;
        }

        var root = FindWarningRoot(hudManager);
        if (root == null)
        {
            Plugin.Log.LogWarning($"RadiationAudit[{stage}] action=root-not-found suffix={WarningRootPathSuffix}");
            return;
        }

        if (_activeAuditCoroutine != null && _activeHudManager != null)
        {
            _activeHudManager.StopCoroutine(_activeAuditCoroutine);
            _activeAuditCoroutine = null;
        }

        _activeHudManager = hudManager;
        _activeAuditCoroutine = hudManager.StartCoroutine(SampleWarningSubtree(hudManager, stage, _sampleCount?.Value ?? 0, _sampleIntervalSeconds?.Value ?? 0f));
    }

    private static IEnumerator SampleWarningSubtree(HUDManager hudManager, string stage, int sampleCount, float sampleIntervalSeconds)
    {
        var boundedSampleCount = Mathf.Max(1, sampleCount);
        var boundedIntervalSeconds = Mathf.Max(0f, sampleIntervalSeconds);

        for (var sampleIndex = 0; sampleIndex < boundedSampleCount; sampleIndex++)
        {
            if (hudManager == null)
            {
                break;
            }

            var root = FindWarningRoot(hudManager);
            if (root == null)
            {
                Plugin.Log.LogInfo($"RadiationAudit[{stage}] sample={sampleIndex} action=sampling-stopped-root-lost suffix={WarningRootPathSuffix}");
                break;
            }

            AuditImages(root, stage, sampleIndex);
            AuditRawImages(root, stage, sampleIndex);
            AuditSpriteRenderers(root, stage, sampleIndex);
            AuditGraphicMaterials(root, stage, sampleIndex);
            AuditRendererMaterials(root, stage, sampleIndex);

            if (sampleIndex + 1 < boundedSampleCount)
            {
                yield return boundedIntervalSeconds > 0f
                    ? new WaitForSeconds(boundedIntervalSeconds)
                    : null;
            }
        }

        _activeAuditCoroutine = null;
        _activeHudManager = null;
    }

    private static void AuditGraphicMaterials(Transform root, string stage, int sampleIndex)
    {
        var seenMaterials = new HashSet<int>();
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            Material? material;
            try
            {
                material = graphic.material;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"RadiationAudit[{stage}] sample={sampleIndex} component=GraphicMaterial path={BuildPath(graphic.transform)} active={graphic.gameObject.activeInHierarchy} enabled={graphic.enabled} alpha={graphic.color.a:0.###} material=<error> texture=<error> error={ex.GetType().Name}:{ex.Message}");
                continue;
            }

            var scope = "none";
            if (material != null)
            {
                scope = seenMaterials.Add(material.GetInstanceID()) ? "unique" : "shared";
            }

            // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
            // Plugin.Log.LogInfo(
            //     $"RadiationAudit[{stage}] sample={sampleIndex} component=GraphicMaterial path={BuildPath(graphic.transform)} active={graphic.gameObject.activeInHierarchy} enabled={graphic.enabled} alpha={graphic.color.a:0.###} material={DescribeName(material)} texture={DescribeName(material?.mainTexture)} scope={scope}");
        }
    }

    private static void AuditRendererMaterials(Transform root, string stage, int sampleIndex)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
                // Plugin.Log.LogInfo(
                //     $"RadiationAudit[{stage}] sample={sampleIndex} component=RendererMaterial path={BuildPath(renderer.transform)} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} materialIndex=-1 material=<none> texture=<none>");
                continue;
            }

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
                // Plugin.Log.LogInfo(
                //     $"RadiationAudit[{stage}] sample={sampleIndex} component=RendererMaterial path={BuildPath(renderer.transform)} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} materialIndex={materialIndex} material={DescribeName(material)} texture={DescribeName(material?.mainTexture)}");
            }
        }
    }

    private static void AuditImages(Transform root, string stage, int sampleIndex)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            var sprite = image.overrideSprite ?? image.sprite;
            var texture = sprite?.texture;
            // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
            // Plugin.Log.LogInfo(
            //     $"RadiationAudit[{stage}] sample={sampleIndex} component=Image path={BuildPath(image.transform)} active={image.gameObject.activeInHierarchy} enabled={image.enabled} alpha={image.color.a:0.###} sprite={DescribeName(sprite)} texture={DescribeName(texture)}");
        }
    }

    private static void AuditRawImages(Transform root, string stage, int sampleIndex)
    {
        foreach (var rawImage in root.GetComponentsInChildren<RawImage>(true))
        {
            // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
            // Plugin.Log.LogInfo(
            //     $"RadiationAudit[{stage}] sample={sampleIndex} component=RawImage path={BuildPath(rawImage.transform)} active={rawImage.gameObject.activeInHierarchy} enabled={rawImage.enabled} alpha={rawImage.color.a:0.###} sprite=<none> texture={DescribeName(rawImage.texture)}");
        }
    }

    private static void AuditSpriteRenderers(Transform root, string stage, int sampleIndex)
    {
        foreach (var spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var sprite = spriteRenderer.sprite;
            // Noisy per-sample audit log; keep code for future diagnostics without flooding LogOutput.log.
            // Plugin.Log.LogInfo(
            //     $"RadiationAudit[{stage}] sample={sampleIndex} component=SpriteRenderer path={BuildPath(spriteRenderer.transform)} active={spriteRenderer.gameObject.activeInHierarchy} enabled={spriteRenderer.enabled} alpha={spriteRenderer.color.a:0.###} sprite={DescribeName(sprite)} texture={DescribeName(sprite?.texture)}");
        }
    }

    private static Transform? FindWarningRoot(HUDManager hudManager)
    {
        var directRoot = hudManager.radiationGraphicAnimator?.transform;
        if (HasExpectedRootPath(directRoot))
        {
            return directRoot;
        }

        foreach (var transform in hudManager.GetComponentsInChildren<Transform>(true))
        {
            if (HasExpectedRootPath(transform))
            {
                return transform;
            }
        }

        return null;
    }

    private static bool HasExpectedRootPath(Transform? transform)
    {
        if (transform == null)
        {
            return false;
        }

        return BuildPath(transform).EndsWith(WarningRootPathSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeName(UnityEngine.Object? obj)
    {
        return string.IsNullOrWhiteSpace(obj?.name) ? "<null>" : obj.name;
    }

    private static string BuildPath(Transform? transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }
}
