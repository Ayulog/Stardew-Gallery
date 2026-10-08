# Stardew Gallery 2.9.0

2026-10-08

署名更新 / Attribution update：发布附件中的作者和版权署名统一为 `ayulog`，保留此前验证的 2.9.0 DLL。The package author and copyright attribution now use `ayulog`; the verified 2.9.0 DLL is retained.

## 中文

2.9.0 修复本轮审计确认的 B1–B11，将名单排除改为 GMCM 中默认关闭的可选功能，并移除停用的历史／预览代码和 SQLite 依赖。

- **回放进度与安全（B1、B2）**：快照和条件读取保留大小写不同的合法事件、邮件、对话、配方和好感键；画廊回放不再通过爷爷蜡烛命令修改真实农场评分。
- **回放演出（B3、B4）**：嵌套事件更新只应用一次 2x/4x 倍速，异常退出后恢复正常；支持绿雨条件，普通下雨要求不会把已有绿雨改成普通雨。
- **剧情分类与查询（B5、B6）**：支持安全解析的 GSQ 好感关系、心数、日期、季节和天气条件，统一角色别名；正确处理否定和组合条件，无法判断的条件保持未知。
- **页面与来源（B7–B9）**：名单刷新保留未保存的改名和筛选草稿；失效的已选角色／地点继续显示原值；合并静态来源时保留运行时证据不完整的提示。
- **异常恢复与手柄（B10、B11）**：内置名单超限时安全回退并继续下载；手柄 A 在详情、工具和照片页仅触发一次，避免重复展开和一次归档两张照片。
- **名单开关**：`EnableAiModExclusion` 进入 GMCM 和标准 `config.json`，默认关闭。保存后生效，回放或确认期间延后刷新；缺少 GMCM 时仍可修改配置。旧 `ai-mod-exclusion.json` 不再使用，运行时删除且不迁移旧值；需要排除功能的玩家请重新开启。
- **依赖清理**：删除旧历史采集／冻结回放、预览注入及 SQLite 程序库和原生运行时；保留当前回放、存档保护、照片／封面和自定义名称。发布包约缩小 50%，12 种语言各保留 533 个有效翻译键。

### 安装与升级

需要 Stardew Valley 1.6.15、SMAPI 4.5.2 或更新兼容版本。退出游戏，完整备份旧 `Mods/StardewGallery`；将新 ZIP 解压为干净的 `Mods/StardewGallery` 文件夹，再恢复 `config.json`、`event-photos/`、`user-data/` 及已有备份／历史数据库。不要仅覆盖旧目录，以免遗留 SQLite DLL 和 `runtimes/`。本版不读取或删除玩家旧数据库。

### 验证与范围

Release 构建 0 警告／0 错误；全量逻辑和存储检查通过。真实 SMAPI 加载器接受构建 DLL，8 组隔离运行时检查共 214 项通过，覆盖来源管线、回放快照／副作用／天气／倍速、菜单草稿、GMCM 回调及手柄分派。12 语言键及占位符检查通过；新包不含 SQLite、玩家数据或游戏／第三方 MOD DLL。

自动检查未启动完整游戏。真实回放生命周期、界面缩放／长文本、物理手柄和第三方外观组合仍需游戏内确认；检查工程保留 .NET 6 支持周期提示，目录符号链接夹具在本机因 IOException 跳过。回放仍仅支持单人，原版快照无法保证恢复第三方私有状态或外部副作用。

## English

2.9.0 fixes all eleven findings (B1–B11) from the current audit, makes list exclusion an optional GMCM setting disabled by default, and removes retired history/preview code and SQLite dependencies.

- **Replay progress and safety (B1, B2):** preserve case-distinct event, mail, dialogue, recipe and friendship keys during snapshots and condition reads. Gallery replays no longer change the real farm evaluation through GrandpaCandles.
- **Replay timing and weather (B3, B4):** apply 2x/4x speed once per outer event update and clean up after exceptions. Recognize GreenRain and retain it when a condition only requires rainy weather.
- **Classification and queries (B5, B6):** share safely parsed GSQ relationship, heart, date, season and weather constraints; normalize NPC aliases and handle negated/combined conditions. Unsupported context remains unknown.
- **Menus and source evidence (B7–B9):** preserve unsaved rename/filter drafts during list refreshes, display unavailable selected NPC/location values, and keep incomplete runtime-evidence warnings alongside static source matches.
- **Recovery and controller input (B10, B11):** recover from oversized bundled lists and continue downloading. One fresh A press performs one action in detail, tool and photo menus, avoiding double toggles and double photo archival.
- **Exclusion setting:** `EnableAiModExclusion` is now in GMCM and standard `config.json`, disabled by default. Saved changes apply when replay/confirmation is idle; config remains usable without GMCM. The retired `ai-mod-exclusion.json` is deleted at runtime without importing its value. Re-enable exclusion explicitly if desired.
- **Cleanup:** remove historical collection/frozen replay, preview injection and SQLite managed/native dependencies. Keep current replay, save protection, photos/covers and custom names. The release package is about 50% smaller; all 12 locales contain 533 active keys.

### Installing and updating

Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or compatible newer versions. Close the game and back up the entire old `Mods/StardewGallery` folder. Extract the ZIP into a clean `Mods/StardewGallery` folder, then restore `config.json`, `event-photos/`, `user-data/` and any existing backups/historical databases. Simply overwriting the old folder can leave SQLite DLLs and `runtimes/` behind. This version neither reads nor deletes existing player databases.

### Validation and limits

The Release build has zero warnings/errors. Full logic/persistence suites and the actual SMAPI assembly loader pass. Eight isolated runtime modes pass 214 assertions covering source observation, replay snapshots/effects/weather/speed, menu drafts, GMCM callbacks and controller dispatch. All 12 locales pass key/placeholder checks. The ZIP excludes SQLite, player data and game/third-party mod DLLs.

These checks do not launch the full game. Live replay lifecycles, UI scaling/long text, physical controllers and cosmetic combinations still require in-game confirmation. Check projects retain the .NET 6 lifecycle warning; the local directory-symlink fixture is skipped after an IOException. Replay remains single-player only, and vanilla snapshots cannot guarantee restoration of third-party private state or external side effects.
