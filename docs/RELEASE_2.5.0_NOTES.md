# Stardew Gallery 2.5.0 / 星露谷画廊 2.5.0

Keep your heart-story album and use the separate Event Search to find ordinary stories and understand prerequisite steps.

## Highlights

- Search by event name, ID, character or location. Use controller-friendly filters for type, character, location, progress, conditions, season, time, weather and hearts.
- View prerequisite markers with unlock conditions, trigger timing and related stories. They are reference records, with no replay or collection credit.
- Enable ordinary story replay in GMCM, off by default. Unseen stories still require Unlock All.
- Give events personal names and search them. Names are shared across local saves; photos and viewing progress keep their existing save scope.
- Improve negative-condition wording, actor aliases, shared heart-story ownership, duplicate location choices and missing location names, including Ginger Island.
- Fix Grandpa's global scenes being counted repeatedly in every location; retain their original farmhouse entries. Fewer results after updating can reflect removal of these duplicates.
- Keep the replay confirmation inside the current window, with wrapped text and visible controls in all supported languages.

## Install Or Update

Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or later compatible versions. GMCM is optional. Content Patcher is only needed by content packs that require it.

Close the game and back up `Mods/StardewGallery`. Extract the archive into `Mods`, preserving `config.json`, `event-photos/` and `user-data/`. The latter stores personal event names. Press G to open the gallery. In Event Search, Y opens filters on a controller, shoulder buttons page through lists, and B returns one level.

To uninstall, remove `Mods/StardewGallery` after keeping any photos or personal names you want to retain.

## Compatibility And Validation

The catalog reflects currently discoverable content; saves and CP conditions can produce different lists. Inactive content-pack branches and arbitrary C# scenes are not guaranteed to appear. Unsupported conditions remain unknown. Replay is single-player only and protects covered native state, not arbitrary third-party files, private state or callbacks.

Build, logic/localization and persistence checks passed. The user accepted the final development build after Windows gameplay testing, including the confirmation-dialog fix. Isolated dialog checks cover all 12 languages and five viewport sizes. Release preparation changes documentation only and reuses the accepted program files. This is not exhaustive testing of every mod combination or platform.

## 中文

2.5.0保留以好感剧情为主的角色相册，并完善独立事件查询：

- 按事件名称、ID、角色或地点搜索，支持手柄操作及类型、角色、地点、进度、条件、季节、时间、天气和好感筛选。
- 前置标记独立展示解锁条件、触发时机和关联剧情，不提供回放、不计入剧情收藏。
- 普通剧情回放可在GMCM开启，默认关闭；未经历剧情仍需一键解锁。
- 支持自定义事件名称及搜索，名称在本机存档间共用；截图和观看进度保留原有存档范围。
- 改善否定条件、重复角色、共同好感事件归属、重复地点选项和缺失地名，补齐姜岛等原版地点。
- 修复爷爷两段全局剧情被计入每张地图的问题，保留原始农舍条目；更新后结果数减少可能来自去除这些重复副本。
- 回放确认框适配当前窗口，各语言正文自动换行，按钮保持可见。

要求游戏1.6.15、SMAPI4.5.2或更新兼容版本。GMCM可选；只有对应内容包需要时才需安装CP。

更新前退出游戏并备份原模组目录。将压缩包中的`StardewGallery`放入`Mods`，保留`config.json`、`event-photos/`和`user-data/`。按G打开画廊；首页只找角色。事件查询中，手柄Y打开筛选，肩键翻页，B返回一级。卸载时可移除模组文件夹，提前保留需要的照片和个人名称。

目录随当前存档和内容加载条件变化，不保证包含未生效分支或任意C#场景；未知条件仍保留问号。回放仅单人可用，保护范围不含第三方外部文件、私有状态和任意回调。

构建、逻辑/本地化、存储检查通过；用户已在Windows实机认可最终开发包，包括回放确认框修复。弹窗通过12语言、五种视口隔离检查。发布准备仅修改文档，复用已验收程序文件；不等同于所有模组组合与平台全面实测。

[Changelog / 更新日志](https://github.com/Ayulog/Stardew-Gallery/blob/v2.5.0/CHANGELOG.md) · [Source and license / 源码与许可](https://github.com/Ayulog/Stardew-Gallery/tree/v2.5.0)
