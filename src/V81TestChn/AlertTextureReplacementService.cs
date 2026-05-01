using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace V81TestChn;

internal static class AlertTextureReplacementService
{
    private enum NativeTextRole
    {
        None,
        SystemOnline,
        EnteringAtmosphere
    }

    private readonly struct CachedNativeTextRole
    {
        public CachedNativeTextRole(int parentId, NativeTextRole role)
        {
            ParentId = parentId;
            Role = role;
        }

        public int ParentId { get; }
        public NativeTextRole Role { get; }
    }

    private const int NativeTextRoleCacheLimit = 4096;
    private const string SystemOnlineTitleObjectName = "TipLeft1";
    private const string SystemOnlineTitleFullPath = "Systems/UI/Canvas/IngamePlayerHUD/BottomMiddle/SystemsOnline/TipLeft1";
    private const string SystemOnlineTitlePathSuffix = "IngamePlayerHUD/BottomMiddle/SystemsOnline/TipLeft1";
    private const string SystemOnlineRelativePath = "BottomMiddle/SystemsOnline/TipLeft1";
    private const string EnteringAtmosphereTitleObjectName = "LoadText";
    private const string RelaySceneName = "SampleSceneRelay";
    private const string SystemOnlineLocalizedText = "\u7cfb\u7edf\u5728\u7ebf";
    private const string EnteringAtmosphereLocalizedText = "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42...";
    private const string HazardLevelLocalizedText = "\u5371\u9669\u7b49\u7ea7\uff1a";
    private const string LifeSupportOfflineLocalizedText = "[\u751f\u547d\u7ef4\u6301\uff1a\u79bb\u7ebf]";
    private static Coroutine? _systemOnlineWatcher;
    private static Coroutine? _fixedSceneLabelWatcher;
    private static readonly Dictionary<int, CachedNativeTextRole> NativeTextRoleCache = new();
    private static readonly Dictionary<string, string> FixedSceneLabels = new(StringComparer.Ordinal)
    {
        ["TO MEET PROFIT QUOTA"] = "\u4ee5\u8fbe\u5230\u5229\u6da6\u914d\u989d",
        ["PERFORMANCE REPORT"] = "\u7ee9\u6548\u62a5\u544a",
        ["EMPLOYEE RANK"] = "\u5458\u5de5\u7b49\u7ea7",
        ["Fines"] = "\u7f5a\u6b3e",
        ["(Dead)"] = "\uff08\u6b7b\u4ea1\uff09",
        ["Deceased"] = "\u6b7b\u4ea1",
        ["[LIFE SUPPORT: OFFLINE]"] = LifeSupportOfflineLocalizedText,
        ["You will keep your employee rank. Your ship and credits will be reset."] = "\u4f60\u7684\u5458\u5de5\u7b49\u7ea7\u5c06\u88ab\u4fdd\u7559\uff0c\u4f46\u98de\u8239\u548c\u70b9\u6570\u5c06\u88ab\u91cd\u7f6e\u3002"
    };
    public static void Initialize(string pluginDir)
    {
        Plugin.Log.LogInfo("Native relay title translation enabled; SYSTEMS ONLINE uses original TMP object only.");
    }

    public static void ForceApplySystemOnlineOverlay(HUDManager? hudManager, string stage)
    {
        var direct = FindSystemOnlineTitle(hudManager, allowGlobalFallback: false);
        if (direct != null)
        {
            ApplySystemOnlineNativeTranslation(direct, stage);
            return;
        }

        Plugin.Log.LogWarning($"NativeRelay[{stage}] target=SystemOnline action=not-found");
        AuditSystemOnlineBranch(stage, hudManager);
    }

    public static void BeginSystemOnlineExactPathWatcher(HUDManager? hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        if (_systemOnlineWatcher != null)
        {
            return;
        }

        _systemOnlineWatcher = hudManager.StartCoroutine(WaitForSystemOnlineTitle(hudManager, stage));
    }

    public static void TryApplySystemOnlineDirect(HUDManager? hudManager, string stage)
    {
        var direct = FindSystemOnlineTitle(hudManager, allowGlobalFallback: true);
        if (direct == null)
        {
            Plugin.Log.LogWarning($"NativeRelay[{stage}] target=SystemOnline action=not-found");
            return;
        }

        ApplySystemOnlineNativeTranslation(direct, stage);
    }

    public static void QueueSystemOnlineSync(HUDManager? hudManager, string stage)
    {
        return;
    }

    public static void TryApplyEnteringAtmosphereOverlayFromLoadingScreen(HUDManager? hudManager, string stage)
    {
        var title = FindEnteringAtmosphereTitle(hudManager);
        if (title == null)
        {
            Plugin.Log.LogWarning($"NativeRelay[{stage}] target=EnteringAtmosphere action=not-found");
            return;
        }

        ApplyEnteringAtmosphereNativeTranslation(title, stage);
    }

    public static void HideEnteringAtmosphereOverlayForHud(HUDManager? hudManager, string stage)
    {
        var title = FindEnteringAtmosphereTitle(hudManager);
        if (title != null)
        {
            ApplyEnteringAtmosphereNativeTranslation(title, stage);
        }
    }

    public static void SyncEnteringAtmosphereOverlayState(TMP_Text? text, string stage)
    {
        if (text == null || GetNativeTextRole(text) != NativeTextRole.EnteringAtmosphere)
        {
            return;
        }

        ApplyEnteringAtmosphereNativeTranslation(text, stage);
    }

    public static void SyncHazardLevelRelay(HUDManager? hudManager, string stage)
    {
        var title = FindHazardLevelTitle(hudManager);
        if (title != null)
        {
            ApplyHazardLevelNativeTranslation(title, stage);
        }
    }

    public static void SyncLandingInfo(HUDManager? hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        ApplyDynamicFieldTranslation(hudManager.planetInfoHeaderText, stage, "PlanetInfoHeader");
        ApplyDynamicFieldTranslation(hudManager.planetInfoSummaryText, stage, "PlanetInfoSummary");
        ApplyDynamicFieldTranslation(hudManager.planetRiskLevelText, stage, "PlanetRiskLevel");
        SyncHazardLevelRelay(hudManager, stage);
    }

    public static void SyncFixedSceneLabels(HUDManager? hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        foreach (var text in hudManager.GetComponentsInChildren<TMP_Text>(true))
        {
            TryReplaceFixedSceneLabel(text, stage);
        }
    }

    public static void BeginFixedSceneLabelWatcher(HUDManager? hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        if (_fixedSceneLabelWatcher != null)
        {
            return;
        }

        _fixedSceneLabelWatcher = hudManager.StartCoroutine(WaitForFixedSceneLabels(stage));
    }

    public static void TryReplaceSystemOnlineText(TMP_Text? text, string stage)
    {
        if (text == null)
        {
            return;
        }

        var role = GetNativeTextRole(text);
        if (role == NativeTextRole.SystemOnline)
        {
            ApplySystemOnlineNativeTranslation(text, stage);
            return;
        }

        if (role == NativeTextRole.EnteringAtmosphere)
        {
            ApplyEnteringAtmosphereNativeTranslation(text, stage);
            return;
        }

        if (IsHazardLevelTitle(text.text))
        {
            ApplyHazardLevelNativeTranslation(text, stage);
            return;
        }

        TryReplaceFixedSceneLabel(text, stage);
    }

    public static bool TryReplaceFixedSceneLabel(TMP_Text? text, string stage)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return false;
        }

        if (!TryResolveFixedSceneLabel(text.text, out var localized))
        {
            return false;
        }

        ApplyLocalizedText(text, localized, stage, "FixedSceneLabel");
        Plugin.Log.LogInfo($"NativeRelay[{stage}] target=FixedSceneLabel action=applied name={text.name} path={BuildPath(text.transform)} text={text.text}");
        return true;
    }

    public static void TryReplaceSystemOnlineText(UnityEngine.UI.Text? text, string stage)
    {
    }

    public static void TryReplaceSystemOnlineText(TextMesh? text, string stage)
    {
    }

    public static void TryReplace(UnityEngine.UI.Image? image, string stage)
    {
    }

    public static void TryReplace(UnityEngine.UI.RawImage? image, string stage)
    {
    }

    public static void TryReplace(SpriteRenderer? renderer, string stage)
    {
    }

    public static void TryReplace(Material? material, string stage)
    {
    }

    private static void ApplySystemOnlineNativeTranslation(TMP_Text text, string stage)
    {
        if (!text.enabled)
        {
            text.enabled = true;
        }

        ApplyLocalizedText(text, ResolveLocalizedText(text.text, SystemOnlineLocalizedText), stage, "SystemOnline");
        Plugin.Log.LogInfo($"NativeRelay[{stage}] target=SystemOnline action=applied name={text.name} path={BuildPath(text.transform)} text={text.text}");
    }

    private static IEnumerator WaitForSystemOnlineTitle(HUDManager hudManager, string stage)
    {
        const float timeoutSeconds = 12f;
        const float intervalSeconds = 0.1f;
        var elapsed = 0f;

        while (hudManager != null && elapsed < timeoutSeconds)
        {
            var direct = FindSystemOnlineTitleByFullPath();
            if (IsExactSystemOnlineTitle(direct))
            {
                ApplySystemOnlineNativeTranslation(direct!, $"{stage}.watcher");
                _systemOnlineWatcher = null;
                yield break;
            }

            yield return new WaitForSeconds(intervalSeconds);
            elapsed += intervalSeconds;
        }

        Plugin.Log.LogWarning($"NativeRelay[{stage}.watcher] target=SystemOnline action=timeout");
        _systemOnlineWatcher = null;
    }

    private static IEnumerator WaitForFixedSceneLabels(string stage)
    {
        const float timeoutSeconds = 8f;
        const float intervalSeconds = 0.25f;
        var elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            var appliedCount = SyncFixedSceneLabelsInRelayScene($"{stage}.relay-scene");
            if (appliedCount > 0)
            {
                _fixedSceneLabelWatcher = null;
                yield break;
            }

            yield return new WaitForSeconds(intervalSeconds);
            elapsed += intervalSeconds;
        }

        Plugin.Log.LogWarning($"NativeRelay[{stage}.relay-scene] target=FixedSceneLabel action=timeout");
        _fixedSceneLabelWatcher = null;
    }

    private static void ApplyEnteringAtmosphereNativeTranslation(TMP_Text text, string stage)
    {
        ApplyLocalizedText(text, ResolveLocalizedText(text.text, EnteringAtmosphereLocalizedText), stage, "EnteringAtmosphere");
    }

    private static void ApplyHazardLevelNativeTranslation(TMP_Text text, string stage)
    {
        ApplyLocalizedText(text, HazardLevelLocalizedText, stage, "HazardLevel");
    }

    private static void ApplyLocalizedText(TMP_Text text, string localized, string stage, string target)
    {
        if (string.IsNullOrWhiteSpace(localized))
        {
            return;
        }

        if (!string.Equals(text.text, localized, StringComparison.Ordinal))
        {
            text.text = localized;
        }

        FontFallbackService.ApplyFallback(text, localized);
        ApplyLifeSupportOfflineTypography(text, localized);
    }

    private static void ApplyLifeSupportOfflineTypography(TMP_Text text, string localized)
    {
        if (!string.Equals(localized, LifeSupportOfflineLocalizedText, StringComparison.Ordinal))
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontStyle = FontStyles.Normal;
        text.characterSpacing = Math.Max(text.characterSpacing, 6f);
        text.wordSpacing = Math.Max(text.wordSpacing, 8f);
    }

    private static TMP_Text? FindSystemOnlineTitle(HUDManager? hudManager, bool allowGlobalFallback)
    {
        if (hudManager != null)
        {
            if (hudManager.transform != null)
            {
                var direct = hudManager.transform.Find(SystemOnlineRelativePath);
                var directText = direct?.GetComponent<TMP_Text>();
                if (IsExactSystemOnlineTitle(directText))
                {
                    return directText;
                }
            }

            foreach (var text in hudManager.GetComponentsInChildren<TMP_Text>(true))
            {
                if (IsExactSystemOnlineTitle(text))
                {
                    return text;
                }
            }
        }

        if (allowGlobalFallback)
        {
            var directText = FindSystemOnlineTitleByFullPath();
            if (IsExactSystemOnlineTitle(directText))
            {
                return directText;
            }

            foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (IsExactSystemOnlineTitle(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static TMP_Text? FindSystemOnlineTitleByFullPath()
    {
        var directObject = GameObject.Find(SystemOnlineTitleFullPath);
        return directObject != null ? directObject.GetComponent<TMP_Text>() : null;
    }

    private static TMP_Text? FindEnteringAtmosphereTitle(HUDManager? hudManager)
    {
        if (hudManager?.LoadingScreen == null)
        {
            return null;
        }

        foreach (var text in hudManager.LoadingScreen.GetComponentsInChildren<TMP_Text>(true))
        {
            if (IsEnteringAtmosphereTitleObject(text))
            {
                return text;
            }
        }

        return null;
    }

    private static TMP_Text? FindHazardLevelTitle(HUDManager? hudManager)
    {
        if (hudManager == null)
        {
            return null;
        }

        foreach (var text in hudManager.GetComponentsInChildren<TMP_Text>(true))
        {
            if (IsHazardLevelTitle(text.text))
            {
                return text;
            }
        }

        return null;
    }

    private static bool IsSystemOnlineObject(TMP_Text? text)
    {
        return GetNativeTextRole(text) == NativeTextRole.SystemOnline;
    }

    private static bool IsSystemOnlinePath(Transform? transform)
    {
        if (transform == null)
        {
            return false;
        }

        return BuildPath(transform).EndsWith(SystemOnlineTitlePathSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnteringAtmosphereTitleObject(TMP_Text? text)
    {
        return GetNativeTextRole(text) == NativeTextRole.EnteringAtmosphere;
    }

    private static bool IsHazardLevelTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        return string.Equals(trimmed, "HAZARD LEVEL:", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, HazardLevelLocalizedText, StringComparison.Ordinal) ||
               string.Equals(trimmed, "\u5371\u9669\u7b49\u7ea7:", StringComparison.Ordinal);
    }

    private static string ResolveLocalizedText(string? current, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(current) && TranslationService.TryTranslate(current, out var translated))
        {
            return translated;
        }

        return fallback;
    }

    private static bool TryResolveFixedSceneLabel(string? current, out string localized)
    {
        localized = string.Empty;
        if (string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        if (FixedSceneLabels.TryGetValue(current.Trim(), out localized) && !string.IsNullOrWhiteSpace(localized))
        {
            return true;
        }

        return false;
    }

    private static int SyncFixedSceneLabelsInRelayScene(string stage)
    {
        var scene = SceneManager.GetSceneByName(RelaySceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return 0;
        }

        var appliedCount = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (TryReplaceFixedSceneLabel(text, stage))
                {
                    appliedCount++;
                }
            }
        }

        return appliedCount;
    }

    private static bool HasNamedAncestor(Transform? transform, string expectedName)
    {
        var current = transform;
        while (current != null)
        {
            if (string.Equals(current.name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void AuditSystemOnlineBranch(string stage, HUDManager? hudManager)
    {
        var seen = 0;
        if (hudManager != null)
        {
            foreach (var text in hudManager.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!HasNamedAncestor(text.transform, "SystemsOnline"))
                {
                    continue;
                }

                seen++;
                Plugin.Log.LogInfo($"NativeRelay[{stage}] target=SystemOnline action=audit exact={IsExactSystemOnlineTitle(text)} name={text.name} path={BuildPath(text.transform)} text={text.text}");
            }
        }

        if (seen > 0)
        {
            return;
        }

    }

    private static void ApplyDynamicFieldTranslation(TMP_Text? text, string stage, string target)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        if (!TranslationService.TryTranslate(text.text, out var translated) || string.IsNullOrWhiteSpace(translated))
        {
            return;
        }

        if (!string.Equals(text.text, translated, StringComparison.Ordinal))
        {
            text.text = translated;
        }

        FontFallbackService.ApplyFallback(text, translated);
        Plugin.Log.LogInfo($"NativeRelay[{stage}] target={target} action=applied name={text.name} path={BuildPath(text.transform)} text={text.text}");
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

    private static bool IsExactSystemOnlineTitle(TMP_Text? text)
    {
        return GetNativeTextRole(text) == NativeTextRole.SystemOnline;
    }

    private static NativeTextRole GetNativeTextRole(TMP_Text? text)
    {
        if (text == null)
        {
            return NativeTextRole.None;
        }

        if (TryGetCachedNativeTextRole(text, out var role))
        {
            return role;
        }

        role = ResolveNativeTextRole(text);
        CacheNativeTextRole(text, role);
        return role;
    }

    private static bool TryGetCachedNativeTextRole(TMP_Text text, out NativeTextRole role)
    {
        var parentId = GetParentInstanceId(text.transform);
        if (NativeTextRoleCache.TryGetValue(text.GetInstanceID(), out var cached) && cached.ParentId == parentId)
        {
            role = cached.Role;
            return true;
        }

        role = NativeTextRole.None;
        return false;
    }

    private static NativeTextRole ResolveNativeTextRole(TMP_Text text)
    {
        if (string.Equals(text.name, EnteringAtmosphereTitleObjectName, StringComparison.OrdinalIgnoreCase) &&
            HasNamedAncestor(text.transform, "LoadingText"))
        {
            return NativeTextRole.EnteringAtmosphere;
        }

        if (string.Equals(text.name, SystemOnlineTitleObjectName, StringComparison.OrdinalIgnoreCase) &&
            IsSystemOnlinePath(text.transform))
        {
            return NativeTextRole.SystemOnline;
        }

        return NativeTextRole.None;
    }

    private static void CacheNativeTextRole(TMP_Text text, NativeTextRole role)
    {
        if (NativeTextRoleCache.Count >= NativeTextRoleCacheLimit)
        {
            NativeTextRoleCache.Clear();
        }

        NativeTextRoleCache[text.GetInstanceID()] = new CachedNativeTextRole(GetParentInstanceId(text.transform), role);
    }

    private static int GetParentInstanceId(Transform? transform)
    {
        return transform?.parent == null ? 0 : transform.parent.GetInstanceID();
    }
}
