using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace V81TestChn;

internal static class RuntimeCommandFeedbackLocalizer
{
    private static readonly Dictionary<string, string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        [">LOBBY [command] (lobby name)"] = ">房间管理 [命令]（房间名称） <color=#A0A0A0>（lobby [command] (lobby name)）</color>",
        ["type lobby help for more info."] = "输入 <color=#A0A0A0>lobby help</color> 查看房间管理帮助。",
        ["There are no items to sort"] = "没有可整理的物品", ["No items sorted"] = "没有整理任何物品",
        ["Script reloaded successfully."] = "脚本已重新加载。", ["Autosort failed, check the logs for more details"] = "自动整理失败，请查看日志中的详细信息",
        ["Manually sorts your ship"] = "手动整理飞船内的物品", ["Reloads your sorting script from disk"] = "重新加载本地整理脚本",
        ["Displays some information about ShipSort"] = "显示物品整理状态",
        ["Lobby cannot be changed at the moment"] = "当前无法修改房间", ["Failed to fetch lobby ( was null )"] = "无法获取房间信息（房间为空）",
        ["Server is limited to local connections"] = "服务器仅允许本地连接", ["Lobby name cannot be null"] = "房间名称不能为空",
        ["Cannot Edit Challenge Save"] = "无法编辑挑战存档", ["You need to specify a destination for the swap!"] = "请指定要切换到的存档！",
        ["Lobby is now Empty!"] = "房间已清空！", ["All Items Dropped"] = "已放下全部物品",
        ["Lobby Status:"] = "房间状态：", ["Lobby Queue Status:"] = "房间排队状态：",
        ["Invalid Command, options:"] = "无效命令，可用选项：", ["Saving:"] = "存档管理：", ["Extra:"] = "其他功能：",
        ["LAN:"] = "局域网：", ["Steam:"] = "Steam 房间：", ["Exception!"] = "操作发生异常！", ["Can only be used while in Orbit"] = "只能在轨道上使用",
        ["Script execution timed out"] = "整理脚本执行超时", ["Check the logs for more details"] = "请查看日志中的详细信息",
        ["Type 'lobby status queue' for more details"] = "输入 <color=#A0A0A0>lobby status queue</color> 查看排队详情",
        ["─ No client is currently connecting"] = "─ 当前没有正在连接的玩家",
        ["SharedConfigSizeLimit"] = "共享脚本大小上限",
        ["Maximum size for shared configs that can be received (in bytes, default: 10MB)"] = "允许接收的共享脚本大小上限（字节，默认 10MB）",
        ["SaveLimit"] = "存档物品上限",
        ["SteamLobby"] = "Steam 房间",
        ["LogSpam"] = "日志过滤",
        ["JoinQueue"] = "加入队列",
        ["auto_lobby"] = "返回轨道后重新开放房间",
        ["CalculatePolygonPath"] = "停止死亡敌人的寻路",
        ["max_size"] = "队列人数上限",
        ["connection_timeout_ms"] = "连接超时（毫秒）",
        ["timeout_notification"] = "连接超时通知",
        ["connection_notification"] = "玩家连接通知",
        ["pre-lobby_channel"] = "预连接频道",
        ["sync_radar_names"] = "同步雷达名称顺序",
        ["reset_player_values"] = "重置玩家状态",
        ["remove the limit to the amount of items that can be saved"] = "移除存档可保存的物品数量上限",
        ["automatically reopen the lobby as soon as you reach orbit"] = "回到轨道后自动重新开放房间",
        ["prevent some annoying log spam"] = "过滤部分重复刷屏日志",
        ["stop pathfinding for dead Enemies"] = "停止已死亡敌人的寻路",
        ["handle joining players as a queue instead of at the same time"] = "让玩家排队加入房间，避免同时连接",
        ["max number of players in queue ( if queue is full extra connections will be refused )"] = "等待队列的人数上限，队列满时拒绝后续连接",
        ["After how much time discard a hanging connection"] = "连接无响应时，等待多久后断开（毫秒）",
        ["show a popup when a client fails to join before the timeout"] = "玩家未能在时限内加入时显示通知",
        ["show a popup when a client tries to join"] = "玩家尝试加入时显示通知",
        ["channel to use for pre-lobby connections"] = "加入房间前预连接所使用的频道",
        ["handle extra actions requested by host"] = "处理房主请求的额外操作",
        ["allow host to reorder radar names to align clients"] = "允许房主重新排列雷达名称，使客户端顺序一致",
        ["WARNING: all clients need to have the mod installed or desyncs might will happen"] = "警告：所有客户端都需要安装此模组，否则可能出现不同步",
        ["allow host to force clients to reset most fields of a playerObject ( fix for invisible players )"] = "允许房主要求客户端重置玩家对象的大部分状态，用于修复玩家不可见问题",
        ["AutoSort"] = "自动整理", ["ScriptPath"] = "整理脚本路径", ["ShareConfig"] = "共享整理脚本",
        ["UseSharedConfig"] = "使用房主的整理脚本", ["Timeout"] = "执行超时",
        ["The item sorting script to use (absolute path or relative to this config file)"] = "要使用的整理脚本路径，可使用绝对路径或相对于配置文件的路径",
        ["The maximum execution time for the sorting script in seconds (prevents freezing, 0 to disable [NOT RECOMMENDED])"] = "整理脚本的最长执行时间（秒），用于防止卡死。0 表示不限时（不建议）",
        ["[HOST-ONLY] Automatically sorts all items before the ship lands and after it takes off"] = "[仅房主] 飞船降落前和起飞后自动整理全部物品",
        ["Shares your sorting script to other players with the mod when they join your lobby"] = "玩家加入房间时，向同样安装此模组的玩家共享整理脚本",
        ["Whether to use sorting scripts sent by the lobby host"] = "是否使用房主发送的整理脚本"
    };
    private static readonly Dictionary<string, string> Help = new(StringComparer.OrdinalIgnoreCase)
    {
        ["prints the current lobby status"] = "查看当前房间状态", ["open the lobby"] = "允许玩家加入房间",
        ["close the lobby"] = "停止接受玩家加入", ["set lobby to Invite Only"] = "设为仅限邀请",
        ["set lobby to Friends Only"] = "设为仅限好友", ["set lobby to Public"] = "设为公开房间",
        ["change the name of the lobby"] = "修改房间名称", ["toggle autosave for the savefile"] = "切换自动保存",
        ["save the lobby"] = "保存当前房间", ["load a savefile"] = "加载指定存档",
        ["swap savefile without loading it"] = "切换保存目标，但不加载该存档", ["reset the current lobby to empty"] = "清空当前房间",
        ["drop all items to the ground"] = "将全部物品放到地面", ["set the lobby to be discoverable"] = "允许搜索到此房间",
        ["set the lobby to be non-discoverable"] = "隐藏此房间"
    };
    private static readonly (string Pattern, string Replacement)[] Patterns =
    {
        (@"^Script '(.*)' could not be found$", "找不到整理脚本“$1”"),
        (@"^Script result invalid: (.+)$", "整理脚本返回结果无效：$1"),
        (@"^Script compilation error: (.+)$", "整理脚本编译错误：$1"),
        (@"^Script error: (.+)$", "整理脚本错误：$1"),
        (@"^Sorted (\d+)/(\d+) in (.+)$", "已整理 $1/$2 件物品，用时 $3"),
        (@"^(\d+) items couldn't be sorted$", "$1 件物品无法整理"),
        (@"^Autosort failed: (.+)$", "自动整理失败：$1"),
        (@"^Current script: (.+)$", "当前脚本：$1"),
        (@"^Autosorted (.+) ago \((\d+)/(\d+) items in (.+)\) using (.+)$", "$1 前自动整理 $2/$3 件物品，用时 $4，脚本：$5"),
        (@"^Sorted (.+) ago \((\d+)/(\d+) items in (.+)\) using (.+)$", "$1 前整理 $2/$3 件物品，用时 $4，脚本：$5"),
        (@"^Version conflict \(Script is outdated: expected (.+), currently (.+)\)$", "版本不兼容（脚本过旧：需要 $1，当前 $2）"),
        (@"^Version conflict \(Mod is outdated: expected (.+), currently (.+)\)$", "版本不兼容（模组过旧：需要 $1，当前 $2）"),
        (@"^Your config script is too large to share: expected less than (.+), got (.+)$", "整理脚本过大，无法共享：需要小于 $1，当前 $2"),
        (@"^Shared config exceeded size limit: (.+)$", "共享脚本超过大小限制：$1"),
        (@"^Lobby renamed to ""(.*)""$", "房间已重命名为“$1”"),
        (@"^Lobby Saved to (.+)$", "房间已保存至 $1"),
        (@"^Lobby '(.*)' loaded$", "已加载存档“$1”"),
        (@"^Lobby is now saving to '(.*)'$", "当前保存目标为“$1”"),
        (@"^- File is '(.*)'$", "- 存档：“$1”"), (@"^- Name is '(.*)'$", "- 房间名：“$1”"),
        (@"^┬ Connecting client is '(.*)'$", "┬ 正在连接：“$1”"), (@"^└ Remaining time: (.+) ms$", "└ 剩余时间：$1 毫秒"),
        (@"^┬ Queued clients: (\d+)$", "┬ 排队人数：$1"), (@"^- (\d+) Players are waiting in queue$", "- $1 名玩家正在排队")
    };
    private static readonly Regex[] Compiled = Array.ConvertAll(Patterns, p => new Regex(p.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20)));

    internal static bool CanHandle(string source)
    {
        var text = source.Trim();
        return QuickSellLocalizationService.CanHandle(text) || Exact.ContainsKey(text) || text.StartsWith("[ShipSort] ", StringComparison.Ordinal) ||
            text.StartsWith("Your config script ", StringComparison.Ordinal) || text.StartsWith("Shared config ", StringComparison.Ordinal) ||
            text.StartsWith("Sorted ", StringComparison.Ordinal) || text.StartsWith("Autosort", StringComparison.Ordinal) ||
            text.StartsWith("Script ", StringComparison.Ordinal) || text.StartsWith("Current script:", StringComparison.Ordinal) || text.StartsWith("Version conflict (", StringComparison.Ordinal) ||
            text.StartsWith("Lobby ", StringComparison.Ordinal) || text.StartsWith("AutoSaving ", StringComparison.Ordinal) ||
            text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("┬ ", StringComparison.Ordinal) || text.StartsWith("└ ", StringComparison.Ordinal) ||
            (text.Length > 0 && char.IsDigit(text[0]) && text.Contains(" items couldn't be sorted", StringComparison.Ordinal));
    }

    internal static bool TryTranslate(string source, out string translated)
    {
        translated = source;
        var text = source.Trim();
        if (text.Length == 0 || text.Length > 2048 || !CanHandle(text)) return false;
        if (text.StartsWith("[ShipSort] ", StringComparison.Ordinal))
        {
            if (!TryTranslate(text.Substring(11), out var body)) return false;
            translated = "[ShipSort] " + body;
            return true;
        }
        if (QuickSellLocalizationService.CanHandle(text) && QuickSellLocalizationService.TryTranslateLine(text, out translated)) return true;
        if (Exact.TryGetValue(text, out var exact)) { translated = exact; return true; }
        var colon = text.IndexOf(':');
        if (text.StartsWith("- ", StringComparison.Ordinal) && colon > 0 && Help.TryGetValue(text.Substring(colon + 1).Trim(), out var help))
        {
            translated = "- " + help + " <color=#A0A0A0>(lobby " + text.Substring(2, colon - 2).Trim() + ")</color>";
            return true;
        }
        foreach (var prefix in new[] { "Lobby is now ", "AutoSaving is now ", "- Status is ", "- Visibility is ", "- Saving is " })
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var value = text.Substring(prefix.Length);
            var localized = value switch { "Open" => "开放", "Closed" => "关闭", "Private" => "仅限邀请", "Friends Only" => "仅限好友", "Public" => "公开", "Automatic" => "自动", "Manual" => "手动", "On" => "开启", "Off" => "关闭", _ => value };
            if (localized == value) continue;
            translated = (prefix switch { "Lobby is now " => "房间已设为：", "AutoSaving is now " => "自动保存：", "- Status is " => "- 状态：", "- Visibility is " => "- 可见范围：", _ => "- 保存方式：" }) + localized;
            return true;
        }
        for (var i = 0; i < Compiled.Length; i++)
        {
            if (!Compiled[i].IsMatch(text)) continue;
            translated = Compiled[i].Replace(text, Patterns[i].Replacement);
            return true;
        }
        return false;
    }

    internal static string TranslateTerminalLines(string source)
    {
        var lines = source.Split('\n');
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
            if (TryTranslate(lines[i], out var line)) { lines[i] = line; changed = true; }
        return changed ? string.Join("\n", lines) : source;
    }
}
