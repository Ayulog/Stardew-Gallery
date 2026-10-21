# Stardew Gallery Phase 2：Resolved Event Index 实施任务书

日期：2026-09-03

## 1. 基线与目标

- 工作分支：`phase2/resolved-event-index`
- 实施基线：Phase 1 commit `47c8fa55ae3f49a8eb3e5ce058e26088be24e0f0` 及其后的 Phase 2 analysis commit。
- 设计依据：`docs/PHASE2_ANALYSIS.md` 与本任务书。
- 本任务书是 Phase 2 实施范围与验收标准的权威约束；已确认的决策覆盖 analysis 文档中的待决建议。

Phase 2 只完成当前 event discovery/catalog pipeline 的分层：

```text
EventAssetCatalog
    -> ResolvedEventReader
    -> ResolvedEventIndex
    -> GalleryCatalogBuilder
    -> GalleryCatalogCache
```

目标：

- 把最终 content pipeline event definitions 的读取与 Gallery UI catalog/ownership 组装分开。
- 建立 typed、read-only、可测试的 `ResolvedEventIndex`。
- 保留当前已经发现的 ordered candidates，为后续阶段提供稳定边界。
- 保持所有用户可见行为、Replay、History 与 cache invalidation timing 不变。

Phase 2 不实现新功能，不扩大当前 event universe。

---

## 2. 已确认领域语义

### 2.1 Event identity

```text
EventIdentity = normalized AssetName + case-sensitive EventId
```

- AssetName 由现有 `EventIdentity` 规范化斜杠并使用 `OrdinalIgnoreCase` equality。
- EventId trim 后使用 `Ordinal` equality。
- Index 与 ownership 全部使用 typed `EventIdentity`。
- `StorageKey` 只保留给现有 UI 临时 state，不得作为 Index 或 ownership key。
- LocationName、raw event key、root hashes、fragments 与 ownership 均不属于 identity。

### 2.2 Candidates

- Candidates 仅指当前最终 content pipeline 中通过现有 runtime location scan 发现的候选 definitions。
- Candidates 不是 ObservedVariant、HistoricalVariant，也不是 Content Patcher 的全部潜在 variants。
- Historical snapshots 不进入 current Index。
- 同一 identity 的 candidates 保持发现顺序。
- Exact duplicate 只定义为完全相同的 `RawEventKey + ResolvedScript`。
- Exact duplicate 跨 location 时 first source wins。
- 相同 raw key + 不同 script 不 dedup。
- 不同 raw key + 相同 script 不 dedup。
- Root hashes 不参与 identity、dedup 或 lookup。

### 2.3 Location roles

- `LaunchLocationName = GameLocation.NameOrUniqueName`。
- `FragmentRootLocationName = GameLocation.Name`。
- Candidate precondition 必须在发现它的 source `GameLocation` 上执行。
- Runtime `GameLocation` 与 delegates 只允许存在于一次 Index build 生命周期内。
- 完成后的 Index 不得保留 `GameLocation`、runtime delegates 或 source objects。
- Index 不提供 LocationName lookup，也不得按 location 构造 identity。

---

## 3. 目标层次职责

### 3.1 EventAssetCatalog

负责：

- `Utility.ForEachLocation(..., includeInteriors: true, includeGenerated: false)`；
- 对每个 location 调用 `TryGetLocationEvents`；
- 读取游戏最终 content pipeline 结果；
- 保存 source 与 raw dictionary entry 顺序；
- 提供 launch location、fragment root、cross-location loader 与 source-bound precondition callback。

约束：

- 使用同步 visitor；Reader 必须在当前 source visitor 返回前完成。
- 不能先收集全部 sources 再批量读取。
- 不做 validity filtering、ID parsing、fragment collection、hash、dedup 或 selection。
- 不缓存，不订阅新 refresh events。

### 3.2 ResolvedEventReader

负责：

- `GameLocation.IsValidLocationEvent` 等价 validation；
- `EventKey.TryGetId`；
- placeholder filtering；
- typed `EventIdentity` construction；
- 复用 `EventFragmentCollector.Collect`；
- 计算 `RootDefinitionHash` 与 `RootScriptHash`；
- 生成 ordered transient candidates。

约束：

- 保留 raw key 与 script 原值。
- Missing fragments 继续保留 event，只记录 `MissingKeys`。
- Reader 不提前执行 precondition callback。
- Reader 不知道 characters、ownership、Gallery UI、History 或 Replay。

### 3.3 ResolvedEventIndex

负责：

- 以 typed `EventIdentity` grouping；
- exact duplicate first-wins dedup；
- identity/group/candidate order；
- current candidate selection；
- typed identity lookup；
- 保存每个 identity 的 `Current` 与 ordered read-only `Candidates`。

`ResolvedEventIndex.Build(...)` 必须是纯 grouping/dedup/selection core。

如果实现 `ResolvedEventIndex.ReadCurrent(...)`，它只能执行：

```text
VisitCurrent
    -> reader.Read
    -> Build
```

Index 不得实现 `Utility.ForEachLocation`、`TryGetLocationEvents` 或其他 runtime discovery。

完成后的 Index：

- 不提供 mutation/refresh/reselect API；
- 不保留 transient callbacks；
- 不提供 string、location、raw key、hash 或 history lookup；
- 对 missing identity 的 candidate lookup 返回 empty，Try APIs 返回 false。

### 3.4 GalleryCatalogBuilder

负责：

- 只从 Index 的 selected `CurrentEvents` 读取 ownership evidence；
- 保持现有 friendship/prerequisite/actor/dialogue evidence parsing；
- 调用现有 `OwnershipResolver`；
- ownership 完成后构造 `GalleryEvent` compatibility adapters；
- 生成 included/excluded events；
- 只保留至少拥有一个 included event 的 Gallery characters。

Builder 应通过 BCL-only contracts 与 injected parse/spouse delegates 可被 Checks source-link。

### 3.5 GalleryCatalogCache

保留：

- `Get()` / `Invalidate()` facade；
- `ScanCharacters()` 与 social asset checks；
- pipeline composition；
- summary logging；
- conflict/missing-fragment/catalog diagnostics；
- 当前 coarse lazy invalidation timing。

Cache 私有原子持有：

```text
(ResolvedEventIndex, GalleryCatalog)
```

- `Get()` 仍只返回 `GalleryCatalog`。
- Index 不暴露给 UI、Replay 或 `ModEntry`。
- Invalidate 同时清空两个 views。
- Catalog 构建与 cache assignment timing 保持当前行为。

---

## 4. Selection compatibility contract

每个 deduplicated identity group 必须：

1. 按 candidate discovery order evaluation。
2. Null result 为 false。
3. Empty result 为 false。
4. Exact `"-1"` 为 false。
5. 其他所有 nonempty result 为 true，包括 `"0"` 与 whitespace。
6. Evaluator exception 视为 false 并继续后续 candidate。
7. 选择第一个 applicable candidate。
8. 多个 applicable 时仍选择第一个。
9. 全部不匹配时 fallback candidate 0。
10. Exact duplicate 被 dedup 后不得重复执行 evaluator。

除 precondition evaluator exception 外，validation、parsing、fragment loading/collection 的异常继续使整次 catalog build 失败，并且不写入新 cache snapshot。

---

## 5. Ownership compatibility contract

- `EventEvidence.Identity` 改为 `EventIdentity`。
- `OwnershipResolver.Resolve` result dictionary key 改为 `EventIdentity`。
- Direct、Inherited、Inferred、Excluded algorithm 不变。
- Multi-direct dominant speaker behavior 不变。
- Prerequisite 仍按 case-sensitive EventId 匹配。
- 只有一个唯一 predecessor 时才能继承。
- 不同 Asset identities 共享同一 EventId 时仍构成 ambiguous predecessor，不得错误继承。
- Non-selected candidates 不得进入 evidence parsing 或 ownership resolver。
- Typed identity equality 不得被 `StorageKey` casing 分裂。

---

## 6. 用户可见与持久化兼容

必须保持：

- raw event dictionary enumeration order；
- identity first-discovery order；
- candidate/group/current order；
- current event count；
- included/excluded ownership count；
- NPC grouping 与每个 NPC event count；
- condition display 使用 selected current raw key；
- `GalleryEvent` compatibility property surface；
- current replay 继续使用现有 `LocationName + EventId` live launch path；
- historical replay 行为；
- watched history schema、typed identity、capture、dedup、save、load 与 replay 行为；
- current cache invalidation handlers/timing；
- active menus/replay closures 保留原 Gallery snapshot 的行为。

`catalog-latest.json` 必须保持当前：

- summary structure；
- conflict structure；
- missing-fragment structure；
- serialized catalog structure。

不得把 Index、source delegates 或 runtime objects 直接序列化到 diagnostics。

---

## 7. 允许文件范围

建议新增：

- `Catalog/EventAssetSource.cs`
- `Catalog/EventAssetCatalog.cs`
- `Catalog/ResolvedEventReader.cs`
- `Catalog/ResolvedEventIndex.cs`
- `Catalog/GalleryCatalogBuilder.cs`

主要允许修改：

- `GalleryCatalogCache.cs`
- `EventOwnership.cs`
- `Checks/Program.cs`
- `Checks/StardewGallery.Checks.csproj`
- `docs/PHASE2_TASK.md`
- `docs/PHASE2_REPORT.md`

若编译兼容要求对其他文件作极小 adapter 调整，必须在最终报告中说明；不得借此重构受保护模块。

---

## 8. Automated Checks 最低要求

Checks 必须覆盖：

- source visitation 与 Reader/fragment load call order；
- launch location 与 fragment root separation；
- AssetName slash/casing typed equality；
- EventId case sensitivity；
- same EventId + different Asset 不合并；
- same identity + different location 进入同 group；
- exact duplicate first-wins；
- same raw key + different script 不 dedup；
- different raw key + same script 不 dedup；
- first true；
- multiple true -> first；
- all false -> candidate 0；
- evaluator exception -> false 后继续；
- duplicate evaluator 不重复执行；
- identity/group/candidate/current ordering；
- raw precondition result conversion；
- missing identity lookup behavior；
- selected candidate casing/location preservation；
- non-selected candidate 不参与 ownership；
- typed ownership 不因 StorageKey casing 分裂；
- different Asset identities with same EventId 不错误继承；
- prerequisite EventId case mismatch 不继承；
- source -> Reader -> Index -> ownership -> GalleryCatalog 的 BCL-only characterization fixture；
- 所有 Phase 1 Checks 继续通过。

不新增 test framework、package 或 game runtime dependency 到 Checks。

---

## 9. Phase 2 明确禁止

- ConditionIR / ConditionEvaluator / ConditionGap；
- SQLite 或数据库；
- PreviewState / PreviewPlan / StateInjector；
- 修改或拆分 `ReplayCoordinator`；
- 修改 `ReplaySnapshot` / `ReplaySaveGuard` / lifecycle / speed；
- CP passive/active variant discovery；
- `AssetsInvalidated` / `AssetReady` 新 refresh architecture；
- watched history schema/identity/capture/replay 语义变化；
- `HistoricalPlaybackBundle` 与 current Index 合并；
- UI layout/text/navigation；
- manifest/version/config/release behavior；
- unified EventLauncher；
- route planner / solver；
- Phase 3 或任何后续能力。

---

## 10. 验证命令

实现完成后必须运行：

```powershell
dotnet build -c Release
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
git diff --check
```

全部命令必须成功。Checks 的既有 `NETSDK1138` 可以记录但不得掩盖其他 warning/error。

---

## 11. Phase 2 报告

创建 `docs/PHASE2_REPORT.md`，至少记录：

- 实施基线与最终 implementation commit；
- 新增/修改文件；
- 实际层次结构；
- Index API；
- selection/dedup compatibility；
- typed ownership migration；
- build/check/diff-check 实际结果；
- 未修改的重要模块；
- 尚需人工 parity 验证内容；
- 非阻塞实施说明。

---

## 12. 验收标准

Phase 2 完成必须同时满足：

1. 五层结构在代码中明确存在，`GalleryCatalogCache` 不再直接解析 raw event entries 或执行 candidate selection。
2. Index 使用 typed identity，保存 Current + ordered read-only Candidates，且不保留 runtime context。
3. Source visitor/Reader 同步时序与当前实现一致。
4. Dedup、selection、fallback、exception 与 order contracts 均由自动 Checks 覆盖。
5. Ownership 使用 typed identity，只分析 CurrentEvents，算法与输出行为不变。
6. Diagnostics shape 与 cache timing 不变。
7. UI、Replay、History 与受保护文件不变。
8. Release build、Checks 与 `git diff --check` 通过。
9. `docs/PHASE2_REPORT.md` 完整记录实际结果与人工 parity 缺口。
10. 未开始 Phase 3 或其他禁止内容。
