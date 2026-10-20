# Stardew Gallery / 星露谷画廊

[![Stardew Gallery 宣传插画](docs/images/stardew-gallery-ai-poster.png)](docs/images/stardew-gallery-ai-poster.png)

*宣传插画由 AI 生成 / Promotional artwork generated with AI*

> **最新版本 2.9.0**：将 AI 模组排除名单整合至 GMCM 设置（默认关闭），修复回放状态恢复、倍速计时、绿雨天气、条件筛选与手柄操作等多项问题，并移除早期遗留的 SQLite 依赖，大幅缩减发布包体积。升级说明详见 [2.9.0 版本说明](docs/RELEASE_2.9.0_NOTES.md)。
>
> **Latest Release 2.9.0**: Moves the AI mod exclusion toggle into GMCM (off by default), fixes replay state restoration, playback speed timing, Green Rain weather, condition filters, and controller input, and removes legacy SQLite dependencies for a much lighter package. See the [2.9.0 Release Notes](docs/RELEASE_2.9.0_NOTES.md) for update instructions.

Copyright (C) 2026 Ayulog. Licensed under the GNU General Public License v3.0.

[中文说明](#中文) · [English](#english) · [下载 / Download 2.9.0](https://github.com/Ayulog/Stardew-Gallery/releases/tag/v2.9.0) · [更新日志 / Changelog](CHANGELOG.md)

---

## 画廊预览 / Gallery Preview

按角色收藏好感剧情，直观查看触发条件与模组来源，随时安全重温经历过的故事。  
Browse heart events by character, check trigger conditions and mod sources in plain language, and safely replay your favorite scenes.

| 角色画廊 / Character Gallery | 好感相册 / Heart Event Album |
| --- | --- |
| [![角色画廊：角色肖像、剧情数量及查询入口](docs/images/gallery-overview.png)](docs/images/gallery-overview.png) | [![角色相册：好感、生日和好感事件列表](docs/images/character-album.png)](docs/images/character-album.png) |
| **条件与来源 / Conditions & Sources** | **事件查询 / Event Search** |
| [![事件详情：地点、来源、好感要求和满足状态](docs/images/event-details.png)](docs/images/event-details.png) | [![事件查询：按名称、角色、地点和来源搜索剧情](docs/images/event-query.png)](docs/images/event-query.png) |

*点击图片可查看大图。截图环境安装了额外的内容扩展与美化模组。 / Click any image to view full size. Screenshots were taken with additional expansion and cosmetic mods installed.*

---

## 中文

**星露谷画廊（Stardew Gallery）** 是一款游戏内剧情图鉴与回放工具。无论你想重温某位村民的爱心事件、弄清下一段剧情为什么迟迟不触发，还是在安装了大量扩展 Mod 后查找某个事件究竟来自哪里，都可以在画廊中直接翻阅。

### 核心功能

- **角色好感相册**
  - 自动整理当前游戏与已安装扩展 Mod 中生效的 NPC 好感事件，按角色分册展示。
  - 支持为事件设置好记的**自定义名称**并用于搜索（原始事件 ID 依然保留，名称配置跨本机存档共用）。
- **直观的触发条件与前置跳转**
  - 将复杂的事件脚本条件（好感心数、已看事件、信件、季节、日期、时间、金钱、背包物品、社区中心进度等）转化为易读的文字清单，并标出当前存档“已满足”或“尚缺”哪一步。
  - 点击条件中的前置事件 ID 可直接跳转到对应事件详情；无法静态判定的特殊或第三方条件会如实标为“未知”，不会误导进度。
- **安全回放与演出控制**
  - 已触发过的事件可直接点击重温；也可开启“**一键解锁全部**”在画廊中临时开放所有可回放剧情（仅改变画廊内的浏览权限，绝不修改存档真实好感度或事件进度）。
  - 回放时自动切入事件所需的季节、时间与原版天气，并在回放结束、跳过或异常退出时恢复玩家原本的位置、时间与状态；回放期间自动锁定保存以保护存档。
  - 支持 **1x / 2x / 4x 倍速**切换与普通对话自动推进（遇到剧情选项时会自动停下等待玩家选择）。
- **纯净剧照抓拍与封面管理**
  - 回放途中可随时抓拍不含对话框和 HUD 的画面，保存在本地的 `event-photos/` 目录中。
  - 支持将抓拍的剧照设为相册与详情页的事件封面，随时替换或恢复默认。
- **全局事件查询与来源追溯**
  - 除角色主相册外，提供独立的**事件查询器**，支持按名称、ID、角色、地点、所属模组及季节/天气/心数等条件组合筛选。
  - 详情页直接显示事件的**原始来源**（原版或具体的 Content Patcher / AliveNpcs 模组及定义文件），方便排查剧情出处。
- **外观适配与多语言支持**
  - 自动读取当前生效的 Content Patcher 肖像与行走图，并兼容 **Portraiture**、**DDFC**、**Scale Up Unofficial** 等高清/动态立绘框架。
  - 完整支持键鼠与手柄操作，内置全部 12 种游戏官方语言，界面随窗口分辨率与 UI 缩放自适应调整。

#### 事件分类说明

画廊将当前可识别的地图事件分为三类（纯过场转换与内部逻辑会被自动过滤）：

| 类型 | 查看位置 | 回放规则 |
| --- | --- | --- |
| **好感剧情** | 角色相册、事件查询 | 已在存档中经历过，或开启“一键解锁全部” |
| **普通剧情** | 事件查询 | 需先在 GMCM 开启“普通事件回放”（默认关闭）；未经历过的同样需开启“一键解锁” |
| **前置事件** | 事件查询、条件前置链接 | 仅作为解锁标记参考（展示触发条件、时机、当前获得状态与关联剧情），不提供回放 |

### 安装与可选配置

1. 确保已安装 **Stardew Valley 1.6.15+** 与 **SMAPI 4.5.2+**。
2. 下载并解压压缩包，将 `StardewGallery` 文件夹放入游戏的 `Mods` 目录。
3. 通过 SMAPI 启动游戏。

**可选依赖：**
- **Generic Mod Config Menu (GMCM)**：安装后可在游戏设置菜单中直接配置快捷键、普通事件回放、对话自动推进、来源修改追踪及 AI 模组名单排除。未安装时也可直接编辑模组目录下的 `config.json`。
- **角色美化框架**：无需为了画廊专门安装任何美化前置。若已安装 Portraiture，请先在正常游戏对话中确认立绘包已切换生效，画廊即会同步读取；加载异常时会自动回退到普通肖像或默认占位图标。

### 从旧版本更新

更新前请退出游戏并备份原 `Mods/StardewGallery` 文件夹。建议**先删除旧版模组文件夹，将新版解压为干净的 `StardewGallery` 目录**（以清理 2.8.0 及更早版本遗留的 SQLite 组件），再将以下个人数据拷回：

| 文件 / 目录 | 说明 |
| --- | --- |
| `config.json` | 快捷键与模组设置 |
| `event-photos/` | 各存档抓拍的回放剧照与自定义封面 |
| `user-data/` | 跨存档共用的自定义事件名称 |

### 操作指南

- **打开画廊**：默认按 `G` 键，或打开原版菜单点击顶部的画廊标签页。
- **浏览与查询**：画廊首页搜索框用于快速定位角色；点击右下角“事件查询”可进入全局搜索与多条件筛选面板（手柄按 `Y` 打开筛选，肩键翻页，`B` 返回上一级）。
- **倍速与抓拍**：回放时点击右上角按钮或按快捷键切换 `1x / 2x / 4x` 倍速；按 `F8`、手柄左肩键（LB）或点击相机图标即可抓拍无 UI 剧照，并在事件详情页点击缩略图管理封面。
- **卸载模组**：直接删除 `Mods/StardewGallery` 文件夹即可（如需留存剧照与自定义命名，请提前备份 `event-photos/` 与 `user-data/`）。

### 来源追溯与排除名单说明

- **查看事件来源**：事件详情页“地点”下方会显示该事件的原始提供者（原版、具体 Content Patcher 内容包或 AliveNpcs 场景包），点击即可查看匹配的定义文件路径。画廊在首次打开目录时自动通过静态文件扫描建立索引，无需额外配置。
- **运行时修改追踪（可选）**：GMCM 中的“追踪事件修改（重启生效）”（`EnableEventSourceDiagnostics`，默认关闭）用于记录运行时其他模组对事件文本或脚本的二次修改，当前仅针对 SMAPI 4.5.2.0 生效；关闭或版本不匹配不影响基础来源识别。实现细节见 [事件来源实现说明](docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md)。
- **AI 模组排除名单（可选）**：在 GMCM 开启“排除 AI 名单模组”（`EnableAiModExclusion`，默认关闭）后，画廊会按社区维护的 [AI Mod Exclusion](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion) 名单隐藏已确认由名单内 Mod ID 原始提供的事件。开启后每次启动与读档会异步拉取最新名单，离线或等待期间使用内置名单兜底；仅匹配原始提供者的 Mod ID，不影响原版、来源不明或其他正常模组，也不会删除任何存档进度与剧照。

### 兼容性与已知限制

- **仅限单人模式**：事件回放仅支持单人游戏，不支持联机模式。
- **基于当前状态**：画廊目录与回放均基于当前存档状态及当前生效的模组内容动态解析。未满足 Content Patcher `When` 加载条件的分支、节日主流程、婚礼仪式、夜间特殊结算（`FarmEvent`）、影院放映及纯 C# 编写的自定义事件不在收录范围内。
- **存档与第三方保护边界**：回放快照会完整保护并恢复原版状态与奖励（如好感、金钱、信件、已看记录、爷爷评价蜡烛等），但如果第三方模组通过自定义事件命令改写了外部文件或私有状态，则超出原版快照的恢复范围。

### 构建与开发检查

从源码构建需要 **.NET 8 SDK** 以及安装了 SMAPI 的游戏目录（项目目标框架仍为 `net6.0`）；运行独立检查程序还需安装 **.NET 6 Runtime**。详细说明见 [BUILDING.md](BUILDING.md)。

```sh
# 编译 Release 版本（请将 GamePath 替换为实际游戏安装路径）
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"

# 运行不依赖游戏本体的逻辑与存储检查
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

2.9.0 版本的构建、SMAPI 加载器与运行时隔离验证详情可参阅 [2.9.0 版本说明](docs/RELEASE_2.9.0_NOTES.md) 与 [整合记录](docs/INTEGRATION_2026-10-08.md)。

### 许可证

本项目基于 GNU General Public License v3.0 开源，完整条款见 [`LICENSE`](LICENSE)。

---

## English

**Stardew Gallery** is an in-game story album and event lookup tool for Stardew Valley. Whether you want to revisit a villager's heart events, figure out why the next cutscene hasn't triggered yet, or see which expansion mod added a specific story, you can browse and replay them right from a book-style menu.

### Key Features

- **Character Heart Albums**
  - Browse active NPC heart events from both the base game and installed expansion mods, organized into individual character albums.
  - Give events custom, memorable names and search by them while keeping original event IDs intact (shared across local saves in `user-data/`).
- **Plain-Language Conditions & Prerequisite Links**
  - Translates raw event preconditions (hearts, seen events, mail, season, date, time, money, inventory, community progress, etc.) into readable checklists showing what your save currently meets or is missing.
  - Click prerequisite event IDs to jump straight to their details. Unparseable or third-party conditions are clearly marked as unknown rather than guessed.
- **Safe Replay & Playback Controls**
  - Replay any event you have already seen, or toggle **Unlock All** to temporarily open all replayable stories in the gallery (this only affects gallery access and never modifies your actual save friendship or event progress).
  - Replays automatically set up the required season, time, and vanilla weather, then restore your original position, time, and state when the scene ends or is skipped. Saving is blocked during replay with a fail-safe backup.
  - Cycle playback speed between **1x, 2x, and 4x**, with optional auto-advance for normal dialogue (player choices always pause and wait for your input).
- **Clean Scene Photos & Custom Covers**
  - Capture HUD-free, dialogue-free screenshots during replay and set your favorite shot as the event's album cover. Photos are stored per save in `event-photos/`.
- **Global Event Search & Mod Source Lookup**
  - A dedicated **Event Search** lets you find ordinary stories and prerequisite markers by name, ID, character, location, mod source, season, weather, or heart threshold.
  - Event details display the **original provider** (base game or a specific Content Patcher / AliveNpcs mod and its definition file) right below the location.
- **Cosmetic Compatibility & Full Localization**
  - Automatically displays active Content Patcher portraits and sprites, with built-in support for **Portraiture**, **DDFC**, and **Scale Up Unofficial**.
  - Full keyboard, mouse, and controller support, with adaptive UI scaling and translations for all 12 official game languages.

#### Event Categories

The gallery organizes discoverable location events into three categories (pure movement transitions and internal scripts are filtered out):

| Category | Where to Find | Replay Rules |
| --- | --- | --- |
| **Heart Stories** | Character Albums & Event Search | Replayable once seen in your save, or with **Unlock All** enabled |
| **Ordinary Stories** | Event Search | Enable **Ordinary Event Replay** in GMCM first (off by default); unseen stories also require **Unlock All** |
| **Prerequisite Events** | Event Search & Prerequisite Links | Reference only (shows unlock conditions, trigger timing, progress, and linked stories); not replayable |

### Installation & Optional Dependencies

1. Install **Stardew Valley 1.6.15+** and **SMAPI 4.5.2+**.
2. Extract the archive and place the `StardewGallery` folder into your `Mods` directory.
3. Launch the game through SMAPI.

**Optional Mods:**
- **Generic Mod Config Menu (GMCM)**: Configure keybinds, ordinary event replay, dialogue auto-advance, source modification tracking, and the AI mod exclusion list in-game. Without GMCM, you can edit `config.json` directly.
- **Cosmetic Frameworks**: None are required to use the gallery. For Portraiture packs, make sure the portrait pack is active in normal game dialogue first; if an image fails to load, the gallery falls back to standard portraits or a placeholder icon.

### Updating from Older Versions

Close the game and back up `Mods/StardewGallery`. We recommend **extracting the new version into a clean `StardewGallery` folder** (so retired SQLite libraries from 2.8.0 and earlier are removed) and copying back your player data:

| File / Folder | Purpose |
| --- | --- |
| `config.json` | Keybinds and mod settings |
| `event-photos/` | Replay screenshots and custom covers for each save |
| `user-data/` | Custom event names shared across local saves |

### Controls & Usage

- **Open the Gallery**: Press `G` (default) or click the Gallery tab in the vanilla game menu.
- **Browse & Filter**: The home search bar filters characters. Click **Event Search** in the bottom-right corner to search across all events and open the filter panel (on a controller, press `Y` for filters, shoulder buttons to page, and `B` to go back).
- **Speed & Screenshots**: During replay, use the top-right button or hotkey to cycle `1x / 2x / 4x`. Press `F8`, the controller's Left Shoulder button, or the camera icon to take a clean screenshot, then click the thumbnail on the event detail page to manage covers.
- **Uninstall**: Delete `Mods/StardewGallery` (keep `event-photos/` and `user-data/` if you want to save your screenshots and custom event names).

### Event Sources & Exclusion List

- **Original Source Lookup**: Event details automatically show which mod (or vanilla XNB) defines each event and which file it comes from by scanning loaded Content Patcher and AliveNpcs files when the catalog is first opened.
- **Runtime Modification Tracking (Optional)**: Enabling *Track event modifications (restart required)* (`EnableEventSourceDiagnostics`, off by default) records secondary runtime edits made by other mods. This optional tracker currently targets SMAPI 4.5.2.0; static file source lookup works regardless of this setting. See [Implementation Notes](docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md) for details.
- **AI Mod Exclusion List (Optional)**: When *Exclude AI-listed mods* (`EnableAiModExclusion`, off by default) is enabled in GMCM, the gallery hides events whose confirmed original provider matches the community [AI Mod Exclusion](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion) list. The latest list is fetched asynchronously on startup and save load, with a bundled list used as a fallback. Only confirmed original provider Mod IDs are matched; vanilla, unknown, and unlisted mods remain untouched, and no save data or photos are deleted.

### Compatibility & Limitations

- **Single-Player Only**: Event replay is designed for single-player mode only.
- **Current-State Catalog**: Both the catalog and replays reflect your current save state and currently active mod patches. Inactive Content Patcher branches, main festival flows, wedding ceremonies, overnight `FarmEvent` scenes, movie screenings, and pure C# custom events are not included.
- **Save Protection Scope**: Replays snapshot and restore native player state and rewards (friendship, money, mail, seen event IDs, Grandpa's evaluation candles, etc.), but cannot roll back external files or private state modified by third-party custom event commands.

### Building & Validation

Building from source requires the **.NET 8 SDK** and a game installation with SMAPI (the project targets `net6.0`). Running the standalone check suites also requires the **.NET 6 Runtime**. See [BUILDING.md](BUILDING.md) for full details.

```sh
# Build Release (replace GamePath with your Stardew Valley folder)
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"

# Run standalone logic and persistence checks
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

For 2.9.0 validation details, see the [2.9.0 Release Notes](docs/RELEASE_2.9.0_NOTES.md) and [Integration Report](docs/INTEGRATION_2026-10-08.md).

### License

This project is licensed under the GNU General Public License v3.0. See [`LICENSE`](LICENSE) for the full terms.
