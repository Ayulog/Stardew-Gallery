# Stardew Gallery 2.0.6 — Stop Historical Collection

## 目标与边界

本版从真实运行链停用已经取消的 historical replay/collection：不再安装自然事件 observer，不再加载、导入或写入历史记录，也不再打开历史 SQLite 会话。已有 SQLite 与 legacy 历史数据原样保留。

不删除历史源码或数据，不迁移 schema，不改当前画廊 UI、解锁、current replay、回放保护、速度控制或 2.0.5 当前状态逻辑。

## 操作流程与实现

- `ModEntry` 不再创建 `WatchedEventHistory`、`HistoricalReplayAssets`、`GalleryDatabase` 或 `HistoryRepository`，并移除其 SaveLoaded、UpdateTicked、ReturnedToTitle 和 AssetRequested 接线。
- `ExecutionTraceObserver` 不再安装；`ReplaySaveGuard` 与 `ReplaySpeedPatches` 继续安装。
- `GalleryMenu` 删除未使用的历史版本委托；界面与导航行为不变。
- `ReplayCoordinator` 删除历史资源依赖和清理，只保留 current replay 所需状态、环境、备份和故障恢复。

## 模块职责、配置与存储

- `gallery-state` 继续只保存 Unlock All。
- `History/*`、`Persistence/*`、`WatchedEventHistory.cs` 及历史 domain/codecs 暂留为兼容源码，不再由生产入口调用。
- `<SMAPI DataPath>/StardewGallery/gallery.sqlite3` 与 `watched-event-versions` 不读取、不导入、不写入、不删除。
- 无新配置、玩家文本、UI 原型或坐标变化。

## 兼容风险

- 升级后不会再记录任何新自然事件历史 occurrence、context 或 legacy snapshot；这是本版目标。
- 历史依赖仍随构建保留，后续源码清理另行决定。
- 多人模式未实测；current replay 仍按现有规则禁用多人。

## 验收

- 静态检查生产入口、GalleryMenu 与 ReplayCoordinator 不再引用历史 runtime wiring，同时确认两项 replay Harmony 仍保留。
- 运行 Checks、PersistenceChecks、Release build 与基线 diff check。
- 实机 H26-1～H26-6：画廊、自然事件无采集、skip/中断、存档/标题生命周期、current replay 和 Unlock All；均待实测。
