using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class RuntimeTextCollector
{
    private const int MaxTextLength = 2000;
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, RuntimeTextRecord> Records = new(StringComparer.Ordinal);
    private static readonly List<RuntimeTextRecord> PendingRecords = new();
    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<float>? _collectorFlushIntervalSeconds;
    private static ConfigEntry<int>? _collectorFlushEveryRecords;
    private static ConfigEntry<int>? _maxCollectedRecords;
    private static ConfigEntry<bool>? _collectorUseFullTranslationCheck;
    private static string? _outputPath;
    private static bool _isInitialized;
    private static bool _maxRecordsWarningLogged;
    private static DateTime _nextFlushUtc;
    private static Timer? _flushTimer;

    public static int Count
    {
        get
        {
            lock (SyncRoot)
            {
                return Records.Count;
            }
        }
    }

    public static bool IsEnabled => _enabled?.Value == true;

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _enabled = config.Bind(
            "Diagnostics",
            "CollectUntranslatedText",
            false,
            "Collect untranslated runtime text candidates into logs/untranslated-texts.csv. Disabled by default to avoid runtime overhead.");
        _collectorFlushIntervalSeconds = config.Bind(
            "Diagnostics",
            "CollectorFlushIntervalSeconds",
            30f,
            "When untranslated text collection is enabled, append pending records at this interval.");
        _collectorFlushEveryRecords = config.Bind(
            "Diagnostics",
            "CollectorFlushEveryRecords",
            20,
            "When untranslated text collection is enabled, append pending records after this many new records.");
        _maxCollectedRecords = config.Bind(
            "Diagnostics",
            "MaxCollectedRecords",
            5000,
            "Maximum unique untranslated text records to keep per plugin session.");
        _collectorUseFullTranslationCheck = config.Bind(
            "Diagnostics",
            "CollectorUseFullTranslationCheck",
            false,
            "When true, untranslated text collection uses the full TranslationService.TryTranslate check. Disabled by default to keep diagnostics cheap.");

        StopFlushTimer();
        lock (SyncRoot)
        {
            Records.Clear();
            PendingRecords.Clear();
            _maxRecordsWarningLogged = false;
        }

        if (!IsEnabled)
        {
            _outputPath = null;
            _isInitialized = false;
            return;
        }

        try
        {
            var logDir = Path.Combine(pluginDir, "logs");
            Directory.CreateDirectory(logDir);
            _outputPath = Path.Combine(logDir, "untranslated-texts.csv");
            _isInitialized = true;
            _nextFlushUtc = DateTime.UtcNow.AddSeconds(GetFlushIntervalSeconds());
            WriteHeaderIfMissing();
            StartFlushTimer();
            Plugin.Log.LogInfo($"RuntimeTextCollector writing untranslated text candidates to '{_outputPath}'.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to initialize untranslated text collector: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        StopFlushTimer();
        FlushPending();
        lock (SyncRoot)
        {
            Records.Clear();
            PendingRecords.Clear();
        }

        _outputPath = null;
        _isInitialized = false;
        _maxRecordsWarningLogged = false;
        _collectorUseFullTranslationCheck = null;
    }

    public static void Record(TMP_Text component, string? source)
    {
        if (component == null)
        {
            return;
        }

        Record("TMP_Text", component.gameObject, component.font != null ? component.font.name : string.Empty, source);
    }

    public static void Record(Text component, string? source)
    {
        if (component == null)
        {
            return;
        }

        Record("UI_Text", component.gameObject, component.font != null ? component.font.name : string.Empty, source);
    }

    public static void Record(TextMesh component, string? source)
    {
        if (component == null)
        {
            return;
        }

        Record("TextMesh", component.gameObject, component.font != null ? component.font.name : string.Empty, source);
    }

    private static void Record(string componentType, GameObject gameObject, string fontName, string? source)
    {
        if (!IsEnabled || !_isInitialized || string.IsNullOrEmpty(_outputPath))
        {
            return;
        }

        if (!ShouldCollect(source))
        {
            return;
        }

        var normalized = Normalize(source!);
        var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
        var objectPath = GetObjectPath(gameObject);
        var key = $"{sceneName}\n{componentType}\n{objectPath}\n{normalized}";
        var shouldFlush = false;

        lock (SyncRoot)
        {
            if (Records.Count >= GetMaxCollectedRecords())
            {
                WarnMaxCollectedRecordsOnce();
                return;
            }

            if (Records.ContainsKey(key))
            {
                shouldFlush = ShouldFlushPendingRecordsLocked();
            }
            else
            {
                var record = new RuntimeTextRecord
                {
                    FirstSeenUtc = DateTime.UtcNow.ToString("o"),
                    Scene = sceneName,
                    Component = componentType,
                    ObjectPath = objectPath,
                    FontName = fontName,
                    Text = normalized
                };
                Records[key] = record;
                PendingRecords.Add(record);
                shouldFlush = ShouldFlushPendingRecordsLocked();
            }
        }

        if (shouldFlush)
        {
            FlushPending();
        }
    }

    private static bool ShouldCollect(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var normalized = Normalize(source);
        if (normalized.Length <= 1 || normalized.Length > MaxTextLength)
        {
            return false;
        }

        var hasAsciiLetter = false;
        foreach (var ch in normalized)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF)
            {
                continue;
            }

            if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
            {
                hasAsciiLetter = true;
            }
        }

        // We only audit user-visible strings that still include latin text.
        // Pure Chinese lines are not useful untranslated candidates.
        if (!hasAsciiLetter)
        {
            return false;
        }

        if (TranslationService.TryTranslateFastExact(normalized, out _) ||
            TranslationService.TryTranslateKnownDynamicTextFast(normalized, out _) ||
            CustomLocalizationExtensionService.TryTranslateFastExact(normalized, out _))
        {
            return false;
        }

        if (_collectorUseFullTranslationCheck?.Value == true)
        {
            return !TranslationService.TryTranslate(normalized, out _);
        }

        return true;
    }

    private static string Normalize(string source)
    {
        return source.Replace("\r\n", "\n").Trim();
    }

    private static string GetObjectPath(GameObject gameObject)
    {
        var parts = new Stack<string>();
        var current = gameObject.transform;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts.ToArray());
    }

    private static void MaybeFlushPendingRecords()
    {
        var shouldFlush = false;
        lock (SyncRoot)
        {
            shouldFlush = ShouldFlushPendingRecordsLocked();
        }

        if (shouldFlush)
        {
            FlushPending();
        }
    }

    private static bool ShouldFlushPendingRecordsLocked()
    {
        return PendingRecords.Count > 0 &&
               (PendingRecords.Count >= GetFlushEveryRecords() || DateTime.UtcNow >= _nextFlushUtc);
    }

    private static void FlushPending()
    {
        List<RuntimeTextRecord> pending;
        string outputPath;
        lock (SyncRoot)
        {
            if (string.IsNullOrEmpty(_outputPath) || PendingRecords.Count == 0)
            {
                return;
            }

            outputPath = _outputPath;
            pending = new List<RuntimeTextRecord>(PendingRecords);
            PendingRecords.Clear();
            _nextFlushUtc = DateTime.UtcNow.AddSeconds(GetFlushIntervalSeconds());
        }

        try
        {
            WriteHeaderIfMissing(outputPath);
            var builder = new StringBuilder();
            foreach (var record in pending)
            {
                builder.Append(Csv(record.FirstSeenUtc)).Append(',')
                    .Append(Csv(record.Scene)).Append(',')
                    .Append(Csv(record.Component)).Append(',')
                    .Append(Csv(record.ObjectPath)).Append(',')
                    .Append(Csv(record.FontName)).Append(',')
                    .Append(Csv(record.Text)).AppendLine();
            }

            File.AppendAllText(outputPath, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to flush untranslated text collector: {ex.Message}");
            lock (SyncRoot)
            {
                if (_isInitialized && !string.IsNullOrEmpty(_outputPath))
                {
                    PendingRecords.InsertRange(0, pending);
                }
            }
        }
    }

    private static void WriteHeaderIfMissing()
    {
        if (string.IsNullOrEmpty(_outputPath))
        {
            return;
        }

        WriteHeaderIfMissing(_outputPath);
    }

    private static void WriteHeaderIfMissing(string outputPath)
    {
        var info = new FileInfo(outputPath);
        if (info.Exists && info.Length > 0)
        {
            return;
        }

        File.AppendAllText(outputPath, "firstSeenUtc,scene,component,objectPath,fontName,text" + Environment.NewLine, Encoding.UTF8);
    }

    private static void StartFlushTimer()
    {
        StopFlushTimer();
        if (!IsEnabled || !_isInitialized)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(GetFlushIntervalSeconds());
        _flushTimer = new Timer(FlushTimerCallback, null, interval, interval);
    }

    private static void StopFlushTimer()
    {
        var timer = _flushTimer;
        _flushTimer = null;
        timer?.Dispose();
    }

    private static void FlushTimerCallback(object? state)
    {
        try
        {
            FlushPending();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"RuntimeTextCollector timer flush failed: {ex.Message}");
        }
    }

    private static void WarnMaxCollectedRecordsOnce()
    {
        if (_maxRecordsWarningLogged)
        {
            return;
        }

        _maxRecordsWarningLogged = true;
        Plugin.Log.LogWarning($"Max collected untranslated text records reached ({GetMaxCollectedRecords()}); RuntimeTextCollector will ignore additional records this session.");
    }

    private static float GetFlushIntervalSeconds()
    {
        return Math.Max(1f, _collectorFlushIntervalSeconds?.Value ?? 30f);
    }

    private static int GetFlushEveryRecords()
    {
        return Math.Max(1, _collectorFlushEveryRecords?.Value ?? 20);
    }

    private static int GetMaxCollectedRecords()
    {
        return Math.Max(0, _maxCollectedRecords?.Value ?? 5000);
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }

    private sealed class RuntimeTextRecord
    {
        public string FirstSeenUtc { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string ObjectPath { get; set; } = string.Empty;
        public string FontName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
