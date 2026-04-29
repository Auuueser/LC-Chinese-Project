using System;
using System.Collections.Generic;
using UnityEngine;

namespace V81TestChn;

internal static class RuntimeIconsCompatibilityService
{
    private static readonly Dictionary<int, string> OriginalItemNames = new();
    private static bool? _runtimeIconsLoaded;
    private static bool _preserveLogWritten;

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

        var originalName = CaptureOriginalItemName(item);
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

    private static string CaptureOriginalItemName(Item item)
    {
        var id = item.GetInstanceID();
        if (!OriginalItemNames.TryGetValue(id, out var originalName))
        {
            originalName = item.itemName ?? string.Empty;
            OriginalItemNames[id] = originalName;
        }

        return originalName;
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
        if (_runtimeIconsLoaded.HasValue)
        {
            return _runtimeIconsLoaded.Value;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (name.IndexOf("RuntimeIcons", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _runtimeIconsLoaded = true;
                return true;
            }
        }

        _runtimeIconsLoaded = false;
        return false;
    }
}
