# 更新日志 / Changelog

## 中文

### 0.2.1 - 性能、观战状态与文本覆盖更新

- 降低右键扫描、富文本消息刷新和高频 UI 文本更新时的短暂卡顿风险，减少全局文本路径中的重复组件分类与 HUD 扫描源节点处理。
- 改进全局文本保护逻辑，使终端输入、聊天输入、玩家名、大厅动态文本和聊天输出等场景的识别更稳定。
- 修复增强观战和结算界面中玩家名与死亡状态文本的边界识别，避免玩家名被状态翻译逻辑误处理。
- 加强退出、场景切换和插件销毁阶段的运行时清理，降低残留 patch 或审计协程在销毁期间触发异常的风险。
- 补充 `Cookie pan` 物品名和 host 关闭房间后说明文本的汉化覆盖。
- 优化断线、连接失败和大厅状态类弹窗文案，改为更短的无标点排版，并主动换行以减少自动折行造成的显示割裂。

### 0.2.0 - 自定义本地化与运行时稳定性更新

- 新增自定义本地化支持：可通过独立 `.cfg` 文件扩展文本替换规则。
- 自定义本地化支持 exact、ignore-case、regex 和 style 规则；regex 默认关闭，适合在确认规则可靠后按需启用。
- 为自定义本地化增加文件大小、加载数量、规则数量和 regex 超时限制，降低错误规则造成卡顿或日志刷屏的风险。
- 优化运行时文本收集和扫描 HUD 的处理节奏，减少长文本、富文本和右键扫描场景下的短暂卡顿。
- 拆分动态文本翻译路径，按终端、HUD、聊天、结算、星球信息、控制提示等场景分别处理，降低高频全局文本路径的负担。
- 补全服装切换光标提示中的服装名汉化，例如 `Change: Bee Suit` 可显示为中文服装名。
- 保持终端输入、聊天输入、玩家名、大厅动态名和图标类模组物品 key 的保护逻辑，减少误翻译和兼容性问题。

### 0.1.7 - Signal Translator HUD 更新

- 增加缓存和节流后的 Signal Translator HUD 汉化路径，减少短窗口内的重复文本层级遍历。
- 放大本地化后的“正在接收信号”提示，同时保留后续信号文本的原始字号。
- 补全 Cruiser、飞船磁铁、信号翻译器和订单状态相关终端文本。
- 使用当前运行时构建刷新 Thunderstore 包体和 GitHub 发布元数据。

## English

### 0.2.1 - Performance, spectate status, and text coverage update

- Reduced short stutter risk during right-click scanning, rich-text message refreshes, and high-frequency UI text updates by cutting repeated component classification and HUD scanner source-node work.
- Improved global text protection so terminal input, chat input, player names, lobby dynamic text, and chat output are classified more reliably.
- Fixed boundary detection between player names and dead-status labels in enhanced spectate and endgame screens, preventing player names from being processed as status text.
- Strengthened runtime cleanup during quit, scene transition, and plugin teardown, reducing the chance of leftover patches or audit coroutines running during destruction.
- Added localization coverage for the `Cookie pan` item name and the host closed-lobby explanation.
- Refined disconnect, connection failure, and lobby status popup wording with shorter punctuation-free lines and explicit line breaks to reduce awkward automatic wrapping.

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
