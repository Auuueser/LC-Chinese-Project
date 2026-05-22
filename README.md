# LC Chinese Project

## 中文说明

LC Chinese Project 是面向 Lethal Company V81 测试环境维护的简体中文本地化模组。项目提供运行时文本汉化、中文 TextMeshPro 字体 fallback、部分 UI 贴图本地化，以及针对常见 UI、图标和动态文本场景的兼容处理。

本项目不依赖 GameTranslator 运行时。文本替换、动态 UI 后处理、字体 fallback、贴图替换和兼容逻辑均由本插件在 BepInEx 环境中完成。

### 功能范围

- 汉化游戏内 UI、HUD、终端、商店、扫描提示、飞船显示屏、星球信息、结算界面、大厅提示和部分场景文本。
- 覆盖终端订单、星球天气、扫描价值、聊天系统消息、投票、剩余天数、重量单位、服装切换提示和载具交互提示等动态文本。
- 保留终端输入、聊天输入、玩家名、大厅动态名、确认命令和图标类模组物品 key 的原版行为。
- 提供中文 TextMeshPro 字体 fallback，降低缺字、透明字和动态文本渲染异常。
- 包含部分本地化 UI 贴图资源。
- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations 和 HoneeItemIcons，保留原版英文物品 key 供图标匹配使用，仅在显示层处理中文。
- 新增自定义本地化支持：可通过独立 `.cfg` 文件扩展文本替换规则。

### 自定义本地化

推荐将个人规则或其他英文模组的补充规则放在：

```text
BepInEx/config/V81TestChn/custom-localization/
```

插件也会读取以下目录：

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
BepInEx/config/translations/custom/
```

规则示例：

```ini
# 精确匹配
exact:Company Cruiser=公司巡航车
Bee Suit=蜜蜂套装

# 忽略大小写匹配
ignorecase:Pull switch=拉动开关
i:Push=推动

# 正则替换，默认关闭
regex:^(\d+) lb$=$1 磅
r:^\s*Random seed:\s*(\d+)\s*$=随机种子：$1

# 样式规则
style:exact:WARNING|color=#FF4D4D|fontSize=28|richText=true
style:ignorecase:discount|color=#FFD447
```

规则前缀：

- 无前缀或 `exact:`：区分大小写的精确匹配。
- `ignorecase:` 或 `i:`：忽略大小写的精确匹配。
- `regex:` 或 `r:`：正则替换，默认关闭，需要在配置中显式启用。
- `style:`：对匹配文本组件应用样式，支持 `color`、`fontSize`、`richText`。

常用配置：

```ini
[CustomLocalization]
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

使用建议：

- 优先使用 exact 或 ignore-case 规则，它们开销最低，也最容易维护。
- 仅在确有必要时启用 regex，并控制规则数量和表达式复杂度。
- regex 发生超时后会被禁用，并只记录一次 warning。
- `fontSize` 会限制在 `4..128` 范围内。
- `color` 支持 HTML 颜色格式，例如 `#FFCC00` 或 `#FFCC00FF`。
- 需要字面量 `=`、`|` 或反斜杠时，请写作 `\=`、`\|`、`\\`。
- TMP rich text 标签内的 `=` 不会被当作规则分隔符。

### 安装与排查

- 使用 Thunderstore 或 r2modman 安装时，请先安装 `BepInExPack`。
- 运行时资源从 `V81TestChn.dll` 所在目录解析，兼容 Thunderstore 和 r2modman 的嵌套安装路径。
- 如果日志显示 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`，通常说明插件 DLL 已加载，但资源目录未被找到。
- 如果输入文本被错误翻译，请优先检查终端输入、聊天输入、玩家名和大厅动态文本保护逻辑。
- 如果自定义本地化规则导致性能问题，请先禁用 regex，再逐步缩小规则范围。

### 许可与鸣谢

本项目以 MIT 协议发布。项目包含或改编了部分第三方 MIT 内容，并分发基于 OFL 字体生成的 TextMeshPro 字体资源。详细归属与分发说明见 `THIRD_PARTY_LICENSES.md`。

## English

LC Chinese Project is a Simplified Chinese localization mod maintained for the Lethal Company V81 test environment. It provides runtime text localization, Chinese TextMeshPro font fallback, selected localized UI textures, and compatibility handling for common UI, icon, and dynamic-text scenarios.

The project does not require GameTranslator at runtime. Text replacement, dynamic UI post-processing, font fallback, texture replacement, and compatibility logic are implemented by this BepInEx plugin.

### Scope

- Localizes in-game UI, HUD, terminal pages, store pages, scan prompts, ship monitor text, planet information, endgame screens, lobby warnings, and selected scene text.
- Covers dynamic text such as terminal orders, planet weather, scanner values, chat system messages, votes, days left, weight units, suit-change prompts, and vehicle interaction prompts.
- Preserves vanilla behavior for terminal input, chat input, player names, lobby dynamic names, confirmation commands, and icon-mod item keys.
- Provides Chinese TextMeshPro fallback to reduce missing glyphs, transparent glyphs, and dynamic text rendering issues.
- Includes selected localized UI texture resources.
- Keeps RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons compatible by preserving vanilla English item keys for icon matching while translating display text separately.
- Adds custom localization support through standalone `.cfg` files.

### Custom Localization

The recommended directory for personal rules and additional English-mod rules is:

```text
BepInEx/config/V81TestChn/custom-localization/
```

The plugin also scans these directories:

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
BepInEx/config/translations/custom/
```

Example rules:

```ini
# Exact match
exact:Company Cruiser=公司巡航车
Bee Suit=蜜蜂套装

# Case-insensitive exact match
ignorecase:Pull switch=拉动开关
i:Push=推动

# Regex replacement, disabled by default
regex:^(\d+) lb$=$1 磅
r:^\s*Random seed:\s*(\d+)\s*$=随机种子：$1

# Style rules
style:exact:WARNING|color=#FF4D4D|fontSize=28|richText=true
style:ignorecase:discount|color=#FFD447
```

Rule prefixes:

- No prefix or `exact:`: case-sensitive exact match.
- `ignorecase:` or `i:`: case-insensitive exact match.
- `regex:` or `r:`: regex replacement. This is disabled by default and must be enabled explicitly.
- `style:`: applies component style to matching text. Supported keys are `color`, `fontSize`, and `richText`.

Common options:

```ini
[CustomLocalization]
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

Guidance:

- Prefer exact or ignore-case rules. They are the lowest-cost and most predictable options.
- Enable regex only when needed, and keep patterns simple and bounded.
- A regex rule is disabled after a timeout and logs one warning.
- `fontSize` is clamped to `4..128`.
- `color` accepts HTML color values such as `#FFCC00` or `#FFCC00FF`.
- Use `\=`, `\|`, and `\\` for literal `=`, `|`, and backslash characters.
- `=` characters inside TMP rich text tags are not treated as rule separators.

### Installation And Troubleshooting

- Install `BepInExPack` first when using Thunderstore or r2modman.
- Runtime resources are resolved from the directory containing `V81TestChn.dll`, which supports Thunderstore and r2modman nested install layouts.
- If logs show `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`, the plugin DLL loaded but the resource folders were not found.
- If input text is translated unexpectedly, check terminal input, chat input, player-name, and lobby dynamic-text protections first.
- If custom localization rules cause performance issues, disable regex first and narrow the affected rules gradually.

### License And Credits

The project is released under the MIT License. It includes or adapts selected third-party MIT content and distributes TextMeshPro font assets generated from OFL-licensed fonts. See `THIRD_PARTY_LICENSES.md` for attribution and distribution notes.
