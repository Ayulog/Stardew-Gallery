# Stardew Gallery / 星露谷画廊

**2.6.0 角色外观兼容**：跟随当前 NPC 外观，适配 DDFC 大立绘、Scale Up 小人及 Portraiture 高清/动态肖像，并提供失败回退与自动恢复。已测试的单个美化模组支持获用户确认；多个美化模组互相覆盖的组合请自行实测。详见 [版本说明](docs/RELEASE_2.6.0_NOTES.md)。

**2.6.0 character appearance compatibility** adds current NPC appearances, DDFC portrait sizing, Scale Up sprite placement, Portraiture HD/animated portraits, fallback visuals and automatic recovery. Tested individual cosmetic mods have received user acceptance. Combinations of overlapping cosmetic mods require your own testing. See the [release notes](docs/RELEASE_2.6.0_NOTES.md).

Copyright (C) 2026 sjt38. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

[Download 2.6.0 / 下载](https://github.com/Ayulog/Stardew-Gallery/releases/tag/v2.6.0) · [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/51593) · [Changelog / 更新日志](CHANGELOG.md)

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
- 主相册只收好感剧情；查询支持名称、ID、角色与地点，以及精确角色/地点、类型、进度和更多条件筛选，返回保留筛选、滚动和焦点。普通剧情不计入好感剧情收藏。
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

GMCM 不是必需依赖；安装后可配置普通事件回放、快捷键、回放提示、自动对白和调试诊断。未安装GMCM时可在config.json设置`EnableOrdinaryEventReplay`，默认false。

角色美化同样为可选：画廊读取当前生效的CP肖像与小人，按已支持的协议接入DDFC、Scale Up Unofficial和Portraiture。无需为了画廊安装这些框架，也不需要在画廊里选择美化来源。Portraiture素材须先在游戏正常对话中生效，再检查画廊；CP/PyTK版素材并不自动成为Portraiture选包。读取失败时尝试可用肖像，最终使用画廊图标占位并限频重试。此适配不解决多个美化包互相覆盖的问题，也不包含Overgrown/Earthy专用画廊皮肤。

### 从旧版更新

退出游戏，先备份原`Mods/StardewGallery`，再用新版替换模组文件。保留并放回以下内容：

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
- 筛选支持角色、地点、进度、条件满足状态，以及季节、时间、天气和好感门槛。角色／地点精确选择可避免同字匹配过多；进度显示“已达成／未达成”，不会将当前条件满足等同于已经看过剧情。
- 详情中的“自定义事件名”可保存或恢复默认名称。更新或卸载前保留`user-data/`及`event-photos/`，即可保留个人命名和截图。
- “一键解锁全部”只改变画廊中的查看权限，不会修改存档的实际好感度或事件进度。

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
- Mod 不联网，也不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。

### 验证与构建

2.6.0已获单个美化模组支持的用户实测确认，复用2.5.0的既有功能验收。逻辑、本地化、存储及12语言隔离界面检查通过；多个美化模组互相覆盖请自行实测，不等同于所有模组版本、组合、Linux或macOS全面实机验证。

构建需要.NET SDK 8或更新兼容版本，以及已安装SMAPI的游戏目录；目标框架保持`net6.0`。在仓库根目录运行：

```sh
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"
```

将`GamePath`替换为实际游戏路径。产物在`bin/Release/net6.0/`，构建不会自动安装。运行检查需.NET 6运行时：

```sh
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

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
- The album contains heart stories; the separate search supports names, IDs, characters, locations and precise filters. Follow prerequisites and return with filters, scroll and focus preserved. Ordinary stories do not count toward the collection.
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

GMCM is optional. It configures ordinary replay, keybinds, warnings, dialogue auto-advance and diagnostics. Without GMCM, set `EnableOrdinaryEventReplay` in config.json; its default is false.

Cosmetic frameworks are optional too. The gallery reads currently active CP portraits/sprites and integrates with supported DDFC, Scale Up Unofficial and Portraiture formats. None is required just to use the gallery. Select and verify Portraiture packs in normal game dialogue first; a CP/PyTK pack does not automatically become a Portraiture set. Failed reads use an available portrait or the gallery icon, with throttled retries. This does not resolve conflicts between cosmetic packs or provide dedicated Overgrown/Earthy gallery skins.

### Updating

Close the game and back up `Mods/StardewGallery` before replacing the mod files. Preserve and restore:

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

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Replay uses currently active content, not historical versions.
- Search covers currently discoverable location events, not every inactive CP branch, nightly FarmEvent, festival flow or pure C# scene. Heart-story classification uses positive relationship evidence and confirmed continuations; NPC participation alone is not album membership.
- Wedding ceremonies, festival systems, special overnight flows and movie screenings are outside the gallery's collection scope. Post-marriage stories supplied as ordinary location events can still be included.
- Replay protects covered native state and rewards, suppressing some effects during presentation. Third-party commands still run through their frameworks; external files, private state and arbitrary callbacks are outside the native snapshot guarantee.
- Unseen events remain locked; condition explanations never rewrite save progress.
- Event replay supports single-player only; multiplayer replay is outside this project's scope.
- The mod does not access the internet or modify game files or other mods.
- Prerequisites come from current direct TriggerActions marker writes and known internal dependencies; actions are never executed for analysis. Matching conditions do not imply an obtained marker.
- Character filters combine confirmed actor aliases and omit animation-only actors or malformed names. Location groups use declared former names and verified scene copies without changing event identities or replay locations. Missing translation markers fall back to maintained or readable names. Weather filters include six vanilla types and explicitly referenced custom weather.
- Fallback names cover vanilla locations, including Ginger Island, and reviewed mod locations; translations for arbitrary new content are not guaranteed. Identical location labels share a filter choice while keeping each event's identity. The mine dwarf and Highlands dwarf remain separate characters.

### Uninstall

Delete the `Mods/StardewGallery` folder.

### Validation And Building

Tested individual cosmetic mods have received user acceptance for 2.6.0, reusing prior 2.5.0 gameplay acceptance. Logic, localization, persistence and isolated rendering checks passed across all 12 languages. Overlapping cosmetic combinations require user testing; this does not represent exhaustive testing of every mod version, Linux or macOS.

Use .NET SDK 8 or a later compatible SDK and a game installation with SMAPI. The mod targets `net6.0`. From the repository root:

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
