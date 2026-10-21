# Stardew Gallery 2.10.0

2026-10-21 发布。需求依据：[REQUIREMENTS.md](REQUIREMENTS.md)；游戏内验收场景：[RELEASE_2.10.0_ACCEPTANCE.md](RELEASE_2.10.0_ACCEPTANCE.md)。

## 中文

2.10.0 按重新整理的需求收窄范围：删去 AI 名单和运行时修改追踪，精简查询筛选；集中修复回放存档安全、剧照、手柄、条件判定、目录和来源识别中的问题；新增 Steam 键盘中文输入和拼音搜索。

### 移除

- **AI 模组排除名单**：连同 GMCM 开关、内置名单和联网更新一起删除。画廊不再联网。
- **运行时修改追踪**（`EnableEventSourceDiagnostics`）：事件来源只来自只读的静态文件匹配。
- **查询筛选**：去掉条件满足状态、季节、天气、时间和心数；条件统一在详情页查看。
- `config.json` 里这两项旧设置会被忽略；旧版本留下的 `ai-mod-exclusion.json`（如有）也不再使用，可以删除。

### 回放存档安全

- **背包**：不再克隆物品，回放后鱼竿上的鱼饵、钓具和弹弓弹药不会消失。
- **季节**：只改变事件地点的季节画面，不再触发全局换季（之前会清空果实、删除肥料和采集物）。
- **失焦与暂停**：窗口失焦或游戏暂停时不计入超时，不会因此误读档。演出或启动出错时正常恢复，并提示“回放中断”；只有恢复本身失败时才用备份读档。
- **节日**：节日当天不能在节日会场回放。
- **隔离**：回放期间不会触发真实事件、节日或 TriggerAction。
- **快照补全**：新增已运行的触发器、已听歌曲、已访问地点、统计数据、Buff 和事件地点的壁炉状态。
- **新存档**：第一次睡觉前（还没有存档文件）会明确说明不能回放。
- **演出**：开门动画和爷爷评价台词恢复显示。

### 剧照

- 默认 100% 缩放下抓拍不再得到全透明图。
- 照片里不再出现跳过按钮。
- 写入失败时不留下无主文件；照片缺失的警告每次运行只记一次。

### 手柄与 Steam Deck

- 从键鼠切换到手柄后，各页面都能正常导航。
- 关闭“吸附菜单”时，`A` 作用于光标所指的按钮（回放确认框、查询页）。
- 用手柄选中输入框时弹出屏幕键盘。
- 工具页的 `Y` / `Start` 改为返回上一级；滚动后焦点跟随；用键盘导航时显示焦点高亮。
- 在搜索框按回车（或屏幕键盘的确定）只列出候选并选中第一个，再按回车或 `A` 才进入；以前会直接进入第一个结果。首页角色搜索和事件查询都是这样，改名页的回车仍是保存。
- Steam Deck 和 Steam 大屏幕模式下，用手柄选中输入框会弹出 Steam 键盘，可以输入中文；提交时仍按着的按键要松开后才生效，不会顺带打开第一个结果。其他情况仍弹出游戏自带的键盘，只能输入英文字母，可配合拼音搜索。
- 摇杆、方向键和 `A` 更跟手：首页、相册、查询和详情页不再每帧遍历所有地点查找角色、读取存档目录与节日数据、重新排版条件文字；回到事件查询时也不再重新整理全部事件。
- 开启 GMCM 的“调试诊断”后，画廊页面打开期间每 5 秒在 SMAPI 日志里记录一次帧耗时（更新、绘制和垃圾回收次数），用于排查卡顿。

### 拼音搜索

- 游戏语言为中文时，首页的角色搜索和事件查询也按拼音查找：全拼（`abigaier`）、首字母（`abge`）、两者混合（`abgaier`）或只打开头几个字母都可以。多音字按每个常用读音匹配，`ü` 写作 `v` 或 `u` 均可。
- 事件查询按拼音匹配事件名称、地点和角色；英文字母仍照常匹配英文名称、事件 ID 和 Mod ID。
- 读音表由 Unicode 汉字数据库（Unihan，Unicode 18.0）生成，随包附带 Unicode 许可。

### 条件判定（与游戏 1.6.15 一致）

- `D`（约会）对已订婚、已结婚的 NPC 同样成立；`R`（室友）包含尚未入住的室友。
- 一个条件写多个季节时，游戏只检查第一个；画廊照此判定，并在说明里标出被忽略的季节。
- `!!X`、整串加引号的 GSQ 等写法按游戏规则显示为格式错误或未知；`WEATHER Here` 按事件地点判断。
- 事件 ID 按游戏方式读取（例如保留尾随空格），已看状态因此与游戏一致。
- 要求“没看过某事件”的条件不再把那个事件算作前置。

### 目录

- Penny 的拖车剧情不再出现两遍（Trailer 与 Trailer_Big 合并）；回放地点按潘姆的房子是否已升级选择。旧版重复条目上的剧照和自定义名称会在打开画廊时自动并入保留的条目，原图仍留在磁盘上。
- 打开画廊更快：内容和状态没有变化时直接复用目录。

### 事件来源

- Linux / Steam Deck 上文件名大小写与引用不一致时也能识别来源。
- 支持 Stardrop 等工具以链接目录部署的模组。
- 原包没能完整读取时显示“证据不完整”，不再把覆盖包误当作原始来源。
- 文件中有空数组元素时不再丢弃整个文件；读取失败会在日志里写明原因。
- 事件数据刷新后只重扫有变化的包。
- 来源详情单独列出“其他声明者（不一定在当前存档生效）”。

### 其他

- **联机与分屏**：联机农场工的一键解锁只在本次会话生效，不再报错；分屏时每位玩家的画廊页面、剧照和解锁状态互不影响。
- **自定义名称**：写入后落盘；文件损坏时自动回退到最近的备份；名称备份和每个事件的照片目录备份各最多保留 5 份（移除的照片不受影响）。
- **外观**：Portraiture 读取失败时先显示普通肖像，最后才用占位图。
- **发布包**：从约 12.8 MB 减到约 8.4 MB（素材无损压缩，去掉只供开发工具使用的原图）。带 C2PA 内容凭证的插图保持原样。
- **打字隔离**：在画廊输入框打字时，按键不会触发游戏和其他 Mod 的快捷键（例如绑在字母上的快捷键）。以前靠补丁 SMAPI 的内部方法实现，SMAPI 内部一变搜索框就会停用；现在改用游戏自带的“忽略按键直到松开”，不再补丁 SMAPI。结束输入的那次按键同样被忽略到松开，所以实体键盘按回车只列出候选，不会再顺带打开第一个结果。粘贴（Ctrl+V，macOS 上 Cmd+V）照常可用。

### 升级

1. 退出游戏，备份 `Mods/StardewGallery`。
2. 建议解压为干净的 `StardewGallery` 文件夹，再拷回 `config.json`、`event-photos/` 和 `user-data/`。
3. 需要 Stardew Valley 1.6.15、SMAPI 4.5.2 或兼容的新版本；GMCM 和各美化框架均为可选。

### 验证

- 构建、Checks、PersistenceChecks 和 10 组 SMAPI 运行时检查全部通过，可用 `tools/Verify-Local.ps1` 一次运行。
- 新增条件对拍检查：在同一玩家状态下直接调用游戏自身的判定代码，与画廊逐项比对，75 项一致。
- 以上都不代替游戏内验收；游戏内验收场景见 [RELEASE_2.10.0_ACCEPTANCE.md](RELEASE_2.10.0_ACCEPTANCE.md)。

## English

2.10.0 narrows the scope to the rewritten requirements: the AI exclusion list and runtime modification tracking are gone, query filters are simpler, and many fixes land in replay safety, photos, controller input, condition checks, the catalog and event sources. Chinese input through Steam's keyboard and pinyin search are new.

### Removed

- **AI mod exclusion list**, including its GMCM toggle, bundled list and online updates. The gallery no longer goes online.
- **Runtime modification tracking** (`EnableEventSourceDiagnostics`). Event sources come only from read-only static file matching.
- **Query filters** for condition status, season, weather, time and hearts. Conditions are shown on the detail page.
- These two old settings in `config.json` are ignored. An `ai-mod-exclusion.json` left by an older version is unused and can be deleted.

### Replay safety

- **Inventory**: items are no longer cloned, so fishing rod bait and tackle and slingshot ammo survive a replay.
- **Season**: only the event location's seasonal look changes; no global season change (which cleared fruit, fertilizer and forage).
- **Focus and pause**: time spent unfocused or paused no longer counts toward a timeout, so it can't trigger a reload. Scene or startup errors end the replay normally with a "replay interrupted" message; the backup is reloaded only if restoring itself fails.
- **Festivals**: a festival's venue can't be used for replay on the festival day.
- **Isolation**: real events, festivals and trigger actions don't run during a replay.
- **Snapshot**: now also covers trigger actions run, songs heard, locations visited, stats, buffs and fireplaces at the event location.
- **New saves**: before the first night (no save file yet) the gallery explains why replay isn't available.
- **Presentation**: door-opening animations and Grandpa's evaluation dialogue are shown again.

### Photos

- Photos at the default 100% zoom are no longer fully transparent.
- The skip button no longer appears in photos.
- A failed write leaves no stray files; a missing photo is logged once per run.

### Controller and Steam Deck

- Pages keep working after switching from mouse and keyboard to a controller.
- With snappy menus off, `A` acts on the button under the cursor (replay confirmation, Event Search).
- Selecting a text field with a controller opens the on-screen keyboard.
- `Y`/`Start` in tool pages goes back one page; focus follows scrolling; keyboard navigation shows the focus highlight.
- Enter in a search box (or OK on the on-screen keyboard) lists the matches with the first one selected; a second Enter or `A` opens it. It used to open the first match right away. This applies to character search and Event Search; Enter on the rename page still saves.
- On Steam Deck and in Steam Big Picture mode, selecting a text field with a controller opens Steam's keyboard, which can type Chinese. A button still held when you submit takes effect only after it is released, so it doesn't also open the first match. Elsewhere the game's own keyboard opens; it types Latin letters only, which pinyin search can use.
- The stick, D-pad and `A` respond faster: the home, album, Event Search and detail pages no longer search every location for characters, read the save folder and festival data, or re-wrap condition text every frame, and returning to Event Search no longer re-indexes every event.
- With GMCM's debug diagnostics on, the SMAPI log records the gallery's frame times (update, draw and garbage collections) every 5 seconds while a gallery page is open, to help track down stutter.

### Pinyin search

- When the game language is Chinese, character search and Event Search also match pinyin: full (`abigaier`), initials (`abge`), a mix (`abgaier`) or just the first few letters. Characters with several readings match each common reading, and `ü` can be typed as `v` or `u`.
- Event Search matches event names, locations and characters by pinyin; Latin letters still match English names, event IDs and mod IDs as before.
- The readings are generated from the Unicode Han Database (Unihan, Unicode 18.0); the Unicode license is included in the package.

### Conditions (matching Stardew Valley 1.6.15)

- `D` (dating) also holds for engaged and married NPCs; `R` (roommate) includes a roommate who hasn't moved in yet.
- When a condition lists several seasons, the game checks only the first; the gallery does the same and names the ignored seasons.
- Forms such as `!!X` or a fully quoted GSQ show as malformed or unknown, as the game treats them; `WEATHER Here` uses the event location.
- Event IDs are read the way the game reads them (including trailing spaces), so seen status matches the game.
- A condition requiring an event to be unseen no longer makes that event a prerequisite.

### Catalog

- Penny's trailer events are listed once (Trailer and Trailer_Big merged); replay uses whichever location matches whether Pam's house is upgraded. Photos and custom names on the old duplicate entries are merged into the kept entry when the gallery opens; the original images stay on disk.
- Opening the gallery is faster: the catalog is reused when nothing has changed.

### Event sources

- Sources are found on Linux and Steam Deck even when file references differ in letter case.
- Mods deployed as directory links (Stardrop and similar) are supported.
- When an original pack couldn't be read completely, the source shows "evidence incomplete" instead of naming an override as the original.
- A file with empty array entries is no longer discarded as a whole; read failures are logged with the reason.
- After event data refreshes, only changed packs are scanned again.
- Source details list "Also declared by (not necessarily active in this save)".

### Other

- **Multiplayer and split-screen**: a farmhand's Unlock All lasts for the session only, without errors; in split-screen each player has their own gallery pages, photos and unlock state.
- **Custom names**: writes are flushed to disk; a damaged file falls back to the newest backup; up to 5 name backups and 5 photo-catalog backups per event are kept (removed photos are never deleted).
- **Appearance**: if Portraiture can't be read, the regular portrait is shown before the placeholder.
- **Package**: down from about 12.8 MB to about 8.4 MB (lossless asset compression; source art used only by development tools is no longer shipped). Illustrations with C2PA content credentials are unchanged.
- **Typing isolation**: keys typed into a gallery text field don't trigger game or mod shortcuts (such as shortcuts bound to letters). This used to rely on a patch on SMAPI internals, which disabled search whenever those internals changed; it now uses the game's own "ignore keys until released", with no patch on SMAPI. The key that ends typing is ignored until released too, so Enter on a physical keyboard lists the matches without also opening the first one. Pasting (Ctrl+V, Cmd+V on macOS) works as before.

### Updating

1. Close the game and back up `Mods/StardewGallery`.
2. Extract into a clean `StardewGallery` folder, then copy back `config.json`, `event-photos/` and `user-data/`.
3. Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or a compatible newer version. GMCM and cosmetic frameworks are optional.

### Validation

- The build, Checks, PersistenceChecks and ten SMAPI runtime check modes pass; `tools/Verify-Local.ps1` runs them all.
- A new condition parity check calls the game's own precondition code on the same player state and compares 75 cases with the gallery.
- None of this replaces in-game acceptance; the in-game scenarios are listed in [RELEASE_2.10.0_ACCEPTANCE.md](RELEASE_2.10.0_ACCEPTANCE.md).
