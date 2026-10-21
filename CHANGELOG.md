# Changelog / 更新日志

## 2.10.0 - 2026-10-21 - Scope Reset & Fixes / 范围收窄与集中修复

- **范围调整 / Scope**：删除 AI 模组排除名单（画廊不再联网）和运行时修改追踪；查询筛选去掉条件状态、季节、天气、时间和心数。 / Removed the AI mod exclusion list (the gallery no longer goes online) and runtime modification tracking; query filters no longer cover condition status, season, weather, time or hearts.
- **回放存档安全 / Replay Safety**：背包不再克隆，鱼饵、钓具和弹药不会丢失；季节只改事件画面，不再清空果实和肥料；失焦与暂停不计入超时；节日会场当天不可回放；回放期间不触发真实事件与 TriggerAction；快照补全 Buff、统计、已听歌曲、已访问地点和壁炉。 / Inventory is no longer cloned, so bait, tackle and ammo survive; season changes are visual only, so fruit and fertilizer stay; unfocused or paused time doesn't count toward timeouts; festival venues can't be replayed on festival days; real events and trigger actions don't run during replay; the snapshot also covers buffs, stats, songs, visited locations and fireplaces.
- **剧照与手柄 / Photos & Controller**：100% 缩放下的抓拍不再透明，照片不含跳过按钮；切换到手柄后页面正常，关闭吸附菜单时 A 作用于光标处，输入框弹出屏幕键盘。 / Photos at 100% zoom are no longer transparent and exclude the skip button; pages keep working after switching to a controller, `A` follows the cursor with snappy menus off, and text fields open the on-screen keyboard.
- **搜索与输入 / Search & Input**：在搜索框按回车只列出候选，再按回车或 `A` 才进入；Steam Deck 和 Steam 大屏幕模式下改用 Steam 键盘，可以输入中文；中文界面的角色搜索和事件查询支持拼音（全拼、首字母或混合）。 / Enter in a search box lists the matches, and a second Enter or `A` opens one. On Steam Deck and in Steam Big Picture mode, text fields use Steam's keyboard, which can type Chinese. In Chinese, character search and Event Search also match pinyin (full, initials or mixed).
- **打字隔离 / Typing Isolation**：在输入框打字时，按键仍不会触发游戏和其他 Mod 的快捷键，但改用游戏自带的方式实现，不再补丁 SMAPI 的内部方法，SMAPI 更新后搜索框也不会因此停用；实体键盘按回车结束搜索后，不会再顺带打开第一个结果。 / Keys typed into a text field still don't trigger game or mod shortcuts, now through the game's own mechanism instead of a patch on SMAPI internals, so a SMAPI update can no longer disable search; pressing Enter on a physical keyboard to finish a search no longer also opens the first match.
- **手柄流畅度 / Controller Responsiveness**：页面不再每帧遍历所有地点查找角色、读取存档目录与节日数据、重新排版条件文字，摇杆和按键更跟手；开启调试诊断时，日志会记录画廊的帧耗时。 / Pages no longer search every location for characters, read the save folder and festival data, or re-wrap condition text every frame, so the stick and buttons respond faster. With debug diagnostics on, the log records the gallery's frame times.
- **条件判定 / Conditions**：`D`、`R`、多季节、`!!`、加引号的 GSQ、`WEATHER Here` 与事件 ID 的读取都与游戏 1.6.15 一致；“未看过”的引用不再算前置。 / `D`, `R`, multi-season lists, `!!`, quoted GSQ, `WEATHER Here` and event IDs now match Stardew Valley 1.6.15; "not seen" references are no longer prerequisites.
- **目录与来源 / Catalog & Sources**：Penny 拖车剧情不再重复，旧条目的照片和名称自动合并；Linux / Steam Deck 大小写、链接目录部署可识别；原包读取不全时显示“证据不完整”；列出其他声明者；只重扫变化的包。 / Penny's trailer events are listed once and photos and names on old duplicates are merged; case-mismatched paths on Linux/Steam Deck and linked mod folders are supported; incomplete scans show "evidence incomplete"; other declaring packs are listed; only changed packs are rescanned.
- **其他 / Other**：联机农场工一键解锁只在本次会话生效；分屏玩家状态独立；名称文件损坏时回退到备份；Portraiture 失败时先用普通肖像；发布包约减小三分之一。 / Farmhand Unlock All is session-only; split-screen players are independent; damaged name files fall back to a backup; Portraiture failures show the regular portrait first; the package is about a third smaller.

详见 [2.10.0 版本说明](docs/RELEASE_2.10.0_NOTES.md)。 / See the [2.10.0 Release Notes](docs/RELEASE_2.10.0_NOTES.md) for details.

## 2.9.0 - 2026-10-08 - Replay Fixes & Cleanup / 回放修复与依赖清理

- **GMCM 名单开关 / GMCM Exclusion Toggle**：将 AI 模组排除开关移入 GMCM 与 `config.json`（`EnableAiModExclusion`，默认 `false`），保存后在非回放状态下直接生效，并自动清理旧版独立的 `ai-mod-exclusion.json`。 / Moved the AI mod exclusion setting into GMCM and `config.json` (`EnableAiModExclusion`, default `false`). Changes apply once any active replay or confirmation finishes, and the retired `ai-mod-exclusion.json` is removed automatically.
- **移除遗留依赖 / Legacy Dependency Cleanup**：彻底移除早期已停用的历史快照、预览注入代码及 SQLite 依赖库，发布包体积缩减约 50%；现有回放、条件解析、剧照封面、自定义名称与存档保护不受影响。 / Removed retired historical collection, preview injection code, and SQLite dependencies, cutting package size by roughly 50% while keeping current replay, condition checks, photos, custom names, and save protection intact.
- **快照大小写保留 / Case-Sensitive State Snapshots**：回放快照与条件读取严格区分事件、信件、对话、配方及好感键名的大小写，防止恢复存档状态时丢失标记或因同名异形键报错。 / Preserved case-sensitive keys for events, mail, dialogue, recipes, and friendship during replay snapshots and condition reads.
- **爷爷评价保护 / Grandpa Evaluation Guard**：仅在画廊触发的回放中拦截 `GrandpaCandles` 状态变更，避免回放改写真实农场的爷爷评价蜡烛数，同时不影响正常游戏流程。 / Suppressed `GrandpaCandles` state changes only during gallery-owned replays so real farm evaluation scores are never overwritten.
- **倍速与绿雨修复 / Playback Speed & Green Rain**：修复嵌套事件更新导致 2x / 4x 倍速被重复叠加放大的问题；回放环境现支持绿雨（Green Rain），且普通雨天条件不会强行覆盖当前的绿雨天气。 / Applied 2x/4x speed once per outer event update with exception-safe cleanup, and added Green Rain support without downgrading active Green Rain when an event only requires rain.
- **条件筛选与好感归类 / Condition & Heart Filters**：支持解析常见的 Game State Query (GSQ) 季节、日期、天气与好感条件（含否定与组合逻辑），统一角色别名的心数筛选。 / Added support for common GSQ season, date, weather, and relationship filters (including negated and combined rules) and unified character alias handling in heart filters.
- **手柄单次触发 / Controller Input Fix**：修复详情页、工具栏与剧照管理页中手柄 `A` 键被重复触发的问题，避免一次按键展开两次面板或连删两张剧照。 / Fixed controller `A` presses firing twice in detail, tool, and photo menus, preventing double toggles and accidental double photo archiving.
- **界面状态与来源展示优化 / UI State & Source Display**：目录刷新时保留未保存的事件改名与筛选草稿；已选中的角色或地点即使当前无匹配结果也保持原筛选项显示；静态来源匹配成功后仍完整显示运行时证据状态，不再截断长文本。 / Preserved unsaved rename/filter drafts across catalog refreshes, kept unavailable character/location filter selections visible, and displayed runtime evidence status alongside static source matches without truncating translated text.
- **内置名单兜底 / Bundled Exclusion Fallback**：内置排除名单文件异常超限时安全回退，不残留旧会话规则且不影响后续异步更新。 / Added safe fallback when the bundled exclusion list exceeds size limits without retaining stale session rules or blocking updates.

详见 [2.9.0 版本说明](docs/RELEASE_2.9.0_NOTES.md)。 / See the [2.9.0 Release Notes](docs/RELEASE_2.9.0_NOTES.md) for update instructions and details.

## 2.8.0 - 2026-09-21 - Source Filters & Original Definitions / 来源筛选与原始定义识别

- **按来源搜索与筛选 / Source Search & Filter**：事件查询新增“来源”筛选，支持按原版、指定模组、未知或多候选来源过滤，搜索框也可直接检索模组名称与 Mod ID。 / Added source filters to Event Search (base game, specific mod, unknown, or multiple candidates) and support for searching by mod name or Mod ID.
- **静态扫描原始定义 / Automatic Source Scanning**：无需开启运行时追踪，首次打开画廊时自动扫描已加载的 Content Patcher 内容包（支持 `Include` / `FromFile`）、AliveNpcs 场景及原版 XNB，按“资源路径 + 事件 ID”匹配原始提供者与定义文件。 / Automatically scanned loaded Content Patcher packs (following `Include`/`FromFile`), AliveNpcs scenes, and vanilla XNBs to identify original providers and files without requiring runtime tracing.
- **AI 模组排除名单 / AI Mod Exclusion List**：支持按公开的 AI Mod Exclusion 名单隐藏已确认由名单内 Mod ID 原始提供的非原版事件；启动与读档时异步拉取最新名单，失败时使用内置名单兜底，并在回放结束后安全刷新目录。 / Added support for hiding events whose confirmed original provider matches the public AI Mod Exclusion list, with async updates on startup/save load and bundled fallback data.
- **SMAPI 兼容性修复 / SMAPI Compatibility Fixes**：改用公开的模组注册表与清单配对定位模组目录，解决 SMAPI 拦截内部反射导致来源为空的问题；修正 JSON 格式化调用以通过 SMAPI 程序集兼容性检查；移除失效的 Nexus 更新键，保留 GitHub 更新。 / Replaced blocked internal reflection with public mod-registry matching, fixed a JSON formatting call rejected by SMAPI assembly validation, and removed the obsolete Nexus update key.

详见 [2.8.0 版本说明](docs/RELEASE_2.8.0_NOTES.md)。 / See the [2.8.0 Release Notes](docs/RELEASE_2.8.0_NOTES.md) for details.

## 2.7.0 - 2026-09-21 - Event Sources / 事件来源查询

- **详情页来源展示 / Event Source Details**：在事件详情页“地点”下方新增来源栏，点击可切换查看事件提供者、执行框架、修改记录与证据状态。 / Added a source row below the location in event details; click to inspect the main provider, executing framework, edit chain, and evidence status.
- **可选运行时追踪 / Opt-in Runtime Tracing**：GMCM 新增“追踪事件修改（重启生效）”（`EnableEventSourceDiagnostics`，默认关闭），基于 SMAPI 4.5.2.0 记录加载期的 `Data/Events` 修改链。 / Added an opt-in GMCM setting (`EnableEventSourceDiagnostics`, off by default) targeting SMAPI 4.5.2.0 to record runtime edits to `Data/Events`.
- **读档缓存修复与多语言适配 / Cache Fix & Localization**：修复读档完成时清空已收集来源记录的问题；补齐 12 种语言文本，支持条件与来源面板独立滚动、长标识自动换行及手柄焦点导航。 / Fixed source records being cleared after save load, and added full 12-language text, independent scroll positions, long-ID wrapping, and controller navigation.

详见 [2.7.0 版本说明](docs/RELEASE_2.7.0_NOTES.md)。 / See the [2.7.0 Release Notes](docs/RELEASE_2.7.0_NOTES.md) for details.

## 2.6.0 - 2026-09-10 - Character Appearance Compatibility / 角色外观兼容

- **主流美化框架适配 / Cosmetic Framework Support**：新增独立的角色外观读取模块，跟随当前季节服装与肖像变化，兼容 **DDFC**、**Scale Up Unofficial** 与 **Portraiture** 的高清/动态立绘及小人规格。 / Added a dedicated appearance module that follows active seasonal outfits and portraits, supporting DDFC, Scale Up Unofficial, and Portraiture HD/animated formats.
- **布局自适应与失败兜底 / Adaptive Sizing & Fallbacks**：将高清立绘与缩放小人居中适配到画廊卡片内（不受对话框上方立绘偏移影响）；遇到临时读取失败或尺寸不匹配时自动重试并回退到普通肖像或默认图标。 / Fitted HD portraits and scaled sprites cleanly inside gallery cards, with automatic retry and fallback to standard portraits or the gallery icon on read failures.

详见 [2.6.0 版本说明](docs/RELEASE_2.6.0_NOTES.md)。 / See the [2.6.0 Release Notes](docs/RELEASE_2.6.0_NOTES.md) for details.

## 2.5.0 - 2026-09-10 - Story Gallery & Event Search / 剧情相册与全局查询

- **相册聚焦与独立查询 / Focused Album & Event Search**：角色主相册专注于收集好感剧情，同时新增独立的“事件查询”页面，支持按名称、ID、角色、地点检索普通剧情与只读前置事件。 / Focused character albums on heart stories and added a separate Event Search for ordinary stories and read-only prerequisite records.
- **多维度筛选与手柄支持 / Rich Filters & Controller Support**：查询页提供事件类型、角色、地点、完成进度、条件满足状态、季节、时间、天气与心数筛选，支持手柄快捷操作；合并角色外观分身与同名地点，清理爷爷评估剧情在各地图的重复副本。 / Added filters for type, character, location, progress, requirement status, season, time, weather, and hearts with full controller support; merged cosmetic NPC aliases and removed duplicate Grandpa evaluation entries across locations.
- **自定义事件名称与普通事件回放开关 / Custom Event Names & Ordinary Replay Toggle**：支持为事件设置跨存档共用的自定义名称并用于搜索；GMCM 新增“普通事件回放”开关（默认关闭）；优化回放确认弹窗的自适应换行与居中布局。 / Added cross-save custom event naming, an opt-in GMCM setting for replaying ordinary events, and improved text wrapping in the replay confirmation dialog.

详见 [2.5.0 版本说明](docs/RELEASE_2.5.0_NOTES.md)。 / See the [2.5.0 Release Notes](docs/RELEASE_2.5.0_NOTES.md) for details.

## 2.4.0 - 2026-09-08 - Condition Coverage / 条件解析扩展

- **扩展只读条件判定 / Expanded Read-Only Checks**：新增对星期、房主身份、金钱、背包物品、出货记录、技能等级、对话记录、秘密纸条、宠物、金核桃、矿井层数、社区中心/Joja 进度、NPC 可见性、房屋升级及节日日历的只读条件解析。 / Added read-only condition checks for weekday, host, money, inventory, shipments, skills, dialogue history, secret notes, pets, golden walnuts, mine depth, Community Center/Joja progress, NPC visibility, house upgrades, and festival dates.
- **支持常见状态查询与原因说明 / GSQ & Unknown Explanations**：支持解析常见被动节日、季节日期与玩家统计量条件，并为随机结果、入场坐标等无法静态预判的条件提供 12 种语言的原因说明。 / Added readable explanations in all 12 languages for supported passive festivals, dates, player stats, and dynamic conditions like random rolls or entry coordinates.

## 2.3.0 – 2.3.2 - Event Navigation & UI Polish / 事件跳转与界面优化

- **前置事件直接跳转 / Prerequisite Links**：点击详情页前置条件中的事件 ID 可直接跳转到目标事件详情，支持多级跳转并返回原页面、原滚动位置与原焦点。 / Made prerequisite event IDs clickable to jump directly to target event details while preserving back-navigation history, scroll position, and focus.
- **非好感事件查找与首页对齐 / Non-Heart Lookup & Home Layout**：支持按 ID 查找并查看已加载的非好感事件详情；优化画廊首页搜索框与“一键解锁全部”按钮的对称布局与多语言尺寸适配。 / Enabled ID lookup for loaded non-heart events and aligned the home search bar and Unlock All button across all 12 languages.

## 2.2.0 - Event Photos / 回放剧照与封面

- **无 UI 剧照抓拍 / Clean Replay Screenshots**：回放期间支持通过相机按钮、`F8` 或手柄左肩键（LB）抓拍不含对话框与 HUD 的场景画面（支持改键）。 / Added HUD-free and dialogue-free scene capture during replay via the camera button, `F8`, or controller Left Shoulder.
- **事件封面管理 / Cover Management**：点击事件详情页缩略图即可浏览抓拍剧照、设为相册与详情封面、恢复默认或移除归档；照片按存档独立保存在 `event-photos/`。 / Added a photo manager accessible from the detail thumbnail to browse shots, set custom covers, restore defaults, or archive removed photos per save.

## 2.1.0 – 2.1.2 - Album Redesign & Input Polish / 相册重制与输入优化

- **书页式相册与详情页重制 / Book-Style Album & Detail Redesign**：将角色事件页重制为两列三行缩略图相册，搭配羊皮纸风格的对勾/叉号/问号条件状态图标与统一滚动条。 / Redesigned character albums into a two-column, three-row thumbnail spread with parchment-style requirement status icons and unified scrollbars.
- **搜索性能与输入隔离 / Search Caching & Input Isolation**：缓存角色搜索结果以避免空闲帧重复计算；在搜索框输入文字时自动隔离 SMAPI 快捷键，并优化星期/季节否定条件的自然语言描述。 / Cached character search results, isolated keyboard shortcuts while typing in search fields, and simplified negated weekday/season descriptions.

## 2.0.0 – 2.0.6 - Current-State Gallery / 2.0 架构重制

- **基于当前状态的剧情画廊 / Current-State Event Gallery**：确立以当前生效游戏内容为核心的剧情画廊、条件解析与安全回放机制，全面停用早期历史采集功能。 / Established the current-state event gallery, readable condition parser (`ConditionIR` covering all 41 vanilla preconditions), and safe replay pipeline while retiring historical event collection.
- **演出环境与存档保护 / Replay Environment & Save Guard**：回放自动设置目标季节、时间与原版天气并在结束后恢复；提供回放期间禁止保存、状态快照恢复、1x/2x/4x 倍速、普通对话自动推进、键鼠/手柄支持、GMCM 配置与 12 种语言翻译。 / Added automatic season/time/weather setup and restoration, save blocking with fail-safe backups, 1x/2x/4x speed controls, optional dialogue auto-advance, controller navigation, GMCM integration, and 12-language localization.
