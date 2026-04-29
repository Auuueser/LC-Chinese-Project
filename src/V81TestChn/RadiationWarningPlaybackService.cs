using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class RadiationWarningPlaybackService
{
    private const string TextureSubfolder = "textures";
    private const string WarningRootPathSuffix = "IngamePlayerHUD/SpecialHUDGraphics/RadiationIncrease";
    private const string PanelObjectName = "Panel";
    private const float OriginalClipDurationSeconds = 103f / 60f;
    private const float DefaultFollowDurationSeconds = 1.85f;

    private static readonly Dictionary<string, string[]> OriginalSpriteFrameFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RedUIPanelGlitchB"] = FrameFileNameCandidates("RedUIPanelGlitchB", "C864CEDFD2-470F6C25C6"),
        ["RedUIPanelGlitchBWarning"] = FrameFileNameCandidates("RedUIPanelGlitchBWarning", "E5C5C6E52B-F9F9EA9261"),
        ["RedUIPanelGlitchBWarningRadiation"] = FrameFileNameCandidates("RedUIPanelGlitchBWarningRadiation", "FFCC97E055-2C688E6DC8"),
        ["RedUIPanelGlitchBWarningRadiationB"] = FrameFileNameCandidates("RedUIPanelGlitchBWarningRadiationB", "7D4ED37952-95F686DE01"),
        ["RedUIPanelGlitchBWarningRadiationC"] = FrameFileNameCandidates("RedUIPanelGlitchBWarningRadiationC", "7FBAA1AA30-9E2C036778"),
        ["RedUIPanelGlitchBWarningRadiationD"] = FrameFileNameCandidates("RedUIPanelGlitchBWarningRadiationD", "4877939F7B-47E20481B0"),
    };

    private static readonly Dictionary<string, Texture2D?> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Texture2D?> ResolvedFrameTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite?> SpriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly FieldInfo? RawOverrideSpriteField = typeof(Image).GetField("m_OverrideSprite", BindingFlags.Instance | BindingFlags.NonPublic);

    private static string? _textureDirectory;
    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<float>? _followDurationSeconds;
    private static HUDManager? _activeHudManager;
    private static Coroutine? _activePlaybackCoroutine;
    private static PlaybackState? _activePlaybackState;

    private sealed class PlaybackState
    {
        public PlaybackState(Transform root, Image panelImage, Sprite? originalRawOverrideSprite)
        {
            Root = root;
            PanelImage = panelImage;
            OriginalRawOverrideSprite = originalRawOverrideSprite;
        }

        public Transform Root { get; }
        public Image PanelImage { get; }
        public Sprite? OriginalRawOverrideSprite { get; }
        public Sprite? LastAppliedLocalizedSprite { get; set; }
        public string? LastAppliedOriginalSpriteName { get; set; }
        public bool CleanupCompleted { get; set; }
    }

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _textureDirectory = Path.Combine(pluginDir, TextureSubfolder);
        _enabled = config.Bind(
            "RadiationWarningPlayback",
            "Enabled",
            true,
            "Enable Animator-following radiation warning sprite substitution.");
        _followDurationSeconds = config.Bind(
            "RadiationWarningPlayback",
            "FollowDurationSeconds",
            DefaultFollowDurationSeconds,
            "Seconds to follow the original RadiationIncreaseWarning Animator clip and substitute localized sprites.");

        foreach (var originalSpriteName in OriginalSpriteFrameFiles.Keys)
        {
            ResolveFrameTexture(originalSpriteName, "Initialize");
        }

        Plugin.Log.LogInfo($"RadiationPlayback initialized mode=animator-follow clipSeconds={OriginalClipDurationSeconds:0.###}");
    }

    public static void OnRadiationWarningTriggered(HUDManager hudManager, string stage)
    {
        if (hudManager == null || _enabled?.Value != true)
        {
            return;
        }

        if (_activePlaybackCoroutine != null)
        {
            Plugin.Log.LogInfo($"RadiationPlayback[{stage}] action=duplicate-trigger-ignored mode=animator-follow");
            return;
        }

        var root = FindWarningRoot(hudManager);
        if (root == null)
        {
            Plugin.Log.LogWarning($"RadiationPlayback[{stage}] action=root-not-found suffix={WarningRootPathSuffix}");
            return;
        }

        var panelImage = FindWarningPanel(root);
        if (panelImage == null)
        {
            Plugin.Log.LogWarning($"RadiationPlayback[{stage}] action=panel-not-found path={BuildPath(root)} child={PanelObjectName}");
            return;
        }

        if (!HasAllLocalizedTextures(stage))
        {
            return;
        }

        var playbackState = new PlaybackState(root, panelImage, ReadRawOverrideSprite(panelImage));
        _activeHudManager = hudManager;
        _activePlaybackState = playbackState;
        _activePlaybackCoroutine = hudManager.StartCoroutine(FollowAnimatorSpriteCurve(playbackState, stage));
    }

    public static void Shutdown()
    {
        CleanupActivePlayback("Shutdown", "shutdown");
    }

    public static void ResetForHudLifecycle(HUDManager hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        if (_activePlaybackCoroutine == null && _activePlaybackState == null)
        {
            return;
        }

        CleanupActivePlayback(stage, "hud-reset");
    }

    private static IEnumerator FollowAnimatorSpriteCurve(PlaybackState playbackState, string stage)
    {
        var duration = Mathf.Max(OriginalClipDurationSeconds, _followDurationSeconds?.Value ?? DefaultFollowDurationSeconds);
        var endTime = Time.realtimeSinceStartup + duration;

        Plugin.Log.LogInfo(
            $"RadiationPlayback[{stage}] FollowAnimatorSpriteCurve started path={BuildPath(playbackState.Root)} panel={BuildPath(playbackState.PanelImage.transform)} duration={duration:0.###}");

        try
        {
            while (playbackState.PanelImage != null && Time.realtimeSinceStartup <= endTime)
            {
                ApplyCurrentAnimatorSpriteSubstitution(playbackState, stage);
                yield return null;
            }

            if (playbackState.PanelImage != null)
            {
                ApplyCurrentAnimatorSpriteSubstitution(playbackState, stage);
            }

            Plugin.Log.LogInfo($"RadiationPlayback[{stage}] FollowAnimatorSpriteCurve finished path={BuildPath(playbackState.Root)}");
        }
        finally
        {
            CleanupPlaybackState(playbackState, stage, "finally");
        }
    }

    private static void ApplyCurrentAnimatorSpriteSubstitution(PlaybackState playbackState, string stage)
    {
        var panelImage = playbackState.PanelImage;
        var originalSprite = panelImage.sprite;
        var originalSpriteName = NormalizeSpriteName(originalSprite?.name);
        if (string.IsNullOrWhiteSpace(originalSpriteName))
        {
            return;
        }

        if (!OriginalSpriteFrameFiles.ContainsKey(originalSpriteName))
        {
            ClearLocalizedOverrideIfOwned(playbackState, "unmapped-original");
            return;
        }

        var texture = ResolveFrameTexture(originalSpriteName, stage);
        if (texture == null)
        {
            return;
        }

        var localizedSprite = ResolveLocalizedSprite(originalSpriteName, originalSprite, texture);
        if (localizedSprite == null)
        {
            return;
        }

        var currentRawOverride = ReadRawOverrideSprite(panelImage);
        if (ReferenceEquals(currentRawOverride, localizedSprite))
        {
            return;
        }

        panelImage.overrideSprite = localizedSprite;
        panelImage.SetVerticesDirty();
        panelImage.SetMaterialDirty();

        if (!string.Equals(playbackState.LastAppliedOriginalSpriteName, originalSpriteName, StringComparison.OrdinalIgnoreCase))
        {
            // Noisy per-sprite-key log; keep code for future diagnostics without flooding LogOutput.log.
            // Plugin.Log.LogInfo(
            //     $"RadiationPlayback[{stage}] action=substitute-original-sprite original={originalSpriteName} localized={localizedSprite.name}");
        }

        playbackState.LastAppliedOriginalSpriteName = originalSpriteName;
        playbackState.LastAppliedLocalizedSprite = localizedSprite;
    }

    private static void CleanupActivePlayback(string stage, string reason)
    {
        var playbackCoroutine = _activePlaybackCoroutine;
        var activeHudManager = _activeHudManager;
        var playbackState = _activePlaybackState;

        _activePlaybackCoroutine = null;
        _activeHudManager = null;
        _activePlaybackState = null;

        if (playbackCoroutine != null && activeHudManager != null)
        {
            activeHudManager.StopCoroutine(playbackCoroutine);
        }

        if (playbackState != null)
        {
            CleanupPlaybackState(playbackState, stage, reason);
        }
    }

    private static void CleanupPlaybackState(PlaybackState playbackState, string stage, string reason)
    {
        if (playbackState.CleanupCompleted)
        {
            return;
        }

        playbackState.CleanupCompleted = true;
        ClearLocalizedOverrideIfOwned(playbackState, reason);

        if (ReferenceEquals(_activePlaybackState, playbackState))
        {
            _activePlaybackState = null;
            _activePlaybackCoroutine = null;
            _activeHudManager = null;
        }

        var panelPath = playbackState.PanelImage != null
            ? BuildPath(playbackState.PanelImage.transform)
            : "<null>";
        Plugin.Log.LogInfo(
            $"RadiationPlayback[{stage}] action=restore reason={reason} path={BuildPath(playbackState.Root)} panel={panelPath}");
    }

    private static void ClearLocalizedOverrideIfOwned(PlaybackState playbackState, string reason)
    {
        var panelImage = playbackState.PanelImage;
        if (panelImage == null)
        {
            return;
        }

        var currentRawOverride = ReadRawOverrideSprite(panelImage);
        if (currentRawOverride != null &&
            !ReferenceEquals(currentRawOverride, playbackState.LastAppliedLocalizedSprite) &&
            !IsCachedLocalizedSprite(currentRawOverride))
        {
            Plugin.Log.LogInfo($"RadiationPlayback action=skip-restore reason={reason} currentOverride={currentRawOverride.name}");
            return;
        }

        panelImage.overrideSprite = playbackState.OriginalRawOverrideSprite;
        panelImage.SetVerticesDirty();
        panelImage.SetMaterialDirty();
    }

    private static bool HasAllLocalizedTextures(string stage)
    {
        foreach (var originalSpriteName in OriginalSpriteFrameFiles.Keys)
        {
            if (ResolveFrameTexture(originalSpriteName, stage) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static string[] FrameFileNameCandidates(string baseName, string contentHash)
    {
        return new[]
        {
            $"{baseName} [{contentHash}].png",
            $"{baseName}[{contentHash}].png",
        };
    }

    private static string? ResolveFrameFileName(string originalSpriteName, string stage)
    {
        if (!OriginalSpriteFrameFiles.TryGetValue(originalSpriteName, out var candidates))
        {
            return null;
        }

        foreach (var candidate in candidates)
        {
            if (LoadTexture(candidate) != null)
            {
                return candidate;
            }
        }

        Plugin.Log.LogWarning($"RadiationPlayback[{stage}] action=incomplete-frame-set original={originalSpriteName} candidates={string.Join("|", candidates)} nativeFallback=true");
        return null;
    }

    private static Texture2D? ResolveFrameTexture(string originalSpriteName, string stage)
    {
        if (ResolvedFrameTextureCache.TryGetValue(originalSpriteName, out var cached))
        {
            return cached;
        }

        var fileName = ResolveFrameFileName(originalSpriteName, stage);
        if (fileName == null)
        {
            ResolvedFrameTextureCache[originalSpriteName] = null;
            return null;
        }

        var texture = LoadTexture(fileName);
        ResolvedFrameTextureCache[originalSpriteName] = texture;
        return texture;
    }

    private static Image? FindWarningPanel(Transform root)
    {
        var directPanel = root.Find(PanelObjectName)?.GetComponent<Image>();
        if (directPanel != null)
        {
            return directPanel;
        }

        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (string.Equals(image.name, PanelObjectName, StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return null;
    }

    private static Sprite? ResolveLocalizedSprite(string originalSpriteName, Sprite? originalSprite, Texture2D texture)
    {
        var cacheKey = BuildSpriteCacheKey(originalSpriteName, originalSprite, texture);
        if (SpriteCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var sprite = CreateLocalizedSprite(texture, originalSprite);
        SpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private static string BuildSpriteCacheKey(string originalSpriteName, Sprite? originalSprite, Texture2D texture)
    {
        if (originalSprite == null)
        {
            return $"{originalSpriteName}:{texture.name}:fallback";
        }

        var rect = originalSprite.rect;
        return $"{originalSpriteName}:{texture.name}:{rect.x}:{rect.y}:{rect.width}:{rect.height}:{originalSprite.pivot.x}:{originalSprite.pivot.y}:{originalSprite.pixelsPerUnit}";
    }

    private static Sprite? CreateLocalizedSprite(Texture2D texture, Sprite? template)
    {
        try
        {
            if (template != null)
            {
                var rect = template.rect;
                if (rect.width > 0f &&
                    rect.height > 0f &&
                    rect.xMax <= texture.width &&
                    rect.yMax <= texture.height)
                {
                    var pivot = new Vector2(template.pivot.x / rect.width, template.pivot.y / rect.height);
                    var sprite = Sprite.Create(texture, rect, pivot, template.pixelsPerUnit, 0u, SpriteMeshType.FullRect, template.border);
                    sprite.name = texture.name;
                    return sprite;
                }
            }

            var fallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            fallbackSprite.name = texture.name;
            return fallbackSprite;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"RadiationPlayback[Sprite] action=create-failed texture={texture.name} error={ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static bool IsCachedLocalizedSprite(Sprite sprite)
    {
        foreach (var cachedSprite in SpriteCache.Values)
        {
            if (ReferenceEquals(cachedSprite, sprite))
            {
                return true;
            }
        }

        return false;
    }

    private static Sprite? ReadRawOverrideSprite(Image image)
    {
        if (RawOverrideSpriteField == null)
        {
            return null;
        }

        try
        {
            return RawOverrideSpriteField.GetValue(image) as Sprite;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"RadiationPlayback[Image] action=read-raw-override-failed error={ex.GetType().Name}: {ex.Message}");
            return null;
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

    private static Texture2D? LoadTexture(string fileName)
    {
        if (TextureCache.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(_textureDirectory))
        {
            return null;
        }

        var path = Path.Combine(_textureDirectory, fileName);
        if (!File.Exists(path))
        {
            TextureCache[fileName] = null;
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, data, false))
            {
                UnityEngine.Object.Destroy(texture);
                TextureCache[fileName] = null;
                return null;
            }

            texture.name = Path.GetFileNameWithoutExtension(fileName);
            texture.wrapMode = TextureWrapMode.Clamp;
            TextureCache[fileName] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"RadiationPlayback[Texture] action=load-failed file={fileName} error={ex.GetType().Name}: {ex.Message}");
            TextureCache[fileName] = null;
            return null;
        }
    }

    private static string NormalizeSpriteName(string? spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return string.Empty;
        }

        const string cloneSuffix = "(Clone)";
        var normalized = spriteName.Trim();
        if (normalized.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).TrimEnd();
        }

        return normalized;
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
