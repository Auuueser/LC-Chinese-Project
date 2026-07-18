using System;

namespace V81TestChn;

internal static class ChatWorkBudgetPolicy
{
    public static bool ExceedsCharacterBudget(string? value, int characterBudget)
    {
        return value != null && value.Length > Math.Max(1, characterBudget);
    }

    public static bool ExceedsLineBudget(string value, int lineBudget)
    {
        var maximumLines = Math.Max(1, lineBudget);
        var lines = 1;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\n' && ++lines > maximumLines)
            {
                return true;
            }
        }

        return false;
    }
}
