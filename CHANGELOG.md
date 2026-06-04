# 更新日志 / Changelog

## 中文

### 3.1.2 - 汉化补全更新

相较 3.1.1，补全部分原版 HUD 与终端动态文本的汉化覆盖，减少交互提示、右键扫描和订单反馈中的英文残留。

- 补全灯光开关、梯子、飞船移动状态和建造模式等 HUD 交互提示。
- 补全 Company Cruiser 扫描提示和终端订单反馈中的动态文本。
- 保持轻量化处理方式，不扩大高频全局扫描范围。

### 3.1.1 - 东亚字符显示与大厅列表稳定性更新

相较 3.1.0，重点改进混合语言大厅、玩家名称、聊天消息和第三方运行时文本中的日韩文字与补充符号显示，同时继续降低高频 TMP 文本路径的重复处理成本。

- 补充基于系统字体的日文、韩文等非中文东亚字符 fallback，使大厅名、玩家名、聊天消息和第三方运行时文本中原本可能缺字或反复刷日志的字符能够更稳定地显示。
- 改进混合中文、日文、韩文文本的分类逻辑，仅在需要时应用字体 fallback，避免将玩家自定义文本、大厅名或跨语言消息误当作可翻译内容处理。
- 优化高频 TMP 与字体 fallback 路径，复用东亚字符分类结果，减少同一帧内的重复字符扫描和 fallback 判断。
- 收窄大厅列表字号补偿范围，仅针对大厅名称中的非 ASCII 文本提供保守的最小字号保护，避免全局字体缩放影响普通 UI。
- 改进 HUD 聊天输出的处理时机，降低短时间大量系统消息、玩家消息或富文本彩色消息在输入和发送阶段触发同步处理冲突并导致闪退的风险。

### 3.1.0 - 覆盖稳定性与大型整合包性能更新

重点提升大型整合包环境下的运行效率、动态界面显示稳定性，以及常见第三方 UI 文本的兼容表现。

- 优化大型整合包下的运行时文本处理路径，减少高频 TMP 文本、目标 UI、扫描 HUD、字体 fallback 等场景中的重复处理，并新增可配置缓存与菜单处理预算。
- 改进原版动态 UI 的显示一致性，降低星球信息、扫描 HUD、交互提示等界面出现旧文本残留、短暂英文回退或中英混排的概率。
- 提升菜单和配置类界面的中文显示稳定性，改善固定宽度输入框、搜索框和占位文本在中文环境下的裁切与排版问题。
- 扩展通用第三方文本兼容能力，使价格、折扣、战斗提示、HUD 通知等运行时英文文本能够更稳定地通过通用文本路径汉化。
- 保持轻量化运行方式，在提升覆盖与兼容性的同时控制额外性能开销。

### 3.0.0 - 独立运行时本地化更新

- 移除对既有第三方汉化和字体修补运行时链路的依赖，文本替换、字体 fallback、贴图替换与兼容逻辑均由本插件独立处理。
- 切换到自维护的干净运行时词库，覆盖物品、怪物、终端、HUD、星球信息、交互提示、设置菜单、联机大厅和结算界面等常用场景。
- 新增默认关闭的自动翻译功能，可通过配置接入 HTTP 翻译提供方，并使用缓存、长度限制、并发限制和后台请求避免阻塞主线程。
- 重做中文 TextMeshPro fallback 与本地化贴图资源，改善菜单、弹窗、感染提示、结算画面和飞船提示的显示稳定性。
- 改进图标类模组兼容处理，保留原版英文物品 key 供图标匹配使用，仅在显示层输出中文名称。
- 补全并统一多处用户可见译名和交互提示，包含怪物/物品术语、加入房间、房间可见性、调试菜单、飞船降落、公司解雇和绩效报告等界面。

### 0.2.3 - 修复 0.2.2 必要运行时文件缺失

- 修复 0.2.2 发布包中缺失的必要运行时文件，该问题可能导致插件部分功能异常或无法正常加载。
### 0.2.2 - 菜单稳定性、配置可读性与文本覆盖更新

- 改进快捷菜单、游戏设置和退出确认界面的本地化时机，减少首次打开或反复打开时的峰值卡顿，并降低首帧短暂显示英文原文的概率。
- 优化运行时字体与材质保护，降低交互提示、菜单按钮和大厅 UI 出现黑色字、半透明字或方块字的风险。
- 补充公司星球说明、观战状态、退出确认弹窗、提前离船投票警告和 Performance Report notes 等原版动态文本覆盖。
- 改进游戏设置、调试菜单和弹窗类 UI 的目标后处理，减少全局文本路径承担的高频工作。
- 发布配置默认分组改为中文大类与小类，并将配置说明改为中文；自定义正则、诊断日志、未翻译文本收集和字体材质调整仍保持默认关闭。

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

### 3.1.2 - Localization coverage update

Compared with 3.1.1, this update completes additional vanilla HUD and terminal dynamic text coverage, reducing leftover English in interaction prompts, right-click scans, and order feedback.

- Added coverage for light switches, ladders, ship-in-motion status, and build-mode HUD prompts.
- Added coverage for Company Cruiser scan text and terminal order feedback.
- Kept the handling lightweight without widening high-frequency global scans.

### 3.1.1 - East Asian glyph display and lobby list stability update

Compared with 3.1.0, this update improves Japanese, Korean, and supplemental-symbol display in mixed-language lobbies, player names, chat messages, and third-party runtime text while further reducing repeated work in high-frequency TMP text paths.

- Added system-font-based fallback for Japanese, Korean, and other non-Chinese East Asian glyphs, improving display stability for lobby names, player names, chat messages, and third-party runtime text that could previously miss glyphs or repeatedly log fallback warnings.
- Improved classification for mixed Chinese, Japanese, and Korean text so font fallback is applied only when needed, reducing the chance of custom player text, lobby names, or cross-language messages being treated as translatable content.
- Optimized high-frequency TMP and font fallback paths by reusing East Asian glyph classification results, reducing repeated character scans and fallback checks within the same frame.
- Narrowed lobby-list font-size compensation to a conservative minimum-size guard for non-ASCII lobby names, avoiding global font scaling side effects on ordinary UI.
- Improved HUD chat output timing to reduce crash risk when rapid system messages, player messages, or rich-text colored chat messages collide with synchronous processing during input and send flows.

### 3.1.0 - Coverage stability and large modpack performance update

Focused on runtime efficiency in large modpacks, more stable dynamic UI display, and better compatibility for common third-party UI text.

- Optimized runtime text processing in large modpacks by reducing repeated work in high-frequency TMP text, targeted UI, scanner HUD, and font fallback paths, with configurable cache limits and menu work budgets.
- Improved vanilla dynamic UI consistency, reducing stale text, brief English fallback, and mixed-language output in planet information, scanner HUD, and interaction tip displays.
- Improved Chinese display stability in menu and configuration UI, especially fixed-width input fields, search fields, and placeholder text.
- Expanded generic third-party text compatibility so price, discount, combat, and HUD notification text can be localized more reliably through shared runtime text paths.
- Kept runtime behavior lightweight, controlling extra performance overhead while improving coverage and compatibility.

### 3.0.0 - Independent runtime localization update

- Removed runtime reliance on existing third-party translation and font-patching runtimes; text replacement, font fallback, texture replacement, and compatibility logic are now handled by this plugin.
- Switched to a maintained clean runtime glossary covering items, enemies, terminal pages, HUD text, moon information, interaction prompts, settings, lobbies, and endgame screens.
- Added an optional automatic translation feature, disabled by default, with an HTTP provider hook, cache, text-length limits, pending-request limits, and background requests to avoid main-thread stalls.
- Rebuilt Chinese TextMeshPro fallback and localized texture resources to improve menu, popup, infection HUD, endgame, and ship-message display stability.
- Improved icon-mod compatibility by preserving vanilla English item keys for matching while translating display text separately.
- Completed and standardized visible wording for item/enemy names, join-room flow, room visibility, debug menu actions, ship landing prompts, company-fired screens, and performance reports.

### 0.2.3 - Fix missing required runtime files from 0.2.2

- Fixed missing required runtime files in the 0.2.2 release package, which could cause partial plugin malfunction or failure to load.
### 0.2.2 - Menu stability, readable configuration, and text coverage update

- Improved localization timing for the quick menu, game settings, and leave-game confirmation screens, reducing peak stutter on first open or repeated opens and lowering the chance of a one-frame English flash.
- Strengthened runtime font and material safeguards to reduce black, translucent, or square glyph regressions in interaction prompts, menu buttons, and lobby UI.
- Added coverage for the Company moon description, spectate status, leave-game confirmation popup, early-leave vote warning, and vanilla Performance Report notes.
- Improved targeted post-processing for game settings, debug menus, and popup UI so less work falls back to global high-frequency text paths.
- Changed generated release cfg sections to Chinese category groups with Chinese descriptions. Custom regex, diagnostic logging, untranslated text collection, and font material tweaks remain disabled by default.

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
