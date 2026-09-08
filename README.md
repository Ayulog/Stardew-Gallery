# Stardew Gallery / 星露谷画廊

Copyright (C) 2026 sjt38. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

## 中文

星露谷画廊是一本“当前事件”图鉴：浏览角色与普通事件，查看触发条件，为支持回放的事件拍照并设置封面。

### 当前功能

- 角色相册分为全部、好感和普通事件；有明确参与关系的普通事件也归入角色，多人剧情可出现在多个相册。
- 首页右上“事件目录”浏览当前已发现的完整事件集合，按类型、地点、角色和事件 ID 筛选。相同内容的多个地点来源合并展示，可选择具体来源查看。
- 搜索角色或事件 ID；已加载的其他事件也可按 ID 查看。从条件下的事件 ID 跳转前置事件，逐层返回原位置。
- 回放截图按存档保存，可选封面、替换封面、恢复默认或移除归档。
- 阅读型条件说明与进度缺口：好感/心数、看过事件、邮件、季节、日期、时间等用可读文本呈现；无法安全解析的模组条件会明确标注，而不是猜测。
- 支持金钱、背包、技能、对话记录、NPC可见性、住宅与节日等只读条件，以及受限的节日/日期/统计量查询。随机结果、缺少入场位置或未支持的第三方条件会说明未知原因。
- 当前状态回放：所有回放都从当前已解析的事件内容与当前游戏状态启动，不再使用历史冻结版本。
- 支持回放的已观看事件可直接回放；GMCM 的“解锁画廊（当前存档）”可用于调试未观看事件，画廊内不再提供解锁按钮。
- 普通事件有可靠地点、完整脚本且通过兼容检查时也可回放，无需好感要求或 NPC 归属。特殊流程、未支持命令或缺少上下文的事件说明不可回放原因。
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

GMCM 不是浏览画廊的必需依赖；安装后可在游戏内配置快捷键、回放提示、对话自动继续、调试诊断和当前存档的画廊解锁开关。

### 使用

- 默认按 `G` 打开或关闭画廊，也可点击原版菜单中的画廊标签。
- 选择角色，查看事件的可读条件和缺失/未知要求；已解锁事件可点击“回放”。
- 回放右上角按钮或配置的快捷键可循环切换 1x / 2x / 4x。
- 回放时按 F8、手柄左肩或点击相机截图；从详情缩略图管理截图与封面。截图不含对话框和 HUD，保存在本 Mod 的 `event-photos/` 内，卸载前可保留此目录。
- 首页搜索角色或事件 ID；完整事件目录还可搜索地点。详情及多来源页面返回时保留原筛选、页码和焦点。
- 调试解锁：进入单人/主机存档，在 GMCM 中开启“解锁画廊（当前存档）”并保存。关闭可恢复普通限制，不修改真实好感或已观看记录；退出到标题、切换存档和回放中不会将设置串用。GMCM 的重置按其原生行为立即保存默认设置。

### 兼容性与限制

- 事件目录取决于当前存档状态、已安装 Mod 及其条件，因此不同存档可能看到不同的当前版本。
- 回放使用当前生效内容，不是历史回放。
- 普通事件补采来自 SMAPI 已观察到的 `Data/Events` 资产；不扫描全部 CP 包，也不会绕过 CP 的加载条件。没有现成地点的补采来源只提供详情。CP 仅由相应内容包要求，画廊不强制依赖 CP。
- 新增普通事件回放只开放已核对的原版演出指令；特殊结束/跳过动作、任意 Action 与第三方命令需额外兼容。运行中仍受已安装 Mod 的补丁影响。
- 未观看事件保持锁定；条件说明不会自动修改存档进度。
- 事件回放目前仅支持单人模式。多人模式未实测。
- Mod 不联网，也不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。

### 许可证

本项目以 GNU General Public License v3.0 发布。完整条款见 `LICENSE`。

## English

Stardew Gallery is a current-state event album: browse character and ordinary events, inspect requirements, and take photos to use as covers for supported replays.

### Features

- Browse all, heart, and ordinary events in character albums. Events with clear NPC participation can appear in multiple relevant albums.
- Open the Event Catalog at the top right of the home page. Filter by type or location and search event IDs, characters, and places. Identical content across locations is grouped, with each exact source available separately.
- Search by character or event ID, including loaded events outside the heart-event gallery. Follow prerequisite IDs and return through previous views with their positions preserved.
- Capture replay photos per save, choose or replace covers, restore defaults, and archive removed photos.
- Readable condition explanation with progress gaps: friendship/hearts, seen events, mail, season, day, time, and more are shown in plain text; mod conditions that can't be parsed safely are labeled as unknown rather than guessed.
- Read-only checks cover money, inventory, skills, dialogue records, NPC visibility, homes and festivals, plus restricted festival/date/stat queries. Random outcomes, missing entry positions and unsupported third-party conditions explain why their results remain unknown.
- Current-state replay: every replay launches from currently resolved event content and current game state, not from a frozen historical version.
- Replay supported events you have seen, or enable the current save's gallery unlock option in GMCM for debugging. The gallery itself no longer has an unlock button.
- Ordinary events can replay with a confirmed location, complete script and supported commands, even without a friendship requirement or NPC owner. Other entries explain why replay is unavailable.
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

GMCM is optional for browsing. It provides settings for keybinds, replay warnings, dialogue auto-advance, diagnostics, and the current save's gallery unlock switch.

### Usage

- Press `G` by default to toggle the gallery, or use its tab in the vanilla game menu.
- Choose a character, review readable conditions and missing/unknown requirements, then click "Replay" on an unlocked event.
- Use the top-right replay button or the configured binding to cycle 1x / 2x / 4x.
- During replay, press F8, the controller's left shoulder, or the camera button to capture the scene without dialogue/HUD. Open the detail thumbnail to manage photos/covers. Photos live in this mod's `event-photos/` folder; keep it when uninstalling to retain your pictures.
- Use the home search for characters or event IDs; the full catalog also searches places. Returning from details or source selection preserves filters, page and focus.
- For debugging, load a single-player/host save, enable "Unlock gallery (current save)" in GMCM, and save settings. Turning it off restores normal limits without changing friendship or watched events. Title-screen, save-switch and replay states do not transfer the setting to another save. GMCM's Reset immediately saves defaults, following its native behavior.

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Replay uses currently active content, not historical versions.
- Discovery supplements existing locations with `Data/Events` assets observed by SMAPI. It does not parse every CP pack or bypass CP loading conditions. Sources without existing locations are view-only. Gallery does not require CP; content packs may require it.
- Newly supported ordinary replays use reviewed native staging commands. Special endings/skip actions, arbitrary Actions and third-party commands need additional compatibility work. Other installed mods can still affect native execution.
- Unseen events remain locked; condition explanations never rewrite save progress.
- Event replay currently supports single-player only. Multiplayer has not been tested.
- The mod does not access the internet or modify game files or other mods.

### Uninstall

Delete the `Mods/StardewGallery` folder.

### License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for the full terms.
