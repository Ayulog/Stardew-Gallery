# Stardew Gallery 2.9.0 版本说明 / Release Notes

发布日期 / Release Date: 2026-10-08

> **署名更新 / Attribution Notice**：发布包中的作者与版权署名现已统一为 `Ayulog`，内部程序文件保持已验证的 2.9.0 构建版本。 / Package author and copyright attribution are now unified under `Ayulog`, retaining the verified 2.9.0 DLL build.

## 中文

**2.9.0** 重点修复了回放状态恢复、倍速计时、天气适配、条件筛选与手柄操作中的多项细节问题，将 AI 模组排除名单整合为 GMCM 中默认关闭的可选开关，并彻底移除了早期遗留的 SQLite 依赖库，使模组体积减半。

### 更新内容

- **回放进度与存档保护**
  - 回放快照与条件读取严格保留事件、信件、对话、配方及好感键名的大小写差异，避免在恢复状态时丢失区分大小写的标记或因同名键冲突报错。
  - 仅在画廊触发的回放中拦截 `GrandpaCandles` 状态修改，防止回放爷爷评估剧情时改写真实农场的评价蜡烛数，同时不影响正常游戏内的评估流程。
- **回放倍速与天气表现**
  - 修复嵌套事件更新导致 2x / 4x 倍速被重复叠加的问题，并在异常退出时自动重置倍速状态。
  - 回放环境现已支持**绿雨（Green Rain）**；当事件仅要求普通下雨（`rainy`）而当前恰好处于绿雨天气时，会保留绿雨氛围而不再强行降级为普通雨天。
- **剧情归类与条件筛选**
  - 支持解析常见的 Game State Query (GSQ) 好感关系、心数、日期、季节与天气条件（包含否定与组合条件），并统一角色别名的心数筛选。无法静态判定的条件继续如实显示为“未知”。
- **界面交互与手柄修复**
  - 修复在事件详情、工具栏与剧照管理页按手柄 `A` 键时触发两次点击的问题，避免一次按键展开两次面板或同时移除两张剧照。
  - 目录刷新时自动保留尚未保存的事件改名与筛选草稿；已选中的角色或地点筛选项即使当前无匹配结果也保持原名称显示，不再误显示为“任意”。
  - 静态匹配到模组原始来源后，若同时开启了运行时修改追踪，仍会完整显示运行时的证据状态，且不再截断翻译后的长文本。
  - 当内置排除名单文件大小异常超限时安全回退，不残留旧会话规则且不影响后续异步拉取。
- **AI 模组排除开关整合至 GMCM**
  - `EnableAiModExclusion` 现已并入 GMCM 与标准 `config.json`，**默认关闭（`false`）**。在 GMCM 中保存后即可生效（若正在回放或处于确认弹窗中，则待结束后刷新）；未安装 GMCM 时也可直接修改 `config.json`。
  - 旧版独立的 `ai-mod-exclusion.json` 已废弃并在运行时自动清理，不会自动迁移旧开关值；如需使用排除功能，请在更新后前往 GMCM 重新开启。
- **移除遗留依赖与精简体积**
  - 彻底移除早期已停用的历史采集/冻结回放、预览注入代码及 SQLite 托管与原生运行时库，发布包体积缩减约 **50%**。
  - 现有的当前状态回放、存档保护、剧照封面与自定义名称功能均完整保留，12 种语言翻译（各 533 键）全部对齐。

### 安装与升级指南

- **环境要求**：Stardew Valley 1.6.15+、SMAPI 4.5.2+。
- **干净升级步骤**：
  1. 退出游戏，完整备份旧的 `Mods/StardewGallery` 文件夹。
  2. 删除旧文件夹并将新版压缩包解压为干净的 `Mods/StardewGallery`（**请勿直接覆盖旧目录**，以免遗留旧版的 SQLite DLL 与 `runtimes/` 文件夹）。
  3. 将备份中的 `config.json`、`event-photos/`（剧照与封面）和 `user-data/`（自定义事件名称）拷回新目录即可。

### 验证范围与已知说明

- Release 构建（0 警告 / 0 错误）、全量逻辑与存储检查、真实 SMAPI 程序集加载器检查，以及涵盖来源扫描、回放快照/副作用/天气/倍速、菜单草稿、GMCM 回调与手柄分派的 8 组共 214 项隔离运行时断言全部通过；发布包已核验不含 SQLite 库与个人数据。
- 回放功能仅支持单人模式；原版回放快照无法保证回滚第三方模组自定义命令对外部文件或私有状态的修改。

---

## English

**Version 2.9.0** fixes a range of issues across replay state restoration, playback speed timing, weather handling, condition filters, and controller input. It also moves the AI mod exclusion setting into GMCM (disabled by default) and removes retired SQLite dependencies, cutting the release archive size by roughly 50%.

### What's New

- **Replay State & Save Protection**
  - Replay snapshots and condition checks now preserve case-distinct keys for events, mail, dialogue, recipes, and friendships, preventing lost markers or key collisions when restoring state.
  - Gallery replays now suppress `GrandpaCandles` changes so replaying Grandpa's evaluation never alters your real farm score, while leaving normal gameplay events untouched.
- **Playback Speed & Weather**
  - Fixed 2x/4x replay speed compounding across nested event updates, with automatic cleanup on exceptions.
  - Added **Green Rain** support for replay environments, and preserved active Green Rain when an event only requires standard rainy weather.
- **Story Classification & Filters**
  - Shared safely parsed Game State Query (GSQ) relationship, heart, date, season, and weather constraints across story classification and search filters (including negated and combined rules), and normalized character aliases for heart filters.
- **Menus, Sources & Controller Input**
  - Fixed controller `A` button presses firing twice in detail, tool, and photo menus, preventing double toggles and accidental double photo archiving.
  - Unsaved rename and filter drafts are now preserved across catalog refreshes, and unavailable selected character/location filters remain visible instead of resetting to "Any".
  - Runtime modification evidence status is still shown alongside matched static file sources without truncating localized text.
  - Added safe fallback handling if a bundled exclusion file exceeds size limits, without retaining stale session rules or blocking updates.
- **GMCM Exclusion Setting**
  - `EnableAiModExclusion` is now managed through GMCM and `config.json`, **disabled by default (`false`)**. Changes apply once any active replay or confirmation finishes.
  - The retired `ai-mod-exclusion.json` file is removed automatically at runtime without importing old values; please re-enable the option in GMCM if you wish to use it.
- **Dependency Cleanup**
  - Removed retired historical collection, preview injection code, and SQLite managed/native dependencies, reducing the release package size by about **50%** while keeping current replay, save protection, photos, covers, and custom names intact across all 12 supported languages (533 keys each).

### Installation & Updating

- **Requirements**: Stardew Valley 1.6.15+ and SMAPI 4.5.2+.
- **Clean Update Steps**:
  1. Close the game and back up your existing `Mods/StardewGallery` folder.
  2. Extract the new ZIP into a **clean** `Mods/StardewGallery` folder (avoid overwriting the old folder directly so retired SQLite DLLs and `runtimes/` folders are not left behind).
  3. Copy your `config.json`, `event-photos/`, and `user-data/` back into the new folder.

### Validation & Notes

- Verified with a clean Release build (0 warnings / 0 errors), full logic and persistence suites, the SMAPI assembly loader, and 214 isolated runtime assertions across 8 test modes (covering source scanning, replay snapshots/effects/weather/speed, menu drafts, GMCM callbacks, and controller dispatch).
- Event replay remains single-player only, and native replay snapshots cannot roll back third-party private state or external file side effects.
