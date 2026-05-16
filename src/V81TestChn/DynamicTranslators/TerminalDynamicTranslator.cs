using System.Text.RegularExpressions;

namespace V81TestChn;

internal static partial class TranslationService
{
    internal static class TerminalDynamicTranslator
    {
        public static bool CanHandleCheap(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var trimmed = source.Trim();
            var looksLikeOrderedConfirmation =
                trimmed.StartsWith("Ordered ", System.StringComparison.OrdinalIgnoreCase) &&
                (trimmed.IndexOf("new balance", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 trimmed.IndexOf("\u65b0\u4f59\u989d", System.StringComparison.Ordinal) >= 0 ||
                 trimmed.IndexOf("\u65b0\u9918\u984d", System.StringComparison.Ordinal) >= 0);
            return trimmed.StartsWith("You have requested to order ", System.StringComparison.OrdinalIgnoreCase) ||
                   looksLikeOrderedConfirmation ||
                   trimmed.StartsWith("The Company is buying", System.StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("Cancelled order.", System.StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals("You have cancelled the order.", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool Translate(DynamicTextDomain domain, string? source, out string translated)
        {
            translated = source ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return TranslateOrderRequest(source, out translated) ||
                   TranslateOrderedItemConfirmation(source, out translated) ||
                   TranslateTerminalStatus(source, out translated) ||
                   TranslateCompanyBuyingStatus(source, out translated) ||
                   TryTranslateMapScreenDescription(source, out translated);
        }

        public static bool TranslateTerminalStatus(string source, out string translated)
        {
            translated = source;
            var trimmed = source.Trim();
            if (trimmed.Equals("Cancelled order.", System.StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002";
                return true;
            }

            if (trimmed.Equals("You have cancelled the order.", System.StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u4f60\u5df2\u53d6\u6d88\u8ba2\u5355\u3002";
                return true;
            }

            return false;
        }

        public static bool TranslateCompanyBuyingStatus(string source, out string translated)
        {
            translated = source;
            var normalized = source.Replace("\r\n", "\n").Trim();
            var routeMatch = SafeRegexMatch(
                normalized,
                @"^The Company is buying at (?<percent>.+?)\.\s*Do you want to route the autopilot to the Company building\?\s*Please CONFIRM or DENY\.?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (routeMatch.Success)
            {
                translated = $"\u516c\u53f8\u5f53\u524d\u6536\u8d2d\u6bd4\u4f8b\u4e3a {routeMatch.Groups["percent"].Value.Trim()}\u3002\u662f\u5426\u5c06\u81ea\u52a8\u9a7e\u9a76\u822a\u7ebf\u8bbe\u4e3a\u516c\u53f8\u5927\u697c\uff1f\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002";
                return true;
            }

            var standaloneMatch = SafeRegexMatch(
                normalized,
                @"^The Company is buying(?: your goods)? at (?<percent>.+?)\.$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!standaloneMatch.Success)
            {
                return false;
            }

            translated = $"\u516c\u53f8\u5f53\u524d\u6536\u8d2d\u6bd4\u4f8b\u4e3a {standaloneMatch.Groups["percent"].Value.Trim()}\u3002";
            return true;
        }

        public static bool TranslateOrderRequest(string source, out string translated)
        {
            translated = source;
            var normalized = source.Replace("\r\n", "\n").Trim();

            var fullOrderMatch = SafeRegexMatch(
                normalized,
                @"^You have requested to order (?<item>.+?)\.\s*(?<warranty>You have a free warranty!\s*)?(?:(?:Total cost(?: of items?)?|商品总价)\s*:?\s*(?<cost>.+?)\.)\s*Please CONFIRM or DENY\.?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (fullOrderMatch.Success)
            {
                var item = BuildTerminalLocalizedItemName(fullOrderMatch.Groups["item"].Value);
                var warranty = fullOrderMatch.Groups["warranty"].Success ? "\n\u4f60\u4eab\u6709\u514d\u8d39\u4fdd\u4fee\uff01" : string.Empty;
                var cost = NormalizeTransactionCost(fullOrderMatch.Groups["cost"].Value);
                translated = $"\n\n\u4f60\u5df2\u8bf7\u6c42\u8ba2\u8d2d {item}\u3002{warranty}\n\u5546\u54c1\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
                return true;
            }

            var localizedFullOrderMatch = SafeRegexMatch(
                normalized,
                @"^(?:\u60a8|\u4f60)\s*(?:\u5df2)?\u8bf7\u6c42\u8ba2\u8d2d\s*(?<item>.+?)[\u3002.]\s*(?<warranty>\u4f60\u4eab\u6709\u514d\u8d39\u4fdd\u4fee\uff01\s*)?(?:\u5546\u54c1\u603b\u4ef7|\u7269\u54c1\u603b\u4ef7|\u5355\u4ef6\u603b\u4ef7)\s*[:\uff1a.\s]*(?<cost>\$?\s*[+-]?\d+(?:\.\d+)?)(?:[\s:：.。]*)\s*\u8bf7\u8f93\u5165\s+CONFIRM\s+\u6216\s+DENY[\u3002.]?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (localizedFullOrderMatch.Success)
            {
                var item = BuildTerminalLocalizedItemName(localizedFullOrderMatch.Groups["item"].Value);
                var warranty = localizedFullOrderMatch.Groups["warranty"].Success ? "\n\u4f60\u4eab\u6709\u514d\u8d39\u4fdd\u4fee\uff01" : string.Empty;
                var cost = NormalizeTransactionCost(localizedFullOrderMatch.Groups["cost"].Value);
                translated = $"\n\n\u4f60\u5df2\u8bf7\u6c42\u8ba2\u8d2d {item}\u3002{warranty}\n\u5546\u54c1\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
                return true;
            }

            var warrantyCostMatch = SafeRegexMatch(
                normalized,
                @"^You have a free warranty!\s+(?:(?:Total cost(?: of items?)?|商品总价)\s*:?\s*(?<cost>.+?)\.)\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (warrantyCostMatch.Success)
            {
                translated = $"\u4f60\u4eab\u6709\u514d\u8d39\u4fdd\u4fee\uff01\n\u5546\u54c1\u603b\u4ef7\uff1a{NormalizeTransactionCost(warrantyCostMatch.Groups["cost"].Value)}\u3002";
                return true;
            }

            var englishLineMatch = SafeRegexMatch(
                normalized,
                @"^You have requested to order (?<item>.+?)\.\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (englishLineMatch.Success)
            {
                translated = $"\u4f60\u5df2\u8bf7\u6c42\u8ba2\u8d2d {BuildTerminalLocalizedItemName(englishLineMatch.Groups["item"].Value)}\u3002";
                return true;
            }

            var mixedChineseLineMatch = SafeRegexMatch(
                normalized,
                @"^(?:您|你)已请求订购\s+(?<item>.+?)[。.]?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (mixedChineseLineMatch.Success)
            {
                translated = $"\u4f60\u5df2\u8bf7\u6c42\u8ba2\u8d2d {BuildTerminalLocalizedItemName(mixedChineseLineMatch.Groups["item"].Value)}\u3002";
                return true;
            }

            if (string.Equals(normalized, "You have a free warranty!", System.StringComparison.OrdinalIgnoreCase))
            {
                translated = "\u4f60\u4eab\u6709\u514d\u8d39\u4fdd\u4fee\uff01";
                return true;
            }

            var totalCostMatch = SafeRegexMatch(
                normalized,
                @"^(?:Total cost(?: of items?)?|商品总价)\s*:?\s*(?<cost>.+?)\.?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (totalCostMatch.Success)
            {
                translated = $"\u5546\u54c1\u603b\u4ef7\uff1a{NormalizeTransactionCost(totalCostMatch.Groups["cost"].Value)}\u3002";
                return true;
            }

            if (SafeRegexMatch(
                    normalized,
                    @"^Please\s+CONFIRM\s+or\s+DENY\.?$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Success)
            {
                translated = "\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002";
                return true;
            }

            return false;
        }

        public static string NormalizeTransactionCost(string cost)
        {
            var normalized = SafeRegexReplace(cost ?? string.Empty, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            var match = SafeRegexMatch(
                normalized,
                @"(?<cost>\$?\s*[+-]?\d+(?:\.\d+)?)",
                RegexOptions.CultureInvariant);
            return match.Success
                ? SafeRegexReplace(match.Groups["cost"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant)
                : normalized.Trim(' ', '.', ':', '\uff1a', '\u3002');
        }

        public static bool TranslateOrderedItemConfirmation(string source, out string translated)
        {
            translated = source;
            if (!TrySafeRegexMatch(OrderedTerminalItemConfirmationRegex, source, out var match) || !match.Success)
            {
                return false;
            }

            var item = BuildTerminalLocalizedItemName(match.Groups["item"].Value.Trim());
            var credits = match.Groups["credits"].Value.Trim();
            var rest = match.Groups["rest"].Value.Trim();
            if (rest.Length == 0)
            {
                translated = $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002";
                return true;
            }

            var normalizedRest = TranslateOrderDetail(rest);
            translated = $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n{normalizedRest}";
            return true;
        }

        public static string StandardizeCruiserWarrantyText(string source)
        {
            if (string.IsNullOrEmpty(source) ||
                (!source.Contains("warranty", System.StringComparison.OrdinalIgnoreCase) &&
                 !source.Contains("Cruiser", System.StringComparison.OrdinalIgnoreCase)))
            {
                return source;
            }

            return SafeRegexReplace(TerminalCruiserWarrantyRegex, source, TerminalCruiserWarrantyLocalizedText);
        }

        public static string TranslateOrderDetail(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var normalized = SafeRegexReplace(source, @"\s+", " ").Trim();
            var warranty = StandardizeCruiserWarrantyText(normalized);
            if (!string.Equals(warranty, normalized, System.StringComparison.Ordinal))
            {
                return SanitizeTranslatedText(warranty);
            }

            var translated = TranslateTerminalOutputBody(normalized);
            translated = TranslateOrderDetailPhrases(translated);
            translated = StandardizeCruiserWarrantyText(translated);
            return SanitizeTranslatedText(translated);
        }

        public static string TranslateOrderDetailPhrases(string source)
        {
            var translated = source;
            translated = ReplaceIgnoreCase(
                translated,
                "Press [B] to rearrange objects in your ship and [V] to confirm.",
                "\u6309 [B] \u5728\u98de\u8239\u5185\u6574\u7406\u7269\u54c1\uff0c\u6309 [V] \u786e\u8ba4\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Press [B] to rearrange fish in your ship and [V] to confirm.",
                "\u6309 [B] \u5728\u98de\u8239\u5185\u6574\u7406\u91d1\u9c7c\uff0c\u6309 [V] \u786e\u8ba4\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Press the button to activate the teleporter. It will teleport whoever is currently being monitored on the ship's radar. You will not be able to keep any of your held items through the teleport. It takes about 10 seconds to recharge.",
                "\u6309\u4e0b\u6309\u94ae\u5373\u53ef\u542f\u52a8\u4f20\u9001\u5668\u3002\u5b83\u4f1a\u4f20\u9001\u5f53\u524d\u5728\u98de\u8239\u96f7\u8fbe\u76d1\u89c6\u4e2d\u7684\u76ee\u6807\u3002\u4f20\u9001\u8fc7\u7a0b\u4e2d\u5c06\u65e0\u6cd5\u4fdd\u7559\u624b\u6301\u7269\u54c1\u3002\u51b7\u5374\u65f6\u95f4\u7ea6 10 \u79d2\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Press the button and step onto the inverse teleporter while it activates.",
                "\u542f\u52a8\u65f6\u6309\u4e0b\u6309\u94ae\u5e76\u8e0f\u4e0a\u9006\u5411\u4f20\u9001\u5668\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Use the light switch to enable cozy lights.",
                "\u4f7f\u7528\u706f\u5149\u5f00\u5173\u542f\u7528\u6e29\u99a8\u706f\u4e32\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Use the light switch to enable the disco.",
                "\u4f7f\u7528\u706f\u5149\u5f00\u5173\u542f\u7528\u8fea\u65af\u79d1\u7403\u3002");
            translated = ReplaceIgnoreCase(
                translated,
                "Your electric chair can be activated by any powerful source of voltage!",
                "\u7535\u6905\u53ef\u7531\u4efb\u4f55\u5f3a\u5927\u7535\u538b\u6e90\u6fc0\u6d3b\uff01");
            translated = ReplaceIgnoreCase(
                translated,
                "Hold the cord to activate the loud horn.",
                "\u62c9\u4f4f\u7ef3\u7d22\u5373\u53ef\u542f\u52a8\u626c\u58f0\u5587\u53ed\u3002");
            translated = TranslateSignalTransmitterInstructions(translated, out var signalTranslated)
                ? signalTranslated
                : translated;
            return translated;
        }

        public static bool TranslateSignalTransmitterInstructions(string source, out string translated)
        {
            translated = SafeRegexReplace(
                SignalTransmitterInstructionsRegex,
                source,
                "\u4fe1\u53f7\u53d1\u5c04\u5668\u53ef\u901a\u8fc7 \"transmit\" \u547d\u4ee4\u6fc0\u6d3b\uff0c\u540e\u63a5\u4e0d\u8d85\u8fc7 10 \u4e2a\u5b57\u7b26\u7684\u6d88\u606f\u3002");
            return !string.Equals(translated, source, System.StringComparison.Ordinal);
        }
    }
}
