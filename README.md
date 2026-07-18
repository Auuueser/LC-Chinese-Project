<p align="center">
  <img src="assets/readme/hero.svg" alt="LC Chinese Project — Lethal Company V81 中文汉化" width="100%">
</p>

<p align="center">
  <a href="https://thunderstore.io/c/lethal-company/p/Aueser/LC_Chinese_Project/"><img alt="当前版本 3.2.0" src="assets/readme/badge-version.svg" height="22"></a>
  <img alt="支持游戏版本 V81" src="assets/readme/badge-game.svg" height="22">
  <img alt="运行环境 BepInEx 5" src="assets/readme/badge-runtime.svg" height="22">
  <a href="LICENSE"><img alt="MIT 许可证" src="assets/readme/badge-license.svg" height="22"></a>
</p>

<p align="center">
  面向 <strong>Lethal Company V81</strong> 的独立简体中文本地化模组<br>
  覆盖原版界面、动态文本、中文字体与常见第三方模组文本
</p>

<p align="center">
  <a href="https://thunderstore.io/c/lethal-company/p/Aueser/LC_Chinese_Project/"><img alt="Thunderstore 下载" src="assets/readme/badge-download.svg" height="24"></a>
  <a href="CHANGELOG.md"><img alt="查看更新日志" src="assets/readme/badge-changelog.svg" height="24"></a>
  <a href="https://github.com/Auuueser/LC-Chinese-Project/issues"><img alt="提交问题反馈" src="assets/readme/badge-feedback.svg" height="24"></a>
</p>

## 安装

<p align="center">
  <img src="assets/readme/install-guide.svg" alt="安装引导：安装 BepInExPack、安装本模组、启动游戏" width="100%">
</p>

推荐使用 **r2modman / Thunderstore Mod Manager**，搜索 `LC Chinese Project` 后点击安装即可；依赖项会由管理器自动处理。

<details>
<summary>手动安装</summary>

1. 安装 `BepInExPack 5.4.2100`。
2. 将发布包中的 `BepInEx` 文件夹合并到游戏或 profile 根目录。
3. 确认以下文件存在后启动游戏：

```text
BepInEx/plugins/V81TestChn/V81TestChn.dll
BepInEx/plugins/V81TestChn/translations-clean/
BepInEx/plugins/V81TestChn/fonts/
BepInEx/plugins/V81TestChn/textures/
```

</details>

## 覆盖范围

| 游戏文本 | 显示支持 | 模组兼容 |
|:--|:--|:--|
| HUD、终端、商店、扫描、星球信息、结算与交互提示 | 中文 TextMeshPro 字体、东亚字符补充字体与本地化贴图 | 动态系统消息、常见 UI、图标类模组及部分第三方命令输出 |

- 保护终端输入、聊天输入、玩家名称与图标匹配键，减少误翻译。
- 保留第三方资源所有权，仅处理运行时显示文本，不复制其他模组资源。
- 缓存和菜单工作预算均有上限，默认配置适用于大型整合包与长时间多人游戏。

## 配置

主配置文件会在首次启动后生成：

```text
BepInEx/config/LC Chinese Project.cfg
```

一般情况下无需修改。自定义翻译可放入：

```text
BepInEx/config/V81TestChn/custom-localization/
```

建议优先使用 `exact` 或 `ignorecase` 规则；正则规则默认关闭。

## 遇到问题

提交反馈时，请附上：

- 问题截图与出现位置
- 当前模组列表或 r2modman profile code
- `BepInEx/LogOutput.log`

请前往 [GitHub Issues](https://github.com/Auuueser/LC-Chinese-Project/issues) 反馈。若日志显示词库加载数量为 `0`，请先检查 `translations-clean` 目录是否完整。

## 许可

项目采用 [MIT License](LICENSE)。字体与第三方资源归属见 [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)。
