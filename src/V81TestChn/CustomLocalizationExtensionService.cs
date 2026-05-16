using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class CustomLocalizationExtensionService
{
    private const int StyleCacheLimit = 2048;
    private const int DefaultMaxExactRules = 4096;
    private const int DefaultMaxIgnoreCaseRules = 4096;
    private const int DefaultMaxRegexRules = 64;
    private const int DefaultMaxStyleRules = 64;
    private const int DefaultMaxLoadedFiles = 32;
    private const int DefaultMaxConfigFileBytes = 262144;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(25);
    private static readonly Dictionary<string, string> ExactEntries = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> ExactIgnoreCaseEntries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<RegexEntry> RegexEntries = new();
    private static readonly List<StyleRule> StyleRules = new();
    private static readonly Dictionary<int, CachedStyleLookup> StyleCache = new();
    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<bool>? _preferCustomTranslations;
    private static ConfigEntry<bool>? _enableRegex;
    private static ConfigEntry<int>? _maxExactRules;
    private static ConfigEntry<int>? _maxIgnoreCaseRules;
    private static ConfigEntry<int>? _maxRegexRules;
    private static ConfigEntry<int>? _maxStyleRules;
    private static ConfigEntry<int>? _maxLoadedFiles;
    private static ConfigEntry<int>? _maxConfigFileBytes;
    private static bool _warnedRegexDisabled;
    private static bool _warnedExactLimit;
    private static bool _warnedIgnoreCaseLimit;
    private static bool _hasStyleRules;
    private static bool _hasGlobalStyleRules;

    public static bool PreferCustomTranslations => _enabled?.Value == true && _preferCustomTranslations?.Value == true;
    public static bool EnableRegex => _enabled?.Value == true && _enableRegex?.Value == true;
    public static bool HasStyleRules => _enabled?.Value == true && _hasStyleRules;
    public static bool HasGlobalStyleRules => _enabled?.Value == true && _hasGlobalStyleRules;

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _enabled = config.Bind(
            "CustomLocalization",
            "Enabled",
            true,
            "Load optional custom localization cfg files from plugin/config custom-localization directories.");
        _preferCustomTranslations = config.Bind(
            "CustomLocalization",
            "PreferCustomTranslations",
            false,
            "When true, custom exact translations are checked before built-in translations. Regex translations are still gated by EnableRegex.");
        _enableRegex = config.Bind(
            "CustomLocalization",
            "EnableRegex",
            false,
            "Enable custom regex translations and regex style rules. Disabled by default to keep global text paths cheap.");
        _maxExactRules = config.Bind(
            "CustomLocalization",
            "MaxExactRules",
            DefaultMaxExactRules,
            "Maximum custom exact translation rules to load.");
        _maxIgnoreCaseRules = config.Bind(
            "CustomLocalization",
            "MaxIgnoreCaseRules",
            DefaultMaxIgnoreCaseRules,
            "Maximum custom ignorecase translation rules to load.");
        _maxRegexRules = config.Bind(
            "CustomLocalization",
            "MaxRegexRules",
            DefaultMaxRegexRules,
            "Maximum custom regex translation rules to load.");
        _maxStyleRules = config.Bind(
            "CustomLocalization",
            "MaxStyleRules",
            DefaultMaxStyleRules,
            "Maximum custom style rules to load.");
        _maxLoadedFiles = config.Bind(
            "CustomLocalization",
            "MaxLoadedFiles",
            DefaultMaxLoadedFiles,
            "Maximum custom localization cfg files to load.");
        _maxConfigFileBytes = config.Bind(
            "CustomLocalization",
            "MaxConfigFileBytes",
            DefaultMaxConfigFileBytes,
            "Maximum size in bytes for each custom localization cfg file.");

        Load(pluginDir);
    }

    public static void Shutdown()
    {
        Clear();
        _enabled = null;
        _preferCustomTranslations = null;
        _enableRegex = null;
        _maxExactRules = null;
        _maxIgnoreCaseRules = null;
        _maxRegexRules = null;
        _maxStyleRules = null;
        _maxLoadedFiles = null;
        _maxConfigFileBytes = null;
    }

    public static void Clear()
    {
        ExactEntries.Clear();
        ExactIgnoreCaseEntries.Clear();
        RegexEntries.Clear();
        StyleRules.Clear();
        ClearStyleCache();
        _warnedRegexDisabled = false;
        _warnedExactLimit = false;
        _warnedIgnoreCaseLimit = false;
        _hasStyleRules = false;
        _hasGlobalStyleRules = false;
    }

    public static bool TryTranslateFastExact(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (_enabled?.Value != true || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (ExactEntries.TryGetValue(source, out translated) ||
            ExactIgnoreCaseEntries.TryGetValue(source, out translated))
        {
            return !string.Equals(source, translated, StringComparison.Ordinal);
        }

        return false;
    }

    public static bool TryTranslate(string? source, out string translated, bool allowRegex)
    {
        translated = source ?? string.Empty;
        if (_enabled?.Value != true || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (TryTranslateFastExact(source, out translated))
        {
            return true;
        }

        if (!(allowRegex && EnableRegex) || RegexEntries.Count == 0)
        {
            return false;
        }

        foreach (var entry in RegexEntries)
        {
            if (entry.Disabled)
            {
                continue;
            }

            try
            {
                if (!entry.Regex.IsMatch(source))
                {
                    continue;
                }

                translated = entry.Regex.Replace(source, entry.Replacement);
                return !string.Equals(source, translated, StringComparison.Ordinal);
            }
            catch (RegexMatchTimeoutException)
            {
                entry.Disabled = true;
                if (!entry.WarnedTimeout)
                {
                    entry.WarnedTimeout = true;
                    Plugin.Log.LogWarning($"CustomLocalization regex timeout; disabled rule '{entry.Pattern}'.");
                }
            }
        }

        return false;
    }

    public static void ApplyStyle(TMP_Text? text, string? value, bool allowRegexStyle = false)
    {
        if (text == null || !TryFindStyle(text, value, allowRegexStyle, out var style))
        {
            return;
        }

        if (style.RichText.HasValue)
        {
            text.richText = style.RichText.Value;
        }

        if (style.Color.HasValue)
        {
            text.color = style.Color.Value;
        }

        if (style.FontSize.HasValue)
        {
            text.fontSize = style.FontSize.Value;
        }
    }

    public static void ApplyStyle(Text? text, string? value, bool allowRegexStyle = false)
    {
        if (text == null || !TryFindStyle(text, value, allowRegexStyle, out var style))
        {
            return;
        }

        if (style.RichText.HasValue)
        {
            text.supportRichText = style.RichText.Value;
        }

        if (style.Color.HasValue)
        {
            text.color = style.Color.Value;
        }

        if (style.FontSize.HasValue)
        {
            text.fontSize = Mathf.RoundToInt(style.FontSize.Value);
        }
    }

    public static void ApplyStyle(TextMesh? text, string? value, bool allowRegexStyle = false)
    {
        if (text == null || !TryFindStyle(text, value, allowRegexStyle, out var style))
        {
            return;
        }

        if (style.RichText.HasValue)
        {
            text.richText = style.RichText.Value;
        }

        if (style.Color.HasValue)
        {
            text.color = style.Color.Value;
        }

        if (style.FontSize.HasValue)
        {
            text.fontSize = Mathf.RoundToInt(style.FontSize.Value);
        }
    }

    private static void Load(string pluginDir)
    {
        Clear();
        if (_enabled?.Value != true)
        {
            return;
        }

        var filesLoaded = 0;
        var filesVisited = 0;
        var maxLoadedFiles = GetMaxLoadedFiles();
        foreach (var directory in ResolveCustomLocalizationDirectories(pluginDir))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cfg", SearchOption.AllDirectories))
            {
                if (filesVisited >= maxLoadedFiles)
                {
                    Plugin.Log.LogWarning($"CustomLocalization MaxLoadedFiles={maxLoadedFiles} reached; remaining cfg files skipped.");
                    LogLoadSummary(filesLoaded);
                    return;
                }

                filesVisited++;
                if (LoadCfgFile(file))
                {
                    filesLoaded++;
                }
            }
        }

        LogLoadSummary(filesLoaded);
    }

    private static void LogLoadSummary(int filesLoaded)
    {
        Plugin.Log.LogInfo(
            $"CustomLocalization loaded files={filesLoaded}; exact={ExactEntries.Count}; ignorecase={ExactIgnoreCaseEntries.Count}; regex={RegexEntries.Count}; style={StyleRules.Count}.");
    }

    private static IEnumerable<string> ResolveCustomLocalizationDirectories(string pluginDir)
    {
        yield return Path.Combine(pluginDir, "V81TestChn", "custom-localization");
        yield return Path.Combine(pluginDir, "custom-localization");
        yield return Path.Combine(Paths.ConfigPath, "V81TestChn", "custom-localization");
        yield return Path.Combine(Paths.ConfigPath, "V81TestChn", "custom-translations");
        yield return Path.Combine(Paths.ConfigPath, "translations", "custom");
    }

    private static bool LoadCfgFile(string file)
    {
        var loadedAny = false;
        try
        {
            var info = new FileInfo(file);
            var maxConfigFileBytes = GetMaxConfigFileBytes();
            if (info.Length > maxConfigFileBytes)
            {
                Plugin.Log.LogWarning($"CustomLocalization skipped '{file}' because it is {info.Length} bytes; MaxConfigFileBytes={maxConfigFileBytes}.");
                return false;
            }

            foreach (var rawLine in File.ReadLines(file))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                loadedAny |= TryAddLine(line, file);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"CustomLocalization failed reading '{file}': {ex.GetType().Name}: {ex.Message}");
        }

        return loadedAny;
    }

    private static bool TryAddLine(string line, string file)
    {
        if (line.StartsWith("style:", StringComparison.OrdinalIgnoreCase))
        {
            return TryAddStyleRule(line["style:".Length..], file);
        }

        var separator = FindUnescapedSeparator(line, '=');
        if (separator <= 0)
        {
            return false;
        }

        var rawKey = line[..separator].Trim();
        var value = UnescapeCustomValue(line[(separator + 1)..]);
        if (rawKey.StartsWith("regex:", StringComparison.OrdinalIgnoreCase) ||
            rawKey.StartsWith("r:", StringComparison.OrdinalIgnoreCase))
        {
            var rawPattern = rawKey.StartsWith("regex:", StringComparison.OrdinalIgnoreCase)
                ? rawKey["regex:".Length..]
                : rawKey["r:".Length..];
            var pattern = UnescapeCustomRegexPattern(rawPattern.Trim());
            return TryAddRegex(pattern.Trim(), value, file);
        }

        if (rawKey.StartsWith("ignorecase:", StringComparison.OrdinalIgnoreCase) ||
            rawKey.StartsWith("i:", StringComparison.OrdinalIgnoreCase))
        {
            var rawExact = rawKey.StartsWith("ignorecase:", StringComparison.OrdinalIgnoreCase)
                ? rawKey["ignorecase:".Length..]
                : rawKey["i:".Length..];
            var exact = UnescapeCustomValue(rawExact).Trim();
            return TryAddExact(exact, value, ignoreCase: true, file);
        }

        var key = rawKey;
        if (key.StartsWith("exact:", StringComparison.OrdinalIgnoreCase))
        {
            key = key["exact:".Length..].Trim();
        }

        key = UnescapeCustomValue(key).Trim();
        return TryAddExact(key, value, ignoreCase: false, file);
    }

    private static bool TryAddExact(string key, string value, bool ignoreCase, string file)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (ignoreCase)
        {
            if (!ExactIgnoreCaseEntries.ContainsKey(key) && ExactIgnoreCaseEntries.Count >= GetMaxIgnoreCaseRules())
            {
                WarnIgnoreCaseLimitOnce(file);
                return false;
            }

            ExactIgnoreCaseEntries[key] = value;
            return true;
        }

        if (!ExactEntries.ContainsKey(key) && ExactEntries.Count >= GetMaxExactRules())
        {
            WarnExactLimitOnce(file);
            return false;
        }

        ExactEntries[key] = value;
        return true;
    }

    private static bool TryAddRegex(string pattern, string replacement, string file)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        if (!EnableRegex)
        {
            WarnRegexDisabledOnce();
            return false;
        }

        var maxRegexRules = GetMaxRegexRules();
        if (RegexEntries.Count >= maxRegexRules)
        {
            Plugin.Log.LogWarning($"CustomLocalization skipped regex in '{file}' because MaxRegexRules={maxRegexRules} was reached.");
            return false;
        }

        try
        {
            RegexEntries.Add(new RegexEntry(
                pattern,
                new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout),
                replacement));
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            Plugin.Log.LogWarning($"CustomLocalization skipped invalid regex in '{file}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TryAddStyleRule(string payload, string file)
    {
        var parts = SplitUnescaped(payload, '|');
        if (parts.Count == 0)
        {
            return false;
        }

        var maxStyleRules = GetMaxStyleRules();
        if (StyleRules.Count >= maxStyleRules)
        {
            Plugin.Log.LogWarning($"CustomLocalization skipped style rule in '{file}' because MaxStyleRules={maxStyleRules} was reached.");
            return false;
        }

        var match = parts[0].Trim();
        var firstColon = FindUnescapedSeparator(match, ':');
        if (firstColon <= 0 || firstColon >= match.Length - 1)
        {
            return false;
        }

        var kind = ParseMatchKind(UnescapeCustomValue(match[..firstColon]));
        var rawPattern = match[(firstColon + 1)..].Trim();
        var pattern = kind == MatchKind.Regex
            ? UnescapeCustomRegexPattern(rawPattern)
            : UnescapeCustomValue(rawPattern);
        pattern = pattern.Trim();
        if (kind == MatchKind.None || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        if (kind == MatchKind.Regex && !EnableRegex)
        {
            WarnRegexDisabledOnce();
            return false;
        }

        var rule = new StyleRule(kind, pattern);
        for (var i = 1; i < parts.Count; i++)
        {
            var separator = FindUnescapedSeparator(parts[i], '=');
            if (separator <= 0)
            {
                continue;
            }

            var key = UnescapeCustomValue(parts[i][..separator]).Trim();
            var value = UnescapeCustomValue(parts[i][(separator + 1)..]).Trim();
            if (key.Equals("color", StringComparison.OrdinalIgnoreCase) &&
                ColorUtility.TryParseHtmlString(value, out var color))
            {
                if (color.a <= 0f)
                {
                    Plugin.Log.LogWarning($"CustomLocalization style in '{file}' has alpha=0 and will be invisible.");
                }

                rule.Color = color;
            }
            else if (key.Equals("fontSize", StringComparison.OrdinalIgnoreCase) &&
                     float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize) &&
                     fontSize > 0f)
            {
                rule.FontSize = Mathf.Clamp(fontSize, 4f, 128f);
            }
            else if (key.Equals("richText", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out var richText))
            {
                rule.RichText = richText;
            }
        }

        if (rule.Color == null && rule.FontSize == null && rule.RichText == null)
        {
            return false;
        }

        if (kind == MatchKind.Regex)
        {
            try
            {
                rule.Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                Plugin.Log.LogWarning($"CustomLocalization skipped invalid style regex in '{file}': {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        StyleRules.Add(rule);
        _hasStyleRules = true;
        if (!rule.HasRegex)
        {
            _hasGlobalStyleRules = true;
        }

        ClearStyleCache();
        return true;
    }

    private static bool TryFindStyle(Component component, string? value, bool allowRegexStyle, out StyleRule style)
    {
        style = null!;
        if (_enabled?.Value != true || string.IsNullOrEmpty(value))
        {
            return false;
        }

        var componentId = component.GetInstanceID();
        if (TryGetCachedStyle(componentId, value, allowRegexStyle, out var cachedMatched, out style))
        {
            return cachedMatched;
        }

        var matched = TryFindStyle(value, allowRegexStyle, out style);
        CacheStyleResult(componentId, value, allowRegexStyle, matched, style);
        return matched;
    }

    private static bool TryFindStyle(string value, bool allowRegexStyle, out StyleRule style)
    {
        style = null!;
        foreach (var rule in StyleRules)
        {
            if (rule.HasRegex && !allowRegexStyle)
            {
                continue;
            }

            if (rule.IsMatch(value))
            {
                style = rule;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetCachedStyle(int componentId, string value, bool allowRegexStyle, out bool matched, out StyleRule style)
    {
        style = null!;
        matched = false;
        if (!StyleCache.TryGetValue(componentId, out var cached) ||
            !string.Equals(cached.Value, value, StringComparison.Ordinal) ||
            cached.AllowRegexStyle != allowRegexStyle)
        {
            return false;
        }

        style = cached.Style!;
        matched = cached.Matched;
        return true;
    }

    private static void CacheStyleResult(int componentId, string value, bool allowRegexStyle, bool matched, StyleRule? style)
    {
        if (StyleCache.Count >= StyleCacheLimit)
        {
            StyleCache.Clear();
        }

        StyleCache[componentId] = new CachedStyleLookup(value, allowRegexStyle, matched, style);
    }

    private static void ClearStyleCache()
    {
        StyleCache.Clear();
    }

    public static void ClearStyleCacheOnly()
    {
        ClearStyleCache();
    }

    public static void ClearRuntimeCaches()
    {
        ClearStyleCache();
    }

    private static int FindUnescapedSeparator(string value, char separator)
    {
        var escaping = false;
        var insideRichTextTag = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                continue;
            }

            if (ch == '<')
            {
                insideRichTextTag = true;
                continue;
            }

            if (ch == '>')
            {
                insideRichTextTag = false;
                continue;
            }

            if (!insideRichTextTag && ch == separator)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<string> SplitUnescaped(string value, char separator)
    {
        var result = new List<string>();
        var start = 0;
        while (start <= value.Length)
        {
            var index = FindUnescapedSeparator(value[start..], separator);
            if (index < 0)
            {
                result.Add(value[start..]);
                break;
            }

            result.Add(value.Substring(start, index));
            start += index + 1;
        }

        return result;
    }

    private static string UnescapeCustomValue(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var escaping = false;
        foreach (var ch in value)
        {
            if (!escaping)
            {
                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            builder.Append(ch switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => ch
            });
            escaping = false;
        }

        if (escaping)
        {
            builder.Append('\\');
        }

        return builder.ToString();
    }

    private static string UnescapeCustomRegexPattern(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var escaping = false;
        foreach (var ch in value)
        {
            if (!escaping)
            {
                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                builder.Append(ch);
                continue;
            }

            if (ch is '=' or '|' or '\\')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('\\');
                builder.Append(ch);
            }

            escaping = false;
        }

        if (escaping)
        {
            builder.Append('\\');
        }

        return builder.ToString();
    }

    private static void WarnRegexDisabledOnce()
    {
        if (_warnedRegexDisabled)
        {
            return;
        }

        _warnedRegexDisabled = true;
        Plugin.Log.LogWarning("CustomLocalization.EnableRegex=false; skipped custom regex rule(s).");
    }

    private static void WarnExactLimitOnce(string file)
    {
        if (_warnedExactLimit)
        {
            return;
        }

        _warnedExactLimit = true;
        Plugin.Log.LogWarning($"CustomLocalization skipped exact rule in '{file}' because MaxExactRules={GetMaxExactRules()} was reached.");
    }

    private static void WarnIgnoreCaseLimitOnce(string file)
    {
        if (_warnedIgnoreCaseLimit)
        {
            return;
        }

        _warnedIgnoreCaseLimit = true;
        Plugin.Log.LogWarning($"CustomLocalization skipped ignorecase rule in '{file}' because MaxIgnoreCaseRules={GetMaxIgnoreCaseRules()} was reached.");
    }

    private static int GetMaxExactRules()
    {
        return Math.Max(0, _maxExactRules?.Value ?? DefaultMaxExactRules);
    }

    private static int GetMaxIgnoreCaseRules()
    {
        return Math.Max(0, _maxIgnoreCaseRules?.Value ?? DefaultMaxIgnoreCaseRules);
    }

    private static int GetMaxRegexRules()
    {
        return Math.Max(0, _maxRegexRules?.Value ?? DefaultMaxRegexRules);
    }

    private static int GetMaxStyleRules()
    {
        return Math.Max(0, _maxStyleRules?.Value ?? DefaultMaxStyleRules);
    }

    private static int GetMaxLoadedFiles()
    {
        return Math.Max(0, _maxLoadedFiles?.Value ?? DefaultMaxLoadedFiles);
    }

    private static int GetMaxConfigFileBytes()
    {
        return Math.Max(1024, _maxConfigFileBytes?.Value ?? DefaultMaxConfigFileBytes);
    }

    private static MatchKind ParseMatchKind(string value)
    {
        if (value.Equals("exact", StringComparison.OrdinalIgnoreCase))
        {
            return MatchKind.Exact;
        }

        if (value.Equals("contains", StringComparison.OrdinalIgnoreCase))
        {
            return MatchKind.Contains;
        }

        return value.Equals("regex", StringComparison.OrdinalIgnoreCase) || value.Equals("r", StringComparison.OrdinalIgnoreCase)
            ? MatchKind.Regex
            : MatchKind.None;
    }

    private enum MatchKind
    {
        None,
        Exact,
        Contains,
        Regex
    }

    private sealed class RegexEntry
    {
        public RegexEntry(string pattern, Regex regex, string replacement)
        {
            Pattern = pattern;
            Regex = regex;
            Replacement = replacement;
        }

        public string Pattern { get; }
        public Regex Regex { get; }
        public string Replacement { get; }
        public bool Disabled { get; set; }
        public bool WarnedTimeout { get; set; }
    }

    private sealed class StyleRule
    {
        public StyleRule(MatchKind kind, string pattern)
        {
            Kind = kind;
            Pattern = pattern;
        }

        public MatchKind Kind { get; }
        public string Pattern { get; }
        public Regex? Regex { get; set; }
        public Color? Color { get; set; }
        public float? FontSize { get; set; }
        public bool? RichText { get; set; }
        public bool Disabled { get; set; }
        public bool WarnedTimeout { get; set; }
        public bool HasRegex => Kind == MatchKind.Regex;

        public bool IsMatch(string value)
        {
            if (Disabled)
            {
                return false;
            }

            try
            {
                return Kind switch
                {
                    MatchKind.Exact => string.Equals(value, Pattern, StringComparison.Ordinal),
                    MatchKind.Contains => value.IndexOf(Pattern, StringComparison.Ordinal) >= 0,
                    MatchKind.Regex => Regex != null && Regex.IsMatch(value),
                    _ => false
                };
            }
            catch (RegexMatchTimeoutException)
            {
                Disabled = true;
                if (!WarnedTimeout)
                {
                    WarnedTimeout = true;
                    Plugin.Log.LogWarning($"CustomLocalization regex timeout; disabled style rule '{Pattern}'.");
                }

                return false;
            }
        }
    }

    private sealed class CachedStyleLookup
    {
        public CachedStyleLookup(string value, bool allowRegexStyle, bool matched, StyleRule? style)
        {
            Value = value;
            AllowRegexStyle = allowRegexStyle;
            Matched = matched;
            Style = style;
        }

        public string Value { get; }
        public bool AllowRegexStyle { get; }
        public bool Matched { get; }
        public StyleRule? Style { get; }
    }
}
