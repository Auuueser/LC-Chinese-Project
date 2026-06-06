using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudScannerLocalizationService
{
    private static readonly AccessTools.FieldRef<HUDManager, Dictionary<RectTransform, ScanNodeProperties>>? HudScanNodesRef =
        CreateHudScanNodesRef();
    private const float HudScannerLocalizationIntervalSeconds = 0.5f;
    private const float HudScannerIdleActiveProbeIntervalSeconds = 0.25f;
    private const float HudScannerActiveProbeIntervalSeconds = 0.1f;
    private const float HudScannerRootLocalizationIntervalSeconds = 2.0f;
    private const int HudScannerTextCacheLimit = 16384;
    private const int DefaultHudScannerMaxTextsPerUpdate = 4;
    private const int HudScannerActiveProbeFallbackElementBudget = 4;
    private static ConfigEntry<int>? _hudScannerMaxTextsPerUpdate;
    private static int _hudScannerMaxTextsPerUpdateFast = DefaultHudScannerMaxTextsPerUpdate;
    private static float _nextHudScannerLocalizationTime;
    private static float _nextHudScannerElementLocalizationTime;
    private static float _nextHudScannerRootLocalizationTime;
    private static float _nextHudScannerSourceNodeLocalizationTime;
    private static float _nextHudScannerActiveProbeTime;
    private static bool _lastHudScannerActive;
    private static bool _lastImmediateHudScannerActive;
    private static int _lastHudScannerRootId;
    private static int _lastHudScannerTranslatedRootId;
    private static int _lastImmediateHudScannerRootId;
    private static int _lastImmediateHudScannerNodeCount;
    private static int _lastImmediateHudScannerElementCount;
    private static int _hudScannerElementCursor;
    private static int _hudScannerSourceNodeCursor;
    private static int _hudScannerActiveProbeCursor;
    private static string? _lastHudScannerTotalText;
    private static string? _lastImmediateHudScannerTotalText;
    private static readonly Dictionary<int, TMP_Text[]> HudScannerElementTextCache = new(HudScannerTextCacheLimit);
    private static readonly Dictionary<int, CachedHudScannerText> HudScannerTextStateCache = new(HudScannerTextCacheLimit);
    private static readonly Dictionary<int, CachedHudScannerNodeState> HudScannerNodeStateCache = new(HudScannerTextCacheLimit);

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
        _hudScannerMaxTextsPerUpdateFast = Mathf.Clamp(_hudScannerMaxTextsPerUpdate.Value, 1, 64);
    }

    public static void Clear()
    {
        RestoreHudScannerSourceNodes();
        _nextHudScannerLocalizationTime = 0f;
        _nextHudScannerElementLocalizationTime = 0f;
        _nextHudScannerRootLocalizationTime = 0f;
        _nextHudScannerSourceNodeLocalizationTime = 0f;
        _nextHudScannerActiveProbeTime = 0f;
        _lastHudScannerActive = false;
        _lastImmediateHudScannerActive = false;
        _lastHudScannerRootId = 0;
        _lastHudScannerTranslatedRootId = 0;
        _lastImmediateHudScannerRootId = 0;
        _lastImmediateHudScannerNodeCount = 0;
        _lastImmediateHudScannerElementCount = 0;
        _hudScannerElementCursor = 0;
        _hudScannerSourceNodeCursor = 0;
        _hudScannerActiveProbeCursor = 0;
        _lastHudScannerTotalText = null;
        _lastImmediateHudScannerTotalText = null;
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
            TranslateHudScannerSourceNodes(hud, reason);
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

    public static void ApplyHudScannerSourceNodesIfDue(HUDManager? hud, string reason)
    {
        ApplyHudScannerSourceNodesIfDue(hud, reason, HasActiveHudScannerElement(hud));
    }

    public static void ApplyHudScannerSourceNodesIfDue(HUDManager? hud, string reason, bool hasActiveScanner)
    {
        ApplyHudScannerSourceNodesIfDue(hud, reason, hasActiveScanner, immediatePass: false);
    }

    public static void ApplyHudScannerSourceNodesIfDue(HUDManager? hud, string reason, bool hasActiveScanner, bool immediatePass)
    {
        if (ShouldTranslateHudScannerSourceNodes(reason, hasActiveScanner, immediatePass))
        {
            TranslateHudScannerSourceNodes(hud, reason, immediatePass);
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
            _lastImmediateHudScannerTotalText = null;
            return false;
        }

        var nodeCount = -1;
        if (TryGetHudScannerNodeCount(hud, out var scanNodeCount))
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
                      !string.Equals(totalText, _lastImmediateHudScannerTotalText, StringComparison.Ordinal);

        _lastImmediateHudScannerActive = true;
        _lastImmediateHudScannerRootId = rootId;
        _lastImmediateHudScannerNodeCount = nodeCount;
        _lastImmediateHudScannerElementCount = elementCount;
        _lastImmediateHudScannerTotalText = totalText;
        return changed;
    }

    private static bool ShouldTranslateHudScannerSourceNodes(string reason, bool hasActiveScanner, bool immediatePass)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes.scan-node-source", StringComparison.Ordinal))
        {
            return true;
        }

        if (!hasActiveScanner)
        {
            return false;
        }

        var now = Time.unscaledTime;
        if (!immediatePass && now < _nextHudScannerSourceNodeLocalizationTime)
        {
            return false;
        }

        _nextHudScannerSourceNodeLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
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
        var maxTexts = immediatePass ? RuntimePerformanceSettings.HudScannerCacheLimit : GetHudScannerMaxTextsPerUpdate();
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

    private static void TranslateHudScannerSourceNodes(HUDManager? hud, string reason)
    {
        TranslateHudScannerSourceNodes(hud, reason, immediatePass: false);
    }

    private static void TranslateHudScannerSourceNodes(HUDManager? hud, string reason, bool immediatePass)
    {
        if (hud == null || HudScanNodesRef == null)
        {
            return;
        }

        var scanNodes = HudScanNodesRef(hud);

        if (scanNodes == null || scanNodes.Count == 0)
        {
            return;
        }

        var isUpdateScanNodes = string.Equals(reason, "HUDManager.UpdateScanNodes.scan-node-source", StringComparison.Ordinal);
        var count = scanNodes.Count;
        var start = isUpdateScanNodes && !immediatePass && _hudScannerSourceNodeCursor < count ? _hudScannerSourceNodeCursor : 0;
        var processed = 0;
        var maxTexts = immediatePass ? RuntimePerformanceSettings.HudScannerCacheLimit : GetHudScannerMaxTextsPerUpdate();
        var index = 0;
        foreach (var node in scanNodes.Values)
        {
            if (index++ < start)
            {
                continue;
            }

            if (node == null)
            {
                continue;
            }

            if (processed >= maxTexts)
            {
                if (isUpdateScanNodes)
                {
                    _hudScannerSourceNodeCursor = Math.Max(0, (index - 1) % count);
                }

                return;
            }

            processed++;
            TranslateHudScannerSourceNode(node, reason);
        }

        if (!isUpdateScanNodes || start == 0 || processed >= maxTexts)
        {
            if (isUpdateScanNodes)
            {
                _hudScannerSourceNodeCursor = 0;
            }

            return;
        }

        index = 0;
        foreach (var node in scanNodes.Values)
        {
            if (index++ >= start)
            {
                break;
            }

            if (node == null)
            {
                continue;
            }

            if (processed >= maxTexts)
            {
                _hudScannerSourceNodeCursor = Math.Max(0, (index - 1) % count);
                return;
            }

            processed++;
            TranslateHudScannerSourceNode(node, reason);
        }

        _hudScannerSourceNodeCursor = 0;
    }

    private static AccessTools.FieldRef<HUDManager, Dictionary<RectTransform, ScanNodeProperties>>? CreateHudScanNodesRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<HUDManager, Dictionary<RectTransform, ScanNodeProperties>>("scanNodes");
        }
        catch
        {
            return null;
        }
    }

    private static void TranslateHudScannerSourceNode(ScanNodeProperties node, string reason)
    {
        var id = node.GetInstanceID();
        if (!HudScannerNodeStateCache.TryGetValue(id, out var state))
        {
            if (HudScannerNodeStateCache.Count >= RuntimePerformanceSettings.HudScannerCacheLimit)
            {
                return;
            }

            state = new CachedHudScannerNodeState { Node = node };
            HudScannerNodeStateCache[id] = state;
        }

        state.Node = node;
        var resolvedUnknownSubText = TryResolveUnknownHudScannerScrapValue(node, out var resolvedSubText);
        if (resolvedUnknownSubText)
        {
            node.subText = resolvedSubText;
        }

        if (!resolvedUnknownSubText && TrySkipUnchangedHudScannerSourceNode(node, state))
        {
            return;
        }

        var changed = resolvedUnknownSubText;
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
        if (HudScanNodesRef == null)
        {
            return false;
        }

        try
        {
            var scanNodes = HudScanNodesRef(hud);
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
        if (HudScannerTextStateCache.Count >= RuntimePerformanceSettings.HudScannerCacheLimit &&
            !HudScannerTextStateCache.ContainsKey(id))
        {
            return;
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
            if (HudScannerElementTextCache.Count < RuntimePerformanceSettings.HudScannerCacheLimit ||
                HudScannerElementTextCache.ContainsKey(id))
            {
                HudScannerElementTextCache[id] = texts;
            }

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
