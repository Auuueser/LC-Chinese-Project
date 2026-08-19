using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace V81TestChn;

internal static class TerminalCatalogueLocalizationService
{
    private const string EnglishAliasColor = "#808080";
    private sealed class ProcessedNodeMarker
    {
        public static readonly ProcessedNodeMarker Instance = new();
    }

    private static ConditionalWeakTable<TerminalNode, ProcessedNodeMarker> ProcessedNodes = new();

    public static int Apply(Terminal? terminal)
    {
        if (terminal == null)
        {
            return 0;
        }

        return ApplyNodes(terminal.enemyFiles) +
               ApplyNodes(terminal.logEntryFiles);
    }

    public static void ClearRuntimeCache()
    {
        ProcessedNodes = new ConditionalWeakTable<TerminalNode, ProcessedNodeMarker>();
    }

    internal static bool TryBuildBilingualCatalogueName(
        string? original,
        string? localized,
        out string bilingual)
    {
        bilingual = localized ?? string.Empty;
        var english = original?.Trim() ?? string.Empty;
        var chinese = localized?.Trim() ?? string.Empty;
        if (english.Length == 0 ||
            chinese.Length == 0 ||
            english.IndexOfAny(new[] { '\r', '\n' }) >= 0 ||
            string.Equals(english, chinese, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bilingual = $"{chinese} <color={EnglishAliasColor}>({english})</color>";
        return true;
    }

    private static int ApplyNodes(IList<TerminalNode>? nodes)
    {
        if (nodes == null)
        {
            return 0;
        }

        var changed = 0;
        foreach (var node in nodes)
        {
            if (node == null || ProcessedNodes.TryGetValue(node, out _))
            {
                continue;
            }

            var nodeChanged = false;
            var originalDisplayText = OriginalResourceStateService.GetOriginalTerminalNodeDisplayText(node);
            if (TranslationService.TryTranslateFastExact(originalDisplayText, out var localizedDisplayText) &&
                !string.Equals(node.displayText, localizedDisplayText, StringComparison.Ordinal))
            {
                node.displayText = localizedDisplayText;
                nodeChanged = true;
            }

            var originalName = OriginalResourceStateService.GetOriginalTerminalNodeCreatureName(node);
            if (TranslationService.TryTranslateFastExact(originalName, out var localizedName) &&
                TryBuildBilingualCatalogueName(originalName, localizedName, out var bilingual) &&
                !string.Equals(node.creatureName, bilingual, StringComparison.Ordinal))
            {
                node.creatureName = bilingual;
                nodeChanged = true;
            }

            if (nodeChanged)
            {
                changed++;
            }

            // The weak table keeps subsequent terminal-open events allocation-free
            // for stable catalogues while still allowing late-added nodes to be
            // handled on the next event. Destroyed Unity wrappers are not retained.
            ProcessedNodes.Add(node, ProcessedNodeMarker.Instance);
        }

        return changed;
    }
}
