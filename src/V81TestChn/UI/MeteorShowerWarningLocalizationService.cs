using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class MeteorShowerWarningLocalizationService
{
    private const string WarningRootName = "MeteorShowerWarning";
    private const string BlackBarsName = "BlackBars";
    private const string HeadlineName = "BodyText (1)";
    private const string BodyName = "BodyText";
    private const string ScrollMaskName = "ScrollTextMask";
    private const string ScrollingTextName = "ScrollingText";
    private const float TextHorizontalPadding = 10f;
    private const float TextVerticalPadding = 4f;
    private const float FitSafetyScale = 0.98f;

    private static HUDManager? _activeHud;
    private static Coroutine? _deferredRepair;

    public static void ResetForHudLifecycle(HUDManager hud)
    {
        StopDeferredRepair();
        _activeHud = hud;
    }

    public static void Apply(HUDManager? hud, string reason)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        StopDeferredRepair();
        _activeHud = hud;
        LocalizeAndFit(hud, reason + ".immediate");
        _deferredRepair = hud.StartCoroutine(RepairAfterAnimatorFrames(hud, reason));
    }

    private static IEnumerator RepairAfterAnimatorFrames(HUDManager hud, string reason)
    {
        for (var frame = 1; frame <= 3; frame++)
        {
            yield return null;
            if (!ReferenceEquals(_activeHud, hud) || hud == null || Plugin.IsRuntimeShuttingDown)
            {
                _deferredRepair = null;
                yield break;
            }

            LocalizeAndFit(hud, reason + ".frame-" + frame);
        }

        _deferredRepair = null;
    }

    private static void LocalizeAndFit(HUDManager hud, string reason)
    {
        var root = ResolveWarningRoot(hud.meteorShowerGraphicAnimator?.transform);
        if (root == null)
        {
            return;
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);
        var headline = FindTmpText(transforms, HeadlineName);
        var body = FindTmpText(transforms, BodyName);
        var scrollingText = FindTmpText(transforms, ScrollingTextName);

        DirectTextLocalizationService.ApplyComposite(headline, reason + ".headline");
        DirectTextLocalizationService.ApplyComposite(body, reason + ".body");
        DirectTextLocalizationService.ApplyComposite(scrollingText, reason + ".scrolling-text");

        FitWarningTextToBlackBars(transforms, headline, body, scrollingText);
    }

    private static Transform? ResolveWarningRoot(Transform? animatorTransform)
    {
        // In the original HUD prefab the Animator is attached to the child named
        // "Image" while MeteorShowerWarning is its parent. Walk a small bounded
        // ancestor chain so compatible prefab wrappers do not disable the layout fix.
        var current = animatorTransform;
        for (var depth = 0; current != null && depth < 4; depth++, current = current.parent)
        {
            if (string.Equals(current.name, WarningRootName, StringComparison.Ordinal))
            {
                return current;
            }
        }

        return null;
    }

    private static void FitWarningTextToBlackBars(Transform[] transforms, TMP_Text? headline, TMP_Text? body, TMP_Text? scrollingText)
    {
        var blackBars = FindTransform(transforms, BlackBarsName);
        if (blackBars == null)
        {
            return;
        }

        var barRects = new List<RectTransform>(4);
        foreach (var rect in blackBars.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == null || ReferenceEquals(rect, blackBars) || rect.parent != blackBars || rect.GetComponent<Image>() == null)
            {
                continue;
            }

            barRects.Add(rect);
        }

        if (barRects.Count == 0)
        {
            return;
        }

        barRects.Sort(CompareVerticalPositionDescending);
        var headlineBar = barRects[0];
        if (headline != null)
        {
            FitTextFontToBars(
                headline,
                new[] { Math.Max(1f, headlineBar.rect.width - TextHorizontalPadding * 2f) },
                Math.Max(1f, headlineBar.rect.height - TextVerticalPadding * 2f));
        }

        if (barRects.Count == 1 || body == null)
        {
            return;
        }

        var bodyBars = barRects.GetRange(1, barRects.Count - 1);
        var smallestBarHeight = float.PositiveInfinity;
        var lineWidths = new List<float>(bodyBars.Count);
        foreach (var bar in bodyBars)
        {
            smallestBarHeight = Math.Min(smallestBarHeight, bar.rect.height);
            lineWidths.Add(Math.Max(1f, bar.rect.width - TextHorizontalPadding * 2f));
        }

        var maxLineHeight = Math.Max(1f, smallestBarHeight - TextVerticalPadding * 2f);
        FitTextFontToBars(body, lineWidths, maxLineHeight);
        MatchBodyLineSpacingToBars(body, bodyBars);

        PositionScrollingTextBetweenGroups(transforms, scrollingText, headlineBar, bodyBars);
    }

    private static void FitTextFontToBars(TMP_Text text, IReadOnlyList<float> perLineWidths, float maxLineHeight)
    {
        if (string.IsNullOrEmpty(text.text) || perLineWidths.Count == 0)
        {
            return;
        }

        var sourceFontSize = text.fontSize;
        if (sourceFontSize <= 0f)
        {
            return;
        }

        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.ForceMeshUpdate(true, true);

        var scale = 1f;
        var largestLineHeight = 0f;
        for (var i = 0; i < text.textInfo.lineCount; i++)
        {
            largestLineHeight = Math.Max(largestLineHeight, text.textInfo.lineInfo[i].lineHeight);
        }

        if (largestLineHeight > 0f && maxLineHeight > 0f)
        {
            scale = Math.Min(scale, maxLineHeight / largestLineHeight);
        }

        var lines = text.text.Replace("\r", string.Empty).Split('\n');
        var count = Math.Min(lines.Length, perLineWidths.Count);
        for (var i = 0; i < count; i++)
        {
            var lineWidth = text.GetPreferredValues(lines[i]).x;
            if (lineWidth > 0f)
            {
                scale = Math.Min(scale, perLineWidths[i] / lineWidth);
            }
        }

        if (scale < 0.999f)
        {
            text.fontSize = Math.Max(1f, sourceFontSize * Math.Max(0.01f, scale * FitSafetyScale));
            text.ForceMeshUpdate(true, true);
        }
    }

    private static void MatchBodyLineSpacingToBars(TMP_Text body, IReadOnlyList<RectTransform> bodyBars)
    {
        var count = Math.Min(body.textInfo.lineCount, bodyBars.Count);
        if (count < 2)
        {
            return;
        }

        var desiredAdvance = 0f;
        for (var i = 1; i < count; i++)
        {
            var previous = body.rectTransform.InverseTransformPoint(bodyBars[i - 1].TransformPoint(bodyBars[i - 1].rect.center));
            var current = body.rectTransform.InverseTransformPoint(bodyBars[i].TransformPoint(bodyBars[i].rect.center));
            desiredAdvance += Math.Abs(previous.y - current.y);
        }

        desiredAdvance /= count - 1;
        var currentAdvance = GetAverageLineAdvance(body, count);
        if (desiredAdvance <= 0f || currentAdvance <= 0f || Math.Abs(desiredAdvance - currentAdvance) < 0.1f)
        {
            return;
        }

        const float probeSpacing = 10f;
        var originalSpacing = body.lineSpacing;
        body.lineSpacing = originalSpacing + probeSpacing;
        body.ForceMeshUpdate(true, true);
        var probedAdvance = GetAverageLineAdvance(body, count);
        var slope = (probedAdvance - currentAdvance) / probeSpacing;
        if (Math.Abs(slope) < 0.0001f)
        {
            body.lineSpacing = originalSpacing;
            body.ForceMeshUpdate(true, true);
            return;
        }

        body.lineSpacing = Mathf.Clamp(originalSpacing + (desiredAdvance - currentAdvance) / slope, -1000f, 1000f);
        body.ForceMeshUpdate(true, true);
    }

    private static float GetAverageLineAdvance(TMP_Text text, int lineCount)
    {
        var total = 0f;
        for (var i = 1; i < lineCount; i++)
        {
            total += Math.Abs(text.textInfo.lineInfo[i - 1].baseline - text.textInfo.lineInfo[i].baseline);
        }

        return total / Math.Max(1, lineCount - 1);
    }

    private static void PositionScrollingTextBetweenGroups(
        Transform[] transforms,
        TMP_Text? scrollingText,
        RectTransform headlineBar,
        IReadOnlyList<RectTransform> bodyBars)
    {
        var mask = FindTransform(transforms, ScrollMaskName) as RectTransform;
        if (mask == null || scrollingText == null || bodyBars.Count == 0)
        {
            return;
        }

        var headlineCenter = headlineBar.TransformPoint(headlineBar.rect.center);
        var headlineBottom = headlineCenter.y - headlineBar.rect.height * Math.Abs(headlineBar.lossyScale.y) * 0.5f;
        var bodyTop = float.NegativeInfinity;
        foreach (var bar in bodyBars)
        {
            var center = bar.TransformPoint(bar.rect.center);
            bodyTop = Math.Max(bodyTop, center.y + bar.rect.height * Math.Abs(bar.lossyScale.y) * 0.5f);
        }

        var targetY = (headlineBottom + bodyTop) * 0.5f;
        var scrollingCenter = scrollingText.rectTransform.TransformPoint(scrollingText.rectTransform.rect.center);
        var maskPosition = mask.position;
        maskPosition.y += targetY - scrollingCenter.y;
        mask.position = maskPosition;
    }

    private static TMP_Text? FindTmpText(Transform[] transforms, string name)
    {
        return FindTransform(transforms, name)?.GetComponent<TMP_Text>();
    }

    private static Transform? FindTransform(Transform[] transforms, string name)
    {
        foreach (var transform in transforms)
        {
            if (transform != null && string.Equals(transform.name, name, StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static int CompareVerticalPositionDescending(RectTransform? left, RectTransform? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return right.anchoredPosition.y.CompareTo(left.anchoredPosition.y);
    }

    private static void StopDeferredRepair()
    {
        if (_deferredRepair != null && _activeHud != null)
        {
            _activeHud.StopCoroutine(_deferredRepair);
        }

        _deferredRepair = null;
    }
}
