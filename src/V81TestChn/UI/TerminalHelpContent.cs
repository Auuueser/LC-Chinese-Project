using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace V81TestChn;

// Rebuilt only at initialization / configuration changes. Each rendered page is bounded.
internal static class TerminalHelpContent
{
    internal const string Header = "\n<color=#F2C14E>中文帮助";
    private static string Input(string text) => "<color=#FF9E3D>" + text + "</color>";
    private static string Note(string text) => "<color=#A0A0A0>" + text + "</color>";
    private static string State(bool on) => on ? "<color=#91D18B>开启</color>" : "<color=#EF9A9A>关闭</color>";
    private static string Title(string title) => Header + " · " + title + "</color>\n\n";
    private static string Back => "\n" + Input("zhhelp") + " 返回中文帮助；" + Input("help") + " 返回主命令列表。\n\n";
    private static readonly HashSet<string> DestinationWords = new(StringComparer.Ordinal)
    {
        "moons", "route", "company", "experimentation", "assurance", "vow", "offense", "march",
        "adamance", "rend", "dine", "titan", "artifice", "embrion"
    };

    internal static Dictionary<string, string> Build(bool chinese, bool pinyin, bool initials)
    {
        var pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        pages[""] = Title("使用导航") + Note("橙色：可输入内容；金色：标题；灰色：说明。\n") +
            "中文 " + State(chinese) + "  拼音 " + State(pinyin) + "  首字母 " + State(initials) + "\n\n" +
            Input("zhhelp rules") + "  输入规则与快速购买\n" +
            Input("zhhelp commands") + "  中文命令、拼音与首字母词表\n" +
            Input("zhhelp items") + "  商品名称速查（分多页）\n" +
            Input("zhhelp furniture") + "  家具与飞船设施（分多页）\n\n" +
            "快速开始：\n" + Input("商店") + " 查看商品\n" +
            Input("2 sd") + " / " + Input("sd 2") + " 订购两支手电筒\n" +
            Input("qr") + " 确认；" + Input("qx") + " 取消\n" +
            Input("fs 消息") + " / " + Input("fasong 消息") + " / " + Input("发送 消息") + " 发报\n\n" +
            Note("设置：LC Config → 06 终端 - 中文输入。\n规则和词表按页打开，输入末尾的下一页命令继续查看。\n") + Back;
        pages["rules"] = Title("输入规则") +
            "1. 命令、名称、数量用空格分隔；数量用数字。\n" +
            Input("购买 手电筒 2") + " / " + Input("goumai shoudiantong 2") + "\n" +
            Input("gm sd 2") + " 与上方两种写法表示同一订单。\n\n" +
            "2. 快速购买可省略购买动词，数量 1–10。\n" + Input("2 sd") + " / " + Input("sd 2") + "\n" +
            Input("2 zysd") + " / " + Input("zysd 2") + " 订购专业手电筒。\n" +
            "仅匹配当前可购商品，仍需核对订单并确认。\n\n" +
            "3. 拼音不带声调；首字母按登记词表识别。\n" +
            "可混用：" + Input("购买 flashlight 2") + "；不自动理解任意句子。\n" +
            "同缩写多个候选时不执行，例如单独 " + Input("sd") + "。\n" +
            "请重新输入完整命令；候选不支持编号选择。\n\n" +
            "4. 发报：" + Input("fs 内容") + " / " + Input("fasong 内容") + " / " + Input("发送 内容") + "\n" +
            "均对应 " + Note("transmit") + "；需有信号翻译器，正文沿用原版规则。\n" +
            "监控：" + Input("qh") + " 切换目标，" + Input("qh 玩家名") + " 指定目标。\n" +
            "命令开关独立；玩家名、正文与设施代码不作别名替换。\n" +
            "目的地名称及航线输入沿用原版规则；第三方命令沿用各模组原有写法。\n" + Back;
        var definitions = TerminalInputRules.Definitions.Where(d => !DestinationWords.Contains(d.English)).ToArray();
        var itemStart = Array.FindIndex(definitions, d => d.English == "flashlight");
        AddPages(pages, "commands", "命令词表", definitions.Take(itemStart).ToArray());
        var furnitureStart = Array.FindIndex(definitions, d => d.English == "teleporter");
        AddPages(pages, "items", "商品词表", definitions.Skip(itemStart).Take(furnitureStart - itemStart).ToArray());
        AddPages(pages, "furniture", "家具与飞船设施", definitions.Skip(furnitureStart).ToArray());
        return pages;
    }

    private static void AddPages(Dictionary<string, string> pages, string key, string title,
        (string English, string Chinese, string Pinyin)[] definitions)
    {
        const int pageSize = 6;
        var count = (definitions.Length + pageSize - 1) / pageSize;
        string PageKey(int page) => page == 1 ? key : key + page;
        for (var page = 1; page <= count; page++)
        {
            var text = new StringBuilder(Title(title + " " + page + "/" + count));
            if (key == "furniture") text.Append("直接输入名称，购买或从仓库取回由原版状态决定。\n")
                .Append(Input("yslb")).Append(" / ").Append(Input("yangshenglaba")).Append(" / ").Append(Input("扬声喇叭")).Append("\n")
                .Append(Input("info xhfyq")).Append(" / ").Append(Input("xx xhfyq")).Append(" 查看详情。\n")
                .Append("部分英文名称仍由原版解析；购买前核对确认页。\n\n");
            foreach (var definition in definitions.Skip((page - 1) * pageSize).Take(pageSize))
            {
                text.Append(Input(definition.Chinese.Replace("|", " / "))).Append(" ").Append(Note("(" + definition.English + ")")).Append("\n")
                    .Append("拼音 ").Append(Input(definition.Pinyin.Replace("|", " / ")));
                var initials = TerminalInputRules.GetInitials(definition.Chinese);
                if (initials.Length > 0) text.Append("；首字母 ").Append(Input(initials));
                text.Append("\n\n");
            }
            if (page > 1) text.Append(Input("zhhelp " + PageKey(page - 1))).Append(" 上一页\n");
            if (page < count) text.Append(Input("zhhelp " + PageKey(page + 1))).Append(" 下一页\n");
            text.Append(Note("仅当前游戏已登记的命令/商品可用。\n")).Append(Back);
            pages[PageKey(page)] = text.ToString();
        }
    }
}
