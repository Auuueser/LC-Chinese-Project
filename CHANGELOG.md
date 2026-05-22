# 更新日志 / Changelog

## 中文

### 0.2.1 - 运行时性能与发布整理更新

- 优化全局文本保护路径，缓存输入框、聊天输出、大厅动态文本等组件分类，减少高频 setter 中的重复层级查询。
- 对 HUD 扫描源节点翻译增加节流，降低右键扫描和富文本刷新场景下的短暂卡顿风险。
- 调整插件销毁与清理顺序，在正式 shutdown 时优先移除 Harmony patch，并补充辐射警告审计协程的停止路径。
- 收窄增强观战和结算 UI 的死亡状态翻译范围，避免将真实玩家名误判为可翻译状态文本。
- 补充 `Cookie pan` 和 host 关闭房间说明的汉化规则。
- 同步 GitHub 与 Thunderstore 文档、版本号和发布元数据，保持发布集合克制、清晰。

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

### 0.2.1 - Runtime performance and release cleanup update

- Optimized global text guard paths by caching component classification for inputs, chat output, lobby dynamic text, and related UI components.
- Added throttling for HUD scanner source-node localization to reduce short stutter risk during right-click scanning and rich-text refreshes.
- Adjusted plugin shutdown order so Harmony patches are removed first during real shutdown, and added a shutdown path for the radiation warning audit coroutine.
- Narrowed enhanced spectate and endgame dead-status localization so real player names are not treated as translatable status text.
- Added localization coverage for `Cookie pan` and the host closed-lobby explanation.
- Synchronized GitHub and Thunderstore documentation, version metadata, and release structure with a more restrained publish set.

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
