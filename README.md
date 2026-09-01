# Stardew Gallery / 星露谷画廊

Copyright (C) 2026 sjt38. Licensed under the GNU General Public License v3.0.

[中文](#中文) · [English](#english)

## 中文

星露谷画廊把当前游戏中生效的角色好感事件整理成一本相册，并允许在单人存档中安全回放已经解锁的事件。

### 当前功能

- 按角色浏览当前游戏和已安装 Mod 提供的好感事件。
- 搜索角色或事件 ID，并查看事件条件、观看状态和角色关系信息。
- 回放当前生效的事件版本；自然观看后，还可保留同一事件 ID 的不同脚本版本供以后选择。
- 回放速度可在 1x、2x、4x 之间切换；普通对话可选择自动继续，选项不会自动选择。
- 回放前自动备份存档，结束后恢复玩家位置、时间和主要游戏状态；回放期间禁止保存。
- 支持键鼠和手柄操作，快捷键可配置多个单键或组合键。
- 支持简体中文和英文，界面会随分辨率与 UI 缩放自动适配。
- 可选支持 Generic Mod Config Menu（GMCM）。

### 安装

1. 安装 Stardew Valley 1.6.15 和 SMAPI 4.5.2 或更高兼容版本。
2. 解压下载文件，将 `StardewGallery` 文件夹放入游戏的 `Mods` 文件夹。
3. 通过 SMAPI 启动游戏。

GMCM 不是必需依赖；安装后可在游戏内配置快捷键、回放提示、对话自动继续和调试诊断。

### 使用

- 默认按 `G` 打开或关闭画廊，也可点击原版菜单中的画廊标签。
- 选择角色，再选择已解锁事件进行回放。
- 回放右上角按钮或配置的快捷键可循环切换 1x / 2x / 4x。
- “一键解锁全部”只改变画廊中的查看权限，不会修改存档的实际好感度或事件进度。

### 兼容性与限制

- 事件目录取决于当前存档状态、已安装 Mod 及其条件，因此不同存档可能看到不同的当前版本。
- 历史版本只会在安装本 Mod 后自然完成事件时开始记录；若对应 Mod 或资源已移除，旧版本可能无法完整回放。
- 事件回放目前仅支持单人模式。多人模式未实测。
- Mod 不联网，也不会修改游戏原始文件或其他 Mod。

### 卸载

删除 `Mods/StardewGallery` 文件夹即可。自然观看版本记录保存在 SMAPI 的存档 ModData 中；不安装本 Mod时不会被游戏使用。

### 许可证

本项目以 GNU General Public License v3.0 发布。完整条款见 `LICENSE`。

## English

Stardew Gallery organizes the NPC heart events currently active in your game into an album and lets you safely replay unlocked events in single-player saves.

### Features

- Browse heart events from the base game and installed mods by character.
- Search by character or event ID, with event conditions, watched status, and relationship details.
- Replay the currently active event version. Naturally completed events can also preserve multiple script versions sharing the same event ID.
- Cycle replay speed between 1x, 2x, and 4x. Optional auto-advance applies only to normal dialogue; choices always wait for the player.
- Automatic pre-replay save backup and post-replay state restoration. Saving is blocked during replay.
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
- Choose a character, then select an unlocked event to replay.
- Use the top-right replay button or the configured binding to cycle 1x / 2x / 4x.
- “Unlock all” changes gallery visibility only. It does not alter friendship or event progress in the save.

### Compatibility and limitations

- The catalog depends on the current save state, installed mods, and their conditions, so different saves may expose different current versions.
- Historical versions are recorded only after this mod is installed and an event completes naturally. Old versions may not fully replay if their providing mod or assets are removed.
- Event replay currently supports single-player only. Multiplayer has not been tested.
- The mod does not access the internet or modify game files or other mods.

### Uninstall

Delete the `Mods/StardewGallery` folder. Naturally watched version records remain in SMAPI save ModData and are ignored while the mod is not installed.

### License

This project is licensed under the GNU General Public License v3.0. See `LICENSE` for the full terms.
