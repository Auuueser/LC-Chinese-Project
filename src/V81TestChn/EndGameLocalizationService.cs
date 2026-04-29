using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class EndGameLocalizationService
{
    private const string DeadLocalizedText = "\uff08\u6b7b\u4ea1\uff09";
    private const string DeceasedLocalizedText = "\u6b7b\u4ea1";
    private const string MissingLocalizedText = "\u5931\u8e2a";
    private const string TextureSubfolder = "textures";
    private const string EndgameAllPlayersDeadTextureFile = "EndgameAllPlayersDeadOverlay.png";
    private const string EndgameStatsBoxesTextureFile = "EndgameStatsBoxesLocalized.png";
    private const string EndgameStatsDeceasedTextureFile = "EndgameStatsDeceased.png";
    private const string EndgameStatsMissingTextureFile = "EndgameStatsMissing.png";
    private static string? _textureDirectory;
    private static readonly Dictionary<string, Sprite?> SpriteCache = new(StringComparer.OrdinalIgnoreCase);

    public static void Initialize(string pluginDir)
    {
        _textureDirectory = Path.Combine(pluginDir, TextureSubfolder);
    }

    public static void ApplyHudEndGameLocalization(HUDManager? hudManager, string stage)
    {
        if (hudManager?.statsUIElements == null)
        {
            return;
        }

        CleanupLegacyEndGameOverlays(hudManager.statsUIElements);
        LocalizeRuntimeDeadTexts(stage);
        LocalizePlayerStateBadges(hudManager.statsUIElements, stage);
        LocalizeAllPlayersDeadOverlayImage(hudManager.statsUIElements, stage);
        LocalizeStatsBoxesImage(hudManager.statsUIElements, stage);
    }

    public static void ApplySpectateUiLocalization(HUDManager? hudManager, string stage)
    {
        if (hudManager == null)
        {
            return;
        }

        LocalizeSpectateDeadTexts(stage);
    }

    public static void ApplyChallengeSlotLocalization(ChallengeLeaderboardSlot? slot, string stage)
    {
        if (slot?.scrapCollectedText == null)
        {
            return;
        }

        var trimmed = slot.scrapCollectedText.text?.Trim();
        if (string.Equals(trimmed, "Deceased", StringComparison.OrdinalIgnoreCase))
        {
            slot.scrapCollectedText.text = DeceasedLocalizedText;
            FontFallbackService.ApplyFallback(slot.scrapCollectedText, DeceasedLocalizedText);
            Plugin.Log.LogInfo($"NativeRelay[{stage}] target=ChallengeLeaderboard action=applied value={slot.scrapCollectedText.text}");
        }
    }

    private static void LocalizeRuntimeDeadTexts(string stage)
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            var trimmed = text.text.Trim();
            if (string.Equals(trimmed, "(Dead)", StringComparison.Ordinal))
            {
                ApplyLocalizedText(text, DeadLocalizedText, stage, "EndGameDeadLabel");
            }
            else if (string.Equals(trimmed, "Deceased", StringComparison.OrdinalIgnoreCase))
            {
                ApplyLocalizedText(text, DeceasedLocalizedText, stage, "EndGameDeceasedLabel");
            }
        }
    }

    public static void TryRewriteSpectateDeadValue(TMP_Text? text, ref string value, string stage)
    {
        if (text == null || string.IsNullOrWhiteSpace(value) || !IsSpectateDeadLabel(text.transform))
        {
            return;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "(Dead)", StringComparison.Ordinal))
        {
            value = DeadLocalizedText;
            Plugin.Log.LogInfo($"NativeRelay[{stage}] target=SpectateDeadLabel action=rewrite path={BuildPath(text.transform)} value={value}");
        }
        else if (string.Equals(trimmed, "Deceased", StringComparison.OrdinalIgnoreCase))
        {
            value = DeceasedLocalizedText;
            Plugin.Log.LogInfo($"NativeRelay[{stage}] target=SpectateDeceasedLabel action=rewrite path={BuildPath(text.transform)} value={value}");
        }
    }

    public static void TryLocalizeSpectateDeadLabel(TMP_Text? text, string stage)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text) || !IsSpectateDeadLabel(text.transform))
        {
            return;
        }

        var trimmed = text.text.Trim();
        if (string.Equals(trimmed, "(Dead)", StringComparison.Ordinal))
        {
            ApplyLocalizedText(text, DeadLocalizedText, stage, "SpectateDeadLabel");
        }
        else if (string.Equals(trimmed, "Deceased", StringComparison.OrdinalIgnoreCase))
        {
            ApplyLocalizedText(text, DeceasedLocalizedText, stage, "SpectateDeceasedLabel");
        }
    }

    private static void LocalizeSpectateDeadTexts(string stage)
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            TryLocalizeSpectateDeadLabel(text, stage);
        }
    }

    private static void LocalizePlayerStateBadges(EndOfGameStatUIElements elements, string stage)
    {
        var template = PickTemplate(elements);
        if (elements.playerStates == null)
        {
            return;
        }

        foreach (var stateImage in elements.playerStates)
        {
            if (stateImage == null)
            {
                continue;
            }

            if (!stateImage.enabled || stateImage.sprite == null)
            {
                SetOverlayActive(stateImage.transform, "__V81_EndgameStateOverlay", false);
                continue;
            }

            if (ReferenceEquals(stateImage.sprite, elements.deceasedIcon))
            {
                if (TryApplyLocalizedStateSprite(stateImage, EndgameStatsDeceasedTextureFile, stage, "deceased"))
                {
                    continue;
                }

                stateImage.enabled = false;
                var label = EnsureOverlayLabel(
                    stateImage.transform,
                    "__V81_EndgameStateOverlay",
                    template,
                    new Vector2(220f, 90f),
                    Vector2.zero,
                    DeceasedLocalizedText,
                    24f,
                    new Color(1f, 0.22f, 0.22f, 1f),
                    TextAlignmentOptions.Center);
                label.gameObject.SetActive(true);
                Plugin.Log.LogInfo($"NativeRelay[{stage}] target=EndGameState action=fallback-label state=deceased path={BuildPath(stateImage.transform)}");
                continue;
            }

            if (ReferenceEquals(stateImage.sprite, elements.missingIcon))
            {
                if (TryApplyLocalizedStateSprite(stateImage, EndgameStatsMissingTextureFile, stage, "missing"))
                {
                    continue;
                }

                stateImage.enabled = false;
                var label = EnsureOverlayLabel(
                    stateImage.transform,
                    "__V81_EndgameStateOverlay",
                    template,
                    new Vector2(220f, 90f),
                    Vector2.zero,
                    MissingLocalizedText,
                    24f,
                    new Color(1f, 0.22f, 0.22f, 1f),
                    TextAlignmentOptions.Center);
                label.gameObject.SetActive(true);
                Plugin.Log.LogInfo($"NativeRelay[{stage}] target=EndGameState action=fallback-label state=missing path={BuildPath(stateImage.transform)}");
                continue;
            }

            SetOverlayActive(stateImage.transform, "__V81_EndgameStateOverlay", false);
        }
    }

    private static void LocalizeAllPlayersDeadOverlayImage(EndOfGameStatUIElements elements, string stage)
    {
        var overlay = elements.allPlayersDeadOverlay;
        if (overlay == null)
        {
            return;
        }

        if (!overlay.enabled)
        {
            DestroyChildIfExists(overlay.transform, "__V81_EndgameNoSurvivorsOverlay");
            DestroyChildIfExists(overlay.transform, "__V81_EndgameAllPlayersDeadTexture");
            return;
        }

        DestroyChildIfExists(overlay.transform, "__V81_EndgameNoSurvivorsOverlay");
        DestroyChildIfExists(overlay.transform, "__V81_EndgameAllScrapLostOverlay");
        DestroyChildIfExists(overlay.transform, "__V81_EndgameAllPlayersDeadTexture");

        var originalSprite = overlay.sprite;
        var originalType = overlay.type;
        var originalPreserveAspect = overlay.preserveAspect;
        var sprite = LoadSprite(EndgameAllPlayersDeadTextureFile, originalSprite);
        if (sprite == null)
        {
            Plugin.Log.LogWarning($"NativeRelay[{stage}] target=EndGameOverlay action=texture-missing file={EndgameAllPlayersDeadTextureFile}");
            return;
        }

        overlay.sprite = sprite;
        overlay.preserveAspect = originalPreserveAspect;
        overlay.type = originalType;
        overlay.color = Color.white;

        Plugin.Log.LogInfo(
            $"NativeRelay[{stage}] target=EndGameOverlay action=applied path={BuildPath(overlay.transform)} " +
            $"type={overlay.type} preserveAspect={overlay.preserveAspect} imageRect={DescribeRect(overlay.rectTransform.rect)} " +
            $"templateSprite={DescribeSprite(originalSprite)} localizedSprite={DescribeSprite(sprite)}");
    }

    private static void LocalizeStatsBoxesImage(EndOfGameStatUIElements elements, string stage)
    {
        var image = FindStatsBoxesImage(elements);
        if (image == null)
        {
            return;
        }

        DestroyChildIfExists(image.transform, "__V81_EndgameGradeLabel");
        DestroyChildIfExists(image.transform, "__V81_EndgameGradeTexture");
        DestroyChildIfExists(image.transform, "__V81_EndgameCollectedTexture");

        var originalSprite = image.sprite;
        var originalType = image.type;
        var originalPreserveAspect = image.preserveAspect;
        var sprite = LoadSprite(EndgameStatsBoxesTextureFile, originalSprite);
        if (sprite == null)
        {
            Plugin.Log.LogWarning($"NativeRelay[{stage}] target=EndGameStatsBoxes action=texture-missing file={EndgameStatsBoxesTextureFile}");
            return;
        }

        image.sprite = sprite;
        image.preserveAspect = originalPreserveAspect;
        image.type = originalType;
        image.color = Color.white;
        Plugin.Log.LogInfo(
            $"NativeRelay[{stage}] target=EndGameStatsBoxes action=applied path={BuildPath(image.transform)} " +
            $"type={image.type} preserveAspect={image.preserveAspect} imageRect={DescribeRect(image.rectTransform.rect)} " +
            $"templateSprite={DescribeSprite(originalSprite)} localizedSprite={DescribeSprite(sprite)}");
    }

    private static TMP_Text? PickTemplate(EndOfGameStatUIElements elements)
    {
        return elements.playerNamesText?.FirstOrDefault(text => text != null) ?? elements.gradeLetter;
    }

    private static Image? FindStatsBoxesImage(EndOfGameStatUIElements elements)
    {
        Image? bestCandidate = null;
        var bestScore = int.MinValue;
        ConsiderCandidate(ref bestCandidate, ref bestScore, FindBestImageOnAncestorChain(elements.quotaNumerator?.transform, elements), elements);
        ConsiderCandidate(ref bestCandidate, ref bestScore, FindBestImageOnAncestorChain(elements.gradeLetter?.transform, elements), elements);

        var root = elements.gradeLetter?.canvas?.transform ?? elements.quotaNumerator?.canvas?.transform;
        if (root != null)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image == null || ReferenceEquals(image, elements.allPlayersDeadOverlay))
                {
                    continue;
                }

                var spriteName = image.sprite?.name;
                if (!string.IsNullOrWhiteSpace(spriteName) &&
                    spriteName.IndexOf("endgameStatsBoxes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return image;
                }

                ConsiderCandidate(ref bestCandidate, ref bestScore, image, elements);
            }

            if (bestCandidate != null)
            {
                Plugin.Log.LogInfo($"NativeRelay[EndGameStatsBoxes] action=fallback-candidate path={BuildPath(bestCandidate.transform)} score={bestScore} sprite={bestCandidate.sprite?.name ?? "<null>"}");
                return bestCandidate;
            }
        }

        return elements.quotaNumerator?.transform.parent?.GetComponent<Image>()
            ?? elements.gradeLetter?.transform.parent?.GetComponent<Image>();
    }

    private static Image? FindBestImageOnAncestorChain(Transform? start, EndOfGameStatUIElements elements)
    {
        var current = start;
        Image? bestCandidate = null;
        var bestScore = int.MinValue;
        while (current != null)
        {
            var image = current.GetComponent<Image>();
            if (image != null &&
                !ReferenceEquals(image, elements.allPlayersDeadOverlay) &&
                image.sprite != null)
            {
                var score = ScoreStatsBoxesCandidate(image, elements);
                Plugin.Log.LogInfo($"NativeRelay[EndGameStatsBoxes] action=ancestor-candidate path={BuildPath(image.transform)} sprite={image.sprite.name} score={score}");
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = image;
                }
            }

            current = current.parent;
        }

        return bestCandidate;
    }

    private static void ConsiderCandidate(ref Image? bestCandidate, ref int bestScore, Image? image, EndOfGameStatUIElements elements)
    {
        if (image == null)
        {
            return;
        }

        var score = ScoreStatsBoxesCandidate(image, elements);
        if (score > bestScore)
        {
            bestScore = score;
            bestCandidate = image;
        }
    }

    private static int ScoreStatsBoxesCandidate(Image image, EndOfGameStatUIElements elements)
    {
        var score = 0;
        var spriteName = image.sprite?.name ?? string.Empty;
        var path = BuildPath(image.transform);
        var rect = image.rectTransform.rect;

        if (spriteName.IndexOf("statsboxes", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 1000;
        }

        if (spriteName.IndexOf("endgamestats", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 120;
        }

        if (spriteName.IndexOf("statsbg", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score -= 120;
        }

        if (path.IndexOf("/EndgameStats/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 40;
        }

        if (path.IndexOf("/Text/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            score += 60;
        }

        if (rect.width >= 400f && rect.height >= 180f)
        {
            score += 25;
        }

        if (rect.width >= 700f && rect.height >= 500f)
        {
            score += 25;
        }

        if (image.transform == elements.quotaNumerator?.transform.parent)
        {
            score += 180;
        }

        if (image.transform == elements.gradeLetter?.transform.parent)
        {
            score += 180;
        }

        if (elements.quotaNumerator != null && image.rectTransform.rect.width > elements.quotaNumerator.rectTransform.rect.width * 2f)
        {
            score += 70;
        }

        if (elements.gradeLetter != null && image.rectTransform.rect.height > elements.gradeLetter.rectTransform.rect.height * 2f)
        {
            score += 40;
        }

        return score;
    }

    private static void CleanupLegacyEndGameOverlays(EndOfGameStatUIElements elements)
    {
        var root = elements.gradeLetter?.canvas?.transform ?? elements.quotaNumerator?.canvas?.transform;
        if (root == null)
        {
            return;
        }

        var legacyNames = new[]
        {
            "__V81_EndgameNoSurvivorsOverlay",
            "__V81_EndgameAllScrapLostOverlay",
            "__V81_EndgameAllPlayersDeadTexture",
            "__V81_EndgameGradeLabel",
            "__V81_EndgameGradeTexture",
            "__V81_EndgameCollectedTexture"
        };

        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == null)
            {
                continue;
            }

            if (legacyNames.Contains(transform.name, StringComparer.Ordinal))
            {
                UnityEngine.Object.Destroy(transform.gameObject);
            }
        }
    }

    private static TextMeshProUGUI EnsureOverlayLabel(
        Transform parent,
        string objectName,
        TMP_Text? template,
        Vector2 sizeDelta,
        Vector2 anchoredPosition,
        string localizedText,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        var existing = parent.Find(objectName);
        TextMeshProUGUI label;
        if (existing == null)
        {
            var overlayObject = new GameObject(objectName, typeof(RectTransform));
            overlayObject.transform.SetParent(parent, false);
            label = overlayObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
        }
        else
        {
            label = existing.GetComponent<TextMeshProUGUI>() ?? existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        var rectTransform = label.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = anchoredPosition;

        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.characterSpacing = template.characterSpacing;
            label.wordSpacing = template.wordSpacing;
            label.lineSpacing = template.lineSpacing;
        }

        label.fontSize = fontSize;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.color = color;
        label.text = localizedText;
        FontFallbackService.ApplyFallback(label, localizedText);
        return label;
    }

    private static void SetOverlayActive(Transform parent, string objectName, bool active)
    {
        var child = parent.Find(objectName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }

    private static void DestroyChildIfExists(Transform parent, string objectName)
    {
        var child = parent.Find(objectName);
        if (child != null)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void ApplyLocalizedText(TMP_Text text, string localized, string stage, string target)
    {
        if (!string.Equals(text.text, localized, StringComparison.Ordinal))
        {
            text.text = localized;
        }

        FontFallbackService.ApplyFallback(text, localized);
        Plugin.Log.LogInfo($"NativeRelay[{stage}] target={target} action=applied path={BuildPath(text.transform)} text={text.text}");
    }

    private static bool TryApplyLocalizedStateSprite(Image stateImage, string textureFileName, string stage, string state)
    {
        var originalSprite = stateImage.sprite;
        var originalType = stateImage.type;
        var originalPreserveAspect = stateImage.preserveAspect;
        var sprite = LoadSprite(textureFileName, originalSprite);
        if (sprite == null)
        {
            Plugin.Log.LogWarning($"NativeRelay[{stage}] target=EndGameState action=texture-missing state={state} file={textureFileName}");
            return false;
        }

        stateImage.enabled = true;
        stateImage.sprite = sprite;
        stateImage.type = originalType;
        stateImage.preserveAspect = originalPreserveAspect;
        stateImage.color = Color.white;
        SetOverlayActive(stateImage.transform, "__V81_EndgameStateOverlay", false);
        Plugin.Log.LogInfo(
            $"NativeRelay[{stage}] target=EndGameState action=applied-sprite state={state} path={BuildPath(stateImage.transform)} " +
            $"type={stateImage.type} preserveAspect={stateImage.preserveAspect} imageRect={DescribeRect(stateImage.rectTransform.rect)} " +
            $"templateSprite={DescribeSprite(originalSprite)} localizedSprite={DescribeSprite(sprite)}");
        return true;
    }

    private static Sprite? LoadSprite(string fileName, Sprite? templateSprite = null)
    {
        var cacheKey = BuildSpriteCacheKey(fileName, templateSprite);
        if (SpriteCache.TryGetValue(cacheKey, out var cached))
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
            SpriteCache[cacheKey] = null;
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, data, false))
            {
                UnityEngine.Object.Destroy(texture);
                SpriteCache[cacheKey] = null;
                return null;
            }

            texture.name = fileName;
            texture.wrapMode = TextureWrapMode.Clamp;
            var spriteSize = ResolveSpriteRectSize(templateSprite, texture.width, texture.height);
            var spriteRect = new Rect(0f, 0f, spriteSize.x, spriteSize.y);
            var pivot = ResolveSpritePivot(templateSprite, spriteRect);
            var border = ResolveSpriteBorder(templateSprite, spriteRect);
            var pixelsPerUnit = templateSprite?.pixelsPerUnit ?? 100f;
            var sprite = Sprite.Create(texture, spriteRect, pivot, pixelsPerUnit, 0u, SpriteMeshType.FullRect, border);
            sprite.name = Path.GetFileNameWithoutExtension(fileName);
            SpriteCache[cacheKey] = sprite;
            return sprite;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"NativeRelay[EndGameTexture] action=load-failed file={fileName} error={ex.GetType().Name}: {ex.Message}");
            SpriteCache[cacheKey] = null;
            return null;
        }
    }

    private static string BuildSpriteCacheKey(string fileName, Sprite? templateSprite)
    {
        if (templateSprite == null)
        {
            return fileName;
        }

        var rect = templateSprite.rect;
        var pivot = templateSprite.pivot;
        var border = templateSprite.border;
        return $"{fileName}|rect={Mathf.RoundToInt(rect.width)}x{Mathf.RoundToInt(rect.height)}|pivot={Mathf.RoundToInt(pivot.x)},{Mathf.RoundToInt(pivot.y)}|ppu={Mathf.RoundToInt(templateSprite.pixelsPerUnit * 1000f)}|border={Mathf.RoundToInt(border.x)},{Mathf.RoundToInt(border.y)},{Mathf.RoundToInt(border.z)},{Mathf.RoundToInt(border.w)}";
    }

    private static Vector2Int ResolveSpriteRectSize(Sprite? templateSprite, int textureWidth, int textureHeight)
    {
        if (templateSprite == null)
        {
            return new Vector2Int(textureWidth, textureHeight);
        }

        var rect = templateSprite.rect;
        var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, textureWidth);
        var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, textureHeight);
        return new Vector2Int(width, height);
    }

    private static Vector2 ResolveSpritePivot(Sprite? templateSprite, Rect spriteRect)
    {
        if (templateSprite == null || spriteRect.width <= 0f || spriteRect.height <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        return new Vector2(
            Mathf.Clamp01(templateSprite.pivot.x / Mathf.Max(templateSprite.rect.width, 1f)),
            Mathf.Clamp01(templateSprite.pivot.y / Mathf.Max(templateSprite.rect.height, 1f)));
    }

    private static Vector4 ResolveSpriteBorder(Sprite? templateSprite, Rect spriteRect)
    {
        if (templateSprite == null)
        {
            return Vector4.zero;
        }

        var border = templateSprite.border;
        return new Vector4(
            Mathf.Clamp(border.x, 0f, spriteRect.width),
            Mathf.Clamp(border.y, 0f, spriteRect.height),
            Mathf.Clamp(border.z, 0f, spriteRect.width),
            Mathf.Clamp(border.w, 0f, spriteRect.height));
    }

    private static string DescribeSprite(Sprite? sprite)
    {
        if (sprite == null)
        {
            return "<null>";
        }

        return $"{sprite.name}|rect={DescribeRect(sprite.rect)}|pivot={DescribeVector2(sprite.pivot)}|ppu={sprite.pixelsPerUnit:0.###}|border={DescribeVector4(sprite.border)}";
    }

    private static string DescribeRect(Rect rect)
    {
        return $"{rect.x:0.###},{rect.y:0.###},{rect.width:0.###},{rect.height:0.###}";
    }

    private static string DescribeVector2(Vector2 vector)
    {
        return $"{vector.x:0.###},{vector.y:0.###}";
    }

    private static string DescribeVector4(Vector4 vector)
    {
        return $"{vector.x:0.###},{vector.y:0.###},{vector.z:0.###},{vector.w:0.###}";
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

    private static bool IsSpectateDeadLabel(Transform? transform)
    {
        if (transform == null)
        {
            return false;
        }

        var path = BuildPath(transform);
        if (path.IndexOf("DeathScreen/SpectateUI/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return transform.name.IndexOf("DeadOrAlive", StringComparison.OrdinalIgnoreCase) >= 0
            || path.EndsWith("/DeadOrAlive", StringComparison.OrdinalIgnoreCase);
    }
}
