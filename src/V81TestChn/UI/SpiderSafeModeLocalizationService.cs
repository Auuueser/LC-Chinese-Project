using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class SpiderSafeModeLocalizationService
{
    private sealed class SpiderState
    {
        public SpiderState(SandSpiderAI spider, MeshRenderer originalRenderer, MeshRenderer localizedRenderer, GameObject localizedObject)
        {
            Spider = spider;
            OriginalRenderer = originalRenderer;
            LocalizedRenderer = localizedRenderer;
            LocalizedObject = localizedObject;
        }

        public SandSpiderAI Spider { get; }
        public MeshRenderer OriginalRenderer { get; }
        public MeshRenderer LocalizedRenderer { get; }
        public GameObject LocalizedObject { get; }
    }

    private const string LocalizedLabel = "蜘蛛";
    // Runtime-tuned against the baked SPIDER label; the user requested a further
    // 2x increase after reviewing the 3.6-size in-game result.
    private const float LocalizedFontSize = 7.2f;
    private static readonly Dictionary<int, SpiderState> States = new();

    public static void Apply(SandSpiderAI? spider)
    {
        if (Plugin.IsRuntimeShuttingDown || spider == null || spider.spiderSafeModeMesh == null)
        {
            return;
        }

        PruneDestroyedStates();
        var spiderId = spider.GetInstanceID();
        if (States.ContainsKey(spiderId))
        {
            return;
        }

        var originalRenderer = spider.spiderSafeModeMesh;
        var localizedObject = new GameObject("SpiderText (zh-CN)");
        localizedObject.transform.SetParent(originalRenderer.transform, false);
        localizedObject.transform.localPosition = new Vector3(-0.034f, 0.227f, 0.04f);
        // TMP's front face is opposite the baked SPIDER mesh. Turn it around in the
        // original label's local space so players see normal, rather than mirrored, text.
        localizedObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        localizedObject.transform.localScale = Vector3.one;

        var text = localizedObject.AddComponent<TextMeshPro>();
        text.text = LocalizedLabel;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = LocalizedFontSize;
        text.color = new Color(0.906f, 0.085f, 0.096f, 1f);
        text.outlineColor = Color.black;
        text.outlineWidth = 0.18f;
        text.rectTransform.sizeDelta = new Vector2(2.1f, 5.4f);
        // A TMP fallback glyph is rendered by a separate TMP_SubMesh child whose
        // MeshRenderer is not the one SandSpiderAI toggles. Use the managed Chinese
        // font as this label's primary font so all glyphs remain on the root renderer.
        if (!FontFallbackService.TryUseManagedFallbackAsPrimary(text, LocalizedLabel))
        {
            UnityEngine.Object.Destroy(localizedObject);
            Plugin.Log.LogWarning("Spider safe-mode Chinese label could not bind a primary Chinese font.");
            return;
        }

        text.ForceMeshUpdate();

        var localizedRenderer = localizedObject.GetComponent<MeshRenderer>();
        if (localizedRenderer == null)
        {
            UnityEngine.Object.Destroy(localizedObject);
            Plugin.Log.LogWarning("Spider safe-mode Chinese label could not create a MeshRenderer.");
            return;
        }

        // SandSpiderAI only updates the renderer when its cached flag differs from
        // the setting. Both default to false, so the first Update can intentionally
        // skip the toggle even when the prefab renderer starts enabled. Seed the
        // localized renderer from the actual setting instead of inheriting that
        // potentially stale prefab state; later setting changes still flow through
        // the original SandSpiderAI.Update logic via spiderSafeModeMesh.
        localizedRenderer.enabled = IsSpiderSafeModeEnabled();
        originalRenderer.enabled = false;
        spider.spiderSafeModeMesh = localizedRenderer;
        States[spiderId] = new SpiderState(spider, originalRenderer, localizedRenderer, localizedObject);
    }

    public static void Shutdown()
    {
        foreach (var state in States.Values)
        {
            try
            {
                if (state.Spider != null && state.Spider.spiderSafeModeMesh == state.LocalizedRenderer)
                {
                    state.OriginalRenderer.enabled = state.LocalizedRenderer != null && state.LocalizedRenderer.enabled;
                    state.Spider.spiderSafeModeMesh = state.OriginalRenderer;
                }
            }
            catch
            {
                // Scene teardown can invalidate Unity objects between comparisons.
            }

            DestroyUnityObject(state.LocalizedObject);
        }

        States.Clear();
    }

    private static void PruneDestroyedStates()
    {
        List<int>? destroyedIds = null;
        foreach (var pair in States)
        {
            if (pair.Value.Spider != null && pair.Value.LocalizedObject != null)
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
            States.Remove(id);
        }
    }

    private static bool IsSpiderSafeModeEnabled()
    {
        return IngamePlayerSettings.Instance != null &&
               IngamePlayerSettings.Instance.unsavedSettings != null &&
               IngamePlayerSettings.Instance.unsavedSettings.spiderSafeMode;
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
