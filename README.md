# LC Chinese Project

## 中文说明

LC Chinese Project 是面向 **Lethal Company V81 测试环境**维护的简体中文本地化项目。项目提供运行时文本汉化、中文 TextMeshPro 字体 fallback、部分 UI 贴图本地化，以及针对常见 UI/图标类模组的兼容处理。

本项目不依赖 GameTranslator 运行时。文本翻译、动态 UI 处理、字体 fallback、贴图替换和兼容逻辑均由本插件在 BepInEx 环境中完成。

### 功能范围

- 汉化游戏内 UI、HUD、终端、商店、扫描提示、飞船显示屏、星球信息、结算界面、大厅提示和部分场景文本。
- 针对终端订单、星球信息、扫描价值、聊天系统消息、投票、截止日期、重量单位、服装切换提示和载具交互提示等动态文本提供专用处理。
- 保留终端输入、聊天输入、玩家名、大厅动态名和确认命令的原版行为。
- 提供中文 TMP 字体 fallback，降低缺字、透明字和动态文本渲染问题。
- 提供部分本地化 UI 贴图资源。
- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations 和 HoneeItemIcons，保留原版英文物品 key 供图标匹配使用，仅在显示层处理中文。
- 支持自定义本地化条目，便于为其他英文模组或个人偏好补充显示文本。

### 自定义本地化教程

插件会在以下目录中查找自定义 `.cfg` 文件：

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
BepInEx/config/translations/custom/
```

推荐优先使用 `BepInEx/config/V81TestChn/custom-localization/`。该目录不会随插件更新被覆盖，适合保存个人规则或其他英文模组的补充汉化。

自定义规则示例：

```ini
# 精确匹配
exact:Company Cruiser=公司巡航车
Bee Suit=蜜蜂套装

# 忽略大小写匹配
ignorecase:Pull switch=拉动开关
i:Push=推动

# 正则匹配，默认不会启用
regex:^(\d+) lb$=$1 磅
r:^\s*Random seed:\s*(\d+)\s*$=随机种子：$1

# 样式规则
style:exact:WARNING|color=#FF4D4D|fontSize=28|richText=true
style:ignorecase:discount|color=#FFD447
```

规则前缀说明：

- 无前缀或 `exact:`：区分大小写的精确匹配。
- `ignorecase:` 或 `i:`：忽略大小写的精确匹配。
- `regex:` 或 `r:`：正则替换。该功能默认关闭，需要在配置中显式启用。
- `style:`：对匹配到的文本组件应用样式，可设置 `color`、`fontSize`、`richText`。

自定义本地化配置项位于：

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

- 优先使用 exact 或 ignore-case 规则，它们开销最低，也最稳定。
- 仅在确有必要时启用 regex，并控制规则数量和表达式复杂度。
- regex 发生超时后会被禁用，并只记录一次警告。
- `fontSize` 会限制在 `4..128` 范围内。
- `color` 支持 HTML 颜色格式，例如 `#FFCC00` 或 `#FFCC00FF`。
- 如果规则中需要字面量 `=`、`|` 或反斜杠，请写作 `\=`、`\|`、`\\`。
- TMP rich text 标签内的 `=` 不会被当作规则分隔符。

### 安装与排查

- 运行时资源从 `V81TestChn.dll` 所在目录解析，兼容 Thunderstore 和 r2modman 的嵌套安装方式。
- 如果日志显示 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`，通常说明插件 DLL 已加载但资源目录未被找到。
- 如果输入文本被错误翻译，请优先检查终端输入、聊天输入、玩家名和大厅动态文本保护逻辑。
- 如果自定义本地化规则导致性能问题，请先禁用 regex，并逐步缩小规则范围。

### 许可与鸣谢

本项目以 MIT 协议发布。项目包含或改编了部分第三方 MIT 内容，并分发基于 OFL 字体生成的 TextMeshPro 字体资源。详细归属与分发说明见 `THIRD_PARTY_LICENSES.md`。

## English

LC Chinese Project is a Simplified Chinese localization project maintained for the **Lethal Company V81 test environment**. It provides runtime text localization, Chinese TextMeshPro font fallback, selected localized UI textures, and compatibility handling for common UI and icon mods.

The project does not require GameTranslator at runtime. Text translation, dynamic UI handling, font fallback, texture replacement, and compatibility logic are implemented by this BepInEx plugin.

### Scope

- Localizes in-game UI, HUD, terminal pages, store pages, scan prompts, ship monitor text, planet information, endgame screens, lobby warnings, and selected scene text.
- Provides targeted dynamic handling for terminal orders, planet information, scanner values, chat system messages, votes, deadlines, weight units, suit-change prompts, and vehicle/control hints.
- Preserves vanilla behavior for terminal input, chat input, player names, lobby dynamic names, and confirmation commands.
- Provides Chinese TMP font fallback to reduce missing glyphs, transparent glyphs, and dynamic text rendering issues.
- Includes selected localized UI texture resources.
- Keeps RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons compatible by preserving vanilla English item keys for icon matching while translating display text separately.
- Supports custom localization entries for additional English mods or personal display-text preferences.

### Custom Localization Guide

The plugin loads custom `.cfg` files from these directories:

```text
BepInEx/plugins/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-localization/
BepInEx/config/V81TestChn/custom-translations/
BepInEx/config/translations/custom/
```

`BepInEx/config/V81TestChn/custom-localization/` is recommended for personal rules because it is not overwritten by plugin updates.

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

Custom localization options:

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

- Runtime resources are resolved from the directory containing `V81TestChn.dll`, which supports Thunderstore and r2modman nested install layouts.
- If logs show `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`, the plugin DLL loaded but the resource folders were not found.
- If input text is translated unexpectedly, check terminal input, chat input, player-name, and lobby dynamic-text protections first.
- If custom localization rules cause performance issues, disable regex first and narrow the affected rules gradually.

### License And Credits

The project is released under the MIT License. It includes or adapts selected third-party MIT content and distributes TextMeshPro font assets generated from OFL-licensed fonts. See `THIRD_PARTY_LICENSES.md` for attribution and distribution notes.
