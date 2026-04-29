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

            translated += Translate(ref node.displayText);
            translated += Translate(ref node.creatureName);
        }

        foreach (var item in Resources.FindObjectsOfTypeAll<Item>())
        {
            if (item == null)
            {
                continue;
            }

            translated += RuntimeIconsCompatibilityService.TranslateResourceItemName(item);
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

            translated += Translate(ref level.PlanetName);
            translated += Translate(ref level.LevelDescription);
            translated += Translate(ref level.riskLevel);
            translated += Translate(ref level.levelIconString);
        }

        foreach (var enemy in Resources.FindObjectsOfTypeAll<EnemyType>())
        {
            if (enemy == null)
            {
                continue;
            }

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
}
