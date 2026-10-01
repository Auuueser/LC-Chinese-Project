using System.Collections.Generic;
using TMPro;

namespace V81TestChn;

internal static class FontSwitchLookupInvalidator
{
    // TMP caches characters resolved through a fallback in the requesting font's
    // lookup dictionary. Only those entries belong to the retiring font; rebuilding
    // every font definition also rebuilds glyph/kerning tables and reloads native
    // dynamic font faces, even for fonts that have never used our fallback.
    internal static int RemoveRetiredCharacters(
        Dictionary<uint, TMP_Character>? lookup,
        TMP_FontAsset retiringFont,
        List<uint> removedKeys)
    {
        removedKeys.Clear();
        if (lookup == null || lookup.Count == 0) return 0;

        foreach (var pair in lookup)
        {
            if (pair.Value != null && pair.Value.textAsset == retiringFont)
                removedKeys.Add(pair.Key);
        }

        var count = removedKeys.Count;
        foreach (var key in removedKeys) lookup.Remove(key);
        removedKeys.Clear();
        return count;
    }
}
