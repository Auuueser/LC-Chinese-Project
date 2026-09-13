using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace V81TestChn;

// Pure input rules. Targets are resolved against this terminal's live keywords;
// aliases never mutate TerminalKeyword assets or bypass its purchase/route flow.
internal sealed class TerminalInputRules
{
    internal static readonly (string English, string Chinese, string Pinyin)[] Definitions =
    {
        ("help", "帮助|中文帮助|命令", "bangzhu|mingling"),
        ("moons", "星球|卫星|目的地", "xingqiu|weixing"),
        ("store", "商店|商城", "shangdian|shangcheng"),
        ("bestiary", "图鉴|生物图鉴", "tujian|shengwutujian"),
        ("storage", "仓库|存储|储存", "cangku|cunchu|chucun"),
        ("other", "其他", "qita"), ("scan", "扫描", "saomiao"),
        ("view monitor", "监控|查看监控", "jiankong|chakanjiankong"),
        ("buy", "购买|买|订购", "goumai|mai|dinggou"),
        ("info", "信息|详情|介绍", "xinxi|xiangqing|jieshao"),
        ("confirm", "确认|确定", "queren|queding"),
        ("deny", "取消|否认", "quxiao|fouren"),
        ("switch", "切换", "qiehuan"), ("ping", "鸣笛", "mingdi"),
        ("flash", "闪光", "shanguang"), ("transmit", "发送|发报", "fasong|fabao"),
        ("sigurd", "日志|西格德", "rizhi|xigede"),
        ("flashlight", "手电筒", "shoudiantong"),
        ("proflashlight", "专业手电筒|强光手电筒", "zhuanyeshoudiantong|qiangguangshoudiantong"),
        ("walkietalkie", "对讲机", "duijiangji"), ("shovel", "铲子", "chanzi"),
        ("lockpicker", "开锁器", "kaisuo qi|kaisuoqi"), ("stungrenade", "眩晕手雷|震撼手雷", "xuanyunshoulei|zhenhanshoulei"),
        ("boombox", "音响", "yinxiang"), ("inhalant", "吸入剂", "xiruji"),
        ("zapgun", "电击枪", "dianjiqiang"), ("jetpack", "喷气背包", "penqibeibao"),
        ("extensionladder", "伸缩梯|梯子", "shensuoti|tizi"),
        ("radarbooster", "雷达增幅器", "leidazengfuqi"), ("spraypaint", "喷漆", "penqi"),
        ("weedkiller", "除草剂", "chucaoji"), ("beltbag", "腰包", "yaobao"),
        ("cruiser", "巡航车|公司巡航车", "xunhangche|gongsixunhangche"),
        ("teleporter", "传送器", "chuansongqi"), ("inverseteleporter", "逆向传送器|反向传送器", "nixiangchuansongqi|fanxiangchuansongqi"),
        ("signaltranslator", "信号文本转换器|信号转换器|信号翻译器", "xinhaowenbenzhuanhuanqi|xinhaozhuanhuanqi|xinhaofanyiqi"),
        ("loudhorn", "扬声喇叭|高音号角|响亮喇叭|号角", "yangshenglaba|gaoyinhaojiao|xianglianglaba|haojiao"),
        ("table", "桌子家具|桌子", "zhuozijiaju|zhuozi"),
        ("romantictable", "浪漫小桌|浪漫桌", "langmanxiaozhuo|langmanzhuo"),
        ("cozylights", "温馨灯串|暖光灯串|温馨灯光", "wenxindengchuan|nuanguangdengchuan|wenxindengguang"),
        ("shower", "淋浴器|淋浴设施|淋浴", "linyuqi|linyusheshi|linyu"), ("toilet", "马桶", "matong"),
        ("television", "电视机|电视", "dianshiji|dianshi"), ("cupboard", "橱柜", "chugui"),
        ("bunkbeds", "上下铺|双层床", "shangxiapu|shuangcengchuang"), ("filecabinet", "文件柜", "wenjiangui"),
        ("light switch", "电灯开关|灯光开关", "diandengkaiguan|dengguangkaiguan"),
        ("sofachair", "沙发椅", "shafayi"), ("recordplayer", "唱片机", "changpianji"),
        ("discoball", "迪斯科球", "disikeqiu"), ("welcomemat", "门口地垫|迎宾垫", "menkoudidian|yingbindian"),
        ("jackolantern", "万圣南瓜灯|南瓜灯", "wanshengnanguadeng|nanguadeng"),
        ("plushiepajamaman", "毛绒睡衣公仔|睡衣小人玩偶", "maorongshuiyigongzai|shuiyixiaorenwanou"),
        ("goldfish", "金鱼|鱼缸", "jinyu|yugang"), ("classicpainting", "经典油画|古典画", "jingdianyouhua|gudianhua"),
        ("doghouse", "狗屋", "gouwu"), ("electricchair", "电椅", "dianyi"),
        ("fridge", "冰箱", "bingxiang"), ("microwave", "微波炉", "weibolu")
    };

    private static readonly Dictionary<string, string> InitialsByChinese = new(StringComparer.Ordinal)
    {
        ["帮助"]="bz", ["商店"]="sd", ["星球"]="xq", ["图鉴"]="tj", ["仓库"]="ck", ["其他"]="qt",
        ["介绍"]="js",
        ["购买"]="gm", ["信息"]="xx", ["确认"]="qr", ["取消"]="qx", ["扫描"]="sm",
        ["切换"]="qh", ["监控"]="jk", ["查看监控"]="ckjk", ["鸣笛"]="md", ["闪光"]="sg", ["发送"]="fs", ["发报"]="fb", ["日志"]="rz",
        ["手电筒"]="sdt|sd", ["专业手电筒"]="zysdt|zysd", ["强光手电筒"]="qgsdt|qgsd", ["对讲机"]="djj", ["铲子"]="cz",
        ["开锁器"]="ksq", ["眩晕手雷"]="xysl", ["音响"]="yx", ["吸入剂"]="xrj", ["电击枪"]="djq", ["喷气背包"]="pqbb",
        ["伸缩梯"]="sst", ["雷达增幅器"]="ldzfq", ["喷漆"]="pq", ["除草剂"]="ccj", ["腰包"]="yb",
        ["扬声喇叭"]="yslb", ["温馨灯串"]="wxdc", ["淋浴器"]="lyq", ["上下铺"]="sxp", ["门口地垫"]="mkdd", ["经典油画"]="jdyh", ["毛绒睡衣公仔"]="mrsygz",
        ["高音号角"]="gyhj", ["号角"]="hj", ["信号文本转换器"]="xhwbzhq", ["信号转换器"]="xhzhq", ["逆向传送器"]="nxcsq",
        ["桌子家具"]="zzjj", ["桌子"]="zz", ["浪漫小桌"]="lmxz", ["浪漫桌"]="lmz", ["暖光灯串"]="ngdc", ["温馨灯光"]="wxdg",
        ["淋浴设施"]="lyss", ["淋浴"]="ly", ["马桶"]="mt", ["电视机"]="dsj", ["电视"]="ds", ["橱柜"]="cg", ["双层床"]="scc",
        ["文件柜"]="wjg", ["电灯开关"]="ddkg", ["灯光开关"]="dgkg", ["沙发椅"]="sfy", ["唱片机"]="cpj", ["迪斯科球"]="dskq",
        ["迎宾垫"]="ybd", ["万圣南瓜灯"]="wsngd", ["南瓜灯"]="ngd", ["睡衣小人玩偶"]="syxrwo", ["金鱼"]="jy", ["鱼缸"]="yg",
        ["古典画"]="gdh", ["狗屋"]="gw", ["电椅"]="dy", ["冰箱"]="bx", ["微波炉"]="wbl",
        ["传送器"]="csq", ["反向传送器"]="fxcsq", ["信号翻译器"]="xhfyq", ["响亮喇叭"]="xllb", ["巡航车"]="xhc"

    };
    internal bool EnableInitials { get; set; } = true;
    private readonly Dictionary<string, HashSet<string>> _initials = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _purchasable = new(StringComparer.OrdinalIgnoreCase);
    internal void RegisterPurchasable(string keyword) => _purchasable.Add(keyword);
    internal static string GetInitials(string chinese) => string.Join(" / ", chinese.Split('|').Where(InitialsByChinese.ContainsKey).SelectMany(a => InitialsByChinese[a].Split('|')).Distinct());
    private readonly HashSet<string> _keywords;
    private readonly HashSet<string> _adaptedKeywords = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _nativeOnly = new(StringComparer.OrdinalIgnoreCase);
    internal void PreserveNativeKeyword(string word) => _nativeOnly.Add(word);
    internal void CompleteRegistration()
    {
        foreach (var keyword in _keywords)
            if (!_adaptedKeywords.Contains(keyword)) _nativeOnly.Add(keyword);
    }
    private readonly Dictionary<string, HashSet<string>> _chinese = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _pinyin = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PayloadCommands = new(StringComparer.OrdinalIgnoreCase)
        { "transmit", "switch", "ping", "flash", "lobby", "route" };
    internal TerminalInputRules(IEnumerable<string> keywords)
    {
        _keywords = new HashSet<string>(keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            var targets = _keywords.Where(k => Compact(k) == Compact(definition.English)).ToArray();
            // 'view monitor' is a vanilla verb + noun command, not one keyword.
            if (definition.English == "view monitor" && _keywords.Contains("view") && _keywords.Contains("monitor")) targets = new[] { "view monitor" };
            // transmit is handled directly by the vanilla parser, even without a keyword asset.
            if (definition.English == "transmit") targets = new[] { "transmit" };
            foreach (var target in targets) AddAliases(target, definition.Chinese, definition.Pinyin);
        }
    }

    internal void AddAliases(string target, string chinese, string pinyin = "")
    {
        _adaptedKeywords.Add(target);
        foreach (var alias in chinese.Split('|'))
        {
            Add(_chinese, alias, target);
            if (InitialsByChinese.TryGetValue(alias, out var initials))
                foreach (var shortName in initials.Split('|')) Add(_initials, shortName, target);
        }
        foreach (var alias in pinyin.Split('|')) Add(_pinyin, alias, target);
    }

    private static void Add(Dictionary<string, HashSet<string>> table, string alias, string target)
    {
        if (string.IsNullOrWhiteSpace(alias)) return;
        var key = Compact(alias);
        if (!table.TryGetValue(key, out var values)) table[key] = values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        values.Add(target);
    }

    internal static string Compact(string text) => new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    internal string Resolve(string source, bool chinese, bool pinyin, bool confirmation, out string[] ambiguous)
    {
        ambiguous = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(source) || source.Length > 256) return source;
        var input = source.Trim();
        var words = input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        // Explicit English commands always keep their argument bytes untouched.
        if (words.Length > 0 && (PayloadCommands.Contains(words[0]) || _nativeOnly.Contains(words[0]) ||
            (_keywords.Contains(words[0]) && !_adaptedKeywords.Contains(words[0]) && words[0] != "view"))) return source;
        if (_keywords.Contains(input) || words.Any(w => _nativeOnly.Contains(w))) return source;
        if (confirmation)
        {
            var matches = Aliases(input, chinese, pinyin).Where(k => k is "confirm" or "deny").ToArray();
            return matches.Length == 1 ? matches[0] : source;
        }

        // A quantity supplies the purchase context; only live buy nouns qualify.
        if (words.Length == 2 && _keywords.Contains("buy"))
        {
            var countIndex = words[0].All(c => c >= '0' && c <= '9') ? 0 : 1;
            if (int.TryParse(words[countIndex], out var count) && count >= 1 && count <= 10)
            {
                var candidates = Aliases(words[1 - countIndex], chinese, pinyin);
                candidates.IntersectWith(_purchasable);
                if (candidates.Count == 1) return "buy " + candidates.Single() + " " + count;
                if (candidates.Count > 1) { ambiguous = candidates.OrderBy(k => k).ToArray(); return source; }
            }
        }

        // Match whole phrases before tokenizing (Chinese names and spaced pinyin).
        var whole = Aliases(input, chinese, pinyin);
        if (whole.Count == 1) return whole.Single();
        if (whole.Count > 1) { ambiguous = whole.OrderBy(k => k).ToArray(); return source; }
        var quantity = Regex.Match(input, @"^(?<name>.+?)(?<count>\d{1,2})$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        if (quantity.Success && !input.Any(char.IsWhiteSpace))
        {
            var names = Aliases(quantity.Groups["name"].Value, chinese, pinyin);
            if (names.Count == 1) return names.Single() + " " + quantity.Groups["count"].Value;
            if (names.Count > 1) { ambiguous = names.OrderBy(k => k).ToArray(); return source; }
        }
        // Only an explicitly adapted command/name may opt into token conversion.
        // Unknown third-party commands keep their complete original argument string.
        if (!_adaptedKeywords.Contains(words[0]) && !words[0].Equals("view", StringComparison.OrdinalIgnoreCase) &&
            Aliases(words[0], chinese, pinyin).Count == 0) return source;
        var output = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (_keywords.Contains(word) || word.All(char.IsDigit)) { output.Add(word); continue; }
            var matches = Aliases(word, chinese, pinyin);
            // A purchasing verb disambiguates sd (store / flashlight) using the noun role.
            if (i > 0 && output.Count > 0 && output[0].Equals("buy", StringComparison.OrdinalIgnoreCase))
                matches.RemoveWhere(k => k is "store" or "storage" or "help" or "moons" or "other" or "bestiary");
            if (matches.Count > 1) { ambiguous = matches.OrderBy(k => k).ToArray(); return source; }
            if (matches.Count == 0) { output.Add(word); continue; }
            var target = matches.Single();
            output.Add(target);
            if (i == 0 && PayloadCommands.Contains(target))
            {
                var offset = input.IndexOf(word, StringComparison.Ordinal) + word.Length;
                return target + input[offset..];
            }
        }
        var resolved = string.Join(" ", output);
        return resolved == string.Join(" ", words) ? source : resolved;
    }

    private HashSet<string> Aliases(string text, bool chinese, bool pinyin)
    {
        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_nativeOnly.Contains(text)) return matches;
        var key = Compact(text);
        if (chinese && _chinese.TryGetValue(key, out var cn)) matches.UnionWith(cn);
        if (pinyin && _pinyin.TryGetValue(key, out var py)) matches.UnionWith(py);
        if (EnableInitials && _initials.TryGetValue(key, out var initials)) matches.UnionWith(initials);
        matches.ExceptWith(_nativeOnly);
        return matches;
    }

}
