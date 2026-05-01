using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class RuntimeTextCollector
{
    private const int MaxTextLength = 2000;
    private static readonly Dictionary<string, RuntimeTextRecord> Records = new(StringComparer.Ordinal);
    private static ConfigEntry<bool>? _enabled;
    private static string? _outputPath;
    private static bool _isInitialized;

    public static int Count => Records.Count;
    public static bool IsEnabled => _enabled?.Value == true;

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _enabled = config.Bind(
            "Diagnostics",
            "CollectUntranslatedText",
            false,
            "Collect untranslated runtime text candidates into logs/untranslated-texts.csv. Disabled by default to avoid runtime overhead.");

        if (!IsEnabled)
        {
            Records.Clear();
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
            Flush();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to initialize untranslated text collector: {ex.Message}");
        }
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
        if (!IsEnabled || !_isInitialized || string.IsNullOrEmpty(_outputPath) || !ShouldCollect(source))
        {
            return;
        }

        var normalized = Normalize(source!);
        var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : string.Empty;
        var objectPath = GetObjectPath(gameObject);
        var key = $"{sceneName}\n{componentType}\n{objectPath}\n{normalized}";
        if (Records.ContainsKey(key))
        {
            return;
        }

        Records[key] = new RuntimeTextRecord
        {
            FirstSeenUtc = DateTime.UtcNow.ToString("o"),
            Scene = sceneName,
            Component = componentType,
            ObjectPath = objectPath,
            FontName = fontName,
            Text = normalized
        };
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

        return !TranslationService.TryTranslate(normalized, out _);
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

    private static void Flush()
    {
        if (string.IsNullOrEmpty(_outputPath))
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("firstSeenUtc,scene,component,objectPath,fontName,text");
            foreach (var record in Records.Values)
            {
                builder.Append(Csv(record.FirstSeenUtc)).Append(',')
                    .Append(Csv(record.Scene)).Append(',')
                    .Append(Csv(record.Component)).Append(',')
                    .Append(Csv(record.ObjectPath)).Append(',')
                    .Append(Csv(record.FontName)).Append(',')
                    .Append(Csv(record.Text)).AppendLine();
            }

            File.WriteAllText(_outputPath, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to flush untranslated text collector: {ex.Message}");
        }
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
