using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class ExternalEnglishCompatibilityUiService
{
    private const int ComponentCacheLimit = 16384;
    private static readonly List<TMP_Text> TmpTextBuffer = new(128);
    private static readonly List<Text> UiTextBuffer = new(128);
    private static readonly List<TextMesh> TextMeshBuffer = new(32);
    private static readonly Dictionary<int, CachedProtectedInput> TmpProtectedInputCache = new(ComponentCacheLimit);
    private static readonly Dictionary<int, CachedProtectedInput> UiProtectedInputCache = new(ComponentCacheLimit);
    private static readonly Dictionary<int, CachedComponentTranslation> TmpTranslationCache = new(ComponentCacheLimit);
    private static readonly Dictionary<int, CachedComponentTranslation> UiTranslationCache = new(ComponentCacheLimit);
    private static readonly Dictionary<int, CachedComponentTranslation> TextMeshTranslationCache = new(ComponentCacheLimit);
    private static readonly HashSet<int> RepairedTmpInputPlaceholderCache = new(ComponentCacheLimit);

    private readonly struct CachedProtectedInput
    {
        public CachedProtectedInput(int parentId, bool value)
        {
            ParentId = parentId;
            Value = value;
        }

        public int ParentId { get; }
        public bool Value { get; }
    }

    private readonly struct CachedComponentTranslation
    {
        public CachedComponentTranslation(string source, string? translated)
        {
            Source = source;
            Translated = translated;
        }

        public string Source { get; }
        public string? Translated { get; }
    }

    public static void ClearRuntimeCaches()
    {
        TmpTextBuffer.Clear();
        UiTextBuffer.Clear();
        TextMeshBuffer.Clear();
        TmpProtectedInputCache.Clear();
        UiProtectedInputCache.Clear();
        TmpTranslationCache.Clear();
        UiTranslationCache.Clear();
        TextMeshTranslationCache.Clear();
        RepairedTmpInputPlaceholderCache.Clear();
    }

    public static int TranslateRoot(GameObject? root, bool includeInactive, string reason)
    {
        if (root == null)
        {
            return 0;
        }

        var translated = 0;
        try
        {
            root.GetComponentsInChildren(includeInactive, TmpTextBuffer);
            foreach (var text in TmpTextBuffer)
            {
                if (TranslateTmpText(text, reason))
                {
                    translated++;
                }
            }

            root.GetComponentsInChildren(includeInactive, UiTextBuffer);
            foreach (var text in UiTextBuffer)
            {
                if (TranslateUiText(text))
                {
                    translated++;
                }
            }

            root.GetComponentsInChildren(includeInactive, TextMeshBuffer);
            foreach (var text in TextMeshBuffer)
            {
                if (TranslateTextMesh(text))
                {
                    translated++;
                }
            }
        }
        finally
        {
            TmpTextBuffer.Clear();
            UiTextBuffer.Clear();
            TextMeshBuffer.Clear();
        }

        return translated;
    }

    public static bool TranslateTmpText(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var source = text.text;
        if (!MightNeedExternalTextTranslation(source) || IsProtectedInputTextComponent(text))
        {
            return false;
        }

        if (TryGetCachedTranslation(text, TmpTranslationCache, source, out var translated))
        {
            return ApplyTranslatedTmpText(text, source, translated, reason);
        }

        if (!ExternalEnglishCompatibilityService.TryTranslateFast(source, out translated) ||
            string.Equals(source, translated, StringComparison.Ordinal))
        {
            CacheTranslation(text, TmpTranslationCache, source, null);
            return false;
        }

        CacheTranslation(text, TmpTranslationCache, source, translated);
        return ApplyTranslatedTmpText(text, source, translated, reason);
    }

    public static bool TranslateTmpTextKnownNonInput(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var source = text.text;
        if (!MightNeedExternalTextTranslation(source))
        {
            return false;
        }

        if (TryGetCachedTranslation(text, TmpTranslationCache, source, out var translated))
        {
            return ApplyTranslatedTmpText(text, source, translated, reason);
        }

        if (!ExternalEnglishCompatibilityService.TryTranslateFast(source, out translated) ||
            string.Equals(source, translated, StringComparison.Ordinal))
        {
            CacheTranslation(text, TmpTranslationCache, source, null);
            return false;
        }

        CacheTranslation(text, TmpTranslationCache, source, translated);
        return ApplyTranslatedTmpText(text, source, translated, reason);
    }

    private static bool TranslateUiText(Text? text)
    {
        if (text == null)
        {
            return false;
        }

        var source = text.text;
        if (!MightNeedExternalTextTranslation(source) || IsProtectedInputTextComponent(text))
        {
            return false;
        }

        if (TryGetCachedTranslation(text, UiTranslationCache, source, out var translated))
        {
            return ApplyTranslatedUiText(text, source, translated);
        }

        if (!ExternalEnglishCompatibilityService.TryTranslateFast(source, out translated) ||
            string.Equals(source, translated, StringComparison.Ordinal))
        {
            CacheTranslation(text, UiTranslationCache, source, null);
            return false;
        }

        CacheTranslation(text, UiTranslationCache, source, translated);
        return ApplyTranslatedUiText(text, source, translated);
    }

    private static bool TranslateTextMesh(TextMesh? text)
    {
        if (text == null)
        {
            return false;
        }

        var source = text.text;
        if (!MightNeedExternalTextTranslation(source))
        {
            return false;
        }

        if (TryGetCachedTranslation(text, TextMeshTranslationCache, source, out var translated))
        {
            return ApplyTranslatedTextMesh(text, source, translated);
        }

        if (!ExternalEnglishCompatibilityService.TryTranslateFast(source, out translated) ||
            string.Equals(source, translated, StringComparison.Ordinal))
        {
            CacheTranslation(text, TextMeshTranslationCache, source, null);
            return false;
        }

        CacheTranslation(text, TextMeshTranslationCache, source, translated);
        return ApplyTranslatedTextMesh(text, source, translated);
    }

    private static bool IsProtectedInputTextComponent(TMP_Text text)
    {
        var parentId = GetParentInstanceId(text);
        if (TmpProtectedInputCache.TryGetValue(text.GetInstanceID(), out var cached) &&
            cached.ParentId == parentId)
        {
            return cached.Value;
        }

        var input = text.GetComponentInParent<TMP_InputField>(true);
        var result = input != null && ReferenceEquals(input.textComponent, text);
        CacheProtectedInput(text, TmpProtectedInputCache, parentId, result);
        return result;
    }

    private static bool IsProtectedInputTextComponent(Text text)
    {
        var parentId = GetParentInstanceId(text);
        if (UiProtectedInputCache.TryGetValue(text.GetInstanceID(), out var cached) &&
            cached.ParentId == parentId)
        {
            return cached.Value;
        }

        var input = text.GetComponentInParent<InputField>(true);
        var result = input != null && ReferenceEquals(input.textComponent, text);
        CacheProtectedInput(text, UiProtectedInputCache, parentId, result);
        return result;
    }

    private static bool ApplyTranslatedTmpText(TMP_Text text, string source, string translated, string reason)
    {
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        TextPatches.MarkTmpHookTranslation(text, source, translated);
        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        RepairTranslatedTmpInputPlaceholder(text, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    private static void RepairTranslatedTmpInputPlaceholder(TMP_Text text, string translated)
    {
        if (!ContainsCjk(translated))
        {
            return;
        }

        var id = text.GetInstanceID();
        if (RepairedTmpInputPlaceholderCache.Contains(id))
        {
            return;
        }

        var input = text.GetComponentInParent<TMP_InputField>(true);
        if (input == null ||
            ReferenceEquals(input.textComponent, text) ||
            !ReferenceEquals(input.placeholder, text))
        {
            return;
        }

        if (RepairedTmpInputPlaceholderCache.Count >= RuntimePerformanceSettings.ComponentTextCacheLimit)
        {
            return;
        }

        var fontSize = text.fontSize;
        if (fontSize <= 0f)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Math.Min(text.fontSizeMin > 0f ? text.fontSizeMin : fontSize * 0.65f, fontSize * 0.65f);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        RepairedTmpInputPlaceholderCache.Add(id);
    }

    private static bool ApplyTranslatedUiText(Text text, string source, string translated)
    {
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        text.text = translated;
        Plugin.ReportTranslationHit();
        return true;
    }

    private static bool ApplyTranslatedTextMesh(TextMesh text, string source, string translated)
    {
        if (string.Equals(source, translated, StringComparison.Ordinal))
        {
            return false;
        }

        text.text = translated;
        Plugin.ReportTranslationHit();
        return true;
    }

    private static bool TryGetCachedTranslation(
        Component text,
        Dictionary<int, CachedComponentTranslation> cache,
        string? source,
        out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrEmpty(source) ||
            !cache.TryGetValue(text.GetInstanceID(), out var cached))
        {
            return false;
        }

        if (string.Equals(cached.Source, source, StringComparison.Ordinal))
        {
            if (cached.Translated == null)
            {
                return true;
            }

            translated = cached.Translated;
            return true;
        }

        if (cached.Translated != null &&
            string.Equals(cached.Translated, source, StringComparison.Ordinal))
        {
            translated = source;
            return true;
        }

        return false;
    }

    private static void CacheTranslation(
        Component text,
        Dictionary<int, CachedComponentTranslation> cache,
        string? source,
        string? translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        var id = text.GetInstanceID();
        if (cache.Count >= RuntimePerformanceSettings.ComponentTextCacheLimit && !cache.ContainsKey(id))
        {
            return;
        }

        cache[id] = new CachedComponentTranslation(source, translated);
    }

    private static void CacheProtectedInput(
        Component text,
        Dictionary<int, CachedProtectedInput> cache,
        int parentId,
        bool value)
    {
        var id = text.GetInstanceID();
        if (cache.Count >= RuntimePerformanceSettings.ComponentTextCacheLimit && !cache.ContainsKey(id))
        {
            return;
        }

        cache[id] = new CachedProtectedInput(parentId, value);
    }

    private static int GetParentInstanceId(Component text)
    {
        var parent = text.transform == null ? null : text.transform.parent;
        return parent == null ? 0 : parent.GetInstanceID();
    }

    private static bool MightNeedExternalTextTranslation(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 512)
        {
            return false;
        }

        var hasAsciiLetter = false;
        var hasCjk = false;
        foreach (var ch in source)
        {
            if (ch is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF')
            {
                hasCjk = true;
                continue;
            }

            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            {
                hasAsciiLetter = true;
            }
        }

        return hasAsciiLetter && (!hasCjk || ExternalEnglishCompatibilityService.CanHandleCheap(source));
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if (ch is >= '\u3400' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF')
            {
                return true;
            }
        }

        return false;
    }
}
