# Stardew Gallery / 星露谷画廊

Copyright (C) 2026 sjt38. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

## 中文

星露谷画廊以NPC好感剧情收藏为主题，提供条件说明、剧情重温、截图与封面；配套自助查询器用于查找其他剧情及前置依赖。

### 当前功能

- 按角色浏览当前游戏与已安装 Mod 实际生效的好感事件。
- 主相册只收好感剧情；自助查询支持剧情ID、NPC与地点，并可查看前置依赖，返回保留筛选、滚动和焦点。普通剧情不计入好感剧情收藏。
- 转场、发信、设置标记等纯内部流程不作为事件列出或回放，相关前置仅解释为解锁步骤。短但具有演出内容的剧情仍可收录。
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
- 可选支持 Generic Mod Config Menu（GMCM）。

### 安装

1. 安装 Stardew Valley 1.6.15 和 SMAPI 4.5.2 或更高兼容版本。
2. 解压下载文件，将 `StardewGallery` 文件夹放入游戏的 `Mods` 文件夹。
3. 通过 SMAPI 启动游戏。

GMCM 不是必需依赖；安装后可配置普通事件回放、快捷键、回放提示、自动对白和调试诊断。未安装GMCM时可在config.json设置`EnableOrdinaryEventReplay`，默认false。

### 使用

- 默认按 `G` 打开或关闭画廊，也可点击原版菜单中的画廊标签。
- 选择角色，查看事件的可读条件和缺失/未知要求；已解锁事件可点击“回放”。
- 回放右上角按钮或配置的快捷键可循环切换 1x / 2x / 4x。
- 回放时按 F8、手柄左肩或点击相机截图；从详情缩略图管理截图与封面。截图不含对话框和 HUD，保存在本 Mod 的 `event-photos/` 内，卸载前可保留此目录。
- 首页原搜索框用于角色与好感剧情；“事件查询”入口可搜索更广的剧情。前置多来源仍分别显示，未加载时就地提示，内部解锁步骤没有单独入口。
- “一键解锁全部”只改变画廊中的查看权限，不会修改存档的实际好感度或事件进度。

### 兼容性与限制

- 事件目录取决于当前存档状态、已安装 Mod 及其条件，因此不同存档可能看到不同的当前版本。
- 回放使用当前生效内容，不是历史回放。
- 查询范围是当前可发现的地点事件内容，不保证覆盖未生效CP分支、夜间FarmEvent、节日主流程或纯C#事件；好感剧情分类依据正向关系证据与可确认的续篇，非社交NPC关联不等于主相册收录。
- 回放统一保护已覆盖的原版进度与奖励，部分效果在演出时阻止。第三方命令仍由其原框架执行，外部文件、私有状态和任意回调不在原版快照保障范围内；新版本回放需按开发包实测说明验证。
- 未观看事件保持锁定；条件说明不会自动修改存档进度。
- 事件回放目前仅支持单人模式。多人模式未实测。
- Mod 不联网，也不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。

### 许可证

本项目以 GNU General Public License v3.0 发布。完整条款见 `LICENSE`。

## English

Stardew Gallery collects NPC heart stories with readable requirements, replay photos and covers. A separate self-service search helps players find other stories and prerequisite events.

### Features

- Browse the heart events actually active in the base game and installed mods, by character.
- The album contains heart stories; the separate search finds stories by ID, NPC and location. Follow prerequisites and return with filters, scroll and focus preserved. Ordinary stories do not count toward the heart-story collection.
- Internal transitions, mail dispatch and marker-only workflows are hidden from story lists and replay. Prerequisites can still describe their unlock steps; brief scenes with narrative content remain eligible for display.
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
- Optional Generic Mod Config Menu support.

### Installation

1. Install Stardew Valley 1.6.15 and SMAPI 4.5.2 or a later compatible version.
2. Extract the download and place the `StardewGallery` folder in the game's `Mods` folder.
3. Launch the game through SMAPI.

GMCM is optional. It configures ordinary replay, keybinds, warnings, dialogue auto-advance and diagnostics. Without GMCM, set `EnableOrdinaryEventReplay` in config.json; its default is false.

### Usage

- Press `G` by default to toggle the gallery, or use its tab in the vanilla game menu.
- Choose a character, review readable conditions and missing/unknown requirements, then click "Replay" on an unlocked event.
- Use the top-right replay button or the configured binding to cycle 1x / 2x / 4x.
- During replay, press F8, the controller's left shoulder, or the camera button to capture the scene without dialogue/HUD. Open the detail thumbnail to manage photos/covers. Photos live in this mod's `event-photos/` folder; keep it when uninstalling to retain your pictures.
- Use the home search for characters and heart stories, or open Event Search for broader story lookup. Prerequisite sources remain separate; missing content and internal unlock steps are explained without exposing workflow cards.
- "Unlock all" changes gallery visibility only. It does not alter friendship or event progress in the save.

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Replay uses currently active content, not historical versions.
- Search covers currently discoverable location events, not every inactive CP branch, nightly FarmEvent, festival flow or pure C# scene. Heart-story classification uses positive relationship evidence and confirmed continuations; NPC participation alone is not album membership.
- Replay protects covered native state and rewards, suppressing some effects during presentation. Third-party commands still run through their frameworks; external files, private state and arbitrary callbacks are outside the native snapshot guarantee. New replay behavior requires the development package's in-game checks.
- Unseen events remain locked; condition explanations never rewrite save progress.
- Event replay currently supports single-player only. Multiplayer has not been tested.
- The mod does not access the internet or modify game files or other mods.

### Uninstall

Delete the `Mods/StardewGallery` folder.

### License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for the full terms.
