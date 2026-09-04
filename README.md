# Stardew Gallery / 星露谷画廊

Copyright (C) 2026 sjt38. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

## 中文

星露谷画廊是一本“当前事件”图鉴与规划工具：它发现当前安装内容实际产生的好感事件，解释每个事件的触发条件，标出当前存档还缺什么，并安全地预览或回放受支持的事件。

### 当前功能

- 按角色浏览当前游戏与已安装 Mod 实际生效的好感事件。
- 搜索角色或事件 ID，查看事件条件、当前观看状态与角色关系。
- 阅读型条件说明与进度缺口：好感/心数、看过事件、邮件、季节、日期、时间等用可读文本呈现；无法安全解析的模组条件会明确标注，而不是猜测。
- 当前状态回放：所有回放都从当前已解析的事件内容与当前游戏状态启动，不再使用历史冻结版本。
- 安全预览：对部分未满足条件（如好感、前置事件、邮件、季节、时间），可暂时模拟并恢复，帮助确认事件演出。
- 回放/预览全程禁止保存；结束后恢复玩家位置、时间与主要游戏状态。
- 回放速度可在 1x、2x、4x 之间切换；普通对话可选择自动继续，选项不会自动选择。
- 支持键鼠和手柄操作，快捷键可配置多个单键或组合键。
- 支持简体中文和英文，界面随分辨率与 UI 缩放自动适配。
- 可选支持 Generic Mod Config Menu（GMCM）。

### 安装

1. 安装 Stardew Valley 1.6.15 和 SMAPI 4.5.2 或更高兼容版本。
2. 解压下载文件，将 `StardewGallery` 文件夹放入游戏的 `Mods` 文件夹。
3. 通过 SMAPI 启动游戏。

GMCM 不是必需依赖；安装后可在游戏内配置快捷键、回放安全提示、对话自动继续和调试诊断。

### 使用

- 默认按 `G` 打开或关闭画廊，也可点击原版菜单中的画廊标签。
- 选择角色，查看事件的可读条件、缺失/未知要求，并根据可用能力点击“回放”或“预览”。
- 回放右上角按钮或配置的快捷键可循环切换 1x / 2x / 4x。
- “一键解锁全部”只改变画廊中的查看权限，不会修改存档的实际好感度或事件进度。

### 兼容性与限制

- 事件目录取决于当前存档状态、已安装 Mod 及其条件，因此不同存档可能看到不同的当前版本。
- 回放/预览代表“当前状态”，不是历史回放；事件结束后恢复临时状态。
- 并非每个事件都可安全预览：无法可靠解析或模拟的条件会降级为“仅可查看”，不会伪造成功。
- 事件回放/预览目前仅支持单人模式。多人模式未实测。
- Mod 不联网，也不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。

### 许可证

本项目以 GNU General Public License v3.0 发布。完整条款见 `LICENSE`。

## English

Stardew Gallery is a current-state event album and planning tool: it discovers the NPC heart events your installed content actually produces, explains each event's requirements, shows what your save is still missing, and safely previews or replays supported events.

### Features

- Browse the heart events actually active in the base game and installed mods, by character.
- Search by character or event ID, with readable conditions, watched status, and relationship details.
- Readable condition explanation with progress gaps: friendship/hearts, seen events, mail, season, day, time, and more are shown in plain text; mod conditions that can't be parsed safely are labeled as unknown rather than guessed.
- Current-state replay: every replay launches from currently resolved event content and current game state, not from a frozen historical version.
- Safe preview: for some unmet conditions (friendship, prerequisite events, mail, season, time), temporarily simulate and restore so you can confirm the event.
- Saving is blocked during replay/preview; player position, time, and key state are restored afterwards.
- Cycle replay speed between 1x, 2x, and 4x. Optional auto-advance applies only to normal dialogue; choices always wait for the player.
- Keyboard, mouse, and controller navigation, with multiple configurable single-key or chord bindings.
- Simplified Chinese and English, with automatic fitting for screen resolution and UI scale.
- Optional Generic Mod Config Menu support.

### Installation

1. Install Stardew Valley 1.6.15 and SMAPI 4.5.2 or a later compatible version.
2. Extract the download and place the `StardewGallery` folder in the game's `Mods` folder.
3. Launch the game through SMAPI.

GMCM is optional. When installed, it provides in-game settings for keybinds, replay warnings, dialogue auto-advance, and diagnostics.

### Usage

- Press `G` by default to toggle the gallery, or use its tab in the vanilla game menu.
- Choose a character, review readable conditions and missing/unknown requirements, then click "Replay" or "Preview" based on the available capability.
- Use the top-right replay button or the configured binding to cycle 1x / 2x / 4x.
- "Unlock all" changes gallery visibility only. It does not alter friendship or event progress in the save.

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Replay/preview reflects the current state, not a historical replay; temporary state is restored afterwards.
- Not every event can be previewed safely: conditions that can't be reliably parsed or simulated degrade to "view only" rather than pretending success.
- Event replay/preview currently supports single-player only. Multiplayer has not been tested.
- The mod does not access the internet or modify game files or other mods.

### Uninstall

Delete the `Mods/StardewGallery` folder.

### License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for the full terms.
