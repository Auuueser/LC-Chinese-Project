# LC Chinese Project

本项目仍处于测试阶段，目标是整理为可独立发布的 Lethal Company 简体中文本地化模组。  
This project is still in testing and is being prepared as an independently released Simplified Chinese localization mod for Lethal Company.

## 项目定位 / Project Status

LC Chinese Project 为 Lethal Company 提供简体中文本地化、中文 TextMeshPro 字体 fallback、部分 UI 贴图替换，以及针对 V81 运行环境的兼容处理。

LC Chinese Project provides Simplified Chinese localization, Chinese TextMeshPro font fallback, selected UI texture replacement, and V81 runtime compatibility handling for Lethal Company.

本项目不依赖 GameTranslator 运行。文本、字体和贴图替换由本 BepInEx 插件在运行时完成。

This project does not require GameTranslator at runtime. Text, font, and texture replacement are handled by this BepInEx plugin.

## 功能范围 / Features

- 本地化游戏内 UI、HUD、终端、商店、结算、扫描提示和部分场景文本。
- 内置中文字体 fallback，降低缺字、透明字和动态文本渲染问题。
- 本地化部分 UI 贴图，包括 warning 警告动画、结算和提示类贴图。
- 保留终端确认输入的原版行为，避免翻译后的终端页面影响 `c`、`confirm` 等确认命令。
- 补全 V81 感染、空气过滤器和大厅修改版主机提示等新增文本。
- 支持感染温度单位配置，默认使用摄氏度，可切换为华氏度。
- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations、HoneeItemIcons：保留原版英文物品 key 给图标类模组使用，中文仅在显示层处理。

- Localizes in-game UI, HUD, terminal, store, endgame, scan prompts, and selected scene text.
- Provides Chinese font fallback to reduce missing glyphs, transparent glyphs, and dynamic text rendering issues.
- Replaces selected localized UI textures, including warning animation, endgame, and prompt textures.
- Preserves vanilla terminal confirmation input behavior so translated terminal pages do not interfere with `c`, `confirm`, and related commands.
- Covers newer V81 infection, air-filter, and modified-host lobby warning text.
- Provides a configurable infection temperature unit. Celsius is the default, and Fahrenheit remains available.
- Supports RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons by preserving vanilla English item keys for icon matching while translating display text separately.

## 安装说明 / Installation

推荐通过 Thunderstore 或 r2modman 安装正式发布包。GitHub 仓库主要用于源码、翻译资源和发布元数据管理，不是直接拖入游戏的发布包根目录。

Installing the official Thunderstore or r2modman package is recommended. This GitHub repository is intended for source code, translation resources, and release metadata; it is not the direct drag-and-drop release package root.

手动安装发布包时，应将发布包内的 `BepInEx` 文件夹合并到游戏或 profile 根目录。

For manual release-package installation, merge the packaged `BepInEx` folder into the game or profile root.

如果日志出现 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`，通常说明 DLL 已加载但资源路径错误，请检查安装目录或旧版本残留。

If logs show `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`, the DLL loaded but resources were not found. Check the install path or stale package remnants.

## 仓库结构 / Repository Layout

```text
assets/              Localized runtime texture assets
fonts/               Chinese TMP font bundle and fallback resources
lib/                 Local build references for BepInEx and Harmony
src/V81TestChn/      BepInEx plugin source code
thunderstore/        Thunderstore metadata files
translations/        JSON translation data
translations-cfg/    Runtime cfg translation catalog
```

## 构建 / Build

构建需要本地安装 Lethal Company，并确保 `src/V81TestChn/V81TestChn.csproj` 中的 `GameDir` 指向本机游戏目录。

Building requires a local Lethal Company installation. Ensure `GameDir` in `src/V81TestChn/V81TestChn.csproj` points to the local game directory.

```powershell
dotnet build .\src\V81TestChn\V81TestChn.csproj -c Release -p:TestDeployDir=
```

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

可选值为 `Celsius` 或 `Fahrenheit`。默认值为 `Celsius`。

The config file is generated after the first launch:

```text
BepInEx/config/cn.codex.v81testchn.cfg
```

The infection status temperature unit is configured under `[InfectionStatus]`:

```ini
[InfectionStatus]
TemperatureUnit = Celsius
```

Accepted values are `Celsius` and `Fahrenheit`. The default is `Celsius`.

## 兼容说明 / Compatibility Notes

- 本项目不打包、不引用、不复制 RuntimeIcons、RuntimeIcons_BetterRotations 或 HoneeItemIcons 的代码与资源。
- 运行时资源从 `V81TestChn.dll` 所在目录解析，以兼容 Thunderstore/r2modman 的嵌套安装路径。
- 若其他模组直接改写同一段 UI 文本或同一张 UI 贴图，仍可能出现显示顺序或覆盖顺序差异。

- This project does not bundle, reference, or copy code or assets from RuntimeIcons, RuntimeIcons_BetterRotations, or HoneeItemIcons.
- Runtime resources are resolved from the directory containing `V81TestChn.dll` to support nested Thunderstore/r2modman install paths.
- If another mod rewrites the same UI text or UI texture, display order or override-order differences may still occur.

## 许可与第三方内容 / License And Third-Party Content

本仓库自身许可证为 MIT。发布包包含或改编了部分第三方 MIT 内容，并包含基于 OFL 字体生成的 TMP 字体资源；详细说明见 `THIRD_PARTY_LICENSES.md`。

This repository is licensed under MIT. Release packages include or adapt some third-party MIT content and include TMP font assets generated from OFL-licensed fonts; see `THIRD_PARTY_LICENSES.md` for details.

### 使用或改编的内容 / Used Or Adapted Content

- `LethalCompany_Chinese_Localized_Translation`: 包含从本地 `BepInEx/config/translations` 导入并继续维护的部分中文翻译配置，以及若干本地化贴图资源；运行时加载和替换逻辑由本项目自主实现。许可证：MIT。来源：https://github.com/CoolLKKPS/LethalCompany_Chinese_Localized_Translation
- `LethalCompany_Chinese_Localized_Translation`: This project contains part of the Chinese translation configuration imported from local `BepInEx/config/translations` and maintained further, plus several localized texture assets; runtime loading and replacement logic is implemented by this project. License: MIT. Source: https://github.com/CoolLKKPS/LethalCompany_Chinese_Localized_Translation

- `LC-FontPatcher`: 包含 `fonts/fontpatcher/default/*` 字体包，并在 `EmbeddedFontPatcherService` 中内置和改编了 FontPatcher 风格的字体包加载、TMP fallback 注入和字体匹配行为。许可证：MIT。来源：https://github.com/lekakid/LC-FontPatcher
- `LC-FontPatcher`: This project includes the `fonts/fontpatcher/default/*` font bundles and embeds/adapts FontPatcher-style font bundle loading, TMP fallback injection, and font matching behavior in `EmbeddedFontPatcherService`. License: MIT. Source: https://github.com/lekakid/LC-FontPatcher

### 兼容格式与参考思路 / Compatible Format And Conceptual Reference

- `GameTranslator`: 本项目不依赖 GameTranslator 运行，也未打包其插件 DLL；但保留 GameTranslator 兼容的 cfg 翻译文件结构、导出文档和资源组织思路，用于迁移、审计和兼容测试。许可证：MIT。来源：https://github.com/CoolLKKPS/GameTranslator
- `GameTranslator`: This project does not require GameTranslator at runtime and does not package its plugin DLL; however, it keeps a GameTranslator-compatible cfg translation file layout, export documentation, and resource organization ideas for migration, auditing, and compatibility testing. License: MIT. Source: https://github.com/CoolLKKPS/GameTranslator
