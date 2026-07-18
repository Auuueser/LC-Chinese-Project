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
            "Maximum entries for global TMP hook translation/no-op/color caches. Larger modpacks benefit from the default 16384.");
        _componentTextCacheLimit = BindInt(
            config,
            "LargeModpackComponentTextCacheLimit",
            DefaultComponentTextCacheLimit,
            1024,
            32768,
            "Maximum entries for targeted UI component text caches. Larger modpacks benefit from the default 16384.");
        _hudScannerCacheLimit = BindInt(
            config,
            "LargeModpackHudScannerCacheLimit",
            DefaultHudScannerCacheLimit,
            1024,
            32768,
            "Maximum entries for HUD scanner text and node caches. Larger modpacks benefit from the default 16384.");
        _externalCompatibilityCacheLimit = BindInt(
            config,
            "LargeModpackExternalCompatibilityCacheLimit",
            DefaultExternalCompatibilityCacheLimit,
            512,
            16384,
            "Maximum entries for generic external English compatibility runtime caches. Default 4096 keeps memory bounded.");
        _fontFallbackCacheLimit = BindInt(
            config,
            "LargeModpackFontFallbackCacheLimit",
            DefaultFontFallbackCacheLimit,
            1024,
            32768,
            "Maximum entries for font fallback/style repair caches. Larger modpacks benefit from the default 16384.");
        _menuTranslationWorkBudgetPerFrame = BindInt(
            config,
            "MenuTranslationWorkBudgetPerFrame",
            DefaultMenuTranslationWorkBudgetPerFrame,
            4,
            64,
            "Maximum targeted menu text components translated per frame. Lower values reduce spikes; higher values finish menu localization sooner.");
        _chatTranslationMaxEntriesPerFrame = BindInt(
            config,
            "ChatTranslationMaxEntriesPerFrame",
            DefaultChatTranslationMaxEntriesPerFrame,
            4,
            256,
            "Maximum chat history entries validated per frame. Visible snapshots use a one-shot line cap of four times this value to avoid stale-work starvation.");
        _chatTranslationMaxCharactersPerFrame = BindInt(
            config,
            "ChatTranslationMaxCharactersPerFrame",
            DefaultChatTranslationMaxCharactersPerFrame,
            512,
            32768,
            "Maximum characters in a visible chat snapshot or history entry translated in one work item. Oversized third-party output remains unchanged to prevent frame spikes.");
        _enableTargetedUiStyleRepairFastGate = config.Bind(
            ConfigSections.Performance,
            "EnableTargetedUiStyleRepairFastGate",
            DefaultEnableTargetedUiStyleRepairFastGate,
            "Skip repeated targeted UI style repairs for the same component/text/style state. Disable if a UI needs repeated style repair.");
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
                $"{description} Range: {min}-{max}. Changes apply immediately.",
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
