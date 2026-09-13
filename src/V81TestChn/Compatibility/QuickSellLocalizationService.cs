using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static class QuickSellLocalizationService
{
    private static readonly Dictionary<string, (string Title, string Body)> Help = new(StringComparer.Ordinal)
    {
        ["HELP PAGE"] = ("使用帮助", "用法：/sell <variation> [flags]\n\nhelp：查看帮助；item：出售指定或手持的同类物品；quota：按剩余配额出售；all：出售所有未被过滤的废料。\n也可输入目标金额，例如 /sell 500。\n\n/sell help pages 查看全部页面\n/sell help flags 查看参数\n/sell help overtime 查看加班费说明\n/sell help <variation> 查看指定命令"),
        ["PAGES HELP PAGE"] = ("帮助目录", "用法：/sell help [page]\n\n可用页面：default, pages, flags, item, quota, all, amount, blacklist, priority, overtime, -o, -e, -a, -p, -n"),
        ["FLAG HELP PAGE"] = ("参数帮助", "用法：/sell <variation> [flags]\n\n参数可分开写：/sell <variation> -e -o -a\n也可合并：/sell <variation> -eoa\n配置中可以更改参数前缀，例如用 +e 代替 -e。\n\n-o：把本次出售产生的加班费计入目标金额\n-e：扣除已有资金；同时用 -o 时计入已有加班费\n-a：忽略黑名单\n-n：强制按未重新开房的情况计算加班费\n\n/sell help <flag> 查看详细说明"),
        ["ITEM HELP PAGE"] = ("按物品出售", "用法：/sell item [item]\n\n出售指定名称的全部同类物品。未指定名称时按当前手持物品识别，手中的这一件也会出售。"),
        ["QUOTA HELP PAGE"] = ("按配额出售", "用法：/sell quota [-a]\n\n按剩余配额选择物品。废料价值不足时不出售；无法凑齐精确金额时，选择满足要求的最小金额。"),
        ["ALL HELP PAGE"] = ("出售全部", "用法：/sell all [-a]\n\n出售所有未列入黑名单的物品；-a 忽略黑名单。"),
        ["AMOUNT HELP PAGE"] = ("按金额出售", "用法：/sell <amount> [-o] [-e] [-a] [-n]\n\n选择满足目标金额的物品。总价值不足时不出售；不能恰好凑齐时选择超过目标的最小金额。\n支持表达式 /sell 1500+25*2、数量后缀 /sell 2k、物品或目的地名称及数量（含折扣计算），例如 /sell art + wee2。\n可以省略加号：/sell art wee2 sho4 jet。\n注意：/sell jet 1k 会按 900*1000 计算，并不是 900+1000。"),
        ["BLACKLIST HELP PAGE"] = ("黑名单", "用法：/sell bl [-a] [-p]\n/sell bl {add | ad | a | +} [itemName] [-p]\n/sell bl {remove | rm | r | -} [itemName] [-p]\n/sell bl {clear | empty | flash | flush}\n\n不带参数时显示当前黑名单；-a 同时显示临时列表，-p 改为永久列表。\n/sell bl + 临时禁止出售手持物品；/sell bl - 临时排除该物品的黑名单限制。加 -p 修改永久列表。\n/sell bl clear 清空临时列表；关闭游戏后临时列表也会重置。"),
        ["PRIORITY HELP PAGE"] = ("优先出售列表", "用法：/sell pr [-a] [-p]\n/sell pr {add | ad | a | +} [itemName] [-p]\n/sell pr {remove | rm | r | -} [itemName] [-p]\n/sell pr {empty | flash | flush}\n\n不带参数时显示当前优先列表；-a 同时显示临时列表，-p 改为永久列表。\n/sell pr + 临时优先出售手持物品；/sell pr - 临时取消优先。加 -p 修改永久列表。\n/sell pr empty 清空临时列表；关闭游戏后临时列表也会重置。"),
        ["OVERTIME HELP PAGE"] = ("加班费帮助", "用法：/ot [-n]\n/ot <amount> [-n]\n\n显示已完成配额和柜台物品带来的加班费、终端与柜台的已有资金，以及两者总和。\n输入目标金额时，还会显示起飞后达到目标需要在终端留下多少资金。"),
        ["-O HELP PAGE"] = ("-o 参数", "用法：/sell <amount> -o\n\n把本次出售所产生的加班费计入计算：目标金额 = 起飞后终端总额 - 已有资金。\n已有出售记录产生的加班费不包含在内，需要同时使用 -e。"),
        ["-E HELP PAGE"] = ("-e 参数", "用法：/sell <amount> -e\n\n旧参数为 -t，现已改为 -e。\n从目标中扣除终端余额和柜台物品价值；同时使用 -o 时也考虑已有部分的加班费。\n目标金额 = 起飞后终端余额 = 已有资金 + 本次出售（使用 -o 时加上本次加班费）。"),
        ["-A HELP PAGE"] = ("-a 参数", "用法：/sell {quota | all | amount | bl | pr} -a\n\n出售时忽略全部黑名单，所有物品均可出售。\n与 /sell bl 或 /sell pr 一起使用时，同时显示当前列表及两种临时列表。"),
        ["-P HELP PAGE"] = ("-p 参数", "用法：/sell {bl | pr} [+ | -] -p\n\n对黑名单或优先列表进行操作时，修改永久列表，而不是临时列表。"),
        ["-N HELP PAGE"] = ("-n 参数", "用法：/sell <amount> -n\n\n本次命令的所有加班费计算均按配额最后一天之后未重新开房处理。\n主要用于房主允许中途加入且你在最后一天之后入场的情况：客户端可能误以为房主重新开过房，导致计算少 15。\n若房主确实重新开过房，使用 -n 反而会多算 15；先与房主确认是否重新开房。"),
    };
    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        ["Item Blacklist"] = "物品黑名单",
        ["Priority Items"] = "优先售卖物品",
        ["Flag Prefix"] = "标志前缀",
        ["Items to never sell by internal name (comma-separated)"] = "永不出售的物品内部名称（逗号分隔）",
        ["Items which are prioritized when selling"] = "出售时优先选择的物品",
        ["The symbol which is used as prefix in flags (aka \"-\" in \"-e\")"] = "命令标志使用的前缀符号（例如 -e 中的 -）",
        ["Failed to evalute expression"] = "表达式计算失败",
        ["No page with this name exists"] = "不存在这个帮助页面",
        ["No items were found"] = "未找到可售物品",
        ["The value must be positive"] = "数值必须为正",
        ["Cannot find terminal!"] = "未找到终端！",
        ["Cannot find terminal?!"] = "未找到终端！",
        ["Wrong item name"] = "物品名称错误",
        ["localPlayerController == null"] = "当前玩家尚未初始化（localPlayerController == null）",
        ["No item is held and no item was specified"] = "未持有物品，也未指定物品",
        ["You can't afford to sell that amount"] = "可售物品不足，无法卖出该金额",
        ["Error selling items"] = "售卖物品时出错",
        ["No items on the desk"] = "柜台上没有物品",
        ["Door already open"] = "门已经打开",
        ["A desk was not found"] = "未找到出售柜台",
        ["Quota is already fulfilled"] = "利润指标已完成",
        ["Successfully emptied temporary blacklist"] = "已清空临时黑名单",
        ["Successfully emptied temporary priority set"] = "已清空临时优先列表",
        ["Flag -t as a check for existing money will be depricated soon. Use -e instead"] = "检查已有资金的 -t 参数即将弃用，请改用 -e",
        ["User-inputted flag prefix was not convertable into a single symbol, it's probably not a single symbol or an invalid one. For now \"-\" flag prefix will be assumed"] = "参数前缀必须是单个有效字符；当前临时使用 -",
        ["Shows how much overtime you will get"] = "显示预计获得的加班费",
        ["-n to force non-restart calculations (if you don't know what it is don't use it)"] = "-n 强制按未重新开房计算（不了解其用途时不要使用）",
        ["The permanent blacklist cannot be emptied by the mod itself for safety reasons. If you really want to do it use something like LethalConfig or R2Modman config editor"] = "此模组不提供一键清空永久黑名单的操作；如需清空，请使用 LethalConfig 或 R2Modman 配置编辑器。",
        ["The permanent priority set cannot be emptied by the mod itself for safety reasons. If you really want to do it use something like LethalConfig or R2Modman config editor"] = "此模组不提供一键清空永久优先列表的操作；如需清空，请使用 LethalConfig 或 R2Modman 配置编辑器。",
        ["Wrong arguments. If you don't know how to use the command use \"/sell help blacklist\""] = "参数无效。输入 /sell help blacklist 查看黑名单用法。",
        ["Wrong arguments. If you don't know how to use the command use \"/sell help priority\""] = "参数无效。输入 /sell help priority 查看优先列表用法。",
        ["Sells items, use \"/sell help\" to see available uses.If you find any bugs, inaccuracies or have any improvements in mind please open an issue on github (the link is on the QuickSell's modpage)."] = "出售物品，输入 /sell help 查看用法。如发现问题或有改进建议，可从 QuickSell 模组页面进入 GitHub 提交反馈。",
    };
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["QUICKSELL"] = "快速出售",
        ["SELL RESULTS"] = "出售结果",
        ["OVERTIME"] = "加班费",
        ["PERMANENT BLACKLIST"] = "永久黑名单",
        ["TEMPORARY UNBLACKLISTED"] = "临时排除黑名单",
        ["TEMPORARY BLACKLIST"] = "临时黑名单",
        ["ACTIVE BLACKLIST"] = "当前黑名单",
        ["PERMANENT PRIORITY SET"] = "永久优先列表",
        ["TEMPORARY UNPRIORITIZED"] = "临时取消优先",
        ["TEMPORARY PRIORITY SET"] = "临时优先列表",
        ["ACTIVE PRIORITY SET"] = "当前优先列表",
    };
    private static readonly (string Pattern, string Replacement)[] Patterns =
    {
        (@"^Part ""(.*)"" was unable to be processed, terminating$", "无法处理表达式部分“$1”，已停止"),
        (@"^The value (.+) is not convertable into double$", "无法把 $1 转换为数值"),
        (@"^No items called ""(.*)"" were detected$", "未找到名为“$1”的物品"),
        (@"^You already have (.+) existing money out of desired (.+)$", "目标金额 $2，当前已有资金 $1"),
        (@"^Successfully permanently blacklisted ""(.*)""$", "已将“$1”加入永久黑名单"),
        (@"^Successfully temporarily blacklisted ""(.*)""$", "已将“$1”加入临时黑名单"),
        (@"^Successfully temporarily prohibited to blacklist ""(.*)""$", "已临时排除“$1”的黑名单限制"),
        (@"^Successfully temporarily prohibited to prioritize ""(.*)""$", "已临时取消“$1”的优先出售"),
        (@"^Successfully removed ""(.*)"" from the permanent blacklist$", "已将“$1”移出永久黑名单"),
        (@"^Successfully added ""(.*)"" to the permanent priority set$", "已将“$1”加入永久优先列表"),
        (@"^Successfully added ""(.*)"" to the temporary priority set$", "已将“$1”加入临时优先列表"),
        (@"^Successfully removed ""(.*)"" from the permanent priority set$", "已将“$1”移出永久优先列表"),
        (@"^""(.*)"" is already in the permanent blacklist$", "“$1”已在永久黑名单中"),
        (@"^""(.*)"" is not in the permanent blacklist$", "“$1”不在永久黑名单中"),
        (@"^""(.*)"" is already temporarily blacklisted$", "“$1”已在临时黑名单中"),
        (@"^""(.*)"" is already temporarily prohibited to blacklist$", "“$1”已临时排除黑名单限制"),
        (@"^""(.*)"" is already in the permanent priority set$", "“$1”已在永久优先列表中"),
        (@"^""(.*)"" is not in the permanent priority set$", "“$1”不在永久优先列表中"),
        (@"^""(.*)"" is already in the temporarily priority set$", "“$1”已在临时优先列表中"),
        (@"^""(.*)"" is already temporarily prohibited to prioritize$", "“$1”已临时取消优先出售"),
        (@"^Overtime: (.+)$", "加班费：$1"),
        (@"^Money in terminal: (.+)$", "终端余额：$1"),
        (@"^Money in terminal \+ on the desk: (.+)$", "终端余额与柜台物品：$1"),
        (@"^Money after takeoff: (.+)$", "起飞后资金：$1"),
        (@"^Money required to get to (.+) after takeoff: (.+)$", "起飞后达到 $1 所需资金：$2"),
        (@"^(\d+) sold / (.+) requested$", "已出售 $1 / 目标 $2")
    };
    private static readonly Regex[] Compiled = Array.ConvertAll(Patterns, p => new Regex(p.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)));

    internal static bool CanHandle(string text) => Exact.ContainsKey(text) ||
        text.StartsWith("Part \"", StringComparison.Ordinal) || text.StartsWith("The value ", StringComparison.Ordinal) ||
        text.StartsWith("No items called ", StringComparison.Ordinal) || text.StartsWith("Successfully ", StringComparison.Ordinal) ||
        text.StartsWith("You already have ", StringComparison.Ordinal) ||
        (text.StartsWith("\"", StringComparison.Ordinal) && text.Contains(" is ", StringComparison.Ordinal));

    internal static bool TryTranslateLine(string source, out string translated)
    {
        translated = source;
        if (Exact.TryGetValue(source, out var exact)) { translated = exact; return true; }
        for (var i = 0; i < Compiled.Length; i++)
        {
            var match = Compiled[i].Match(source);
            if (!match.Success) continue;
            translated = match.Result(Patterns[i].Replacement).Replace(" (unreachable right now)", "（当前无法达到）");
            return true;
        }
        return false;
    }

    internal static void TranslateDisplay(ref string message, ref string title)
    {
        if (message.StartsWith("Usage:", StringComparison.Ordinal) && Help.TryGetValue(title, out var help))
        {
            message = help.Body;
            title = help.Title;
            return;
        }
        var isList = title.Contains("BLACKLIST", StringComparison.Ordinal) || title.Contains("PRIORITY", StringComparison.Ordinal) || title.Contains("UNPRIORITIZED", StringComparison.Ordinal);
        if (Titles.TryGetValue(title, out var heading)) title = heading;
        var lines = message.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("Command: ", StringComparison.Ordinal)) { lines[i] = "命令：" + line.Substring(9); continue; }
            if (TryTranslateLine(line, out var translated)) { lines[i] = translated; continue; }
            if (line.StartsWith("Selling ", StringComparison.Ordinal))
            {
                lines[i] = Regex.Replace(line, @"^Selling (\d+) items?(?: named ""(?<name>.*?)"")? with a total value of ", m =>
                    "正在出售 " + m.Groups[1].Value + " 件" + (m.Groups["name"].Success ? "“" + m.Groups["name"].Value + "”" : "物品") + "，总价值：", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30))
                    .Replace(" overtime", " 加班费").Replace(" existing money", " 已有资金")
                    .Replace(", sold every unblacklisted item", "，已出售全部未列入黑名单的物品").Replace(", sold every item", "，已出售全部物品");
            }
            else if (isList)
            {
                lines[i] = Regex.Replace(line, @"^(?<open><color=[^>]+>)(?<name>.+?) (?<id>\([^\r\n]*\))(?<close></color>)$", m =>
                    m.Groups["open"].Value + (m.Groups["name"].Value == "MISSING NAME" ? "名称缺失" : TranslationService.BuildTerminalLocalizedItemName(m.Groups["name"].Value)) + " " + m.Groups["id"].Value + m.Groups["close"].Value,
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30));
            }
        }
        message = string.Join("\n", lines);
    }
}
