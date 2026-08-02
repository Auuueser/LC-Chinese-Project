using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudScannerLocalizationService
{
    private const float HudScannerLocalizationIntervalSeconds = 0.5f;
    private const float HudScannerIdleActiveProbeIntervalSeconds = 0.25f;
    private const float HudScannerActiveProbeIntervalSeconds = 0.1f;
    private const float HudScannerRootLocalizationIntervalSeconds = 2.0f;
    private const float HudScannerBindingSignatureProbeIntervalSeconds = 1.0f;
    private const int HudScannerTextCacheLimit = 16384;
    private const int DefaultHudScannerMaxTextsPerUpdate = 4;
    private const int HudScannerActiveProbeFallbackElementBudget = 4;
    private static ConfigEntry<int>? _hudScannerMaxTextsPerUpdate;
    private static int _hudScannerMaxTextsPerUpdateFast = DefaultHudScannerMaxTextsPerUpdate;
    private static float _nextHudScannerLocalizationTime;
    private static float _nextHudScannerElementLocalizationTime;
    private static float _nextHudScannerRootLocalizationTime;
    private static float _nextHudScannerBoundNodeLocalizationTime;
    private static float _nextHudScannerActiveProbeTime;
    private static float _nextHudScannerBindingSignatureProbeTime;
    private static int _cachedHudScannerBindingCount = -1;
    private static int _cachedHudScannerBindingSignature;
    private static bool _lastHudScannerActive;
    private static bool _lastImmediateHudScannerActive;
    private static int _lastHudScannerRootId;
    private static int _lastHudScannerTranslatedRootId;
    private static int _lastImmediateHudScannerRootId;
    private static int _lastImmediateHudScannerNodeCount;
    private static int _lastImmediateHudScannerElementCount;
    private static int _lastImmediateHudScannerBindingSignature;
    private static int _hudScannerElementCursor;
    private static int _hudScannerBoundNodeCursor;
    private static int _hudScannerActiveProbeCursor;
    private static Dictionary<RectTransform, ScanNodeProperties>? _hudScannerBoundNodeSnapshotSource;
    private static readonly List<KeyValuePair<RectTransform, ScanNodeProperties>> HudScannerBoundNodeSnapshot = new();
    private static string? _lastHudScannerTotalText;
    private static string? _lastImmediateHudScannerTotalText;
    private static readonly BoundedCache<int, TMP_Text[]> HudScannerElementTextCache = new(HudScannerTextCacheLimit);
    private static readonly BoundedCache<int, CachedHudScannerText> HudScannerTextStateCache = new(HudScannerTextCacheLimit);

    private sealed class CachedHudScannerText
    {
        public string? LastOriginal { get; set; }
        public string? LastTranslated { get; set; }
    }

    public static void Initialize(ConfigFile config)
    {
        _hudScannerMaxTextsPerUpdate = config.Bind(
            ConfigSections.Performance,
            "HudScannerMaxTextsPerUpdate",
            DefaultHudScannerMaxTextsPerUpdate,
            "每次 UpdateScanNodes 最多处理的扫描 HUD 文本数量。数值越低越稳，翻译补齐可能越慢。");
        _hudScannerMaxTextsPerUpdateFast = Mathf.Clamp(_hudScannerMaxTextsPerUpdate.Value, 1, 64);
    }

    public static void Clear()
    {
        _nextHudScannerLocalizationTime = 0f;
        _nextHudScannerElementLocalizationTime = 0f;
        _nextHudScannerRootLocalizationTime = 0f;
        _nextHudScannerBoundNodeLocalizationTime = 0f;
        _nextHudScannerActiveProbeTime = 0f;
        _nextHudScannerBindingSignatureProbeTime = 0f;
        _cachedHudScannerBindingCount = -1;
        _cachedHudScannerBindingSignature = 0;
        _lastHudScannerActive = false;
        _lastImmediateHudScannerActive = false;
        _lastHudScannerRootId = 0;
        _lastHudScannerTranslatedRootId = 0;
        _lastImmediateHudScannerRootId = 0;
        _lastImmediateHudScannerNodeCount = 0;
        _lastImmediateHudScannerElementCount = 0;
        _lastImmediateHudScannerBindingSignature = 0;
        _hudScannerElementCursor = 0;
        _hudScannerBoundNodeCursor = 0;
        _hudScannerActiveProbeCursor = 0;
        _hudScannerBoundNodeSnapshotSource = null;
        HudScannerBoundNodeSnapshot.Clear();
        _lastHudScannerTotalText = null;
        _lastImmediateHudScannerTotalText = null;
        ClearRuntimeCaches();
    }

    public static void ClearRuntimeCaches()
    {
        HudScannerElementTextCache.Clear();
        HudScannerTextStateCache.Clear();
        _hudScannerBoundNodeSnapshotSource = null;
        HudScannerBoundNodeSnapshot.Clear();
        _hudScannerBoundNodeCursor = 0;
        _nextHudScannerBindingSignatureProbeTime = 0f;
        _cachedHudScannerBindingCount = -1;
        _cachedHudScannerBindingSignature = 0;
    }

    public static void ApplyHudScannerLocalization(HUDManager? hud, string reason)
    {
        ApplyHudScannerLocalization(hud, reason, hasActiveScanner: true);
    }

    public static void ApplyHudScannerLocalization(HUDManager? hud, string reason, bool hasActiveScanner)
    {
        ApplyHudScannerLocalization(hud, reason, hasActiveScanner, immediatePass: false);
    }

    public static void ApplyHudScannerLocalization(HUDManager? hud, string reason, bool hasActiveScanner, bool immediatePass)
    {
        if (hud == null)
        {
            return;
        }

        var isUpdateScanNodes = string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal);
        if (isUpdateScanNodes && !hasActiveScanner)
        {
            return;
        }

        if (!isUpdateScanNodes)
        {
            TranslateHudScannerBoundNodes(hud, reason);
        }

        var root = GetHudScannerRoot(hud);
        if (!ShouldSkipHudScannerLocalization(hud, root, reason, immediatePass))
        {
            ApplyHudScannerTextTranslation(hud.totalValueText, reason);
        }

        ApplyHudScannerElementTextLocalization(hud, reason, immediatePass);

        if (!isUpdateScanNodes &&
            ShouldTranslateHudScannerRoot(root, reason))
        {
            TargetedUiTranslator.TranslateRoot(root, reason);
        }
    }

    public static void ApplyHudScannerBoundNodesIfDue(HUDManager? hud, string reason)
    {
        ApplyHudScannerBoundNodesIfDue(hud, reason, HasActiveHudScannerElement(hud));
    }

    public static void ApplyHudScannerBoundNodesIfDue(HUDManager? hud, string reason, bool hasActiveScanner)
    {
        ApplyHudScannerBoundNodesIfDue(hud, reason, hasActiveScanner, immediatePass: false);
    }

    public static void ApplyHudScannerBoundNodesIfDue(HUDManager? hud, string reason, bool hasActiveScanner, bool immediatePass)
    {
        if (ShouldTranslateHudScannerBoundNodes(reason, hasActiveScanner, immediatePass))
        {
            TranslateHudScannerBoundNodes(hud, reason, immediatePass);
        }
    }

    private static int GetHudScannerMaxTextsPerUpdate()
    {
        return _hudScannerMaxTextsPerUpdateFast;
    }

    public static bool ShouldRunImmediateHudScannerLocalizationPass(
        HUDManager? hud,
        bool probedHasActiveScanner,
        out bool hasActiveScanner)
    {
        hasActiveScanner = probedHasActiveScanner;
        if (hud == null)
        {
            _lastImmediateHudScannerActive = false;
            _lastImmediateHudScannerRootId = 0;
            _lastImmediateHudScannerNodeCount = 0;
            _lastImmediateHudScannerElementCount = 0;
            _lastImmediateHudScannerBindingSignature = 0;
            _lastImmediateHudScannerTotalText = null;
            return false;
        }

        var nodeCount = -1;
        var bindingSignature = 0;
        if (TryGetHudScannerBindingState(hud, out var scanNodeCount, out bindingSignature))
        {
            nodeCount = scanNodeCount;
            hasActiveScanner = hasActiveScanner || scanNodeCount > 0;
        }

        if (!hasActiveScanner)
        {
            _lastImmediateHudScannerActive = false;
            _lastImmediateHudScannerRootId = 0;
            _lastImmediateHudScannerNodeCount = nodeCount;
            _lastImmediateHudScannerElementCount = hud.scanElements?.Length ?? 0;
            _lastImmediateHudScannerBindingSignature = bindingSignature;
            _lastImmediateHudScannerTotalText = null;
            return false;
        }

        var root = GetHudScannerRoot(hud);
        var rootId = root == null ? 0 : root.GetInstanceID();
        var elementCount = hud.scanElements?.Length ?? 0;
        var totalText = hud.totalValueText?.text ?? string.Empty;
        var changed = !_lastImmediateHudScannerActive ||
                      rootId != _lastImmediateHudScannerRootId ||
                      nodeCount != _lastImmediateHudScannerNodeCount ||
                      elementCount != _lastImmediateHudScannerElementCount ||
                      bindingSignature != _lastImmediateHudScannerBindingSignature ||
                      !string.Equals(totalText, _lastImmediateHudScannerTotalText, StringComparison.Ordinal);

        _lastImmediateHudScannerActive = true;
        _lastImmediateHudScannerRootId = rootId;
        _lastImmediateHudScannerNodeCount = nodeCount;
        _lastImmediateHudScannerElementCount = elementCount;
        _lastImmediateHudScannerBindingSignature = bindingSignature;
        _lastImmediateHudScannerTotalText = totalText;
        return changed;
    }

    private static bool ShouldTranslateHudScannerBoundNodes(string reason, bool hasActiveScanner, bool immediatePass)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes.bound-nodes", StringComparison.Ordinal))
        {
            return true;
        }

        if (!hasActiveScanner)
        {
            return false;
        }

        var now = Time.unscaledTime;
        if (!immediatePass && now < _nextHudScannerBoundNodeLocalizationTime)
        {
            return false;
        }

        _nextHudScannerBoundNodeLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return true;
    }

    private static void ApplyHudScannerElementTextLocalization(HUDManager hud, string reason, bool immediatePass)
    {
        if (ShouldSkipHudScannerElementTextLocalization(reason, immediatePass))
        {
            return;
        }

        var elements = hud.scanElements;
        if (elements == null)
        {
            return;
        }

        var isUpdateScanNodes = string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal);
        var processed = 0;
        var maxTexts = GetHudScannerMaxTextsPerUpdate();
        var count = elements.Length;
        if (count <= 0)
        {
            _hudScannerElementCursor = 0;
            return;
        }

        var start = isUpdateScanNodes && !immediatePass && _hudScannerElementCursor < count ? _hudScannerElementCursor : 0;
        for (var offset = 0; offset < count; offset++)
        {
            var index = isUpdateScanNodes ? (start + offset) % count : offset;
            var element = elements[index];
            if (element == null || !element.gameObject.activeInHierarchy)
            {
                continue;
            }

            foreach (var text in GetHudScannerElementTexts(element))
            {
                if (isUpdateScanNodes && processed >= maxTexts)
                {
                    _hudScannerElementCursor = (index + 1) % count;
                    return;
                }

                if (text != null)
                {
                    processed++;
                }

                ApplyHudScannerTextTranslation(text, reason);
            }
        }

        if (isUpdateScanNodes)
        {
            _hudScannerElementCursor = 0;
        }
    }

    private static void TranslateHudScannerBoundNodes(HUDManager? hud, string reason)
    {
        TranslateHudScannerBoundNodes(hud, reason, immediatePass: false);
    }

    private static void TranslateHudScannerBoundNodes(HUDManager? hud, string reason, bool immediatePass)
    {
        if (hud == null)
        {
            return;
        }

        Dictionary<RectTransform, ScanNodeProperties>? scanNodes;
        try
        {
            scanNodes = hud.scanNodes;
        }
        catch (Exception)
        {
            return;
        }

        if (scanNodes == null || scanNodes.Count == 0)
        {
            return;
        }

        var isUpdateScanNodes = string.Equals(reason, "HUDManager.UpdateScanNodes.bound-nodes", StringComparison.Ordinal);
        RefreshHudScannerBoundNodeSnapshot(scanNodes);
        var count = HudScannerBoundNodeSnapshot.Count;
        if (count == 0)
        {
            _hudScannerBoundNodeCursor = 0;
            return;
        }

        var start = isUpdateScanNodes && _hudScannerBoundNodeCursor < count ? _hudScannerBoundNodeCursor : 0;
        var processed = 0;
        var maxTexts = GetHudScannerMaxTextsPerUpdate();
        for (var offset = 0; offset < count; offset++)
        {
            var index = (start + offset) % count;
            var pair = HudScannerBoundNodeSnapshot[index];

            if (pair.Key == null || pair.Value == null)
            {
                continue;
            }

            if (processed >= maxTexts)
            {
                if (isUpdateScanNodes)
                {
                    _hudScannerBoundNodeCursor = index;
                }

                return;
            }

            processed++;
            TranslateHudScannerBoundNode(pair.Key, pair.Value);
            if (isUpdateScanNodes)
            {
                _hudScannerBoundNodeCursor = (index + 1) % count;
            }
        }

        if (isUpdateScanNodes)
        {
            _hudScannerBoundNodeCursor = (start + Math.Max(1, count)) % count;
        }
    }

    private static void RefreshHudScannerBoundNodeSnapshot(
        Dictionary<RectTransform, ScanNodeProperties> scanNodes,
        bool force = false)
    {
        if (!force &&
            ReferenceEquals(_hudScannerBoundNodeSnapshotSource, scanNodes) &&
            HudScannerBoundNodeSnapshot.Count == scanNodes.Count)
        {
            return;
        }

        HudScannerBoundNodeSnapshot.Clear();
        foreach (var pair in scanNodes)
        {
            if (pair.Key == null || pair.Value == null)
            {
                continue;
            }

            HudScannerBoundNodeSnapshot.Add(pair);
        }

        _hudScannerBoundNodeSnapshotSource = scanNodes;
        if (_hudScannerBoundNodeCursor >= HudScannerBoundNodeSnapshot.Count)
        {
            _hudScannerBoundNodeCursor = 0;
        }
    }

    private static void TranslateHudScannerBoundNode(RectTransform element, ScanNodeProperties node)
    {
        if (!element.gameObject.activeInHierarchy)
        {
            return;
        }

        var texts = GetHudScannerElementTexts(element);
        if (texts.Length == 0)
        {
            return;
        }

        // ScanNodeProperties stays in its original language because scanner mods use exact
        // source strings (for example "Value: $0") to make visibility and caching decisions.
        ApplyHudScannerBoundText(texts[0], node.headerText, preserveHidden: false);
        if (texts.Length < 2)
        {
            return;
        }

        var sourceSubText = node.subText ?? string.Empty;
        if (TryResolveUnknownHudScannerScrapValue(node, out var resolvedSubText))
        {
            sourceSubText = resolvedSubText;
        }

        ApplyHudScannerBoundText(texts[1], sourceSubText, preserveHidden: true);
    }

    private static void ApplyHudScannerBoundText(TMP_Text? text, string? source, bool preserveHidden)
    {
        if (text == null)
        {
            return;
        }

        var sourceValue = source ?? string.Empty;
        if (preserveHidden && ShouldPreserveHiddenHudScannerSubText(sourceValue, text.text))
        {
            return;
        }

        var translated = TryTranslateHudScannerSourceText(sourceValue, out var translatedValue) &&
                         !string.Equals(sourceValue, translatedValue, StringComparison.Ordinal);
        var displayValue = translated ? translatedValue : sourceValue;
        if (!string.Equals(text.text, displayValue, StringComparison.Ordinal))
        {
            text.text = displayValue;
        }

        CacheHudScannerText(text.GetInstanceID(), sourceValue, translated ? displayValue : null);
        ApplyFallbackIfCjk(text, displayValue);
        if (translated)
        {
            Plugin.ReportTranslationHit();
        }
    }

    private static bool ShouldPreserveHiddenHudScannerSubText(string? source, string? rendered)
    {
        // An empty rendered value with a non-empty source is a third-party visibility decision.
        return !string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(rendered);
    }

    private static bool TryTranslateHudScannerSourceText(string source, out string translated)
    {
        return TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.HudScanner, source, out translated) ||
               TranslationService.TryTranslateFastExact(source, out translated);
    }

    private static bool TryResolveUnknownHudScannerScrapValue(ScanNodeProperties node, out string resolved)
    {
        resolved = node.subText ?? string.Empty;
        if (node.nodeType != 2 || !LooksLikeUnknownHudScannerValueText(node.subText))
        {
            return false;
        }

        var grabbable = node.GetComponentInParent<GrabbableObject>();
        if (!CanRevealHudScannerScrapValue(grabbable))
        {
            return false;
        }

        var scrapValue = grabbable.scrapValue > 0 ? grabbable.scrapValue : node.scrapValue;
        return TryBuildResolvedHudScannerScrapValueText(node.subText, scrapValue, out resolved);
    }

    private static bool CanRevealHudScannerScrapValue(GrabbableObject? grabbable)
    {
        return grabbable != null &&
               (grabbable.hasBeenHeld ||
                grabbable.isHeld ||
                grabbable.heldByPlayerOnServer ||
                grabbable.playerHeldBy != null);
    }

    private static bool TryBuildResolvedHudScannerScrapValueText(string? current, int scrapValue, out string resolved)
    {
        resolved = current ?? string.Empty;
        if (scrapValue <= 0 || !LooksLikeUnknownHudScannerValueText(current))
        {
            return false;
        }

        resolved = "Value: $" + scrapValue.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool LooksLikeUnknownHudScannerValueText(string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        var text = current.Trim();
        var colonIndex = text.IndexOf(':');
        var fullWidthColonIndex = text.IndexOf('\uff1a');
        if (fullWidthColonIndex >= 0 && (colonIndex < 0 || fullWidthColonIndex < colonIndex))
        {
            colonIndex = fullWidthColonIndex;
        }

        if (colonIndex <= 0 || colonIndex >= text.Length - 1)
        {
            return false;
        }

        var label = text[..colonIndex].Trim();
        var value = text[(colonIndex + 1)..].Trim();
        return string.Equals(value, "???", StringComparison.Ordinal) &&
               (string.Equals(label, "Value", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "\u4ef7\u503c", StringComparison.Ordinal));
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
                ApplyFallbackIfCjk(text, original);
                return false;
            }

            if (string.Equals(cached.LastOriginal, original, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(cached.LastTranslated))
                {
                    ApplyFallbackIfCjk(text, original);
                    return false;
                }

                text.text = cached.LastTranslated;
                ApplyFallbackIfCjk(text, cached.LastTranslated);
                return true;
            }
        }

        if (!TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.HudScanner, original, out var translated) &&
            !TranslationService.TryTranslateFastExact(original, out translated))
        {
            CacheHudScannerText(id, original, null);
            ApplyFallbackIfCjk(text, original);
            return false;
        }

        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            CacheHudScannerText(id, original, null);
            ApplyFallbackIfCjk(text, original);
            return false;
        }

        CacheHudScannerText(id, original, translated);
        text.text = translated;
        ApplyFallbackIfCjk(text, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    private static void ApplyFallbackIfCjk(TMP_Text text, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var ch in value)
        {
            if (ch is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF')
            {
                FontFallbackService.ApplyFallback(text, value);
                return;
            }
        }
    }

    public static bool HasActiveHudScannerElement(HUDManager? hud)
    {
        var now = Time.unscaledTime;
        if (hud == null)
        {
            _lastHudScannerActive = false;
            _nextHudScannerActiveProbeTime = now + HudScannerIdleActiveProbeIntervalSeconds;
            return false;
        }

        if (_lastHudScannerActive && now < _nextHudScannerActiveProbeTime)
        {
            return true;
        }

        if (!_lastHudScannerActive && now < _nextHudScannerActiveProbeTime)
        {
            return false;
        }

        if (TryGetHudScannerNodeCount(hud, out var nodeCount))
        {
            _lastHudScannerActive = nodeCount > 0;
            _nextHudScannerActiveProbeTime = now + (_lastHudScannerActive
                ? HudScannerActiveProbeIntervalSeconds
                : HudScannerIdleActiveProbeIntervalSeconds);
            return _lastHudScannerActive;
        }

        var elements = hud.scanElements;
        if (elements == null)
        {
            _lastHudScannerActive = false;
            _hudScannerActiveProbeCursor = 0;
            _nextHudScannerActiveProbeTime = now + HudScannerIdleActiveProbeIntervalSeconds;
            return false;
        }

        var count = elements.Length;
        if (count <= 0)
        {
            _lastHudScannerActive = false;
            _hudScannerActiveProbeCursor = 0;
            _nextHudScannerActiveProbeTime = now + HudScannerIdleActiveProbeIntervalSeconds;
            return false;
        }

        var budget = Math.Min(count, HudScannerActiveProbeFallbackElementBudget);
        var start = _hudScannerActiveProbeCursor < count ? _hudScannerActiveProbeCursor : 0;
        for (var offset = 0; offset < budget; offset++)
        {
            var index = (start + offset) % count;
            var element = elements[index];
            if (element != null && element.gameObject.activeInHierarchy)
            {
                _lastHudScannerActive = true;
                _hudScannerActiveProbeCursor = (index + 1) % count;
                _nextHudScannerActiveProbeTime = now + HudScannerActiveProbeIntervalSeconds;
                return true;
            }
        }

        _hudScannerActiveProbeCursor = (start + budget) % count;
        _lastHudScannerActive = false;
        _nextHudScannerActiveProbeTime = now + HudScannerIdleActiveProbeIntervalSeconds;
        return false;
    }

    private static bool TryGetHudScannerNodeCount(HUDManager hud, out int count)
    {
        count = 0;
        try
        {
            var scanNodes = hud.scanNodes;
            if (scanNodes == null)
            {
                return true;
            }

            count = scanNodes.Count;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetHudScannerBindingState(HUDManager hud, out int count, out int signature)
    {
        count = 0;
        signature = 0;
        try
        {
            var scanNodes = hud.scanNodes;
            if (scanNodes == null)
            {
                _cachedHudScannerBindingCount = 0;
                _cachedHudScannerBindingSignature = 0;
                return true;
            }

            count = scanNodes.Count;
            var now = Time.unscaledTime;
            if (count == _cachedHudScannerBindingCount &&
                now < _nextHudScannerBindingSignatureProbeTime)
            {
                signature = _cachedHudScannerBindingSignature;
                return true;
            }

            unchecked
            {
                var hash = 17;
                foreach (var pair in scanNodes)
                {
                    hash = hash * 31 + (pair.Key == null ? 0 : pair.Key.GetInstanceID());
                    hash = hash * 31 + (pair.Value == null ? 0 : pair.Value.GetInstanceID());
                }

                signature = hash;
            }

            _cachedHudScannerBindingCount = count;
            _cachedHudScannerBindingSignature = signature;
            _nextHudScannerBindingSignatureProbeTime = now + HudScannerBindingSignatureProbeIntervalSeconds;
            RefreshHudScannerBoundNodeSnapshot(scanNodes, force: true);

            return true;
        }
        catch
        {
            count = 0;
            signature = 0;
            return false;
        }
    }

    private static bool ShouldSkipHudScannerElementTextLocalization(string reason, bool immediatePass)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return false;
        }

        var now = Time.unscaledTime;
        if (!immediatePass && now < _nextHudScannerElementLocalizationTime)
        {
            return true;
        }

        _nextHudScannerElementLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return false;
    }

    private static void CacheHudScannerText(int id, string original, string? translated)
    {
        HudScannerTextStateCache.Set(id, new CachedHudScannerText
        {
            LastOriginal = original,
            LastTranslated = translated
        }, RuntimePerformanceSettings.HudScannerCacheLimit);
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
            HudScannerElementTextCache.Set(id, texts, RuntimePerformanceSettings.HudScannerCacheLimit);

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

    private static bool ShouldSkipHudScannerLocalization(HUDManager hud, GameObject? root, string reason, bool immediatePass)
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
        if (!immediatePass && !changed && now < _nextHudScannerLocalizationTime)
        {
            return true;
        }

        _lastHudScannerRootId = rootId;
        _lastHudScannerTotalText = totalText;
        _nextHudScannerLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return false;
    }

    private static GameObject? GetHudScannerRoot(HUDManager hud)
    {
        return hud.scanInfoAnimator == null
            ? hud.totalValueText?.transform.parent?.gameObject
            : hud.scanInfoAnimator.gameObject;
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
