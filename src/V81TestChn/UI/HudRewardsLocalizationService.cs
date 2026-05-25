using System;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static class HudRewardsLocalizationService
{
    public static void ApplyCreditsEarning(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        ApplyTargetedDirectTextTranslation(hud.moneyRewardsTotalText, reason, DynamicTextDomain.HudRewards);
        ApplyTargetedDirectTextTranslation(hud.moneyRewardsListText, reason, DynamicTextDomain.HudRewards);
        var root = hud.moneyRewardsAnimator == null ? hud.moneyRewardsTotalText?.transform.parent?.gameObject : hud.moneyRewardsAnimator.gameObject;
        TargetedUiTranslator.TranslateRoot(root, reason);
    }

    public static void ApplyNewScrapFound(HUDManager? hud, string reason)
    {
        TargetedUiTranslator.TranslateHudScrapItemBoxes(hud, reason);
    }

    private static bool ApplyTargetedDirectTextTranslation(TMP_Text? text, string reason, DynamicTextDomain domain)
    {
        if (text == null)
        {
            return false;
        }

        var original = text.text;
        var translated = TargetedUiTranslator.TranslateDynamicTargeted(original, domain);
        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            RuntimeTextCollector.Record(text, original);
            return false;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, translated);
        Plugin.ReportTranslationHit();
        return true;
    }
}
