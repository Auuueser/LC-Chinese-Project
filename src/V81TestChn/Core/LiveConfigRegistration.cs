using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace V81TestChn;

internal static class LiveConfigRegistration
{
    internal static void RegisterBool(ConfigEntry<bool> entry, bool requiresRestart = false)
        => Register(entry, "BoolCheckBoxConfigItem", requiresRestart);

    internal static void RegisterFloat(ConfigEntry<float> entry, bool requiresRestart = false)
        => Register(entry, "FloatSliderConfigItem", requiresRestart);

    internal static void RegisterEnum<T>(ConfigEntry<T> entry) where T : Enum
        => Register(entry, "EnumDropDownConfigItem`1", false, typeof(T));

    private static void Register(ConfigEntryBase entry, string itemType, bool requiresRestart, Type? enumType = null)
    {
        if (!Chainloader.PluginInfos.TryGetValue("ainavt.lc.lethalconfig", out var plugin)) return;
        try
        {
            var assembly = plugin.Instance.GetType().Assembly;
            var type = assembly.GetType("LethalConfig.ConfigItems." + itemType, true)!;
            if (enumType != null) type = type.MakeGenericType(enumType);
            var item = Activator.CreateInstance(type, entry, requiresRestart);
            var baseType = assembly.GetType("LethalConfig.ConfigItems.BaseConfigItem", true)!;
            assembly.GetType("LethalConfig.LethalConfigManager", true)!
                .GetMethod("AddConfigItem", new[] { baseType, typeof(Assembly) })!
                .Invoke(null, new[] { item, typeof(Plugin).Assembly });
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"Config setting registration failed: {ex.Message}"); }
    }
}
