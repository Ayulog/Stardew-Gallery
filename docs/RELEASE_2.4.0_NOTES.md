# Stardew Gallery 2.4.0 / 星露谷画廊 2.4.0

This release includes all updates since the previous GitHub release, 2.0.3.

## Highlights

- Browse event albums and dedicated detail pages with readable conditions, met/missing/unknown indicators and current values where available.
- Capture photos during Gallery replay using F8, the camera button or the controller's left shoulder button. Choose or replace event covers, restore defaults and archive photos. Photos are stored per save and event.
- Follow prerequisite event IDs and search loaded events, including view-only details for events outside the character gallery. Navigation restores your previous search, scroll position and focus.
- Reduce unknown conditions with read-only checks for weekday, money, inventory, skills, dialogue records, NPC visibility, homes, festivals and more. Supported festival/date/player-stat queries have readable descriptions in all 12 maintained languages.
- Improve search performance, isolate keyboard shortcuts while editing search, align controls and fit longer translations. Simplify equivalent negative weekday/season lists and improve location names.
- Harden current-event selection and replay cleanup, and stop collecting new historical event records.

## Install Or Update

Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or compatible newer versions. Generic Mod Config Menu is optional.

Close the game, back up the existing `Mods/StardewGallery` folder, then extract the archive into `Mods`. Preserve your `config.json` and `event-photos/` when updating. Press G to open the gallery.

## Scope

The catalog shows currently loaded content. Inactive content-pack branches may not be available; ordinary event category browsing is planned for a later version. Newly searchable non-character events remain view-only. Random outcomes, missing entry context and unsupported third-party conditions can still show an explained question mark. Replay is single-player only.

Validation: build, logic/localization checks and persistence/photo checks passed. Isolated game-assembly checks cover state reads and 12-language detail rendering; they do not constitute exhaustive in-game testing of every content pack or platform.

## 中文更新说明

本次整合自GitHub正式版2.0.3之后的全部更新：

- 新增事件相册与详情页，用勾、叉、问号展示条件状态，并在可用时显示当前值与要求。
- 回放时可用F8、相机按钮或手柄左肩截图；按存档保存，支持选封面、替换、恢复默认和移除归档。
- 点击前置事件ID跳转，搜索已加载事件，包括角色目录外事件的只读详情；返回时保留搜索、滚动位置和焦点。
- 补齐星期、金钱、背包、技能、对话记录、NPC可见性、住宅与节日等只读判定；受限的节日、日期与玩家统计查询提供12语言可读说明。
- 优化搜索性能与输入快捷键隔离，统一按钮排版，改善地点名称和过长的星期、季节否定描述。
- 加固当前事件选择和回放退出恢复，停止新增历史事件采集。

更新前关闭游戏并备份原Mod目录，保留`config.json`和`event-photos/`。将压缩包内`StardewGallery`放入`Mods`，按G打开。要求游戏1.6.15、SMAPI4.5.2或更新兼容版本，GMCM可选。

目录取决于当前已加载内容；普通事件分类浏览尚未加入，角色目录外的新搜索结果仅供查看。随机结果、入场上下文缺失及未支持的第三方条件仍可能显示有原因的问号。回放仅支持单人。

构建、逻辑/本地化和存储/截图检查通过；隔离游戏程序集验证了状态读取和12语言详情渲染，不等同于所有内容包与平台的完整实机测试。

[Full changelog / 完整更新日志](https://github.com/Ayulog/Stardew-Gallery/blob/v2.4.0/CHANGELOG.md) · [Source / 源码](https://github.com/Ayulog/Stardew-Gallery/tree/v2.4.0)
