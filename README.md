# LC Chinese Project

Version: `3.1.2`

## 中文说明

LC Chinese Project 是面向 Lethal Company V81 测试环境维护的简体中文本地化模组。项目提供运行时文本汉化、中文 TextMeshPro 字体 fallback、部分 UI 贴图本地化、动态文本后处理，以及常见 UI 与图标类模组的通用兼容处理。

本项目不依赖既有第三方汉化或字体修补运行时；文本、字体、贴图与兼容逻辑均由本插件独立处理。第三方兼容以运行时文本路径为主，不复制第三方模组资源，也不要求额外安装翻译框架。

### 主要功能

- 覆盖游戏内 UI、HUD、终端、商店、扫描提示、飞船显示屏、星球信息、结算界面、大厅提示和部分场景文本。
- 处理终端订单、星球天气、扫描价值、聊天系统消息、投票、剩余天数、重量单位、服装切换提示、载具交互提示和观战状态等动态文本。
- 改善主菜单、房间创建、加入游戏、游戏设置、控制器绑定、退出确认和大厅状态弹窗等界面的中文显示。
- 补全 HUD 交互提示、建造模式提示、扫描 HUD 副文本和终端订单反馈中的常见英文残留。
- 提供中文 TextMeshPro fallback，降低缺字、透明字、黑色字、裁切和动态文本渲染异常。
- 按需使用系统补充字体显示日文、韩文等非中文东亚字符，改善大厅名、玩家名、聊天消息和第三方运行时文本中的跨语言字符显示。
- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations 和 HoneeItemIcons，保留原版英文物品 key 供图标匹配使用，仅在显示层输出中文。
- 支持自定义本地化与可选自动翻译。自动翻译默认关闭，开启后通过异步请求和本地缓存处理非原版英文文本。
- 面向大型整合包提供可配置的运行时缓存与菜单处理预算，在保持汉化覆盖的同时减少重复热路径工作。

### 安装

使用 Thunderstore 或 r2modman 安装时，请先安装 `BepInExPack`，再安装本模组。

手动安装时，将发布包根目录中的 `BepInEx` 文件夹合并到游戏或 profile 根目录。正确安装后应存在以下文件和目录：

```text
BepInEx/plugins/V81TestChn/V81TestChn.dll
BepInEx/plugins/V81TestChn/translations-clean/
BepInEx/plugins/V81TestChn/fonts/
BepInEx/plugins/V81TestChn/textures/
```

不要将整个发布包解压为 `BepInEx/plugins/package`，也不要在压缩包外额外套一层 `package/` 目录。

### 发布包内容

发布包仅包含运行所需文件：

- 插件 DLL：`BepInEx/plugins/V81TestChn/V81TestChn.dll`
- 运行时词库：`BepInEx/plugins/V81TestChn/translations-clean/`
- 字体资源：`BepInEx/plugins/V81TestChn/fonts/`
- 本地化贴图：`BepInEx/plugins/V81TestChn/textures/`
- 发布元数据：`manifest.json`、`README.md`、`CHANGELOG.md`、`LICENSE`、`THIRD_PARTY_LICENSES.md`

### 主配置文件

插件的主配置文件会生成在：

```text
BepInEx/config/LC Chinese Project.cfg
```

如果检测到旧版 `BepInEx/config/cn.codex.v81testchn.cfg`，且新配置文件尚不存在，插件会在启动时复制一份到新文件名。旧文件不会被自动删除。

### 自定义本地化

推荐将个人规则或其他英文模组的补充规则放在：

```text
BepInEx/config/V81TestChn/custom-localization/
```

插件也会读取：

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
```

规则示例：

```ini
exact:Company Cruiser=公司巡航车
ignorecase:Pull switch=拉动开关
regex:^(\d+) lb$=$1 磅
style:exact:WARNING|color=#FF4D4D|fontSize=28|richText=true
```

常用配置：

```ini
[01 基础 - 自定义本地化]
Enabled = true
PreferCustomTranslations = false
EnableRegex = false
MaxLoadedFiles = 32
MaxConfigFileBytes = 262144
MaxExactRules = 4096
MaxIgnoreCaseRules = 4096
MaxRegexRules = 64
MaxStyleRules = 64
```

建议优先使用 `exact` 或 `ignorecase` 规则。`regex` 默认关闭，只应在规则可靠、范围明确且性能可控时启用。

### 自动翻译

自动翻译用于补充非原版英文文本，默认关闭，不影响常规游戏性能。开启后，插件只在未命中已知词条、文本长度与并发数量均满足限制时，向配置的 HTTP provider 发起后台请求。

```ini
[02 Performance - Automatic Translation]
EnableAutomaticTranslation = false
ProviderEndpoint =
ProviderTimeoutMilliseconds = 2500
MaxTextLength = 300
MaxCacheEntries = 2000
MaxPendingRequests = 32
LogAutomaticTranslation = false
```

HTTP provider 接收 JSON：`{ text, source, target }`。返回值可以是纯文本，也可以是包含翻译字段的 JSON。主线程文本钩子不会等待 provider 响应；可用结果会写入本地缓存，并在后续显示中使用。

### 大型整合包性能配置

以下配置用于控制通用运行时文本路径的缓存规模和菜单处理节奏。默认值面向大型 r2modman profile，单独使用本模组时也保持较低开销。

```ini
[02 性能 - 运行时预算]
LargeModpackTmpHookCacheLimit = 16384
LargeModpackComponentTextCacheLimit = 16384
LargeModpackHudScannerCacheLimit = 16384
LargeModpackExternalCompatibilityCacheLimit = 4096
LargeModpackFontFallbackCacheLimit = 16384
MenuTranslationWorkBudgetPerFrame = 12
EnableTargetedUiStyleRepairFastGate = true
```

缓存项都有上限和范围校验，配置变更会在运行时刷新。一般不建议低于默认值；只有在极端内存约束或排查特定 UI 样式问题时，才需要降低缓存或关闭样式修复快速门。

### 兼容性说明

- 图标类模组如果依赖原版英文物品名，本模组会尽量保留图标匹配 key，只在显示层翻译中文。
- 终端输入、聊天输入、玩家名、大厅动态名和图标匹配文本会优先进入保护逻辑，减少误翻译。
- 常见第三方运行时英文文本会通过通用文本路径处理，不依赖插件 GUID、类型名或第三方资源复制。
- 大型整合包中如果菜单或 HUD 文本频繁刷新，建议优先保留默认性能预算。

### 排查

- 如果日志显示 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`，通常说明插件 DLL 已加载，但 `translations-clean` 资源目录未被找到。
- 如果输入文本被错误翻译，请优先检查终端输入、聊天输入、玩家名和大厅动态文本是否被自定义规则覆盖。
- 如果自定义本地化规则导致卡顿，请先禁用 `regex`，再逐步缩小规则范围。
- 如果自动翻译 provider 不可用，插件会保留原文并跳过本次结果，不会阻塞 UI。

### 许可与致谢

本项目以 MIT 协议发布，并分发基于 OFL 字体生成的 TextMeshPro 字体资源。详细归属与分发说明见 `THIRD_PARTY_LICENSES.md`。

## English

LC Chinese Project is a Simplified Chinese localization mod maintained for the Lethal Company V81 test environment. It provides runtime text localization, Chinese TextMeshPro fallback, selected localized UI textures, dynamic text post-processing, and generic compatibility handling for common UI and icon-related mods.

This project does not require existing third-party translation or font-patching runtimes. Text, font, texture, and compatibility logic are handled by this plugin. Third-party compatibility is handled through runtime text paths; the package does not copy third-party mod resources or require an additional translation framework.

### Features

- Localizes in-game UI, HUD, terminal pages, store pages, scan prompts, ship monitor text, planet information, endgame screens, lobby warnings, and selected scene text.
- Handles dynamic text such as terminal orders, planet weather, scanner values, chat system messages, votes, days left, weight units, suit-change prompts, vehicle prompts, and spectate status.
- Improves Chinese display for the main menu, lobby creation, game joining, settings, controller binding, leave-game confirmation, and lobby status popups.
- Completes common missing coverage in HUD interaction prompts, build-mode prompts, scanner subtext, and terminal order feedback.
- Provides Chinese TextMeshPro fallback to reduce missing glyphs, transparent glyphs, black glyphs, clipping, and dynamic text rendering issues.
- Uses system supplemental fonts only when needed for Japanese, Korean, and other non-Chinese East Asian characters in lobby names, player names, chat messages, and third-party runtime text.
- Keeps RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons compatible by preserving vanilla English item keys for icon matching while translating display text separately.
- Supports custom localization and optional automatic translation. Automatic translation is disabled by default and uses asynchronous requests plus a local cache for non-vanilla English text.
- Provides configurable runtime caches and menu work budgets for large modpacks, reducing repeated hot-path work while preserving localization coverage.

### Installation

When using Thunderstore or r2modman, install `BepInExPack` first, then install this mod.

For manual installation, merge the package `BepInEx` folder into the game or profile root. A correct install contains:

```text
BepInEx/plugins/V81TestChn/V81TestChn.dll
BepInEx/plugins/V81TestChn/translations-clean/
BepInEx/plugins/V81TestChn/fonts/
BepInEx/plugins/V81TestChn/textures/
```

Do not install the package as `BepInEx/plugins/package`, and do not create a zip with an extra top-level `package/` directory.

### Package Contents

The release package contains only required runtime files:

- Plugin DLL: `BepInEx/plugins/V81TestChn/V81TestChn.dll`
- Runtime glossary: `BepInEx/plugins/V81TestChn/translations-clean/`
- Font assets: `BepInEx/plugins/V81TestChn/fonts/`
- Localized textures: `BepInEx/plugins/V81TestChn/textures/`
- Release metadata: `manifest.json`, `README.md`, `CHANGELOG.md`, `LICENSE`, `THIRD_PARTY_LICENSES.md`

### Main Config File

The main plugin config file is generated at:

```text
BepInEx/config/LC Chinese Project.cfg
```

If the legacy `BepInEx/config/cn.codex.v81testchn.cfg` exists and the new config file does not, the plugin copies the legacy file to the new name during startup. The legacy file is not deleted automatically.

### Custom Localization

The recommended directory for personal rules and additional English-mod rules is:

```text
BepInEx/config/V81TestChn/custom-localization/
```

The plugin also reads:

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
```

Example:

```ini
exact:Company Cruiser=公司巡航车
ignorecase:Pull switch=拉动开关
regex:^(\d+) lb$=$1 磅
style:exact:WARNING|color=#FF4D4D|fontSize=28|richText=true
```

Common settings:

```ini
[01 基础 - 自定义本地化]
Enabled = true
PreferCustomTranslations = false
EnableRegex = false
MaxLoadedFiles = 32
MaxConfigFileBytes = 262144
MaxExactRules = 4096
MaxIgnoreCaseRules = 4096
MaxRegexRules = 64
MaxStyleRules = 64
```

Prefer `exact` or `ignorecase` rules. `regex` is disabled by default and should only be enabled for verified, bounded rules.

### Automatic Translation

Automatic translation is intended for non-vanilla English text. It is disabled by default and does not affect normal runtime performance. When enabled, the plugin queues background HTTP provider requests only after known translations miss and length/concurrency limits pass.

```ini
[02 Performance - Automatic Translation]
EnableAutomaticTranslation = false
ProviderEndpoint =
ProviderTimeoutMilliseconds = 2500
MaxTextLength = 300
MaxCacheEntries = 2000
MaxPendingRequests = 32
LogAutomaticTranslation = false
```

The HTTP provider receives JSON: `{ text, source, target }`. It may return plain text or JSON containing a translation field. Main-thread text hooks never wait for provider responses; usable results are cached locally and used on later displays.

### Large Modpack Performance

These settings control cache sizes and menu processing cadence for generic runtime text paths. Defaults are tuned for large r2modman profiles while remaining lightweight when this mod is installed alone.

```ini
[02 性能 - 运行时预算]
LargeModpackTmpHookCacheLimit = 16384
LargeModpackComponentTextCacheLimit = 16384
LargeModpackHudScannerCacheLimit = 16384
LargeModpackExternalCompatibilityCacheLimit = 4096
LargeModpackFontFallbackCacheLimit = 16384
MenuTranslationWorkBudgetPerFrame = 12
EnableTargetedUiStyleRepairFastGate = true
```

Cache entries are bounded and range-checked, and config changes refresh at runtime. Keeping the defaults is recommended for large modpacks; reduce them only under strict memory limits or when diagnosing a specific UI style repair issue.

### Compatibility Notes

- If an icon mod depends on vanilla English item names, this mod preserves icon-matching keys where possible and translates display text separately.
- Terminal input, chat input, player names, lobby dynamic names, and icon-matching text are protected first to reduce mistranslation.
- Common third-party runtime English text is handled through generic text paths, without plugin GUID checks, type-name checks, or third-party resource copying.
- If a large modpack refreshes menu or HUD text frequently, keep the default performance budget first.

### Troubleshooting

- If the log shows `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`, the plugin DLL loaded but the `translations-clean` resource directory was not found.
- If input text is translated incorrectly, check whether terminal input, chat input, player names, or lobby dynamic text are being affected by custom rules.
- If custom localization rules cause stutter, disable `regex` first and reduce rule scope.
- If the automatic translation provider is unavailable, the plugin keeps the original text and skips that result without blocking UI.

### License

This project is released under the MIT license and distributes TextMeshPro font resources generated from OFL-licensed font material. See `THIRD_PARTY_LICENSES.md` for attribution and redistribution notes.
