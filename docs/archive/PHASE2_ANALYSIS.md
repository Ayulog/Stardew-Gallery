# Stardew Gallery Phase 2：Resolved Event Index 源码审计与实施设计

日期：2026-09-03

## 1. 文档状态与审计基线

- 工作分支：`phase2/resolved-event-index`
- Phase 1 基线 commit：`47c8fa55ae3f49a8eb3e5ce058e26088be24e0f0`
- 分支创建方式：从上述 Phase 1 HEAD 直接创建并推送。
- 本轮工作性质：只进行源码审计和实施设计，不修改业务代码。
- 仓库当前没有独立的 `PHASE2_TASK.md` 或 `PHASE2_DEVELOPMENT.md`。本设计以本次任务要求、`docs/PHASE1_TASK.md`、`docs/PHASE1_DEVELOPMENT.md` 和当前受版本控制源码为依据。
- 已审计全部 24 个 C# 源文件、主项目与 Checks 项目文件、build/manifest 配置，以及现有 Phase 1 文档。

Phase 2 的目标是把“最终内容管线中的 resolved events”与“Gallery UI catalog/ownership 组装”分层，并引入稳定的 `ResolvedEventIndex`。本阶段仍是零用户可见行为变化的结构迁移。

---

## 2. 当前数据流

```text
Utility.ForEachLocation
    -> GameLocation.TryGetLocationEvents
    -> final Data/Events dictionary
    -> validate/filter entry
    -> EventIdentity + EventFragments + root hashes
    -> ResolvedEvent
    -> group/deduplicate/select candidate
    -> temporary GalleryEvent with excluded ownership
    -> ParseEvidence
    -> OwnershipResolver
    -> GalleryEvent with final ownership
    -> included/excluded GalleryCatalog
    -> Gallery UI / Replay / Watched History
```

上述完整流程目前几乎都位于 `GalleryCatalogCache.cs`。`GalleryCatalogCache` 同时知道 SMAPI/游戏内容、事件定义解析、variant selection、NPC 候选、ownership、UI adapter、diagnostics 和 cache lifetime。

---

## 3. 当前 GalleryCatalogCache 的职责

### 3.1 Cache 与生命周期

`GalleryCatalogCache` 当前负责：

- 保存单个 nullable `GalleryCatalog` snapshot。
- `Invalidate()` 只清空 snapshot，`Get()` 在下一次打开 Gallery 时同步重建。
- 只有完整构建成功后才写入 cache。
- 保持同一次 Gallery home/detail/replay-return 流程继续使用原 snapshot。

当前 invalidation 来源位于 `ModEntry.cs`：

- `SaveLoaded`；
- `ReturnedToTitle`；
- locale change；
- `Data/Characters` invalidation；
- `Data/Events/*` invalidation；
- debug diagnostics 配置变化。

Phase 2 必须保留这个 coarse, lazy snapshot lifetime，不把 catalog 改成 live view，也不新增 refresh event architecture。

### 3.2 Character catalog

`ScanCharacters()` 当前负责：

- 枚举 child 与可社交 villager；
- 处理重名角色；
- 纳入当前事件 actor；
- 使用 friendship data 补充当前未加载但具有社交资源的 NPC；
- 检查 Character/Portrait 资源；
- 应用 `SocialTab` 可见性；
- 生成 `GalleryCharacter` 的 display name、met state 与 friendship points snapshot。

这些职责属于 Gallery catalog 层，不属于 resolved-event 读取层。

### 3.3 Event asset discovery

`ScanEvents()` 当前负责：

- 调用 `Utility.ForEachLocation(..., includeInteriors: true, includeGenerated: false)`；
- 对每个 runtime `GameLocation` 调用 `TryGetLocationEvents`；
- 读取内容管线最终结果，而不是原始 XNB 或 Mod 来源；
- 获取 authoritative `assetName`、event dictionary 和用于 precondition evaluation 的 source `GameLocation`；
- 保留 location 枚举顺序与 dictionary entry 顺序。

这部分应进入 `EventAssetCatalog`，作为唯一 game/content integration boundary。

### 3.4 Resolved event reading

`ScanEvents()` 对每个 raw entry 还负责：

- `GameLocation.IsValidLocationEvent(key, script)` validation；
- `EventKey.TryGetId`；
- placeholder script filtering；
- 构造 typed `EventIdentity(assetName, eventId)`；
- 使用 `EventFragmentCollector.Collect` 收集 root、`fork`、`switchEvent`、translation 与 cross-location fragments；
- 计算 `RootDefinitionHash` 与 `RootScriptHash`；
- 生成 `ResolvedEvent`。

这部分应进入 `ResolvedEventReader`。Reader 不应知道 character、ownership、Gallery UI、watched history 或 replay。

### 3.5 Candidate grouping 与 current selection

`ScanEvents()` 当前还负责：

- 以 typed `EventIdentity` 分组；
- 只在同一 identity 内比较 candidates；
- 按 `(RawEventKey, ResolvedScript)` 精确去重；
- exact duplicate 出现于多个 location context 时保留首次发现项；
- 使用 source `GameLocation.checkEventPrecondition(rawKey, check_seen: false)` 判断 applicability；
- applicability evaluator 抛异常时把该 candidate 当作不匹配；
- 多个 candidates 匹配时选择第一个；
- 全部不匹配时回退到第一个 candidate；
- 每个 identity 最终只输出一个 current `ResolvedEvent`；
- 生成 conflict diagnostics 所需的 selected key 与有序 candidate keys。

这部分应进入 `ResolvedEventIndex`。上述规则是 Phase 2 compatibility contract，不是可自由优化的实现细节。

### 3.6 Ownership 与 Gallery projection

`GalleryCatalogCache.Get()` 和 `ParseEvidence()` 当前负责：

- 从 selected event raw key 读取 friendship 与 prerequisite conditions；
- 从 root script 读取 actor positions；
- 从 ordered fragments 读取 dialogue counts；
- 调用 `OwnershipResolver.Resolve`；
- 生成 included 与 excluded `GalleryEvent`；
- 只保留至少拥有一个 included event 的 Gallery characters；
- 输出 summary log、conflicts、missing fragments 与完整 catalog diagnostics。

这些职责应留在 Gallery catalog/ownership 层。Ownership 必须只分析每个 identity 当前选中的 event，不能分析全部 candidates。

---

## 4. 当前架构问题

### 4.1 层次耦合

- `ScanEvents()` 同时进行 infrastructure discovery、domain construction、selection policy 和 UI adapter construction。
- `GalleryEvent` 在 ownership 尚未分析前就以 `Excluded/not-analyzed` placeholder 构造，随后再通过 `with` 替换 ownership。
- `ParseEvidence()` 接收 Gallery adapter，而它实际只需要 selected `ResolvedEvent` 的 identity、raw key、script 与 fragments。
- resolved-event pipeline 无法脱离 Gallery character scan 独立测试或复用。

### 4.2 Identity 表达仍不统一

- Candidate grouping 与 watched history 已使用 typed `EventIdentity`。
- Ownership 的 `EventEvidence.Identity` 和 result dictionary 仍使用 `GalleryEvent.Identity` 的 `StorageKey` string。
- `StorageKey` 保留 AssetName 原始 casing；两个相等的 typed identities 可以产生不同 casing 的字符串。
- Phase 2 若继续使用 string key，会让新的 index 与 Gallery ownership 存在两套相等语义。

### 4.3 Location 语义混杂

一个 event candidate 当前同时涉及三个不同概念：

- `AssetName`：最终事件定义来自哪个 content asset，是 `EventIdentity` 的组成部分；
- `LocationName = GameLocation.NameOrUniqueName`：当前 selected entry 的显示和 replay launch location；
- fragment root location = `GameLocation.Name`：`changeLocation` / `fork` / `switchEvent` 解析的起点。

另外，candidate applicability 必须在发现该定义的 source `GameLocation` 上执行。若只保留一个笼统的 `LocationName`，拆层后很容易把 identity、launch target、fragment root 和 runtime evaluation context 混为一谈。

### 4.4 Variant 信息被过早丢弃

- 同一 identity 的非 selected candidates 仅剩 conflict key 文本。
- 无法通过正式 API 查询当前 candidate set。
- 后续 ConditionIR 或 variant/history split 没有稳定的 current-content boundary。
- 不能使用 root hashes 补救该问题，因为它们既不包含 fragments，也不是 variant identity。

### 4.5 可测试性不足

- Checks 是纯 `net6.0` console project，通过 source-link 测试 BCL-only 文件。
- `ScanEvents()` 是 private 且直接依赖 `Game1`、`GameLocation`、SMAPI content 与 runtime enumeration。
- 目前没有自动测试固定 source order、exact deduplication、first-match、exception-as-false 或 first-candidate fallback。
- 没有端到端 pure fixture 固定 selected event count 和 ownership projection。

---

## 5. 正式语义与不变量

### 5.1 EventIdentity

```text
EventIdentity = normalized AssetName + case-sensitive EventId
```

- AssetName 统一斜杠并使用 `OrdinalIgnoreCase` equality。
- EventId trim 后使用 `Ordinal` equality。
- `LocationName`、raw event key、root hashes、fragments 和 ownership 都不属于 identity。
- Index 的唯一正式 key 必须是 `EventIdentity`，不得使用 `StorageKey` string。

### 5.2 Raw definition 与 candidate

- 一个 raw event dictionary entry 是 `(RawEventKey, ResolvedScript)`。
- RawEventKey 的第一个 `/` 前部分提供 EventId；完整 raw key 保留 conditions。
- 同一 asset 中的 Summer/Winter 等 raw keys 可以属于同一 `EventIdentity`。
- 同一 identity 下，不同 raw key 或不同 root script 都是不同 candidate。
- 当前兼容去重规则只折叠 raw key 和 script 都完全相同的 candidates。
- `RootDefinitionHash` 与 `RootScriptHash` 是 metadata，不参与 identity、candidate deduplication 或 lookup。

### 5.3 Location 与 launch context

- Source location 决定 candidate 的 live precondition evaluation context。
- Selected candidate 的 `ResolvedEvent.LocationName` 继续保存 `NameOrUniqueName`，供显示与现有 replay 使用。
- Fragment traversal 继续从 source location 的 `Name` 开始。
- 同一 LocationName、不同 AssetName、相同 EventId 必须是不同 identities。
- 同一 AssetName + EventId 即使从不同 GameLocations 暴露，仍属于同一 identity group。
- Phase 2 不新增按 location lookup，也不根据 location 重写 identity。

### 5.4 Current 与 historical

- `ResolvedEventIndex` 只描述当前最终内容管线中的 definitions。
- `Current` 是每个 identity 按现有 live precondition algorithm 选中的一个 candidate。
- `WatchedEventSnapshot` / `HistoricalPlaybackBundle` 是自然观看历史，继续由 `WatchedEventHistory` 独立管理。
- Historical versions 不进入 current index，也不会创建 Gallery rows、characters 或 counts。

---

## 6. 建议目标架构

```text
Game/SMAPI runtime
    -> EventAssetCatalog
       - enumerate current GameLocations
       - call TryGetLocationEvents
       - synchronously visit each source in current call order
       - provide transient location-bound callbacks
    -> ResolvedEventReader
       - validate/filter raw entries
       - construct EventIdentity
       - collect fragments/translations
       - compute root hashes
       - emit ordered ResolvedEventCandidate values
    -> ResolvedEventIndex.Build
       - group by typed EventIdentity
       - exact-deduplicate candidates
       - preserve order
       - select current candidate with existing policy
       - retain read-only candidate groups
    -> GalleryCatalogBuilder
       - parse ownership evidence from CurrentEvents only
       - resolve ownership
       - create GalleryEvent adapters
       - partition included/excluded
    -> GalleryCatalogCache
       - scan characters
       - log/diagnose/cache GalleryCatalog
    -> existing UI / Replay / Watched History
```

### 6.1 Layer responsibilities

| Layer | Owns | Must not own |
| --- | --- | --- |
| `EventAssetCatalog` | Runtime location enumeration, final `TryGetLocationEvents` results, source ordering, location-bound loaders/evaluators | Event identity, hashes, ownership, UI, history, replay |
| `ResolvedEventReader` | Raw entry validation, ID extraction, placeholder filtering, fragments, hashes, `ResolvedEvent` construction | Candidate selection, characters, ownership, cache lifetime |
| `ResolvedEventIndex` | Typed grouping, exact deduplication, stable ordering, current selection, identity lookup | GameLocation retention, GalleryEvent, ownership, history, refresh hooks |
| `GalleryCatalogBuilder` | Ownership evidence, existing resolver invocation, Gallery adapters, include/exclude and character projection | Asset discovery, candidate selection, cache lifetime, UI behavior |
| `GalleryCatalogCache` | Existing lazy cache, character scan, pipeline composition, composite snapshot ownership, logging and diagnostics | Raw asset parsing, fragment resolution, variant selection internals |
| Existing consumers | UI display, replay workflow, watched history | Index construction or candidate interpretation |

### 6.2 Lifetime rule

`EventAssetSource` 与 runtime callbacks 是 build-time objects。Source 必须在 `EventAssetCatalog` visitor 回调中同步交给 Reader，使 validation、fragment asset load 和 translation load 继续发生在进入下一个 location 之前。Applicability callback 可以随 candidate 保留到全部 locations 枚举完成后的 Index build；Index build 完成后必须丢弃 callback 与 `GameLocation` context。

`ResolvedEventIndex` 是没有 mutation API 的 read-only snapshot，保存私有复制的 group/candidate collections，不保存 live `GameLocation` 或 delegates。`ResolvedEvent` 内现有 `EventFragments` 仍按当前 read-only-by-contract 语义使用，本阶段不重写为深度 immutable collection。

`GalleryCatalogCache` 应把 Index 与 Gallery catalog 作为一个 private composite snapshot 原子缓存：

```csharp
private sealed record CacheSnapshot(
    ResolvedEventIndex ResolvedEvents,
    GalleryCatalog Gallery
);
```

`Invalidate()` 清空整个 composite；`Get()` 仍只向现有消费者返回 `snapshot.Gallery`。Index 与 Gallery catalog 共享 cache residency 和 invalidation lifetime，但 active menu/replay closures 仍可像当前一样在 invalidation 后继续持有已返回的 Gallery snapshot；Phase 2 不向 UI、Replay 或 `ModEntry` 暴露新的 Index 入口。

这个边界为未来 refresh 提供可替换 snapshot，但 Phase 2 不订阅新的 events，也不改变 invalidation timing。

---

## 7. 类型与 API 草案

以下是设计草案，不是本轮实现。

### 7.1 Event asset source

建议新增 `Catalog/EventAssetSource.cs`：

```csharp
internal sealed record EventAssetDefinition(
    string RawEventKey,
    string Script
);

internal sealed record EventAssetSource(
    string AssetName,
    string LaunchLocationName,
    string FragmentRootLocationName,
    IReadOnlyList<EventAssetDefinition> Definitions,
    Func<string, IReadOnlyDictionary<string, string>?> LoadLocationEvents,
    Func<string, string?> CheckPrecondition
);

internal interface IEventAssetSourceCatalog
{
    void VisitCurrent(Action<EventAssetSource> visit);
}
```

约束：

- `Definitions` 按原 dictionary enumeration order materialize。
- `LaunchLocationName` 来自 `NameOrUniqueName`。
- `FragmentRootLocationName` 来自 `Name`。
- `LoadLocationEvents` 只在同步 visitor 中由 Reader 使用，不能排队到后续 locations 枚举完成后才调用。
- `CheckPrecondition(rawKey)` 必须调用当前 source location 的 `checkEventPrecondition(rawKey, check_seen: false)` 并返回原始 string result。
- Raw result 的 nonempty/non-`"-1"` interpretation 与 exception-as-false policy 由 Index 统一实现并自动测试。
- `CheckPrecondition` 可以由 candidate 捕获到 Index build，但不能进入完成后的 Index snapshot。

### 7.2 EventAssetCatalog

建议新增 `Catalog/EventAssetCatalog.cs`：

```csharp
internal sealed class EventAssetCatalog : IEventAssetSourceCatalog
{
    public void VisitCurrent(
        Action<EventAssetSource> visit
    );
}
```

`VisitCurrent()` 只做：

- `Utility.ForEachLocation`，参数保持 `includeInteriors: true, includeGenerated: false`；
- `TryGetLocationEvents`；
- materialize 当前 source descriptor 与 callbacks；
- 在当前 `ForEachLocation` callback 返回前同步调用 `visit(source)`；
- 保留现有 `LoadLocationEvents` root alias 和 cross-location asset lookup 规则。

它不做 event validation、ID parsing、fragment traversal 或 selection。它也不保存 cache。调用者必须在 visitor 中立即调用 Reader；不能先收集所有 sources 再批量读取，否则会改变当前 `TryGetLocationEvents`、fragment asset load 与 translation load 的调用时序。

### 7.3 ResolvedEventReader

建议新增 `Catalog/ResolvedEventReader.cs`：

```csharp
internal sealed record ResolvedEventCandidate(
    ResolvedEvent Resolved,
    Func<string?> CheckPrecondition
);

internal sealed class ResolvedEventReader(
    Func<string, string, bool> isValidLocationEvent,
    Func<string, string[]> parseCommands,
    Func<string, string[]> parseArguments,
    Func<string, string?> loadTranslation
)
{
    internal IReadOnlyList<ResolvedEventCandidate> Read(
        EventAssetSource source
    );
}
```

生产组合时继续使用：

- `GameLocation.IsValidLocationEvent`；
- `Event.ParseCommands`；
- `ArgUtility.SplitBySpaceQuoteAware`；
- `Game1.content.LoadStringReturnNullIfNotFound`。

Reader 必须保持：

- invalid/empty-ID/placeholder entries 被过滤；
- raw key 与 script 原样保留；
- missing fragments 不删除 event，只写入 `Fragments.MissingKeys`；
- root hashes 公式不变；
- 输出顺序与 source definitions 顺序相同；
- precondition callback 捕获当前 source 与当前 raw key，但不在 Reader 中提前执行；它返回游戏的原始 string result。

### 7.4 ResolvedEventIndex

建议新增 `Catalog/ResolvedEventIndex.cs`：

```csharp
internal sealed record ResolvedEventGroup(
    ResolvedEvent Current,
    IReadOnlyList<ResolvedEvent> Candidates
)
{
    internal EventIdentity Identity => Current.Identity;
}

internal sealed class ResolvedEventIndex
{
    internal IReadOnlyList<ResolvedEventGroup> Groups { get; }

    internal IReadOnlyList<ResolvedEvent> CurrentEvents { get; }

    internal bool TryGetGroup(
        EventIdentity identity,
        out ResolvedEventGroup group
    );

    internal bool TryGetCurrent(
        EventIdentity identity,
        out ResolvedEvent resolved
    );

    internal IReadOnlyList<ResolvedEvent> GetCandidates(
        EventIdentity identity
    );

    internal static bool MatchesCurrentState(
        string? preconditionResult
    );

    internal static ResolvedEventIndex ReadCurrent(
        IEventAssetSourceCatalog assets,
        ResolvedEventReader reader
    );

    internal static ResolvedEventIndex Build(
        IReadOnlyList<ResolvedEventCandidate> candidates
    );
}
```

内部结构建议：

```text
ordered List<ResolvedEventGroup>
+
Dictionary<EventIdentity, ResolvedEventGroup>
```

这样 lookup 使用 typed equality，而 `Groups` / `CurrentEvents` 明确保留 first identity discovery order，不依赖 Dictionary enumeration 作为未声明契约。`ResolvedEventGroup.Identity` 始终代理 `Current.Identity`，因此 selected candidate 的 AssetName spelling/casing 与现有 `GalleryEvent.Identity` 一致。内部 lookup dictionary 可以使用任一 equal typed identity，但不得把 group key 的 casing 投影到 Current。

`GetCandidates` 对不存在的 identity 返回空 list；`TryGetGroup` / `TryGetCurrent` 返回 false。

`ReadCurrent` 是 production orchestration seam：它在 `VisitCurrent` callback 内立即调用 `reader.Read(source)`，累计 candidates 后再调用 `Build`。Checks 使用 fake `IEventAssetSourceCatalog` 测试 call order；`GalleryCatalogCache` 必须调用这个 API，不能自行先 materialize sources。

Index build algorithm：

1. 按输入顺序读取 candidates。
2. 以 `Resolved.Identity` 建立 group。
3. 在 group 内按 exact `RawEventKey ==` 与 `ResolvedScript ==` 去重。
4. Exact duplicate 保留第一次出现的 candidate 与 location context。
5. 对 deduplicated candidates 调用现有 `EventKey.SelectVariantIndex`。
6. `MatchesCurrentState` 只在 raw result 非 null/empty 且不等于 `"-1"` 时返回 true。
7. `CheckPrecondition` exception 视为 false 并继续下一个 candidate。
8. 多个 true 选择第一个；全部 false 使用 index 0。
9. 构建只包含 `ResolvedEvent` 的 read-only group，丢弃 callbacks/runtime context。

不提供以下 API：

- string/`StorageKey` lookup；
- LocationName lookup；
- raw key lookup；
- hash lookup；
- historical version lookup；
- mutation、refresh 或 reselect API。

### 7.5 Typed ownership 与 GalleryCatalogBuilder

建议在 Phase 2 将 ownership 的 key 一并改为 typed identity，但不改变算法：

```csharp
internal sealed record EventEvidence(
    EventIdentity Identity,
    string EventId,
    IReadOnlyDictionary<string, int> FriendshipRequirements,
    IReadOnlyList<string> PrerequisiteEventIds,
    IReadOnlySet<string> Actors,
    IReadOnlyDictionary<string, int> DialogueCounts
);

internal static IReadOnlyDictionary<EventIdentity, EventOwnership> Resolve(
    IReadOnlyList<EventEvidence> events,
    IReadOnlySet<string> eligibleCharacters
);
```

建议新增可 source-link 的 `Catalog/GalleryCatalogBuilder.cs`：

```csharp
internal sealed record GalleryCatalogBuildResult(
    GalleryCatalog Catalog,
    IReadOnlyList<GalleryEvent> AnalyzedEvents
);

internal sealed class GalleryCatalogBuilder(
    Func<string, string[]> splitPreconditions,
    Func<string, string[]> parseCommands,
    Func<string, string[]> splitArguments,
    Func<string, string[]> splitPositions,
    Func<string?> getSpouse
)
{
    internal GalleryCatalogBuildResult Build(
        IReadOnlyList<GalleryCharacter> characters,
        IReadOnlyList<ResolvedEvent> currentEvents
    );
}
```

Builder 内部保存生产使用的 `ParseEvidence(ResolvedEvent)`，通过 injected delegates 继续调用等价于当前的：

- `Event.SplitPreconditions`；
- `Event.ParseCommands`；
- `ArgUtility.SplitBySpaceQuoteAware`；
- `ArgUtility.SplitBySpace`；
- `Game1.player.spouse`。

Builder 只接收 Index 的 selected `CurrentEvents`，调用现有 `OwnershipResolver`，在 ownership 已确定后构造 `GalleryEvent`，再生成 included/excluded lists 与 filtered Gallery characters。`AnalyzedEvents` 让 `GalleryCatalogCache` 在不重复 ownership projection 的情况下保持现有 summary counts 和 diagnostics。

这个调整消除 resolved/index 与 ownership 之间的 string identity seam，同时继续保留 `GalleryEvent.Identity` 给 UI 临时 state 使用。

---

## 8. 各操作的目标归属

| 当前操作 | Phase 2 目标位置 | 说明 |
| --- | --- | --- |
| `ScanEvents()` orchestration | 由 `EventAssetCatalog` + Reader + Index 替代 | 原 private monolith 最终删除 |
| `Utility.ForEachLocation` | `EventAssetCatalog` | 参数和顺序不变 |
| `TryGetLocationEvents` | `EventAssetCatalog` | 继续读取 final content pipeline |
| `LoadLocationEvents` | `EventAssetCatalog` 的 source-bound callback | root aliases 与 conventional asset path 不变 |
| validity / ID / placeholder filtering | `ResolvedEventReader` | 原 helper 继续复用 |
| `EventFragmentCollector.Collect` | `ResolvedEventReader` | collector 本身不重写 |
| root hash calculation | `ResolvedEventReader` | hash 类型和公式不变 |
| candidate grouping | `ResolvedEventIndex` | key 只能是 `EventIdentity` |
| exact candidate dedup | `ResolvedEventIndex` | raw key + script，first wins |
| `SelectVariantIndex` | `ResolvedEventIndex` | helper 与 behavior 保留 |
| precondition call | source callback，由 Index 按需调用 | source GameLocation 不进入 index snapshot |
| conflict projection | `GalleryCatalogCache` | 从 Index groups 生成现有 diagnostics shape |
| character scan | `GalleryCatalogCache` | 不迁移 |
| ownership evidence/resolve | `GalleryCatalogBuilder` | 只接收 `CurrentEvents` |
| `GalleryEvent` creation | `GalleryCatalogBuilder` | ownership 完成后构造 |
| included/excluded partition | `GalleryCatalogBuilder` | 行为不变 |
| cache/invalidation | `GalleryCatalogCache` + 当前 `ModEntry` handlers | 不新增 refresh architecture |

---

## 9. Phase 2 数据流细节

建议 `GalleryCatalogCache.Get()` 保留当前调用时序：先 `ScanCharacters()`，再构建 events。不要因为拆层而交换顺序。

伪代码：

```csharp
IReadOnlyList<GalleryCharacter> characters = ScanCharacters();

ResolvedEventIndex index =
    ResolvedEventIndex.ReadCurrent(
        eventAssets,
        eventReader
    );

IReadOnlyList<ResolvedEvent> currentEvents =
    index.CurrentEvents;

GalleryCatalogBuildResult result = galleryBuilder.Build(
    characters,
    currentEvents
);

CacheSnapshot snapshot = new(index, result.Catalog);
```

`ReadCurrent` 在每个 location callback 内同步调用 Reader，因此 fragment/content calls 的相对时序与当前 `ScanEvents()` 一致。Precondition callbacks 仍在全部 sources 扫描完成后由 `ResolvedEventIndex.Build` 执行，匹配当前 selection timing。Composite snapshot 的赋值点保持与当前 Gallery cache 一致：catalog build 与 summary log 完成后、optional diagnostics 之前；diagnostics 继续自行捕获写入错误。

行为计数必须保持：

- `CurrentEvents` count = deduplicated identity groups count；
- Gallery included event count = ownership kind 非 Excluded；
- `ExcludedEvents` count = ownership kind 为 Excluded；
- Gallery character count = 至少拥有一个 included event 的 character 数；
- 一个 event 有多个 owners 时仍在多个 NPC 页面计数；
- seen state 继续按 `Game1.player.eventsSeen.Contains(EventId)`，不改成 typed identity。

---

## 10. 文件级迁移计划

### 10.1 新增文件

`Catalog/EventAssetSource.cs`

- 定义 build-time source 与 ordered raw definitions。
- 定义 BCL-only `IEventAssetSourceCatalog.VisitCurrent` contract。
- 明确 AssetName、launch location、fragment root location 与 runtime callbacks。
- 保持 BCL-only，便于 Checks source-link。

`Catalog/EventAssetCatalog.cs`

- 从 `GalleryCatalogCache.ScanEvents()` 移入 runtime location enumeration、`TryGetLocationEvents` 与 cross-location asset loader。
- 这是唯一直接知道 `Utility.ForEachLocation` 和 source `GameLocation` 的新层。
- 使用同步 visitor，保持每个 location 内 reader/fragment load 完成后才继续下一个 location。
- 不缓存，不订阅 events。

`Catalog/ResolvedEventReader.cs`

- 移入 entry validation/filtering、identity construction、fragment collection、hashes 与 candidate creation。
- 使用 injected parsing/content delegates，保持 functional core 可测试。

`Catalog/ResolvedEventIndex.cs`

- 定义 read-only group/index 与 Build algorithm，并私有复制输入 collections。
- 通过 `ReadCurrent(IEventAssetSourceCatalog, ResolvedEventReader)` 固化同步 visitor orchestration。
- 使用 typed key，保留 ordered candidate groups 与 selected current events。
- 不引用 UI、history、replay 或持久化模型。

`Catalog/GalleryCatalogBuilder.cs`

- 移入 `ParseEvidence`、typed ownership orchestration、`GalleryEvent` creation 与 included/excluded/character projection。
- 使用 injected parse/spouse delegates，保持生产代码路径可由现有 BCL-only Checks source-link。
- 返回 Gallery catalog 与 analyzed events，供 cache 层保持现有 logging/diagnostics。

### 10.2 修改文件

`GalleryCatalogCache.cs`

- 保留 `Get()`、`Invalidate()`、`ScanCharacters()`、`HasSocialAssets()`、pipeline composition、logging 与 diagnostics。
- 用 source -> reader -> index pipeline 替代 `ScanEvents()`。
- 以 private `CacheSnapshot(ResolvedEventIndex, GalleryCatalog)` 原子持有同一 build 的两个 views。
- 从 index groups 投影当前 `IdentityConflict` diagnostics，保持现有输出字段和计数。
- 删除已迁移的 `ScanEvents()`、`LoadLocationEvents()` 与 `ParseEvidence()`。

`EventOwnership.cs`

- 只把 `EventEvidence.Identity` 和 result dictionary key 从 string 改为 `EventIdentity`。
- 不修改 direct/inherited/inferred/excluded algorithm、tie behavior 或 prerequisite EventId rules。

`Checks/StardewGallery.Checks.csproj`

- Source-link 新的 BCL-only source、reader、index 与 Gallery builder 文件。
- 不新增 test framework 或 package。

`Checks/Program.cs`

- 先添加 characterization fixtures，再覆盖 reader/index 和 Gallery projection parity。
- 保留所有现有 Checks。

### 10.3 预期不修改文件

- `Domain/EventIdentity.cs`
- `Domain/ResolvedEvent.cs`
- `Domain/EventHashes.cs`
- `Domain/HistoricalPlaybackBundle.cs`
- `GalleryCatalog.cs`
- `GalleryMenu.cs`
- `GalleryCharacterMenu.cs`
- `ModEntry.cs`
- `WatchedEventHistory.cs`
- `EventFragments.cs`
- `EventKey.cs`
- `ReplayCoordinator.cs`
- `ReplaySnapshot.cs`
- `ReplaySaveGuard.cs`
- `ReplayLifecycleRules.cs`
- `ReplaySpeedPatches.cs`
- UI assets、i18n、config、manifest、version 与 release documents

若实现中需要修改上述文件才能编译，应先确认这是纯 adapter compatibility，而不是扩大 Phase 2 范围。

---

## 11. 建议实施顺序

1. 在现有 Checks 中加入当前 candidate ordering、deduplication 与 selection 的 characterization cases。
2. 新增 `EventAssetSource` contract，不切换生产调用。
3. 抽出同步 visitor 形式的 `EventAssetCatalog`，暂时让旧 `ScanEvents()` 在 visitor 内消费 source，确认输入顺序、数量与 fragment/content load call order 不变。
4. 抽出 `ResolvedEventReader`，暂时保留旧 grouping/selection，确认 resolved fields、fragments 和 hashes 不变。
5. 新增 `ResolvedEventIndex.Build` 并以 characterization checks 固定 selection parity。
6. 抽出 BCL-only `GalleryCatalogBuilder`，通过 injected production delegates 固定 evidence parsing 与 selected-only ownership projection。
7. 将 ownership key 改为 typed `EventIdentity`，不改算法，并增加 duplicate EventId ambiguity checks。
8. 改造 `GalleryCatalogCache.Get()` 使用 index + builder composite snapshot。
9. 删除被替代的 `ScanEvents()`、`LoadLocationEvents()` 与 cache 内 `ParseEvidence()`，检查没有生产调用继续使用 `EventKey.GetIdentity`。
10. 审查完整 diff，确认 Replay、History、UI 与后续阶段文件没有变化。
11. 运行 Release build、全部 Checks、`git diff --check`，再进行 catalog parity 实机验收。

每一步都应保持可编译；不要一次同时改变 discovery、selection 和 ownership algorithm。

---

## 12. Compatibility risks

### 12.1 Source order 与 selection

风险：LINQ regrouping、sorting 或不同 collection materialization 改变 first candidate、first match 或 fallback。先 materialize 全部 sources 再运行 Reader 还会把早期 location 的 fragment/translation loads 移到所有 `TryGetLocationEvents` calls 之后，改变当前 content-pipeline call order 与 exception timing。

控制：`EventAssetCatalog` 使用同步 visitor；Reader 在每个 location callback 返回前完成。所有 definition、group 和 candidate collections 显式保序，不排序，并用 characterization checks 固定结果。

### 12.2 Exact duplicate across locations

风险：把 source location 加入 dedup key 会增加 candidate/conflict count；忽略 current first-wins 又可能改变 launch location。

控制：Phase 2 继续只按 raw key + script 去重，并保留首次发现 context。

### 12.3 AssetName 与 StorageKey

风险：用 string key 会在 slash/casing 等价 identity 上分裂 index 或 ownership。

控制：Index 和 ownership 使用 `EventIdentity`；`StorageKey` 只留给现有 UI state。

### 12.4 Location fields

风险：使用 `Name` 代替 `NameOrUniqueName` 会改变显示/replay；反向替换会改变 fragment traversal。

控制：source contract 分开 `LaunchLocationName` 与 `FragmentRootLocationName`，并分别建立 tests。

### 12.5 Content Patcher compatibility

风险：直接扫描已知 asset names 或原始内容会绕过当前 final content pipeline。

控制：只通过 runtime locations 的 `TryGetLocationEvents` 读取最终内容；不做 source-mod discovery。

### 12.6 Fragment behavior

风险：reader 把 missing fragment 当作 fatal，或改变 scripts/missing keys 顺序。

控制：复用 `EventFragmentCollector`，不重写；missing fragments 继续保留 resolved event。

### 12.7 Ownership 与 NPC grouping

风险：对全部 candidates 分析 ownership，或在 typed-key 迁移时改变 prerequisite matching。

控制：只对 `CurrentEvents` 调用现有 resolver；prerequisite 仍按 case-sensitive EventId 且要求唯一 predecessor。

### 12.8 Current replay

风险：Index 看起来拥有 resolved script 后，容易把 current replay 改成直接启动该 script。

控制：不修改 `ReplayCoordinator`。Current replay 继续用 selected `GalleryEvent.LocationName` + `EventId` 调用 live `Game1.PlayEvent`。

### 12.9 Watched history

风险：把 historical snapshots 加入 index，或按 location/raw key 查询 history。

控制：不修改 `WatchedEventHistory`。UI 继续通过 `GalleryEvent` 的 typed resolved identity 获取历史版本。

### 12.10 Cache lifetime

风险：给 EventAssetCatalog/Index 添加独立 cache 后产生双 cache、stale GameLocation 或不同 refresh timing。

控制：Asset catalog、Reader 与 Builder stateless；完成后的 Index read-only；Index 与 Gallery catalog 由现有 cache 作为同一个 composite snapshot 原子持有和失效。

### 12.11 Diagnostics

风险：直接序列化新 index graph 改变 `catalog-latest.json` shape 或暴露 delegates/runtime objects。

控制：diagnostics 继续由 Gallery 层投影；index 不序列化 source callbacks；现有 summary/conflict/missing-fragment 字段保留。

### 12.12 实机噪声

Phase 1 报告记录过节庆布置状态下外部 dialogue patch 与 UI mode 不匹配导致的恢复超时。Phase 2 replay parity 失败只有在干净、可重复的运行环境中复现并排除外部 patch 后，才应归因于本次 catalog 分层。

---

## 13. Automated checks 计划

### 13.1 EventAssetSource contract

- Definitions 保持输入顺序。
- `LaunchLocationName` 与 `FragmentRootLocationName` 可不同且不互相覆盖。
- 同一 asset 可由多个 source contexts 暴露。
- Empty source 不产生 candidates。
- Fake visitor 按 `visit source A -> A fragment loads -> visit source B -> B fragment loads` 记录 call order，禁止 `visit A -> visit B -> A loads`。
- Reader exception 在当前 source visitor 内传播，后续 source 不再被访问，匹配当前 enumeration timing。

Live `Utility.ForEachLocation` / `TryGetLocationEvents` 枚举以及 source adapter 是否传入 `check_seen: false` 仍需 code review 与游戏内 smoke test；纯 Checks 不模拟 SMAPI runtime。

### 13.2 ResolvedEventReader

- Valid entry 完整映射 identity、location、raw key、script、fragments 与 hashes。
- Invalid location event 被过滤。
- Empty ID 被过滤。
- Dotted/modded ID 与带多个 `/` 的 raw key 保持现有解析。
- Placeholder script 被过滤。
- 相同 script、不同 raw key 只改变 `RootDefinitionHash`。
- Root script 变化同时改变两个 root hashes。
- Fragment-only dependency 变化不改变两个 root hashes。
- `fork` 两种参数形式、`switchEvent`、translation、`changeLocation`、cycle、repeated reference、missing event key 与 missing translation。
- Missing fragments 保留 event 且按当前顺序写入 `MissingKeys`。

### 13.3 ResolvedEventIndex identity

- `Data\\Events\\Town` 与 `data/events/town` + 同 EventId 合并。
- EventId 大小写不同不合并。
- AssetName 不同、EventId 相同不合并。
- LocationName 相同不导致合并。
- LocationName 不同不阻止相同 AssetName + EventId 分组。
- `StorageKey`、raw key 与 hashes 不作为 dictionary key。

### 13.4 Deduplication 与 selection

- 相同 raw key + 相同 script 折叠，保留第一次出现项。
- 相同 raw key + 不同 script 保留两个 candidates。
- 不同 raw key + 相同 script 保留两个 candidates。
- Hash 相同不能单独触发 deduplication。
- 第一个 true 被选中。
- 多个 true 仍选择第一个。
- 全 false 回退 candidate 0。
- Raw precondition result 为 null、empty 或 `"-1"` 时 false；`"0"`、whitespace 与其他 nonempty value 按当前规则为 true。
- Precondition callback 收到 candidate 的完整 raw key。
- callback exception 视为 false，并继续检查后续 candidate。
- Exact duplicate 的 evaluator 不被重复调用。
- Groups 与 CurrentEvents 保持 first identity discovery order。
- `GetCandidates` 保持 post-dedup candidate order。
- Missing identity 的 `GetCandidates` 返回 empty，两个 `TryGet` APIs 返回 false。

### 13.5 Location context

- Applicability callback 来自 candidate 的 source context，而不是按 identity 或 LocationName 重新查找。
- Selected candidate 的 `LocationName` 原样进入 current event。
- Exact duplicate from two locations 保留 first source location。
- Fragment loader 使用 `FragmentRootLocationName`，current replay-facing value使用 `LaunchLocationName`。
- Same typed identity 的 later candidate 使用不同 AssetName casing 且被选中时，group identity 与 Current/adapter string 均保留 selected candidate casing。

### 13.6 Gallery ownership projection

建立一个 BCL-only end-to-end fixture：

```text
ordered asset sources
    -> reader
    -> index
    -> selected ResolvedEvents
    -> GalleryCatalogBuilder production evidence parser
    -> existing OwnershipResolver
    -> GalleryCatalog
```

断言：

- selected event count；
- included/excluded count；
- direct/inherited/inferred/excluded ownership；
- multi-owner count；
- friendship/prerequisite raw-condition parsing；
- root actor、spouse substitution 与 fragment dialogue evidence；
- Gallery adapter compatibility properties；
- same identity 不因 StorageKey casing 分裂 ownership；
- non-selected candidates 不参与 ownership；
- 两个不同 Asset identities 共享同一 EventId 时，prerequisite predecessor 保持 ambiguous，不能继承；
- prerequisite EventId 大小写不匹配时不能继承；
- Gallery characters 只保留至少拥有一个 included event 的角色。

### 13.7 Existing checks 与最终验证

- 保留全部 Phase 1 identity/hash/JSON adapter checks。
- 保留 EventKey snapshot fingerprint、Ownership、Fragments、Layout、UI rules 与 Replay lifecycle checks。
- Phase 2 实现完成后运行：

```powershell
dotnet build -c Release
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
git diff --check
```

自动检查不能替代以下实机 parity：

- current event count；
- included/excluded count；
- NPC grouping 与每个 NPC event count；
- raw condition display；
- current replay；
- historical replay 与 watched history；
- catalog invalidation 后重新打开行为。

---

## 14. Phase 2 明确 out-of-scope

本阶段禁止：

- ConditionIR、ConditionEvaluator 或 ConditionGap；
- SQLite 或任何数据库；
- PreviewState、PreviewPlan、StateInjector 或各类 state injectors；
- 修改或拆分 `ReplayCoordinator`；
- 修改 `ReplaySnapshot`、`ReplaySaveGuard`、Replay lifecycle 或 speed patches；
- CP variant discovery、passive discovery 或 source-mod provenance；
- `AssetsInvalidated` / `AssetReady` 新 refresh architecture；
- watched-history schema、identity、dedup、capture、save 或 replay 语义变更；
- Historical snapshots 与 current index 合并；
- UI layout、display text、navigation 或 interaction 变更；
- manifest、version、config、release package 或 dependency 变更；
- route planner、solver 或 unified EventLauncher；
- 任何 Phase 3 或后续功能。

Phase 2 可以保留当前已经发现的同 identity candidates，但不能把它扩展成新的 CP discovery、用户可见 variant picker 或历史 variant store。

---

## 15. 尚需 Codex 决策的问题

以下问题不阻塞本轮分析，但应在 Phase 2 实现前由 Codex 明确：

1. **Candidate API surface**：是否接受 Index 在 Phase 2 同时暴露 `Current` 与 read-only `Candidates`？建议接受，以建立后续 variant split seam；UI 仍只接收 Current。
2. **Runtime source context 表达**：采用本文建议的 transient delegates，还是采用 source ordinal + 外部 GameLocation table？建议 delegates；fragment loader 在 visitor 内消费，precondition callback 只保留到 Build，并在 Build 后完全丢弃。
3. **Ownership typed-key migration**：是否在 Phase 2 将 `EventEvidence`/resolver result 一并改为 `EventIdentity`？建议本阶段完成，否则新 index 之后仍保留易错 string seam。
4. **Exact duplicate across locations**：是否正式确认 raw key + script 相同时继续 first source wins，且不保留第二个 location context？零行为变化要求建议确认当前规则；未来若要多 launch contexts，应另立阶段。
5. **Diagnostics compatibility**：`catalog-latest.json` 是否视为需要字段级稳定的 debug contract？建议 Phase 2 保留现有 summary/conflict/catalog shape，仅允许内部来源改为 index projection。
6. **Reader/Builder test seams**：是否接受 constructor delegates 与 `IEventAssetSourceCatalog` fake 以保持 Checks BCL-only，还是允许 Checks 引用 game assemblies？建议保留当前 source-link、无 game runtime 的测试模式，并测试生产 Reader、Index orchestration 与 Gallery builder 本身。
7. **Exception policy**：是否确认仅 precondition evaluator exceptions 按当前规则转为 false，而 validation、parsing 与 fragment collection exceptions 继续让整个 catalog build 失败且不写 cache？建议严格保持当前行为。
8. **目录布局**：新增类型放入 `Catalog/` 目录，还是继续放 repository root？建议使用 `Catalog/`，不移动现有文件，避免 rename churn。
9. **Parity fixture**：哪一个固定 save/mod set 作为 Phase 2 event count 与 NPC grouping A/B 基线？建议沿用已验证存档，并在实现前记录 1.0.0/Phase 1 的具体 counts。
10. **Index lookup 最小集**：是否只实现 `TryGetGroup`、`TryGetCurrent`、`GetCandidates`，暂不增加 raw key/location/hash lookup？建议采用最小集，后续需求出现后再扩展。
11. **Index ownership**：是否接受 `GalleryCatalogCache` 私有原子持有 `CacheSnapshot(Index, GalleryCatalog)`，但 Phase 2 不向现有消费者暴露 Index？建议接受；这样 Index 有明确 snapshot lifetime，又不会扩大 UI/Replay API。

如果 Codex 未另行决定，实施时应采用每项后的建议默认值，因为它们最接近当前行为且最容易回退。

---

## 16. Phase 2 验收条件

Phase 2 可以判定完成的最低条件：

1. `GalleryCatalogCache` 不再直接枚举/解析 raw event definitions 或执行 variant selection。
2. `ResolvedEventIndex` 使用 typed `EventIdentity`，并明确保存 current + ordered candidates。
3. Event asset discovery、resolved reading、indexing、Gallery ownership projection 四层边界在代码中可见。
4. 当前 source/fragment call order、filtering、ordering、deduplication、raw precondition interpretation、selection、fallback、fragment 与 ownership projection 行为有自动 characterization checks。
5. UI、Replay、Watched History、cache invalidation handlers 与 persisted schema 不变。
6. Release build、全部 Checks 与 `git diff --check` 通过。
7. 实机 A/B 确认 event count、NPC grouping、current replay 和 historical/watched history 无回归。
8. 未实现本文件第 14 节中的任何后续阶段能力。
