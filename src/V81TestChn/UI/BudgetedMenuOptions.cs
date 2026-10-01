using System;
using System.Collections;
using System.Collections.Generic;

namespace V81TestChn;

// Each option consumes shared menu work, rather than treating an arbitrarily
// large dropdown as one component. Re-read mutable lists after every yield.
internal static class BudgetedMenuOptions
{
    internal static IEnumerator Run<T>(Func<bool> alive, Func<IList<T>?> getOptions,
        Func<bool> spend, Func<T, int, bool> translate, Action refresh) where T : class
    {
        IList<T>? previous = null;
        var previousCount = -1;
        var index = 0;
        var dirty = false;
        try
        {
            while (alive())
            {
                var options = getOptions();
                if (options == null) break;
                if (!ReferenceEquals(previous, options) || previousCount != options.Count)
                {
                    previous = options;
                    previousCount = options.Count;
                    index = 0;
                }
                if (index >= options.Count) break;
                if (!spend())
                {
                    if (dirty) { dirty = false; refresh(); }
                    yield return null;
                    continue;
                }
                var option = options[index];
                if (option != null) dirty |= translate(option, index);
                index++;
            }
        }
        finally { if (dirty && alive()) refresh(); }
    }
}
