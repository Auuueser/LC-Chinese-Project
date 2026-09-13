using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace V81TestChn;

internal static class LiveConfigRegistration
{
    internal static void RegisterBool(ConfigEntry<bool> entry)
    {
        if (!Chainloader.PluginInfos.TryGetValue("ainavt.lc.lethalconfig", out var plugin)) return;
        try
        {
            var assembly = plugin.Instance.GetType().Assembly;
            var type = assembly.GetType("LethalConfig.ConfigItems.BoolCheckBoxConfigItem", true)!;
            var item = Activator.CreateInstance(type, entry, false);
            var baseType = assembly.GetType("LethalConfig.ConfigItems.BaseConfigItem", true)!;
            assembly.GetType("LethalConfig.LethalConfigManager", true)!
                .GetMethod("AddConfigItem", new[] { baseType, typeof(Assembly) })!
                .Invoke(null, new[] { item, typeof(Plugin).Assembly });
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"Live terminal setting registration failed: {ex.Message}"); }
    }
}
