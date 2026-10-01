using BepInEx.Configuration;

namespace V81TestChn;

internal static class PlayerNameSettings
{
    internal static bool Enabled { get; private set; } = true;

    internal static void Initialize(ConfigFile config)
    {
        var entry = Bind(config);
        Enabled = entry.Value;
        LiveConfigRegistration.RegisterBool(entry, requiresRestart: true);
    }

    internal static ConfigEntry<bool> Bind(ConfigFile config) => config.Bind(
        "05 界面 - 玩家名称修复", "EnablePlayerNameFix", true,
        "保留玩家名称中的中文、数字与空格，修复短名/重名编号，并同步房间昵称、防止其他名称修复覆盖。默认开启。修改后需重启游戏；关闭后不安装本模组的名称修复补丁，也不发送或读取本模组的房间昵称数据，名称由原版及其他模组处理。普通界面汉化不受影响。");
}
