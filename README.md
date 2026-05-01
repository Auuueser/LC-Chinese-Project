本项目仍处于测试阶段，目标是整理为可独立发布的 Lethal Company 简体中文本地化模组。 / This project is still in testing and is being prepared as an independently released Simplified Chinese localization mod for Lethal Company.

# LC Chinese Project

## 项目概述 / Overview

LC Chinese Project 为 Lethal Company 提供简体中文本地化、中文 TMP 字体 fallback、部分 UI 贴图替换，以及面向 V81 运行环境的兼容处理。项目不依赖 GameTranslator 运行；文本、字体和贴图替换均由本插件在 BepInEx 运行时完成。

LC Chinese Project provides Simplified Chinese localization, Chinese TMP font fallback, selected UI texture replacement, and V81 runtime compatibility handling for Lethal Company. It does not require GameTranslator at runtime; text, font, and texture replacement are handled by this BepInEx plugin.

## 覆盖范围 / Scope

- 游戏内 UI、HUD、终端、商店、结算、扫描提示、房间列表、大厅提示和部分场景文本本地化。 / Localizes in-game UI, HUD, terminal, store, endgame, scan prompts, lobby browser text, lobby warnings, and selected scene text.
- 内置中文字体 fallback，降低缺字、透明字和动态文本渲染问题。 / Provides Chinese font fallback to reduce missing glyphs, transparent glyphs, and dynamic text rendering issues.
- 本地化部分 UI 贴图，包括 warning 警告动画、结算页和提示类贴图。 / Replaces selected UI textures, including warning animation, endgame, and prompt textures.
- 保留终端确认输入的原版行为，避免翻译页面影响 `c`、`confirm` 等命令。 / Preserves vanilla terminal confirmation behavior so translated pages do not interfere with `c`, `confirm`, and related commands.
- 覆盖 V81 感染、空气过滤器、修改版主机提示、飞船磁铁、信号翻译器等新增文本。 / Covers V81 infection, air-filter, modified-host warning, ship magnet, signal translator, and related new text.
- 信号翻译器 HUD 使用缓存与短窗口重试，降低重复遍历；“正在接收信号”使用更清晰的字号。 / Uses cached HUD references and a short retry window for Signal Translator text, with a larger localized “receiving signal” display.
- 提供感染温度单位配置，默认摄氏度，可切换为华氏度。 / Provides a configurable infection temperature unit. Celsius is the default, and Fahrenheit remains available.
- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations、HoneeItemIcons：保留原版英文物品 key 给图标类模组使用，中文仅在显示层处理。 / Supports RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons by preserving vanilla English item keys for icon matching while translating display text separately.

## 目录结构 / Repository Layout

- `src/V81TestChn/`: BepInEx 插件源码。 / BepInEx plugin source code.
- `translations/`: JSON 翻译数据，用于兼容和旧查找路径。 / JSON translation data for compatibility and legacy lookup.
- `translations-cfg/`: 插件运行时加载的 cfg 翻译目录。 / cfg translation catalogs loaded at runtime.
- `assets/textures/`: 运行时贴图替换使用的本地化资源。 / Localized texture resources used by runtime replacement.
- `fonts/`: 中文 TMP 字体与 fallback/fontpatcher 资源。 / Chinese TMP font and fallback/fontpatcher resources.
- `lib/`: 本地构建引用。 / Local build references.
- `thunderstore/`: Thunderstore 发布根目录。 / Thunderstore release root.
- `thunderstore-tools/`: 本地构建与发布包校验脚本。 / Local package build and validation scripts.
- `github/V81-CHN/`: 最小化 GitHub 发布源码镜像。 / Minimal GitHub source mirror.

## 构建 / Build

```powershell
dotnet build .\src\V81TestChn\V81TestChn.csproj -c Release -p:TestDeployDir=
```

构建 Thunderstore 发布目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\thunderstore-tools\build-package.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\thunderstore-tools\validate-package.ps1"
```

`thunderstore/` 是发布前的压缩根目录。发布 zip 的根目录必须直接包含 `manifest.json`、`README.md`、`CHANGELOG.md`、`icon.png`、`LICENSE`、`THIRD_PARTY_LICENSES.md` 和 `BepInEx/`，不能额外套一层 `package/`。

`thunderstore/` is the pre-zip release root. The release zip root must directly contain `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png`, `LICENSE`, `THIRD_PARTY_LICENSES.md`, and `BepInEx/`; do not add an extra top-level `package/` folder.

## 安装与排查 / Installation And Troubleshooting

- 使用模组管理器安装时，Thunderstore/r2modman 会按发布包结构自动放置 `BepInEx/` 内容。 / When installed through a mod manager, Thunderstore/r2modman places the `BepInEx/` content according to the package layout.
- 手动安装时，将 zip 根目录中的 `BepInEx` 文件夹合并到游戏或 profile 根目录。 / For manual installation, merge the `BepInEx` folder from the zip into the game or profile root.
- 不要把整个发布目录放入 `BepInEx/plugins/package`。 / Do not place the entire release directory under `BepInEx/plugins/package`.
- 运行时资源从 `V81TestChn.dll` 所在目录解析，以兼容 Thunderstore/r2modman 的嵌套安装路径。 / Runtime resources are resolved from the directory containing `V81TestChn.dll` to support nested Thunderstore/r2modman install paths.
- 如日志出现 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`，通常说明 DLL 已加载但资源路径错误，请检查旧版本残留和安装目录。 / If logs show `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`, the DLL loaded but resources were not found. Check stale package remnants and the install path.

## 配置 / Configuration

插件首次运行后会生成：

```text
BepInEx/config/cn.codex.v81testchn.cfg
```

感染状态温度单位位于 `[InfectionStatus]`：

```ini
[InfectionStatus]
TemperatureUnit = Celsius
```

`TemperatureUnit` 支持 `Celsius` 和 `Fahrenheit`。默认值为 `Celsius`。

The plugin creates the following config file after first launch:

```text
BepInEx/config/cn.codex.v81testchn.cfg
```

The infection status temperature unit is configured under `[InfectionStatus]`:

```ini
[InfectionStatus]
TemperatureUnit = Celsius
```

`TemperatureUnit` accepts `Celsius` or `Fahrenheit`. The default is `Celsius`.

## 第三方内容与许可 / Third-Party Content And Licenses

本仓库自身以 MIT 协议发布。项目包含或改编了部分第三方 MIT 内容，并包含基于 OFL 字体生成的 TMP 字体资源；详细说明见 `THIRD_PARTY_LICENSES.md`。

This repository is licensed under MIT. It includes or adapts some third-party MIT content and includes TMP font assets generated from OFL-licensed fonts; see `THIRD_PARTY_LICENSES.md` for details.

### 已使用或改编的内容 / Used Or Adapted Content

- `LethalCompany_Chinese_Localized_Translation`: 包含从本地 `BepInEx/config/translations` 导入并继续维护的部分中文翻译配置，以及若干本地化贴图资源；运行时加载和替换逻辑由本项目自主实现。许可：MIT。来源：https://github.com/CoolLKKPS/LethalCompany_Chinese_Localized_Translation
- `LC-FontPatcher`: 包含 `fonts/fontpatcher/default/*` 字体包，并在 `EmbeddedFontPatcherService` 中内置和改编了 FontPatcher 风格的字体包加载、TMP fallback 注入和字体匹配行为。许可：MIT。来源：https://github.com/lekakid/LC-FontPatcher

### 参考思路 / Conceptual Reference

- `GameTranslator`: 本项目曾审查其贴图翻译思路，但运行时实现没有依赖、复制或打包 GameTranslator 代码。许可：MIT。来源：https://github.com/CoolLKKPS/GameTranslator
