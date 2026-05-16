using System.Collections.Generic;

namespace V81TestChn;

internal static class OriginalResourceStateService
{
    private sealed class ItemState
    {
        public ItemState(Item item, string? itemName, string[]? toolTips)
        {
            Item = item;
            ItemName = itemName;
            ToolTips = Clone(toolTips);
        }

        public Item Item { get; }
        public string? ItemName { get; }
        public string[]? ToolTips { get; }
    }

    private sealed class TerminalNodeState
    {
        public TerminalNodeState(TerminalNode node)
        {
            Node = node;
            DisplayText = node.displayText;
            CreatureName = node.creatureName;
        }

        public TerminalNode Node { get; }
        public string? DisplayText { get; }
        public string? CreatureName { get; }
    }

    private sealed class SelectableLevelState
    {
        public SelectableLevelState(SelectableLevel level)
        {
            Level = level;
            PlanetName = level.PlanetName;
            LevelDescription = level.LevelDescription;
            RiskLevel = level.riskLevel;
            LevelIconString = level.levelIconString;
        }

        public SelectableLevel Level { get; }
        public string? PlanetName { get; }
        public string? LevelDescription { get; }
        public string? RiskLevel { get; }
        public string? LevelIconString { get; }
    }

    private sealed class EnemyTypeState
    {
        public EnemyTypeState(EnemyType enemy)
        {
            Enemy = enemy;
            EnemyName = enemy.enemyName;
        }

        public EnemyType Enemy { get; }
        public string? EnemyName { get; }
    }

    private static readonly Dictionary<int, ItemState> Items = new();
    private static readonly Dictionary<int, TerminalNodeState> TerminalNodes = new();
    private static readonly Dictionary<int, SelectableLevelState> SelectableLevels = new();
    private static readonly Dictionary<int, EnemyTypeState> EnemyTypes = new();

    public static void CaptureItem(Item? item)
    {
        if (item == null)
        {
            return;
        }

        var id = item.GetInstanceID();
        if (!Items.ContainsKey(id))
        {
            Items[id] = new ItemState(item, item.itemName, item.toolTips);
        }
    }

    public static string GetOriginalItemName(Item? item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        CaptureItem(item);
        return Items.TryGetValue(item.GetInstanceID(), out var state)
            ? state.ItemName ?? string.Empty
            : item.itemName ?? string.Empty;
    }

    public static void CaptureTerminalNode(TerminalNode? node)
    {
        if (node == null)
        {
            return;
        }

        var id = node.GetInstanceID();
        if (!TerminalNodes.ContainsKey(id))
        {
            TerminalNodes[id] = new TerminalNodeState(node);
        }
    }

    public static void CaptureSelectableLevel(SelectableLevel? level)
    {
        if (level == null)
        {
            return;
        }

        var id = level.GetInstanceID();
        if (!SelectableLevels.ContainsKey(id))
        {
            SelectableLevels[id] = new SelectableLevelState(level);
        }
    }

    public static void CaptureEnemyType(EnemyType? enemy)
    {
        if (enemy == null)
        {
            return;
        }

        var id = enemy.GetInstanceID();
        if (!EnemyTypes.ContainsKey(id))
        {
            EnemyTypes[id] = new EnemyTypeState(enemy);
        }
    }

    public static void RestoreAll()
    {
        try
        {
            foreach (var state in Items.Values)
            {
                try
                {
                    if (state.Item == null)
                    {
                        continue;
                    }

                    state.Item.itemName = state.ItemName;
                    state.Item.toolTips = Clone(state.ToolTips);
                }
                catch
                {
                    // Cleanup must stay best-effort and idempotent during Unity object teardown.
                }
            }

            foreach (var state in TerminalNodes.Values)
            {
                try
                {
                    if (state.Node == null)
                    {
                        continue;
                    }

                    state.Node.displayText = state.DisplayText;
                    state.Node.creatureName = state.CreatureName;
                }
                catch
                {
                    // Cleanup must stay best-effort and idempotent during Unity object teardown.
                }
            }

            foreach (var state in SelectableLevels.Values)
            {
                try
                {
                    if (state.Level == null)
                    {
                        continue;
                    }

                    state.Level.PlanetName = state.PlanetName;
                    state.Level.LevelDescription = state.LevelDescription;
                    state.Level.riskLevel = state.RiskLevel;
                    state.Level.levelIconString = state.LevelIconString;
                }
                catch
                {
                    // Cleanup must stay best-effort and idempotent during Unity object teardown.
                }
            }

            foreach (var state in EnemyTypes.Values)
            {
                try
                {
                    if (state.Enemy == null)
                    {
                        continue;
                    }

                    state.Enemy.enemyName = state.EnemyName;
                }
                catch
                {
                    // Cleanup must stay best-effort and idempotent during Unity object teardown.
                }
            }
        }
        finally
        {
            Clear();
        }
    }

    public static void Clear()
    {
        Items.Clear();
        TerminalNodes.Clear();
        SelectableLevels.Clear();
        EnemyTypes.Clear();
    }

    private static string[]? Clone(string[]? source)
    {
        return source == null ? null : (string[])source.Clone();
    }
}
