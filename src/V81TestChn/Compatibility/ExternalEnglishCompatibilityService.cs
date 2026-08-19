using System;
using System.Collections.Generic;
using System.Text;

namespace V81TestChn;

internal static class ExternalEnglishCompatibilityService
{
    private const int MaxSourceLength = 512;
    private const int RuntimeCacheLimit = 4096;
    private const string DiscountAlertNoDiscountLocalizedText = "\u6682\u65e0\u6298\u6263\n\u660e\u5929\u518d\u6765\u67e5\u770b";

    private static readonly Dictionary<string, string> ExactEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Emote Menu"] = "\u52a8\u4f5c\u83dc\u5355",
        ["Random Emote"] = "\u968f\u673a\u52a8\u4f5c",
        ["Perform Random Emote"] = "\u6267\u884c\u968f\u673a\u52a8\u4f5c",
        ["Zoom"] = "\u7f29\u653e",
        ["Freeze"] = "\u51bb\u7ed3",
        ["Swap Page"] = "\u5207\u6362\u9875\u9762",
        ["Favorite Emote"] = "\u6536\u85cf\u52a8\u4f5c",
        ["Set Quick Emote"] = "\u8bbe\u7f6e\u5feb\u6377\u52a8\u4f5c",
        ["Mute emote audio"] = "\u9759\u97f3\u52a8\u4f5c\u97f3\u9891",
        ["Only perform emote"] = "\u4ec5\u64ad\u653e\u52a8\u4f5c",
        ["no audio"] = "\u65e0\u97f3\u9891",
        ["(no audio)"] = "\uff08\u65e0\u97f3\u9891\uff09",
        ["First person emotes"] = "\u7b2c\u4e00\u4eba\u79f0\u52a8\u4f5c",
        ["Move while emoting"] = "\u505a\u52a8\u4f5c\u65f6\u53ef\u79fb\u52a8",
        ["experimental"] = "\u5b9e\u9a8c\u6027",
        ["(experimental)"] = "\uff08\u5b9e\u9a8c\u6027\uff09",
        ["DMCA-free mode"] = "DMCA \u5b89\u5168\u6a21\u5f0f",
        ["Emote volume"] = "\u52a8\u4f5c\u97f3\u91cf",
        ["Tell autopilot ship to leave early"] = "\u547d\u4ee4\u81ea\u52a8\u9a7e\u9a76\u98de\u8239\u63d0\u524d\u79bb\u5f00",
        ["Spectate Previous Player"] = "\u5207\u6362\u4e0a\u4e00\u540d\u73a9\u5bb6",
        ["Open Admin UI"] = "\u6253\u5f00\u7ba1\u7406\u754c\u9762",
        ["Copy Lobby ID"] = "\u590d\u5236\u623f\u95f4 ID",
        ["Enter tag or id..."] = "\u8f93\u5165\u6807\u7b7e\u6216 ID...",
        ["Enter tag or ip..."] = "\u8f93\u5165\u6807\u7b7e\u6216 IP...",
        ["Search Mods"] = "\u641c\u7d22\u6a21\u7ec4",
        ["Search Configs"] = "\u641c\u7d22\u914d\u7f6e",
        ["Close"] = "\u5173\u95ed",
        ["Apply"] = "\u5e94\u7528",
        ["Delete"] = "\u5220\u9664",
        ["Go back"] = "\u8fd4\u56de",
        ["OK"] = "\u786e\u8ba4",
        ["Some of the modified settings may require a restart to take effect."] = "\u90e8\u5206\u5df2\u4fee\u6539\u7684\u8bbe\u7f6e\u53ef\u80fd\u9700\u8981\u91cd\u542f\u6e38\u620f\u624d\u80fd\u751f\u6548\u3002",
        ["This is a test notification"] = "\u8fd9\u662f\u4e00\u6761\u6d4b\u8bd5\u901a\u77e5",
        ["Connection refused"] = "\u8fde\u63a5\u88ab\u62d2\u7edd",
        ["Connection request"] = "\u8fde\u63a5\u8bf7\u6c42",
        ["Connection Timeout"] = "\u8fde\u63a5\u8d85\u65f6",
        ["Low Connection Timeout!"] = "\u8fde\u63a5\u8d85\u65f6\u8bbe\u7f6e\u8fc7\u4f4e\uff01",
        ["If clients frequently fail to connect maybe consider increasing \"connection_timeout_ms\" in LobbyControl config"] = "\u5982\u679c\u5ba2\u6237\u7aef\u7ecf\u5e38\u8fde\u63a5\u5931\u8d25\uff0c\u8bf7\u8003\u8651\u8c03\u9ad8 LobbyControl \u914d\u7f6e\u4e2d\u7684 \"connection_timeout_ms\"\u3002",
        ["Server Password"] = "\u623f\u95f4\u5bc6\u7801",
        ["Validate Steam Sessions"] = "\u9a8c\u8bc1 Steam \u4f1a\u8bdd",
        ["Validate Steam Sessions is not currently usable in 'public' lobbies unless you set a custom lobby tag!"] = "\u9a8c\u8bc1 Steam \u4f1a\u8bdd\u5f53\u524d\u65e0\u6cd5\u7528\u4e8e\u201c\u516c\u5f00\u201d\u623f\u95f4\uff0c\u9664\u975e\u8bbe\u7f6e\u81ea\u5b9a\u4e49\u623f\u95f4\u6807\u7b7e\uff01",
        ["Password Protection is not currently usable in 'public' lobbies unless you set a custom lobby tag!"] = "\u5bc6\u7801\u4fdd\u62a4\u5f53\u524d\u65e0\u6cd5\u7528\u4e8e\u201c\u516c\u5f00\u201d\u623f\u95f4\uff0c\u9664\u975e\u8bbe\u7f6e\u81ea\u5b9a\u4e49\u623f\u95f4\u6807\u7b7e\uff01",
        ["Loading server list..."] = "\u6b63\u5728\u52a0\u8f7d\u670d\u52a1\u5668\u5217\u8868...",
        ["No available servers to join."] = "\u6ca1\u6709\u53ef\u52a0\u5165\u7684\u670d\u52a1\u5668\u3002",
        ["Crew Size:"] = "\u8239\u5458\u4eba\u6570\uff1a",
        ["Sort: Friends"] = "\u6392\u5e8f\uff1a\u4ec5\u597d\u53cb",
        ["Sort: Similar rank"] = "\u6392\u5e8f\uff1a\u76f8\u8fd1\u6392\u540d",
        ["Sort: Top 20"] = "\u6392\u5e8f\uff1a\u524d 20 \u540d",
        ["Loading ranking..."] = "\u6b63\u5728\u52a0\u8f7d\u6392\u540d...",
        ["No entries to display!"] = "\u6ca1\u6709\u53ef\u663e\u793a\u7684\u6761\u76ee\uff01",
        ["Max Players"] = "\u6700\u5927\u4eba\u6570",
        ["Server Access"] = "\u623f\u95f4\u6743\u9650",
        ["Invite-only"] = "\u4ec5\u9650\u9080\u8bf7",
        ["Friends-only"] = "\u4ec5\u9650\u597d\u53cb",
        ["Server Tag"] = "\u623f\u95f4\u6807\u7b7e",
        ["New File"] = "\u65b0\u5b58\u6863",
        ["Players"] = "\u73a9\u5bb6",
        ["Connection resumed"] = "\u8fde\u63a5\u5df2\u6062\u590d",
        ["GAME START CANCELLED"] = "\u6e38\u620f\u5f00\u59cb\u5df2\u53d6\u6d88",
        ["Today's discounts"] = "\u4eca\u65e5\u6298\u6263",
        ["AVAILABLE NOW!"] = "\u73b0\u5df2\u53d1\u552e\uff01",
        ["CURES CANCER!"] = "\u6cbb\u6108\u764c\u75c7\uff01",
        ["NO WAY!"] = "\u4e0d\u4f1a\u5427\uff01",
        ["LIMITED TIME ONLY!"] = "\u9650\u65f6\u4f9b\u5e94\uff01",
        ["GET YOURS TODAY!"] = "\u4eca\u5929\u5c31\u6765\u9886\u53d6\uff01",
        ["Cannot return from storage while the ship is landing or leaving."] = "\u98de\u8239\u6b63\u5728\u964d\u843d\u6216\u79bb\u5f00\u65f6\uff0c\u65e0\u6cd5\u4ece\u4ed3\u50a8\u53d6\u56de\u7269\u54c1\u3002",
        ["Infection"] = "\u611f\u67d3",
        ["HUDScale"] = "HUD \u7f29\u653e",
        ["HideHealthbarAutomatically"] = "\u81ea\u52a8\u9690\u85cf\u751f\u547d\u6761",
        ["HealthbarHideDelay"] = "\u751f\u547d\u6761\u9690\u85cf\u5ef6\u8fdf",
        ["FlashlightBattery"] = "\u624b\u7535\u7b52\u7535\u91cf\u663e\u793a",
        ["DetailedStamina"] = "\u8be6\u7ec6\u8010\u529b",
        ["DisplayTimeLeft"] = "\u663e\u793a\u5269\u4f59\u65f6\u95f4",
        ["HidePlanetInfo"] = "\u9690\u85cf\u661f\u7403\u4fe1\u606f",
        ["PercentageOnly"] = "\u4ec5\u767e\u5206\u6bd4",
        ["The size of the HUD."] = "HUD \u754c\u9762\u7684\u7f29\u653e\u5927\u5c0f\u3002",
        ["Should the healthbar be hidden after not taking damage for a while."] = "\u4e00\u6bb5\u65f6\u95f4\u672a\u53d7\u4f24\u540e\u662f\u5426\u81ea\u52a8\u9690\u85cf\u751f\u547d\u6761\u3002",
        ["The amount of time before the healthbar starts fading away."] = "\u751f\u547d\u6761\u5f00\u59cb\u6de1\u51fa\u524d\u7684\u7b49\u5f85\u65f6\u95f4\u3002",
        ["How the flashlight battery is displayed whilst unequipped."] = "\u672a\u88c5\u5907\u624b\u7535\u7b52\u65f6\u5982\u4f55\u663e\u793a\u5176\u7535\u91cf\u3002",
        ["Disabled - Flashlight battery will not be displayed."] = "\u7981\u7528 - \u4e0d\u663e\u793a\u624b\u7535\u7b52\u7535\u91cf\u3002",
        ["Vanilla - Flashlight battery will be displayed when you don't have a battery-using item equipped."] = "\u539f\u7248 - \u672a\u88c5\u5907\u5176\u4ed6\u8017\u7535\u7269\u54c1\u65f6\u663e\u793a\u624b\u7535\u7b52\u7535\u91cf\u3002",
        ["Separate - Flashlight battery will be displayed using a dedicated panel. (recommended)"] = "\u72ec\u7acb - \u4f7f\u7528\u72ec\u7acb\u9762\u677f\u663e\u793a\u624b\u7535\u7b52\u7535\u91cf\u3002\uff08\u63a8\u8350\uff09",
        ["What the stamina text should display."] = "\u8010\u529b\u6587\u5b57\u8981\u663e\u793a\u7684\u5185\u5bb9\u3002",
        ["Disabled - The stamina text will be hidden."] = "\u7981\u7528 - \u9690\u85cf\u8010\u529b\u6587\u5b57\u3002",
        ["PercentageOnly - Only the percentage will be displayed. (recommended)"] = "\u4ec5\u767e\u5206\u6bd4 - \u53ea\u663e\u793a\u767e\u5206\u6bd4\u3002\uff08\u63a8\u8350\uff09",
        ["Full - Both percentage and rate of gain/loss will be displayed."] = "\u5b8c\u6574 - \u540c\u65f6\u663e\u793a\u767e\u5206\u6bd4\u4e0e\u589e\u51cf\u901f\u7387\u3002",
        ["Should the uses/time left for a battery-using item be displayed."] = "\u662f\u5426\u663e\u793a\u8017\u7535\u7269\u54c1\u7684\u5269\u4f59\u4f7f\u7528\u6b21\u6570\u6216\u65f6\u95f4\u3002",
        ["Should planet info be hidden. If modifying from an in-game menu, this requires you to rejoin the game."] = "\u662f\u5426\u9690\u85cf\u661f\u7403\u4fe1\u606f\u3002\u82e5\u5728\u6e38\u620f\u5185\u83dc\u5355\u4fee\u6539\uff0c\u9700\u8981\u91cd\u65b0\u52a0\u5165\u6e38\u620f\u3002",
        ["Target on ship"] = "\u98de\u8239\u4e0a\u7684\u76ee\u6807",
        ["Signal lost"] = "\u4fe1\u53f7\u4e22\u5931",
        ["Antenna stored"] = "\u5929\u7ebf\u5df2\u5b58\u653e",
        ["PERFORMANCE REPORT"] = "\u7ee9\u6548\u62a5\u544a",
        ["NO SURVIVORS"] = "\u65e0\u4eba\u751f\u8fd8",
        ["NOTES"] = "\u5907\u6ce8",
        ["DECEASED"] = "\u6b7b\u4ea1",
        ["MISSING"] = "\u5931\u8e2a",
        ["Collected"] = "\u5df2\u6536\u96c6",
        ["Grade"] = "\u8bc4\u7ea7",
        ["Rating"] = "\u8bc4\u7ea7",
        ["Lost 100% scrap"] = "\u635f\u5931 100% \u5e9f\u6599",
        ["* Unknown"] = "* \u672a\u77e5\u539f\u56e0",
        ["* Bludgeoning"] = "* \u949d\u51fb",
        ["* Gravity"] = "* \u5760\u843d",
        ["* Blast"] = "* \u7206\u70b8",
        ["* Strangulation"] = "* \u52d2\u6740",
        ["* Suffocation"] = "* \u7a92\u606f",
        ["* Mauling"] = "* \u6495\u54ac",
        ["* Gunshots"] = "* \u67aa\u51fb",
        ["* Crushing"] = "* \u78be\u538b",
        ["* Drowning"] = "* \u6eba\u6c34",
        ["* Abandoned"] = "* \u906d\u9057\u5f03",
        ["* Electrocution"] = "* \u89e6\u7535",
        ["* Kicking"] = "* \u8e22\u51fb",
        ["* Burning"] = "* \u71c3\u70e7",
        ["* Stabbing"] = "* \u523a\u4f24",
        ["* Fan"] = "* \u98ce\u6247",
        ["* Inertia"] = "* \u60ef\u6027\u649e\u51fb",
        ["* Snipping"] = "* \u526a\u4f24",
        ["* Scratching"] = "* \u6293\u4f24",
        ["* The laziest employee."] = "* \u6700\u61d2\u60f0\u7684\u5458\u5de5",
        ["The laziest employee."] = "\u6700\u61d2\u60f0\u7684\u5458\u5de5",
        ["* Most profitable"] = "* \u6700\u4f1a\u8d5a\u94b1\u7684\u5458\u5de5",
        ["Most profitable"] = "\u6700\u4f1a\u8d5a\u94b1\u7684\u5458\u5de5",
        ["No items were found"] = "\u672a\u627e\u5230\u53ef\u552e\u7269\u54c1",
        ["Error selling items"] = "\u552e\u5356\u7269\u54c1\u65f6\u51fa\u9519",
        ["You can't afford to sell that amount"] = "\u53ef\u552e\u7269\u54c1\u4e0d\u8db3\uff0c\u65e0\u6cd5\u5356\u51fa\u8be5\u91d1\u989d",
        ["Successfully emptied temporary blacklist"] = "\u5df2\u6e05\u7a7a\u4e34\u65f6\u9ed1\u540d\u5355",
        ["Successfully emptied temporary priority set"] = "\u5df2\u6e05\u7a7a\u4e34\u65f6\u4f18\u5148\u5217\u8868",
        ["Item Blacklist"] = "\u7269\u54c1\u9ed1\u540d\u5355",
        ["Priority Items"] = "\u4f18\u5148\u552e\u5356\u7269\u54c1",
        ["Flag Prefix"] = "\u6807\u5fd7\u524d\u7f00",
        ["Items to never sell by internal name (comma-separated)"] = "\u6c38\u4e0d\u51fa\u552e\u7684\u7269\u54c1\u5185\u90e8\u540d\u79f0\uff08\u9017\u53f7\u5206\u9694\uff09",
        ["Items which are prioritized when selling"] = "\u51fa\u552e\u65f6\u4f18\u5148\u9009\u62e9\u7684\u7269\u54c1",
        ["QUICKSELL"] = "\u5feb\u901f\u51fa\u552e",
        ["SELL RESULTS"] = "\u51fa\u552e\u7ed3\u679c",
        ["Command"] = "\u547d\u4ee4",
        ["Invalid command"] = "\u547d\u4ee4\u65e0\u6548",
        ["HELP PAGE"] = "\u5e2e\u52a9\u9875\u9762",
        ["ALL HELP PAGE"] = "\u5168\u90e8\u51fa\u552e\u5e2e\u52a9\u9875\u9762",
        ["AMOUNT HELP PAGE"] = "\u91d1\u989d\u5e2e\u52a9\u9875\u9762",
        ["BLACKLIST HELP PAGE"] = "\u9ed1\u540d\u5355\u5e2e\u52a9\u9875\u9762",
        ["ITEM HELP PAGE"] = "\u7269\u54c1\u5e2e\u52a9\u9875\u9762",
        ["PRIORITY HELP PAGE"] = "\u4f18\u5148\u5217\u8868\u5e2e\u52a9\u9875\u9762",
        ["QUOTA HELP PAGE"] = "\u914d\u989d\u5e2e\u52a9\u9875\u9762",
        ["FLAG HELP PAGE"] = "\u6807\u5fd7\u5e2e\u52a9\u9875\u9762",
        ["OVERTIME HELP PAGE"] = "\u52a0\u73ed\u5956\u52b1\u5e2e\u52a9\u9875\u9762",
        ["PAGES HELP PAGE"] = "\u9875\u9762\u5e2e\u52a9",
        ["-A HELP PAGE"] = "-A \u6807\u5fd7\u5e2e\u52a9",
        ["-E HELP PAGE"] = "-E \u6807\u5fd7\u5e2e\u52a9",
        ["-N HELP PAGE"] = "-N \u6807\u5fd7\u5e2e\u52a9",
        ["-O HELP PAGE"] = "-O \u6807\u5fd7\u5e2e\u52a9",
        ["-P HELP PAGE"] = "-P \u6807\u5fd7\u5e2e\u52a9",
        ["Combining flags"] = "\u7ec4\u5408\u6807\u5fd7",
        ["\"help\" to open this page or a specific help page"] = "\"help\"\uff1a\u6253\u5f00\u6b64\u9875\u6216\u6307\u5b9a\u5e2e\u52a9\u9875",
        ["\"quota\" to sell exactly for quota"] = "\"quota\"\uff1a\u6309\u914d\u989d\u7cbe\u786e\u51fa\u552e",
        ["\"all\" to sell all unfiltered scrap available"] = "\"all\"\uff1a\u51fa\u552e\u6240\u6709\u672a\u8fc7\u6ee4\u7684\u53ef\u7528\u5e9f\u6599",
        ["\"item\" to sell all items like the one you are holding or the one you specified"] = "\"item\"\uff1a\u51fa\u552e\u6240\u6301\u6709\u6216\u6307\u5b9a\u7684\u540c\u7c7b\u7269\u54c1",
        ["\"-a\" to ignore blacklist (used with quota, all, <amount>)"] = "\"-a\"\uff1a\u5ffd\u7565\u9ed1\u540d\u5355\uff08\u7528\u4e8e quota\u3001all\u3001<amount>\uff09",
        ["\"-o\" to sell accounting for overtime (used with <amount>)"] = "\"-o\"\uff1a\u8ba1\u5165\u52a0\u73ed\u5956\u52b1\u540e\u51fa\u552e\uff08\u7528\u4e8e <amount>\uff09",
        ["The symbol which is used as prefix in flags (aka \"-\" in \"-e\")"] = "\u547d\u4ee4\u6807\u5fd7\u4f7f\u7528\u7684\u524d\u7f00\u7b26\u53f7\uff08\u4f8b\u5982 -e \u4e2d\u7684 -\uff09",
        ["Command variations"] = "\u547d\u4ee4\u7528\u6cd5",
        ["The sell command was initiated"] = "\u552e\u5356\u547d\u4ee4\u5df2\u5f00\u59cb",
        ["The sell command completed it's job, terminating"] = "\u552e\u5356\u547d\u4ee4\u5df2\u5b8c\u6210\uff0c\u6b63\u5728\u7ed3\u675f",
        ["The overtime command was initiated"] = "\u52a0\u73ed\u5956\u52b1\u547d\u4ee4\u5df2\u5f00\u59cb",
        ["Use \"/sell help flags\" to see info on important flags"] = "\u8f93\u5165 \"/sell help flags\" \u67e5\u770b\u91cd\u8981\u6807\u5fd7\u8bf4\u660e",
        ["Use \"/sell help <flag>\" to see info on specific flag"] = "\u8f93\u5165 \"/sell help <flag>\" \u67e5\u770b\u6307\u5b9a\u6807\u5fd7\u8bf4\u660e",
        ["Use \"/sell help <variation>\" to see info on specific command"] = "\u8f93\u5165 \"/sell help <variation>\" \u67e5\u770b\u6307\u5b9a\u547d\u4ee4\u8bf4\u660e",
        ["Use \"/sell help pages\" to see info on all pages"] = "\u8f93\u5165 \"/sell help pages\" \u67e5\u770b\u6240\u6709\u5e2e\u52a9\u9875\u9762",
        ["Use \"/sell help overtime\" to see info on the \"/ot\" (overtime command)"] = "\u8f93\u5165 \"/sell help overtime\" \u67e5\u770b \"/ot\"\uff08\u52a0\u73ed\u5956\u52b1\u547d\u4ee4\uff09\u8bf4\u660e",
        ["The value must be positive"] = "\u6570\u503c\u5fc5\u987b\u4e3a\u6b63",
        ["Cannot find terminal!"] = "\u672a\u627e\u5230\u7ec8\u7aef\uff01",
        ["Cannot find terminal?!"] = "\u672a\u627e\u5230\u7ec8\u7aef\uff01",
        ["Quota is already fulfilled"] = "\u5229\u6da6\u6307\u6807\u5df2\u5b8c\u6210",
        ["No item is held and no item was specified"] = "\u672a\u6301\u6709\u7269\u54c1\uff0c\u4e5f\u672a\u6307\u5b9a\u7269\u54c1",
        ["No items on the desk"] = "\u67dc\u53f0\u4e0a\u6ca1\u6709\u7269\u54c1",
        ["Door already open"] = "\u95e8\u5df2\u7ecf\u6253\u5f00",
        ["Wrong item name"] = "\u7269\u54c1\u540d\u79f0\u9519\u8bef",
        ["No page with this name exists"] = "\u4e0d\u5b58\u5728\u8fd9\u4e2a\u5e2e\u52a9\u9875\u9762",
        ["Failed to evalute expression"] = "\u8868\u8fbe\u5f0f\u8ba1\u7b97\u5931\u8d25",
        ["No items to sort"] = "\u6ca1\u6709\u53ef\u6574\u7406\u7684\u7269\u54c1",
        ["No items on the ship"] = "\u98de\u8239\u4e0a\u6ca1\u6709\u7269\u54c1",
        ["Sorting all items..."] = "\u6b63\u5728\u6574\u7406\u6240\u6709\u7269\u54c1...",
        ["Finished sorting items"] = "\u7269\u54c1\u6574\u7406\u5b8c\u6210",
        ["Invalid arguments"] = "\u53c2\u6570\u65e0\u6548",
        ["Invalid item name"] = "\u7269\u54c1\u540d\u79f0\u65e0\u6548",
        ["Error running command"] = "\u8fd0\u884c\u547d\u4ee4\u65f6\u51fa\u9519",
        ["Error getting position"] = "\u83b7\u53d6\u4f4d\u7f6e\u65f6\u51fa\u9519",
        ["Automatic sorting failed"] = "\u81ea\u52a8\u6574\u7406\u5931\u8d25",
        ["Error while autosorting items"] = "\u81ea\u52a8\u6574\u7406\u7269\u54c1\u65f6\u51fa\u9519",
        ["Automatic sorting failed due to an internal error, check the log for details"] = "\u81ea\u52a8\u6574\u7406\u56e0\u5185\u90e8\u9519\u8bef\u5931\u8d25\uff0c\u8bf7\u67e5\u770b\u65e5\u5fd7",
        ["Couldn't find ship"] = "\u672a\u627e\u5230\u98de\u8239",
        ["Raycast unsuccessful"] = "\u5c04\u7ebf\u68c0\u6d4b\u672a\u6210\u529f",
        ["The ship must be in orbit"] = "\u98de\u8239\u5fc5\u987b\u5904\u4e8e\u8f68\u9053\u4e2d",
        ["You can't pick anything up while sorting items."] = "\u6574\u7406\u7269\u54c1\u65f6\u65e0\u6cd5\u62fe\u53d6\u4efb\u4f55\u7269\u54c1\u3002",
        ["Toggles automatic item sorting when leaving a planet"] = "\u5207\u6362\u79bb\u5f00\u661f\u7403\u65f6\u662f\u5426\u81ea\u52a8\u6574\u7406\u7269\u54c1",
        ["* The most paranoid employee."] = "* \u6700\u591a\u7591\u7684\u5458\u5de5",
        ["The most paranoid employee."] = "\u6700\u591a\u7591\u7684\u5458\u5de5",
        ["* Sustained the most injuries."] = "* \u53d7\u4f24\u6700\u591a",
        ["Sustained the most injuries."] = "\u53d7\u4f24\u6700\u591a",
        ["* Dislikes smoke."] = "* \u8ba8\u538c\u70df\u96fe",
        ["Dislikes smoke."] = "\u8ba8\u538c\u70df\u96fe",
        ["* The least likely to die next time."] = "* \u4e0b\u6b21\u6700\u4e0d\u53ef\u80fd\u6b7b\u4ea1",
        ["The least likely to die next time."] = "\u4e0b\u6b21\u6700\u4e0d\u53ef\u80fd\u6b7b\u4ea1",
        ["* I think this one's a serial killer."] = "* \u6211\u89c9\u5f97\u8fd9\u4eba\u50cf\u4e2a\u8fde\u73af\u6740\u624b",
        ["I think this one's a serial killer."] = "\u6211\u89c9\u5f97\u8fd9\u4eba\u50cf\u4e2a\u8fde\u73af\u6740\u624b",
        ["* Go! Freaky on a Friday night."] = "* \u5468\u4e94\u591c\u665a\u5c3d\u60c5\u75af\u72c2",
        ["Go! Freaky on a Friday night."] = "\u5468\u4e94\u591c\u665a\u5c3d\u60c5\u75af\u72c2"
    };

    private static readonly Dictionary<string, string> KeyTokenEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scroll Mouse"] = "\u6eda\u52a8\u9f20\u6807",
        ["keybind #"] = "\u6309\u952e\u7ed1\u5b9a #"
    };

    private static readonly string[] SellingNamedMarkers =
    {
        " item(s) named \"",
        " items named \"",
        " item named \""
    };

    private static readonly string[] SellingCountMarkers =
    {
        " item(s) with a total value of ",
        " items with a total value of ",
        " item with a total value of "
    };

    private static readonly Dictionary<string, string> ShipLootPlusWeatherEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = "\u65e0",
        ["Clear"] = "\u6674\u6717",
        ["DustClouds"] = "\u6c99\u5c18",
        ["Rainy"] = "\u591a\u96e8",
        ["Stormy"] = "\u66b4\u98ce\u96e8",
        ["Foggy"] = "\u96fe\u5929",
        ["Flooded"] = "\u6d2a\u6c34",
        ["Eclipsed"] = "\u65e5\u98df",
        ["Hell"] = "\u5730\u72f1"
    };

    private static readonly BoundedCache<string, bool> CanHandleCache = new(RuntimeCacheLimit, StringComparer.Ordinal);
    private static readonly BoundedCache<string, string?> TranslationCache = new(RuntimeCacheLimit, StringComparer.Ordinal);

    public static void ClearRuntimeCaches()
    {
        CanHandleCache.Clear();
        TranslationCache.Clear();
    }

    public static bool CanHandleCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (CanHandleCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var result = CanHandleCheapCore(source);
        CacheCanHandleResult(source, result);
        return result;
    }

    public static bool MightTranslateStatusLikeTextCheap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (ContainsLineBreak(source))
        {
            var start = 0;
            while (start <= source.Length)
            {
                var newline = source.IndexOf('\n', start);
                var end = newline < 0 ? source.Length : newline;
                var lineSpan = source.AsSpan(start, end - start);
                if (lineSpan.Length > 0 && lineSpan[^1] == '\r')
                {
                    lineSpan = lineSpan[..^1];
                }

                if (LineMightNeedExternalCompatibilityCheck(lineSpan) &&
                    MightTranslateStatusLikeSingleLineCheap(lineSpan.ToString()))
                {
                    return true;
                }

                if (newline < 0)
                {
                    break;
                }

                start = newline + 1;
            }

            return false;
        }

        return MightTranslateStatusLikeSingleLineCheap(source);
    }

    public static bool MightTranslateStatusLikeLabelCheap(string? label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > 64)
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(label)).Trim());
        if (content.EndsWith(":", StringComparison.Ordinal))
        {
            content = content[..^1].TrimEnd();
        }

        return ExactEntries.ContainsKey(content);
    }

    private static bool CanHandleCheapCore(string source)
    {
        if (LooksLikeLobbyControlNotification(source))
        {
            return true;
        }

        if (MightContainDeleteFilePrompt(source) &&
            LooksLikeDeleteFilePrompt(source))
        {
            return true;
        }

        if (MightContainDiscountAlertNoDiscountText(source) &&
            LooksLikeDiscountAlertNoDiscountText(source))
        {
            return true;
        }

        if (ContainsLineBreak(source))
        {
            return CanHandleAnyLineCheap(source);
        }

        if (ContainsCjk(source))
        {
            return false;
        }

        return CanHandleSingleLineCheap(source);
    }

    private static bool CanHandleAnyLineCheap(string source)
    {
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var lineSpan = source.AsSpan(start, end - start);
            if (lineSpan.Length > 0 && lineSpan[^1] == '\r')
            {
                lineSpan = lineSpan[..^1];
            }

            if (LineMightNeedExternalCompatibilityCheck(lineSpan) &&
                CanHandleSingleLineCheap(lineSpan.ToString()))
            {
                return true;
            }

            if (newline < 0)
            {
                break;
            }

            start = newline + 1;
        }

        return false;
    }

    private static bool LineMightNeedExternalCompatibilityCheck(ReadOnlySpan<char> line)
    {
        line = line.Trim();
        if (line.Length == 0 || line.Length > MaxSourceLength)
        {
            return false;
        }

        var hasAsciiLetter = false;
        foreach (var ch in line)
        {
            if (IsCjk(ch))
            {
                return false;
            }

            if (IsAsciiLetter(ch))
            {
                hasAsciiLetter = true;
            }
        }

        return hasAsciiLetter;
    }

    private static bool MightContainDeleteFilePrompt(string source) =>
        source.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0 &&
        source.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MightContainDiscountAlertNoDiscountText(string source) =>
        source.IndexOf("None", StringComparison.OrdinalIgnoreCase) >= 0 &&
        source.IndexOf("tomorrow", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MightContainDiscountLine(string source) =>
        source.IndexOf('$') >= 0 &&
        (source.IndexOf(" off!", StringComparison.OrdinalIgnoreCase) >= 0 ||
         source.IndexOf(" up!", StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool MightContainCompositeExternalUiShape(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsWhiteSpace(ch) || ch is ':' or '[' or ']' or '#' or '(' or ')' or '$' or '%' or '*' or '!' or '-')
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanHandleSingleLineCheap(string source)
    {
        if (TooManyEmotesCompatibilityTranslator.MightTranslateUiTextCheap(source) &&
            TooManyEmotesCompatibilityTranslator.TryTranslateUiText(source, out _))
        {
            return true;
        }

        var text = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(source)).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(text);
        if (ExactEntries.ContainsKey(content) ||
            LooksLikeShipLootPlusHudText(content) ||
            LooksLikeVersionedServerListLoadingText(content) ||
            LooksLikeChallengeLeaderboardHeader(content) ||
            LooksLikeAdvertisementSaleText(content) ||
            LooksLikeEladsHudMetricText(content) ||
            LooksLikeInfectionPercentageText(content) ||
            LooksLikeBracketedKnownExternalUiToken(content) ||
            LooksLikeDecoratedKnownExternalUiToken(content))
        {
            return true;
        }

        if (!MightContainCompositeExternalUiShape(content))
        {
            return false;
        }

        return LooksLikeKnownExternalUiLabel(content) ||
               LooksLikeSaveFileLabel(content) ||
               LooksLikeAdvancedFeaturesPlayerLabel(content) ||
               LooksLikeDeleteFilePrompt(content) ||
               text.IndexOf("Emote", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Admin UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Lobby ID", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Server Access", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("Steam Sessions", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("FRIENDS ONLY", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("INVITE ONLY", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf("discount", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf(" off!", StringComparison.OrdinalIgnoreCase) >= 0 ||
               text.IndexOf(" up!", StringComparison.OrdinalIgnoreCase) >= 0 ||
               LooksLikeChatCommandCompatibilityText(content);
    }

    private static bool MightTranslateStatusLikeSingleLineCheap(string source)
    {
        var text = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(source)).Trim();
        if (text.Length == 0 || ContainsCjk(text))
        {
            return false;
        }

        var content = StripMenuSelectionPrefix(text).Trim();
        return LooksLikeKnownExternalUiLabel(content) ||
               LooksLikeBracketedKnownExternalUiToken(content) ||
               LooksLikeDecoratedKnownExternalUiToken(content) ||
               LooksLikeChatCommandCompatibilityText(content);
    }

    public static bool TryTranslateFast(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxSourceLength)
        {
            return false;
        }

        if (TryGetCachedTranslation(source, out translated, out var hasTranslation))
        {
            return hasTranslation;
        }

        if (!CanHandleCheap(source))
        {
            CacheTranslationResult(source, null);
            return false;
        }

        if (TryTranslateLobbyControlNotification(source!, out translated) ||
            TryTranslateDiscountAlertNoDiscountText(source!, out translated) ||
            TryTranslateDeleteFilePrompt(source!, out translated))
        {
            CacheTranslationResult(source, translated);
            return true;
        }

        if (ContainsLineBreak(source!))
        {
            var changedLines = TryTranslateLines(source!, out translated);
            CacheTranslationResult(source, changedLines ? translated : null);
            return changedLines;
        }

        var changed = TryTranslateSingleLinePreservingWhitespace(source!, out translated);
        CacheTranslationResult(source, changed ? translated : null);
        return changed;
    }

    public static bool TryTranslateDisplayTipText(string? source, out string translated)
    {
        translated = source ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (source.Length <= MaxSourceLength)
        {
            return TryTranslateFast(source, out translated);
        }

        return ContainsLineBreak(source) &&
               TryTranslateDisplayTipLines(source, out translated);
    }

    public static bool MightContainDisplayTipCompatibilityText(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (source.Length <= MaxSourceLength)
        {
            return CanHandleCheap(source) ||
                   MightContainDiscountLine(source);
        }

        return MightContainDiscountLine(source);
    }

    private static bool LooksLikeLobbyControlNotification(string source)
    {
        var trimmed = source.Trim();
        return (trimmed.StartsWith("Client ", StringComparison.OrdinalIgnoreCase) &&
                trimmed.EndsWith(" requested a connection but queue was full!", StringComparison.OrdinalIgnoreCase)) ||
               (trimmed.StartsWith("Player ", StringComparison.OrdinalIgnoreCase) &&
                (trimmed.EndsWith(" requested a connection", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.EndsWith("\n has been disconnected", StringComparison.OrdinalIgnoreCase))) ||
               (trimmed.StartsWith("Lobby took ", StringComparison.OrdinalIgnoreCase) &&
                trimmed.IndexOf("connectionTimeout is only ", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool TryTranslateLobbyControlNotification(string source, out string translated)
    {
        translated = source;
        var trimmed = source.Trim();

        const string clientPrefix = "Client ";
        const string queueFullSuffix = " requested a connection but queue was full!";
        if (trimmed.StartsWith(clientPrefix, StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(queueFullSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var client = trimmed.Substring(clientPrefix.Length, trimmed.Length - clientPrefix.Length - queueFullSuffix.Length).Trim();
            if (client.Length > 0)
            {
                translated = $"客户端 {client} 请求连接，但加入队列已满！";
                return true;
            }
        }

        const string playerPrefix = "Player ";
        const string requestSuffix = " requested a connection";
        if (trimmed.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(requestSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var player = trimmed.Substring(playerPrefix.Length, trimmed.Length - playerPrefix.Length - requestSuffix.Length).Trim();
            if (player.Length > 0)
            {
                translated = $"玩家 {player} 请求连接";
                return true;
            }
        }

        const string disconnectedSuffix = "\n has been disconnected";
        if (trimmed.StartsWith(playerPrefix, StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(disconnectedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var player = trimmed.Substring(playerPrefix.Length, trimmed.Length - playerPrefix.Length - disconnectedSuffix.Length).Trim();
            if (player.Length > 0)
            {
                translated = $"玩家 {player}\n已断开连接";
                return true;
            }
        }

        const string lobbyPrefix = "Lobby took ";
        const string lobbySeparator = "ms to load but the configured connectionTimeout is only ";
        const string lobbySuffix = "ms";
        if (trimmed.StartsWith(lobbyPrefix, StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(lobbySuffix, StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = trimmed.IndexOf(lobbySeparator, lobbyPrefix.Length, StringComparison.OrdinalIgnoreCase);
            if (separatorIndex > lobbyPrefix.Length)
            {
                var elapsed = trimmed.Substring(lobbyPrefix.Length, separatorIndex - lobbyPrefix.Length).Trim();
                var timeoutStart = separatorIndex + lobbySeparator.Length;
                var timeout = trimmed.Substring(timeoutStart, trimmed.Length - timeoutStart - lobbySuffix.Length).Trim();
                if (elapsed.Length > 0 && timeout.Length > 0)
                {
                    translated = $"房间加载耗时 {elapsed} 毫秒，但当前连接超时仅设置为 {timeout} 毫秒";
                    return true;
                }
            }
        }

        return false;
    }

    private static void CacheCanHandleResult(string source, bool result)
    {
        if (!result && LooksLikeVolatileNegativeCacheSource(source))
        {
            return;
        }

        CanHandleCache.Set(source, result, RuntimePerformanceSettings.ExternalCompatibilityCacheLimit);
    }

    private static bool TryGetCachedTranslation(string source, out string translated, out bool hasTranslation)
    {
        if (!TranslationCache.TryGetValue(source, out var cached))
        {
            translated = source;
            hasTranslation = false;
            return false;
        }

        hasTranslation = cached != null;
        translated = cached ?? source;
        return true;
    }

    private static void CacheTranslationResult(string source, string? translated)
    {
        if (translated == null && LooksLikeVolatileNegativeCacheSource(source))
        {
            return;
        }

        TranslationCache.Set(source, translated, RuntimePerformanceSettings.ExternalCompatibilityCacheLimit);
    }

    private static bool LooksLikeVolatileNegativeCacheSource(string source)
    {
        if (source.Length > 128)
        {
            return true;
        }

        foreach (var ch in source)
        {
            if (char.IsDigit(ch) ||
                ch is '\r' or '\n' or ':' or '[' or ']' or '(' or ')' or '<' or '>' or '$' or '%' or '#' or '/' or '\\')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateLines(string source, out string translated)
    {
        var changed = false;
        var builder = new StringBuilder(source.Length + 16);
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var line = source.Substring(start, end - start);
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (TryTranslateSingleLinePreservingWhitespace(content, out var rewrittenLine))
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

            if (newline < 0)
            {
                break;
            }

            builder.Append('\n');
            start = newline + 1;
        }

        translated = changed ? builder.ToString() : source;
        return changed;
    }

    private static bool TryTranslateDisplayTipLines(string source, out string translated)
    {
        var changed = false;
        var builder = new StringBuilder(source.Length + 16);
        var start = 0;
        while (start <= source.Length)
        {
            var newline = source.IndexOf('\n', start);
            var end = newline < 0 ? source.Length : newline;
            var line = source.Substring(start, end - start);
            var hasCarriageReturn = line.EndsWith("\r", StringComparison.Ordinal);
            var content = hasCarriageReturn ? line[..^1] : line;
            if (content.Length <= MaxSourceLength &&
                LineMightNeedExternalCompatibilityCheck(content.AsSpan()) &&
                TryTranslateSingleLinePreservingWhitespace(content, out var rewrittenLine))
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

            if (newline < 0)
            {
                break;
            }

            builder.Append('\n');
            start = newline + 1;
        }

        translated = changed ? builder.ToString() : source;
        return changed;
    }

    private static bool TryTranslateSingleLinePreservingWhitespace(string source, out string translated)
    {
        translated = source;
        var leadingLength = source.Length - source.TrimStart().Length;
        var trailingLength = source.Length - source.TrimEnd().Length;
        var coreLength = source.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var leading = leadingLength > 0 ? source[..leadingLength] : string.Empty;
        var trailing = trailingLength > 0 ? source[^trailingLength..] : string.Empty;
        var core = source.Substring(leadingLength, coreLength);
        if (!TryTranslateSingleLineCore(core, out var rewrittenCore))
        {
            return false;
        }

        translated = leading + rewrittenCore + trailing;
        return true;
    }

    private static bool TryTranslateSingleLineCore(string source, out string translated)
    {
        translated = source;
        if (TooManyEmotesCompatibilityTranslator.TryTranslateUiText(source, out translated))
        {
            return true;
        }

        var text = source.Trim();
        var richPrefix = string.Empty;
        var richSuffix = string.Empty;
        while (TryExtractOuterSimpleRichTextEnvelope(text, out var envelopePrefix, out var inner, out var envelopeSuffix))
        {
            richPrefix += envelopePrefix;
            richSuffix = envelopeSuffix + richSuffix;
            text = inner.Trim();
        }

        richPrefix += ExtractLeadingSimpleRichTextPrefix(ref text);
        var menuPrefix = string.Empty;
        if (text.StartsWith(">", StringComparison.Ordinal))
        {
            menuPrefix = "> ";
            text = StripMenuSelectionPrefix(text);
        }

        if (LooksLikeNonUiName(text) && !LooksLikeEladsHudConfigToken(text))
        {
            return false;
        }

        if (TryTranslateBracketedCommand(text, out translated) ||
            TryTranslateShipLootPlusHudText(text, out translated) ||
            TryTranslateVersionedServerListLoadingText(text, out translated) ||
            TryTranslateChallengeLeaderboardHeader(text, out translated) ||
            TryTranslateAdvertisementSaleText(text, out translated) ||
            TryTranslateEladsHudMetricText(text, out translated) ||
            TryTranslateInfectionPercentageText(text, out translated) ||
            TryTranslateDecoratedExactUiText(text, out translated) ||
            TryTranslateControlTip(text, out translated) ||
            TryTranslateLabelValue(text, out translated) ||
            TryTranslateDiscountLine(text, out translated) ||
            TryTranslateSaveFileLabel(text, out translated) ||
            TryTranslateAdvancedFeaturesPlayerLabel(text, out translated) ||
            TryTranslateDeleteFilePrompt(text, out translated) ||
            TryTranslateChatCommandCompatibilityText(text, out translated) ||
            TryTranslateExactUiText(text, out translated))
        {
            translated = menuPrefix + richPrefix + translated + richSuffix;
            return true;
        }

        return false;
    }

    private static bool LooksLikeShipLootPlusHudText(string text)
    {
        return (text.StartsWith("Ship: $", StringComparison.OrdinalIgnoreCase) &&
                text.IndexOf(" / $", StringComparison.Ordinal) > "Ship: ".Length) ||
               (text.StartsWith("Quota: $", StringComparison.OrdinalIgnoreCase) &&
                text.IndexOf(" - Profit: $", StringComparison.OrdinalIgnoreCase) > "Quota: ".Length) ||
               (text.StartsWith("Deadline: ", StringComparison.OrdinalIgnoreCase) &&
                text.IndexOf(" - ", StringComparison.Ordinal) > "Deadline: ".Length);
    }

    private static bool TryTranslateShipLootPlusHudText(string text, out string translated)
    {
        translated = text;

        const string shipPrefix = "Ship: ";
        const string shipSeparator = " / ";
        if (text.StartsWith(shipPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = text.IndexOf(shipSeparator, shipPrefix.Length, StringComparison.Ordinal);
            if (separatorIndex > shipPrefix.Length)
            {
                var shipValue = text.Substring(shipPrefix.Length, separatorIndex - shipPrefix.Length);
                var moonValue = text.Substring(separatorIndex + shipSeparator.Length);
                if (shipValue.StartsWith("$", StringComparison.Ordinal) &&
                    moonValue.StartsWith("$", StringComparison.Ordinal))
                {
                    translated = $"\u98de\u8239\uff1a{shipValue} / \u6708\u7403\uff1a{TranslateShipLootPlusWeather(moonValue)}";
                    return true;
                }
            }
        }

        const string quotaPrefix = "Quota: ";
        const string quotaSeparator = " - Profit: ";
        if (text.StartsWith(quotaPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = text.IndexOf(quotaSeparator, quotaPrefix.Length, StringComparison.OrdinalIgnoreCase);
            if (separatorIndex > quotaPrefix.Length)
            {
                var quotaValue = text.Substring(quotaPrefix.Length, separatorIndex - quotaPrefix.Length);
                var profitValue = text.Substring(separatorIndex + quotaSeparator.Length);
                if (quotaValue.StartsWith("$", StringComparison.Ordinal) &&
                    profitValue.StartsWith("$", StringComparison.Ordinal))
                {
                    translated = $"\u914d\u989d\uff1a{quotaValue} / \u5229\u6da6\uff1a{profitValue}";
                    return true;
                }
            }
        }

        const string deadlinePrefix = "Deadline: ";
        const string deadlineSeparator = " - ";
        if (text.StartsWith(deadlinePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = text.IndexOf(deadlineSeparator, deadlinePrefix.Length, StringComparison.Ordinal);
            if (separatorIndex > deadlinePrefix.Length)
            {
                var deadlineValue = text.Substring(deadlinePrefix.Length, separatorIndex - deadlinePrefix.Length).Trim();
                var dayValue = text.Substring(separatorIndex + deadlineSeparator.Length).Trim();
                if (deadlineValue.Length > 0 && TryParseShipLootPlusOrdinalDay(dayValue, out var dayNumber))
                {
                    translated = $"\u671f\u9650\uff1a{deadlineValue} - \u7b2c {dayNumber} \u5929";
                    return true;
                }
            }
        }

        return false;
    }

    private static string TranslateShipLootPlusWeather(string source)
    {
        var closeBracket = source.LastIndexOf(']');
        if (closeBracket < 0)
        {
            return source;
        }

        var openBracket = source.LastIndexOf('[', closeBracket);
        var separator = source.LastIndexOf(':', closeBracket);
        if (openBracket < 0 || separator <= openBracket || separator >= closeBracket)
        {
            return source;
        }

        var weather = source.Substring(separator + 1, closeBracket - separator - 1).Trim();
        if (!ShipLootPlusWeatherEntries.TryGetValue(weather, out var localizedWeather))
        {
            return source;
        }

        return source.Substring(0, separator + 1) + localizedWeather + source.Substring(closeBracket);
    }

    private static bool TryParseShipLootPlusOrdinalDay(string source, out string dayNumber)
    {
        dayNumber = string.Empty;
        const string daySuffix = " day";
        if (!source.EndsWith(daySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var ordinal = source[..^daySuffix.Length].Trim();
        if (ordinal.EndsWith("st", StringComparison.OrdinalIgnoreCase) ||
            ordinal.EndsWith("nd", StringComparison.OrdinalIgnoreCase) ||
            ordinal.EndsWith("rd", StringComparison.OrdinalIgnoreCase) ||
            ordinal.EndsWith("th", StringComparison.OrdinalIgnoreCase))
        {
            ordinal = ordinal[..^2];
        }

        if (ordinal.Length == 0)
        {
            return false;
        }

        foreach (var ch in ordinal)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        dayNumber = ordinal;
        return true;
    }

    private static bool LooksLikeVersionedServerListLoadingText(string text)
    {
        const string Prefix = "Loading ";
        const string Suffix = " server list...";
        return text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
               text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) &&
               text.Length > Prefix.Length + Suffix.Length;
    }

    private static bool LooksLikeChallengeLeaderboardHeader(string text)
    {
        const string Prefix = "Challenge Moon ";
        const string Suffix = " Results";
        return text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
               text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) &&
               text.Length > Prefix.Length + Suffix.Length;
    }

    private static bool TryTranslateChallengeLeaderboardHeader(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeChallengeLeaderboardHeader(text))
        {
            return false;
        }

        const string Prefix = "Challenge Moon ";
        const string Suffix = " Results";
        var moon = text.Substring(Prefix.Length, text.Length - Prefix.Length - Suffix.Length).Trim();
        if (moon.Length == 0)
        {
            return false;
        }

        translated = $"\u6311\u6218\u536b\u661f {moon} \u7ed3\u679c";
        return true;
    }

    private static bool TryTranslateVersionedServerListLoadingText(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeVersionedServerListLoadingText(text))
        {
            return false;
        }

        const string Prefix = "Loading ";
        const string Suffix = " server list...";
        var label = text.Substring(Prefix.Length, text.Length - Prefix.Length - Suffix.Length).Trim();
        if (label.Length == 0)
        {
            return false;
        }

        translated = $"\u6b63\u5728\u52a0\u8f7d {label} \u670d\u52a1\u5668\u5217\u8868...";
        return true;
    }

    private static bool LooksLikeEladsHudConfigToken(string text)
    {
        return text.Equals("HUDScale", StringComparison.Ordinal) ||
               text.Equals("HideHealthbarAutomatically", StringComparison.Ordinal) ||
               text.Equals("HealthbarHideDelay", StringComparison.Ordinal) ||
               text.Equals("FlashlightBattery", StringComparison.Ordinal) ||
               text.Equals("DetailedStamina", StringComparison.Ordinal) ||
               text.Equals("DisplayTimeLeft", StringComparison.Ordinal) ||
               text.Equals("HidePlanetInfo", StringComparison.Ordinal) ||
               text.Equals("PercentageOnly", StringComparison.Ordinal);
    }

    private static bool LooksLikeAdvertisementSaleText(string text)
    {
        text = text.Trim();
        if (!text.EndsWith("% OFF!", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var percent = text[..^"% OFF!".Length].Trim();
        return LooksLikeSimpleNumber(percent);
    }

    private static bool TryTranslateAdvertisementSaleText(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeAdvertisementSaleText(text))
        {
            return false;
        }

        var percent = text.Trim()[..^"% OFF!".Length].Trim();
        translated = $"\u4f18\u60e0 {percent}%\uff01";
        return true;
    }

    private static bool LooksLikeInfectionPercentageText(string text)
    {
        text = text.Trim();
        const string Prefix = "Infection ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !text.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        var percent = text.Substring(Prefix.Length, text.Length - Prefix.Length - 1).Trim();
        return LooksLikeSimpleNumber(percent);
    }

    private static bool TryTranslateInfectionPercentageText(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeInfectionPercentageText(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        const string Prefix = "Infection ";
        var percent = trimmed.Substring(Prefix.Length, trimmed.Length - Prefix.Length - 1).Trim();
        translated = $"\u611f\u67d3 {percent}%";
        return true;
    }

    private static bool LooksLikeEladsHudMetricText(string text)
    {
        text = text.Trim();
        return text.EndsWith("/sec</size>", StringComparison.OrdinalIgnoreCase) ||
               LooksLikePercentageUsesRemainingText(text) ||
               LooksLikePercentageTimeRemainingText(text) ||
               LooksLikeUsesRemainingText(text) ||
               LooksLikeTimeRemainingText(text);
    }

    private static bool TryTranslateEladsHudMetricText(string text, out string translated)
    {
        translated = text;
        var trimmed = text.Trim();
        const string RateSuffix = "/sec</size>";
        if (trimmed.EndsWith(RateSuffix, StringComparison.OrdinalIgnoreCase))
        {
            translated = trimmed[..^RateSuffix.Length] + "/\u79d2</size>";
            return true;
        }

        if (TryExtractPercentageTimeRemaining(trimmed, out var percent, out var remainingTime))
        {
            translated = $"{percent}%\uff08\u5269\u4f59 {remainingTime}\uff09";
            return true;
        }

        if (TryExtractPercentageUsesRemaining(trimmed, out percent, out var remainingUses))
        {
            translated = $"{percent}%\uff08\u5269\u4f59 {remainingUses} \u6b21\uff09";
            return true;
        }

        if (LooksLikeUsesRemainingText(trimmed))
        {
            const string Suffix = " uses remaining)";
            var count = trimmed.Substring(1, trimmed.Length - 1 - Suffix.Length).Trim();
            translated = $"\uff08\u5269\u4f59 {count} \u6b21\uff09";
            return true;
        }

        if (LooksLikeTimeRemainingText(trimmed))
        {
            const string Suffix = " remaining)";
            var time = trimmed.Substring(1, trimmed.Length - 1 - Suffix.Length).Trim();
            translated = $"\uff08\u5269\u4f59 {time}\uff09";
            return true;
        }

        return false;
    }

    private static bool LooksLikePercentageTimeRemainingText(string text)
    {
        return TryExtractPercentageTimeRemaining(text, out _, out _);
    }

    private static bool LooksLikePercentageUsesRemainingText(string text)
    {
        return TryExtractPercentageUsesRemaining(text, out _, out _);
    }

    private static bool TryExtractPercentageUsesRemaining(string text, out string percent, out string remainingUses)
    {
        percent = string.Empty;
        remainingUses = string.Empty;
        var trimmed = text.Trim();
        var percentIndex = trimmed.IndexOf('%');
        if (percentIndex <= 0 || percentIndex != trimmed.LastIndexOf('%'))
        {
            return false;
        }

        percent = trimmed[..percentIndex].Trim();
        if (!LooksLikeSimpleNumber(percent))
        {
            return false;
        }

        var suffix = trimmed[(percentIndex + 1)..].TrimStart();
        const string UsesSuffix = " uses remaining)";
        if (!suffix.StartsWith("(", StringComparison.Ordinal) ||
            !suffix.EndsWith(UsesSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        remainingUses = suffix.Substring(1, suffix.Length - 1 - UsesSuffix.Length).Trim();
        return LooksLikeSimpleNumber(remainingUses);
    }

    private static bool TryExtractPercentageTimeRemaining(string text, out string percent, out string remainingTime)
    {
        percent = string.Empty;
        remainingTime = string.Empty;
        var trimmed = text.Trim();
        var percentIndex = trimmed.IndexOf('%');
        if (percentIndex <= 0 || percentIndex != trimmed.LastIndexOf('%'))
        {
            return false;
        }

        percent = trimmed[..percentIndex].Trim();
        if (!LooksLikeSimpleNumber(percent))
        {
            return false;
        }

        var suffix = trimmed[(percentIndex + 1)..].TrimStart();
        const string RemainingSuffix = " remaining)";
        if (!suffix.StartsWith("(", StringComparison.Ordinal) ||
            !suffix.EndsWith(RemainingSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        remainingTime = suffix.Substring(1, suffix.Length - 1 - RemainingSuffix.Length).Trim();
        var colon = remainingTime.IndexOf(':');
        return colon > 0 &&
               colon == remainingTime.LastIndexOf(':') &&
               LooksLikeSimpleNumber(remainingTime[..colon]) &&
               LooksLikeSimpleNumber(remainingTime[(colon + 1)..]);
    }

    private static bool LooksLikeUsesRemainingText(string text)
    {
        const string Suffix = " uses remaining)";
        if (!text.StartsWith("(", StringComparison.Ordinal) ||
            !text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var count = text.Substring(1, text.Length - 1 - Suffix.Length).Trim();
        return LooksLikeSimpleNumber(count);
    }

    private static bool LooksLikeTimeRemainingText(string text)
    {
        const string Suffix = " remaining)";
        if (!text.StartsWith("(", StringComparison.Ordinal) ||
            !text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var time = text.Substring(1, text.Length - 1 - Suffix.Length).Trim();
        var colon = time.IndexOf(':');
        return colon > 0 &&
               colon == time.LastIndexOf(':') &&
               LooksLikeSimpleNumber(time[..colon]) &&
               LooksLikeSimpleNumber(time[(colon + 1)..]);
    }

    private static bool TryTranslateSaveFileLabel(string text, out string translated)
    {
        translated = text;
        var content = StripMenuSelectionPrefix(text).Trim();
        if (!LooksLikeSaveFileLabel(content))
        {
            return false;
        }

        translated = content["File ".Length..].TrimStart() + " \u53f7\u5b58\u6863";
        return true;
    }

    private static bool TryTranslateDeleteFilePrompt(string text, out string translated)
    {
        translated = text;
        if (TryExtractDeleteFileAlias(text, out var alias))
        {
            translated = "\u8981\u5220\u9664 " + alias + " \u5417";
            return true;
        }

        if (!TryExtractDeleteFileNumber(text, out var fileNumber))
        {
            return false;
        }

        translated = "\u8981\u5220\u9664 " + fileNumber + " \u53f7\u5b58\u6863\u5417";
        return true;
    }

    private static bool TryTranslateChatCommandCompatibilityText(string text, out string translated)
    {
        translated = text;
        var value = text.Trim();
        return TryTranslateCommandItemLabel(value, "Item to sell:", "\u8981\u552e\u5356\u7684\u7269\u54c1", out translated) ||
               TryTranslateCommandItemLabel(value, "Items with priority:", "\u4f18\u5148\u552e\u5356\u7269\u54c1", out translated) ||
               TryTranslateNoItemsCalled(value, out translated) ||
               TryTranslateThereIsScrapOnShip(value, out translated) ||
               TryTranslateItemsCouldNotBeSorted(value, out translated) ||
               TryTranslateItemsOfTypePosition(value, out translated) ||
               TryTranslateMovingAllItemsOfType(value, out translated) ||
               TryTranslateSellingItemsSummary(value, out translated) ||
               TryTranslateSoldRequestedSummary(value, out translated) ||
               TryTranslateCommandValueLabel(value, "Overtime:", "\u52a0\u73ed\u5956\u52b1", out translated) ||
               TryTranslateCommandValueLabel(value, "Money after takeoff:", "\u8d77\u98de\u540e\u8d44\u91d1", out translated) ||
               TryTranslateMoneyInTerminal(value, out translated);
    }

    private static bool LooksLikeChatCommandCompatibilityText(string text)
    {
        var value = text.Trim();
        return value.StartsWith("Item to sell:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Items with priority:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("No items called \"", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("There is ", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Items of type ", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Moving all items of type ", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Selling ", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Overtime:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Money after takeoff:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("Money in terminal", StringComparison.OrdinalIgnoreCase) ||
               value.IndexOf(" sold / ", StringComparison.OrdinalIgnoreCase) > 0 ||
               value.EndsWith(" items couldn't be sorted", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryTranslateCommandItemLabel(string text, string prefix, string localizedLabel, out string translated)
    {
        translated = text;
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var item = text[prefix.Length..].Trim();
        if (item.Length == 0)
        {
            return false;
        }

        translated = localizedLabel + "\uff1a" + LocalizeCommandItemName(item);
        return true;
    }

    private static bool TryTranslateNoItemsCalled(string text, out string translated)
    {
        translated = text;
        const string Prefix = "No items called \"";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var endQuote = text.IndexOf('"', Prefix.Length);
        if (endQuote <= Prefix.Length)
        {
            return false;
        }

        var item = text.Substring(Prefix.Length, endQuote - Prefix.Length).Trim();
        var trailing = text[(endQuote + 1)..].Trim();
        if (item.Length == 0 ||
            (trailing.Length > 0 && !string.Equals(trailing, "were detected", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        translated = "\u672a\u627e\u5230\u540d\u4e3a \"" + LocalizeCommandItemName(item) + "\" \u7684\u7269\u54c1";
        return true;
    }

    private static bool TryTranslateThereIsScrapOnShip(string text, out string translated)
    {
        translated = text;
        const string Prefix = "There is ";
        const string Suffix = " scrap on ship";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var count = text.Substring(Prefix.Length, text.Length - Prefix.Length - Suffix.Length).Trim();
        if (!LooksLikeSimpleNumber(count))
        {
            return false;
        }

        translated = "\u98de\u8239\u4e0a\u6709 " + count + " \u4ef6\u5e9f\u6599";
        return true;
    }

    private static bool TryTranslateItemsCouldNotBeSorted(string text, out string translated)
    {
        translated = text;
        const string Suffix = " items couldn't be sorted";
        if (!text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var count = text[..^Suffix.Length].Trim();
        if (!LooksLikeSimpleNumber(count))
        {
            return false;
        }

        translated = count + " \u4ef6\u7269\u54c1\u65e0\u6cd5\u6574\u7406";
        return true;
    }

    private static bool TryTranslateItemsOfTypePosition(string text, out string translated)
    {
        translated = text;
        const string Prefix = "Items of type ";
        const string Marker = " will be put on position ";
        const string ThisGameSuffix = " for this game";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var marker = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (marker <= Prefix.Length)
        {
            return false;
        }

        var item = text.Substring(Prefix.Length, marker - Prefix.Length).Trim();
        var position = text[(marker + Marker.Length)..].Trim();
        var thisGame = false;
        if (position.EndsWith(ThisGameSuffix, StringComparison.OrdinalIgnoreCase))
        {
            thisGame = true;
            position = position[..^ThisGameSuffix.Length].TrimEnd();
        }

        if (item.Length == 0 || position.Length == 0)
        {
            return false;
        }

        var localizedItem = LocalizeCommandItemName(item);
        translated = thisGame
            ? "\u672c\u5c40\u5185\uff0c\u7c7b\u578b\u4e3a " + localizedItem + " \u7684\u7269\u54c1\u5c06\u653e\u5230 " + position + " \u4f4d\u7f6e"
            : "\u7c7b\u578b\u4e3a " + localizedItem + " \u7684\u7269\u54c1\u5c06\u653e\u5230 " + position + " \u4f4d\u7f6e";
        return true;
    }

    private static bool TryTranslateMovingAllItemsOfType(string text, out string translated)
    {
        translated = text;
        const string Prefix = "Moving all items of type ";
        const string Marker = " to position ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var marker = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (marker <= Prefix.Length)
        {
            return false;
        }

        var item = text.Substring(Prefix.Length, marker - Prefix.Length).Trim();
        var position = text[(marker + Marker.Length)..].Trim();
        if (item.Length == 0 || position.Length == 0)
        {
            return false;
        }

        translated = "\u6b63\u5728\u5c06\u6240\u6709 " + LocalizeCommandItemName(item) + " \u7c7b\u7269\u54c1\u79fb\u52a8\u5230 " + position + " \u4f4d\u7f6e";
        return true;
    }

    private static bool TryTranslateSellingItemsSummary(string text, out string translated)
    {
        translated = text;
        const string Prefix = "Selling ";
        const string NamedValueMarker = "\" with a total value of ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var namedMarker = FindFirstMarker(
            text,
            Prefix.Length,
            SellingNamedMarkers,
            out var namedMarkerText);
        if (namedMarker > Prefix.Length)
        {
            var count = text.Substring(Prefix.Length, namedMarker - Prefix.Length).Trim();
            var itemStart = namedMarker + namedMarkerText.Length;
            var valueMarker = text.IndexOf(NamedValueMarker, itemStart, StringComparison.OrdinalIgnoreCase);
            if (!LooksLikeSimpleNumber(count) || valueMarker <= itemStart)
            {
                return false;
            }

            var item = text.Substring(itemStart, valueMarker - itemStart).Trim();
            var value = text[(valueMarker + NamedValueMarker.Length)..].Trim();
            if (item.Length == 0 || value.Length == 0)
            {
                return false;
            }

            translated = "\u6b63\u5728\u51fa\u552e " + count + " \u4ef6\u540d\u4e3a \"" + LocalizeCommandItemName(item) + "\" \u7684\u7269\u54c1\uff0c\u603b\u4ef7\u503c " + LocalizeCommandResultValueTail(value);
            return true;
        }

        var marker = FindFirstMarker(
            text,
            Prefix.Length,
            SellingCountMarkers,
            out var countMarkerText);
        if (marker <= Prefix.Length)
        {
            return false;
        }

        var soldCount = text.Substring(Prefix.Length, marker - Prefix.Length).Trim();
        var totalValue = text[(marker + countMarkerText.Length)..].Trim();
        if (!LooksLikeSimpleNumber(soldCount) || totalValue.Length == 0)
        {
            return false;
        }

        translated = "\u6b63\u5728\u51fa\u552e " + soldCount + " \u4ef6\u7269\u54c1\uff0c\u603b\u4ef7\u503c " + LocalizeCommandResultValueTail(totalValue);
        return true;
    }

    private static int FindFirstMarker(string text, int startIndex, string[] markers, out string markerText)
    {
        var bestIndex = -1;
        markerText = string.Empty;
        foreach (var marker in markers)
        {
            var index = text.IndexOf(marker, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || (bestIndex >= 0 && index >= bestIndex))
            {
                continue;
            }

            bestIndex = index;
            markerText = marker;
        }

        return bestIndex;
    }

    private static string LocalizeCommandResultValueTail(string value)
    {
        var result = value
            .Replace(" overtime", " \u52a0\u73ed\u5956\u52b1", StringComparison.OrdinalIgnoreCase)
            .Replace(", sold every unblacklisted item", "\uff0c\u5df2\u51fa\u552e\u6240\u6709\u672a\u5217\u5165\u9ed1\u540d\u5355\u7684\u7269\u54c1", StringComparison.OrdinalIgnoreCase);
        return TryTranslateSoldRequestedResultTail(result, out var localized)
            ? localized
            : result;
    }

    private static bool TryTranslateSoldRequestedSummary(string text, out string translated)
    {
        return TryTranslateSoldRequestedSummaryCore(text, out translated);
    }

    private static bool TryTranslateSoldRequestedResultTail(string text, out string translated)
    {
        translated = text;
        if (TryTranslateSoldRequestedSummaryCore(text, out translated))
        {
            return true;
        }

        var colon = text.LastIndexOf(':');
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var prefix = text[..colon].TrimEnd();
        var tail = text[(colon + 1)..].TrimStart();
        if (prefix.Length == 0 || !TryTranslateSoldRequestedSummaryCore(tail, out var localizedTail))
        {
            return false;
        }

        translated = prefix + "\uff1a" + localizedTail;
        return true;
    }

    private static bool TryTranslateSoldRequestedSummaryCore(string text, out string translated)
    {
        translated = text;
        const string Marker = " sold / ";
        const string Suffix = " requested";
        text = text.Trim();
        var marker = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (marker <= 0 || !text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sold = text[..marker].Trim();
        var requested = text.Substring(marker + Marker.Length, text.Length - marker - Marker.Length - Suffix.Length).Trim();
        if (!LooksLikeSimpleNumber(sold) || !LooksLikeSimpleNumber(requested))
        {
            return false;
        }

        translated = "\u5df2\u51fa\u552e " + sold + " / \u76ee\u6807 " + requested;
        return true;
    }

    private static bool TryTranslateCommandValueLabel(string text, string prefix, string localizedLabel, out string translated)
    {
        translated = text;
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = text[prefix.Length..].Trim();
        if (value.Length == 0)
        {
            return false;
        }

        translated = localizedLabel + "\uff1a" + value;
        return true;
    }

    private static bool TryTranslateMoneyInTerminal(string text, out string translated)
    {
        translated = text;
        const string Prefix = "Money in terminal";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var label = text[..colon];
        var value = text[(colon + 1)..].Trim();
        if (value.Length == 0)
        {
            return false;
        }

        translated = label.IndexOf("desk", StringComparison.OrdinalIgnoreCase) >= 0
            ? "\u7ec8\u7aef\u8d44\u91d1\uff08\u542b\u67dc\u53f0\uff09\uff1a" + value
            : "\u7ec8\u7aef\u8d44\u91d1\uff1a" + value;
        return true;
    }

    private static string LocalizeCommandItemName(string item)
    {
        var trimmed = item.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var localized = TranslationService.BuildTerminalLocalizedItemName(trimmed);
        return string.IsNullOrWhiteSpace(localized) ? trimmed : localized;
    }

    private static bool LooksLikeSimpleNumber(string value)
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

    private static bool TryTranslateDiscountAlertNoDiscountText(string text, out string translated)
    {
        translated = text;
        var leadingLength = text.Length - text.TrimStart().Length;
        var trailingLength = text.Length - text.TrimEnd().Length;
        var coreLength = text.Length - leadingLength - trailingLength;
        if (coreLength <= 0)
        {
            return false;
        }

        var leading = leadingLength > 0 ? text[..leadingLength] : string.Empty;
        var trailing = trailingLength > 0 ? text[^trailingLength..] : string.Empty;
        var core = text.Substring(leadingLength, coreLength);
        var richPrefix = string.Empty;
        var richSuffix = string.Empty;
        while (TryExtractOuterSimpleRichTextEnvelope(core.Trim(), out var envelopePrefix, out var inner, out var envelopeSuffix))
        {
            richPrefix += envelopePrefix;
            richSuffix = envelopeSuffix + richSuffix;
            core = inner;
        }

        if (!LooksLikeDiscountAlertNoDiscountCore(core))
        {
            return false;
        }

        translated = leading + richPrefix + DiscountAlertNoDiscountLocalizedText + richSuffix + trailing;
        return true;
    }

    private static bool LooksLikeDiscountAlertNoDiscountText(string text)
    {
        var core = StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text));
        return LooksLikeDiscountAlertNoDiscountCore(core);
    }

    private static bool LooksLikeDiscountAlertNoDiscountCore(string text)
    {
        var normalized = NormalizeAsciiWhitespace(
            text.Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n'));
        return normalized.Equals("None :( Check back tomorrow!", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryTranslateBetterSavesDeleteFilePrompt(string? source, int fileToDelete, out string translated)
    {
        translated = source ?? string.Empty;
        if (fileToDelete <= 0 || string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (TryExtractDeleteFileAlias(source, out var alias))
        {
            translated = "\u8981\u5220\u9664 " + alias + " \u5417";
            return true;
        }

        if (!TryExtractDeleteFileNumber(source, out _) &&
            !LooksLikeLocalizedDeleteFileNumberPrompt(source))
        {
            return false;
        }

        translated = "\u8981\u5220\u9664 " + fileToDelete.ToString(System.Globalization.CultureInfo.InvariantCulture) + " \u53f7\u5b58\u6863\u5417";
        return true;
    }

    private static bool TryTranslateExactUiText(string text, out string translated)
    {
        translated = text;
        if (ExactEntries.TryGetValue(text, out translated))
        {
            return true;
        }

        if (text.EndsWith(":", StringComparison.Ordinal))
        {
            var label = text[..^1].TrimEnd();
            if (ExactEntries.TryGetValue(label, out var localizedLabel))
            {
                translated = localizedLabel + "\uff1a";
                return true;
            }
        }

        if (string.Equals(text, "FRIENDS ONLY means only friends or invited people can join.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4ec5\u9650\u597d\u53cb\uff1a\u597d\u53cb\u6216\u53d7\u9080\u73a9\u5bb6\u53ef\u4ee5\u52a0\u5165";
            return true;
        }

        if (string.Equals(text, "INVITE ONLY means you must send invites through Steam for players to join.", StringComparison.OrdinalIgnoreCase))
        {
            translated = "\u4ec5\u9650\u9080\u8bf7\uff1a\u5fc5\u987b\u901a\u8fc7 Steam \u9080\u8bf7\u73a9\u5bb6\u52a0\u5165";
            return true;
        }

        return false;
    }

    private static bool TryTranslateBracketedCommand(string text, out string translated)
    {
        translated = text;
        if (text.Length < 3 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        var inner = text.Substring(1, text.Length - 2).Trim();
        if (!ExactEntries.TryGetValue(inner, out var localized))
        {
            return false;
        }

        translated = "[ " + localized + " ]";
        return true;
    }

    private static bool TryTranslateDecoratedExactUiText(string text, out string translated)
    {
        translated = text;
        if (!TryGetDecoratedKnownExternalUiTokenSpan(text, out var start, out var length))
        {
            return false;
        }

        var token = text.Substring(start, length).Trim();
        if (!ExactEntries.TryGetValue(token, out var localized))
        {
            return false;
        }

        translated = text[..start] + localized + text[(start + length)..];
        return true;
    }

    private static bool TryTranslateControlTip(string text, out string translated)
    {
        translated = text;
        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var action = text.Substring(0, colon).Trim();
        if (!ExactEntries.TryGetValue(action, out var localizedAction))
        {
            return false;
        }

        var payload = text.Substring(colon + 1).Trim();
        if (!LooksLikeControlPayload(payload))
        {
            return false;
        }

        translated = localizedAction + "\uff1a" + NormalizeControlPayload(payload);
        return true;
    }

    private static bool TryTranslateLabelValue(string text, out string translated)
    {
        translated = text;
        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon >= text.Length - 1)
        {
            return false;
        }

        var label = text.Substring(0, colon).Trim();
        if (!ExactEntries.TryGetValue(label, out var localizedLabel))
        {
            return false;
        }

        var payload = text.Substring(colon + 1).Trim();
        if (payload.Length == 0 || LooksLikeNonUiName(payload))
        {
            return false;
        }

        translated = localizedLabel + "\uff1a" + payload;
        return true;
    }

    private static bool TryTranslateDiscountLine(string text, out string translated)
    {
        translated = text;
        var trimmed = text.Trim();
        var prefix = string.Empty;
        if (trimmed.StartsWith("*", StringComparison.Ordinal))
        {
            prefix = "* ";
            trimmed = trimmed[1..].TrimStart();
        }

        var parenStart = trimmed.LastIndexOf('(');
        if (parenStart <= 0 || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var itemAndPrice = trimmed.Substring(0, parenStart).TrimEnd();
        var discount = trimmed.Substring(parenStart + 1, trimmed.Length - parenStart - 2).Trim();
        var isDiscount = discount.EndsWith("off!", StringComparison.OrdinalIgnoreCase);
        var isPriceUp = discount.EndsWith("up!", StringComparison.OrdinalIgnoreCase);
        if ((!isDiscount && !isPriceUp) || !TryTranslateItemAndPrice(itemAndPrice, out var localizedItemAndPrice))
        {
            return false;
        }

        var suffixLength = isDiscount ? "off!".Length : "up!".Length;
        var percent = discount[..^suffixLength].Trim();
        if (percent.Length == 0 || !ContainsDigit(percent))
        {
            return false;
        }

        translated = prefix + localizedItemAndPrice + " \uff08" + percent + (isDiscount ? " \u6298\u6263\uff09" : " \u6da8\u4ef7\uff09");
        return true;
    }

    private static bool TryTranslateItemAndPrice(string value, out string translated)
    {
        translated = value;
        var dollar = value.LastIndexOf('$');
        if (dollar <= 0)
        {
            return false;
        }

        var item = value.Substring(0, dollar).TrimEnd();
        var price = value[dollar..].TrimStart();
        if (!LooksLikePrice(price))
        {
            return false;
        }

        var localizedItem = TranslationService.BuildTerminalLocalizedItemName(item);
        if (localizedItem.Length == 0 ||
            string.Equals(localizedItem, item, StringComparison.Ordinal))
        {
            return false;
        }

        translated = localizedItem + " " + price;
        return true;
    }

    private static string NormalizeControlPayload(string payload)
    {
        var value = payload.Trim();
        if (value.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase))
        {
            return "\u6309\u4f4f " + NormalizeKeyTokens(value["Hold ".Length..].Trim());
        }

        value = NormalizeKeyTokens(value);
        if (value.IndexOf("(Hold)", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            value = value.Replace("(Hold)", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd() + "\uff08\u957f\u6309\uff09";
        }

        return value;
    }

    private static string NormalizeKeyTokens(string value)
    {
        var builder = value;
        foreach (var entry in KeyTokenEntries)
        {
            builder = builder.Replace("[" + entry.Key + "]", "[" + entry.Value + "]", StringComparison.OrdinalIgnoreCase);
        }

        return builder;
    }

    private static bool LooksLikeControlPayload(string payload)
    {
        if (payload.Length < 3)
        {
            return false;
        }

        if (payload.StartsWith("Hold [", StringComparison.OrdinalIgnoreCase))
        {
            return payload.EndsWith("]", StringComparison.Ordinal);
        }

        return payload.IndexOf('[') >= 0 && payload.IndexOf(']') > payload.IndexOf('[');
    }

    private static bool LooksLikeKnownExternalUiLabel(string text)
    {
        if (text.EndsWith(":", StringComparison.Ordinal))
        {
            var label = text[..^1].TrimEnd();
            return ExactEntries.ContainsKey(label);
        }

        var colon = FindTopLevelColon(text);
        if (colon <= 0 || colon > 64)
        {
            return false;
        }

        var labelBeforeColon = text.Substring(0, colon).Trim();
        return ExactEntries.ContainsKey(labelBeforeColon);
    }

    private static string StripMenuSelectionPrefix(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith(">", StringComparison.Ordinal)
            ? trimmed[1..].TrimStart()
            : text;
    }

    private static bool LooksLikeBracketedKnownExternalUiToken(string text)
    {
        if (text.Length < 3 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        var inner = text.Substring(1, text.Length - 2).Trim();
        return ExactEntries.ContainsKey(inner);
    }

    private static bool LooksLikeDecoratedKnownExternalUiToken(string text)
    {
        if (!TryGetDecoratedKnownExternalUiTokenSpan(text, out var start, out var length))
        {
            return false;
        }

        var token = text.Substring(start, length).Trim();
        return ExactEntries.ContainsKey(token);
    }

    private static bool TryGetDecoratedKnownExternalUiTokenSpan(string text, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (text.Length < 5)
        {
            return false;
        }

        var left = 0;
        var right = text.Length - 1;
        while (left <= right && IsDecoratedTitleFrameChar(text[left]))
        {
            left++;
        }

        while (right >= left && IsDecoratedTitleFrameChar(text[right]))
        {
            right--;
        }

        if (left == 0 || right == text.Length - 1 || right < left)
        {
            return false;
        }

        start = left;
        length = right - left + 1;
        return true;
    }

    private static bool IsDecoratedTitleFrameChar(char ch) =>
        char.IsWhiteSpace(ch) || ch is '=' or '-' or '_';

    private static bool LooksLikeSaveFileLabel(string text)
    {
        const string Prefix = "File ";
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = text[Prefix.Length..].TrimStart();
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryTranslateAdvancedFeaturesPlayerLabel(string text, out string translated)
    {
        translated = text;
        if (!LooksLikeAdvancedFeaturesPlayerLabel(text))
        {
            return false;
        }

        translated = "\u73a9\u5bb6 #" + text.Trim()["Player #".Length..];
        return true;
    }

    private static bool LooksLikeAdvancedFeaturesPlayerLabel(string text)
    {
        const string Prefix = "Player #";
        var trimmed = text.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = trimmed[Prefix.Length..];
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeDeleteFilePrompt(string text) =>
        TryExtractDeleteFileNumber(text, out _) || TryExtractDeleteFileAlias(text, out _);

    private static bool TryExtractDeleteFileNumber(string text, out string fileNumber)
    {
        fileNumber = string.Empty;
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "Do you want to delete File ";
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        var number = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - 1).Trim();
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        fileNumber = number;
        return true;
    }

    private static bool TryExtractDeleteFileAlias(string text, out string alias)
    {
        alias = string.Empty;
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "Do you want to delete file (";
        const string Suffix = ")?";
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !normalized.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        alias = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - Suffix.Length).Trim();
        return alias.Length > 0;
    }

    private static bool LooksLikeLocalizedDeleteFileNumberPrompt(string text)
    {
        var normalized = NormalizeAsciiWhitespace(StripLeadingSimpleRichTextTags(StripOuterSimpleRichTextEnvelope(text)));
        const string Prefix = "\u8981\u5220\u9664 ";
        const string Suffix = " \u53f7\u5b58\u6863\u5417";
        if (!normalized.StartsWith(Prefix, StringComparison.Ordinal) ||
            !normalized.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var number = normalized.Substring(Prefix.Length, normalized.Length - Prefix.Length - Suffix.Length);
        if (number.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < number.Length; i++)
        {
            if (!char.IsDigit(number[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeNonUiName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.IndexOf(' ') >= 0)
        {
            return false;
        }

        if (trimmed.IndexOf('-') >= 0 && ContainsDigit(trimmed))
        {
            return true;
        }

        return LooksLikeCamelCaseIdentifier(trimmed);
    }

    private static bool LooksLikeCamelCaseIdentifier(string text)
    {
        if (text.Length < 6 || !IsAsciiLetter(text[0]))
        {
            return false;
        }

        var hasLower = false;
        var uppercaseAfterLower = false;
        foreach (var ch in text)
        {
            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            if (char.IsLower(ch))
            {
                hasLower = true;
            }
            else if (hasLower && char.IsUpper(ch))
            {
                uppercaseAfterLower = true;
            }
        }

        return uppercaseAfterLower;
    }

    private static int FindTopLevelColon(string value)
    {
        var inBracket = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '[')
            {
                inBracket = true;
                continue;
            }

            if (ch == ']' && inBracket)
            {
                inBracket = false;
                continue;
            }

            if (!inBracket && ch == ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static string StripOuterSimpleRichTextEnvelope(string value)
    {
        var text = value.Trim();
        for (var depth = 0; depth < 3; depth++)
        {
            if (text.Length < 7 || text[0] != '<')
            {
                break;
            }

            var tagClose = text.IndexOf('>');
            if (tagClose <= 1 || tagClose > 24)
            {
                break;
            }

            var tagNameLength = 0;
            for (var i = 1; i < tagClose; i++)
            {
                var ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '=')
                {
                    break;
                }

                if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
                {
                    tagNameLength = 0;
                    break;
                }

                tagNameLength++;
            }

            if (tagNameLength == 0)
            {
                break;
            }

            var tagName = text.Substring(1, tagNameLength);
            var closingTag = "</" + tagName + ">";
            if (!text.EndsWith(closingTag, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            text = text.Substring(tagClose + 1, text.Length - tagClose - 1 - closingTag.Length).Trim();
        }

        return text;
    }

    private static bool TryExtractOuterSimpleRichTextEnvelope(
        string value,
        out string prefix,
        out string inner,
        out string suffix)
    {
        prefix = string.Empty;
        inner = string.Empty;
        suffix = string.Empty;

        var text = value.Trim();
        if (!TryReadSimpleOpeningRichTextTag(text, out var tagEnd) ||
            !TryReadSimpleOpeningTagName(text, tagEnd, out var tagName))
        {
            return false;
        }

        var closingTag = "</" + tagName + ">";
        if (!text.EndsWith(closingTag, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        prefix = text.Substring(0, tagEnd + 1);
        suffix = text[^closingTag.Length..];
        inner = text.Substring(tagEnd + 1, text.Length - tagEnd - 1 - closingTag.Length);
        return true;
    }

    private static string StripLeadingSimpleRichTextTags(string value)
    {
        var text = value.Trim();
        while (TryReadSimpleOpeningRichTextTag(text, out var tagEnd))
        {
            text = text[(tagEnd + 1)..].TrimStart();
        }

        return text;
    }

    private static string ExtractLeadingSimpleRichTextPrefix(ref string value)
    {
        var text = value.Trim();
        StringBuilder? prefix = null;
        while (TryReadSimpleOpeningRichTextTag(text, out var tagEnd))
        {
            prefix ??= new StringBuilder();
            prefix.Append(text, 0, tagEnd + 1);
            text = text[(tagEnd + 1)..].TrimStart();
        }

        value = text;
        return prefix?.ToString() ?? string.Empty;
    }

    private static bool TryReadSimpleOpeningRichTextTag(string value, out int tagEnd)
    {
        tagEnd = -1;
        if (value.Length < 4 || value[0] != '<' || value[1] == '/')
        {
            return false;
        }

        var close = value.IndexOf('>');
        if (close <= 1 || close > 40)
        {
            return false;
        }

        var tagNameLength = 0;
        for (var i = 1; i < close; i++)
        {
            var ch = value[i];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            tagNameLength++;
        }

        if (tagNameLength == 0)
        {
            return false;
        }

        tagEnd = close;
        return true;
    }

    private static bool TryReadSimpleOpeningTagName(string value, int tagEnd, out string tagName)
    {
        tagName = string.Empty;
        if (tagEnd <= 1 || tagEnd >= value.Length)
        {
            return false;
        }

        var tagNameLength = 0;
        for (var i = 1; i < tagEnd; i++)
        {
            var ch = value[i];
            if (char.IsWhiteSpace(ch) || ch == '=' || ch == '#')
            {
                break;
            }

            if (!IsAsciiLetter(ch) && !char.IsDigit(ch))
            {
                return false;
            }

            tagNameLength++;
        }

        if (tagNameLength == 0)
        {
            return false;
        }

        tagName = value.Substring(1, tagNameLength);
        return true;
    }

    private static bool LooksLikePrice(string value)
    {
        if (value.Length < 2 || value[0] != '$')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]) && value[i] != ',' && value[i] != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsDigit(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeAsciiWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static bool ContainsLineBreak(string value) =>
        value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if (IsCjk(ch))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCjk(char ch) => ch >= 0x3400 && ch <= 0x9FFF;

    private static bool IsAsciiLetter(char ch) => (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z');
}
