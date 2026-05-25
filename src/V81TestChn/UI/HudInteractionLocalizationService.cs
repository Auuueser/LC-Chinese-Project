using GameNetcodeStuff;

namespace V81TestChn;

internal static class HudInteractionLocalizationService
{
    public static void ApplyDisplayTip(ref string headerText, ref string bodyText)
    {
        headerText = TargetedUiTranslator.TranslateDynamic(headerText);
        bodyText = TargetedUiTranslator.TranslateDynamic(bodyText);
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

        TargetedUiTranslator.TranslateItem(grabbable.itemProperties);
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
