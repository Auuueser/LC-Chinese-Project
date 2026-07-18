using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class SignalTranslatorLocalizationService
{
    private const float SignalTranslatorLocalizationWindowSeconds = 2.0f;
    private const float SignalTranslatorLocalizationRetryIntervalSeconds = 0.05f;
    private const float SignalTranslatorReceivingSignalFontScale = 1.25f;
    private const string SignalTranslatorReceivingSignalEnglish = "RECEIVING SIGNAL";
    private const string SignalTranslatorReceivingSignalChinese = "\u6b63\u5728\u63a5\u6536\u4fe1\u53f7";

    private static float _signalTranslatorLocalizationUntil;
    private static float _nextSignalTranslatorLocalizationTime;
    private static int _signalTranslatorTextCacheRootId;
    private static TMP_Text[] SignalTranslatorTextCache = Array.Empty<TMP_Text>();
    private static readonly Dictionary<int, float> SignalTranslatorReceivingSignalOriginalFontSizes = new();

    public static void Clear()
    {
        _signalTranslatorLocalizationUntil = 0f;
        _nextSignalTranslatorLocalizationTime = 0f;
        ClearCaches();
    }

    public static void ClearCaches()
    {
        _signalTranslatorTextCacheRootId = 0;
        SignalTranslatorTextCache = Array.Empty<TMP_Text>();
        SignalTranslatorReceivingSignalOriginalFontSizes.Clear();
    }

    public static void BeginLocalizationWindow(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        _signalTranslatorLocalizationUntil = Math.Max(
            _signalTranslatorLocalizationUntil,
            Time.unscaledTime + SignalTranslatorLocalizationWindowSeconds);
        ApplyHudLocalization(hud, reason);
        _nextSignalTranslatorLocalizationTime = Time.unscaledTime + SignalTranslatorLocalizationRetryIntervalSeconds;
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

        _nextSignalTranslatorLocalizationTime = now + SignalTranslatorLocalizationRetryIntervalSeconds;
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

        var normalized = NormalizeSignalTranslatorText(value);
        return string.Equals(normalized, SignalTranslatorReceivingSignalEnglish, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, SignalTranslatorReceivingSignalChinese, StringComparison.Ordinal);
    }

    private static string NormalizeSignalTranslatorText(string value)
    {
        var builder = new StringBuilder(value.Length);
        var sawWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!sawWhitespace)
                {
                    builder.Append(' ');
                    sawWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            sawWhitespace = false;
        }

        return builder.ToString().Trim();
    }
}
