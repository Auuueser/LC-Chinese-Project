using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace V81TestChn;

internal static class FontSelectionService
{
    internal const string DefaultChoice = "默认字体";
    private static string _fontDirectory = string.Empty;
    private static ConfigEntry<string>? _selection;
    private static FontChoices? _choices;
    private static object? _dropdown;
    private static Type? _controllerType;
    private static string? _pending;
    private static string _activeChoice = DefaultChoice;
    private static bool _restoring;

    internal static string? SelectedFontPath => _selection != null && _selection.Value != DefaultChoice &&
        _choices != null && _choices.IsValid(_selection.Value)
            ? Path.Combine(_fontDirectory, _selection.Value) : null;

    internal static void Initialize(string pluginDir, ConfigFile config)
    {
        _fontDirectory = Path.Combine(pluginDir, "fonts");
        _choices = new FontChoices(Discover());
        _selection = config.Bind(ConfigSections.FontCompatibility, "ChineseFont", DefaultChoice,
            new ConfigDescription("中文字体。将 TTF/OTF/TTC 文件放入插件 fonts 文件夹；启动游戏或重新打开模组配置时检测。应用选择后立即切换，无需改名或删除默认字体。缺字或损坏的字体会被拒绝。", _choices));
        _activeChoice = _selection.Value;
        _selection.SettingChanged += OnSelectionChanged;
        RegisterDropdown();
        config.Save();
    }

    private static string[] Discover()
    {
        try
        {
            return new[] { DefaultChoice }.Concat(Directory.Exists(_fontDirectory)
                ? Directory.EnumerateFiles(_fontDirectory).Where(p =>
                    new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                    .Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n))
                    .Cast<string>().OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                : Enumerable.Empty<string>()).ToArray();
        }
        catch (IOException ex) { Plugin.Log.LogWarning($"Font discovery failed: {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { Plugin.Log.LogWarning($"Font discovery failed: {ex.Message}"); }
        return new[] { DefaultChoice };
    }

    internal static void RefreshChoices()
    {
        if (_choices == null || _selection == null) return;
        var names = Discover();
        if (_choices.Names.SequenceEqual(names, StringComparer.Ordinal)) return;
        _choices.Names = names;
        // LethalConfig creates the dropdown controller when the mod is selected.
        // Update its source list before that view is constructed.
        _dropdown?.GetType().GetField("Values")?.SetValue(_dropdown, names.ToList());
        if (!_choices.IsValid(_selection.Value)) _selection.Value = DefaultChoice;
        _selection.ConfigFile.Save();
        if (_dropdown != null && _controllerType != null)
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var itemField = _controllerType.GetField("BaseConfigItem", flags);
                var refresh = _controllerType.GetMethod("OnSetConfigItem", flags);
                foreach (var controller in UnityEngine.Resources.FindObjectsOfTypeAll(_controllerType))
                    if (ReferenceEquals(itemField?.GetValue(controller), _dropdown)) refresh?.Invoke(controller, null);
            }
            catch (Exception ex) { Plugin.Log.LogWarning($"Font dropdown refresh failed: {ex.Message}"); }
        }
    }

    private static void RegisterDropdown()
    {
        if (!Chainloader.PluginInfos.TryGetValue("ainavt.lc.lethalconfig", out var plugin)) return;
        try
        {
            var assembly = plugin.Instance.GetType().Assembly;
            _controllerType = assembly.GetType("LethalConfig.MonoBehaviours.Components.TextDropDownController");
            var type = assembly.GetType("LethalConfig.ConfigItems.TextDropDownConfigItem", true)!;
            // The bool constructor maps to RequiresRestart = false.
            _dropdown = Activator.CreateInstance(type, _selection, false);
            var baseType = assembly.GetType("LethalConfig.ConfigItems.BaseConfigItem", true)!;
            assembly.GetType("LethalConfig.LethalConfigManager", true)!
                .GetMethod("AddConfigItem", new[] { baseType, typeof(Assembly) })!
                .Invoke(null, new[] { _dropdown, typeof(Plugin).Assembly });
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"Live font dropdown registration failed: {ex.Message}"); }
    }

    private static void OnSelectionChanged(object sender, EventArgs args)
    {
        if (!_restoring && _selection != null) Volatile.Write(ref _pending, _selection.Value);
    }

    // Config reload may originate off-thread; Unity font work always runs here
    // on the main thread, once per requested change, never as a scanning loop.
    internal static void ApplyPendingSelection()
    {
        var requested = Interlocked.Exchange(ref _pending, null);
        if (requested == null || _selection == null || requested == _activeChoice) return;
        var path = requested == DefaultChoice ? null : SelectedFontPath;
        if ((requested == DefaultChoice || path != null) && FontFallbackService.TrySwitchFontFile(path))
        {
            _activeChoice = requested;
            Plugin.Log.LogInfo($"Chinese font switched to: {requested}");
            return;
        }
        Plugin.Log.LogWarning($"Chinese font '{requested}' could not be loaded; retaining '{_activeChoice}'.");
        _restoring = true;
        try { _selection.Value = _activeChoice; }
        finally { _restoring = false; }
    }

    internal static void ResetFailedStartupSelection()
    {
        if (_selection == null) return;
        _restoring = true;
        try { _selection.Value = DefaultChoice; _activeChoice = DefaultChoice; }
        finally { _restoring = false; }
    }

    internal static void Shutdown()
    {
        if (_selection != null) _selection.SettingChanged -= OnSelectionChanged;
        _selection = null;
        _dropdown = null;
        _controllerType = null;
        _pending = null;
    }

    private sealed class FontChoices : AcceptableValueList<string>
    {
        internal string[] Names;
        internal FontChoices(string[] names) : base(names) { Names = names; }
        public override bool IsValid(object value) => value is string name && Names.Contains(name, StringComparer.Ordinal);
        public override object Clamp(object value) => IsValid(value) ? value : DefaultChoice;
        public override string ToDescriptionString() => "# Acceptable values: " + string.Join(", ", Names);
    }
}
