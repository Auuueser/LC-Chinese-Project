using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace V81TestChn;

internal static class RuntimeIconsCompatibilityService
{
    private static bool _runtimeIconsLoaded;
    private static bool _preserveLogWritten;

    static RuntimeIconsCompatibilityService()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    public static int TranslateResourceItemName(Item? item)
    {
        return TryTranslateItemName(item) ? 1 : 0;
    }

    public static bool TryTranslateItemName(Item? item)
    {
        if (item == null)
        {
            return false;
        }

        OriginalResourceStateService.CaptureItem(item);
        var originalName = OriginalResourceStateService.GetOriginalItemName(item);
        if (ShouldPreserveItemNames())
        {
            RestoreItemName(item, originalName);
            return false;
        }

        if (TranslationService.TryTranslate(item.itemName, out var translated))
        {
            item.itemName = translated;
            return true;
        }

        return false;
    }

    private static void RestoreItemName(Item item, string originalName)
    {
        if (item.itemName != originalName)
        {
            item.itemName = originalName;
        }
    }

    private static bool ShouldPreserveItemNames()
    {
        if (!IsRuntimeIconsLoaded())
        {
            return false;
        }

        if (!_preserveLogWritten)
        {
            _preserveLogWritten = true;
            // Plugin.Log.LogInfo("RuntimeIcons compatibility enabled; preserving original Item.itemName values.");
        }

        return true;
    }

    private static bool IsRuntimeIconsLoaded()
    {
        if (_runtimeIconsLoaded)
        {
            return true;
        }

        if (IsRuntimeIconsPluginInfoLoaded())
        {
            _runtimeIconsLoaded = true;
            return true;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (IsRuntimeIconsAssembly(assembly))
            {
                _runtimeIconsLoaded = true;
                return true;
            }
        }

        return false;
    }

    public static void Clear()
    {
        _runtimeIconsLoaded = default;
        _preserveLogWritten = false;
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        if (IsRuntimeIconsAssembly(args.LoadedAssembly))
        {
            _runtimeIconsLoaded = true;
        }
    }

    private static bool IsRuntimeIconsPluginInfoLoaded()
    {
        try
        {
            foreach (var pluginInfo in Chainloader.PluginInfos)
            {
                if (IsKnownIconProvider(pluginInfo.Key) ||
                    IsKnownIconProvider(pluginInfo.Value.Metadata.GUID) ||
                    IsKnownIconProvider(pluginInfo.Value.Metadata.Name))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsRuntimeIconsAssembly(Assembly? assembly)
    {
        return IsKnownIconProvider(assembly?.GetName().Name);
    }

    private static bool IsKnownIconProvider(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.IndexOf("RuntimeIcons", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("HoneeItemIcons", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
