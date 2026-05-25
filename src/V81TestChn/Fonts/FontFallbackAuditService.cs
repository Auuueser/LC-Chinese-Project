using BepInEx.Configuration;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class FontFallbackAuditService
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(25);
    private static readonly HashSet<string> LoggedSnapshotKeys = new();
    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<int>? _budgetConfig;
    private static ConfigEntry<int>? _maxPerFrameConfig;
    private static ConfigEntry<string>? _pathRegexConfig;
    private static ConfigEntry<string>? _textRegexConfig;
    private static ConfigEntry<bool>? _includeTextInfoConfig;
    private static Regex? _pathRegex;
    private static Regex? _textRegex;
    private static string? _lastPathPattern;
    private static string? _lastTextPattern;
    private static int _remainingBudget;
    private static int _lastFrame = -1;
    private static int _loggedThisFrame;
    private static bool _disabledByRegexError;
    private static bool _initialized;

    public static void Initialize(ConfigFile config)
    {
        _enabled = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditEnabled",
            false,
            "启用只读 TMP 字体与 fallback 诊断。默认关闭；开启后只记录信息，不修改材质或文本。");

        _budgetConfig = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditBudget",
            100,
            "单次插件运行最多写入的字体诊断日志条数。");

        _maxPerFrameConfig = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditMaxLogsPerFrame",
            2,
            "每帧最多写入的详细字体诊断日志条数。");

        _pathRegexConfig = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditPathRegex",
            string.Empty,
            "对象路径过滤正则。留空表示不过滤。");

        _textRegexConfig = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditTextRegex",
            string.Empty,
            "文本内容过滤正则。留空表示不过滤。");

        _includeTextInfoConfig = config.Bind(
            ConfigSections.DiagnosticsFontFallback,
            "FontFallbackAuditIncludeTextInfo",
            true,
            "字体诊断日志是否包含 TMP_TextInfo、材质编号和中文字符路由细节。");

        _remainingBudget = Math.Max(0, _budgetConfig.Value);
        _initialized = true;
        if (_enabled.Value)
        {
            Plugin.Log.LogInfo($"FontFallbackAudit enabled; budget={_remainingBudget}, maxPerFrame={Math.Max(1, _maxPerFrameConfig.Value)}.");
        }
    }

    public static void Shutdown()
    {
        LoggedSnapshotKeys.Clear();
        _pathRegex = null;
        _textRegex = null;
        _lastPathPattern = null;
        _lastTextPattern = null;
        _remainingBudget = 0;
        _lastFrame = -1;
        _loggedThisFrame = 0;
        _disabledByRegexError = false;
        _initialized = false;
    }

    public static void RecordFontAssetSnapshot(TMP_FontAsset? fontAsset, string stage)
    {
        if (!IsActive() || fontAsset == null)
        {
            return;
        }

        var key = $"font|{stage}|{fontAsset.GetInstanceID()}|{GetMaterialId(fontAsset.material)}|{GetFallbackCount(fontAsset)}";
        if (!ShouldLog(key))
        {
            return;
        }

        Plugin.Log.LogWarning(
            $"FontFallbackAudit[{stage}] font={DescribeFont(fontAsset)}, fontMaterial={DescribeMaterial(fontAsset.material)}, fallbackCount={GetFallbackCount(fontAsset)}, fallbacks={DescribeFallbackFonts(fontAsset)}");
    }

    public static void RecordTextSnapshot(TMP_Text? text, string stage, string? candidateText = null)
    {
        if (!IsActive() || text == null)
        {
            return;
        }

        var displayedText = candidateText ?? text.text;
        if (string.IsNullOrWhiteSpace(displayedText))
        {
            return;
        }

        var path = BuildPath(text.transform);
        if (!PassesFilters(path, displayedText))
        {
            return;
        }

        var font = text.font;
        var shared = text.fontSharedMaterial;
        var info = text.textInfo;
        var materialCount = info?.materialCount ?? -1;
        var key = $"text|{stage}|{text.GetInstanceID()}|{StableHash(displayedText)}|{GetFontId(font)}|{GetMaterialId(shared)}|{materialCount}";
        if (!ShouldLog(key))
        {
            return;
        }

        Plugin.Log.LogWarning(
            $"FontFallbackAudit[{stage}] role={ClassifyRole(path, text.name)}, path={path}, type={text.GetType().Name}, id={text.GetInstanceID()}, textHash={StableHash(displayedText):X8}, len={displayedText.Length}, cjk={ContainsCjk(displayedText)}, color={text.color}, canvasAlpha={GetParentCanvasGroupAlpha(text):0.###}, font={DescribeFont(font)}, shared={DescribeMaterial(shared)}, fontFallbacks={DescribeFallbackFonts(font)}, textInfo={DescribeTextInfo(text, displayedText)}, subMeshes={GetExistingSubMeshInfo(text)}, text='{Trim(displayedText)}'");
    }

    public static void RecordControlTips(HUDManager? hudManager, string stage)
    {
        if (!IsActive() || hudManager?.controlTipLines == null)
        {
            return;
        }

        for (var i = 0; i < hudManager.controlTipLines.Length; i++)
        {
            RecordTextSnapshot(hudManager.controlTipLines[i], $"{stage}.controlTipLines[{i}]");
        }
    }

    public static void RecordCursorTip(PlayerControllerB? player, string stage)
    {
        if (!IsActive() || player?.cursorTip == null)
        {
            return;
        }

        RecordTextSnapshot(player.cursorTip, $"{stage}.cursorTip");
    }

    public static void RecordLobbySlot(LobbySlot? slot, string stage)
    {
        if (!IsActive() || slot == null)
        {
            return;
        }

        RecordTextSnapshot(slot.LobbyName, $"{stage}.LobbyName");
        RecordTextSnapshot(slot.playerCount, $"{stage}.playerCount");
    }

    private static bool IsActive()
    {
        if (!_initialized || _enabled?.Value != true || _remainingBudget <= 0 || _disabledByRegexError)
        {
            return false;
        }

        RefreshRegexFilters();
        return !_disabledByRegexError;
    }

    private static void RefreshRegexFilters()
    {
        var pathPattern = _pathRegexConfig?.Value ?? string.Empty;
        if (!string.Equals(pathPattern, _lastPathPattern, StringComparison.Ordinal))
        {
            _pathRegex = CompileOptionalRegex(pathPattern, "FontFallbackAuditPathRegex");
            _lastPathPattern = pathPattern;
        }

        var textPattern = _textRegexConfig?.Value ?? string.Empty;
        if (!string.Equals(textPattern, _lastTextPattern, StringComparison.Ordinal))
        {
            _textRegex = CompileOptionalRegex(textPattern, "FontFallbackAuditTextRegex");
            _lastTextPattern = textPattern;
        }
    }

    private static Regex? CompileOptionalRegex(string pattern, string name)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is RegexMatchTimeoutException)
        {
            _disabledByRegexError = true;
            Plugin.Log.LogWarning($"{name} disabled font fallback audit because the regex is invalid or timed out: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static bool PassesFilters(string path, string text)
    {
        try
        {
            if (_pathRegex != null && !_pathRegex.IsMatch(path))
            {
                return false;
            }

            if (_textRegex != null && !_textRegex.IsMatch(text))
            {
                return false;
            }

            return _pathRegex != null || _textRegex != null || ContainsCjk(text) || LooksLikeWatchedPath(path);
        }
        catch (RegexMatchTimeoutException ex)
        {
            _disabledByRegexError = true;
            Plugin.Log.LogWarning($"FontFallbackAudit disabled after regex timeout: {ex.Message}");
            return false;
        }
    }

    private static bool LooksLikeWatchedPath(string path)
    {
        return ContainsOrdinalIgnoreCase(path, "ControlTip") ||
               ContainsOrdinalIgnoreCase(path, "cursorTip") ||
               ContainsOrdinalIgnoreCase(path, "Tip") ||
               ContainsOrdinalIgnoreCase(path, "Lobby") ||
               ContainsOrdinalIgnoreCase(path, "Menu") ||
               ContainsOrdinalIgnoreCase(path, "Terminal") ||
               ContainsOrdinalIgnoreCase(path, "MapScreen");
    }

    private static bool ShouldLog(string key)
    {
        if (!LoggedSnapshotKeys.Add(key))
        {
            return false;
        }

        var frame = Time.frameCount;
        if (frame != _lastFrame)
        {
            _lastFrame = frame;
            _loggedThisFrame = 0;
        }

        var maxPerFrame = Math.Max(1, _maxPerFrameConfig?.Value ?? 2);
        if (_loggedThisFrame >= maxPerFrame)
        {
            return false;
        }

        _loggedThisFrame++;
        _remainingBudget--;
        return true;
    }

    private static string DescribeTextInfo(TMP_Text text, string displayedText)
    {
        if (_includeTextInfoConfig?.Value != true)
        {
            return "disabled";
        }

        var info = text.textInfo;
        if (info == null)
        {
            return "null";
        }

        var cjk = new StringBuilder();
        var cjkCount = 0;
        for (var i = 0; i < info.characterCount && cjkCount < 8; i++)
        {
            var character = info.characterInfo[i];
            if (!IsCjkCodepoint(character.character))
            {
                continue;
            }

            if (cjk.Length > 0)
            {
                cjk.Append('|');
            }

            cjk.Append("U+");
            cjk.Append(((int)character.character).ToString("X4"));
            cjk.Append(":mat");
            cjk.Append(character.materialReferenceIndex);
            cjk.Append(":font=");
            cjk.Append(character.fontAsset != null ? character.fontAsset.name : "null");
            cjkCount++;
        }

        return $"chars={info.characterCount}, visible={info.characterCount}, materials={info.materialCount}, cjkSample={cjk}";
    }

    private static string GetExistingSubMeshInfo(TMP_Text text)
    {
        var builder = new StringBuilder();
        var uiSubMeshes = text.GetComponentsInChildren<TMP_SubMeshUI>(true);
        if (uiSubMeshes.Length > 0)
        {
            builder.Append("ui=");
            builder.Append(uiSubMeshes.Length);
            builder.Append('[');
            for (var i = 0; i < uiSubMeshes.Length && i < 4; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                var uiSubMesh = uiSubMeshes[i];
                builder.Append(uiSubMesh.name);
                builder.Append(":shared=");
                builder.Append(DescribeMaterial(uiSubMesh.sharedMaterial));
                builder.Append(":fallback=");
                builder.Append(DescribeMaterial(uiSubMesh.fallbackMaterial));
                builder.Append(":color=");
                builder.Append(uiSubMesh.color);
            }

            builder.Append(']');
        }

        var worldSubMeshes = text.GetComponentsInChildren<TMP_SubMesh>(true);
        if (worldSubMeshes.Length > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append(';');
            }

            builder.Append("world=");
            builder.Append(worldSubMeshes.Length);
            builder.Append('[');
            for (var i = 0; i < worldSubMeshes.Length && i < 4; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                var worldSubMesh = worldSubMeshes[i];
                builder.Append(worldSubMesh.name);
                builder.Append(":shared=");
                builder.Append(DescribeMaterial(worldSubMesh.sharedMaterial));
                builder.Append(":fallback=");
                builder.Append(DescribeMaterial(worldSubMesh.fallbackMaterial));
            }

            builder.Append(']');
        }

        return builder.Length == 0 ? "none" : builder.ToString();
    }

    private static string DescribeFont(TMP_FontAsset? font)
    {
        if (font == null)
        {
            return "null";
        }

        return $"{font.name}#{font.GetInstanceID()} atlas={font.atlasPopulationMode}";
    }

    private static string DescribeFallbackFonts(TMP_FontAsset? font)
    {
        var table = font?.fallbackFontAssetTable;
        if (table == null || table.Count == 0)
        {
            return "none";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < table.Count && i < 6; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            var fallback = table[i];
            builder.Append(fallback != null ? $"{fallback.name}#{fallback.GetInstanceID()}" : "null");
        }

        if (table.Count > 6)
        {
            builder.Append("|...");
        }

        return builder.ToString();
    }

    private static string DescribeMaterial(Material? material)
    {
        if (material == null)
        {
            return "null";
        }

        var mainTexture = material.mainTexture;
        return $"{material.name}#{material.GetInstanceID()}, tex={(mainTexture != null ? mainTexture.name + "#" + mainTexture.GetInstanceID() : "null")}, face={GetColorProperty(material, ShaderUtilities.ID_FaceColor)}, outline={GetColorProperty(material, ShaderUtilities.ID_OutlineColor)}, underlay={GetColorProperty(material, ShaderUtilities.ID_UnderlayColor)}";
    }

    private static string GetColorProperty(Material material, int propertyId)
    {
        return material.HasProperty(propertyId) ? material.GetColor(propertyId).ToString() : "N/A";
    }

    private static int GetFontId(TMP_FontAsset? font)
    {
        return font != null ? font.GetInstanceID() : 0;
    }

    private static int GetMaterialId(Material? material)
    {
        return material != null ? material.GetInstanceID() : 0;
    }

    private static int GetFallbackCount(TMP_FontAsset? font)
    {
        return font?.fallbackFontAssetTable?.Count ?? 0;
    }

    private static float GetParentCanvasGroupAlpha(Component component)
    {
        var alpha = 1f;
        for (var transform = component.transform; transform != null; transform = transform.parent)
        {
            if (transform.TryGetComponent<CanvasGroup>(out var group))
            {
                alpha *= group.alpha;
            }
        }

        return alpha;
    }

    private static string ClassifyRole(string path, string componentName)
    {
        if (ContainsOrdinalIgnoreCase(path, "LobbyName"))
        {
            return "LobbyName";
        }

        if (ContainsOrdinalIgnoreCase(path, "playerCount"))
        {
            return "LobbyPlayerCount";
        }

        if (ContainsOrdinalIgnoreCase(path, "cursorTip") || ContainsOrdinalIgnoreCase(componentName, "cursorTip"))
        {
            return "CursorTip";
        }

        if (ContainsOrdinalIgnoreCase(path, "ControlTip") || ContainsOrdinalIgnoreCase(path, "controlTip"))
        {
            return "ControlTip";
        }

        if (ContainsOrdinalIgnoreCase(path, "Terminal"))
        {
            return "Terminal";
        }

        return "General";
    }

    private static string BuildPath(Transform? transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var names = new string?[32];
        var count = 0;
        for (var current = transform; current != null && count < names.Length; current = current.parent)
        {
            names[count++] = current.name;
        }

        var builder = new StringBuilder();
        for (var i = count - 1; i >= 0; i--)
        {
            if (builder.Length > 0)
            {
                builder.Append('/');
            }

            builder.Append(names[i]);
        }

        return builder.ToString();
    }

    private static bool ContainsCjk(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (IsCjkCodepoint(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjkCodepoint(char ch)
    {
        return (ch >= '\u3400' && ch <= '\u9fff') ||
               (ch >= '\uf900' && ch <= '\ufaff');
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string needle)
    {
        return source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            return hash;
        }
    }

    private static string Trim(string input)
    {
        input = input.Replace('\r', ' ').Replace('\n', ' ');
        return input.Length <= 120 ? input : input.Substring(0, 120);
    }
}
