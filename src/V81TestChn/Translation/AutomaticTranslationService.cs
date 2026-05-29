using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace V81TestChn;

internal static class AutomaticTranslationService
{
    private const string CacheFileName = "auto-translation-cache.zh-CN.json";
    private const int DefaultTimeoutMilliseconds = 2500;
    private const int CacheSaveIntervalSeconds = 20;
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> PendingSources = new(StringComparer.Ordinal);
    private static readonly Queue<AutomaticTranslationResult> CompletedRequests = new();
    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<string>? _providerEndpoint;
    private static ConfigEntry<int>? _providerTimeoutMilliseconds;
    private static ConfigEntry<int>? _maxTextLength;
    private static ConfigEntry<int>? _maxCacheEntries;
    private static ConfigEntry<int>? _maxPendingRequests;
    private static ConfigEntry<bool>? _logAutomaticTranslation;
    private static string? _cachePath;
    private static bool _initialized;
    private static bool _enabledFast;
    private static bool _cacheDirty;
    private static int _inFlightRequests;
    private static DateTime _nextCacheSaveUtc;

    public static bool IsEnabled => _enabled?.Value == true;
    public static bool NeedsMainThreadPump => _enabledFast && _initialized;

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _enabled = config.Bind(
            ConfigSections.AutomaticTranslation,
            "EnableAutomaticTranslation",
            false,
            "Default false. When enabled, untranslated English mod text can be translated through the configured HTTP provider.");
        _providerEndpoint = config.Bind(
            ConfigSections.AutomaticTranslation,
            "ProviderEndpoint",
            string.Empty,
            "HTTP POST endpoint. Request JSON: { text, source, target }. Response can be JSON or plain text.");
        _providerTimeoutMilliseconds = config.Bind(
            ConfigSections.AutomaticTranslation,
            "ProviderTimeoutMilliseconds",
            DefaultTimeoutMilliseconds,
            "Background provider timeout in milliseconds. Main-thread text hooks never wait for this request.");
        _maxTextLength = config.Bind(
            ConfigSections.AutomaticTranslation,
            "MaxTextLength",
            300,
            "Maximum source text length accepted for automatic translation.");
        _maxCacheEntries = config.Bind(
            ConfigSections.AutomaticTranslation,
            "MaxCacheEntries",
            2000,
            "Maximum cached automatic translations kept per plugin directory.");
        _maxPendingRequests = config.Bind(
            ConfigSections.AutomaticTranslation,
            "MaxPendingRequests",
            32,
            "Maximum background automatic translation requests allowed at the same time.");
        _logAutomaticTranslation = config.Bind(
            ConfigSections.AutomaticTranslation,
            "LogAutomaticTranslation",
            false,
            "Log automatic translation cache hits, provider results, and provider failures.");

        lock (SyncRoot)
        {
            Cache.Clear();
            PendingSources.Clear();
            CompletedRequests.Clear();
            _cacheDirty = false;
            _inFlightRequests = 0;
            _nextCacheSaveUtc = DateTime.UtcNow.AddSeconds(CacheSaveIntervalSeconds);
            _cachePath = Path.Combine(pluginDir, "config", CacheFileName);
            _enabledFast = _enabled.Value;
            _initialized = true;
        }

        if (_enabledFast)
        {
            LoadCache();
        }
    }

    public static void Shutdown()
    {
        SaveCacheIfDirty(force: true);
        lock (SyncRoot)
        {
            Cache.Clear();
            PendingSources.Clear();
            CompletedRequests.Clear();
            _cachePath = null;
            _initialized = false;
            _enabledFast = false;
            _cacheDirty = false;
            _inFlightRequests = 0;
        }
    }

    public static bool TryTranslateOrQueue(string? source, out string translated)
    {
        translated = string.Empty;
        if (!_enabledFast || !_initialized || string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var normalized = NormalizeSource(source);
        if (normalized.Length == 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(normalized, out translated))
            {
                LogVerbose($"Automatic translation cache hit len={normalized.Length}.");
                return true;
            }
        }

        if (!CanQueueProviderRequest(normalized))
        {
            return false;
        }

        var endpoint = _providerEndpoint?.Value?.Trim();
        if (string.IsNullOrEmpty(endpoint))
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(normalized, out translated))
            {
                return true;
            }

            if (PendingSources.Contains(normalized) || _inFlightRequests >= GetMaxPendingRequests())
            {
                return false;
            }

            PendingSources.Add(normalized);
            _inFlightRequests++;
        }

        var timeoutMilliseconds = GetProviderTimeoutMilliseconds();
        _ = Task.Run(async () =>
        {
            var result = await RequestTranslationAsync(normalized, endpoint, timeoutMilliseconds).ConfigureAwait(false);
            CompleteRequest(result);
        });
        return false;
    }

    public static void PumpMainThread()
    {
        if (!NeedsMainThreadPump)
        {
            return;
        }

        var successes = 0;
        var failures = 0;
        lock (SyncRoot)
        {
            while (CompletedRequests.Count > 0)
            {
                var result = CompletedRequests.Dequeue();
                if (result.TranslatedText == null)
                {
                    failures++;
                    continue;
                }

                if (!Cache.ContainsKey(result.Source) && Cache.Count >= GetMaxCacheEntries())
                {
                    failures++;
                    continue;
                }

                Cache[result.Source] = result.TranslatedText;
                _cacheDirty = true;
                successes++;
            }
        }

        if (successes > 0)
        {
            LogVerbose($"Automatic translation cached {successes} result(s).");
        }

        if (failures > 0)
        {
            LogVerbose($"Automatic translation ignored {failures} provider result(s).");
        }

        SaveCacheIfDirty(force: false);
    }

    private static bool CanQueueProviderRequest(string source)
    {
        if (source.Length > GetMaxTextLength())
        {
            return false;
        }

        var hasAsciiLetter = false;
        var hasWordCharacter = false;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ContainsCjk(ch))
            {
                return false;
            }

            if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
            {
                hasAsciiLetter = true;
            }

            if (char.IsLetterOrDigit(ch))
            {
                hasWordCharacter = true;
            }
        }

        if (!hasAsciiLetter || !hasWordCharacter)
        {
            return false;
        }

        if (source.IndexOf("://", StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        return true;
    }

    private static async Task<AutomaticTranslationResult> RequestTranslationAsync(
        string source,
        string endpoint,
        int timeoutMilliseconds)
    {
        try
        {
            var request = JsonConvert.SerializeObject(new AutomaticTranslationRequest
            {
                Text = source,
                Source = "en",
                Target = "zh-CN"
            });
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                using (var content = new StringContent(request, Encoding.UTF8, "application/json"))
                using (var response = await client.PostAsync(endpoint, content).ConfigureAwait(false))
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return AutomaticTranslationResult.Failed(source, $"HTTP {(int)response.StatusCode}");
                    }

                    var translated = ExtractTranslatedText(responseText);
                    if (!IsAcceptableTranslation(source, translated))
                    {
                        return AutomaticTranslationResult.Failed(source, "provider returned unusable translation");
                    }

                    return AutomaticTranslationResult.Succeeded(source, translated!);
                }
            }
        }
        catch (Exception ex)
        {
            return AutomaticTranslationResult.Failed(source, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string? ExtractTranslatedText(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var trimmed = responseText.Trim();
        try
        {
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                var json = JObject.Parse(trimmed);
                foreach (var field in new[] { "translatedText", "translation", "target", "text" })
                {
                    var value = json[field]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }

            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                return JsonConvert.DeserializeObject<string>(trimmed)?.Trim();
            }
        }
        catch (JsonException)
        {
            return trimmed;
        }

        return trimmed;
    }

    private static void CompleteRequest(AutomaticTranslationResult result)
    {
        lock (SyncRoot)
        {
            PendingSources.Remove(result.Source);
            if (_inFlightRequests > 0)
            {
                _inFlightRequests--;
            }

            CompletedRequests.Enqueue(result);
        }
    }

    private static void LoadCache()
    {
        var path = _cachePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (loaded == null)
            {
                return;
            }

            var loadedCount = 0;
            lock (SyncRoot)
            {
                foreach (var pair in loaded)
                {
                    var source = NormalizeSource(pair.Key);
                    var translated = pair.Value?.Trim();
                    if (source.Length == 0 || !IsAcceptableTranslation(source, translated))
                    {
                        continue;
                    }

                    if (!Cache.ContainsKey(source) && Cache.Count >= GetMaxCacheEntries())
                    {
                        break;
                    }

                    Cache[source] = translated!;
                    loadedCount++;
                }
            }

            LogVerbose($"Automatic translation loaded {loadedCount} cached result(s).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Automatic translation cache load failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SaveCacheIfDirty(bool force)
    {
        Dictionary<string, string>? snapshot = null;
        var path = _cachePath;
        lock (SyncRoot)
        {
            if (string.IsNullOrEmpty(path) || !_cacheDirty)
            {
                return;
            }

            if (!force && DateTime.UtcNow < _nextCacheSaveUtc)
            {
                return;
            }

            snapshot = new Dictionary<string, string>(Cache, StringComparer.Ordinal);
            _cacheDirty = false;
            _nextCacheSaveUtc = DateTime.UtcNow.AddSeconds(CacheSaveIntervalSeconds);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            lock (SyncRoot)
            {
                _cacheDirty = true;
            }

            Plugin.Log.LogWarning($"Automatic translation cache save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsAcceptableTranslation(string source, string? translated)
    {
        if (string.IsNullOrWhiteSpace(translated))
        {
            return false;
        }

        var trimmed = translated.Trim();
        if (string.Equals(source, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length > Math.Max(GetMaxTextLength() * 4, 1024))
        {
            return false;
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (ContainsCjk(trimmed[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCjk(char ch)
    {
        return (ch >= '\u3400' && ch <= '\u9fff') ||
            (ch >= '\uf900' && ch <= '\ufaff');
    }

    private static string NormalizeSource(string source)
    {
        return source.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }

    private static int GetProviderTimeoutMilliseconds()
    {
        return Math.Max(250, _providerTimeoutMilliseconds?.Value ?? DefaultTimeoutMilliseconds);
    }

    private static int GetMaxTextLength()
    {
        return Math.Max(1, _maxTextLength?.Value ?? 300);
    }

    private static int GetMaxCacheEntries()
    {
        return Math.Max(1, _maxCacheEntries?.Value ?? 2000);
    }

    private static int GetMaxPendingRequests()
    {
        return Math.Max(0, _maxPendingRequests?.Value ?? 32);
    }

    private static void LogVerbose(string message)
    {
        if (_logAutomaticTranslation?.Value == true)
        {
            Plugin.Log.LogInfo(message);
        }
    }

    private sealed class AutomaticTranslationRequest
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = "en";

        [JsonProperty("target")]
        public string Target { get; set; } = "zh-CN";
    }

    private readonly struct AutomaticTranslationResult
    {
        private AutomaticTranslationResult(string source, string? translatedText, string? error)
        {
            Source = source;
            TranslatedText = translatedText;
            Error = error;
        }

        public string Source { get; }
        public string? TranslatedText { get; }
        public string? Error { get; }

        public static AutomaticTranslationResult Succeeded(string source, string translatedText)
        {
            return new AutomaticTranslationResult(source, translatedText, null);
        }

        public static AutomaticTranslationResult Failed(string source, string error)
        {
            return new AutomaticTranslationResult(source, null, error);
        }
    }
}
