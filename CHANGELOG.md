# 更新日志 / Changelog

## 中文

### 0.2.0 - 自定义本地化与运行时稳定性更新

- 新增自定义本地化支持：可通过独立 `.cfg` 文件扩展文本替换规则。
- 自定义本地化支持 exact、ignore-case、regex 和 style 规则；regex 默认关闭，适合在确认规则可靠后按需启用。
- 为自定义本地化增加文件大小、加载数量、规则数量和 regex 超时限制，降低错误规则造成卡顿或日志刷屏的风险。
- 优化运行时文本收集和扫描 HUD 的处理节奏，减少长文本、富文本和右键扫描场景下的短暂卡顿。
- 拆分动态文本翻译路径，按终端、HUD、聊天、结算、星球信息、控制提示等场景分别处理，降低高频全局文本路径的负担。
- 补全服装切换光标提示中的服装名汉化，例如 `Change: Bee Suit` 可显示为中文服装名。
- 保持终端输入、聊天输入、玩家名、大厅动态名和图标类模组物品 key 的保护逻辑，减少误翻译和兼容性问题。

### 0.1.7 - 信号翻译器 HUD 更新

- 增加缓存和节流后的 Signal Translator HUD 汉化路径，减少短窗口内的重复文本层级遍历。
- 放大本地化后的“正在接收信号”提示，同时保留后续信号文本的原始字号。
- 补全 Cruiser、飞船磁铁、信号翻译器和订单状态相关终端文本。
- 使用当前运行时构建刷新 Thunderstore 包体和 GitHub 发布元数据。

### 0.1.6 - V81 状态文本和大厅警告更新

- 增加 V81 尸体感染和空气过滤器状态文本。
- 增加感染温度单位配置，默认摄氏度，可在配置文件中切换为华氏度。
- 汉化加入房间时的修改版主机警告，并调整换行以贴近原版提示布局。
- 刷新发布元数据和 Thunderstore 包体。

### 0.1.5 - 终端确认输入修复

- 修复终端页面翻译改写导致原版输入跟踪被破坏的问题。
- 恢复购买和导航确认页面中的 `c` / `confirm` 行为。
- 增加终端页面改写和玩家输入跟踪的静态回归覆盖。
- 重新构建 Thunderstore 运行时包体。

### 0.1.4 - RuntimeIcons 兼容更新

- 兼容 RuntimeIcons、RuntimeIcons_BetterRotations 和 HoneeItemIcons，保留原版英文物品 key 用于图标匹配。
- 为丢弃提示和相关 HUD 文本增加显示层物品名汉化，不修改底层物品定义。
- 说明兼容层不打包、不引用、不复制第三方图标模组代码或资源。
- 兼容诊断默认关闭，避免重复日志噪音。

### 0.1.3 - V81 打包结构更新

- 说明 Thunderstore zip 根目录结构，避免额外套一层 `package/`。
- 补充手动安装和模组管理器安装的区别。
- 说明运行时资源必须从 `V81TestChn.dll` 所在目录解析，以兼容管理器嵌套安装路径。
- 增加 `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).` 的排查说明。
- 增加旧错误包目录如 `BepInEx/plugins/package` 的清理说明。

## English

### 0.2.0 - Custom localization and runtime stability update

- Added custom localization support: standalone `.cfg` files can extend text replacement rules.
- Custom localization supports exact, ignore-case, regex, and style rules. Regex rules are disabled by default and should be enabled only after the rule is verified.
- Added file size, loaded file count, rule count, and regex timeout limits for custom localization, reducing the risk of stutter or repeated log noise from faulty rules.
- Improved runtime text collection and scan HUD processing cadence to reduce short stutters around long text, rich text, and right-click scanning.
- Split dynamic text handling by domain, including terminal, HUD, chat, endgame, planet information, and control prompts, reducing work in global high-frequency text paths.
- Completed suit-change hover prompt localization, so prompts such as `Change: Bee Suit` can display localized suit names.
- Preserved protections for terminal input, chat input, player names, lobby dynamic names, and icon-mod item keys to reduce mistranslation and compatibility issues.

### 0.1.7 - Signal Translator HUD update

- Added a cached and throttled Signal Translator HUD localization path to reduce repeated text hierarchy traversal during the short activation window.
- Enlarged the localized `RECEIVING SIGNAL` display while preserving original font sizes for subsequent signal messages.
- Completed translation coverage for recent Cruiser, ship magnet, Signal Translator, and order-status terminal text.
- Rebuilt the Thunderstore payload from the current runtime build and synchronized GitHub release metadata.

### 0.1.6 - V81 status text and lobby warning update

- Added V81 cadaver infection and air-filter status translations.
- Added configurable infection temperature display; Celsius is used by default, with Fahrenheit available through the plugin config.
- Localized the join-lobby modified-host warning and manually wrapped it to better match the vanilla tooltip layout.
- Updated release metadata and rebuilt the Thunderstore payload from the current runtime build.

### 0.1.5 - Terminal confirmation input fix

- Fixed terminal screen translation rewrites so vanilla input tracking is preserved after translated terminal pages load.
- Restored confirmation shortcut behavior for terminal purchase and route confirmation pages, including `c` / `confirm` flows.
- Added static regression coverage for terminal screen rewrites and player input tracking.
- Rebuilt the Thunderstore payload from the current runtime build.

### 0.1.4 - Runtime icon compatibility update

- Added compatibility handling for RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons by preserving vanilla English item keys for icon matching while translating display text separately.
- Added display-only item-name translation for drop prompts and related HUD text without mutating the underlying item definition.
- Documented that the compatibility layer does not bundle, reference, or copy third-party mod code or assets.
- Kept compatibility diagnostics disabled by default to avoid repeated runtime log noise.

### 0.1.3 - V81 packaging update

- Documented the required Thunderstore zip root layout to avoid accidental `package/` nesting.
- Documented manual install and mod-manager install differences.
- Documented the resource-root requirement: runtime assets must be resolved from the `V81TestChn.dll` directory because mod managers may use nested install folders.
- Added troubleshooting notes for `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`
- Added cleanup notes for old broken-package remnants such as `BepInEx/plugins/package`.
