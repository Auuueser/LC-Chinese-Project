using System;
using BepInEx.Configuration;

namespace V81TestChn;

internal static class RuntimePerformanceSettings
{
    private const int DefaultTmpHookCacheLimit = 16384;
    private const int DefaultComponentTextCacheLimit = 16384;
    private const int DefaultHudScannerCacheLimit = 16384;
    private const int DefaultExternalCompatibilityCacheLimit = 4096;
    private const int DefaultFontFallbackCacheLimit = 16384;
    private const int DefaultMenuTranslationWorkBudgetPerFrame = 12;
    private const int DefaultChatTranslationMaxEntriesPerFrame = 32;
    private const int DefaultChatTranslationMaxCharactersPerFrame = 4096;
    private const bool DefaultEnableTargetedUiStyleRepairFastGate = true;

    private static ConfigEntry<int>? _tmpHookCacheLimit;
    private static ConfigEntry<int>? _componentTextCacheLimit;
    private static ConfigEntry<int>? _hudScannerCacheLimit;
    private static ConfigEntry<int>? _externalCompatibilityCacheLimit;
    private static ConfigEntry<int>? _fontFallbackCacheLimit;
    private static ConfigEntry<int>? _menuTranslationWorkBudgetPerFrame;
    private static ConfigEntry<int>? _chatTranslationMaxEntriesPerFrame;
    private static ConfigEntry<int>? _chatTranslationMaxCharactersPerFrame;
    private static ConfigEntry<bool>? _enableTargetedUiStyleRepairFastGate;

    public static int TmpHookCacheLimit { get; private set; } = DefaultTmpHookCacheLimit;
    public static int ComponentTextCacheLimit { get; private set; } = DefaultComponentTextCacheLimit;
    public static int HudScannerCacheLimit { get; private set; } = DefaultHudScannerCacheLimit;
    public static int ExternalCompatibilityCacheLimit { get; private set; } = DefaultExternalCompatibilityCacheLimit;
    public static int FontFallbackCacheLimit { get; private set; } = DefaultFontFallbackCacheLimit;
    public static int MenuTranslationWorkBudgetPerFrame { get; private set; } = DefaultMenuTranslationWorkBudgetPerFrame;
    public static int ChatTranslationMaxEntriesPerFrame { get; private set; } = DefaultChatTranslationMaxEntriesPerFrame;
    public static int ChatTranslationMaxCharactersPerFrame { get; private set; } = DefaultChatTranslationMaxCharactersPerFrame;
    public static bool EnableTargetedUiStyleRepairFastGate { get; private set; } = DefaultEnableTargetedUiStyleRepairFastGate;

    public static void Initialize(ConfigFile config)
    {
        _tmpHookCacheLimit = BindInt(
            config,
            "LargeModpackTmpHookCacheLimit",
            DefaultTmpHookCacheLimit,
            1024,
            32768,
            "全局 TMP 文本翻译、无需处理结果及颜色缓存的条数上限。大型整合包建议保持默认值 16384。");
        _componentTextCacheLimit = BindInt(
            config,
            "LargeModpackComponentTextCacheLimit",
            DefaultComponentTextCacheLimit,
            1024,
            32768,
            "定向界面组件文本缓存的条数上限。大型整合包建议保持默认值 16384。");
        _hudScannerCacheLimit = BindInt(
            config,
            "LargeModpackHudScannerCacheLimit",
            DefaultHudScannerCacheLimit,
            1024,
            32768,
            "HUD 扫描文本与节点缓存的条数上限。大型整合包建议保持默认值 16384。");
        _externalCompatibilityCacheLimit = BindInt(
            config,
            "LargeModpackExternalCompatibilityCacheLimit",
            DefaultExternalCompatibilityCacheLimit,
            512,
            16384,
            "通用第三方英文兼容缓存的条数上限。默认 4096，限制内存占用。");
        _fontFallbackCacheLimit = BindInt(
            config,
            "LargeModpackFontFallbackCacheLimit",
            DefaultFontFallbackCacheLimit,
            1024,
            32768,
            "字体回退与样式修复缓存的条数上限。大型整合包建议保持默认值 16384。");
        _menuTranslationWorkBudgetPerFrame = BindInt(
            config,
            "MenuTranslationWorkBudgetPerFrame",
            DefaultMenuTranslationWorkBudgetPerFrame,
            4,
            64,
            "每帧定向翻译的菜单文本组件数量上限。调低可减少单帧负担，调高可更快补齐菜单汉化。");
        _chatTranslationMaxEntriesPerFrame = BindInt(
            config,
            "ChatTranslationMaxEntriesPerFrame",
            DefaultChatTranslationMaxEntriesPerFrame,
            4,
            256,
            "每帧检查的聊天历史条数上限。当前可见内容单次最多处理此值的四倍行数，避免持续被旧任务延后。");
        _chatTranslationMaxCharactersPerFrame = BindInt(
            config,
            "ChatTranslationMaxCharactersPerFrame",
            DefaultChatTranslationMaxCharactersPerFrame,
            512,
            32768,
            "单次翻译可见聊天内容或历史记录的最大字符数。超长第三方输出保持原样，避免单帧负担过重。");
        _enableTargetedUiStyleRepairFastGate = config.Bind(
            ConfigSections.Performance,
            "EnableTargetedUiStyleRepairFastGate",
            DefaultEnableTargetedUiStyleRepairFastGate,
            "组件、文本和样式状态未变化时，跳过重复的定向界面样式修复。若界面需要反复修复样式，可关闭此项。");
        _enableTargetedUiStyleRepairFastGate.SettingChanged += OnSettingsChanged;

        RefreshFastValues();
    }

    private static ConfigEntry<int> BindInt(
        ConfigFile config,
        string key,
        int defaultValue,
        int min,
        int max,
        string description)
    {
        var entry = config.Bind(
            ConfigSections.Performance,
            key,
            defaultValue,
            new ConfigDescription(
                $"{description} 范围：{min}–{max}。修改后立即生效。",
                new AcceptableValueRange<int>(min, max)));
        entry.SettingChanged += OnSettingsChanged;
        return entry;
    }

    private static void OnSettingsChanged(object sender, EventArgs args)
    {
        RefreshFastValues();
    }

    private static void RefreshFastValues()
    {
        TmpHookCacheLimit = Clamp(_tmpHookCacheLimit?.Value ?? DefaultTmpHookCacheLimit, 1024, 32768);
        ComponentTextCacheLimit = Clamp(_componentTextCacheLimit?.Value ?? DefaultComponentTextCacheLimit, 1024, 32768);
        HudScannerCacheLimit = Clamp(_hudScannerCacheLimit?.Value ?? DefaultHudScannerCacheLimit, 1024, 32768);
        ExternalCompatibilityCacheLimit = Clamp(_externalCompatibilityCacheLimit?.Value ?? DefaultExternalCompatibilityCacheLimit, 512, 16384);
        FontFallbackCacheLimit = Clamp(_fontFallbackCacheLimit?.Value ?? DefaultFontFallbackCacheLimit, 1024, 32768);
        MenuTranslationWorkBudgetPerFrame = Clamp(_menuTranslationWorkBudgetPerFrame?.Value ?? DefaultMenuTranslationWorkBudgetPerFrame, 4, 64);
        ChatTranslationMaxEntriesPerFrame = Clamp(_chatTranslationMaxEntriesPerFrame?.Value ?? DefaultChatTranslationMaxEntriesPerFrame, 4, 256);
        ChatTranslationMaxCharactersPerFrame = Clamp(_chatTranslationMaxCharactersPerFrame?.Value ?? DefaultChatTranslationMaxCharactersPerFrame, 512, 32768);
        EnableTargetedUiStyleRepairFastGate = _enableTargetedUiStyleRepairFastGate?.Value ?? DefaultEnableTargetedUiStyleRepairFastGate;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
