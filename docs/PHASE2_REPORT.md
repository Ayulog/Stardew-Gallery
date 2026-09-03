# Stardew Gallery Phase 2：Resolved Event Index 实施报告

日期：2026-09-03

## 1. 实施基线与 commit

- 工作分支：`phase2/resolved-event-index`
- Phase 1 基线：`47c8fa55ae3f49a8eb3e5ce058e26088be24e0f0`
- Phase 2 analysis commit：`52806414be38cfa0b0dec5d651c2b5c98d3485fe`
- 最终 implementation commit：`1ae94064ab67582fcd5c54e18328b68f59f059b0`
- Commit 标题：`refactor: introduce resolved event index`

本报告使用独立 documentation commit 提交，因此上面的 implementation commit 精确对应已构建和检查的业务代码、Checks 与 `PHASE2_TASK.md`。

---

## 2. 新增与修改文件

### 2.1 新增

- `Catalog/EventAssetSource.cs`
- `Catalog/EventAssetCatalog.cs`
- `Catalog/ResolvedEventReader.cs`
- `Catalog/ResolvedEventIndex.cs`
- `Catalog/GalleryCatalogBuilder.cs`
- `docs/PHASE2_TASK.md`
- `docs/PHASE2_REPORT.md`

### 2.2 修改

- `GalleryCatalogCache.cs`
- `EventOwnership.cs`
- `Checks/Program.cs`
- `Checks/StardewGallery.Checks.csproj`

除上述文件外，没有为 Phase 2 修改其他业务、UI、Replay、History、配置或发布文件。

---

## 3. 实际层次结构

```text
EventAssetCatalog
    -> ResolvedEventReader
    -> ResolvedEventIndex
    -> GalleryCatalogBuilder
    -> GalleryCatalogCache
    -> existing Gallery UI / Replay / Watched History
```

### 3.1 EventAssetCatalog

- 唯一直接调用 `Utility.ForEachLocation` 与 `TryGetLocationEvents` 的新层。
- 参数保持 `includeInteriors: true, includeGenerated: false`。
- 继续读取游戏最终 content pipeline event dictionary。
- 为每个 source 保留 `AssetName`、`NameOrUniqueName` launch location、`Name` fragment root 与 raw dictionary order。
- 使用同步 `VisitCurrent` visitor；Reader 在进入下一个 runtime location 前完成当前 source 的 validation、fragment 与 translation loads。
- Source-bound precondition callback 固定调用 `checkEventPrecondition(..., check_seen: false)`。

### 3.2 ResolvedEventReader

- 负责 validity filtering、EventId extraction、placeholder filtering、typed identity、fragment collection、root hashes 与 `ResolvedEventCandidate` construction。
- 复用现有 `EventKey`、`EventFragmentCollector` 与 `EventHashes`。
- Raw key、script、launch location 和 fragment root 语义保持不变。
- Missing fragment 不删除 event，继续记录在 `Fragments.MissingKeys`。
- Reader 不提前运行 candidate precondition。

### 3.3 ResolvedEventIndex

- `Build(...)` 是 BCL-only grouping/dedup/selection core。
- Index 以 typed `EventIdentity` lookup。
- 每个 group 保存 selected `Current` 与 ordered read-only `Candidates`。
- `ReadCurrent(...)` 只执行 `VisitCurrent -> reader.Read -> Build`。
- 完成后的 Index 只保存 `ResolvedEvent` 与 lookup/group collections，不保存 source、runtime `GameLocation` 或 delegates。

### 3.4 GalleryCatalogBuilder

- 只消费 `ResolvedEventIndex.CurrentEvents`。
- 保存原 friendship、prerequisite、actor、spouse 与 dialogue evidence parsing。
- 调用未改变算法的 `OwnershipResolver`。
- Ownership 完成后才构造 `GalleryEvent` compatibility adapter。
- 负责 included/excluded lists 与 Gallery character filtering。

### 3.5 GalleryCatalogCache

- 保留原 `Get()` / `Invalidate()`、character scan、summary logging 与 diagnostics 职责。
- 不再直接枚举或解析 raw event definitions，也不执行 candidate selection。
- 私有原子缓存 `CacheSnapshot(ResolvedEventIndex, GalleryCatalog)`。
- `Get()` 仍只向现有调用者返回 `GalleryCatalog`。
- 现有 `ModEntry` invalidation handlers 和 active menu snapshot behavior 未改变。

---

## 4. ResolvedEventIndex API

实现的 read-only API：

```csharp
IReadOnlyList<ResolvedEventGroup> Groups
IReadOnlyList<ResolvedEvent> CurrentEvents

bool TryGetGroup(
    EventIdentity identity,
    out ResolvedEventGroup group
)

bool TryGetCurrent(
    EventIdentity identity,
    out ResolvedEvent resolved
)

IReadOnlyList<ResolvedEvent> GetCandidates(
    EventIdentity identity
)

static bool MatchesCurrentState(
    string? preconditionResult
)

static ResolvedEventIndex ReadCurrent(
    IEventAssetSourceCatalog assets,
    ResolvedEventReader reader
)

static ResolvedEventIndex Build(
    IReadOnlyList<ResolvedEventCandidate> candidates
)
```

Lookup 限制：

- 没有 string/`StorageKey` lookup。
- 没有 LocationName、raw key、hash 或 historical lookup。
- Missing identity 的 Try APIs 返回 false，`GetCandidates` 返回 empty。
- 没有 mutation、refresh 或 reselect API。

---

## 5. Selection 与 dedup compatibility

实现保持：

- Identity first-discovery order。
- Candidate order 与 CurrentEvents group order。
- Exact duplicate = 完全相同的 `RawEventKey + ResolvedScript`。
- Exact duplicate 跨 location 时 first source wins。
- Same raw key + different script 不 dedup。
- Different raw key + same script 不 dedup。
- Root hashes 不参与 identity 或 dedup。
- Null、empty 与 exact `"-1"` precondition result 为 false。
- 其他 nonempty result 为 true，包括 `"0"` 与 whitespace。
- Evaluator exception 视为 false 并继续后续 candidate。
- First applicable candidate wins。
- Multiple applicable 仍选择第一个。
- All false 时 fallback candidate 0。
- Deduplicated candidate 的 evaluator 不重复执行。
- Selected candidate 自身的 AssetName casing 与 LocationName 原样进入 Current 和 Gallery adapter。

Validation、parsing 与 fragment exceptions 没有被吞掉，仍会中止整次 build；cache 只在完整 Gallery catalog 构建及 summary log 完成后赋值。

---

## 6. Typed ownership migration

- `EventEvidence.Identity` 已从 string 改为 `EventIdentity`。
- `OwnershipResolver.Resolve` 已返回 `IReadOnlyDictionary<EventIdentity, EventOwnership>`。
- Internal result dictionary 使用 typed equality，不再依赖 `StorageKey` casing。
- Direct、Inherited、Inferred、Excluded、dominant speaker、tie 与 dependency-cycle algorithms 未改变。
- Prerequisite lookup 仍按 case-sensitive EventId 分组。
- 不同 Asset identities 共享同一 EventId 时仍是 ambiguous predecessor，不会错误继承。
- Ownership 只分析 selected CurrentEvents；未选 candidates 不进入 evidence parsing。

---

## 7. Diagnostics compatibility

`catalog-latest.json` 继续由 `GalleryCatalogCache` 投影，保持：

- `Timestamp`；
- `Summary` 及原 count fields；
- `Conflicts` 的 `Location`、`EventId`、`SelectedKey`、`CandidateKeys`；
- `MissingFragments` 的 `LocationName`、`EventId`、`MissingKeys`；
- `Catalog` serialized shape。

Index、runtime source、delegates 与 `GameLocation` 不进入 diagnostics。

---

## 8. Build 与 Checks 结果

以下命令在 implementation commit 对应的同一代码树上运行。

### 8.1 Release build

命令：

```powershell
dotnet build -c Release
```

结果：成功。

- Warnings：0
- Errors：0
- `StardewGallery.dll` 与现有 1.0.0 build ZIP 正常生成。

### 8.2 Checks

命令：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
```

结果：成功，最终输出：

```text
Stardew Gallery checks passed.
```

Checks 项目仍报告既有 `NETSDK1138`，原因是目标框架为已停止支持的 `net6.0`。本阶段没有修改 target framework；没有其他 warning 或 error。

新增 characterization coverage 包括：

- source visitation、Reader/fragment load 与 precondition call order；
- launch location / fragment root separation；
- Reader filtering、raw key/script/hash preservation 与 missing fragments；
- typed identity slash/casing、EventId casing、different assets 与 same-identity locations；
- exact dedup first-wins 及 evaluator suppression；
- first/multiple/all-false/exception selection；
- group/candidate/current ordering 与 missing lookup；
- selected candidate casing/location；
- typed ownership normalization、ambiguous same-EventId predecessors 与 case-sensitive prerequisites；
- selected-only source -> Reader -> Index -> GalleryCatalogBuilder end-to-end fixture；
- spouse/fragment dialogue evidence 与 Gallery character filtering；
- 所有既有 Phase 1 Checks。

### 8.3 Diff check

命令：

```powershell
git diff --check
```

结果：成功，无 whitespace errors。

---

## 9. 未修改的重要模块

以下文件未修改：

- `ReplayCoordinator.cs`
- `ReplaySnapshot.cs`
- `ReplaySaveGuard.cs`
- `ReplayLifecycleRules.cs`
- `ReplaySpeedPatches.cs`
- `WatchedEventHistory.cs`
- `Domain/HistoricalPlaybackBundle.cs`
- `Domain/EventIdentity.cs`
- `Domain/ResolvedEvent.cs`
- `Domain/EventHashes.cs`
- `EventFragments.cs`
- `EventKey.cs`
- `GalleryCatalog.cs`
- `GalleryMenu.cs`
- `GalleryCharacterMenu.cs`
- `ModEntry.cs`
- `GalleryDiagnostics.cs`
- UI assets、i18n、manifest、version、config 与 release materials

没有实现 ConditionIR、SQLite、Preview/StateInjector、CP variant discovery、new refresh architecture、unified EventLauncher 或 Phase 3 内容。

---

## 10. 尚需人工 parity 验证

自动 Checks 不加载真实游戏 runtime。以下仍需在游戏内与 Phase 1 基线 A/B：

1. Current event count、included/excluded count 一致。
2. NPC grouping 与每个 NPC event count 一致。
3. Event detail 和 condition display 一致。
4. Current replay 正常，并在结束后正常恢复。
5. Historical replay、`fork`、`switchEvent`、translations 与 cross-location fragments 正常。
6. Watched version capture、save、reload 与历史版本读取正常。
7. `Data/Characters`、`Data/Events/*` 与 locale invalidation 后重新打开 Gallery 的 timing/结果一致。
8. 两个 Gallery entry points 与 active menu/replay-return snapshot behavior 一致。
9. Multiplayer 继续保持现有禁止 replay 行为。

Phase 1 报告中的节庆布置 + external dialogue patch/UI mode 异常仍按外部运行环境异常处理。Phase 2 parity 结论应使用可重复的干净运行条件。

---

## 11. 非阻塞说明

- `EventAssetCatalog` 依赖真实 Stardew runtime，不能加入当前 BCL-only Checks；其 `ForEachLocation` flags、`TryGetLocationEvents` 和 `check_seen: false` wiring 已通过源码审查，仍需游戏内 smoke test。
- `GalleryCatalogCache` diagnostics 与 private composite assignment 通过完整 diff/code review 验证，没有引入 game-runtime test harness。
- `EventAssetSource.Definitions` 在每个 source visitor 内按原 dictionary order materialize，Reader 随即同步消费；没有跨 source 延迟 fragment/translation loads。
- 本阶段没有实际执行游戏内 parity，因此不把 build/Checks 通过表述为实机回归通过。

---

## 12. 结论

Phase 2 implementation 已完成源码层分层和自动验收。Resolved current content、candidate indexing 与 Gallery ownership/catalog projection 已形成独立边界，现有 UI、Replay、History 与持久化路径保持不变。完成第 10 节实机 A/B 前，不声明完整用户流程 parity 已通过。
