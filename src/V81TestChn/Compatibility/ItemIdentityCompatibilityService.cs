using UnityEngine;

namespace V81TestChn;

internal static class ItemIdentityCompatibilityService
{
    public static void Initialize()
    {
    }

    public static void Shutdown()
    {
        Clear();
    }

    public static int TranslateResourceItemName(Item? item)
    {
        PreserveItemName(item);
        return 0;
    }

    public static bool TryTranslateItemName(Item? item)
    {
        PreserveItemName(item);
        return false;
    }

    public static void Clear()
    {
    }

    private static void PreserveItemName(Item? item)
    {
        if (item == null)
        {
            return;
        }

        OriginalResourceStateService.CaptureItem(item);
        var originalName = OriginalResourceStateService.GetOriginalItemName(item);
        if (item.itemName != originalName)
        {
            item.itemName = originalName;
        }
    }
}
