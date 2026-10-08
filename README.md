# Stardew Gallery / 星露谷画廊

**2.9.0**：整合 GMCM 名单开关（默认关闭），修复回放恢复、倍速、绿雨、条件筛选及手柄输入，清除停用历史／预览代码和 SQLite 依赖。升级步骤与验证范围见[版本说明](docs/RELEASE_2.9.0_NOTES.md)。

**2.9.0**: includes the GMCM exclusion setting (off by default), fixes replay restoration, speed, green rain, condition filters and controller input, and removes retired history/preview code and SQLite dependencies. See the [release notes](docs/RELEASE_2.9.0_NOTES.md) for updating and validation scope.

Copyright (C) 2026 ayulog. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

[Download 2.9.0 / 下载](https://github.com/Ayulog/Stardew-Gallery/releases/tag/v2.9.0) · [Changelog / 更新日志](CHANGELOG.md)

## 中文

星露谷画廊以NPC好感剧情收藏为主题，提供条件说明、剧情重温、截图与封面；配套自助查询器用于查找其他剧情及前置依赖。

| 内容 | 入口 | 回放 |
| --- | --- | --- |
| 好感剧情 | 角色相册、事件查询 | 已经历，或开启一键解锁 |
| 普通剧情 | 事件查询 | 先开启GMCM“普通事件回放”；未经历剧情仍需一键解锁 |
| 前置事件 | 事件查询、剧情前置链接 | 仅查看条件、触发时机、进度和关联剧情 |

“全部事件”包含上述三类。纯转场不进入剧情列表，也不提供回放。

### 当前功能

- 按角色浏览当前游戏与已安装 Mod 实际生效的好感事件。
- 主相册只收好感剧情；查询支持名称、ID、角色、地点与来源，以及精确角色/地点/来源、类型、进度和更多条件筛选，返回保留筛选、滚动和焦点。普通剧情不计入好感剧情收藏。
- 标记与明确的解锁依赖单列“前置事件”，展示条件、获得状态和关联剧情，不提供回放。纯转场继续隐藏；短但具有演出内容的剧情仍可收录。
- 可自定义事件名称并搜索，原ID保持有效。名称在本机存档间共用，独立保存在`user-data/`。
- 回放截图按存档保存，可选封面、替换封面、恢复默认或移除归档。
- 阅读型条件说明与进度缺口：好感/心数、看过事件、邮件、季节、日期、时间等用可读文本呈现；无法安全解析的模组条件会明确标注，而不是猜测。
- 支持金钱、背包、技能、对话记录、NPC可见性、住宅与节日等只读条件，以及受限的节日/日期/统计量查询。随机结果、缺少入场位置或未支持的第三方条件会说明未知原因。
- 当前状态回放：所有回放都从当前已解析的事件内容与当前游戏状态启动，不再使用历史冻结版本。
- 已观看的事件可直接回放，也可用“一键解锁全部”临时开放画廊回放。
- 普通剧情需在GMCM开启“普通事件回放”（默认关闭）；未经历剧情仍需“一键解锁”。该开关不能让内部流程进入回放。
- 回放会尽力构造事件要求的季节、时间和原版天气；结束后恢复玩家位置和原环境。
- 回放全程禁止保存，并保留故障恢复备份。
- 回放速度可在 1x、2x、4x 之间切换；普通对话可选择自动继续，选项不会自动选择。
- 支持键鼠和手柄操作，快捷键可配置多个单键或组合键。
- 支持现有 12 种游戏语言，界面随分辨率与 UI 缩放自动适配。
- 回放确认框限制窗口宽度、自动换行，改变窗口大小后重新居中，确认和取消按钮保持可见。
- 可选支持 Generic Mod Config Menu（GMCM）。

### 安装

1. 安装 Stardew Valley 1.6.15 和 SMAPI 4.5.2 或更高兼容版本。
2. 解压下载文件，将 `StardewGallery` 文件夹放入游戏的 `Mods` 文件夹。
3. 通过 SMAPI 启动游戏。

GMCM 不是必需依赖；安装后可配置普通事件回放、事件来源追踪、AI 名单排除、快捷键、回放提示、自动对白和调试诊断。未安装GMCM时可在config.json设置`EnableOrdinaryEventReplay`，默认false。

角色美化同样为可选：画廊读取当前生效的CP肖像与小人，按已支持的协议接入DDFC、Scale Up Unofficial和Portraiture。无需为了画廊安装这些框架，也不需要在画廊里选择美化来源。Portraiture素材须先在游戏正常对话中生效，再检查画廊；CP/PyTK版素材并不自动成为Portraiture选包。读取失败时尝试可用肖像，最终使用画廊图标占位并限频重试。此适配不解决多个美化包互相覆盖的问题，也不包含Overgrown/Earthy专用画廊皮肤。

### 从旧版更新

退出游戏，先备份原`Mods/StardewGallery`，再用新版替换模组文件。保留并放回以下内容：

从旧版更新时，将新包解压到干净的 `StardewGallery` 文件夹，再放回下列玩家数据，以去掉旧 SQLite 程序库和运行时目录。以前的历史数据库无需迁移，本版不会读取或删除它们。

| 文件或目录 | 玩家数据 |
| --- | --- |
| `config.json` | 快捷键和设置 |
| `event-photos/` | 按存档保存的截图及封面 |
| `user-data/` | 本机各存档共用的自定义事件名称 |

2.5.0会去除爷爷评估剧情被游戏复制到各地图的重复目录项。更新后结果数量可能减少，原始农舍剧情仍保留，实际已看进度不变。

### 使用

- 默认按 `G` 打开或关闭画廊，也可点击原版菜单中的画廊标签。
- 选择角色，查看事件的可读条件和缺失/未知要求；已解锁事件可点击“回放”。
- 回放右上角按钮或配置的快捷键可循环切换 1x / 2x / 4x。
- 回放时按 F8、手柄左肩或点击相机截图；从详情缩略图管理截图与封面。截图不含对话框和 HUD，保存在本 Mod 的 `event-photos/` 内，卸载前可保留此目录。
- 首页搜索框只找角色；事件查询提供筛选面板。手柄Y打开筛选，确认进入角色/地点列表，肩键翻页，B返回上一级，应用后统一更新结果。类型为全部事件、好感剧情、普通剧情、前置事件；全部事件包含后三类。
- 筛选支持角色、地点、来源、进度、条件满足状态，以及季节、时间、天气和好感门槛。角色／地点精确选择可避免同字匹配过多；进度显示“已达成／未达成”，不会将当前条件满足等同于已经看过剧情。
- 详情中的“自定义事件名”可保存或恢复默认名称。更新或卸载前保留`user-data/`及`event-photos/`，即可保留个人命名和截图。
- “一键解锁全部”只改变画廊中的查看权限，不会修改存档的实际好感度或事件进度。

### 按来源查询与名单排除

在事件查询的“筛选 → 来源”中选择原版、具体模组、未知或多个候选来源；搜索框也支持模组名称和完整 Mod ID。模组同名时通过 ID 区分，可与角色、地点、进度等筛选组合。

在 GMCM 开启“排除 AI 名单模组”后，按 [AI Mod Exclusion](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion) 名单隐藏已确认由名单内模组提供的事件。每次启动及读档异步获取最新名单；等待期间及下载失败时使用发布包内置名单，不使用以前下载的缓存。只匹配原始提供者的 Mod ID（包括名单明确给出的通配规则）；不按角色、地点或修改者推断。原版、未知、来源冲突和名单外模组的事件保留，存档进度、照片及名称不删除。

开关统一保存在 `config.json` 的 `EnableAiModExclusion`，默认 `false`。在 GMCM 保存后生效；回放或确认尚未结束时延后应用。关闭后停止下载并恢复事件可见性，无需重新读档或重启。未安装 GMCM 时可编辑此配置并重启。旧的 `ai-mod-exclusion.json` 会在启动或读档时删除，不再读取、生成或迁移其中的 `Enabled` 值；内置名单 `assets/ai-mod-exclusion.seed.json` 仍作为开启后的回退数据保留。

### 事件来源查询

在事件详情页的地点下方直接查看原始来源，点击可查看匹配文件与修改记录，再点击返回条件。首次打开画廊目录时自动扫描已加载 Content Patcher 模组的 Include/FromFile 定义，以及 AliveNpcs 的场景文件；按资源路径＋事件 ID 匹配，即使后续文本或条件被改动仍能保留来源。原版事件读取游戏原始 XNB；翻译/兼容包结合声明的依赖关系消歧，独立多来源会列出候选，未找到时保留未知。此扫描无需开启配置，也不新增 Harmony。

GMCM 的“追踪事件修改（重启生效）”仅控制可选的运行时修改记录，默认关闭；未安装 GMCM 时可设置 `EnableEventSourceDiagnostics=true`。追踪当前适配 SMAPI 4.5.2.0，追踪不可用不影响文件来源。返回标题或事件资源刷新后重新扫描，也可用 `gallery_event_source rescan` 手动刷新。扫描不执行 When，不证明当前所有文本作者；其他尚未适配的 C# 动态事件和令牌仍可能未知。详见[实现说明](docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md)。

### 兼容性与限制

- 事件目录取决于当前存档状态、已安装 Mod 及其条件，因此不同存档可能看到不同的当前版本。
- 回放使用当前生效内容，不是历史回放。
- 查询范围是当前可发现的地点事件内容，不保证覆盖未生效CP分支、夜间FarmEvent、节日主流程或纯C#事件；好感剧情分类依据正向关系证据与可确认的续篇，非社交NPC关联不等于主相册收录。
- 婚礼仪式、节日主流程、夜间特殊流程和影院专用放映不纳入画廊；按普通地点事件提供的婚后剧情仍可收录。
- 前置资料读取当前TriggerActions的直接标记动作和明确内部依赖，不执行动作来探测。条件现在满足不代表标记已生成；未知条件不计为满足。
- 角色筛选合并有明确依据的演员别名，忽略纯动画角色和误解析文本；地点合并作者声明的旧名及已核实场景副本，保留真实事件身份与回放地点。缺译标记回退为已维护译名或可读名称。天气包含六种原版天气及事件明确使用的自定义天气。
- 姜岛等原版地点和已核查的扩展地点提供译名回退；任意新增模组不保证全部地名有翻译。相同地点显示名共用一个筛选项，各事件仍保留原始身份。矿井矮人与高地矮人是不同角色。
- 回放统一保护已覆盖的原版进度与奖励，部分效果在演出时阻止。第三方命令仍由其原框架执行，外部文件、私有状态和任意回调不在原版快照保障范围内。
- 未观看事件保持锁定；条件说明不会自动修改存档进度。
- 事件回放仅支持单人模式；本项目不开发多人回放。
- 名单功能开启时仅下载公开排除名单，不上传存档或游戏内容；不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。

### 验证与构建

2.9.0 的验证覆盖完整构建、逻辑与存储检查、真实 SMAPI 加载器、回放状态／天气／倍速、菜单草稿、手柄分派及 GMCM 回调；12 语言各 533 键一致。发布验证见 [2.9.0 版本说明](docs/RELEASE_2.9.0_NOTES.md)，修复过程见 [2026-10-08 整合记录](docs/INTEGRATION_2026-10-08.md)。隔离检查不替代 GMCM 画面、来源页面、长文本换行及实际手柄操作的游戏内验收。

可选运行时修改追踪另有 36 项 SMAPI 来源管线检查，包含中文资源、原版 XNB 与读档缓存回归；这些检查不等于真实 CP/SVE 组合验收。2.6.0 的单个美化模组支持已有用户确认，多个美化覆盖组合仍需自行实测。

构建需要.NET SDK 8，以及已安装SMAPI的游戏目录；`global.json`固定使用已安装的正式版8.0 SDK，目标框架保持`net6.0`。完整环境与打包步骤见[构建说明](BUILDING.md)。在仓库根目录运行：

```sh
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"
```

将`GamePath`替换为实际游戏路径。产物在`bin/Release/net6.0/`，构建不会自动安装。运行检查需.NET 6运行时：

```sh
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

两套检查不依赖游戏安装，GitHub Actions会在PR和main推送时自动运行；完整构建和游戏内验收仍需本机游戏环境。

### 许可证

本项目以 GNU General Public License v3.0 发布。完整条款见 `LICENSE`。

## English

Stardew Gallery collects NPC heart stories with readable requirements, replay photos and covers. A separate self-service search helps players find other stories and prerequisite events.

| Content | Where to find it | Replay |
| --- | --- | --- |
| Heart stories | Character albums and Event Search | Seen stories, or Unlock All |
| Ordinary stories | Event Search | Enable Ordinary Event Replay in GMCM; unseen stories still need Unlock All |
| Prerequisite events | Event Search and prerequisite links | Reference only: conditions, timing, progress and related stories |

All Events includes these three categories. Pure transitions stay outside story lists and replay.

### Features

- Browse the heart events actually active in the base game and installed mods, by character.
- The album contains heart stories; the separate search supports names, IDs, characters, locations, sources and precise filters. Follow prerequisites and return with filters, scroll and focus preserved. Ordinary stories do not count toward the collection.
- Prerequisite records show marker conditions, progress and related stories without replay. Pure transitions stay hidden; brief scenes with narrative content remain eligible.
- Give events personal names and search them while retaining the original IDs. Names are shared across local saves and stored separately in `user-data/`.
- Capture replay photos per save, choose or replace covers, restore defaults, and archive removed photos.
- Readable condition explanation with progress gaps: friendship/hearts, seen events, mail, season, day, time, and more are shown in plain text; mod conditions that can't be parsed safely are labeled as unknown rather than guessed.
- Read-only checks cover money, inventory, skills, dialogue records, NPC visibility, homes and festivals, plus restricted festival/date/stat queries. Random outcomes, missing entry positions and unsupported third-party conditions explain why their results remain unknown.
- Current-state replay: every replay launches from currently resolved event content and current game state, not from a frozen historical version.
- Replay events you've seen, or temporarily expose all gallery replays with Unlock All.
- Ordinary stories additionally require "Ordinary event replay" in GMCM (off by default). Unseen stories still require Unlock All. Neither option exposes internal workflows.
- Replay applies supported season, time, and vanilla weather requirements when possible, then restores the original environment.
- Saving is blocked during replay, with a recovery backup kept for failures.
- Cycle replay speed between 1x, 2x, and 4x. Optional auto-advance applies only to normal dialogue; choices always wait for the player.
- Keyboard, mouse, and controller navigation, with multiple configurable single-key or chord bindings.
- All 12 maintained game languages, with automatic fitting for screen resolution and UI scale.
- The replay confirmation wraps long text, stays centered when resizing, and keeps both buttons visible.
- Optional Generic Mod Config Menu support.

### Installation

1. Install Stardew Valley 1.6.15 and SMAPI 4.5.2 or a later compatible version.
2. Extract the download and place the `StardewGallery` folder in the game's `Mods` folder.
3. Launch the game through SMAPI.

GMCM is optional. It configures ordinary replay, event source tracing, AI list exclusion, keybinds, warnings, dialogue auto-advance and diagnostics. Without GMCM, set `EnableOrdinaryEventReplay` in config.json; its default is false.

Cosmetic frameworks are optional too. The gallery reads currently active CP portraits/sprites and integrates with supported DDFC, Scale Up Unofficial and Portraiture formats. None is required just to use the gallery. Select and verify Portraiture packs in normal game dialogue first; a CP/PyTK pack does not automatically become a Portraiture set. Failed reads use an available portrait or the gallery icon, with throttled retries. This does not resolve conflicts between cosmetic packs or provide dedicated Overgrown/Earthy gallery skins.

### Updating

Close the game and back up `Mods/StardewGallery` before replacing the mod files. Preserve and restore:

Extract this build into a clean `StardewGallery` folder, then restore the player data below so retired SQLite libraries and runtime folders are not retained. Existing historical databases need no migration; this build neither reads nor deletes them.

| File or folder | Player data |
| --- | --- |
| `config.json` | Settings and key bindings |
| `event-photos/` | Photos and covers for each save |
| `user-data/` | Personal event names shared across local saves |

2.5.0 removes duplicate catalog entries created when the game copies Grandpa's evaluation scenes into other locations. Result counts can decrease; the original farmhouse stories and actual seen progress are retained.

### Usage

- Press `G` by default to toggle the gallery, or use its tab in the vanilla game menu.
- Choose a character, review readable conditions and missing/unknown requirements, then click "Replay" on an unlocked event.
- Use the top-right replay button or the configured binding to cycle 1x / 2x / 4x.
- During replay, press F8, the controller's left shoulder, or the camera button to capture the scene without dialogue/HUD. Open the detail thumbnail to manage photos/covers. Photos live in this mod's `event-photos/` folder; keep it when uninstalling to retain your pictures.
- Home search finds characters only. Event Search contains the filter panel: Y opens filters, confirm opens a character/location picker, shoulder buttons page through lists, B returns one level, and Apply updates the results. All Events includes heart stories, ordinary stories, and prerequisites.
- Filter by character, location, progress, requirement status, season, time, weather or heart threshold. Precise character/location choices narrow ambiguous text matches. Completed progress is separate from currently meeting trigger conditions.
- Use Rename Event in details to save a personal name or restore the default. Preserve `user-data/` and `event-photos/` when updating or uninstalling.
- "Unlock all" changes gallery visibility only. It does not alter friendship or event progress in the save.

### Source filters and exclusion list

Use event query → Filters → Source to select the base game, a specific mod, unknown, or ambiguous sources. Search also accepts provider names and Mod IDs. Same-name mods have distinct ID labels, and source filters combine with existing filters.

After enabling “Exclude AI-listed mods” in GMCM, events with a confirmed original provider listed in [AI Mod Exclusion](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion) are hidden. Startup and each save load fetch the latest list asynchronously; the bundled release list applies while waiting and on failure. Previous downloads are never a fallback. Only the original provider's complete Mod ID or an explicitly declared wildcard rule is used; NPCs, locations and modifying packs do not imply exclusion. Vanilla, unknown, ambiguous and unlisted sources remain visible. Save progress, photos and names are not deleted.

The setting is stored in standard `config.json` as `EnableAiModExclusion`, defaulting to `false`. Save in GMCM to apply; changes wait until any replay or confirmation has finished. Disabling stops downloads and restores event visibility without reloading or restarting. Without GMCM, edit this setting and restart. The retired `ai-mod-exclusion.json` is deleted on startup or save load; its `Enabled` value is no longer read, created or migrated. The bundled `assets/ai-mod-exclusion.seed.json` remains as fallback data when the feature is enabled.

### Event source lookup

Event details automatically show original sources below the location. Click the row for matched files and optional modification records. The first gallery catalog access scans loaded Content Patcher packs, following Include/FromFile from content.json, and supported AliveNpcs scene files, then matches asset path plus event ID. Later script or condition edits do not erase the match. Raw game XNBs identify vanilla events; declared dependencies help distinguish translation/compatibility packs, while independent conflicting declarations remain ambiguous. This requires no configuration switch and adds no Harmony hooks.

The GMCM option "Track event modifications (restart required)" controls optional runtime evidence only; it remains off by default (`EnableEventSourceDiagnostics`). That adapter currently supports SMAPI 4.5.2.0; unavailable tracing does not disable file matching. Returning to title or invalidating event assets resets the scan, and `gallery_event_source rescan` refreshes it manually. The scan does not evaluate When or establish full-text authorship. Other unsupported C# event formats and tokens may remain unknown. See the [implementation notes](docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md).

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Replay uses currently active content, not historical versions.
- Search covers currently discoverable location events, not every inactive CP branch, nightly FarmEvent, festival flow or pure C# scene. Heart-story classification uses positive relationship evidence and confirmed continuations; NPC participation alone is not album membership.
- Wedding ceremonies, festival systems, special overnight flows and movie screenings are outside the gallery's collection scope. Post-marriage stories supplied as ordinary location events can still be included.
- Replay protects covered native state and rewards, suppressing some effects during presentation. Third-party commands still run through their frameworks; external files, private state and arbitrary callbacks are outside the native snapshot guarantee.
- Unseen events remain locked; condition explanations never rewrite save progress.
- Event replay supports single-player only; multiplayer replay is outside this project's scope.
- When exclusion is enabled, the mod downloads only the public exclusion list; it uploads no saves or game content and does not modify game files or other mods.
- Prerequisites come from current direct TriggerActions marker writes and known internal dependencies; actions are never executed for analysis. Matching conditions do not imply an obtained marker.
- Character filters combine confirmed actor aliases and omit animation-only actors or malformed names. Location groups use declared former names and verified scene copies without changing event identities or replay locations. Missing translation markers fall back to maintained or readable names. Weather filters include six vanilla types and explicitly referenced custom weather.
- Fallback names cover vanilla locations, including Ginger Island, and reviewed mod locations; translations for arbitrary new content are not guaranteed. Identical location labels share a filter choice while keeping each event's identity. The mine dwarf and Highlands dwarf remain separate characters.

### Uninstall

Delete the `Mods/StardewGallery` folder.

### Validation And Building

Validation for 2.9.0 covers the build, logic/persistence suites, the actual SMAPI loader, replay state/weather/speed, menu drafts, controller dispatch and GMCM callbacks. All 12 locales contain matching sets of 533 keys. See the [2.9.0 release notes](docs/RELEASE_2.9.0_NOTES.md) and [2026-10-08 integration report](docs/INTEGRATION_2026-10-08.md). Isolated checks do not replace in-game acceptance of GMCM, source pages, long text wrapping or physical controller input.

Optional runtime modification tracking has 36 SMAPI source pipeline assertions, including localized resources, a vanilla XNB and save-load cache regressions. These do not establish real CP/SVE combination acceptance. Tested individual cosmetic mods received user confirmation in 2.6.0; overlapping cosmetic combinations still require testing.

Use a .NET 8 SDK and a game installation with SMAPI. `global.json` selects an installed stable 8.0 SDK; the mod still targets `net6.0`. See [BUILDING.md](BUILDING.md) for setup and packaging. From the repository root:

```sh
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"
```

Set `GamePath` to your installation. Output is written to `bin/Release/net6.0/`; building does not deploy the mod. Checks require the .NET 6 runtime:

```sh
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

### License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for the full terms.
