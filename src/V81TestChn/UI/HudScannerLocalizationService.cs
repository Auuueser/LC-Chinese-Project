using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudScannerLocalizationService
{
    private static readonly FieldInfo? HudScanNodesField = AccessTools.Field(typeof(HUDManager), "scanNodes");
    private const float HudScannerLocalizationIntervalSeconds = 0.1f;
    private const float HudScannerRootLocalizationIntervalSeconds = 0.5f;
    private const int HudScannerTextCacheLimit = 2048;
    private const int DefaultHudScannerMaxTextsPerUpdate = 16;
    private static ConfigEntry<int>? _hudScannerMaxTextsPerUpdate;
    private static float _nextHudScannerLocalizationTime;
    private static float _nextHudScannerElementLocalizationTime;
    private static float _nextHudScannerRootLocalizationTime;
    private static float _nextHudScannerSourceNodeLocalizationTime;
    private static int _lastHudScannerRootId;
    private static int _lastHudScannerTranslatedRootId;
    private static string? _lastHudScannerTotalText;
    private static readonly Dictionary<int, TMP_Text[]> HudScannerElementTextCache = new();
    private static readonly Dictionary<int, CachedHudScannerText> HudScannerTextStateCache = new();
    private static readonly Dictionary<int, CachedHudScannerNodeState> HudScannerNodeStateCache = new();

    private sealed class CachedHudScannerText
    {
        public string? LastOriginal { get; set; }
        public string? LastTranslated { get; set; }
    }

    private sealed class CachedHudScannerNodeState
    {
        public ScanNodeProperties? Node { get; set; }
        public string? OriginalHeader { get; set; }
        public string? OriginalSubText { get; set; }
        public string? TranslatedHeader { get; set; }
        public string? TranslatedSubText { get; set; }
        public string? LastSeenHeader { get; set; }
        public string? LastSeenSubText { get; set; }
    }

    public static void Initialize(ConfigFile config)
    {
        _hudScannerMaxTextsPerUpdate = config.Bind(
            ConfigSections.Performance,
            "HudScannerMaxTextsPerUpdate",
            DefaultHudScannerMaxTextsPerUpdate,
            "每次 UpdateScanNodes 最多处理的扫描 HUD 文本数量。数值越低越稳，翻译补齐可能越慢。");
    }

    public static void Clear()
    {
        RestoreHudScannerSourceNodes();
        _nextHudScannerLocalizationTime = 0f;
        _nextHudScannerElementLocalizationTime = 0f;
        _nextHudScannerRootLocalizationTime = 0f;
        _nextHudScannerSourceNodeLocalizationTime = 0f;
        _lastHudScannerRootId = 0;
        _lastHudScannerTranslatedRootId = 0;
        _lastHudScannerTotalText = null;
        ClearRuntimeCaches();
    }

    public static void ClearRuntimeCaches()
    {
        HudScannerElementTextCache.Clear();
        HudScannerTextStateCache.Clear();
        HudScannerNodeStateCache.Clear();
    }

    public static void ApplyHudScannerLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            TranslateHudScannerSourceNodes(hud, reason);
        }

        var root = hud.scanInfoAnimator == null ? hud.totalValueText?.transform.parent?.gameObject : hud.scanInfoAnimator.gameObject;
        if (!ShouldSkipHudScannerLocalization(hud, root, reason))
        {
            ApplyHudScannerTextTranslation(hud.totalValueText, reason);
        }

        ApplyHudScannerElementTextLocalization(hud, reason);

        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal) &&
            ShouldTranslateHudScannerRoot(root, reason))
        {
            TargetedUiTranslator.TranslateRoot(root, reason);
        }
    }

    public static void ApplyHudScannerSourceNodesIfDue(HUDManager? hud, string reason)
    {
        if (ShouldTranslateHudScannerSourceNodes(reason))
        {
            TranslateHudScannerSourceNodes(hud, reason);
        }
    }

    private static int GetHudScannerMaxTextsPerUpdate()
    {
        return Mathf.Clamp(_hudScannerMaxTextsPerUpdate?.Value ?? DefaultHudScannerMaxTextsPerUpdate, 4, 64);
    }

    private static bool ShouldTranslateHudScannerSourceNodes(string reason)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes.scan-node-source", StringComparison.Ordinal))
        {
            return true;
        }

        var now = Time.unscaledTime;
        if (now < _nextHudScannerSourceNodeLocalizationTime)
        {
            return false;
        }

        _nextHudScannerSourceNodeLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return true;
    }

    private static void ApplyHudScannerElementTextLocalization(HUDManager hud, string reason)
    {
        if (ShouldSkipHudScannerElementTextLocalization(reason))
        {
            return;
        }

        var elements = hud.scanElements;
        if (elements == null)
        {
            return;
        }

        var processed = 0;
        foreach (var element in elements)
        {
            if (element == null || !element.gameObject.activeInHierarchy)
            {
                continue;
            }

            foreach (var text in GetHudScannerElementTexts(element))
            {
                if (string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal) &&
                    processed >= GetHudScannerMaxTextsPerUpdate())
                {
                    return;
                }

                if (text != null)
                {
                    processed++;
                }

                ApplyHudScannerTextTranslation(text, reason);
            }
        }
    }

    private static void TranslateHudScannerSourceNodes(HUDManager? hud, string reason)
    {
        if (hud == null || HudScanNodesField == null)
        {
            return;
        }

        Dictionary<RectTransform, ScanNodeProperties>? scanNodes;
        try
        {
            scanNodes = HudScanNodesField.GetValue(hud) as Dictionary<RectTransform, ScanNodeProperties>;
        }
        catch
        {
            return;
        }

        if (scanNodes == null || scanNodes.Count == 0)
        {
            return;
        }

        var processed = 0;
        foreach (var node in scanNodes.Values)
        {
            if (node == null)
            {
                continue;
            }

            if (processed >= GetHudScannerMaxTextsPerUpdate())
            {
                return;
            }

            processed++;
            TranslateHudScannerSourceNode(node, reason);
        }
    }

    private static void TranslateHudScannerSourceNode(ScanNodeProperties node, string reason)
    {
        var id = node.GetInstanceID();
        if (!HudScannerNodeStateCache.TryGetValue(id, out var state))
        {
            if (HudScannerNodeStateCache.Count >= HudScannerTextCacheLimit)
            {
                RestoreHudScannerSourceNodes();
                HudScannerNodeStateCache.Clear();
            }

            state = new CachedHudScannerNodeState { Node = node };
            HudScannerNodeStateCache[id] = state;
        }

        state.Node = node;
        if (TrySkipUnchangedHudScannerSourceNode(node, state))
        {
            return;
        }

        var changed = false;
        if (TryTranslateHudScannerSourceField(
                node.headerText,
                state.OriginalHeader,
                state.TranslatedHeader,
                out var originalHeader,
                out var translatedHeader,
                out var headerValue))
        {
            state.OriginalHeader = originalHeader;
            state.TranslatedHeader = translatedHeader;
            node.headerText = headerValue;
            changed = true;
        }

        if (TryTranslateHudScannerSourceField(
                node.subText,
                state.OriginalSubText,
                state.TranslatedSubText,
                out var originalSubText,
                out var translatedSubText,
                out var subTextValue))
        {
            state.OriginalSubText = originalSubText;
            state.TranslatedSubText = translatedSubText;
            node.subText = subTextValue;
            changed = true;
        }

        UpdateHudScannerSourceNodeSnapshot(node, state);
        if (changed)
        {
            Plugin.ReportTranslationHit();
        }
    }

    private static bool TrySkipUnchangedHudScannerSourceNode(ScanNodeProperties node, CachedHudScannerNodeState state)
    {
        return string.Equals(node.headerText, state.LastSeenHeader, StringComparison.Ordinal) &&
               string.Equals(node.subText, state.LastSeenSubText, StringComparison.Ordinal);
    }

    private static void UpdateHudScannerSourceNodeSnapshot(ScanNodeProperties node, CachedHudScannerNodeState state)
    {
        state.LastSeenHeader = node.headerText;
        state.LastSeenSubText = node.subText;
    }

    private static bool TryTranslateHudScannerSourceField(
        string? current,
        string? cachedOriginal,
        string? cachedTranslated,
        out string? original,
        out string? translated,
        out string value)
    {
        original = cachedOriginal;
        translated = cachedTranslated;
        value = current ?? string.Empty;
        if (string.IsNullOrEmpty(current))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(cachedTranslated))
        {
            if (string.Equals(current, cachedTranslated, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(current, cachedOriginal, StringComparison.Ordinal))
            {
                value = cachedTranslated;
                return true;
            }
        }

        original = current;
        if (!TryTranslateHudScannerSourceText(current, out var newTranslated) ||
            string.Equals(current, newTranslated, StringComparison.Ordinal))
        {
            translated = null;
            return false;
        }

        translated = newTranslated;
        value = newTranslated;
        return true;
    }

    private static bool TryTranslateHudScannerSourceText(string source, out string translated)
    {
        return TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.HudScanner, source, out translated) ||
               TranslationService.TryTranslateFastExact(source, out translated);
    }

    private static void RestoreHudScannerSourceNodes()
    {
        foreach (var state in HudScannerNodeStateCache.Values)
        {
            try
            {
                var node = state.Node;
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(state.OriginalHeader) &&
                    string.Equals(node.headerText, state.TranslatedHeader, StringComparison.Ordinal))
                {
                    node.headerText = state.OriginalHeader;
                }

                if (!string.IsNullOrEmpty(state.OriginalSubText) &&
                    string.Equals(node.subText, state.TranslatedSubText, StringComparison.Ordinal))
                {
                    node.subText = state.OriginalSubText;
                }
            }
            catch
            {
                // Unity objects may already be tearing down; cleanup must stay best-effort.
            }
        }
    }

    private static bool ApplyHudScannerTextTranslation(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var original = text.text;
        if (string.IsNullOrEmpty(original))
        {
            return false;
        }

        var id = text.GetInstanceID();
        if (HudScannerTextStateCache.TryGetValue(id, out var cached))
        {
            if (string.Equals(cached.LastTranslated, original, StringComparison.Ordinal))
            {
                FontFallbackService.ApplyFallback(text, original);
                return false;
            }

            if (string.Equals(cached.LastOriginal, original, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(cached.LastTranslated))
                {
                    FontFallbackService.ApplyFallback(text, original);
                    return false;
                }

                text.text = cached.LastTranslated;
                FontFallbackService.ApplyFallback(text, cached.LastTranslated);
                FontFallbackService.ApplySystemOnlineProbeFix(text, reason, cached.LastTranslated);
                return true;
            }
        }

        if (!TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.HudScanner, original, out var translated) &&
            !TranslationService.TryTranslateFastExact(original, out translated))
        {
            CacheHudScannerText(id, original, null);
            FontFallbackService.ApplyFallback(text, original);
            return false;
        }

        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            CacheHudScannerText(id, original, null);
            FontFallbackService.ApplyFallback(text, original);
            return false;
        }

        CacheHudScannerText(id, original, translated);
        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    private static bool ShouldSkipHudScannerElementTextLocalization(string reason)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return false;
        }

        var now = Time.unscaledTime;
        if (now < _nextHudScannerElementLocalizationTime)
        {
            return true;
        }

        _nextHudScannerElementLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return false;
    }

    private static void CacheHudScannerText(int id, string original, string? translated)
    {
        if (HudScannerTextStateCache.Count >= HudScannerTextCacheLimit)
        {
            HudScannerTextStateCache.Clear();
        }

        HudScannerTextStateCache[id] = new CachedHudScannerText
        {
            LastOriginal = original,
            LastTranslated = translated
        };
    }

    private static TMP_Text[] GetHudScannerElementTexts(RectTransform element)
    {
        var id = element.GetInstanceID();
        if (HudScannerElementTextCache.TryGetValue(id, out var cached) &&
            HasAnyLiveText(cached))
        {
            return cached;
        }

        try
        {
            var texts = element.GetComponentsInChildren<TMP_Text>(true) ?? Array.Empty<TMP_Text>();
            HudScannerElementTextCache[id] = texts;
            return texts;
        }
        catch
        {
            HudScannerElementTextCache.Remove(id);
            return Array.Empty<TMP_Text>();
        }
    }

    private static bool HasAnyLiveText(TMP_Text[] texts)
    {
        for (var i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSkipHudScannerLocalization(HUDManager hud, GameObject? root, string reason)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return false;
        }

        var totalText = hud.totalValueText?.text ?? string.Empty;
        var rootId = root == null ? 0 : root.GetInstanceID();
        var changed = rootId != _lastHudScannerRootId ||
                      !string.Equals(totalText, _lastHudScannerTotalText, StringComparison.Ordinal);
        var now = Time.unscaledTime;
        if (!changed && now < _nextHudScannerLocalizationTime)
        {
            return true;
        }

        _lastHudScannerRootId = rootId;
        _lastHudScannerTotalText = totalText;
        _nextHudScannerLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return false;
    }

    private static bool ShouldTranslateHudScannerRoot(GameObject? root, string reason)
    {
        if (root == null)
        {
            return false;
        }

        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return true;
        }

        var rootId = root.GetInstanceID();
        var changed = rootId != _lastHudScannerTranslatedRootId;
        var now = Time.unscaledTime;
        if (!changed && now < _nextHudScannerRootLocalizationTime)
        {
            return false;
        }

        _lastHudScannerTranslatedRootId = rootId;
        _nextHudScannerRootLocalizationTime = now + HudScannerRootLocalizationIntervalSeconds;
        return true;
    }

}
