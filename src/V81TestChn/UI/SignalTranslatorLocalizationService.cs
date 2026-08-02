using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class SignalTranslatorLocalizationService
{
    private const float SignalTranslatorLocalizationWindowSeconds = 2.0f;
    private const float SignalTranslatorLocalizationInitialRetryIntervalSeconds = 0.05f;
    private const float SignalTranslatorLocalizationMaxRetryIntervalSeconds = 0.4f;
    private const float SignalTranslatorReceivingSignalFontScale = 1.25f;
    private const string SignalTranslatorReceivingSignalEnglish = "RECEIVING SIGNAL";
    private const string SignalTranslatorReceivingSignalChinese = "\u6b63\u5728\u63a5\u6536\u4fe1\u53f7";

    private static float _signalTranslatorLocalizationUntil;
    private static float _nextSignalTranslatorLocalizationTime;
    private static float _signalTranslatorLocalizationRetryInterval;
    private static int _lastLocalizationBeginFrame = -1;
    private static int _signalTranslatorTextCacheRootId;
    private static TMP_Text[] SignalTranslatorTextCache = Array.Empty<TMP_Text>();
    private static readonly Dictionary<int, float> SignalTranslatorReceivingSignalOriginalFontSizes = new();

    public static void Clear()
    {
        _signalTranslatorLocalizationUntil = 0f;
        _nextSignalTranslatorLocalizationTime = 0f;
        _signalTranslatorLocalizationRetryInterval = 0f;
        _lastLocalizationBeginFrame = -1;
        ClearCaches();
    }

    public static void ClearCaches()
    {
        _signalTranslatorTextCacheRootId = 0;
        SignalTranslatorTextCache = Array.Empty<TMP_Text>();
        SignalTranslatorReceivingSignalOriginalFontSizes.Clear();
    }

    public static void BeginLocalizationWindow(HUDManager? hud, string reason, bool applyImmediately = true)
    {
        if (hud == null)
        {
            return;
        }

        _signalTranslatorLocalizationUntil = Math.Max(
            _signalTranslatorLocalizationUntil,
            Time.unscaledTime + SignalTranslatorLocalizationWindowSeconds);
        if (!applyImmediately)
        {
            return;
        }

        if (_lastLocalizationBeginFrame == Time.frameCount)
        {
            return;
        }

        _lastLocalizationBeginFrame = Time.frameCount;
        _signalTranslatorLocalizationRetryInterval = SignalTranslatorLocalizationInitialRetryIntervalSeconds;
        ApplyHudLocalization(hud, reason);
        _nextSignalTranslatorLocalizationTime = Time.unscaledTime + _signalTranslatorLocalizationRetryInterval;
    }

    public static bool ShouldRetryLocalization()
    {
        if (_signalTranslatorLocalizationUntil <= 0f)
        {
            return false;
        }

        var now = Time.unscaledTime;
        if (now > _signalTranslatorLocalizationUntil)
        {
            _signalTranslatorLocalizationUntil = 0f;
            return false;
        }

        if (now < _nextSignalTranslatorLocalizationTime)
        {
            return false;
        }

        _nextSignalTranslatorLocalizationTime = now + Math.Max(
            SignalTranslatorLocalizationInitialRetryIntervalSeconds,
            _signalTranslatorLocalizationRetryInterval);
        _signalTranslatorLocalizationRetryInterval = Math.Min(
            SignalTranslatorLocalizationMaxRetryIntervalSeconds,
            Math.Max(SignalTranslatorLocalizationInitialRetryIntervalSeconds, _signalTranslatorLocalizationRetryInterval * 2f));
        return true;
    }

    public static void EndLocalizationWindow()
    {
        _signalTranslatorLocalizationUntil = 0f;
    }

    public static void ApplyHudLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        var translated = 0;
        var seen = 0;
        TranslateSignalTranslatorText(hud.signalTranslatorText, ref translated, ref seen);

        var root = hud.signalTranslatorAnimator == null ? null : hud.signalTranslatorAnimator.gameObject;
        if (root != null)
        {
            foreach (var text in GetSignalTranslatorTexts(root))
            {
                if (ReferenceEquals(text, hud.signalTranslatorText))
                {
                    continue;
                }

                TranslateSignalTranslatorText(text, ref translated, ref seen);
            }
        }

        if (translated > 0)
        {
            Plugin.ReportTranslationHit();
            // Keep the reason parameter for future focused diagnostics without startup log noise.
        }
    }

    public static bool IsPlayerMessageText(TMP_Text? text)
    {
        return text != null && ReferenceEquals(text, HUDManager.Instance?.signalTranslatorText);
    }

    public static void PreservePlayerMessageText(TMP_Text? text, string? value)
    {
        if (text == null)
        {
            return;
        }

        // Signal Translator payloads are player-authored content. Keep every
        // character unchanged; only attach a fallback when the player actually
        // used Chinese or another supported East Asian glyph.
        ChatEmojiSpriteService.ApplyToText(text);
        FontFallbackService.ApplyFallback(text, value);
    }

    private static TMP_Text[] GetSignalTranslatorTexts(GameObject root)
    {
        var rootId = root.GetInstanceID();
        if (rootId == _signalTranslatorTextCacheRootId)
        {
            return SignalTranslatorTextCache;
        }

        _signalTranslatorTextCacheRootId = rootId;
        SignalTranslatorTextCache = root.GetComponentsInChildren<TMP_Text>(true);
        return SignalTranslatorTextCache;
    }

    private static void TranslateSignalTranslatorText(TMP_Text? text, ref int translated, ref int seen)
    {
        if (text == null)
        {
            return;
        }

        seen++;
        var original = text.text;
        if (IsPlayerMessageText(text))
        {
            PreservePlayerMessageText(text, original);
            return;
        }

        var isReceivingSignal = IsSignalTranslatorReceivingSignalText(original);
        if (!TranslationService.TryTranslate(original, out var value) ||
            string.Equals(original, value, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            ApplySignalTranslatorReceivingSignalFontSize(text, original, isReceivingSignal);
            return;
        }

        text.text = value;
        FontFallbackService.ApplyFallback(text, value);
        ApplySignalTranslatorReceivingSignalFontSize(text, value, isReceivingSignal || IsSignalTranslatorReceivingSignalText(value));
        translated++;
    }

    private static void ApplySignalTranslatorReceivingSignalFontSize(TMP_Text text, string value, bool isReceivingSignal)
    {
        var id = text.GetInstanceID();
        if (isReceivingSignal || IsSignalTranslatorReceivingSignalText(value))
        {
            if (!SignalTranslatorReceivingSignalOriginalFontSizes.TryGetValue(id, out var originalSize))
            {
                originalSize = text.fontSize;
                SignalTranslatorReceivingSignalOriginalFontSizes[id] = originalSize;
            }

            var targetSize = originalSize * SignalTranslatorReceivingSignalFontScale;
            if (text.fontSize < targetSize)
            {
                text.fontSize = targetSize;
            }

            return;
        }

        if (SignalTranslatorReceivingSignalOriginalFontSizes.TryGetValue(id, out var storedSize))
        {
            text.fontSize = storedSize;
            SignalTranslatorReceivingSignalOriginalFontSizes.Remove(id);
        }
    }

    private static bool IsSignalTranslatorReceivingSignalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return EqualsNormalizedWhitespace(value, SignalTranslatorReceivingSignalEnglish, ignoreCase: true) ||
               EqualsNormalizedWhitespace(value, SignalTranslatorReceivingSignalChinese, ignoreCase: false);
    }

    private static bool EqualsNormalizedWhitespace(string value, string expected, bool ignoreCase)
    {
        var valueIndex = 0;
        var expectedIndex = 0;
        while (valueIndex < value.Length && char.IsWhiteSpace(value[valueIndex]))
        {
            valueIndex++;
        }

        while (valueIndex < value.Length)
        {
            if (char.IsWhiteSpace(value[valueIndex]))
            {
                while (valueIndex < value.Length && char.IsWhiteSpace(value[valueIndex]))
                {
                    valueIndex++;
                }

                if (valueIndex >= value.Length)
                {
                    break;
                }

                if (expectedIndex >= expected.Length || expected[expectedIndex++] != ' ')
                {
                    return false;
                }

                continue;
            }

            if (expectedIndex >= expected.Length ||
                !CharsEqual(value[valueIndex], expected[expectedIndex], ignoreCase))
            {
                return false;
            }

            valueIndex++;
            expectedIndex++;
        }

        return expectedIndex == expected.Length;
    }

    private static bool CharsEqual(char left, char right, bool ignoreCase)
    {
        return left == right ||
               (ignoreCase && char.ToUpperInvariant(left) == char.ToUpperInvariant(right));
    }
}
