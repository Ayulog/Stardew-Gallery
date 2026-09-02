# Stardew Gallery 1.0.0 Phase 1 开发文档

日期：2026-09-02

## 1. 功能目标

在现有 Stardew Gallery 1.0.0 上引入稳定的事件领域模型，并把当前目录与自然观看历史记录迁移到正式事件身份：

```text
EventIdentity = normalized AssetName + case-sensitive EventId
```

本阶段保持零用户可见行为变化：画廊扫描、NPC 分组、条件显示、当前版本回放、历史快照回放、快照恢复和保存保护均沿用现有流程。

## 2. 不做的内容

- 不实现 Phase 2 的 `EventAssetCatalog`、`ResolvedEventReader`、`ResolvedEventIndex` 或资源刷新架构。
- 不实现 ConditionIR、PreviewState、StateInjector、SQLite、ObservedVariant、Route Planner 或 Solver。
- 不拆分或重构 `ReplayCoordinator`，不改变 `ReplaySnapshot`、`ReplaySaveGuard`、`ReplayLifecycleRules`、`ReplaySpeedPatches` 和 `HistoricalReplayAssets` 的逻辑。
- 不重写 `EventFragments` 或 `EventOwnership`。
- 不改变 UI 布局、显示文本、操作方式、配置项、manifest、版本号或发布材料。

## 3. 用户操作流程

用户操作流程不变：

1. 从收藏菜单标签或快捷键打开画廊。
2. 选择角色并浏览当前生效事件。
3. 选择当前版本或自然观看历史版本回放。
4. 回放结束后恢复状态并回到原角色页。

本阶段新增类型不直接暴露给玩家。

## 4. 实现方式

### 4.1 领域模型

- 新增 `Domain/EventIdentity.cs`：规范化 AssetName 路径分隔符并裁剪两部分；AssetName 使用 `OrdinalIgnoreCase`，EventId 使用 `Ordinal`。
- 新增 `Domain/EventHashes.cs`：为 resolved root script 和 raw key + root script 生成完整 64 位十六进制 SHA-256。
- 新增 `Domain/ResolvedEvent.cs`：保存当前内容管线解析后的 root 定义、片段和 root hashes。
- 新增 `Domain/HistoricalPlaybackBundle.cs`：把旧 DTO 的 `Fingerprint` 映射为领域层 `PlaybackHash`，不改变持久化字段名。

### 4.2 兼容适配

- `GalleryEvent` 改为包装 `ResolvedEvent` 与 `EventOwnership`，保留现有 `Identity`、`LocationName`、`AssetName`、`EventId`、`EventKey`、`Script`、`Fragments` 属性。
- `Identity` 字符串仅作为 Ownership 和 UI 临时状态 key，值来自 `Resolved.Identity.StorageKey`。
- `GalleryCatalogCache` 只改事件构造和候选 key，不改扫描、条件选择、片段收集或归属流程。

### 4.3 历史数据

- `WatchedEventSnapshot` 的 11 个构造字段及 JSON 名称保持不变。
- 新增 `[JsonIgnore]` 计算属性 `Identity` 与 `Playback`。
- `WatchedEventHistory.entries` 改用 `EventIdentity` key；保存时仍仅序列化 `entries.Values`。
- `Add` 按 `snapshot.AssetName + snapshot.EventId` 分组，修正当前按 LocationName 分组的旧语义。
- UI 历史查询 delegate 接收 `GalleryEvent`，避免 UI 解析 identity 字符串。

## 5. 参考依据

- 实施任务书：`C:/Users/sjt38/Downloads/PHASE1_TASK.md.md`。
- 当前 Stardew Gallery 1.0.0 全部受版本控制源码、配置、文档和 UI 资产。
- 现有原版扫描研究：`drafts/星露谷画廊/research/vanilla-scan-foundation.md`。
- 现有自然观看快照记录：`memory/星露谷画廊/0.16.0-自然观看版本快照-20260902.md`。
- 现有实现继续依赖 SMAPI 公开内容 API 和原版事件 API；本阶段不新增第三方代码、素材或依赖。

## 6. 项目架构与职责

```text
Data/Events final content
  -> GalleryCatalogCache
  -> ResolvedEvent
  -> GalleryEvent compatibility adapter
  -> existing Ownership / UI / Replay

Natural Event
  -> WatchedEventSnapshot persisted DTO
     -> EventIdentity computed view
     -> HistoricalPlaybackBundle computed view
```

- Domain：事件身份、resolved root definition、root hash、历史播放 bundle 语义。
- Catalog：继续负责当前地点和事件扫描、variant 选择、片段收集及 UI adapter 创建。
- History：继续负责自然事件捕获、压缩存档、版本去重和历史资产注入。
- UI / Replay：只接受必要的类型适配，不改变业务流程。

## 7. 数据保存与配置

- Save key 仍为 `watched-event-versions`。
- GZip + Base64 + JSON 列表格式不变。
- 持久化字段仍为 `LocationName`、`AssetName`、`EventId`、`EventKey`、`RootScript`、`EventAssets`、`Translations`、`Locale`、`Fingerprint`、`FirstWatchedAt`、`LastWatchedAt`。
- Dictionary 的新 typed key 不写入 JSON。
- 不新增或修改 config 字段。

## 8. UI 原型与交互

无 UI 改动。继续使用当前 `1672 x 941` 逻辑画布、角色页四行事件列表、历史版本按钮和当前回放按钮。按钮位置、尺寸、焦点导航、文本和版本选择 state 均保持不变。

## 9. 兼容性与潜在冲突

- Windows、Linux、macOS：AssetName 统一使用 `/` 存储，输入中的 `\` 会规范化，不依赖平台路径 API。
- Content Patcher 与其他内容 Mod：仍读取内容管线最终的 `TryGetLocationEvents` 结果，不增加来源扫描。
- 旧 save data：JSON schema 不变；旧 AssetName 中的反斜杠在计算 identity 时规范化。
- 特殊地点：`LocationName` 仍用于显示和启动事件，但不再参与逻辑 identity。同地点若由不同 AssetName 提供同 EventId，将正确分开。
- 现有术语冲突：`drafts/星露谷画廊/CONTEXT.md` 仍定义“地点 + EventId”，与 Phase 1 正式语义冲突。实施完成后只更新该术语定义，不扩展其他架构内容。
- 历史数据若 `AssetName` 缺失或为空，类型仍可构造空 AssetName identity；本阶段不增加迁移猜测或兜底，以免错误合并记录。

## 10. 自动检查方案

- 先运行修改前基线 `dotnet build -c Release` 和 Checks。
- EventIdentity：斜杠规范化、AssetName 大小写不敏感、EventId 大小写敏感、不同 Asset 同 ID 不相等。
- ResolvedEvent：相同 LocationName、不同 AssetName、相同 EventId 的 identity 不相等。
- EventHashes：相同 root script hash 相同，不同 root script hash 不同，输出 64 字符；相同 script 不同 raw key 的 root definition hash 不同。
- 旧 JSON：反序列化旧字段，验证全部原字段、规范化 identity 和 historical playback 映射；重新序列化时不出现 `Identity` / `Playback`。
- 保留现有 EventKey、snapshot fingerprint、Ownership、Fragments、Layout、UI rules、Replay lifecycle checks。
- 完成后运行整个主项目 Release build 与全部 Checks。

## 11. 单人模式测试

能自动检查的模型和序列化行为全部加入 Checks。以下需要玩家在游戏内回归：

1. 画廊正常打开，事件数和 NPC 分组与 1.0.0 基线一致。
2. 事件详情和条件文本不变。
3. 当前版本与历史版本回放正常。
4. 历史 `fork`、`switchEvent`、translation 和跨地点片段正常。
5. 旧 `watched-event-versions` 正常读取，新自然事件仍生成观看版本。
6. 回放后 snapshot restore 与 SaveGuard 行为不变。

本环境无法代替玩家完成实际游戏内流程，不把 build/checks 通过表述为实机回归通过。

## 12. 多人模式

现有代码明确禁止多人回放。本阶段不改变多人逻辑，也不声称多人模式已经测试或兼容。Catalog 和 history 的主机/客户端行为仍需未来专门验证。

## 13. 版本与发布材料

- 开发基准：Stardew Valley 1.6.15、SMAPI 4.5.2、Stardew Gallery 1.0.0。
- Phase 1 是内部零行为迁移，本轮不自行提升版本号。
- 不生成新 release zip，不修改 CHANGELOG、README、Nexus 或 GitHub 发布材料。
- 若后续要发布，版本号和发布材料需单独确认。

## 14. 验收边界

允许修改：4 个 Domain 文件、`GalleryCatalog.cs`、`GalleryCatalogCache.cs`、`WatchedEventHistory.cs`、`GalleryMenu.cs`、`GalleryCharacterMenu.cs`、Checks 项目，以及冲突的术语文档。

除非发生编译兼容问题，不修改 Replay 文件；不修改 `EventFragments.cs`；不开始 Phase 2。
