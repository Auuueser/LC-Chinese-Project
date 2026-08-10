using System;
using System.Collections.Generic;
using GameNetcodeStuff;

namespace V81TestChn;

internal static class HudInteractionLocalizationService
{
    private const string InspectControlTip = "Inspect: [Z]";

    private static readonly HashSet<string> VanillaInspectableItemsMissingControlTip = new(StringComparer.OrdinalIgnoreCase)
    {
        "BabyKiwiEgg",
        "MagnifyingGlass",
        "Mug",
        "Toothpaste",
        "TZPInhalant",
        "WalkieTalkie"
    };

    public static void ApplyDisplayTip(ref string headerText, ref string bodyText)
    {
        headerText = TranslateDisplayTipText(headerText);
        bodyText = TranslateDisplayTipText(bodyText);
    }

    private static string TranslateDisplayTipText(string source)
    {
        if (ExternalEnglishCompatibilityService.TryTranslateDisplayTipText(source, out var externalTranslated))
        {
            return externalTranslated;
        }

        if (ExternalEnglishCompatibilityService.MightContainDisplayTipCompatibilityText(source))
        {
            return source;
        }

        return TargetedUiTranslator.TranslateDynamic(source);
    }

    public static void ApplyDisplayStatusEffect(ref string statusEffect)
    {
        statusEffect = TargetedUiTranslator.TranslateDynamic(statusEffect);
    }

    public static void ApplyControlTipPrefix(ref string changeTo)
    {
        changeTo = TargetedUiTranslator.TranslateDynamicTargeted(changeTo, DynamicTextDomain.HudControlTip);
    }

    public static void ApplyControlTipPostfix(HUDManager? hud, string reason)
    {
        TargetedUiTranslator.TranslateHudControlTips(hud, reason);
        FontFallbackAuditService.RecordControlTips(hud, reason);
    }

    public static void ApplyControlTipMultiplePrefix(ref string[] allLines, Item itemProperties)
    {
        TargetedUiTranslator.TranslateItem(itemProperties);
        if (allLines == null)
        {
            return;
        }

        for (var i = 0; i < allLines.Length; i++)
        {
            allLines[i] = TargetedUiTranslator.TranslateDynamicTargeted(allLines[i], DynamicTextDomain.HudControlTip);
        }
    }

    public static void ApplyGrabbableItem(GrabbableObject? grabbable)
    {
        if (grabbable == null)
        {
            return;
        }

        EnsureVanillaInspectControlTip(grabbable.itemProperties);
        TargetedUiTranslator.TranslateItem(grabbable.itemProperties);
    }

    private static void EnsureVanillaInspectControlTip(Item? item)
    {
        if (item == null ||
            !item.canBeInspected ||
            !VanillaInspectableItemsMissingControlTip.Contains(item.name))
        {
            return;
        }

        var toolTips = item.toolTips ?? Array.Empty<string>();
        for (var i = 0; i < toolTips.Length; i++)
        {
            if (IsInspectControlTip(toolTips[i]))
            {
                return;
            }
        }

        // Capture the pristine array before adding the missing vanilla HUD prompt.
        OriginalResourceStateService.CaptureItem(item);

        var expanded = new string[toolTips.Length + 1];
        Array.Copy(toolTips, expanded, toolTips.Length);
        expanded[expanded.Length - 1] = InspectControlTip;
        item.toolTips = expanded;
    }

    private static bool IsInspectControlTip(string? controlTip)
    {
        if (string.IsNullOrWhiteSpace(controlTip))
        {
            return false;
        }

        var trimmed = controlTip.TrimStart();
        return trimmed.StartsWith("Inspect", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("查看", StringComparison.Ordinal) ||
               trimmed.StartsWith("检查", StringComparison.Ordinal);
    }

    public static void ApplyGrabbableControlTips(GrabbableObject? grabbable, string reason)
    {
        if (grabbable is StunGrenadeItem stunGrenade)
        {
            TargetedUiTranslator.TranslateStunGrenadeControlTip(stunGrenade, $"{reason}.StunGrenadeItem");
            return;
        }

        TargetedUiTranslator.TranslateHudControlTips(HUDManager.Instance, reason);
    }

    public static void ApplyStunGrenadeControlTip(StunGrenadeItem? grenade, string reason)
    {
        TargetedUiTranslator.TranslateStunGrenadeControlTip(grenade, reason);
    }

    public static void ApplyPlayerCursorTip(PlayerControllerB? player, string reason)
    {
        TargetedUiTranslator.TranslatePlayerCursorTip(player, reason);
        FontFallbackAuditService.RecordCursorTip(player, reason);
    }

    public static void ApplyVehicleStaticTexts(VehicleController? vehicle, string reason)
    {
        TargetedUiTranslator.TranslateVehicleStaticTexts(vehicle, reason);
    }
}
