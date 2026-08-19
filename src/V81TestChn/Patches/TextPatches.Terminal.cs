using HarmonyLib;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static void TerminalStartPostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        // Process the serialized vanilla catalogue after Terminal.Start and
        // other Start postfixes, before the player can interact with it.
        TerminalCatalogueLocalizationService.Apply(__instance);
    }

    [HarmonyPatch(typeof(Terminal), "SetItemSales")]
    [HarmonyPrefix]
    private static void TerminalSetItemSalesPrefix(Terminal __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe Terminal.SetItemSales enter items={__instance.buyableItemsList?.Length ?? -1} vehicles={__instance.buyableVehicles?.Length ?? -1} sales={__instance.itemSalesPercentages?.Length ?? -1}");
    }

    [HarmonyPatch(typeof(Terminal), "SetItemSales")]
    [HarmonyPostfix]
    private static void TerminalSetItemSalesPostfix(Terminal __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe Terminal.SetItemSales exit items={__instance.buyableItemsList?.Length ?? -1} vehicles={__instance.buyableVehicles?.Length ?? -1} sales={__instance.itemSalesPercentages?.Length ?? -1}");
    }

    [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
    [HarmonyPostfix]
    private static void TerminalTextPostProcessPostfix(TerminalNode node, ref string __result)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TerminalScreenLocalizationService.ApplyTextPostProcess(node, ref __result);
    }

    [HarmonyPatch(typeof(Terminal), "LoadNewNode")]
    [HarmonyPostfix]
    private static void TerminalLoadNewNodePostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        Plugin.LogPatchEntry("Terminal.LoadNewNode");
        TerminalScreenLocalizationService.ApplyScreenFallback(__instance, "Terminal.LoadNewNode");
    }

    [HarmonyPatch(typeof(Terminal), "OnSubmit")]
    [HarmonyPostfix]
    private static void TerminalOnSubmitPostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TerminalScreenLocalizationService.ApplyFontFallback(__instance);
    }

    [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
    [HarmonyPostfix]
    private static void TerminalParsePlayerSentencePostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TerminalScreenLocalizationService.ApplyFontFallback(__instance);
    }

    [HarmonyPatch(typeof(Terminal), "PlayBroadcastCodeEffect")]
    [HarmonyPostfix]
    private static void TerminalPlayBroadcastCodeEffectPostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TerminalBroadcastLocalizationService.ApplyToAnimator(__instance.codeBroadcastAnimator);
    }

    [HarmonyPatch(typeof(Terminal), "loadTextAnimation")]
    [HarmonyPostfix]
    private static void TerminalLoadTextAnimationPostfix(Terminal __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        TerminalScreenLocalizationService.ApplyFontFallback(__instance);
    }

    [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
    [HarmonyPostfix]
    private static void TerminalBeginUsingPostfix(Terminal __instance)
    {
        // Normally a cache-only pass. It remains as a compatibility fallback
        // for terminal nodes appended by another mod after Terminal.Start.
        TerminalCatalogueLocalizationService.Apply(__instance);
        ChatEmojiPasteService.RegisterTerminalInput(__instance);
        ChatEmojiSpriteService.ApplyToText(__instance?.screenText?.textComponent);
        TerminalScreenLocalizationService.ApplyFontFallback(__instance);
    }

    private static bool TerminalTextChangedPrefix(Terminal __instance, string newText)
    {
        return !ChatEmojiPasteService.TryHandleTerminalTextChanged(__instance, newText);
    }
}
