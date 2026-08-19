using System;

namespace V81TestChn;

internal static class PlayerLevelLocalizationService
{
    internal static bool TryTranslateRankName(string? source, out string localized)
    {
        localized = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        localized = source.Trim() switch
        {
            "Intern" or "\u5b9e\u4e60\u5458\u5de5" or "\u5b9e\u4e60\u751f" => "\u5b9e\u4e60\u751f",
            "Part-timer" or "\u517c\u804c\u5458\u5de5" => "\u517c\u804c\u5458\u5de5",
            "Employee" or "\u96c7\u5458" or "\u6b63\u5f0f\u5458\u5de5" => "\u6b63\u5f0f\u5458\u5de5",
            "Leader" or "\u961f\u957f" or "\u9886\u961f" => "\u9886\u961f",
            "Boss" or "\u8001\u677f" or "\u9886\u5bfc" => "\u9886\u5bfc",
            _ => string.Empty
        };
        return localized.Length > 0;
    }
}
