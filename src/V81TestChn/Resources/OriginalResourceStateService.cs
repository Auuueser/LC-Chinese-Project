using System;
using System.Collections.Generic;

namespace V81TestChn;

internal static class OriginalResourceStateService
{
    private sealed class ItemState
    {
        public ItemState(Item item, string? itemName, string[]? toolTips)
        {
            Item = new WeakReference<Item>(item);
            ItemName = itemName;
            ToolTips = Clone(toolTips);
        }

        public WeakReference<Item> Item { get; }
        public string? ItemName { get; }
        public string[]? ToolTips { get; }
    }

    private sealed class TerminalNodeState
    {
        public TerminalNodeState(TerminalNode node)
        {
            Node = new WeakReference<TerminalNode>(node);
            DisplayText = node.displayText;
            CreatureName = node.creatureName;
        }

        public WeakReference<TerminalNode> Node { get; }
        public string? DisplayText { get; }
        public string? CreatureName { get; }
    }

    private sealed class SelectableLevelState
    {
        public SelectableLevelState(SelectableLevel level)
        {
            Level = new WeakReference<SelectableLevel>(level);
            PlanetName = level.PlanetName;
            LevelDescription = level.LevelDescription;
            RiskLevel = level.riskLevel;
            LevelIconString = level.levelIconString;
        }

        public WeakReference<SelectableLevel> Level { get; }
        public string? PlanetName { get; }
        public string? LevelDescription { get; }
        public string? RiskLevel { get; }
        public string? LevelIconString { get; }
    }

    private sealed class EnemyTypeState
    {
        public EnemyTypeState(EnemyType enemy)
        {
            Enemy = new WeakReference<EnemyType>(enemy);
            EnemyName = enemy.enemyName;
        }

        public WeakReference<EnemyType> Enemy { get; }
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
        if (!Items.TryGetValue(id, out var existing) ||
            !existing.Item.TryGetTarget(out var existingItem) ||
            existingItem == null ||
            existingItem != item)
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
        return Items.TryGetValue(item.GetInstanceID(), out var state) &&
               state.Item.TryGetTarget(out var capturedItem) &&
               capturedItem != null &&
               capturedItem == item
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
        if (!TerminalNodes.TryGetValue(id, out var existing) ||
            !existing.Node.TryGetTarget(out var existingNode) ||
            existingNode == null ||
            existingNode != node)
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
        if (!SelectableLevels.TryGetValue(id, out var existing) ||
            !existing.Level.TryGetTarget(out var existingLevel) ||
            existingLevel == null ||
            existingLevel != level)
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
        if (!EnemyTypes.TryGetValue(id, out var existing) ||
            !existing.Enemy.TryGetTarget(out var existingEnemy) ||
            existingEnemy == null ||
            existingEnemy != enemy)
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
                    if (!state.Item.TryGetTarget(out var item) || item == null)
                    {
                        continue;
                    }

                    item.itemName = state.ItemName;
                    item.toolTips = Clone(state.ToolTips);
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
                    if (!state.Node.TryGetTarget(out var node) || node == null)
                    {
                        continue;
                    }

                    node.displayText = state.DisplayText;
                    node.creatureName = state.CreatureName;
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
                    if (!state.Level.TryGetTarget(out var level) || level == null)
                    {
                        continue;
                    }

                    level.PlanetName = state.PlanetName;
                    level.LevelDescription = state.LevelDescription;
                    level.riskLevel = state.RiskLevel;
                    level.levelIconString = state.LevelIconString;
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
                    if (!state.Enemy.TryGetTarget(out var enemy) || enemy == null)
                    {
                        continue;
                    }

                    enemy.enemyName = state.EnemyName;
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

    public static void PruneDestroyed()
    {
        Prune(Items, static state => state.Item.TryGetTarget(out var item) && item != null);
        Prune(TerminalNodes, static state => state.Node.TryGetTarget(out var node) && node != null);
        Prune(SelectableLevels, static state => state.Level.TryGetTarget(out var level) && level != null);
        Prune(EnemyTypes, static state => state.Enemy.TryGetTarget(out var enemy) && enemy != null);
    }

    private static void Prune<TState>(Dictionary<int, TState> states, Func<TState, bool> isAlive)
    {
        List<int>? staleIds = null;
        foreach (var pair in states)
        {
            if (isAlive(pair.Value))
            {
                continue;
            }

            staleIds ??= new List<int>();
            staleIds.Add(pair.Key);
        }

        if (staleIds == null)
        {
            return;
        }

        foreach (var id in staleIds)
        {
            states.Remove(id);
        }
    }

    private static string[]? Clone(string[]? source)
    {
        return source == null ? null : (string[])source.Clone();
    }
}
