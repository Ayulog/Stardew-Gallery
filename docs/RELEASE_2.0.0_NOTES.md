# Stardew Gallery 2.0.0 / 星露谷画廊 2.0.0

Stardew Gallery is a bilingual current-state heart-event gallery and planning tool for Stardew Valley 1.6. Browse the events produced by your current game and installed mods, understand their requirements, see what your save is missing, and safely preview or replay supported events.

星露谷画廊是一款支持中英双语的“当前状态”好感事件图鉴与规划工具。它会读取当前游戏与已安装 Mod 实际生效的事件，解释触发条件和进度缺口，并安全地预览或回放受支持的事件。

## Highlights / 主要功能

- Browse currently active NPC heart events by character. / 按角色浏览当前实际生效的好感事件。
- Search by character or event ID. / 按角色或事件 ID 搜索。
- Readable condition explanations and progress gaps, with unknown mod conditions clearly marked. / 可读条件说明与进度缺口，无法安全解析的 Mod 条件会明确标注。
- Current-state replay using the exact currently resolved event script. / 使用当前精确生效脚本进行回放。
- Safe preview for supported unmet requirements such as friendship, prerequisite events, mail, season, date, and time. / 对好感、前置事件、邮件、季节、日期与时间等支持条件进行安全预览。
- Temporary preview state is restored afterwards; saving is blocked during replay and preview. / 预览结束后恢复临时状态，回放与预览期间禁止保存。
- 1x / 2x / 4x playback speed and optional normal-dialogue auto-advance. / 支持 1x、2x、4x 速度及普通对话自动继续。
- Keyboard, mouse, and controller support, including configurable multi-key bindings. / 支持键鼠、手柄及可配置的多按键绑定。
- Adaptive UI scaling and optional Generic Mod Config Menu support. / 自适应界面缩放，可选支持 GMCM。

## Requirements / 前置要求

- Stardew Valley 1.6.15 or later compatible version. / Stardew Valley 1.6.15 或更高兼容版本。
- SMAPI 4.5.2 or later compatible version. / SMAPI 4.5.2 或更高兼容版本。
- Generic Mod Config Menu is optional. / Generic Mod Config Menu 为可选依赖。

## Installation / 安装

Extract the archive and place the `StardewGallery` folder in the game's `Mods` folder, then launch through SMAPI.

解压压缩包，将 `StardewGallery` 文件夹放入游戏 `Mods` 目录，然后通过 SMAPI 启动游戏。

## Important notes / 注意事项

- The gallery reflects the current save state and currently installed content; different saves may expose different event versions. / 画廊反映当前存档与当前安装内容，不同存档可能显示不同事件版本。
- Not every mod condition can be safely simulated. Unsupported or opaque conditions are reported instead of being forced. / 并非所有 Mod 条件都能安全模拟；不支持或不透明的条件会被标注，不会强行预览。
- Replay and preview currently support single-player only. Multiplayer has not been tested. / 回放与预览目前仅支持单人模式，多人模式未实测。
- Weather, relationship status, and complex world-state requirements are analysis-only and are not injected. / 天气、关系状态及复杂世界状态条件仅分析，不进行临时注入。

Full documentation and source code: https://github.com/Ayulog/Stardew-Gallery

Licensed under GNU GPL-3.0.
