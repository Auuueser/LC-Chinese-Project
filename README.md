<p align="center">
  <img src="assets/readme/hero.svg" alt="LC Chinese Project — Lethal Company V81 中文汉化" width="100%">
</p>

<p align="center">
  <a href="https://thunderstore.io/c/lethal-company/p/Aueser/LC_Chinese_Project/"><img alt="当前版本 3.2.1" src="assets/readme/badge-version.svg" height="22"></a>
  <img alt="支持游戏版本 V81" src="assets/readme/badge-game.svg" height="22">
  <img alt="运行环境 BepInEx 5" src="assets/readme/badge-runtime.svg" height="22">
  <a href="LICENSE"><img alt="MIT 许可证" src="assets/readme/badge-license.svg" height="22"></a>
</p>

<p align="center">
  面向 <strong>Lethal Company V81</strong> 的简体中文本地化模组<br>
  从运行时文本、中文字体到烘焙贴图，统一构建自然、稳定的中文体验
</p>

<p align="center">
  <a href="https://thunderstore.io/c/lethal-company/p/Aueser/LC_Chinese_Project/"><img alt="Thunderstore 下载" src="assets/readme/badge-download.svg" height="24"></a>
  <a href="CHANGELOG.md"><img alt="查看更新日志" src="assets/readme/badge-changelog.svg" height="24"></a>
  <a href="https://github.com/Auuueser/LC-Chinese-Project/issues"><img alt="提交问题反馈" src="assets/readme/badge-feedback.svg" height="24"></a>
</p>

## 项目概览

| 原版覆盖 | 视觉本地化 | 显示支持 |
|:--|:--|:--|
| HUD、终端、商店、扫描、星球信息、设置、结算与交互提示 | 船内海报、文件夹板、巡航车车标与车载手册等烘焙贴图 | 中文字体、常用 Emoji 与输入内容保护 |

- 中文字体与 256 个常用单码位 Emoji 由局部 TextMeshPro 资源提供。
- 玩家输入、终端命令、玩家名称和图标匹配键受到保护，减少误翻译。
- 常见运行时界面、系统消息与动态文本可直接进入统一汉化路径。

## 视觉预览

<p align="center">
  <img src="assets/readme/cruiser-localization-preview.png" alt="公司巡航车干净与脏污版本中文车标" width="100%">
  <br><sub>公司巡航车：干净与脏污版本均保留原版边框、标志、材质与磨损细节</sub>
</p>

<table>
  <tr>
    <td width="50%" align="center">
      <img src="assets/textures/zh-CN/Texture/ShipPostersLocalized.png" alt="船内海报中文贴图" width="100%"><br>
      <sub>船内海报</sub>
    </td>
    <td width="50%" align="center">
      <img src="assets/textures/zh-CN/Texture/CruiserManualPage4.png" alt="巡航车车载手册中文贴图" width="100%"><br>
      <sub>巡航车车载手册</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center">
      <img src="assets/textures/zh-CN/Texture/ClipboardManualPage4.png" alt="文件夹板中文贴图" width="100%"><br>
      <sub>手持文件夹板</sub>
    </td>
    <td width="50%" align="center">
      <img src="assets/textures/zh-CN/Texture/StickyNoteLocalized.png" alt="船内便签中文贴图" width="100%"><br>
      <sub>船内便签</sub>
    </td>
  </tr>
</table>

## 安装

<p align="center">
  <img src="assets/readme/install-guide.svg" alt="安装引导：安装 BepInExPack、安装本模组、启动游戏" width="100%">
</p>

推荐使用 **r2modman / Thunderstore Mod Manager**，搜索 `LC Chinese Project` 后安装；依赖项会由管理器自动处理。

<details>
<summary>手动安装</summary>

1. 安装 `BepInExPack 5.4.2100`。
2. 将发布包中的 `BepInEx` 文件夹合并到游戏或 profile 根目录。
3. 确认以下内容存在后启动游戏：

```text
BepInEx/plugins/V81TestChn/V81TestChn.dll
BepInEx/plugins/V81TestChn/translations-clean/
BepInEx/plugins/V81TestChn/fonts/
BepInEx/plugins/V81TestChn/textures/
```

</details>

## 配置与扩展

首次启动会生成：

```text
BepInEx/config/LC Chinese Project.cfg
```

自定义翻译可放入：

```text
BepInEx/config/V81TestChn/custom-localization/
```

建议优先使用 `exact` 或 `ignorecase`；正则规则默认关闭。

## 构建与验证

```powershell
dotnet build src\V81TestChn\V81TestChn.csproj -c Release -p:GameDir="D:\Steam\steamapps\common\Lethal Company"
```

## 反馈

请在 [GitHub Issues](https://github.com/Auuueser/LC-Chinese-Project/issues) 附上问题截图、出现位置、模组列表或 profile code，以及 `BepInEx/LogOutput.log`。

## 许可

项目采用 [MIT License](LICENSE)。字体与第三方资源归属见 [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)。
