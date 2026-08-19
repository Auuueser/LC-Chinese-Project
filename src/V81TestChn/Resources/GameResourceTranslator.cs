using UnityEngine;

namespace V81TestChn;

internal static class GameResourceTranslator
{
    public static int TranslateLoadedResources()
    {
        var translated = 0;

        foreach (var node in Resources.FindObjectsOfTypeAll<TerminalNode>())
        {
            if (node == null)
            {
                continue;
            }

            OriginalResourceStateService.CaptureTerminalNode(node);
            translated += Translate(ref node.displayText);
            translated += Translate(ref node.creatureName);
        }

        // Terminal.TextPostProcess builds the bestiary and log catalogues from
        // creatureName. Apply the bilingual presentation only after all nodes
        // have been captured and translated, preserving the original English
        // names for the gray aliases and leaving command keywords untouched.
        foreach (var terminal in Resources.FindObjectsOfTypeAll<Terminal>())
        {
            translated += TerminalCatalogueLocalizationService.Apply(terminal);
        }

        foreach (var item in Resources.FindObjectsOfTypeAll<Item>())
        {
            if (item == null)
            {
                continue;
            }

            OriginalResourceStateService.CaptureItem(item);
            translated += ItemIdentityCompatibilityService.TranslateResourceItemName(item);
            if (item.toolTips == null)
            {
                continue;
            }

            for (var i = 0; i < item.toolTips.Length; i++)
            {
                translated += Translate(ref item.toolTips[i]);
            }
        }

        foreach (var level in Resources.FindObjectsOfTypeAll<SelectableLevel>())
        {
            if (level == null)
            {
                continue;
            }

            OriginalResourceStateService.CaptureSelectableLevel(level);
            translated += Translate(ref level.PlanetName);
            translated += Translate(ref level.LevelDescription);
            translated += TranslatePlanetRiskLevel(ref level.riskLevel);
            translated += Translate(ref level.levelIconString);
        }

        foreach (var enemy in Resources.FindObjectsOfTypeAll<EnemyType>())
        {
            if (enemy == null)
            {
                continue;
            }

            OriginalResourceStateService.CaptureEnemyType(enemy);
            translated += Translate(ref enemy.enemyName);
        }

        return translated;
    }

    private static int Translate(ref string value)
    {
        if (TranslationService.TryTranslate(value, out var translated))
        {
            value = translated;
            return 1;
        }

        return 0;
    }

    private static int TranslatePlanetRiskLevel(ref string value)
    {
        if (TranslationService.TryTranslateKnownDynamicTextTargeted(DynamicTextDomain.PlanetInfo, value, out var translated))
        {
            value = translated;
            return 1;
        }

        if (TranslationService.TryTranslate(value, out translated))
        {
            value = translated;
            return 1;
        }

        return 0;
    }
}
