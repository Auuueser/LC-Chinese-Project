using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace V81TestChn;

internal enum DynamicTextDomain
{
    GeneralFast,
    Terminal,
    HudControlTip,
    HudScanner,
    HudRewards,
    ChatOutput,
    EndGame,
    PlanetInfo,
    MenuNotification,
    SpectateStatus
}

internal static partial class TranslationService
{
    private const int TerminalRollingWindowChars = 250;

    private enum KnownSlowCfgRegexKind
    {
        PurchasedItemsOnRoute,
        PurchasedVehicleOnRoute,
        ColonAll,
        ColonNone
    }

    private sealed class RegexEntry
    {
        public Regex? Regex { get; }
        public KnownSlowCfgRegexKind? Kind { get; }
        public string Pattern { get; }
        public string Replacement { get; }
        public bool Disabled { get; private set; }

        public RegexEntry(string pattern, Regex regex, string replacement)
        {
            Regex = regex;
            Kind = null;
            Pattern = pattern;
            Replacement = replacement;
        }

        public RegexEntry(string pattern, KnownSlowCfgRegexKind kind, string replacement)
        {
            Regex = null;
            Kind = kind;
            Pattern = pattern;
            Replacement = replacement;
        }

        public void DisableAfterTimeout()
        {
            Disabled = true;
        }
    }

    private static readonly Dictionary<string, string> ExactMap = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> ExactMapIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<KeyValuePair<string, string>> CompositeEntries = new();
    private static readonly List<RegexEntry> RegexEntries = new();
    private static readonly HashSet<string> RegexPatternSet = new(StringComparer.Ordinal);
    private static readonly HashSet<string> WarnedRegexTimeoutPatterns = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string?> TranslationResultCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> CompositeTranslationResultCache = new(StringComparer.Ordinal);
    private const int MaxTranslationResultCache = 8000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(25);
    private const string TemperatureUnitCelsius = "Celsius";
    private const string TemperatureUnitFahrenheit = "Fahrenheit";
    private const int KnownDynamicHitLogBudget = 80;
    private static ConfigEntry<string>? _temperatureUnit;
    private static ConfigEntry<bool>? _logKnownDynamicHits;
    private static int _knownDynamicHitLogCount;
    [ThreadStatic]
    private static bool _suppressKnownDynamicHitLog;
    private static readonly Regex ControlTipItemNameRegex = new(
        @"^(?<prefix>\s*)(?<verb>Drop|\u4e22\u5f03)\s+(?<item>.+?)(?<suffix>\s*[:\uff1a]\s*\[[^\]]+\]\s*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);
    private static readonly Regex HighFeverFahrenheitRegex = new(
        @"^(?<prefix>\s*)HIGH\s+FEVER\s+DETECTED!\s+REACHING\s+(?<fahrenheit>-?\d+)°F(?<suffix>\s*)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
    private static readonly Regex OrderedTerminalItemConfirmationRegex = new(
        @"^Ordered(?: the)?\s+(?!\d+\s)(?<item>.+?)[.!]\s*(?:Your new balance is|(?:\u4f60|\u60a8)\u7684(?:\u65b0\u4f59\u989d\u4e3a|\u65b0\u9918\u984d(?:\u70ba|\u4e3a)))\s*(?<credits>[$\u25a0]?\s*[+-]?\d+(?:\.\d+)?)\s*[\.\u3002]?(?<rest>[\s\S]*)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
    private static readonly Regex TerminalCruiserWarrantyRegex = new(
        @"(?:We\s+are\s+so\s+confident\s+in\s+the\s+quality\s+of\s+this\s+product,\s*it\s+comes\s+with\s+a\s+life-time\s+warranty!\s+If\s+your\s+(?:Company\s+)?Cruiser\s+is\s+lost\s+or\s+destroyed,\s+you\s+can\s+get\s+one\s+free\s+replacement\.\s+Items\s+cannot\s+be\s+purchased\s+while\s+the\s+vehicle\s+is\s+en\s+route\.|The\s+Company\s+is\s+very\s+confident\s+in\s+the\s+quality\s+of\s+this\s+product,\s*and\s+therefore\s+is\s+supplying\s+a\s+lifetime\s+warranty!\s+If\s+your\s+(?:Company\s+)?Cruiser\s+is\s+lost\s+or\s+destroyed,\s+you\s+can\s+get\s+a\s+free\s+replacement\s+once\.\s+Cannot\s+purchase\s+while\s+vehicle\s+is\s+on\s+route\.)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
    private static readonly Regex SignalTransmitterInstructionsRegex = new(
        @"The signal transmitter can be activated with the\s+[""\u201c\u201d]transmit[""\u201c\u201d]\s+command followed by any message under\s+10\s+letters\.",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
    private const string TerminalCruiserWarrantyLocalizedText =
        "\u516c\u53f8\u5bf9\u6b64\u4ea7\u54c1\u8d28\u91cf\u975e\u5e38\u6709\u4fe1\u5fc3\uff0c\u56e0\u6b64\u63d0\u4f9b\u7ec8\u8eab\u4fdd\u4fee\u3002\n\u5982\u679c\u4f60\u7684\u5de1\u822a\u8f66\u4e22\u5931\u6216\u635f\u574f\uff0c\u53ef\u4ee5\u83b7\u5f97\u4e00\u6b21\u514d\u8d39\u66ff\u6362\u3002\n\u5728\u8f7d\u5177\u8fd0\u9001\u9014\u4e2d\u65e0\u6cd5\u8d2d\u4e70\u7269\u54c1\u3002";
    private const string BootSplashCanonicalText =
        "      BG IG, A System-Act Ally\n" +
        "      Copyright (C) 2084-2108, Halden Electronics Inc.\n" +
        "\n" +
        "CPU Type      :     BORSON 300 CPU at 2500 MHz\n" +
        "Memory test  :      4521586K OK\n" +
        "\n" +
        "Boot Distributioner Application v0.04\n" +
        "Copyright (C) 2107 Distributioner\n" +
        "    Detecting Sting X ROM\n" +
        "    Detecting Web LNV Extender\n" +
        "    Detecting Heartbeats OK\n" +
        "\n" +
        "\n" +
        "UTGF Device Listening...\n" +
        "\n" +
        "Body   ID   Neural   Device Class\n" +
        "________________________________________\n" +
        "\n" +
        "2      52   Jo152       H515\n" +
        "2      52   Sa5155      H515\n" +
        "2      52   Bo75        H515\n" +
        "2      52   Eri510      H515\n" +
        "1      36   Ell567      H515\n" +
        "1      36   Jos912      H515\n" +
        "0\n";
    private const string BootSplashLocalizedText =
        "<line-height=90%>      BG IG, \u7cfb\u7edf\u6cd5\u6848\u76df\u53cb\n" +
        "      \u7248\u6743\u6240\u6709 (C) 2084-2108, Halden Electronics Inc.\n" +
        "\n" +
        "CPU \u7c7b\u578b     :     BORSON 300 CPU at 2500 MHz\n" +
        "\u5185\u5b58\u6d4b\u8bd5     :     4521586K OK\n" +
        "\n" +
        "\u542f\u52a8 Distributioner \u5e94\u7528 v0.04\n" +
        "\u7248\u6743\u6240\u6709 (C) 2107 Distributioner\n" +
        "    \u68c0\u6d4b Sting X ROM\n" +
        "    \u68c0\u6d4b Web LNV \u6269\u5c55\u5668\n" +
        "    \u68c0\u6d4b\u5fc3\u8df3 OK\n" +
        "\n" +
        "\n" +
        "UTGF \u8bbe\u5907\u76d1\u542c\u4e2d...\n" +
        "\n" +
        "\u8eab\u4f53   ID   \u795e\u7ecf     \u8bbe\u5907\u7c7b\u522b\n" +
        "________________________________________\n" +
        "\n" +
        "2      52   Jo152       H515\n" +
        "2      52   Sa5155      H515\n" +
        "2      52   Bo75        H515\n" +
        "2      52   Eri510      H515\n" +
        "1      36   Ell567      H515\n" +
        "1      36   Jos912      H515\n" +
        "0\n";
    private static readonly string[] BootSplashEnglishMarkers =
    {
        "BG IG, A System-Act Ally",
        "Boot Distributioner Application v0.04",
        "UTGF Device Listening",
        "Body   ID   Neural   Device Class"
    };
    private static readonly string[] BootSplashPollutedMarkers =
    {
        "\u7cfb\u7edf-\u6cd5\u6848\u76df\u53cb",
        "\u7cfb\u7edf\u6cd5\u6848\u76df\u53cb",
        "CPU\u7c7b\u578b",
        "CPU \u7c7b\u578b",
        "\u8bb0\u5fc6\u4f53\u6d4b\u8bd5",
        "\u5185\u5b58\u6d4b\u8bd5",
        "\u542f\u52a8\u5206\u914d\u5668\u5e94\u7528\u7a0b\u5e8f",
        "\u542f\u52a8 Distributioner",
        "\u68c0\u6d4b\u82af\u8df3",
        "\u68c0\u6d4b\u5fc3\u8df3",
        "UTGF\u8bbe\u5907\u76d1\u542c",
        "UTGF \u8bbe\u5907\u76d1\u542c"
    };
    private static readonly Dictionary<string, string> TerminalHeadingEntries = new(StringComparer.Ordinal)
    {
        [">MOONS"] = ">\u661f\u7403 \u3008Moons\u3009",
        ["> MOONS"] = "> \u661f\u7403 \u3008Moons\u3009",
        [">STORE"] = ">\u5546\u5e97 \u3008Store\u3009",
        ["> STORE"] = "> \u5546\u5e97 \u3008Store\u3009",
        [">BESTIARY"] = ">\u56fe\u9274 \u3008Bestiary\u3009",
        ["BESTIARY"] = ">\u56fe\u9274 \u3008Bestiary\u3009",
        [">STORAGE"] = ">\u50a8\u5b58 \u3008Storage\u3009",
        ["> STORAGE"] = "> \u50a8\u5b58 \u3008Storage\u3009",
        [">OTHER"] = ">\u5176\u4ed6 \u3008Other\u3009",
        ["> OTHER"] = "> \u5176\u4ed6 \u3008Other\u3009"
    };
    private static readonly Dictionary<string, string> TerminalBodyEntries = new(StringComparer.Ordinal)
    {
        ["Welcome to the FORTUNE-9 OS"] = "\u6b22\u8fce\u4f7f\u7528 FORTUNE-9 OS",
        ["Courtesy of the Company"] = "\u7531\u516c\u53f8\u63d0\u4f9b",
        ["Type \"Help\" for a list of commands."] = "\u8f93\u5165 \"Help\" \u67e5\u770b\u547d\u4ee4\u5217\u8868\u3002",
        ["Welcome to the Company store"] = "\u6b22\u8fce\u6765\u5230\u516c\u53f8\u5546\u5e97\u3002",
        ["Welcome to the Company store."] = "\u6b22\u8fce\u6765\u5230\u516c\u53f8\u5546\u5e97\u3002",
        ["Good luck."] = "\u795d\u4f60\u597d\u8fd0\u3002",
        ["Use words BUY and INFO on any item."] = "\u53ef\u5bf9\u4efb\u610f\u5546\u54c1\u4f7f\u7528 BUY \u548c INFO \u547d\u4ee4\u3002",
        ["Order tools in bulk by typing a number."] = "\u8f93\u5165\u6570\u91cf\u5373\u53ef\u6279\u91cf\u8d2d\u4e70\u5de5\u5177\u3002",
        ["The selection of ship decor rotates per-quota. Be"] = "\u98de\u8239\u88c5\u9970\u7684\u5546\u54c1\u4f1a\u6309\u914d\u989d\u5468\u671f\u8f6e\u6362\u3002",
        ["sure to check back next week:"] = "\u8bb0\u5f97\u4e0b\u5468\u518d\u6765\u67e5\u770b\uff1a",
        ["To see the company store's selection of useful items."] = "\u8f93\u5165\u4ee5\u67e5\u770b\u516c\u53f8\u5546\u5e97\u7684\u5b9e\u7528\u7269\u54c1\u5217\u8868\u3002",
        ["To see the company store's selection of useful items"] = "\u8f93\u5165\u4ee5\u67e5\u770b\u516c\u53f8\u5546\u5e97\u7684\u5b9e\u7528\u7269\u54c1\u5217\u8868\u3002",
        ["To see the list of moons the autopilot can route to."] = "\u8f93\u5165\u4ee5\u67e5\u770b\u81ea\u52a8\u9a7e\u9a76\u7cfb\u7edf\u53ef\u5230\u8fbe\u7684\u536b\u661f\u5217\u8868\u3002",
        ["To see the list of moons the autopilot can route to"] = "\u8f93\u5165\u4ee5\u67e5\u770b\u81ea\u52a8\u9a7e\u9a76\u7cfb\u7edf\u53ef\u5230\u8fbe\u7684\u536b\u661f\u5217\u8868\u3002",
        ["To access objects placed into storage."] = "\u8f93\u5165\u4ee5\u8bbf\u95ee\u5df2\u653e\u5165\u50a8\u5b58\u533a\u7684\u7269\u54c1\u3002",
        ["To access objects placed into storage"] = "\u8f93\u5165\u4ee5\u8bbf\u95ee\u5df2\u653e\u5165\u50a8\u5b58\u533a\u7684\u7269\u54c1\u3002",
        ["To see the list of other commands."] = "\u8f93\u5165\u4ee5\u67e5\u770b\u5176\u4ed6\u547d\u4ee4\u5217\u8868\u3002",
        ["To see the list of other commands"] = "\u8f93\u5165\u4ee5\u67e5\u770b\u5176\u4ed6\u547d\u4ee4\u5217\u8868\u3002",
        ["To see the list of wildlife on record."] = "\u8f93\u5165\u4ee5\u67e5\u770b\u5df2\u8bb0\u5f55\u7684\u751f\u7269\u5217\u8868\u3002",
        ["To see the list of wildlife on record"] = "\u8f93\u5165\u4ee5\u67e5\u770b\u5df2\u8bb0\u5f55\u7684\u751f\u7269\u5217\u8868\u3002",
        ["To read a log, use keyword \"VIEW\" before its name."] = "\u8981\u9605\u8bfb\u65e5\u5fd7\uff0c\u8bf7\u5728\u540d\u79f0\u524d\u8f93\u5165\u5173\u952e\u5b57 \"VIEW\"\u3002",
        ["Welcome to the exomoons catalogue."] = "\u6b22\u8fce\u67e5\u9605\u5916\u536b\u661f\u76ee\u5f55\u3002",
        ["Welcome to the exomoons catalogue"] = "\u6b22\u8fce\u67e5\u9605\u5916\u536b\u661f\u76ee\u5f55\u3002",
        ["To route the autopilot to a moon, use the word ROUTE."] = "\u8981\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u67d0\u4e2a\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 ROUTE \u6307\u4ee4\u3002",
        ["To route the autopilot to a moon, use the word ROUTE"] = "\u8981\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u67d0\u4e2a\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 ROUTE \u6307\u4ee4\u3002",
        ["To route the autopilot to a moon, use the word"] = "\u8981\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u67d0\u4e2a\u536b\u661f\uff0c\u8bf7\u4f7f\u7528\u6307\u4ee4",
        ["ROUTE."] = "ROUTE\u3002",
        ["INFO."] = "INFO\u3002",
        ["To learn about any moon, use the word INFO."] = "\u8981\u4e86\u89e3\u4efb\u610f\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 INFO \u6307\u4ee4\u3002",
        ["To learn about any moon, use the word INFO"] = "\u8981\u4e86\u89e3\u4efb\u610f\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 INFO \u6307\u4ee4\u3002",
        ["To learn about any moon, use INFO."] = "\u8981\u4e86\u89e3\u4efb\u610f\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 INFO \u6307\u4ee4\u3002",
        ["To learn about any moon, use INFO"] = "\u8981\u4e86\u89e3\u4efb\u610f\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 INFO \u6307\u4ee4\u3002",
        ["Do you want to route the autopilot to the Company building?"] = "\u662f\u5426\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u516c\u53f8\u5927\u697c\uff1f",
        ["Do you want to route the autopilot to the Company"] = "\u662f\u5426\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u516c\u53f8",
        ["building?"] = "\u5927\u697c\uff1f",
        ["Happy [currentDay]."] = "\u4eca\u5929\u662f [currentDay]\u3002",
        ["Sent transmission."] = "\u5df2\u53d1\u9001\u4f20\u8f93\u3002",
        ["Switched radar to player."] = "\u5df2\u5c06\u96f7\u8fbe\u5207\u6362\u5230\u73a9\u5bb6\u3002",
        ["Switching radar cam view."] = "\u6b63\u5728\u5207\u6362\u96f7\u8fbe\u6444\u50cf\u5934\u89c6\u89d2\u3002",
        ["Toggling radar cam"] = "\u6b63\u5728\u5207\u6362\u96f7\u8fbe\u6444\u50cf\u5934\u3002",
        ["Cancelled ejection sequence."] = "\u5df2\u53d6\u6d88\u5f39\u5c04\u5e8f\u5217\u3002",
        ["Cancelled order."] = "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002",
        ["Our contractors enjoy fast, free shipping while on the job! Any purchased items will arrive hourly at your approximate location."] = "\u6211\u4eec\u7684\u627f\u5305\u5546\u5728\u5de5\u4f5c\u671f\u95f4\u4eab\u6709\u5feb\u901f\u514d\u8d39\u914d\u9001\u670d\u52a1\uff0c\u5df2\u8d2d\u7269\u54c1\u4f1a\u6309\u5c0f\u65f6\u9001\u8fbe\u4f60\u5927\u81f4\u6240\u5728\u4f4d\u7f6e\u3002",
        ["There was no action supplied with the word."] = "\u8be5\u5355\u8bcd\u672a\u63d0\u4f9b\u5bf9\u5e94\u52a8\u4f5c\u3002",
        ["There was no object supplied with the action, or your word was typed incorrectly or does not exist."] = "\u672a\u4e3a\u8be5\u52a8\u4f5c\u63d0\u4f9b\u5bf9\u8c61\uff0c\u6216\u4f60\u8f93\u5165\u7684\u5355\u8bcd\u6709\u8bef\u6216\u4e0d\u5b58\u5728\u3002",
        ["This action was not compatible with this object."] = "\u8be5\u52a8\u4f5c\u4e0e\u5f53\u524d\u5bf9\u8c61\u4e0d\u517c\u5bb9\u3002",
        ["An error occured! Try again."] = "\u53d1\u751f\u9519\u8bef\uff0c\u8bf7\u91cd\u8bd5\u3002",
        ["The autopilot ship is already orbiting this moon!"] = "\u81ea\u52a8\u9a7e\u9a76\u98de\u8239\u5df2\u7ecf\u5728\u8fd9\u9897\u536b\u661f\u7684\u8f68\u9053\u4e0a\uff01",
        ["To purchase decorations, the ship cannot be landed."] = "\u8d2d\u4e70\u88c5\u9970\u65f6\uff0c\u98de\u8239\u4e0d\u80fd\u5904\u4e8e\u7740\u9646\u72b6\u6001\u3002",
        ["You have cancelled the order."] = "\u4f60\u5df2\u53d6\u6d88\u8ba2\u5355\u3002",
        ["You selected the Challenge Moon save file. You can't route to another moon during the challenge."] = "\u4f60\u9009\u62e9\u4e86\u6311\u6218\u536b\u661f\u5b58\u6863\u3002\u5728\u6311\u6218\u671f\u95f4\u65e0\u6cd5\u5207\u6362\u822a\u7ebf\u5230\u5176\u4ed6\u536b\u661f\u3002",
        ["Press [B] to rearrange objects in your ship and [V] to confirm."] = "\u6309 [B] \u5728\u98de\u8239\u5185\u6574\u7406\u7269\u54c1\uff0c\u6309 [V] \u786e\u8ba4\u3002",
        ["Press [B] to rearrange fish in your ship and [V] to confirm."] = "\u6309 [B] \u5728\u98de\u8239\u5185\u6574\u7406\u91d1\u9c7c\uff0c\u6309 [V] \u786e\u8ba4\u3002",
        ["Press the button to activate the teleporter. It will teleport whoever is currently being monitored on the ship's radar. You will not be able to keep any of your held items through the teleport. It takes about 10 seconds to recharge."] = "\u6309\u4e0b\u6309\u94ae\u5373\u53ef\u542f\u52a8\u4f20\u9001\u5668\u3002\u5b83\u4f1a\u4f20\u9001\u5f53\u524d\u5728\u98de\u8239\u96f7\u8fbe\u76d1\u89c6\u4e2d\u7684\u76ee\u6807\u3002\u4f20\u9001\u8fc7\u7a0b\u4e2d\u5c06\u65e0\u6cd5\u4fdd\u7559\u624b\u6301\u7269\u54c1\u3002\u51b7\u5374\u65f6\u95f4\u7ea6 10 \u79d2\u3002",
        ["Use the light switch to enable cozy lights."] = "\u4f7f\u7528\u706f\u5149\u5f00\u5173\u542f\u7528\u6e29\u99a8\u706f\u4e32\u3002",
        ["Use the light switch to enable the disco."] = "\u4f7f\u7528\u706f\u5149\u5f00\u5173\u542f\u7528\u8fea\u65af\u79d1\u7403\u3002",
        ["Your electric chair can be activated by any powerful source of voltage!"] = "\u7535\u6905\u53ef\u7531\u4efb\u4f55\u5f3a\u5927\u7535\u538b\u6e90\u6fc0\u6d3b\uff01",
        ["Hold the cord to activate the loud horn."] = "\u62c9\u4f4f\u7ef3\u7d22\u5373\u53ef\u542f\u52a8\u626c\u58f0\u5587\u53ed\u3002",
        ["The signal transmitter can be activated with the \"transmit\" command followed by any message under 10 letters."] = "\u4fe1\u53f7\u53d1\u9001\u5668\u53ef\u901a\u8fc7 \"transmit\" \u547d\u4ee4\u6fc0\u6d3b\uff0c\u540e\u63a5\u4e0d\u8d85\u8fc7 10 \u4e2a\u5b57\u7b26\u7684\u6d88\u606f\u3002",
        ["Press the button and step onto the inverse teleporter while it activates."] = "\u542f\u52a8\u65f6\u6309\u4e0b\u6309\u94ae\u5e76\u8e0f\u4e0a\u9006\u5411\u4f20\u9001\u5668\u3002",
        ["To scan for the number of items left on the current planet"] = "\u626b\u63cf\u5f53\u524d\u661f\u7403\u4e0a\u5269\u4f59\u7269\u54c1\u7684\u6570\u91cf\u3002"
    };
    private static readonly Dictionary<string, string> ControlTipActionEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Push"] = "\u63a8",
        ["Pull up"] = "\u62c9\u8d77",
        ["Pull switch"] = "\u62c9\u52a8\u5f00\u5173",
        ["Sit down"] = "\u5750\u4e0b",
        ["Switch headlight"] = "\u5207\u6362\u8f66\u706f",
        ["Switch headlights"] = "\u5207\u6362\u8f66\u706f",
        ["Toggle cabin window"] = "\u5f00\u5173\u9a7e\u9a76\u5ba4\u8f66\u7a97",
        ["Toggle wiper"] = "\u5207\u6362\u96e8\u5237",
        ["Toggle wipers"] = "\u5207\u6362\u96e8\u5237",
        ["Honk"] = "\u9e23\u7b1b",
        ["Try ignition"] = "\u5c1d\u8bd5\u70b9\u706b",
        ["Eject"] = "\u5f39\u5c04",
        ["Tune radio"] = "\u8c03\u8c10\u6536\u97f3\u673a",
        ["Toggle radio"] = "\u5f00\u5173\u6536\u97f3\u673a",
        ["Gas pedal"] = "\u6cb9\u95e8",
        ["Brake pedal"] = "\u5239\u8f66",
        ["Boost"] = "\u52a0\u901f",
        ["Open hood"] = "\u6253\u5f00\u5f15\u64ce\u76d6",
        ["Shut hood"] = "\u5173\u95ed\u5f15\u64ce\u76d6",
        ["Close hood"] = "\u5173\u95ed\u5f15\u64ce\u76d6",
        ["Use door"] = "\u4f7f\u7528\u95e8",
        ["Exit"] = "\u79bb\u5f00"
    };
    private static readonly Dictionary<string, string> TerminalBilingualOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Shovel"] = "\u94f2\u5b50",
        ["Clock"] = "\u65f6\u949f",
        ["Bell"] = "\u949f",
        ["Brass bell"] = "\u9ec4\u94dc\u949f",
        ["Brush"] = "\u5237\u5b50",
        ["Chemical jug"] = "\u5316\u5b66\u7f50",
        ["Flashlight"] = "\u624b\u7535\u7b52",
        ["Pro flashlight"] = "\u4e13\u4e1a\u624b\u7535\u7b52",
        ["Jetpack"] = "\u55b7\u6c14\u80cc\u5305",
        ["Lock-picker"] = "\u5f00\u9501\u5668",
        ["Lockpicker"] = "\u5f00\u9501\u5668",
        ["Mapper tool"] = "\u6d4b\u7ed8\u5de5\u5177",
        ["Company Cruiser"] = "\u516c\u53f8\u5de1\u822a\u8f66",
        ["Cruiser"] = "\u8d27\u8f66",
        ["Boombox"] = "\u97f3\u54cd",
        ["Cupboard"] = "\u6a71\u67dc",
        ["Extension ladder"] = "\u4f38\u7f29\u68af",
        ["Radar-booster"] = "\u96f7\u8fbe\u589e\u5e45\u5668",
        ["Spray paint"] = "\u55b7\u6f06",
        ["Weed killer"] = "\u9664\u8349\u5242",
        ["Belt bag"] = "\u8170\u5305",
        ["Stun grenade"] = "\u7729\u6655\u624b\u96f7",
        ["Survival kit"] = "\u751f\u5b58\u5957\u88c5",
        ["TZP-Inhalant"] = "TZP \u5438\u5165\u5242",
        ["Walkie-talkie"] = "\u5bf9\u8bb2\u673a",
        ["Zap gun"] = "\u7535\u51fb\u67aa",
        ["Loud horn"] = "\u626c\u58f0\u5587\u53ed",
        ["Signal Translator"] = "\u4fe1\u53f7\u7ffb\u8bd1\u5668",
        ["Teleporter"] = "\u4f20\u9001\u5668",
        ["Inverse Teleporter"] = "\u9006\u5411\u4f20\u9001\u5668",
        ["Record player"] = "\u5531\u7247\u673a",
        ["Romantic table"] = "\u6d6a\u6f2b\u684c",
        ["Toilet"] = "\u9a6c\u6876",
        ["Cozy lights"] = "\u6e29\u99a8\u706f\u4e32",
        ["Disco ball"] = "\u8fea\u65af\u79d1\u7403",
        ["Sofa chair"] = "\u6c99\u53d1\u6905",
        ["Table"] = "\u684c\u5b50",
        ["Jack-o-Lantern"] = "\u5357\u74dc\u706f",
        ["Goldfish"] = "\u91d1\u9c7c",
        ["Classic painting"] = "\u7ecf\u5178\u6cb9\u753b",
        ["Television"] = "\u7535\u89c6",
        ["Electric chair"] = "\u7535\u6905",
        ["Shower"] = "\u6dcb\u6d74\u5668",
        ["Plushie pajama man"] = "\u6bdb\u7ed2\u7761\u8863\u516c\u4ed4",
        ["Dog house"] = "\u72d7\u5c4b",
        ["Fridge"] = "\u51b0\u7bb1",
        ["Microwave"] = "\u5fae\u6ce2\u7089",
        ["Coffee mug"] = "\u5496\u5561\u676f",
        ["Fancy lamp"] = "\u7cbe\u81f4\u706f\u5177",
        ["Plasma ball"] = "\u7b49\u79bb\u5b50\u7403",
        ["Remote"] = "\u9065\u63a7\u5668",
        ["Bunkbeds"] = "\u4e0a\u4e0b\u94fa",
        ["File cabinet"] = "\u6587\u4ef6\u67dc",
        ["Small rug"] = "\u5c0f\u5730\u6bef",
        ["Welcome mat"] = "\u95e8\u53e3\u5730\u57ab",
        ["Green suit"] = "\u7eff\u8272\u5957\u88c5",
        ["Hazard suit"] = "\u9632\u5371\u5957\u88c5",
        ["Pajama suit"] = "\u7761\u8863\u5957\u88c5",
        ["Purple suit"] = "\u7d2b\u8272\u5957\u88c5",
        ["Purple suits"] = "\u7d2b\u8272\u5957\u88c5",
        ["Bunny suit"] = "\u5154\u5b50\u5957\u88c5",
        ["Bee suit"] = "\u871c\u8702\u5957\u88c5",
        ["VIEW MONITOR"] = "\u67e5\u770b\u76d1\u89c6\u5668",
        ["SWITCH"] = "\u5207\u6362\u89c6\u89d2",
        ["PING"] = "\u63d0\u793a",
        ["TRANSMIT"] = "\u53d1\u9001",
        ["SCAN"] = "\u626b\u63cf",
        ["Player name"] = "\u73a9\u5bb6\u540d\u79f0",
        ["Radar booster name"] = "\u96f7\u8fbe\u589e\u5e45\u5668\u540d\u79f0",
        ["message"] = "\u6d88\u606f"
    };
    private static readonly KeyValuePair<string, string>[] ForcedPhraseEntries =
    {
        new("ENTERING THE ATMOSPHERE...", "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42\u2026"),
        new("Entering the atmosphere...", "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42\u2026"),
        new("Autosaving...", "\u81ea\u52a8\u4fdd\u5b58\u4e2d..."),
        new("RECEIVING SIGNAL", "\u6b63\u5728\u63a5\u6536\u4fe1\u53f7"),
        new("Light Switch", "\u7535\u706f\u5f00\u5173"),
        new("Light switch", "\u7535\u706f\u5f00\u5173"),
        new("Ship Magnet Activated", "\u98de\u8239\u78c1\u5438\u5df2\u542f\u7528"),
        new("Ship Magnet Deactivated", "\u98de\u8239\u78c1\u5438\u5df2\u505c\u7528"),
        new("Toggle Sprint", "\u5207\u6362\u51b2\u523a"),
        new("Toggle \u51b2\u523a", "\u5207\u6362\u51b2\u523a"),
        new("Head Bobbing", "\u955c\u5934\u6643\u52a8"),
        new("\u5934\u90e8\u6653\u52a8", "\u955c\u5934\u6643\u52a8"),
        new("Terrain / Grass Detail", "\u5730\u5f62 / \u8349\u4e1b\u7ec6\u8282"),
        new("Motion Blur", "\u52a8\u6001\u6a21\u7cca"),
        new("Pixel Resolution", "\u50cf\u7d20\u5206\u8fa8\u7387"),
        new("Indirect lighting", "\u95f4\u63a5\u5149\u7167"),
        new("Ultra performance", "\u6781\u81f4\u6027\u80fd"),
        new("Performance", "\u6027\u80fd"),
        new("Retro", "\u590d\u53e4"),
        new("Moderate", "\u4e2d\u7b49"),
        new("High (Default)", "\u9ad8\uff08\u9ed8\u8ba4\uff09"),
        new("Subtle", "\u8f7b\u5fae"),
        new("Ultra", "\u6781\u9ad8"),
        new("High", "\u9ad8"),
        new("Medium", "\u4e2d"),
        new("Low", "\u4f4e"),
        new("GRAPHICS", "\u56fe\u5f62"),
        new("Graphics", "\u56fe\u5f62"),
        new("SYSTEMS ONLINE", "\u7cfb\u7edf\u5728\u7ebf"),
        new("HAZARD LEVEL:", "\u5371\u9669\u7b49\u7ea7\uff1a"),
        new("HAZARD LEVEL", "\u5371\u9669\u7b49\u7ea7"),
        new("HAZARD_LEVEL:", "\u5371\u9669\u7b49\u7ea7\uff1a"),
        new("HAZARD_LEVEL", "\u5371\u9669\u7b49\u7ea7"),
        new("Hazard Level:", "\u5371\u9669\u7b49\u7ea7\uff1a"),
        new("Hazard Level", "\u5371\u9669\u7b49\u7ea7"),
        new("Hazard level:", "\u5371\u9669\u7b49\u7ea7\uff1a"),
        new("Hazard level", "\u5371\u9669\u7b49\u7ea7"),
        new("DOOR HYDRAULICS:", "\u8231\u95e8\u6db2\u538b\uff1a"),
        new("DOOR HYDRAULICS", "\u8231\u95e8\u6db2\u538b"),
        new("With detected mods", "\u5305\u542b\u5df2\u68c0\u6d4b\u5230\u7684\u6a21\u7ec4"),
        new("Press \"/\" to talk.", "\u6309 \"/\" \u8bf4\u8bdd\u3002"),
        new("Typing...", "\u8f93\u5165\u4e2d..."),
        new("Join", "\u52a0\u5165"),
        new("Delete", "\u5220\u9664"),
        new("Go back", "\u8fd4\u56de"),
        new("Walk : [W/A/S/D]", "\u79fb\u52a8\uff1a[W/A/S/D]"),
        new("Sprint: [Shift]", "\u51b2\u523a\uff1a[Shift]"),
        new("Scan : [RMB]", "\u626b\u63cf\uff1a[\u53f3\u952e]"),
        new("[Hands full]", "[\u53cc\u624b\u5df2\u6ee1]"),
        new("HANDS FULL", "\u53cc\u624b\u5df2\u6ee1"),
        new("TOTAL:", "\u603b\u4ef7\u503c\uff1a"),
        new("TOTAL", "\u603b\u4ef7\u503c"),
        new("Paycheck!", "\u85aa\u6c34\uff01"),
        new("Paycheck", "\u85aa\u6c34"),
        new("QUOTA REACHED!", "\u914d\u989d\u5df2\u8fbe\u6210\uff01"),
        new("QUOTA REACHED", "\u914d\u989d\u5df2\u8fbe\u6210"),
        new("NEW PROFIT QUOTA:", "\u65b0\u5229\u6da6\u914d\u989d\uff1a"),
        new("NEW PROFIT QUOTA", "\u65b0\u5229\u6da6\u914d\u989d"),
        new("Overtime bonus:", "\u52a0\u73ed\u5956\u91d1\uff1a"),
        new("Overtime bonus", "\u52a0\u73ed\u5956\u91d1"),
        new("Equip to belt : [E]", "\u88c5\u5907\u5230\u8170\u5e26\uff1a[E]"),
        new("Equipped to utility belt!", "\u5df2\u88c5\u5907\u5230\u5de5\u5177\u8170\u5e26\uff01"),
        new("Press TAB to select the utility belt. This can only hold one-handed tools.", "\u6309 TAB \u9009\u62e9\u5de5\u5177\u8170\u5e26\u3002\u5de5\u5177\u8170\u5e26\u53ea\u80fd\u5b58\u653e\u5355\u624b\u5de5\u5177\u3002"),
        new("(Dead)", "\uff08\u6b7b\u4ea1\uff09"),
        new("Deceased", "\u6b7b\u4ea1")
    };

    public static void Initialize(ConfigFile config)
    {
        _temperatureUnit = config.Bind(
            "InfectionStatus",
            "TemperatureUnit",
            TemperatureUnitCelsius,
            "Temperature unit for infection high-fever status prompts. Use Celsius or Fahrenheit. Invalid values fall back to Celsius.");
        _logKnownDynamicHits = config.Bind(
            "Diagnostics",
            "LogKnownDynamicTranslationHits",
            false,
            "Log a small budget of known dynamic translation hits such as votes, random seed, control tips, and chat system messages.");
    }

    public static int EntryCount => ExactMap.Count + RegexEntries.Count;

    public static void ClearCaches()
    {
        TranslationResultCache.Clear();
        CompositeTranslationResultCache.Clear();
        _knownDynamicHitLogCount = 0;
    }

    public static void Load(string pluginDir)
    {
        ExactMap.Clear();
        ExactMapIgnoreCase.Clear();
        CompositeEntries.Clear();
        RegexEntries.Clear();
        RegexPatternSet.Clear();
        WarnedRegexTimeoutPatterns.Clear();
        TranslationResultCache.Clear();
        CompositeTranslationResultCache.Clear();

        var loadedSources = new List<string>();

        LoadPluginCfgDirectories(pluginDir, loadedSources);
        LoadCfgDirectories(loadedSources);

        CompositeEntries.AddRange(ExactMap
            .Where(entry => entry.Key.Length >= 4 && entry.Key != entry.Value)
            .OrderByDescending(entry => entry.Key.Length));

        Plugin.Log.LogInfo($"TranslationService loaded {ExactMap.Count} exact + {RegexEntries.Count} regex entries from {loadedSources.Count} source(s).");
        if (loadedSources.Count > 0)
        {
            Plugin.Log.LogInfo($"TranslationService sources: {string.Join("; ", loadedSources)}");
        }
    }

    public static bool TryTranslate(string? source, out string translated)
    {
        translated = string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (MayBeHighFeverFahrenheitStatus(source) && TryTranslateHighFeverFahrenheitStatus(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            return true;
        }

        if (CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslate(source, out translated, allowRegex: true))
        {
            translated = SanitizeTranslatedText(translated);
            CacheTranslationResult(source, translated);
            return true;
        }

        if (TryTranslateKnownDynamicText(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            CacheTranslationResult(source, translated);
            return true;
        }

        if (TryTranslateExact(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                CacheTranslationResult(source, translated);
                return true;
            }

            CacheTranslationResult(source, null);
            return false;
        }

        if (TryGetCachedTranslation(source, out var cached, out var hasTranslation))
        {
            translated = cached;
            return hasTranslation;
        }

        if (TryPreserveBootSplashText(source, out translated))
        {
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                CacheTranslationResult(source, translated);
                return true;
            }

            CacheTranslationResult(source, null);
            return false;
        }

        if (TryTranslateMapScreenDescription(source, out translated))
        {
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                CacheTranslationResult(source, translated);
                return true;
            }

            CacheTranslationResult(source, null);
            return false;
        }

        if (TryTranslateControlTipItemName(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                CacheTranslationResult(source, translated);
                return true;
            }

            CacheTranslationResult(source, null);
            return false;
        }

        if (TryTranslateRegex(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                CacheTranslationResult(source, translated);
                return true;
            }

            CacheTranslationResult(source, null);
            return false;
        }

        if (!CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslate(source, out translated, allowRegex: true))
        {
            translated = SanitizeTranslatedText(translated);
            CacheTranslationResult(source, translated);
            return true;
        }

        translated = TranslateComposite(source);
        if (!string.Equals(translated, source, StringComparison.Ordinal))
        {
            CacheTranslationResult(source, translated);
            return true;
        }

        CacheTranslationResult(source, null);
        return false;
    }

    public static bool TryTranslateFastExact(string? source, out string translated)
    {
        translated = string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslateFastExact(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            return true;
        }

        if (TryTranslateExact(source, out translated) ||
            TryTranslateForcedPhraseExact(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            return !string.Equals(translated, source, StringComparison.Ordinal);
        }

        if (!CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslateFastExact(source, out translated))
        {
            translated = SanitizeTranslatedText(translated);
            return true;
        }

        if (TryGetCachedTranslation(source, out translated, out var hasTranslation))
        {
            return hasTranslation;
        }

        return false;
    }

    public static bool TryTranslateKnownDynamicText(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (TryTranslateHostModWarning(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "HostModWarning");
        }

        if (TryTranslateTerminalOrderRequest(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "TerminalOrder");
        }

        if (TryTranslateOrderedTerminalItemConfirmation(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "TerminalOrderedItem");
        }

        if (TryTranslateControlTipText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ControlTip");
        }

        if (TryTranslateStandaloneControlText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ControlTip");
        }

        if (TryTranslateScanValueText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ScanValue");
        }

        if (TryTranslateHudRewardLine(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "HudRewards");
        }

        if (TryTranslateRandomSeedText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "RandomSeed");
        }

        if (TryTranslateVotesText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "Votes");
        }

        if (TryTranslateDaysLeftText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "DaysLeft");
        }

        if (TryTranslateSaveFileStatsTextFast(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "SaveFileStats");
        }

        if (TryTranslatePlayersFiredText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "PlayersFired");
        }

        if (source.IndexOf('\n') >= 0 && TryTranslateKnownDynamicLines(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "KnownDynamicLines");
        }

        if (TryTranslateFixedSceneLabel(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "FixedSceneLabel");
        }

        if (TryTranslatePlanetInfoLine(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "PlanetInfo");
        }

        if (TryTranslateMapScreenDescription(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "MapScreen");
        }

        if (TryTranslateTimePeriodText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "TimePeriod");
        }

        if (TryTranslatePlayerStatusText(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "PlayerStatus");
        }

        if (ChatDynamicTranslator.Translate(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "ChatSystem");
        }

        if (TryTranslateEndgameStatLine(source, out translated))
        {
            return FinishKnownDynamicTranslation(source, ref translated, "EndgameStats");
        }

        if (WeightUnitTranslator.CanHandleCheap(source))
        {
            translated = SanitizeTranslatedText(WeightUnitTranslator.Normalize(source));
            if (!string.Equals(translated, source, StringComparison.Ordinal))
            {
                LogKnownDynamicHit("WeightUnit", source, translated);
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static bool FinishKnownDynamicTranslation(string source, ref string translated, string kind)
    {
        translated = SanitizeTranslatedText(translated);
        if (string.Equals(translated, source, StringComparison.Ordinal))
        {
            return false;
        }

        LogKnownDynamicHit(kind, source, translated);
        return true;
    }

    private static bool TryTranslateKnownDynamicLines(string source, out string translated)
    {
        translated = source;
        var lines = source.Split('\n');
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (!TryTranslateKnownDynamicText(content, out var rewrittenLine))
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

    public static bool LooksLikeKnownDynamicText(string? source)
    {
        var previous = _suppressKnownDynamicHitLog;
        _suppressKnownDynamicHitLog = true;
        try
        {
            return TryTranslateKnownDynamicText(source, out var translated) &&
                   !string.Equals(translated, source, StringComparison.Ordinal);
        }
        finally
        {
            _suppressKnownDynamicHitLog = previous;
        }
    }

    public static bool LooksLikeControlTipText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslateControlTipText(source!, out _);
    }

    public static bool LooksLikeRandomSeedText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslateRandomSeedText(source!, out _);
    }

    public static bool LooksLikeVoteText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslateVotesText(source!, out _);
    }

    public static bool LooksLikeDaysLeftText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslateDaysLeftText(source!, out _);
    }

    public static bool LooksLikeEndgameStatText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslateEndgameStatLine(source!, out _);
    }

    public static bool LooksLikePlayerStatusText(string? source)
    {
        return !string.IsNullOrWhiteSpace(source) && TryTranslatePlayerStatusText(source!, out _);
    }

    public static bool LooksLikeWeightUnitText(string? source)
    {
        return WeightUnitTranslator.CanHandleCheap(source);
    }

    public static string NormalizeWeightUnitText(string? source)
    {
        return WeightUnitTranslator.Normalize(source);
    }

    public static string TranslateStunGrenadeControlTip(string? source, bool pinPulled)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source ?? string.Empty;
        }

        var text = source.Trim();
        var match = SafeRegexMatch(
            text,
            @"^(?:Use\s+grenade|Pull\s+pin|Throw\s+grenade|使用雷|使用闪光弹|使用闪光震撼弹|拔出拉环|拉保险针|投掷雷|投掷闪光弹|投掷闪光震撼弹)\s*[:：]\s*(?<key>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return source;
        }

        var key = match.Groups["key"].Value.Trim();
        return pinPulled
            ? $"投掷闪光弹：{key}"
            : $"拔出拉环：{key}";
    }

    private static bool TryTranslateHostModWarning(string source, out string translated)
    {
        translated = source;
        var normalized = NormalizeLoose(StripRichTextTags(source));
        if (string.Equals(
                normalized,
                "The host is detected to be using a modified version of Lethal Company; you are likely to experience unintended behavior. (Modding requires caution and is not supported.)",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                normalized,
                "The host is detected to be using a modified version of Lethal Company; you are likely to experience unintended behavior. (Modifying Lethal Company is dangerous and not recommended.)",
                StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u68c0\u6d4b\u5230\u4e3b\u673a\u6b63\u5728\u4f7f\u7528\u4fee\u6539\u7248 Lethal Company\uff1b\n\u4f60\u53ef\u80fd\u4f1a\u9047\u5230\u5f02\u5e38\u60c5\u51b5\u3002\n\uff08\u4f7f\u7528\u6a21\u7ec4\u9700\u8c28\u614e\uff0c\u4e14\u4e0d\u53d7\u5b98\u65b9\u652f\u6301\u3002\uff09";
            return true;
        }

        return false;
    }

    private static bool TryTranslateTerminalOrderRequest(string source, out string translated)
    {
        return TerminalDynamicTranslator.TranslateOrderRequest(source, out translated);
    }

    private static string NormalizeTerminalTransactionCost(string cost)
    {
        return TerminalDynamicTranslator.NormalizeTransactionCost(cost);
    }

    private static bool TryTranslateFixedSceneLabel(string source, out string translated)
    {
        translated = source;
        var trimmed = StripRichTextTags(source).Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

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

        var daysMatch = SafeRegexMatch(
            trimmed,
            @"^(?<count>\d+)\s+Days?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (daysMatch.Success)
        {
            translated = $"{daysMatch.Groups["count"].Value} \u5929";
            return true;
        }

        return false;
    }

    private static bool TryTranslateControlTipText(string source, out string translated)
    {
        return ControlTipTranslator.Translate(source, out translated);
    }

    private static string NormalizeControlTipAction(string action, out bool impliesHold)
    {
        impliesHold = false;
        var normalized = SafeRegexReplace(action, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        if (normalized.EndsWith(" hold", StringComparison.OrdinalIgnoreCase))
        {
            impliesHold = true;
            normalized = normalized[..^" hold".Length].TrimEnd();
        }

        return normalized;
    }

    private static string NormalizeControlTipSuffix(string suffix, bool actionImpliesHold)
    {
        var normalized = SafeRegexReplace(suffix ?? string.Empty, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        if (actionImpliesHold ||
            normalized.Contains("Hold", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("\u957f\u6309", StringComparison.Ordinal))
        {
            return "\uff08\u957f\u6309\uff09";
        }

        return normalized.Length == 0 ? string.Empty : $" {normalized}";
    }

    private static bool TryTranslateScanValueText(string source, out string translated)
    {
        return HudDynamicTranslator.TranslateScanValue(source, out translated);
    }

    private static bool TryTranslateHudRewardLine(string source, out string translated)
    {
        return HudDynamicTranslator.TranslateRewardLine(source, out translated);
    }

    private static bool TryTranslateStandaloneControlText(string source, out string translated)
    {
        return ControlTipTranslator.TranslateStandalone(source, out translated);
    }

    private static bool TryTranslateRandomSeedText(string source, out string translated)
    {
        return HudDynamicTranslator.TranslateRandomSeed(source, out translated);
    }

    private static bool TryTranslateVotesText(string source, out string translated)
    {
        return HudDynamicTranslator.TranslateVotes(source, out translated);
    }

    private static bool TryTranslateDaysLeftText(string source, out string translated)
    {
        return HudDynamicTranslator.TranslateDaysLeft(source, out translated);
    }

    private static bool TryTranslatePlayersFiredText(string source, out string translated)
    {
        return EndGameDynamicTranslator.TranslatePlayersFired(source, out translated);
    }

    private static bool TryTranslatePlanetInfoLine(string source, out string translated)
    {
        return PlanetInfoDynamicTranslator.TranslateLine(source, out translated);
    }

    private static bool TryTranslateTimePeriodText(string source, out string translated)
    {
        translated = source;
        var trimmed = StripRichTextTags(source).Trim();
        if (string.Equals(trimmed, "AM", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4e0a\u5348";
            return true;
        }

        if (string.Equals(trimmed, "PM", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4e0b\u5348";
            return true;
        }

        var match = SafeRegexMatch(
            source.Trim(),
            @"^(?<time>\d{1,2}:\d{2}(?::\d{2})?)(?<sep>\s+)(?<period>AM|PM)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var period = string.Equals(match.Groups["period"].Value, "AM", StringComparison.OrdinalIgnoreCase)
            ? "\u4e0a\u5348"
            : "\u4e0b\u5348";
        var sep = match.Groups["sep"].Value.Contains('\n') ? "\n" : " ";
        translated = match.Groups["time"].Value + sep + period;
        return true;
    }

    private static bool TryTranslatePlayerStatusText(string source, out string translated)
    {
        return EndGameDynamicTranslator.TranslatePlayerStatus(source, out translated);
    }

    private static bool TryTranslateChatSystemMessage(string source, out string translated)
    {
        return ChatDynamicTranslator.Translate(source, out translated);
    }

    private static bool TryUnwrapColorTag(string source, out string openTag, out string body, out string closeTag)
    {
        openTag = string.Empty;
        body = source;
        closeTag = string.Empty;

        const string close = "</color>";
        if (!source.StartsWith("<color=", StringComparison.OrdinalIgnoreCase) ||
            !source.EndsWith(close, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var openEnd = source.IndexOf('>');
        if (openEnd <= 0)
        {
            return false;
        }

        openTag = source[..(openEnd + 1)];
        closeTag = source.Substring(source.Length - close.Length, close.Length);
        body = source.Substring(openEnd + 1, source.Length - openEnd - 1 - close.Length);
        return true;
    }

    private static bool TryTranslateEndgameStatLine(string source, out string translated)
    {
        return EndGameDynamicTranslator.TranslateStatLine(source, out translated);
    }

    private static bool TryTranslateStatusToken(string source, out string translated)
    {
        translated = source.Trim() switch
        {
            "(Dead)" => "\uff08\u6b7b\u4ea1\uff09",
            "Dead" => "\uff08\u6b7b\u4ea1\uff09",
            "Deceased" => "\uff08\u6b7b\u4ea1\uff09",
            "(Deceased)" => "\uff08\u6b7b\u4ea1\uff09",
            "Missing" => "\uff08\u5931\u8e2a\uff09",
            "(Missing)" => "\uff08\u5931\u8e2a\uff09",
            _ => string.Empty
        };
        return translated.Length > 0;
    }

    private static string BuildTerminalLocalizedItemName(string item)
    {
        var sourceName = NormalizeTerminalArticleItem(item);
        if (TryExtractTerminalBilingualEnglish(sourceName, out var extractedEnglish))
        {
            sourceName = extractedEnglish;
        }

        if (TryGetTerminalBilingualLocalized(sourceName, out var localized) ||
            TryTranslateExact(sourceName, out localized) ||
            TryTranslateRegex(sourceName, out localized))
        {
            localized = SanitizeTranslatedText(localized.Trim());
            return string.IsNullOrWhiteSpace(localized) ? sourceName : localized;
        }

        return sourceName;
    }

    private static string TranslateKnownPlanetInfoValue(string value)
    {
        return PlanetInfoDynamicTranslator.TranslateKnownValue(value);
    }

    private static bool TryTranslateKnownPlanetName(string value, out string translated)
    {
        translated = value;
        foreach (var (english, chinese) in new[]
                 {
                     ("Experimentation", "\u5b9e\u9a8c"),
                     ("Assurance", "\u4fdd\u969c"),
                     ("Vow", "\u8a93\u7ea6"),
                     ("March", "\u4e09\u6708"),
                     ("Offense", "\u8fdb\u653b"),
                     ("Adamance", "\u575a\u5b9a"),
                     ("Rend", "\u6495\u88c2"),
                     ("Dine", "\u9910\u5bb4"),
                     ("Titan", "\u6cf0\u5766"),
                     ("Artifice", "\u9020\u7269"),
                     ("Embrion", "\u80da\u80ce"),
                     ("Gordion", "\u6208\u8fea\u5b89"),
                     ("Company building", "\u516c\u53f8\u5927\u697c"),
                     ("The Company building", "\u516c\u53f8\u5927\u697c")
                 })
        {
            translated = SafeRegexReplace(
                translated,
                $@"(?<![A-Za-z]){Regex.Escape(english)}(?![A-Za-z])",
                chinese,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return !string.Equals(translated, value, StringComparison.Ordinal);
    }

    private static string StripRichTextTags(string source)
    {
        return SafeRegexReplace(source, "<[^>]+>", string.Empty, RegexOptions.CultureInvariant);
    }

    public static bool TryTranslateMapScreenDescription(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source) || !LooksLikeMapScreenDescription(source))
        {
            return false;
        }

        var lines = source.Replace("\r\n", "\n").Split('\n');
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = TranslateMapScreenDescriptionLine(content);
            if (string.Equals(rewritten, content, StringComparison.Ordinal))
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        translated = string.Join("\n", lines);
        translated = SanitizeTranslatedText(translated);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool LooksLikeMapScreenDescription(string source)
    {
        return source.Contains("Orbiting:", StringComparison.Ordinal) ||
               source.Contains("ORBITING:", StringComparison.Ordinal) ||
               source.Contains("Celestial Body:", StringComparison.Ordinal) ||
               source.Contains("CELESTIAL BODY:", StringComparison.Ordinal) ||
               source.Contains("Weather:", StringComparison.Ordinal) ||
               source.Contains("WEATHER:", StringComparison.Ordinal) ||
               source.Contains("Population:", StringComparison.Ordinal) ||
               source.Contains("POPULATION:", StringComparison.Ordinal) ||
               source.Contains("Conditions:", StringComparison.Ordinal) ||
               source.Contains("CONDITIONS:", StringComparison.Ordinal) ||
               source.Contains("Fauna:", StringComparison.Ordinal) ||
               source.Contains("FAUNA:", StringComparison.Ordinal) ||
               source.Contains("地点:", StringComparison.Ordinal) ||
               source.Contains("地点：", StringComparison.Ordinal) ||
               source.Contains("星球:", StringComparison.Ordinal) ||
               source.Contains("星球：", StringComparison.Ordinal) ||
               source.Contains("天体:", StringComparison.Ordinal) ||
               source.Contains("天体：", StringComparison.Ordinal) ||
               source.Contains("入口:", StringComparison.Ordinal) ||
               source.Contains("入口：", StringComparison.Ordinal) ||
               source.Contains("环境:", StringComparison.Ordinal) ||
               source.Contains("环境：", StringComparison.Ordinal) ||
               source.Contains("生态圈:", StringComparison.Ordinal) ||
               source.Contains("生态圈：", StringComparison.Ordinal) ||
               source.Contains("生态群系:", StringComparison.Ordinal) ||
               source.Contains("生态群系：", StringComparison.Ordinal);
    }

    public static string TranslateComposite(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        if (TryPreserveBootSplashText(source, out var preservedBootSplash))
        {
            CacheCompositeTranslationResult(source, preservedBootSplash);
            return preservedBootSplash;
        }

        if (TryTranslateHighFeverFahrenheitStatus(source, out var highFeverFahrenheit))
        {
            return SanitizeTranslatedText(highFeverFahrenheit);
        }

        if (CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslate(source, out var customTranslation, allowRegex: true))
        {
            customTranslation = SanitizeTranslatedText(customTranslation);
            CacheCompositeTranslationResult(source, customTranslation);
            return customTranslation;
        }

        if (TryTranslateKnownDynamicText(source, out var knownDynamic))
        {
            CacheCompositeTranslationResult(source, knownDynamic);
            return knownDynamic;
        }

        if (TryGetCachedCompositeTranslation(source, out var cachedComposite))
        {
            return cachedComposite;
        }

        if (TryTranslateExact(source, out var exact))
        {
            CacheCompositeTranslationResult(source, exact);
            return exact;
        }

        if (TryTranslateControlTipItemName(source, out var controlTipItemName))
        {
            CacheCompositeTranslationResult(source, controlTipItemName);
            return controlTipItemName;
        }

        if (TryTranslateRegex(source, out var regex))
        {
            CacheCompositeTranslationResult(source, regex);
            return regex;
        }

        if (TryTranslateForcedPhraseExact(source, out var forcedExact))
        {
            CacheCompositeTranslationResult(source, forcedExact);
            return forcedExact;
        }

        var translated = source;
        if (TryApplyBuiltInPhraseRegexes(translated, out var phraseRegex))
        {
            translated = phraseRegex;
        }

        foreach (var entry in CompositeEntries)
        {
            if (ShouldApplyCompositeEntryAsSubstring(entry.Key) && translated.Contains(entry.Key))
            {
                translated = translated.Replace(entry.Key, entry.Value);
            }
        }

        foreach (var entry in ForcedPhraseEntries)
        {
            if (ShouldApplyForcedPhraseAsSubstring(entry.Key))
            {
                translated = ReplaceIgnoreCase(translated, entry.Key, entry.Value);
            }
        }

        foreach (var regexEntry in RegexEntries)
        {
            if (TryApplyRegexEntry(translated, regexEntry, out var updated))
            {
                translated = updated;
            }
        }

        if (!CustomLocalizationExtensionService.PreferCustomTranslations &&
            CustomLocalizationExtensionService.TryTranslate(translated, out var customComposite, allowRegex: true))
        {
            translated = customComposite;
        }

        translated = SanitizeTranslatedText(translated);
        CacheCompositeTranslationResult(source, translated);
        return translated;
    }

    private static bool TryTranslateHighFeverFahrenheitStatus(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (!TrySafeRegexMatch(HighFeverFahrenheitRegex, source, out var match) || !match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["fahrenheit"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fahrenheit))
        {
            return false;
        }

        if (UseFahrenheitTemperature())
        {
            translated = $"{match.Groups["prefix"].Value}检测到高烧！\n体温达到 {fahrenheit}°F{match.Groups["suffix"].Value}";
            return true;
        }

        var celsius = (int)Math.Round((fahrenheit - 32d) * 5d / 9d, MidpointRounding.AwayFromZero);
        translated = $"{match.Groups["prefix"].Value}检测到高烧！\n体温达到 {celsius}°C{match.Groups["suffix"].Value}";
        return true;
    }

    private static bool MayBeHighFeverFahrenheitStatus(string source)
    {
        return source.IndexOf("HIGH", StringComparison.OrdinalIgnoreCase) >= 0 &&
               source.IndexOf("FEVER", StringComparison.OrdinalIgnoreCase) >= 0 &&
               source.IndexOf("°F", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool UseFahrenheitTemperature()
    {
        return string.Equals(_temperatureUnit?.Value, TemperatureUnitFahrenheit, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBootSplashText(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var normalized = NormalizeBootSplashText(source);
        if (!normalized.Contains("BG IG", StringComparison.Ordinal) ||
            !normalized.Contains("BORSON 300 CPU", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalized.Contains("<line-height=90%>", StringComparison.Ordinal))
        {
            return true;
        }

        var englishHits = BootSplashEnglishMarkers.Count(marker =>
            normalized.Contains(marker, StringComparison.Ordinal));
        if (englishHits >= 3)
        {
            return true;
        }

        var pollutedHits = BootSplashPollutedMarkers.Count(marker =>
            normalized.Contains(marker, StringComparison.Ordinal));
        return pollutedHits >= 2;
    }

    private static bool TryPreserveBootSplashText(string source, out string preserved)
    {
        preserved = source;
        if (!IsBootSplashText(source))
        {
            return false;
        }

        // This screen is a fixed-width boot diagnostic block; generic phrase translation breaks the border/table layout.
        preserved = ToBootSplashLineEndings(source, BootSplashLocalizedText);
        return true;
    }

    private static string NormalizeBootSplashText(string source)
    {
        return source.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static string ToBootSplashLineEndings(string source, string text)
    {
        return source.Contains("\r\n", StringComparison.Ordinal)
            ? text.Replace("\n", "\r\n")
            : text;
    }

    private static bool TryTranslateForcedPhraseExact(string source, out string translated)
    {
        foreach (var entry in ForcedPhraseEntries)
        {
            if (string.Equals(source.Trim(), entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                translated = entry.Value;
                return true;
            }
        }

        translated = string.Empty;
        return false;
    }

    private static bool TryApplyBuiltInPhraseRegexes(string source, out string translated)
    {
        if (TryTranslateKnownDynamicText(source, out var knownDynamic))
        {
            translated = knownDynamic;
            return true;
        }

        if (TryTranslateControlTipItemName(source, out translated))
        {
            return true;
        }

        if (TryTranslateOrderedTerminalItemConfirmation(source, out translated))
        {
            return true;
        }

        if (TryTranslateSignalTransmitterInstructions(source, out translated))
        {
            return true;
        }

        translated = SafeRegexReplace(
            source,
            @"Press\s+[""\u201c\u201d]\s*/\s*[""\u201c\u201d]\s+to\s+talk\.?",
            "\u6309 \"/\" \u8bf4\u8bdd\u3002",
            RegexOptions.IgnoreCase);
        translated = SafeRegexReplace(
            translated,
            @"^TOTAL:\s*(?<value>.+)$",
            "\u603b\u4ef7\u503c\uff1a ${value}",
            RegexOptions.IgnoreCase);
        translated = SafeRegexReplace(
            translated,
            @"^Equip\s+to\s+belt\s*:\s*\[E\]$",
            "\u88c5\u5907\u5230\u8170\u5e26\uff1a[E]",
            RegexOptions.IgnoreCase);
        return !string.Equals(translated, source, StringComparison.Ordinal);
    }

    private static bool TryTranslateOrderedTerminalItemConfirmation(string source, out string translated)
    {
        return TerminalDynamicTranslator.TranslateOrderedItemConfirmation(source, out translated);
    }

    private static string StandardizeTerminalCruiserWarrantyText(string source)
    {
        return TerminalDynamicTranslator.StandardizeCruiserWarrantyText(source);
    }

    private static string TranslateTerminalOrderDetail(string source)
    {
        return TerminalDynamicTranslator.TranslateOrderDetail(source);
    }

    private static string TranslateTerminalOrderDetailPhrases(string source)
    {
        return TerminalDynamicTranslator.TranslateOrderDetailPhrases(source);
    }

    private static bool TryTranslateSignalTransmitterInstructions(string source, out string translated)
    {
        return TerminalDynamicTranslator.TranslateSignalTransmitterInstructions(source, out translated);
    }

    private static bool TryTranslateControlTipItemName(string source, out string translated)
    {
        translated = source;

        if (!TrySafeRegexMatch(ControlTipItemNameRegex, source, out var match) || !match.Success)
        {
            return false;
        }

        var itemName = match.Groups["item"].Value.Trim();
        if (itemName.Length == 0 || !TryTranslateExact(itemName, out var localizedItemName))
        {
            return false;
        }

        localizedItemName = SanitizeTranslatedText(localizedItemName.Trim());
        if (localizedItemName.Length == 0 || string.Equals(localizedItemName, itemName, StringComparison.Ordinal))
        {
            return false;
        }

        translated = match.Groups["prefix"].Value + "\u4e22\u5f03 " + localizedItemName + match.Groups["suffix"].Value;
        return true;
    }

    private static bool ShouldApplyCompositeEntryAsSubstring(string key)
    {
        if (key.Length >= 12)
        {
            return true;
        }

        foreach (var ch in key)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldApplyForcedPhraseAsSubstring(string key)
    {
        return key.Length >= 12 || key.Contains(' ') || key.Contains('/') || key.Contains(':') || key.Contains('.');
    }

    private static string TranslateMapScreenDescriptionLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var leadingLength = line.Length - line.TrimStart().Length;
        var leading = leadingLength > 0 ? line[..leadingLength] : string.Empty;
        var trimmed = line.Trim();

        string Rewrite(string englishLabel, string chineseLabel, bool translateValue = true)
        {
            if (!trimmed.StartsWith(englishLabel, StringComparison.Ordinal))
            {
                return line;
            }

            var value = trimmed[englishLabel.Length..].Trim();
            var localizedValue = !translateValue
                ? value
                : IsMapScreenWeatherLabel(englishLabel)
                ? LocalizeMapScreenWeather(value)
                : TranslateMapScreenDescriptionValue(value);
            return string.IsNullOrEmpty(localizedValue)
                ? leading + chineseLabel
                : leading + chineseLabel + " " + localizedValue;
        }

        if (trimmed.StartsWith("Orbiting:", StringComparison.Ordinal))
        {
            return Rewrite("Orbiting:", "地点：", translateValue: false);
        }

        if (trimmed.StartsWith("ORBITING:", StringComparison.Ordinal))
        {
            return Rewrite("ORBITING:", "地点：", translateValue: false);
        }

        if (trimmed.StartsWith("地点:", StringComparison.Ordinal))
        {
            return Rewrite("地点:", "地点：", translateValue: false);
        }

        if (trimmed.StartsWith("地点：", StringComparison.Ordinal))
        {
            return Rewrite("地点：", "地点：", translateValue: false);
        }

        if (trimmed.StartsWith("星球:", StringComparison.Ordinal))
        {
            return Rewrite("星球:", "星球：", translateValue: false);
        }

        if (trimmed.StartsWith("星球：", StringComparison.Ordinal))
        {
            return Rewrite("星球：", "星球：", translateValue: false);
        }

        if (trimmed.StartsWith("Celestial Body:", StringComparison.Ordinal))
        {
            return Rewrite("Celestial Body:", "天体:", translateValue: false);
        }

        if (trimmed.StartsWith("CELESTIAL BODY:", StringComparison.Ordinal))
        {
            return Rewrite("CELESTIAL BODY:", "天体:", translateValue: false);
        }

        if (trimmed.StartsWith("天体:", StringComparison.Ordinal))
        {
            return Rewrite("天体:", "天体:", translateValue: false);
        }

        if (trimmed.StartsWith("天体：", StringComparison.Ordinal))
        {
            return Rewrite("天体：", "天体:", translateValue: false);
        }

        if (trimmed.StartsWith("Weather:", StringComparison.Ordinal))
        {
            return Rewrite("Weather:", "天气：");
        }

        if (trimmed.StartsWith("WEATHER:", StringComparison.Ordinal))
        {
            return Rewrite("WEATHER:", "天气：");
        }

        if (trimmed.StartsWith("天气:", StringComparison.Ordinal))
        {
            return Rewrite("天气:", "天气：");
        }

        if (trimmed.StartsWith("天气：", StringComparison.Ordinal))
        {
            return Rewrite("天气：", "天气：");
        }

        if (trimmed.StartsWith("Population:", StringComparison.Ordinal))
        {
            return Rewrite("Population:", "人口：");
        }

        if (trimmed.StartsWith("POPULATION:", StringComparison.Ordinal))
        {
            return Rewrite("POPULATION:", "人口：");
        }

        if (trimmed.StartsWith("人口:", StringComparison.Ordinal))
        {
            return Rewrite("人口:", "人口：");
        }

        if (trimmed.StartsWith("人口：", StringComparison.Ordinal))
        {
            return Rewrite("人口：", "人口：");
        }

        if (trimmed.StartsWith("Entrance:", StringComparison.Ordinal))
        {
            return Rewrite("Entrance:", "入口：");
        }

        if (trimmed.StartsWith("ENTRANCE:", StringComparison.Ordinal))
        {
            return Rewrite("ENTRANCE:", "入口：");
        }

        if (trimmed.StartsWith("入口:", StringComparison.Ordinal))
        {
            return Rewrite("入口:", "入口：");
        }

        if (trimmed.StartsWith("入口：", StringComparison.Ordinal))
        {
            return Rewrite("入口：", "入口：");
        }

        if (trimmed.StartsWith("Conditions:", StringComparison.Ordinal))
        {
            return Rewrite("Conditions:", "环境：");
        }

        if (trimmed.StartsWith("CONDITIONS:", StringComparison.Ordinal))
        {
            return Rewrite("CONDITIONS:", "环境：");
        }

        if (trimmed.StartsWith("Condition:", StringComparison.Ordinal))
        {
            return Rewrite("Condition:", "环境：");
        }

        if (trimmed.StartsWith("环境:", StringComparison.Ordinal))
        {
            return Rewrite("环境:", "环境：");
        }

        if (trimmed.StartsWith("环境：", StringComparison.Ordinal))
        {
            return Rewrite("环境：", "环境：");
        }

        if (trimmed.StartsWith("Fauna:", StringComparison.Ordinal))
        {
            return Rewrite("Fauna:", "生态：");
        }

        if (trimmed.StartsWith("FAUNA:", StringComparison.Ordinal))
        {
            return Rewrite("FAUNA:", "生态：");
        }

        if (trimmed.StartsWith("生态圈:", StringComparison.Ordinal))
        {
            return Rewrite("生态圈:", "生态：");
        }

        if (trimmed.StartsWith("生态圈：", StringComparison.Ordinal))
        {
            return Rewrite("生态圈：", "生态：");
        }

        if (trimmed.StartsWith("生态群系:", StringComparison.Ordinal))
        {
            return Rewrite("生态群系:", "生态：");
        }

        if (trimmed.StartsWith("生态群系：", StringComparison.Ordinal))
        {
            return Rewrite("生态群系：", "生态：");
        }

        if (trimmed.StartsWith("Flora:", StringComparison.Ordinal))
        {
            return Rewrite("Flora:", "植被：");
        }

        if (trimmed.StartsWith("FLORA:", StringComparison.Ordinal))
        {
            return Rewrite("FLORA:", "植被：");
        }

        if (trimmed.StartsWith("History:", StringComparison.Ordinal))
        {
            return Rewrite("History:", "历史：");
        }

        if (trimmed.StartsWith("HISTORY:", StringComparison.Ordinal))
        {
            return Rewrite("HISTORY:", "历史：");
        }

        var translated = TranslateMapScreenDescriptionValue(trimmed);
        return string.Equals(translated, trimmed, StringComparison.Ordinal) ? line : leading + translated;
    }

    private static bool IsMapScreenWeatherLabel(string label)
    {
        return label.StartsWith("Weather", StringComparison.OrdinalIgnoreCase) ||
               label.StartsWith("\u5929\u6c14", StringComparison.Ordinal);
    }

    private static string LocalizeMapScreenWeather(string weather)
    {
        return LocalizeWeatherName(weather, appendWeatherSuffix: false, colorize: true);
    }

    private static string TranslateMapScreenDescriptionValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TryTranslateExact(value, out var translated))
        {
            return SanitizeTranslatedText(translated);
        }

        if (TryTranslateRegex(value, out translated))
        {
            return SanitizeTranslatedText(translated);
        }

        translated = TranslateKnownPlanetInfoValue(value);
        if (!string.Equals(translated, value, StringComparison.Ordinal))
        {
            return SanitizeTranslatedText(translated);
        }

        translated = TranslateComposite(value);
        return SanitizeTranslatedText(translated);
    }

    public static string TranslateTerminalOutput(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source ?? string.Empty;
        }

        var translated = RewriteTerminalRouteWeatherBlocks(source);
        translated = StandardizeTerminalStorePage(translated);
        translated = TranslateTerminalOutputBody(translated);
        translated = StandardizeTerminalCruiserWarrantyText(translated);
        translated = StandardizeTerminalSignalTranslatorText(translated);
        translated = StandardizeTerminalMoonsIntro(translated);
        translated = StandardizeTerminalStoreDecorNotice(translated);
        translated = StandardizeTerminalPageTitles(translated);
        translated = StandardizeTerminalHeadings(translated);
        translated = StandardizeTerminalHelpPage(translated);
        translated = StandardizeTerminalMoonList(translated);
        translated = StandardizeTerminalMoonInfoPages(translated);
        translated = StandardizeTerminalBestiaryPage(translated);
        translated = StandardizeTerminalStorePage(translated);
        translated = StandardizeTerminalStoragePage(translated);
        translated = StandardizeTerminalOtherPage(translated);
        translated = StandardizeTerminalRouteAndEjectPages(translated);
        translated = StandardizeTerminalStoreTransactions(translated);
        translated = StandardizeTerminalGeneralStatus(translated);
        return SanitizeTranslatedText(translated);
    }

    public static string TranslateTerminalOutputForNode(string? source, bool clearPreviousText)
    {
        var original = source ?? string.Empty;
        if (!clearPreviousText && TryTranslateTerminalAppendedResult(original, out var appendedResult))
        {
            return appendedResult;
        }

        var translated = TranslateTerminalOutput(source);
        return clearPreviousText
            ? RestoreTerminalLeadingNewlines(original, translated)
            : TrimTerminalRollingWindow(translated);
    }

    private static bool TryTranslateTerminalAppendedResult(string source, out string translated)
    {
        translated = string.Empty;
        var trimmed = source.TrimEnd();
        if (EndsWithTerminalStatus(trimmed, "Cancelled order.", "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002", out translated) ||
            EndsWithTerminalStatus(trimmed, "You have cancelled the order.", "\u4f60\u5df2\u53d6\u6d88\u8ba2\u5355\u3002", out translated))
        {
            translated = "\n\n\n" + translated;
            return true;
        }

        if (TryExtractLatestTerminalResultBlock(trimmed, out var latestBlock))
        {
            translated = RestoreTerminalLeadingNewlines(latestBlock, TranslateTerminalOutput(latestBlock));
            return true;
        }

        return false;
    }

    private static bool TryExtractLatestTerminalResultBlock(string source, out string latestBlock)
    {
        latestBlock = string.Empty;
        foreach (var marker in new[]
                 {
                     "\nOrdered ",
                     "\nThe Company is buying at ",
                     "\nThe Company is buying your goods at ",
                     "\nYou have requested to order ",
                     "\nThe cost to route to ",
                     "\nRouting autopilot to "
                 })
        {
            var index = source.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            latestBlock = "\n\n\n" + source[(index + 1)..].TrimStart('\r', '\n');
            return true;
        }

        foreach (var marker in new[]
                 {
                     "Ordered ",
                     "The Company is buying at ",
                     "The Company is buying your goods at ",
                     "You have requested to order ",
                     "The cost to route to ",
                     "Routing autopilot to "
                 })
        {
            if (!source.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            latestBlock = "\n\n\n" + source.TrimStart('\r', '\n');
            return true;
        }

        return false;
    }

    private static bool EndsWithTerminalStatus(string source, string status, string replacement, out string translated)
    {
        translated = string.Empty;
        if (!source.EndsWith(status, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefixLength = source.Length - status.Length;
        if (prefixLength > 0 && !char.IsWhiteSpace(source[prefixLength - 1]))
        {
            return false;
        }

        translated = replacement;
        return true;
    }

    private static string RestoreTerminalLeadingNewlines(string source, string translated)
    {
        var expected = CountLeadingNewlines(source);
        if (expected <= 0)
        {
            return translated;
        }

        var actual = CountLeadingNewlines(translated);
        return actual >= expected
            ? translated
            : new string('\n', expected - actual) + translated;
    }

    private static int CountLeadingNewlines(string value)
    {
        var count = 0;
        while (count < value.Length && value[count] == '\n')
        {
            count++;
        }

        return count;
    }

    private static string TrimTerminalRollingWindow(string source)
    {
        if (string.IsNullOrEmpty(source) || source.Length <= TerminalRollingWindowChars)
        {
            return source;
        }

        var start = source.Length - TerminalRollingWindowChars;
        var newline = source.IndexOf('\n', start);
        if (newline >= 0 && newline < source.Length - 1)
        {
            return source[(newline + 1)..];
        }

        return source[start..];
    }

    private static string StandardizeTerminalSignalTranslatorText(string source)
    {
        return TryTranslateSignalTransmitterInstructions(source, out var translated)
            ? translated
            : source;
    }

    private static string StandardizeTerminalMoonsIntro(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var updated = source;
        updated = SafeRegexReplace(
            updated,
            @"Welcome to the exomoons catalogue\.",
            "\u6b22\u8fce\u67e5\u9605\u5916\u536b\u661f\u76ee\u5f55\u3002",
            RegexOptions.IgnoreCase);
        updated = SafeRegexReplace(
            updated,
            @"To route the autopilot to a moon,\s*use the word\s+ROUTE\.",
            "\u8981\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u67d0\u4e2a\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 ROUTE \u6307\u4ee4\u3002",
            RegexOptions.IgnoreCase);
        updated = SafeRegexReplace(
            updated,
            @"To learn about any moon,\s*use(?:\s+the word)?\s+INFO\.?",
            "\u8981\u4e86\u89e3\u4efb\u610f\u536b\u661f\uff0c\u8bf7\u4f7f\u7528 INFO \u6307\u4ee4\u3002",
            RegexOptions.IgnoreCase);
        return updated;
    }

    private static string StandardizeTerminalStoreDecorNotice(string source)
    {
        if (string.IsNullOrEmpty(source) ||
            !source.Contains("The selection of ship decor rotates per-quota.", StringComparison.Ordinal))
        {
            return source;
        }

        return SafeRegexReplace(
            source,
            @"The selection of ship decor rotates per-quota\.\s*Be\s+sure to check back next week:",
            "\u98de\u8239\u88c5\u9970\u7684\u5546\u54c1\u4f1a\u6309\u914d\u989d\u5468\u671f\u8f6e\u6362\u3002\n\u8bb0\u5f97\u4e0b\u5468\u518d\u6765\u67e5\u770b\uff1a",
            RegexOptions.IgnoreCase);
    }

    private static bool TryTranslateExact(string source, out string translated)
    {
        translated = string.Empty;

        if (ExactMap.TryGetValue(source, out translated))
        {
            return true;
        }

        var normalized = source.Replace("\r\n", "\n");
        if (!ReferenceEquals(normalized, source) && ExactMap.TryGetValue(normalized, out translated))
        {
            return true;
        }

        var trimmed = normalized.Trim();
        if (trimmed.Length != normalized.Length && ExactMap.TryGetValue(trimmed, out var trimmedTranslation))
        {
            translated = normalized.Replace(trimmed, trimmedTranslation);
            return true;
        }

        if (trimmed.EndsWith("\\", StringComparison.Ordinal))
        {
            var withoutTrailingSlash = trimmed.TrimEnd('\\').TrimEnd();
            if (ExactMap.TryGetValue(withoutTrailingSlash, out var slashTranslation))
            {
                translated = normalized.Replace(withoutTrailingSlash, slashTranslation);
                return true;
            }
        }

        if (ExactMapIgnoreCase.TryGetValue(trimmed, out var caseInsensitiveTranslation))
        {
            translated = trimmed.Length == normalized.Length
                ? caseInsensitiveTranslation
                : normalized.Replace(trimmed, caseInsensitiveTranslation);
            return true;
        }

        var looseNormalized = NormalizeLoose(trimmed);
        if (looseNormalized.Length > 0 && ExactMap.TryGetValue(looseNormalized, out var looseTranslation))
        {
            translated = trimmed.Length == normalized.Length
                ? looseTranslation
                : normalized.Replace(trimmed, looseTranslation);
            return true;
        }

        if (looseNormalized.Length > 0 && ExactMapIgnoreCase.TryGetValue(looseNormalized, out var looseCaseTranslation))
        {
            translated = trimmed.Length == normalized.Length
                ? looseCaseTranslation
                : normalized.Replace(trimmed, looseCaseTranslation);
            return true;
        }

        return false;
    }

    private static bool TryTranslateRegex(string source, out string translated)
    {
        foreach (var entry in RegexEntries)
        {
            if (TryApplyRegexEntry(source, entry, out translated))
            {
                return true;
            }
        }

        translated = source;
        return false;
    }

    private static string TranslateTerminalOutputBody(string source)
    {
        if (TryTranslateKnownDynamicText(source, out var knownDynamic))
        {
            return TranslateTerminalOutputLinewise(knownDynamic);
        }

        if (TryTranslateExact(source, out var translated))
        {
            return translated;
        }

        if (TryTranslateRegex(source, out translated))
        {
            return translated;
        }

        return TranslateTerminalOutputLinewise(source);
    }

    private static string TranslateTerminalOutputLinewise(string source)
    {
        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var leadingLength = content.Length - content.TrimStart().Length;
            var leading = leadingLength > 0 ? content[..leadingLength] : string.Empty;
            var trimmedContent = content.Trim();

            if (!TryTranslateTerminalBodyLine(trimmedContent, out var translatedLine))
            {
                continue;
            }

            var rebuilt = leading + translatedLine;
            lines[i] = hasCarriageReturn ? rebuilt + "\r" : rebuilt;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static bool TryTranslateTerminalBodyLine(string line, out string translated)
    {
        translated = line;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        if (TerminalBodyEntries.TryGetValue(line, out translated))
        {
            return true;
        }

        if (TryTranslateKnownDynamicText(line, out translated))
        {
            return !string.Equals(translated, line, StringComparison.Ordinal);
        }

        if (TryTranslateExact(line, out translated))
        {
            return !string.Equals(translated, line, StringComparison.Ordinal);
        }

        if (TryTranslateRegex(line, out translated))
        {
            return !string.Equals(translated, line, StringComparison.Ordinal);
        }

        return false;
    }

    private static string StandardizeTerminalPageTitles(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (ContainsTerminalKnownHeading(source))
        {
            return source;
        }

        if (source.Contains("\u6b22\u8fce\u67e5\u9605\u5916\u536b\u661f\u76ee\u5f55", StringComparison.Ordinal))
        {
            return EnsureTerminalPageHeading(source, TerminalHeadingEntries[">MOONS"], 3);
        }

        if (source.Contains("\u6b22\u8fce\u6765\u5230\u516c\u53f8\u5546\u5e97", StringComparison.Ordinal) ||
            source.Contains("\u5728\u4efb\u610f\u5546\u54c1\u4e0a\u53ef\u4f7f\u7528 BUY \u548c INFO \u547d\u4ee4", StringComparison.Ordinal) ||
            source.Contains("//  Price:", StringComparison.Ordinal))
        {
            return EnsureTerminalPageHeading(source, TerminalHeadingEntries[">STORE"], 3);
        }

        if (source.Contains("\u4ee5\u4e0b\u662f\u4ed3\u5e93\u4e2d\u7684\u7269\u54c1", StringComparison.Ordinal) ||
            source.Contains("\u65e0\u6cd5\u4ece\u4ed3\u5e93\u53d6\u56de\u7269\u54c1", StringComparison.Ordinal) ||
            source.Contains("These are the items in storage:", StringComparison.Ordinal))
        {
            return EnsureTerminalPageHeading(source, TerminalHeadingEntries[">STORAGE"], 3);
        }

        if (source.Contains("\u5176\u4ed6\u547d\u4ee4\uff1a", StringComparison.Ordinal) ||
            source.Contains(">VIEW MONITOR", StringComparison.Ordinal))
        {
            return EnsureTerminalPageHeading(source, TerminalHeadingEntries[">OTHER"], 3);
        }

        if (source.Contains("\u8f93\u5165\u4ee5\u67e5\u770b\u5df2\u8bb0\u5f55\u7684\u751f\u7269\u5217\u8868", StringComparison.Ordinal) ||
            source.Contains("To see the list of wildlife on record", StringComparison.Ordinal))
        {
            return EnsureTerminalPageHeading(source, TerminalHeadingEntries["BESTIARY"], 3);
        }

        return source;
    }

    private static bool ContainsTerminalKnownHeading(string source)
    {
        var lines = source.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TryStandardizeTerminalHeading(line, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string StandardizeTerminalHelpPage(string source)
    {
        if (string.IsNullOrEmpty(source) ||
            (!source.Contains(TerminalHeadingEntries[">MOONS"], StringComparison.Ordinal) &&
             !source.Contains(TerminalHeadingEntries[">STORE"], StringComparison.Ordinal) &&
             !source.Contains(TerminalHeadingEntries["BESTIARY"], StringComparison.Ordinal) &&
             !source.Contains(TerminalHeadingEntries[">STORAGE"], StringComparison.Ordinal) &&
             !source.Contains(TerminalHeadingEntries[">OTHER"], StringComparison.Ordinal)))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalHelpLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalHelpLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        if (!TerminalBodyEntries.TryGetValue(trimmed, out var translated))
        {
            return line;
        }

        var leadingLength = line.Length - line.TrimStart().Length;
        var leading = leadingLength > 0 ? line[..leadingLength] : string.Empty;
        return leading + translated;
    }

    private static string EnsureTerminalPageHeading(string source, string heading, int leadingBlankLines = 0)
    {
        var trimmed = source.TrimStart('\r', '\n');
        if (trimmed.StartsWith(heading, StringComparison.Ordinal))
        {
            return source;
        }

        var prefix = leadingBlankLines <= 0 ? string.Empty : string.Concat(Enumerable.Repeat("\n", leadingBlankLines));
        return prefix + heading + "\n" + trimmed;
    }

    private static string StandardizeTerminalHeadings(string source)
    {
        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var trimmed = content.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!TryStandardizeTerminalHeading(trimmed, out var heading))
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? heading + "\r" : heading;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static bool TryStandardizeTerminalHeading(string line, out string translated)
    {
        translated = line;
        var normalized = SafeRegexReplace(line, "<[^>]+>", string.Empty).Trim();
        normalized = normalized.TrimStart('>').Trim();
        normalized = StripTerminalHeadingDecoration(normalized);

        return normalized switch
        {
            "MOONS" or "Moons" or "\u661f\u7403" => TryTranslateTerminalHeadingExact(">MOONS", out translated),
            "STORE" or "Store" or "\u5546\u5e97" => TryTranslateTerminalHeadingExact(">STORE", out translated),
            "BESTIARY" or "Bestiary" or "\u56fe\u9274" or "\u751f\u7269" or "\u751f\u7269\u56fe\u9274" => TryTranslateTerminalHeadingExact("BESTIARY", out translated),
            "STORAGE" or "Storage" or "\u50a8\u5b58" or "\u5b58\u50a8" => TryTranslateTerminalHeadingExact(">STORAGE", out translated),
            "OTHER" or "Other" or "\u5176\u4ed6" => TryTranslateTerminalHeadingExact(">OTHER", out translated),
            _ => false
        };
    }

    private static string StripTerminalHeadingDecoration(string normalized)
    {
        var cut = -1;
        foreach (var separator in new[] { '<', '(', '\u3008', '\u300a' })
        {
            var index = normalized.IndexOf(separator);
            if (index >= 0 && (cut < 0 || index < cut))
            {
                cut = index;
            }
        }

        return cut >= 0 ? normalized[..cut].Trim() : normalized;
    }

    private static bool TryTranslateTerminalHeadingExact(string key, out string translated)
    {
        if (TerminalHeadingEntries.TryGetValue(key, out translated))
        {
            return true;
        }

        translated = key;
        return false;
    }

    private static string StandardizeTerminalMoonList(string source)
    {
        if (string.IsNullOrEmpty(source) || !source.Contains("ROUTE", StringComparison.Ordinal))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalMoonLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalMoonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var companyMatch = SafeRegexMatch(line, @"^\* The Company building\s*//\s*Buying at\s+(.+)$");
        if (companyMatch.Success)
        {
            return $"* \u516c\u53f8\u5927\u697c   //   <color=#F2C14E>\u6536\u8d2d\u4ef7 {companyMatch.Groups[1].Value}</color>";
        }

        var moonMatch = SafeRegexMatch(line, @"^\* (?<name>.+?) \((?<weather>Stormy|Foggy|Eclipsed|Flooded|Rainy)\)\s*$", RegexOptions.IgnoreCase);
        if (!moonMatch.Success)
        {
            return line;
        }

        var moonName = moonMatch.Groups["name"].Value;
        var weather = moonMatch.Groups["weather"].Value;
        var formattedWeather = weather.ToLowerInvariant() switch
        {
            "stormy" => "<color=#D6A23C>\u66b4\u98ce</color>",
            "foggy" => "<color=#9FB7C4>\u6d53\u96fe</color>",
            "eclipsed" => "<color=#C74B50>\u65e5\u98df</color>",
            "flooded" => "<color=#4DA3FF>\u6d2a\u6c34</color>",
            "rainy" => "<color=#6BC5FF>\u964d\u96e8</color>",
            _ => weather
        };

        return $"* {moonName} \uff08{formattedWeather}\uff09";
    }

    private static string StandardizeTerminalMoonInfoPages(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (!source.Contains("CONDITIONS:", StringComparison.Ordinal) &&
            !source.Contains("HISTORY:", StringComparison.Ordinal) &&
            !source.Contains("FAUNA:", StringComparison.Ordinal) &&
            !source.Contains("Sigurd's danger level", StringComparison.Ordinal) &&
            !source.Contains("Scientific name", StringComparison.Ordinal))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalMoonInfoLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string StandardizeTerminalBestiaryPage(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalBestiaryLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalBestiaryLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        if (trimmed.StartsWith("Sigurd's danger level:", StringComparison.Ordinal))
        {
            return line.Replace("Sigurd's danger level:", "Sigurd 危险等级：");
        }

        if (trimmed.StartsWith("Scientific name:", StringComparison.Ordinal))
        {
            return line.Replace("Scientific name:", "学名：");
        }

        if (trimmed.StartsWith("Population:", StringComparison.Ordinal))
        {
            return line.Replace("Population:", "估计种群：");
        }

        return line;
    }

    private static string RewriteTerminalMoonInfoLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var trimmed = line.Trim();
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CONDITIONS:"] = "\u73af\u5883\u6761\u4ef6\uff1a",
            ["HISTORY:"] = "\u5386\u53f2\uff1a",
            ["FAUNA:"] = "\u751f\u6001\u7fa4\u7cfb\uff1a",
            ["Sigurd's danger level:"] = "Sigurd \u5371\u9669\u7b49\u7ea7\uff1a",
            ["Scientific name:"] = "\u5b66\u540d\uff1a",
            ["Population:"] = "\u4f30\u8ba1\u79cd\u7fa4\uff1a"
        };

        if (!replacements.TryGetValue(trimmed, out var replaced))
        {
            return line;
        }

        var leadingLength = line.Length - line.TrimStart().Length;
        var leading = leadingLength > 0 ? line[..leadingLength] : string.Empty;
        return leading + replaced;
    }

    private static string StandardizeTerminalStorePage(string source)
    {
        if (string.IsNullOrEmpty(source) ||
            ((!source.Contains("//", StringComparison.Ordinal) ||
              (!source.Contains("Price", StringComparison.OrdinalIgnoreCase) &&
               !source.Contains("\u4ef7\u683c", StringComparison.Ordinal))) &&
             !source.Contains("SHIP UPGRADES:", StringComparison.Ordinal) &&
             !source.Contains("\u98de\u8239\u5347\u7ea7\uff1a", StringComparison.Ordinal)))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalStoreLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalStoreLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var trimmed = line.Trim();
        if (string.Equals(trimmed, "Ship upgrades:", StringComparison.Ordinal))
        {
            return "\u98de\u8239\u5347\u7ea7\uff1a";
        }

        if (string.Equals(trimmed, "SHIP UPGRADES:", StringComparison.Ordinal))
        {
            return "\u98de\u8239\u5347\u7ea7\uff1a";
        }

        var itemMatch = SafeRegexMatch(line, @"^(?<indent>\s*)(?<bullet>\*\s*)?(?<name>.+?)\s+//\s+(?:Price|\u4ef7\u683c)\s*[:\uff1a]\s*[$\u25a0]?(?<price>\d+)(?<discount>\s+\((?<percent>\d+)%\s*(?:OFF!|\u6298\u6263)\))?\s*$");
        if (!itemMatch.Success)
        {
            return line;
        }

        var indent = itemMatch.Groups["indent"].Value;
        var bullet = itemMatch.Groups["bullet"].Value;
        var name = itemMatch.Groups["name"].Value.Trim();
        var price = itemMatch.Groups["price"].Value;
        var percent = itemMatch.Groups["percent"].Success ? itemMatch.Groups["percent"].Value : null;
        var bilingualName = BuildChineseFirstBilingual(name);
        var discount = string.IsNullOrEmpty(percent) ? string.Empty : $" <color={ResolveTerminalDiscountColor(percent)}>\uff08{percent}% \u6298\u6263\uff09</color>";
        return $"{indent}{bullet}{bilingualName}  //  \u4ef7\u683c: \u25a0{price}{discount}";
    }

    private static string ResolveTerminalDiscountColor(string percent)
    {
        if (!int.TryParse(percent, out var value))
        {
            return "#F2C14E";
        }

        return value >= 50
            ? "#FF8C42"
            : value >= 30
                ? "#F2C14E"
                : "#9FB7C4";
    }

    private static string StandardizeTerminalStoragePage(string source)
    {
        if (string.IsNullOrEmpty(source) ||
            (!source.Contains("These are the items in storage:", StringComparison.Ordinal) &&
             !source.Contains("\u4ee5\u4e0b\u662f\u4ed3\u5e93\u4e2d\u7684\u7269\u54c1", StringComparison.Ordinal)))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalStorageLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalStorageLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        if (string.Equals(trimmed, "While moving furniture with [B], you can press [X] to send it to storage. You can call it back from storage here.", StringComparison.Ordinal))
        {
            return "\u79fb\u52a8\u5bb6\u5177\u65f6\u6309 [B]\uff0c\u4f60\u53ef\u4ee5\u6309 [X] \u5c06\u5176\u9001\u5165\u50a8\u5b58\u533a\u3002\u4f60\u53ef\u4ee5\u5728\u8fd9\u91cc\u4ece\u50a8\u5b58\u533a\u53ec\u56de\u5b83\u3002";
        }

        if (string.Equals(trimmed, "These are the items in storage:", StringComparison.Ordinal))
        {
            return "\u4ee5\u4e0b\u662f\u50a8\u5b58\u533a\u4e2d\u7684\u7269\u54c1\uff1a";
        }

        if (trimmed.StartsWith(">", StringComparison.Ordinal))
        {
            return line;
        }

        var leadingLength = line.Length - line.TrimStart().Length;
        var leading = leadingLength > 0 ? line[..leadingLength] : string.Empty;
        var bilingual = BuildChineseFirstBilingual(trimmed);
        return bilingual == trimmed ? line : leading + bilingual;
    }

    private static string StandardizeTerminalOtherPage(string source)
    {
        if (string.IsNullOrEmpty(source) ||
            (!source.Contains(">VIEW MONITOR", StringComparison.Ordinal) &&
             !source.Contains("\u5176\u4ed6\u547d\u4ee4\uff1a", StringComparison.Ordinal)))
        {
            return source;
        }

        var lines = source.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalOtherLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : source;
    }

    private static string RewriteTerminalOtherLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return line;
        }

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Other commands:"] = "\u5176\u4ed6\u547d\u4ee4\uff1a",
            [">VIEW MONITOR"] = ">VIEW MONITOR\uff08\u67e5\u770b\u76d1\u89c6\u5668\uff09",
            [">SWITCH [Player name]"] = ">SWITCH\uff08\u5207\u6362\u89c6\u89d2\uff09 [Player name / \u73a9\u5bb6\u540d\u79f0]",
            [">PING [Radar booster name]"] = ">PING\uff08\u63d0\u793a\uff09 [Radar booster name / \u96f7\u8fbe\u589e\u5e45\u5668\u540d\u79f0]",
            [">TRANSMIT [message]"] = ">TRANSMIT\uff08\u53d1\u9001\uff09 [message / \u6d88\u606f]",
            [">SCAN"] = ">SCAN\uff08\u626b\u63cf\uff09",
            ["To toggle on AND off the main monitor's map cam"] = "\u5f00\u542f\u6216\u5173\u95ed\u4e3b\u76d1\u89c6\u5668\u7684\u5730\u56fe\u6444\u50cf\u3002",
            ["To switch view to a player on the main monitor"] = "\u5c06\u4e3b\u76d1\u89c6\u5668\u7684\u89c6\u89d2\u5207\u6362\u5230\u6307\u5b9a\u73a9\u5bb6\u3002",
            ["To make a radar booster play a noise."] = "\u8ba9\u96f7\u8fbe\u589e\u5e45\u5668\u64ad\u653e\u63d0\u793a\u97f3\u3002",
            ["To transmit a message with the signal translator"] = "\u4f7f\u7528\u4fe1\u53f7\u7ffb\u8bd1\u5668\u53d1\u9001\u4e00\u6761\u6d88\u606f\u3002",
            ["To scan for the number of items left on the current planet."] = "\u626b\u63cf\u5f53\u524d\u661f\u7403\u4e0a\u5269\u4f59\u7269\u54c1\u7684\u6570\u91cf\u3002"
        };

        if (!replacements.TryGetValue(trimmed, out var replaced))
        {
            return line;
        }

        var leadingLength = line.Length - line.TrimStart().Length;
        var leading = leadingLength > 0 ? line[..leadingLength] : string.Empty;
        return leading + replaced;
    }

    private static string StandardizeTerminalRouteAndEjectPages(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var blockRewritten = RewriteTerminalRouteWeatherBlocks(source);
        var lines = blockRewritten.Split('\n');
        var changed = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            var rewritten = RewriteTerminalRouteOrEjectLine(content);
            if (rewritten == content)
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? rewritten + "\r" : rewritten;
            changed = true;
        }

        return changed ? string.Join("\n", lines) : blockRewritten;
    }

    private static string RewriteTerminalRouteWeatherBlocks(string source)
    {
        var updated = SafeRegexReplace(
            source,
            @"The cost to route to (?<moon>[^\r\n.]+?) is (?<cost>[^.\r\n]+?)\. It is\s*(?:\r?\n\s*)?currently (?<weather>[^\r\n.]+?) on this moon\.",
            m =>
            {
                var moon = m.Groups["moon"].Value.Trim();
                var cost = m.Groups["cost"].Value.Trim();
                var weather = LocalizeTerminalRouteWeather(m.Groups["weather"].Value.Trim());
                return $"\u524d\u5f80 {moon} \u7684\u5bfc\u822a\u8d39\u7528\u4e3a {cost}\u3002\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a {weather}\u3002";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        updated = SafeRegexReplace(
            updated,
            @"\u524d\u5f80\s+(?<moon>.+?)\s+\u7684(?:\u5bfc\u822a)?\u8d39\u7528\u4e3a\s*(?<cost>[^.\u3002\r\n]+)[.\u3002]?\s*(?:It is\s*)?(?:\r?\n\s*)?currently (?<weather>[^\r\n.]+?) on this moon\.",
            m =>
            {
                var moon = m.Groups["moon"].Value.Trim();
                var cost = m.Groups["cost"].Value.Trim();
                var weather = LocalizeTerminalRouteWeather(m.Groups["weather"].Value.Trim());
                return $"\u524d\u5f80 {moon} \u7684\u5bfc\u822a\u8d39\u7528\u4e3a {cost}\u3002\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a {weather}\u3002";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return updated;
    }

    private static string RewriteTerminalRouteOrEjectLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var routeCostMatch = SafeRegexMatch(line, @"^The cost to route to (?<moon>.+?) is (?<cost>.+?)\. It is currently (?<weather>.+?) on this moon\.$", RegexOptions.IgnoreCase);
        if (routeCostMatch.Success)
        {
            var moon = routeCostMatch.Groups["moon"].Value.Trim();
            var cost = routeCostMatch.Groups["cost"].Value.Trim();
            var weather = LocalizeTerminalRouteWeather(routeCostMatch.Groups["weather"].Value.Trim());
            return $"\u524d\u5f80 {moon} \u7684\u5bfc\u822a\u8d39\u7528\u4e3a {cost}\u3002\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a {weather}\u3002";
        }

        var routingMatch = SafeRegexMatch(line, @"^Routing autopilot to (?<moon>.+?)\.$", RegexOptions.IgnoreCase);
        if (routingMatch.Success)
        {
            return $"\u6b63\u5728\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3 {routingMatch.Groups["moon"].Value.Trim()}\u3002";
        }

        var balanceMatch = SafeRegexMatch(line, @"^Your new balance is (?<balance>.+?)\.$", RegexOptions.IgnoreCase);
        if (balanceMatch.Success)
        {
            return $"\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {balanceMatch.Groups["balance"].Value.Trim()}\u3002";
        }

        var partialWeatherMatch = SafeRegexMatch(line, @"^(?<prefix>\u524d\u5f80 .+? \u7684(?:\u5bfc\u822a)?\u8d39\u7528\u4e3a .+?(?:\u3002|\.))\s*(?:It is\s*)?Currently (?<weather>.+?) on this moon\.$", RegexOptions.IgnoreCase);
        if (partialWeatherMatch.Success)
        {
            var weather = LocalizeTerminalRouteWeather(partialWeatherMatch.Groups["weather"].Value.Trim());
            return $"{partialWeatherMatch.Groups["prefix"].Value}\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a {weather}\u3002";
        }

        var standaloneWeatherMatch = SafeRegexMatch(line.Trim(), @"^Currently (?<weather>.+?) on this moon\.$", RegexOptions.IgnoreCase);
        if (standaloneWeatherMatch.Success)
        {
            var weather = LocalizeTerminalRouteWeather(standaloneWeatherMatch.Groups["weather"].Value.Trim());
            return $"\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a {weather}\u3002";
        }

        var localizedWeatherMatch = SafeRegexMatch(line.Trim(), @"^(?<prefix>.*\u8be5\u536b\u661f\u5f53\u524d\u5929\u6c14\u4e3a\s*)(?<weather>mild weather|stormy|foggy|eclipsed|flooded|rainy)(?<suffix>[\u3002\.]?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (localizedWeatherMatch.Success)
        {
            var weather = LocalizeTerminalRouteWeather(localizedWeatherMatch.Groups["weather"].Value.Trim());
            var suffix = localizedWeatherMatch.Groups["suffix"].Value;
            return $"{localizedWeatherMatch.Groups["prefix"].Value}{weather}{(suffix.Length == 0 ? "\u3002" : suffix)}";
        }

        var flightPromptMatch = SafeRegexMatch(line, @"^(?<prefix>.*?)(?:\s*)Please enjoy your flight\.$", RegexOptions.IgnoreCase);
        if (flightPromptMatch.Success)
        {
            var prefix = flightPromptMatch.Groups["prefix"].Value.TrimEnd();
            return string.IsNullOrEmpty(prefix)
                ? "\u8bf7\u4eab\u53d7\u4f60\u7684\u822a\u884c\u3002"
                : $"{prefix} \u8bf7\u4eab\u53d7\u4f60\u7684\u822a\u884c\u3002";
        }

        if (string.Equals(line.Trim(), "Do you want to eject all crew members, including yourself? You must be in orbit around a moon.", StringComparison.Ordinal))
        {
            return "\u4f60\u786e\u5b9a\u8981\u5f39\u5c04\u5168\u90e8\u8239\u5458\uff08\u5305\u62ec\u4f60\u81ea\u5df1\uff09\u5417\uff1f\u4f60\u5fc5\u987b\u4f4d\u4e8e\u67d0\u9897\u536b\u661f\u8f68\u9053\u4e0a\u3002";
        }

        if (string.Equals(line.Trim(), "Please CONFIRM or DENY.", StringComparison.Ordinal))
        {
            return "\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002";
        }

        if (string.Equals(line.Trim(), "Cancelled ejection sequence.", StringComparison.Ordinal))
        {
            return "\u5df2\u53d6\u6d88\u5f39\u5c04\u5e8f\u5217\u3002";
        }

        return line;
    }

    private static string LocalizeTerminalRouteWeather(string weather)
    {
        return LocalizeWeatherName(weather, appendWeatherSuffix: false, colorize: true);
    }

    private static string LocalizeWeatherName(string weather, bool appendWeatherSuffix, bool colorize)
    {
        if (string.IsNullOrWhiteSpace(weather))
        {
            return weather;
        }

        var weatherText = StripRichTextTags(weather).Trim();
        if (weatherText.EndsWith("\u5929\u6c14", StringComparison.Ordinal))
        {
            weatherText = weatherText[..^"\u5929\u6c14".Length].TrimEnd();
        }

        var normalized = weatherText.ToLowerInvariant();
        normalized = normalized.EndsWith(" weather", StringComparison.Ordinal)
            ? normalized[..^" weather".Length].TrimEnd()
            : normalized;

        var (plain, color) = normalized switch
        {
            "mild" => ("\u6e29\u548c", "#A8E6A1"),
            "\u6e29\u548c" => ("\u6e29\u548c", "#A8E6A1"),
            "stormy" => ("\u66b4\u98ce", "#D6A23C"),
            "\u66b4\u98ce" => ("\u66b4\u98ce", "#D6A23C"),
            "foggy" => ("\u6d53\u96fe", "#9FB7C4"),
            "\u6d53\u96fe" => ("\u6d53\u96fe", "#9FB7C4"),
            "eclipsed" => ("\u65e5\u98df", "#C74B50"),
            "\u65e5\u98df" => ("\u65e5\u98df", "#C74B50"),
            "flooded" => ("\u6d2a\u6c34", "#4DA3FF"),
            "\u6d2a\u6c34" => ("\u6d2a\u6c34", "#4DA3FF"),
            "rainy" => ("\u964d\u96e8", "#6BC5FF"),
            "\u964d\u96e8" => ("\u964d\u96e8", "#6BC5FF"),
            _ => (weather, string.Empty)
        };

        if (color.Length == 0 &&
            (ReferenceEquals(plain, weather) || string.Equals(plain, weather, StringComparison.Ordinal)))
        {
            return weather;
        }

        var localized = appendWeatherSuffix ? plain + "\u5929\u6c14" : plain;
        return colorize && color.Length > 0
            ? $"<color={color}>{localized}</color>"
            : localized;
    }

    private static string StandardizeTerminalStoreTransactions(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var updated = source;

        updated = SafeRegexReplace(
            updated,
            @"^(?<count>\d+)\s+purchased items on route:\s*\n\s*-\s*(?<item>.+?)\s*$",
            m =>
            {
                var count = m.Groups["count"].Value.Trim();
                var item = BuildChineseFirstBilingual(ToSingularTerminalItem(m.Groups["item"].Value.Trim()));
                return $"\u6709 {count} \u4ef6\u5df2\u8d2d\u5546\u54c1\u6b63\u5728\u8fd0\u9001\u9014\u4e2d\uff1a\n- {item}";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        updated = SafeRegexReplace(
            updated,
            @"^(?<count>\d+)\s+purchased items on route\.$",
            "\u6709 ${count} \u4ef6\u5df2\u8d2d\u7269\u54c1\u6b63\u5728\u8fd0\u9001\u9014\u4e2d\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^You could not afford these items!\s*\nYour balance is (?<credits>.+?)\. Total cost of these items is (?<cost>.+?)\.\s*$",
            "\u4f60\u7684\u8d44\u91d1\u4e0d\u8db3\uff0c\u65e0\u6cd5\u8d2d\u4e70\u8fd9\u4e9b\u7269\u54c1\uff01\n\u4f59\u989d\u4e3a ${credits}\uff0c\u8fd9\u4e9b\u7269\u54c1\u7684\u603b\u4ef7\u4e3a ${cost}\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^You have requested to order (?<item>.+?)\. Amount: (?<amount>.+?)\.\s*Total cost of items: (?<cost>.+?)\.\s*Please CONFIRM or DENY\.\s*$",
            m =>
            {
                var item = BuildChineseFirstBilingual(ToSingularTerminalItem(m.Groups["item"].Value.Trim()));
                var amount = m.Groups["amount"].Value.Trim();
                var cost = NormalizeTerminalTransactionCost(m.Groups["cost"].Value);
                return $"\n\n\u4f60\u8bf7\u6c42\u8ba2\u8d2d {item}\u3002\u6570\u91cf\uff1a{amount}\u3002\n\u7269\u54c1\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^You have requested to order (?<item>.+?)\.\s*(?<detail>[\s\S]*?)Total cost of items?: (?<cost>.+?)\.\s*Please CONFIRM or DENY\.\s*$",
            m =>
            {
                var item = BuildTerminalLocalizedItemName(m.Groups["item"].Value.Trim());
                var detail = TranslateTerminalOrderDetail(m.Groups["detail"].Value);
                var cost = NormalizeTerminalTransactionCost(m.Groups["cost"].Value);
                var detailBlock = detail.Length == 0 ? string.Empty : $"\n{detail}\n";
                return $"\n\n\u4f60\u8bf7\u6c42\u8ba2\u8d2d {item}\u3002{detailBlock}\n\u7269\u54c1\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        updated = SafeRegexReplace(
            updated,
            @"^You have requested to order (?<item>.+?), which (?<detail>.+?)\.\s*Total cost of item: (?<cost>.+?)\.\s*Please CONFIRM or DENY\.\s*$",
            m =>
            {
                var item = BuildTerminalLocalizedItemName(m.Groups["item"].Value.Trim());
                var detail = TranslateTerminalOrderDetail(m.Groups["detail"].Value.Trim());
                var cost = NormalizeTerminalTransactionCost(m.Groups["cost"].Value);
                return $"\n\n\u4f60\u8bf7\u6c42\u8ba2\u8d2d {item}\uff0c{detail}\u3002\n\u5355\u4ef6\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^\s*Ordered the (?<item>Company Cruiser|Cruiser)[.!]\s*(?:Your new balance is|(?:\u4f60|\u60a8)\u7684\u65b0\u4f59\u989d\u4e3a)\s*(?<credits>[$\u25a0]?\s*[+-]?\d+(?:\.\d+)?)\s*[\.\u3002]?(?<rest>[\s\S]*?)\s*$",
            m =>
            {
                var item = BuildTerminalLocalizedItemName(m.Groups["item"].Value.Trim());
                var credits = SafeRegexReplace(m.Groups["credits"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
                var rest = m.Groups["rest"].Value.Trim();
                if (rest.Length == 0)
                {
                    return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002";
                }

                var normalizedRest = TranslateTerminalOrderDetail(rest);
                return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n{normalizedRest}";
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        updated = SafeRegexReplace(
            updated,
            @"^\s*Ordered (?<count>\d+) (?<item>.+?)\.\s*(?:Your new balance is|(?:\u4f60|\u60a8)\u7684\u65b0\u4f59\u989d\u4e3a)\s*(?<credits>[$\u25a0]?\s*[+-]?\d+(?:\.\d+)?)\s*[\.\u3002]?(?<rest>[\s\S]*?)\s*$",
            m =>
            {
                var count = m.Groups["count"].Value;
                var item = BuildChineseFirstBilingual(ToSingularTerminalItem(m.Groups["item"].Value.Trim()));
                var credits = SafeRegexReplace(m.Groups["credits"].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant);
                var rest = m.Groups["rest"].Value.Trim();
                if (rest.Length == 0)
                {
                    return $"\u5df2\u8ba2\u8d2d {count} \u4ef6 {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002";
                }

                var normalizedRest = TranslateTerminalOrderDetail(rest);
                return $"\u5df2\u8ba2\u8d2d {count} \u4ef6 {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n{normalizedRest}";
            },
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        updated = SafeRegexReplace(
            updated,
            @"^Ordered (?<count>\d+) (?<item>.+?)\. Your new balance is (?<credits>.+?)\.\s*\n?\s*Our contractors enjoy fast, free shipping while on the job! Any purchased items will arrive hourly at your approximate location\.\s*$",
            m =>
            {
                var count = m.Groups["count"].Value;
                var item = BuildChineseFirstBilingual(ToSingularTerminalItem(m.Groups["item"].Value.Trim()));
                var credits = m.Groups["credits"].Value;
                return $"\u5df2\u8ba2\u8d2d {count} \u4ef6 {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n\u6211\u4eec\u7684\u627f\u5305\u5546\u5728\u5de5\u4f5c\u671f\u95f4\u4eab\u6709\u5feb\u901f\u514d\u8d39\u914d\u9001\u670d\u52a1\uff0c\n\u5df2\u8d2d\u7269\u54c1\u4f1a\u6309\u5c0f\u65f6\u9001\u8fbe\u4f60\u5927\u81f4\u6240\u5728\u4f4d\u7f6e\u3002";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^Ordered (?<count>.+?) (?<item>.+?)\. Your new balance is (?<credits>.+?)\.\s*Our contractors enjoy fast, free shipping while on the job! Any purchased items will arrive hourly at your approximate location\.\s*$",
            m =>
            {
                var count = m.Groups["count"].Value.Trim();
                var item = BuildChineseFirstBilingual(ToSingularTerminalItem(m.Groups["item"].Value.Trim()));
                var credits = m.Groups["credits"].Value.Trim();
                return $"\u5df2\u8ba2\u8d2d {count} \u4ef6 {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n\u6211\u4eec\u7684\u627f\u5305\u5546\u5728\u5de5\u4f5c\u671f\u95f4\u4eab\u6709\u5feb\u901f\u514d\u8d39\u914d\u9001\u670d\u52a1\uff0c\n\u5df2\u8d2d\u7269\u54c1\u4f1a\u6309\u5c0f\u65f6\u9001\u8fbe\u4f60\u5927\u81f4\u6240\u5728\u4f4d\u7f6e\u3002";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^Ordered the (?<item>Company Cruiser|Cruiser)[.!] Your new balance is (?<credits>.+?)\.(?<rest>[\s\S]*)$",
            m =>
            {
                var item = BuildTerminalLocalizedItemName(m.Groups["item"].Value.Trim());
                var credits = m.Groups["credits"].Value.Trim();
                var rest = m.Groups["rest"].Value.Trim();
                if (rest.Length == 0)
                {
                    return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002";
                }

                var normalizedRest = TranslateTerminalOrderDetail(rest);

                return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n{normalizedRest}";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^Ordered the (?<item>.+?)[.!] Your new balance is (?<credits>.+?)\.(?<rest>[\s\S]*)$",
            m =>
            {
                var item = BuildChineseFirstBilingual(NormalizeTerminalArticleItem(m.Groups["item"].Value.Trim()));
                var credits = m.Groups["credits"].Value;
                var rest = m.Groups["rest"].Value.Trim();
                if (rest.Length == 0)
                {
                    return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002";
                }

                var normalizedRest = TranslateTerminalOrderDetail(rest);
                return $"\u5df2\u8ba2\u8d2d {item}\u3002\n\u4f60\u7684\u65b0\u4f59\u989d\u4e3a {credits}\u3002\n\n{normalizedRest}";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^You have requested to order (?<item>.+?)\.\s*\nTotal cost of item: (?<cost>.+?)\.\s*\n\s*\nPlease CONFIRM or DENY\.\s*$",
            m =>
            {
                var item = BuildChineseFirstBilingual(NormalizeTerminalArticleItem(m.Groups["item"].Value.Trim()));
                var cost = NormalizeTerminalTransactionCost(m.Groups["cost"].Value);
                return $"\n\n\u4f60\u8bf7\u6c42\u8ba2\u8d2d {item}\u3002\n\u5355\u4ef6\u603b\u4ef7\uff1a{cost}\u3002\n\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002\n\n";
            },
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        return updated;
    }

    private static string StandardizeTerminalGeneralStatus(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var updated = source;
        updated = updated.Replace(
            "Unable to route the ship currently. It must be in orbit around a moon to route the autopilot.\nUse the main lever at the front desk to enter orbit.",
            "\u5f53\u524d\u65e0\u6cd5\u8bbe\u7f6e\u98de\u8239\u822a\u7ebf\u3002\u98de\u8239\u5fc5\u987b\u5904\u4e8e\u67d0\u9897\u536b\u661f\u7684\u8f68\u9053\u4e0a\u624d\u80fd\u8bbe\u7f6e\u81ea\u52a8\u9a7e\u9a76\u3002\n\u8bf7\u4f7f\u7528\u524d\u53f0\u4e3b\u63a7\u6746\u8fdb\u5165\u8f68\u9053\u3002");

        updated = SafeRegexReplace(
            updated,
            @"^The Company is buying at (?<percent>.+?)\.\s*Do you want to route the autopilot to the Company building\?\s*Please CONFIRM or DENY\.$",
            "\u516c\u53f8\u5f53\u524d\u6536\u8d2d\u6bd4\u4f8b\u4e3a ${percent}\u3002\u662f\u5426\u5c06\u81ea\u52a8\u9a7e\u9a76\u822a\u7ebf\u8bbe\u4e3a\u516c\u53f8\u5927\u697c\uff1f\n\u8bf7\u8f93\u5165 CONFIRM \u6216 DENY\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^The Company is buying(?: your goods)? at (?<percent>.+?)\.$",
            "\u516c\u53f8\u5f53\u524d\u6536\u8d2d\u6bd4\u4f8b\u4e3a ${percent}\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"Do you want to route the autopilot to the Company\s+building\?",
            "\u662f\u5426\u5c06\u81ea\u52a8\u9a7e\u9a76\u5bfc\u822a\u81f3\u516c\u53f8\u5927\u697c\uff1f",
            RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^Your balance is (?<credits>.+?)\. Total cost of these items is (?<cost>.+?)\.$",
            "\u4f60\u7684\u4f59\u989d\u4e3a ${credits}\u3002\u8fd9\u4e9b\u7269\u54c1\u7684\u603b\u4ef7\u4e3a ${cost}\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^There are (?<count>\d+) objects in the ship, totalling at (?<value>.+?)\.$",
            "\u98de\u8239\u5185\u5171\u6709 ${count} \u4e2a\u7269\u4f53\uff0c\u603b\u4ef7\u503c\u4e3a ${value}\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^There are (?<count>\d+) objects outside the ship, totalling at an approximate value of (?<value>.+?)\.$",
            "\u98de\u8239\u5916\u5171\u6709 ${count} \u4e2a\u7269\u4f53\uff0c\u9884\u4f30\u603b\u4ef7\u503c\u4e3a ${value}\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = SafeRegexReplace(
            updated,
            @"^Cancelled ejection sequence\.$",
            "\u5df2\u53d6\u6d88\u5f39\u5c04\u5e8f\u5217\u3002",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        updated = ReplaceStandaloneTerminalStatus(
            updated,
            @"(?:Cancelled order\.|\u5df2\u53d6\u6d88\u8ba2\u5355\u3002|\u8ba2\u5355\u5df2\u53d6\u6d88\u3002)",
            "\u5df2\u53d6\u6d88\u8ba2\u5355\u3002");

        updated = ReplaceStandaloneTerminalStatus(
            updated,
            @"Pinged radar booster\.",
            "\u5df2 ping \u96f7\u8fbe\u589e\u5e45\u5668\u3002");

        updated = ReplaceStandaloneTerminalStatus(
            updated,
            @"Flashed radar booster\.",
            "\u5df2\u89e6\u53d1\u96f7\u8fbe\u589e\u5e45\u5668\u95ea\u5149\u3002");

        return updated;
    }

    private static string ReplaceStandaloneTerminalStatus(string source, string bodyPattern, string translatedBody)
    {
        return SafeRegexReplace(
            source,
            $@"^(?<lead>\s*){bodyPattern}(?<trail>\s*)$",
            match => match.Groups["lead"].Value + translatedBody + match.Groups["trail"].Value,
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string ToSingularTerminalItem(string item)
    {
        var normalized = item.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "shovels" => "Shovel",
            "extension ladders" => "Extension ladder",
            "flashlights" => "Flashlight",
            "jetpack" => "Jetpack",
            "lock-pickers" => "Lock-picker",
            "mapper tools" => "Mapper tool",
            "pro flashlights" => "Pro flashlight",
            "radar boosters" => "Radar-booster",
            "spray paint cans" => "Spray paint",
            "stun grenades" => "Stun grenade",
            "survival kit" => "Survival kit",
            "tactical belt bags" => "Belt bag",
            "tzp-inhalants" => "TZP-Inhalant",
            "walkie-talkies" => "Walkie-talkie",
            "weed killer spray bottles" => "Weed killer",
            "zap guns" => "Zap gun",
            _ => normalized
        };
    }

    private static string NormalizeTerminalArticleItem(string item)
    {
        if (item.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
        {
            return item[2..].Trim();
        }

        if (item.StartsWith("an ", StringComparison.OrdinalIgnoreCase))
        {
            return item[3..].Trim();
        }

        if (item.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
        {
            return item[4..].Trim();
        }

        return item.Trim();
    }

    private static string BuildChineseFirstBilingual(string english)
    {
        if (string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        var sourceName = english.Trim();
        if (TryExtractTerminalBilingualEnglish(sourceName, out var extractedEnglish))
        {
            sourceName = extractedEnglish;
        }
        else if (ContainsCjk(sourceName))
        {
            return SanitizeTranslatedText(sourceName);
        }

        if (!TryGetTerminalBilingualLocalized(sourceName, out var localized) &&
            !TryTranslateExact(sourceName, out localized) &&
            !TryTranslateRegex(sourceName, out localized))
        {
            localized = TranslateComposite(sourceName);
        }

        localized = SanitizeTranslatedText(localized);
        if (string.IsNullOrWhiteSpace(localized) || string.Equals(localized, sourceName, StringComparison.Ordinal))
        {
            return sourceName;
        }

        return $"{localized}\uff08{sourceName}\uff09";
    }

    private static bool TryExtractTerminalBilingualEnglish(string source, out string english)
    {
        english = string.Empty;
        var trimmed = source.Trim();
        if (TryCanonicalizeTerminalEnglishName(trimmed, out english))
        {
            return true;
        }

        foreach (var match in SafeRegexMatches(trimmed, @"[\(\uff08](?<candidate>[^()\uff08\uff09]+)[\)\uff09]"))
        {
            var candidate = match.Groups["candidate"].Value.Trim();
            if (TryCanonicalizeTerminalEnglishName(candidate, out english) ||
                IsLikelyTerminalEnglishName(candidate))
            {
                english = TryCanonicalizeTerminalEnglishName(candidate, out var canonicalEnglish)
                    ? canonicalEnglish
                    : candidate;
                return true;
            }
        }

        foreach (var key in TerminalBilingualOverrides.Keys.OrderByDescending(key => key.Length))
        {
            if (trimmed.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                english = key;
                return true;
            }
        }

        foreach (var entry in TerminalBilingualOverrides)
        {
            if (string.Equals(trimmed, entry.Value, StringComparison.Ordinal))
            {
                english = entry.Key;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTerminalBilingualLocalized(string english, out string localized)
    {
        if (TerminalBilingualOverrides.TryGetValue(english, out localized))
        {
            return true;
        }

        if (TryCanonicalizeTerminalEnglishName(english, out var canonicalEnglish) &&
            TerminalBilingualOverrides.TryGetValue(canonicalEnglish, out localized))
        {
            return true;
        }

        localized = string.Empty;
        return false;
    }

    private static bool TryCanonicalizeTerminalEnglishName(string candidate, out string english)
    {
        english = string.Empty;
        if (TerminalBilingualOverrides.ContainsKey(candidate))
        {
            english = candidate;
            return true;
        }

        var normalizedCandidate = NormalizeTerminalEnglishName(candidate);
        if (normalizedCandidate.Length == 0)
        {
            return false;
        }

        foreach (var key in TerminalBilingualOverrides.Keys.OrderByDescending(key => key.Length))
        {
            if (NormalizeTerminalEnglishName(key) != normalizedCandidate)
            {
                continue;
            }

            english = key;
            return true;
        }

        return false;
    }

    private static string NormalizeTerminalEnglishName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static bool IsLikelyTerminalEnglishName(string value)
    {
        var hasLetter = false;
        foreach (var ch in value)
        {
            if (ContainsCjk(ch.ToString()))
            {
                return false;
            }

            hasLetter |= ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        return hasLetter;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if ((ch >= '\u3400' && ch <= '\u9fff') ||
                (ch >= '\uf900' && ch <= '\ufaff'))
            {
                return true;
            }
        }

        return false;
    }

    private static void LoadJsonFallback(string pluginDir, List<string> loadedSources)
    {
        foreach (var path in ResolveJsonPaths(pluginDir))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonConvert.DeserializeObject<TranslationFile>(text);
                if (data?.entries == null)
                {
                    continue;
                }

                foreach (var entry in data.entries)
                {
                    AddJsonEntry(entry);
                }

                loadedSources.Add(path);
                return;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"TranslationService failed reading JSON '{path}': {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void LoadPluginCfgDirectories(string pluginDir, List<string> loadedSources)
    {
        foreach (var dir in ResolvePluginCfgDirectories(pluginDir))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var files = Directory.GetFiles(dir, "*.cfg")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    if (ShouldSkipCfgFile(file))
                    {
                        Plugin.Log.LogInfo($"TranslationService skipped non-text cfg '{file}'.");
                        continue;
                    }

                    LoadCfgFile(file);
                    loadedSources.Add(file);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"TranslationService failed reading cfg '{file}': {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    private static void LoadCfgDirectories(List<string> loadedSources)
    {
        foreach (var dir in ResolveCfgDirectories())
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var files = Directory.GetFiles(dir, "*.cfg")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    if (ShouldSkipCfgFile(file))
                    {
                        Plugin.Log.LogInfo($"TranslationService skipped non-text cfg '{file}'.");
                        continue;
                    }

                    LoadCfgFile(file);
                    loadedSources.Add(file);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"TranslationService failed reading cfg '{file}': {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    private static void LoadCfgFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("/*", StringComparison.Ordinal) ||
                line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseRegexEntry(line, out var regexPattern, out var regexReplacement))
            {
                AddRegexEntry(regexPattern, regexReplacement);
                continue;
            }

            var splitIndex = FindCfgEntrySeparator(line);
            if (splitIndex <= 0)
            {
                continue;
            }

            var source = line.Substring(0, splitIndex).Trim();
            var target = line.Substring(splitIndex + 1).Trim();
            if (source.Length == 0)
            {
                continue;
            }

            AddExactEntry(UnescapeCfgValue(source), UnescapeCfgValue(target));
        }
    }

    private static int FindCfgEntrySeparator(string line)
    {
        var escaped = false;
        var inRichTextTag = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '<')
            {
                inRichTextTag = true;
                continue;
            }

            if (inRichTextTag)
            {
                if (ch == '>')
                {
                    inRichTextTag = false;
                }

                continue;
            }

            if (ch == '=')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseRegexEntry(string line, out string pattern, out string replacement)
    {
        pattern = string.Empty;
        replacement = string.Empty;

        if (!(line.StartsWith("r:\"", StringComparison.Ordinal) || line.StartsWith("sr:\"", StringComparison.Ordinal)))
        {
            return false;
        }

        var prefixLength = line.StartsWith("sr:\"", StringComparison.Ordinal) ? 4 : 3;
        var equalsIndex = line.IndexOf("\"=", prefixLength, StringComparison.Ordinal);
        if (equalsIndex < 0)
        {
            return false;
        }

        pattern = line.Substring(prefixLength, equalsIndex - prefixLength);
        replacement = line.Substring(equalsIndex + 2);
        pattern = UnescapeCfgValue(pattern);
        replacement = UnescapeCfgValue(replacement);
        return pattern.Length > 0;
    }

    private static string UnescapeCfgValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        value = value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
        value = value.Replace("\\=", "=").Replace("\\\"", "\"");
        return value;
    }

    private static void AddJsonEntry(TranslationEntry entry)
    {
        if (string.IsNullOrEmpty(entry.source))
        {
            return;
        }

        var mode = entry.mode?.Trim().ToLowerInvariant();
        if (mode == "skip")
        {
            return;
        }

        var target = mode == "preserve" ? entry.source : entry.target;
        if (mode == "bilingual")
        {
            target = $"{entry.target}\n{entry.source}";
        }

        if (!string.IsNullOrEmpty(target))
        {
            AddExactEntry(entry.source, target);
        }
    }

    private static void AddExactEntry(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrEmpty(target) || IsCorruptedTranslation(target))
        {
            return;
        }

        if (!ExactMap.ContainsKey(source))
        {
            ExactMap[source] = target;
        }

        if (!ExactMapIgnoreCase.ContainsKey(source))
        {
            ExactMapIgnoreCase[source] = target;
        }
    }

    private static void AddRegexEntry(string pattern, string replacement)
    {
        if (string.IsNullOrWhiteSpace(pattern) || IsCorruptedTranslation(replacement))
        {
            return;
        }

        try
        {
            if (!RegexPatternSet.Add(pattern))
            {
                return;
            }

            if (TryGetKnownSlowCfgRegexKind(pattern, out var knownSlowKind))
            {
                RegexEntries.Add(new RegexEntry(pattern, knownSlowKind, replacement));
                return;
            }

            RegexEntries.Add(new RegexEntry(
                pattern,
                new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout),
                replacement));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"TranslationService skipped invalid regex '{pattern}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryApplyRegexEntry(string source, RegexEntry entry, out string translated)
    {
        if (entry.Disabled)
        {
            translated = source;
            return false;
        }

        if (entry.Regex != null)
        {
            try
            {
                if (!entry.Regex.IsMatch(source))
                {
                    translated = source;
                    return false;
                }

                translated = entry.Regex.Replace(source, entry.Replacement, 1);
                return true;
            }
            catch (RegexMatchTimeoutException ex)
            {
                entry.DisableAfterTimeout();
                LogRegexTimeout(entry.Pattern, ex);
                translated = source;
                return false;
            }
        }

        if (!entry.Kind.HasValue)
        {
            translated = source;
            return false;
        }

        return TryApplyKnownSlowRegexEntry(source, entry.Kind.Value, entry.Replacement, out translated);
    }

    private static bool TryGetKnownSlowCfgRegexKind(string pattern, out KnownSlowCfgRegexKind kind)
    {
        switch (pattern)
        {
            case "(.+?) purchased items on route\\.":
                kind = KnownSlowCfgRegexKind.PurchasedItemsOnRoute;
                return true;
            case "(.+?) purchased vehicle on route\\.":
                kind = KnownSlowCfgRegexKind.PurchasedVehicleOnRoute;
                return true;
            case "(.+)\\: ALL":
                kind = KnownSlowCfgRegexKind.ColonAll;
                return true;
            case "(.+)\\: NONE":
                kind = KnownSlowCfgRegexKind.ColonNone;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryApplyKnownSlowRegexEntry(string source, KnownSlowCfgRegexKind kind, string replacement, out string translated)
    {
        if (string.IsNullOrEmpty(source))
        {
            translated = source;
            return false;
        }

        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (!TryApplyKnownSlowRegexToLine(content, kind, replacement, out var updatedContent))
            {
                continue;
            }

            lines[i] = hasCarriageReturn ? updatedContent + "\r" : updatedContent;
            translated = string.Join("\n", lines);
            return true;
        }

        translated = source;
        return false;
    }

    private static bool TryApplyKnownSlowRegexToLine(string line, KnownSlowCfgRegexKind kind, string replacement, out string translated)
    {
        translated = line;
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var suffix = kind switch
        {
            KnownSlowCfgRegexKind.PurchasedItemsOnRoute => " purchased items on route.",
            KnownSlowCfgRegexKind.PurchasedVehicleOnRoute => " purchased vehicle on route.",
            KnownSlowCfgRegexKind.ColonAll => ": ALL",
            KnownSlowCfgRegexKind.ColonNone => ": NONE",
            _ => string.Empty
        };

        if (suffix.Length == 0)
        {
            return false;
        }

        var matchIndex = kind switch
        {
            KnownSlowCfgRegexKind.ColonAll or KnownSlowCfgRegexKind.ColonNone => line.LastIndexOf(suffix, StringComparison.Ordinal),
            _ => line.IndexOf(suffix, StringComparison.Ordinal)
        };

        if (matchIndex <= 0)
        {
            return false;
        }

        var capture = line[..matchIndex];
        var matchEnd = matchIndex + suffix.Length;
        var tail = matchEnd < line.Length ? line[matchEnd..] : string.Empty;
        translated = replacement.Replace("$1", capture, StringComparison.Ordinal) + tail;
        return true;
    }

    private static IEnumerable<string> ResolveJsonPaths(string pluginDir)
    {
        yield return Path.Combine(pluginDir, "V81TestChn", "translations", "zh-CN.json");
        yield return Path.Combine(pluginDir, "translations", "zh-CN.json");
    }

    private static IEnumerable<string> ResolvePluginCfgDirectories(string pluginDir)
    {
        yield return Path.Combine(pluginDir, "V81TestChn", "translations-cfg", "zh-CN");
        yield return Path.Combine(pluginDir, "V81TestChn", "translations-cfg", "zh-Hans");
        yield return Path.Combine(pluginDir, "translations-cfg", "zh-CN");
        yield return Path.Combine(pluginDir, "translations-cfg", "zh-Hans");
    }

    private static IEnumerable<string> ResolveCfgDirectories()
    {
        yield return Path.Combine(Paths.ConfigPath, "translations", "zh-CN");
        yield return Path.Combine(Paths.ConfigPath, "translations", "zh-Hans");
    }

    private static bool ShouldSkipCfgFile(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "CMD-PY-Translate.cfg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "CMD-ZH-Translate.cfg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(fileName, "Terminal-Translate.cfg", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(path));
            if (string.Equals(parent, "zh-CN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
            {
                return false;
            }

            var sampleLength = Math.Min(bytes.Length, 512);
            for (var i = 0; i < sampleLength; i++)
            {
                if (bytes[i] == 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsCorruptedTranslation(string value)
    {
        return value.IndexOf('\uFFFD') >= 0;
    }

    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
        {
            return source;
        }

        var index = source.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return source;
        }

        var result = source;
        while (index >= 0)
        {
            result = result.Remove(index, oldValue.Length).Insert(index, newValue);
            index = result.IndexOf(oldValue, index + newValue.Length, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string NormalizeLoose(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        var normalized = source
            .Replace("\u2026", "...")
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"');

        var builder = new StringBuilder(normalized.Length);
        var sawWhitespace = false;
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!sawWhitespace)
                {
                    builder.Append(' ');
                    sawWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            sawWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static string SanitizeTranslatedText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        StringBuilder? builder = null;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            var keep = ch == '\n' || ch == '\r' || ch == '\t' || !char.IsControl(ch);
            if (keep)
            {
                if (builder != null)
                {
                    builder.Append(ch);
                }

                continue;
            }

            builder ??= new StringBuilder(source.Length);
            if (builder.Length == 0 && i > 0)
            {
                builder.Append(source, 0, i);
            }
        }

        return builder?.ToString() ?? source;
    }

    private static Match SafeRegexMatch(string source, string pattern, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.Match(source, pattern, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(pattern, ex);
            return Match.Empty;
        }
    }

    private static IEnumerable<Match> SafeRegexMatches(string source, string pattern, RegexOptions options = RegexOptions.None)
    {
        MatchCollection matches;
        try
        {
            matches = Regex.Matches(source, pattern, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(pattern, ex);
            yield break;
        }

        foreach (Match match in matches)
        {
            yield return match;
        }
    }

    private static string SafeRegexReplace(string source, string pattern, string replacement, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.Replace(source, pattern, replacement, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(pattern, ex);
            return source;
        }
    }

    private static string SafeRegexReplace(string source, string pattern, MatchEvaluator evaluator, RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.Replace(source, pattern, evaluator, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(pattern, ex);
            return source;
        }
    }

    private static string SafeRegexReplace(Regex regex, string source, string replacement)
    {
        try
        {
            return regex.Replace(source, replacement);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(regex.ToString(), ex);
            return source;
        }
    }

    private static bool TrySafeRegexMatch(Regex regex, string source, out Match match)
    {
        try
        {
            match = regex.Match(source);
            return true;
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogRegexTimeout(regex.ToString(), ex);
            match = Match.Empty;
            return false;
        }
    }

    private static void LogRegexTimeout(string pattern, RegexMatchTimeoutException ex)
    {
        if (!WarnedRegexTimeoutPatterns.Add(pattern))
        {
            return;
        }

        Plugin.Log?.LogWarning($"TranslationService regex timeout after {ex.MatchTimeout.TotalMilliseconds:0.#}ms pattern='{TrimLogText(pattern, 120)}'");
    }

    private static string TrimLogText(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength) + "...";
    }

    private static void LogKnownDynamicHit(string kind, string source, string translated)
    {
        if (_suppressKnownDynamicHitLog ||
            _logKnownDynamicHits?.Value != true ||
            _knownDynamicHitLogCount >= KnownDynamicHitLogBudget)
        {
            return;
        }

        _knownDynamicHitLogCount++;
        try
        {
            Plugin.Log.LogInfo(
                $"TryTranslateKnownDynamicText hit kind={kind} source='{TrimKnownDynamicLogText(source)}' translated='{TrimKnownDynamicLogText(translated)}'");
        }
        catch
        {
            // Diagnostics must never affect translation.
        }
    }

    private static string TrimKnownDynamicLogText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return normalized.Length <= 140 ? normalized : normalized[..140] + "...";
    }

    private static bool TryGetCachedTranslation(string source, out string translated, out bool hasTranslation)
    {
        translated = source;
        hasTranslation = false;
        if (!TranslationResultCache.TryGetValue(source, out var cached))
        {
            return false;
        }

        if (cached == null)
        {
            hasTranslation = false;
            return true;
        }

        hasTranslation = true;
        translated = cached;
        return true;
    }

    private static void CacheTranslationResult(string source, string? translated)
    {
        if (source.Length > 256 || TranslationResultCache.ContainsKey(source))
        {
            return;
        }

        if (TranslationResultCache.Count >= MaxTranslationResultCache)
        {
            TranslationResultCache.Clear();
        }

        TranslationResultCache[source] = translated;
    }

    private static bool TryGetCachedCompositeTranslation(string source, out string translated)
    {
        if (CompositeTranslationResultCache.TryGetValue(source, out var cached))
        {
            translated = cached;
            return true;
        }

        translated = source;
        return false;
    }

    private static void CacheCompositeTranslationResult(string source, string translated)
    {
        if (source.Length > 512 || CompositeTranslationResultCache.ContainsKey(source))
        {
            return;
        }

        if (CompositeTranslationResultCache.Count >= MaxTranslationResultCache)
        {
            CompositeTranslationResultCache.Clear();
        }

        CompositeTranslationResultCache[source] = translated;
    }
}

