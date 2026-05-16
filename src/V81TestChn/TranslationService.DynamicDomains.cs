using System;
using System.Text;

namespace V81TestChn;

internal static partial class TranslationService
{
    public static bool TryTranslateKnownDynamicTextFast(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (LooksLikeHostModWarningTextCheap(source) &&
            TryTranslateHostModWarning(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "HostModWarning.Fast");
        }

        if (WeightUnitTranslator.Translate(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "WeightUnit.Fast");
        }

        if (TryTranslateFixedSceneLabelFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "FixedSceneLabel.Fast");
        }

        if (TryTranslateShipMonitorTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ShipMonitor.Fast");
        }

        if (TryTranslateClockTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "Clock.Fast");
        }

        if (PlanetInfoDynamicTranslator.TranslateFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "PlanetInfo.Fast");
        }

        if (TryTranslateStandaloneControlTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ControlTip.Fast");
        }

        if (HudDynamicTranslator.Translate(DynamicTextDomain.GeneralFast, source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "Hud.Fast");
        }

        if (TryTranslateSaveFileStatsTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "SaveFileStats.Fast");
        }

        if (TryTranslateTimePeriodTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "TimePeriod.Fast");
        }

        var trimmed = source.Trim();
        if (string.Equals(trimmed, "Cancelled order.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002";
            return FinishKnownDynamicTranslation(source, ref translated, "TerminalStatus.Fast");
        }

        if (string.Equals(trimmed, "You have cancelled the order.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4f60\u5df2\u53d6\u6d88\u8ba2\u5355\u3002";
            return FinishKnownDynamicTranslation(source, ref translated, "TerminalStatus.Fast");
        }

        translated = source;
        return false;
    }

    public static bool TryTranslateKnownDynamicTextTargeted(DynamicTextDomain domain, string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (domain == DynamicTextDomain.GeneralFast)
        {
            return TryTranslateKnownDynamicTextFast(source, out translated);
        }

        if (domain == DynamicTextDomain.ChatOutput &&
            source.IndexOf('\n') >= 0 &&
            TryTranslateKnownDynamicLinesTargeted(domain, source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ChatOutput.Lines");
        }

        bool matched;
        var kind = domain.ToString();
        switch (domain)
        {
            case DynamicTextDomain.Terminal:
                matched = TerminalDynamicTranslator.Translate(domain, source, out translated);
                break;
            case DynamicTextDomain.HudControlTip:
                matched = ControlTipTranslator.Translate(source, out translated) ||
                          ControlTipTranslator.TranslateStandalone(source, out translated);
                break;
            case DynamicTextDomain.HudScanner:
                matched = HudDynamicTranslator.Translate(domain, source, out translated) ||
                          TryTranslateKnownDynamicTextFast(source, out translated);
                break;
            case DynamicTextDomain.HudRewards:
                matched = HudDynamicTranslator.Translate(domain, source, out translated) ||
                          TryTranslateKnownDynamicTextFast(source, out translated);
                break;
            case DynamicTextDomain.ChatOutput:
                matched = ChatDynamicTranslator.Translate(source, out translated);
                break;
            case DynamicTextDomain.EndGame:
                matched = EndGameDynamicTranslator.Translate(source, out translated) ||
                          TryTranslateKnownDynamicTextFast(source, out translated);
                break;
            case DynamicTextDomain.PlanetInfo:
                matched = PlanetInfoDynamicTranslator.Translate(source, out translated) ||
                          TryTranslateMapScreenDescription(source, out translated);
                break;
            case DynamicTextDomain.MenuNotification:
                matched = TryTranslateHostModWarning(source, out translated);
                break;
            case DynamicTextDomain.SpectateStatus:
                matched = EndGameDynamicTranslator.TranslatePlayerStatus(source, out translated);
                break;
            default:
                matched = false;
                break;
        }

        if (!matched &&
            source.IndexOf('\n') >= 0 &&
            domain is DynamicTextDomain.HudRewards or DynamicTextDomain.EndGame or DynamicTextDomain.PlanetInfo or DynamicTextDomain.SpectateStatus &&
            TryTranslateKnownDynamicLinesTargeted(domain, source, out translated))
        {
            matched = true;
            kind += ".Lines";
        }

        return matched && FinishKnownDynamicTranslation(source, ref translated, kind);
    }

    public static bool TryTranslateKnownDynamicTextTargeted(int domain, string? source, out string translated)
    {
        return TryTranslateKnownDynamicTextTargeted((DynamicTextDomain)domain, source, out translated);
    }

    public static bool MaybeKnownDynamicTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return WeightUnitTranslator.CanHandleCheap(source) ||
               LooksLikeVoteTextCheap(source) ||
               LooksLikeDaysLeftTextCheap(source) ||
               LooksLikeRandomSeedTextCheap(source) ||
               LooksLikeEndgameStatTextCheap(source) ||
               LooksLikeControlTipTextCheap(source) ||
               LooksLikeClockTextCheap(source) ||
               LooksLikeShipMonitorTextCheap(source) ||
               LooksLikePlanetInfoTextCheap(source) ||
               LooksLikeChatSystemMessageCheap(source) ||
               LooksLikeHostModWarningTextCheap(source);
    }

    public static bool LooksLikeHostModWarningTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var trimmed = source.TrimStart();
        return trimmed.StartsWith("The host is detected", StringComparison.OrdinalIgnoreCase) &&
               trimmed.IndexOf("modified version", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool LooksLikeWeightUnitTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return WeightUnitTranslator.CanHandleCheap(source);
    }

    public static bool LooksLikeVoteTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            source.IndexOf("Vote", StringComparison.OrdinalIgnoreCase) < 0 ||
            source.IndexOf('/') < 0)
        {
            return false;
        }

        var text = StripOuterParens(StripRichTextTagsCheap(source).Trim());
        return text.EndsWith(" Vote", StringComparison.OrdinalIgnoreCase) ||
               text.EndsWith(" Votes", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeDaysLeftTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            source.IndexOf("Day", StringComparison.OrdinalIgnoreCase) < 0 ||
            source.IndexOf("Left", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        return text.EndsWith(" Day Left", StringComparison.OrdinalIgnoreCase) ||
               text.EndsWith(" Days Left", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeRandomSeedTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        return text.StartsWith("Random seed:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeClockTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        if (!text.EndsWith("AM", StringComparison.OrdinalIgnoreCase) &&
            !text.EndsWith("PM", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return LooksLikeClockTime(text[..^2].TrimEnd());
    }

    public static bool LooksLikeShipMonitorTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        return text.StartsWith("PROFIT QUOTA:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("DEADLINE:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikePlanetInfoTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).TrimStart();
        return text.StartsWith("CELESTIAL BODY:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("CELESTIAL_BODY:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("POPULATION:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("CONDITIONS:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("FAUNA:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("\u5929\u4f53:", StringComparison.Ordinal) ||
               text.StartsWith("\u5929\u4f53\uff1a", StringComparison.Ordinal) ||
               text.StartsWith("\u4eba\u53e3:", StringComparison.Ordinal) ||
               text.StartsWith("\u4eba\u53e3\uff1a", StringComparison.Ordinal) ||
               text.StartsWith("\u73af\u5883:", StringComparison.Ordinal) ||
               text.StartsWith("\u73af\u5883\uff1a", StringComparison.Ordinal) ||
               text.StartsWith("\u751f\u6001:", StringComparison.Ordinal) ||
               text.StartsWith("\u751f\u6001\uff1a", StringComparison.Ordinal);
    }

    public static bool LooksLikeEndgameStatTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        return text.IndexOf("casualties", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("bodies recovered", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.StartsWith("DUE:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeControlTipTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var text = StripRichTextTagsCheap(source).Trim();
        return text.IndexOf('[') >= 0 &&
               text.IndexOf(']') > text.IndexOf('[') &&
               text.IndexOf(':') >= 0;
    }

    public static bool LooksLikeChatSystemMessageCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return ChatDynamicTranslator.CanHandleCheap(source);
    }

    private static bool TryTranslateKnownDynamicLinesTargeted(DynamicTextDomain domain, string source, out string translated)
    {
        translated = source;
        var lines = source.Split('\n');
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (!TryTranslateKnownDynamicTextTargeted(domain, content, out var rewrittenLine))
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewrittenLine + "\r" : rewrittenLine;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        translated = string.Join("\n", lines);
        return true;
    }

    private static bool TryTranslateWeightUnitTextFast(string source, out string translated)
    {
        translated = source;
        return WeightUnitTranslator.Translate(source, out translated);
    }

    private static string NormalizeWeightUnitTextFast(string source)
    {
        return WeightUnitTranslator.Normalize(source);
    }

    private static bool TryTranslateFixedSceneLabelFast(string source, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        translated = trimmed switch
        {
            "PROFIT" => "\u5229\u6da6",
            "PROFIT:" => "\u5229\u6da6:",
            "QUOTA" => "\u914d\u989d",
            "QUOTA:" => "\u914d\u989d:",
            "PROFIT QUOTA" => "\u5229\u6da6\u914d\u989d",
            "PROFIT QUOTA:" => "\u5229\u6da6\u914d\u989d:",
            "DEADLINE" => "\u622a\u6b62\u65e5\u671f",
            "DEADLINE:" => "\u622a\u6b62\u65e5\u671f:",
            "Day" => "\u5929",
            "Days" => "\u5929",
            "Park" => "\u505c\u8f66\u6321",
            "Reverse" => "\u5012\u8f66\u6321",
            "Drive" => "\u524d\u8fdb\u6321",
            _ => source
        };

        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            return true;
        }

        if (!trimmed.EndsWith(" Days", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.EndsWith(" Day", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffixLength = trimmed.EndsWith(" Days", StringComparison.OrdinalIgnoreCase) ? 5 : 4;
        var count = trimmed[..^suffixLength].Trim();
        if (!AllDigits(count))
        {
            return false;
        }

        translated = $"{count} \u5929";
        return true;
    }

    private static bool TryTranslateShipMonitorTextFast(string source, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (TryRewriteMonitorBlock(trimmed, "PROFIT QUOTA:", "\u5229\u6da6\u914d\u989d:", out translated))
        {
            return true;
        }

        if (TryRewriteMonitorBlock(trimmed, "DEADLINE:", "\u622a\u6b62\u65e5\u671f:", out translated))
        {
            return true;
        }

        return false;
    }

    private static bool TryRewriteMonitorBlock(string trimmed, string englishPrefix, string localizedPrefix, out string translated)
    {
        translated = trimmed;
        if (!trimmed.StartsWith(englishPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = trimmed[englishPrefix.Length..].Trim();
        if (value.Equals("NOW", StringComparison.OrdinalIgnoreCase))
        {
            value = "\u73b0\u5728";
        }
        else if (TryTranslateFixedSceneLabelFast(value, out var localizedValue))
        {
            value = localizedValue;
        }

        translated = value.Length == 0 ? localizedPrefix : $"{localizedPrefix}\n{value}";
        return true;
    }

    private static bool TryTranslateClockTextFast(string source, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Length < 4)
        {
            return false;
        }

        string period;
        if (trimmed.EndsWith("AM", StringComparison.OrdinalIgnoreCase))
        {
            period = "\u4e0a\u5348";
        }
        else if (trimmed.EndsWith("PM", StringComparison.OrdinalIgnoreCase))
        {
            period = "\u4e0b\u5348";
        }
        else
        {
            return false;
        }

        var timePart = trimmed[..^2].TrimEnd();
        if (!LooksLikeClockTime(timePart))
        {
            return false;
        }

        translated = timePart + (trimmed.IndexOf('\n') >= 0 ? "\n" : " ") + period;
        return true;
    }

    private static bool TryTranslateStandaloneControlTextFast(string source, out string translated)
    {
        translated = source;
        return ControlTipTranslator.TranslateStandaloneFast(source, out translated);
    }

    private static bool TryTranslatePlanetInfoTextFast(string source, out string translated)
    {
        translated = source;
        if (!LooksLikePlanetInfoTextCheap(source))
        {
            return false;
        }

        var newlineIndex = source.IndexOf('\n');
        if (newlineIndex < 0)
        {
            return PlanetInfoDynamicTranslator.TranslateFast(source, out translated);
        }

        var changed = false;
        var builder = new StringBuilder(source.Length + 16);
        var start = 0;
        while (start <= source.Length)
        {
            var nextNewline = source.IndexOf('\n', start);
            var end = nextNewline < 0 ? source.Length : nextNewline;
            var line = source[start..end];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (PlanetInfoDynamicTranslator.TranslateFast(content, out var rewrittenLine))
            {
                builder.Append(rewrittenLine);
                if (hasCarriageReturn)
                {
                    builder.Append('\r');
                }

                changed = true;
            }
            else
            {
                builder.Append(line);
            }

            if (nextNewline < 0)
            {
                break;
            }

            builder.Append('\n');
            start = nextNewline + 1;
        }

        if (!changed)
        {
            return false;
        }

        translated = builder.ToString();
        return true;
    }

    private static bool TryTranslatePlanetInfoLineFast(string source, out string translated)
    {
        translated = source;
        var leadingLength = source.Length - source.TrimStart().Length;
        var leading = leadingLength > 0 ? source[..leadingLength] : string.Empty;
        var trimmed = StripRichTextTagsCheap(source).Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var rewritten = source;
        var matched =
            TryRewrite("CELESTIAL BODY:", "\u5929\u4f53:", translateValue: false) ||
            TryRewrite("CELESTIAL_BODY:", "\u5929\u4f53:", translateValue: false) ||
            TryRewrite("\u5929\u4f53:", "\u5929\u4f53:", translateValue: false) ||
            TryRewrite("\u5929\u4f53\uff1a", "\u5929\u4f53:", translateValue: false) ||
            TryRewrite("POPULATION:", "\u4eba\u53e3:", translateValue: true) ||
            TryRewrite("\u4eba\u53e3:", "\u4eba\u53e3:", translateValue: true) ||
            TryRewrite("\u4eba\u53e3\uff1a", "\u4eba\u53e3:", translateValue: true) ||
            TryRewrite("CONDITIONS:", "\u73af\u5883:", translateValue: true) ||
            TryRewrite("\u73af\u5883:", "\u73af\u5883:", translateValue: true) ||
            TryRewrite("\u73af\u5883\uff1a", "\u73af\u5883:", translateValue: true) ||
            TryRewrite("FAUNA:", "\u751f\u6001:", translateValue: true) ||
            TryRewrite("\u751f\u6001:", "\u751f\u6001:", translateValue: true) ||
            TryRewrite("\u751f\u6001\uff1a", "\u751f\u6001:", translateValue: true);
        translated = rewritten;
        return matched;

        bool TryRewrite(string label, string localizedLabel, bool translateValue)
        {
            if (!trimmed.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var value = trimmed[label.Length..].Trim();
            rewritten = value.Length == 0
                ? leading + localizedLabel
                : leading + localizedLabel + " " + (translateValue ? TranslateKnownPlanetInfoValueFast(value) : value);
            return true;
        }
    }

    private static string TranslateKnownPlanetInfoValueFast(string value)
    {
        return PlanetInfoDynamicTranslator.TranslateKnownValue(value);
    }

    private static bool TryTranslateTimePeriodTextFast(string source, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();
        if (trimmed.Equals("AM", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4e0a\u5348";
            return true;
        }

        if (trimmed.Equals("PM", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4e0b\u5348";
            return true;
        }

        return false;
    }

    private static bool TryTranslateSaveFileStatsTextFast(string source, out string translated)
    {
        translated = source;
        var newlineIndex = source.IndexOf('\n');
        if (newlineIndex <= 0 || newlineIndex >= source.Length - 1)
        {
            return false;
        }

        var firstLineEnd = newlineIndex;
        var usesCarriageReturn = firstLineEnd > 0 && source[firstLineEnd - 1] == '\r';
        var firstLine = source[..(usesCarriageReturn ? firstLineEnd - 1 : firstLineEnd)].Trim();
        var secondLine = source[(newlineIndex + 1)..].Trim();
        if (firstLine.Length < 2 ||
            firstLine[0] != '$' ||
            !AllDigits(firstLine[1..].Trim()))
        {
            return false;
        }

        const string dayPrefix = "Days:";
        if (!secondLine.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var days = secondLine[dayPrefix.Length..].Trim();
        if (!AllDigits(days))
        {
            return false;
        }

        translated = $"{firstLine}{(usesCarriageReturn ? "\r\n" : "\n")}\u5929\u6570: {days}";
        return true;
    }

    private static bool TryTranslateRandomSeedTextFast(string source, out string translated)
    {
        translated = source;
        return HudDynamicTranslator.Translate(DynamicTextDomain.GeneralFast, source, out translated);
    }

    private static bool TryTranslateVotesTextFast(string source, out string translated)
    {
        translated = source;
        return HudDynamicTranslator.Translate(DynamicTextDomain.GeneralFast, source, out translated);
    }

    private static bool TryTranslateDaysLeftTextFast(string source, out string translated)
    {
        translated = source;
        return HudDynamicTranslator.Translate(DynamicTextDomain.GeneralFast, source, out translated);
    }

    private static string StripRichTextTagsCheap(string source)
    {
        if (source.IndexOf('<') < 0)
        {
            return source;
        }

        var builder = new StringBuilder(source.Length);
        var inTag = false;
        foreach (var ch in source)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }

            if (ch == '>' && inTag)
            {
                inTag = false;
                continue;
            }

            if (!inTag)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string StripOuterParens(string source)
    {
        return source.Length >= 2 &&
               ((source[0] == '(' && source[^1] == ')') || (source[0] == '\uff08' && source[^1] == '\uff09'))
            ? source[1..^1].Trim()
            : source;
    }

    private static bool LooksLikeSimpleNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var sawDigit = false;
        var sawDot = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if ((ch == '+' || ch == '-') && i == 0)
            {
                continue;
            }

            if (ch == '.' && !sawDot)
            {
                sawDot = true;
                continue;
            }

            if (!char.IsDigit(ch))
            {
                return false;
            }

            sawDigit = true;
        }

        return sawDigit;
    }

    private static bool AllDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeSignedInteger(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var start = value[0] is '+' or '-' ? 1 : 0;
        if (start == value.Length)
        {
            return false;
        }

        for (var i = start; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeClockTime(string value)
    {
        var colon = value.IndexOf(':');
        if (colon < 1 || colon > 2 || colon >= value.Length - 2)
        {
            return false;
        }

        var hour = value[..colon];
        var minute = value[(colon + 1)..];
        if (minute.Length == 5 && minute[2] == ':')
        {
            return AllDigits(hour) && AllDigits(minute[..2]) && AllDigits(minute[3..]);
        }

        return minute.Length == 2 && AllDigits(hour) && AllDigits(minute);
    }

    private static string RemoveAsciiWhitespace(string value)
    {
        StringBuilder? builder = null;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch is not (' ' or '\t' or '\r' or '\n'))
            {
                builder?.Append(ch);
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            if (builder.Length == 0 && i > 0)
            {
                builder.Append(value, 0, i);
            }
        }

        return builder?.ToString() ?? value;
    }

    private static bool HasAsciiLetterBefore(string source, int index)
    {
        return index > 0 && IsAsciiLetter(source[index - 1]);
    }

    private static bool HasAsciiLetterAfter(string source, int index)
    {
        return index + 1 < source.Length && IsAsciiLetter(source[index + 1]);
    }

    private static bool IsAsciiLetter(char ch)
    {
        return ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
