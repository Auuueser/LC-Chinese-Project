using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

namespace V81TestChn;

internal static class TerminalCommandLocalizationService
{
    private static ConfigEntry<bool>? _chinese;
    private static ConfigEntry<bool>? _pinyin;
    private static ConfigEntry<bool>? _guide;
    private static ConfigEntry<bool>? _initials;
    private static readonly System.Collections.Generic.Dictionary<string, TerminalNode> GuideNodes = new(StringComparer.OrdinalIgnoreCase);
    private static System.Collections.Generic.Dictionary<string, string> _guidePages = TerminalHelpContent.Build(true, true, true);
    private sealed class Rules { internal TerminalInputRules Input = null!; internal readonly TerminalRegistrationState State = new(); }
    private static ConditionalWeakTable<Terminal, Rules> _rules = new();
    [ThreadStatic] private static string? _original;
    [ThreadStatic] private static string? _resolved;
    [ThreadStatic] private static Terminal? _parsingTerminal;
    private static TerminalNode? _feedback;

    internal static void Initialize(ConfigFile config)
    {
        const string section = "06 终端 - 中文输入";
        _chinese = config.Bind(section, "EnableChineseCommands", true, "允许输入中文命令与物品名称，例如：商店、购买 手电筒 2。仍使用原版确认流程。");
        _pinyin = config.Bind(section, "EnablePinyinCommands", true, "允许输入已登记中文命令和名称的完整拼音，例如 shangdian、goumai shoudiantong 2。同音词显示候选，不自动选择。");
        RemoveRetiredInputSetting(config, section);
        _initials = config.Bind(section, "EnablePinyinInitials", true, "允许拼音首字母，例如 gm sd 2（购买手电）、qh（切换监控目标）。同名缩写显示候选，不猜测执行。");
        LiveConfigRegistration.RegisterBool(_initials);
        _guide = config.Bind(section, "ShowChineseCommandGuide", true, "在终端帮助页显示中文教程命令入口，输入 中文教程 或 zhhelp 查看独立教程页。关闭后仍可使用已启用的输入方式。");
        LiveConfigRegistration.RegisterBool(_chinese);
        LiveConfigRegistration.RegisterBool(_pinyin);
        LiveConfigRegistration.RegisterBool(_guide);
        _chinese.SettingChanged += RefreshGuidePages;
        _pinyin.SettingChanged += RefreshGuidePages;
        _initials.SettingChanged += RefreshGuidePages;
        RefreshGuidePages(null, EventArgs.Empty);
    }

    internal static void Register(Terminal terminal)
    {
        if (terminal?.terminalNodes?.allKeywords == null) return;
        if (!_rules.TryGetValue(terminal, out var cached))
        {
            cached = new Rules();
            _rules.Add(terminal, cached);
        }
        if (!CaptureRegistrationState(terminal, cached.State) && cached.Input != null) return;
        cached.Input = null!;
        PrepareGuideNodes();
        var keywords = terminal.terminalNodes.allKeywords;
        var input = new TerminalInputRules(keywords.Where(k => k != null).Select(k => k.word));
        foreach (var keyword in keywords)
            if (keyword != null && (keyword.accessTerminalObjects || keyword.word == "route")) input.PreserveNativeKeyword(keyword.word);
        foreach (var verb in keywords)
        {
            if (verb?.compatibleNouns == null || verb.word is not ("buy" or "info" or "view" or "return" or "route")) continue;
            foreach (var noun in verb.compatibleNouns)
            {
                if (noun?.noun == null || noun.noun.accessTerminalObjects || noun.result == null) continue;
                if (verb.word == "route" || noun.result.buyRerouteToMoon >= 0)
                {
                    input.PreserveNativeKeyword(noun.noun.word);
                    continue;
                }
                if (verb.word is not ("buy" or "info" or "view" or "return")) continue;
                if (verb.word.Equals("buy", StringComparison.OrdinalIgnoreCase)) input.RegisterPurchasable(noun.noun.word);
                RegisterNodeAliases(input, terminal, noun.noun.word, noun.result, 0);
            }
        }
        input.CompleteRegistration();
        cached.Input = input;
    }

    private static bool CaptureRegistrationState(Terminal terminal, TerminalRegistrationState state)
    {
        state.Begin();
        var keywords = terminal.terminalNodes.allKeywords;
        state.Observe(keywords, a: keywords.Length);
        foreach (var keyword in keywords)
        {
            state.Observe(keyword, keyword?.word, keyword != null && keyword.accessTerminalObjects ? 1 : 0);
            if (keyword == null || keyword.compatibleNouns == null || keyword.word is not ("buy" or "info" or "view" or "return" or "route")) continue;
            state.Observe(keyword.compatibleNouns, a: keyword.compatibleNouns.Length);
            foreach (var noun in keyword.compatibleNouns)
            {
                state.Observe(noun?.noun, noun?.noun?.word, noun?.noun != null && noun.noun.accessTerminalObjects ? 1 : 0);
                ObserveNode(state, noun?.result, 0);
            }
        }
        state.Observe(terminal.buyableItemsList);
        if (terminal.buyableItemsList != null)
            foreach (var item in terminal.buyableItemsList) state.Observe(item, item?.itemName);
        state.Observe(terminal.enemyFiles);
        if (terminal.enemyFiles != null)
            foreach (var node in terminal.enemyFiles) state.Observe(node, node?.creatureName, node?.creatureFileID ?? -1);
        state.Observe(terminal.logEntryFiles);
        if (terminal.logEntryFiles != null)
            foreach (var node in terminal.logEntryFiles) state.Observe(node, node?.creatureName, node?.storyLogFileID ?? -1);
        var unlockables = StartOfRound.Instance?.unlockablesList?.unlockables;
        state.Observe(unlockables);
        if (unlockables != null)
            foreach (var item in unlockables) state.Observe(item, item?.unlockableName);
        return state.End();
    }

    private static void ObserveNode(TerminalRegistrationState state, TerminalNode? node, int depth)
    {
        state.Observe(node, node?.creatureName, a: node?.buyItemIndex ?? -1, b: node?.creatureFileID ?? -1,
            c: node?.storyLogFileID ?? -1, d: node?.shipUnlockableID ?? -1);
        if (node == null) return;
        state.Observe(node.terminalOptions, a: node.buyRerouteToMoon);
        if (depth >= 2 || node.terminalOptions == null) return;
        foreach (var option in node.terminalOptions)
        {
            state.Observe(option?.noun, option?.noun?.word);
            if (option?.noun != null && option.noun.word == "confirm") ObserveNode(state, option.result, depth + 1);
        }
    }

    private static void RegisterNodeAliases(TerminalInputRules rules, Terminal terminal, string word, TerminalNode node, int depth)
    {
        if (node.buyRerouteToMoon >= 0) { rules.PreserveNativeKeyword(word); return; }
        string? name = null;
        if (node.buyItemIndex >= 0 && terminal.buyableItemsList != null && node.buyItemIndex < terminal.buyableItemsList.Length)
            name = OriginalResourceStateService.GetOriginalItemName(terminal.buyableItemsList[node.buyItemIndex]);
        else if (node.creatureFileID >= 0 && terminal.enemyFiles != null)
            name = OriginalResourceStateService.GetOriginalTerminalNodeCreatureName(terminal.enemyFiles.FirstOrDefault(n => n != null && n.creatureFileID == node.creatureFileID));
        else if (node.storyLogFileID >= 0 && terminal.logEntryFiles != null)
            name = OriginalResourceStateService.GetOriginalTerminalNodeCreatureName(terminal.logEntryFiles.FirstOrDefault(n => n != null && n.storyLogFileID == node.storyLogFileID));
        else if (node.shipUnlockableID >= 0 && StartOfRound.Instance?.unlockablesList?.unlockables != null &&
                 node.shipUnlockableID < StartOfRound.Instance.unlockablesList.unlockables.Count)
            name = StartOfRound.Instance.unlockablesList.unlockables[node.shipUnlockableID].unlockableName;
        if (string.IsNullOrWhiteSpace(name))
            name = OriginalResourceStateService.GetOriginalTerminalNodeCreatureName(node);
        RegisterKnownName(rules, word, name);
        if (depth >= 2 || node.terminalOptions == null) return;
        foreach (var option in node.terminalOptions)
            if (option?.result != null && option.noun != null && option.noun.word == "confirm")
                RegisterNodeAliases(rules, terminal, word, option.result, depth + 1);
    }

    internal static bool RegisterKnownName(TerminalInputRules rules, string word, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var compact = TerminalInputRules.Compact(name!);
        foreach (var definition in TerminalInputRules.Definitions)
            if (TerminalInputRules.Compact(definition.English) == compact ||
                (definition.English == "inhalant" && compact == "tzpinhalant"))
            {
                rules.AddAliases(word, definition.Chinese, definition.Pinyin);
                return true;
            }
        return false;
    }

    internal static bool Prepare(Terminal terminal, ref TerminalNode result)
    {
        ClearParse();
        if (terminal?.screenText == null || terminal.textAdded <= 0) return true;
        var text = terminal.screenText.text;
        if (terminal.textAdded > text.Length) return true;
        var source = text.Substring(text.Length - terminal.textAdded);
        if (!(terminal.currentNode != null && terminal.currentNode.overrideOptions) && (_guide?.Value ?? true) && IsGuideCommand(source))
        {
            // A registered external command has precedence over our optional help shortcut.
            var command = source.Trim().Split(' ')[0];
            if (terminal.terminalNodes?.allKeywords != null && terminal.terminalNodes.allKeywords.Any(k =>
                k != null && string.Equals(k.word, command, StringComparison.OrdinalIgnoreCase))) return true;
            var page = GetGuidePageKey(source);
            if (!GuideNodes.TryGetValue(page, out var guideNode)) { PrepareGuideNodes(); guideNode = GuideNodes[page]; }
            guideNode.displayText = _guidePages[page];
            result = guideNode;
            return false;
        }
        if (!_rules.TryGetValue(terminal, out var rules) || rules.Input == null)
        {
            Register(terminal);
            if (!_rules.TryGetValue(terminal, out rules) || rules.Input == null) return true;
        }
        rules.Input.EnableInitials = _initials?.Value ?? true;
        var resolved = rules.Input.Resolve(source, _chinese?.Value ?? true, _pinyin?.Value ?? true,
            terminal.currentNode != null && terminal.currentNode.overrideOptions, out var ambiguous);
        if (ambiguous.Length > 0)
        {
            if (_feedback == null) { _feedback = ScriptableObject.CreateInstance<TerminalNode>(); _feedback.hideFlags = HideFlags.HideAndDontSave; _feedback.clearPreviousText = true; }
            _feedback.displayText = "\n输入有多个匹配项，请输入更完整的名称：\n\n" +
                string.Join("\n", ambiguous.Take(12).Select(k => "* " + TranslationService.BuildTerminalLocalizedItemName(k) + " <color=#A0A0A0>(" + k + ")</color>")) + "\n\n未执行命令。\n";
            result = _feedback;
            return false;
        }
        _parsingTerminal = terminal;
        _original = source;
        _resolved = resolved;
        return true;
    }

    internal static void RewriteParserInput(Terminal terminal, ref string source)
    {
        if (ReferenceEquals(terminal, _parsingTerminal) && source == _original && _resolved != null) source = _resolved;
    }

    internal static int GetInputLimit(int original)
        => (_chinese?.Value ?? true) || (_pinyin?.Value ?? true) || (_initials?.Value ?? true) ? Math.Max(original, 128) : original;

    internal static void ClearParse() { _parsingTerminal = null; _original = null; _resolved = null; }

    internal static string AppendGuide(string text)
    {
        if (!(_guide?.Value ?? true) || text.Contains(">中文教程", StringComparison.Ordinal) ||
            !(text.Contains("〈Moons〉", StringComparison.Ordinal) && text.Contains("〈Store〉", StringComparison.Ordinal) && text.Contains("〈Storage〉", StringComparison.Ordinal))) return text;
        return text.TrimEnd() + "\n\n>中文教程 <color=#A0A0A0>（zhhelp）</color>\n输入“中文教程”查看中文、拼音与首字母输入规则。\n\n";
    }

    internal static bool IsGuideCommand(string source)
    {
        var words = source.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.Length <= 2 && words[0].ToLowerInvariant() is "中文教程" or "中文帮助" or "zhhelp" or "zhongwenjiaocheng" or "zwjc";
    }

    internal static string GetGuidePageKey(string source)
    {
        var words = source.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var key = words.Length == 2 ? words[1].ToLowerInvariant() : "";
        if (key == "规则") key = "rules";
        if (key.StartsWith("命令", StringComparison.Ordinal)) key = "commands" + key.Substring(2);
        if (key.StartsWith("家具", StringComparison.Ordinal)) key = "furniture" + key.Substring(2);
        if (key.StartsWith("商品", StringComparison.Ordinal)) key = "items" + key.Substring(2);
        return _guidePages.ContainsKey(key) ? key : "";
    }

    internal static bool IsGuideText(string text) => text.StartsWith(TerminalHelpContent.Header, StringComparison.Ordinal);
    internal static string BuildGuide() => _guidePages[""];
    internal static string GetGuidePage(string key) => _guidePages[key];

    private static void RefreshGuidePages(object? sender, EventArgs args)
        => _guidePages = TerminalHelpContent.Build(_chinese?.Value ?? true, _pinyin?.Value ?? true, _initials?.Value ?? true);

    private static void PrepareGuideNodes()
    {
        foreach (var pair in _guidePages)
        {
            if (!GuideNodes.TryGetValue(pair.Key, out var node))
            {
                node = ScriptableObject.CreateInstance<TerminalNode>();
                node.hideFlags = HideFlags.HideAndDontSave;
                node.clearPreviousText = true;
                GuideNodes.Add(pair.Key, node);
            }
            node.displayText = pair.Value;
        }
    }

    private static void RemoveRetiredInputSetting(ConfigFile config, string section)
    {
        var definition = new ConfigDefinition(section, "EnableFuzzyCommands");
        var changed = config.Remove(definition);
        var property = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (property?.GetValue(config) is System.Collections.IDictionary entries && entries.Contains(definition))
        {
            entries.Remove(definition);
            changed = true;
        }
        if (changed) config.Save();
    }

    internal static string UnstyleHelp(string text)
    {
        foreach (var name in new[] { "Moons", "Store", "Bestiary", "Storage", "Other" })
        {
            var token = "〈" + name + "〉";
            text = text.Replace("<color=#A0A0A0>" + token + "</color>", token);
        }
        return text;
    }

    internal static string StyleHelp(string text)
    {
        // Fixed headings only; never style payloads, arbitrary names, or nested tags.
        foreach (var name in new[] { "Moons", "Store", "Bestiary", "Storage", "Other" })
        {
            var token = "〈" + name + "〉";
            var styled = "<color=#A0A0A0>" + token + "</color>";
            text = text.Replace(styled, token).Replace(token, styled);
        }
        return text;
    }

    internal static void Shutdown()
    {
        ClearParse();
        _rules = new ConditionalWeakTable<Terminal, Rules>();
        if (_feedback != null) UnityEngine.Object.Destroy(_feedback);
        _feedback = null;
        foreach (var node in GuideNodes.Values) if (node != null) UnityEngine.Object.Destroy(node);
        GuideNodes.Clear();
        if (_chinese != null) _chinese.SettingChanged -= RefreshGuidePages;
        if (_pinyin != null) _pinyin.SettingChanged -= RefreshGuidePages;
        if (_initials != null) _initials.SettingChanged -= RefreshGuidePages;
    }
}
