# Stardew Gallery Phase 4：History / Variant Semantic Split 审计与设计

日期：2026-09-03

## 0. 文档属性

- 工作分支：`phase4/history-variant-semantics`
- 基线：`6ada19e3fe047e0d7a1826410bf9f0b92cb49b7d`（Phase 3 final）
- 性质：只读源码审计 + 语义设计 + 兼容方案 + 实施计划。
- 本阶段不实现任何业务代码。不开始 SQLite。
- 所有语义均标注证据等级：`[REPO]` 仓库已证实；`[CHAIN]` 沿现有计算链确认；`[NATIVE]` 官方文档证实。

---

## 1. 当前 History Pipeline（实测）

以 `WatchedEventHistory.cs` 为主，`[REPO]`。

```text
Game1.UpdateTicked (每 tick)
  → watchedHistory.Update(replay.IsActive)
  → Game1.CurrentEvent? 当前事件
  → ReplayActive? 若 true：清空 observedEvent/pendingSnapshot 并 return（replay 排除）
  → ReferenceEquals(current, observedEvent)? 相同则 return
  → CommitPending()  (对上一个 pendingSnapshot 落盘)
  → current null? 重置 observedEvent 并 return
  → observedEvent = current
  → TryCapture(current) → pendingSnapshot 或 reason
  → (下一 tick 进入 commit / 或事件仍进行中不 commit)
```

核心方法：

- `Load()`：读 save `watched-event-versions` → gzip + base64 + JSON `List<WatchedEventSnapshot>` → 逐个 `Add(save:false)`。解压失败/损坏时设 `canSave=false`，避免覆盖原记录。
- `Clear()`：`ReturnedToTitle` 调用（清空 entries、observedEvent、pendingSnapshot）。`SaveLoaded` 调用 `Load()`（内部同样清空这三个字段后重读）。
- `Get(EventIdentity)`：返回该 identity 的版本列表，按 `LastWatchedAt` 降序。`Get(GalleryEvent)` 委托到 `Get(entry.Resolved.Identity)`。
- `Update(bool replayActive)`：见上。
- `TryCapture(Event current, ...)`：
  1. 用 `current.fromAssetName`（asset）+ `current.id`（EventId）+ `Game1.currentLocation`。
  2. 要求 location 非空、assetName/eventId 非空、asset 存在。
  3. 在 asset 内找同 EventId (`EventKey.TryGetId`) 且 `Event.ParseCommands(value)`.SequenceEqual(`current.eventCommands`) 的条目——即从内容管线中「还原出」与当前运行命令完全一致的 root definition。
  4. 找不到匹配 → reason，返回 false（不生成 snapshot）。
  5. CollectFragments(match.value, location.Name, eventAssets, translations)：递归收集 `fork`/`switchEvent`/translation/`changeLocation`。任一 fragment asset 缺失或 translation 缺失 → false。
  6. fingerprint = `EventKey.GetSnapshotFingerprint(rootScript, eventAssets, translations)`。
  7. snapshot = `WatchedEventSnapshot(location.NameOrUniqueName, assetName, eventId, match.key, match.value, eventAssets, translations, locale, fingerprint, now, now)`。
- `CommitPending()`：`pendingSnapshot` 且 `Game1.player.eventsSeen.Contains(snapshot.EventId)` 才 `Add(save:true)`。即「仅当事件进入 eventsSeen 后」才承认完整观看。
- `Add(snapshot, save)`：
  - `identity = snapshot.Identity`（= AssetName + EventId，typed）。
  - 同 identity 的列表里按 `Fingerprint` 查找；找到则用 `snapshot with { FirstWatchedAt = 原值, LastWatchedAt = new }` 替换（**同一 variant 只更新 LastWatchedAt，保留 FirstWatchedAt**）；找不到则新增。
  - 列表按 `LastWatchedAt` 降序排序。
  - `save` 时序列化 `entries.Values.SelectMany(...)`（typed key 不会进 JSON）。
- `Save()`：gzip + base64 写入 `helper.Data.WriteSaveData(SaveKey, ...)`。

`HistoricalReplayAssets`（同文件）：`Activate(WatchedEventSnapshot)` 时记录 active snapshot 并 invalidate 其 EventAssets + Translations 资产；`OnAssetRequested` 在对应内容 asset 请求时 late-edit 注入捕获的 variant fragment 内容；`Clear` 移除注入并重新失效。

---

## 2. WatchedEventSnapshot 语义逐字段审计

`[REPO]` 现 11-field DTO：

| 字段 | 实际语义 | 更接近哪一层 |
| --- | --- | --- |
| LocationName | `location.NameOrUniqueName` | Observation / launch metadata（非 EventIdentity，不是 variant identity） |
| AssetName | `current.fromAssetName` | Identity 组成部分（EventIdentity.AssetName） |
| EventId | `current.id` | Identity 组成部分（EventIdentity.EventId） |
| EventKey | `match.key`（raw key） | ObservedVariant definition（RootDefinitionHash 会反映 raw key + root script） |
| RootScript | `match.value` | ObservedVariant definition（content） |
| EventAssets | 收集的 fork/switch/translation fragments | Playback bundle（+ ObservedVariant definition 的一部分） |
| Translations | 收集的 translation 内容 | Playback bundle（+ ObservedVariant） |
| Locale | `LocalizedContentManager.CurrentLanguageCode` | Playback bundle（语言变体） |
| Fingerprint | `GetSnapshotFingerprint(rootScript, eventAssets, translations)` | 见 §9 —— 实为 **PlaybackHash** |
| FirstWatchedAt | 首次捕获该 variant 的时间（Add 保留首次值） | Observation aggregate（version 聚合） |
| LastWatchedAt | 最近一次捕获该 variant 的时间（Add 更新） | Observation aggregate（version 聚合） |

### 2.1 结论：单个 DTO 混合了多个概念

`WatchedEventSnapshot` 同时在领域层承载：

1. **ObservedVariant definition**：AssetName + EventId（identity 来源）+ EventKey + RootScript + EventAssets + Translations + Locale + Fingerprint(PlaybackHash)。
2. **Playback bundle**：RootScript + EventAssets + Translations + Locale + PlaybackHash。
3. **Observation aggregate metadata**：FirstWatchedAt / LastWatchedAt（同 variant 聚合，非单次）。

它**不是**单次历史实例（HistoricalEventRecord）——因为 `Add` 对同一 `Fingerprint` 会合并成一条并只更新 `LastWatchedAt`，多个 natural 观看不会产生多条记录。这与任务书 §7、§15 的预期一致。

### 2.2 FirstWatchedAt / LastWatchedAt 是聚合时间，不是 instance 时间

沿 `Add` 计算链 `[CHAIN]`：同 `Fingerprint` 的 snapshot 用 `with { FirstWatchedAt = 原值, LastWatchedAt = new }` 合并。因此：

- `FirstWatchedAt` = 该 content variant **第一次**被本 Mod 捕获观察到的时间。
- `LastWatchedAt` = 该 content variant **最近一次**被捕获观察到的时间。
- 两者都是「同一 variant 的观察聚合时间轴」，不代表几次独立观看、更不代表每次自然经历的精确时间点。

---

## 3. 术语与领域定义（Phase 4 目标语义）

Phase 4 必须正式区分：

```text
EventIdentity            逻辑身份：normalized AssetName + case-sensitive EventId
Current ResolvedEvent    当前 content pipeline 此刻解析出的定义
ObservedVariant          某个内容版本曾被实际捕获/观察过
VariantObservation       记录「某次观察到了某 variant」（可能未完整观看）
HistoricalEventRecord    玩家自然游戏中真正完整经历了一次事件
HistoricalPlaybackBundle 冻结的、用于精确历史回放的 immutable-ish 播放内容
KnownSeen evidence       由 eventsSeen 提供的「看过」证据（无内容版本信息）
Replay / Preview         回放 / 预览（不产生 HistoricalEventRecord）
```

关键等价断言：

```text
ObservedVariant != HistoricalEventRecord
"这个内容版本曾经出现过" != "玩家在某个具体时间实际经历了一次事件"
```

`WatchedEventSnapshot` 不能继续在领域层同时代表 ObservedVariant + observation aggregate。

---

## 4. ObservedVariant 模型

### 4.1 唯一 identity（回答 5A）

ObservedVariant 的唯一 identity 候选：

| 候选 | 判定 | 理由 |
| --- | --- | --- |
| EventIdentity + PlaybackHash | **推荐** | PlaybackHash 覆盖 root script + 全部 fragments + translations，能区分「root 相同但 fork 内容/翻译不同的变体」，是完整可回放变体的最稳 identity |
| EventIdentity + RootDefinitionHash | 不用于 variant identity | RootDefinitionHash 只含 raw key + root script，不含 fragments/translations，无法区分 fragment-only 或 translation-only 变体 |
| EventIdentity + RootScriptHash | 不用于 variant identity | 只含 root script，连 raw key 都不含 |
| composite | 见下 | 使用 EventIdentity + PlaybackHash 作为天然 composite |

结论：`ObservedVariant` 唯一 identity = `EventIdentity + PlaybackHash`。这是「完整可回放 variant」的 identity。`PlaybackHash` 用于 historical replay identity（§9）。

### 4.2 LocationName 是否属于 variant identity（回答 5B）

默认：**不属于 variant identity**。EventIdentity 已含 AssetName + EventId；LocationName 只是观察/本次启动的地点元数据。同一 variant 可能在不同地点被观察到（跨地点片段），LocationName 不应进 variant identity。它属于 `VariantObservation` / `HistoricalEventRecord` 的 launch / observation metadata。

### 4.3 RawEventKey 是否属于 variant identity（回答 5C）

默认：**不属于 EventIdentity**。但 `RootDefinitionHash` 会反映 raw key + root script。对 ObservedVariant，`RawEventKey` 仍作为定义的一部分保存（供调试 / 与 Current 对齐），但不作为 identity 组成；identity 用 `EventIdentity + PlaybackHash`。

### 4.4 推荐模型

```csharp
internal sealed record ObservedVariant(
    EventIdentity Identity,
    string LocationName,            // 首次观察地点（launch/observation metadata，非 identity）
    string RawEventKey,             // 定义一部分（非 identity）
    string RootDefinitionHash,      // raw key + root script
    string RootScriptHash,          // root script
    HistoricalPlaybackBundle Playback  // 冻结播放内容
)
{
    internal string PlaybackHash => Playback.PlaybackHash;
}
```

ObservedVariant 暴露 `PlaybackHash` 作为 variant 内容 identity。`LocationName` 只作 metadata 保留（首次观察地点），后续 observation 的 location 由 `VariantObservation` / `HistoricalEventRecord` 记录。

---

## 5. VariantObservation 决策（回答 8）

方案 A：`ObservedVariant` + `HistoricalEventRecord`，每条 natural HistoricalEventRecord 本身就是一次 observation。

方案 B：`ObservedVariant` + `VariantObservation` + `HistoricalEventRecord` 三层。VariantObservation 用于「runtime 观察到了这个 version」（可能未完整观看）；HistoricalEventRecord 只用于「玩家完整自然经历了一次」。

推荐：**Phase 4 采用类似方案 A 的过渡——把「观察」从「完整历史记录」中先拆出来，但暂不强制建独立 VariantObservation 表；用 minimal 的观察元数据表达**。理由：

1. 当前 capture 机制（pendingSnapshot + CommitPending 需要 eventsSeen）只能确认「完整观看」，无法在 Phase 4 不加 Replay 重构的情况下可靠地区分「观察到了 variant」与「完整看完」。
2. CP1 passive observed variants（未来）需要一个与 HistoricalEventRecord 正交的「观察」通道，但 CP1 尚未到 Phase。Phase 4 只需在领域语义上预留：ObservedVariant 代表「版本存在过」，HistoryEventRecord 代表「完整自然经历过一次」。

综合：**推荐在 Phase 4 把 ObservedVariant 与 HistoricalEventRecord 拆开**；`VariantObservation` 是否单独成模型推迟到 CP1（Phase 9）引入 passive observation 时再定。若 CP1 需要，届时可在 ObservedVariant 上附一条「observed-at aggregate」，或单独建 VariantObservation。Phase 4 不建第三层，以避免过度设计且不触碰 Replay/Preview。

（注：任务书允许「不照抄」，此处选择方案 A 的变体，理由见 §7 分析；如 Codex 坚持方案 B，见 §22 决策点。）

---

## 6. HistoricalEventRecord 模型

### 6.1 定位（回答 7）

代表「玩家自然游戏中真正完整经历了一次事件」。最低字段：

```csharp
internal sealed record HistoricalEventRecord(
    EventIdentity Identity,
    string PlaybackHash,          // 指向 ObservedVariant 内容
    DateTimeOffset WatchedAt,     // 本次完整经历的时间
    string? LocationName,          // launch metadata（可空）
    string? SaveProfileId          // 未来 SQLite 存 profile；Phase 4 仅占位/不持久化
)
```

决策：

- `EventIdentity`：必含。
- `variant reference / PlaybackHash`：必含，指回 `ObservedVariant`（通过 PlaybackHash）。
- `WatchedAt`：本次经历时间。
- `LocationName?`：launch metadata，可空。
- `SaveProfileId?`：Phase 4 不持久化 / 不生成；仅作为未来 SQLite（Phase 5）的领域占位概念，Phase 4 不实现。
- `RawEventKey`：**不直接嵌**——由 ObservedVariant（`Identity + PlaybackHash`）提供；record 只存 `PlaybackHash` 引用，避免重复。
- `Playback bundle`：**不直接嵌**——通过 `PlaybackHash` 引用 `ObservedVariant.Playback`，保持 `HistoricalPlaybackBundle` 作为独立可复用对象（§8）。

### 6.2 一次事件看两次 → 1 ObservedVariant + 2 HistoricalEventRecord

这是 Phase 4 的核心语义修正。当前 `WatchedEventHistory.Add` 对同 `Fingerprint` 只更新 `LastWatchedAt`（§7 会论证其是否足以产生 natural instance）。正确的领域语义：同 `PlaybackHash` 的自然观看应产生 **1 个 ObservedVariant**（去重），但**每次完整观看都产生 1 条 HistoricalEventRecord**（`WatchedAt` 各自记录），而不是覆盖 `LastWatchedAt`。

### 6.3 当前 capture 机制能否可靠产生一次 natural instance

审计 `TryCapture` + `CommitPending`「是否能准确产生一次 natural instance」：

- 能识别：一次事件在 `eventsSeen` 被点亮后 commit——代表玩家至少**看过**一次该 EventId。
- 不能区分：skipped event（`skippable` 事件被跳过也算看到？当前 `CommitPending` 只判 `eventsSeen.Contains(EventId)`，跳过事件不点亮 eventsSeen，故不会 commit，安全）。
- `event aborted`：abort 不点亮 eventsSeen → 不 commit。
- `event transition / secondary / fork / switchEvent`：当前 `Update` 对 `Game1.CurrentEvent` 引用变化时，会先 `CommitPending` 再重新 capture 新事件。其中 `ReferenceEquals(current, observedEvent)` 防重复；但 fork/switchEvent 后 CurrentEvent 可能变化 → 可能产生多个 pendingSnapshot / 多次 commit。需要审计是否会把「同一次自然经历」误拆成多条。这是 Phase 4 必须明确的不确定点（见 §17 failure semantics）。
- `game exit mid-event`：不 commit（eventsSeen 未点亮 / pendingSnapshot 未落盘）。
- `save reload`：`Load()` 重读现有记录；`Clear` 在 reload 前重置。不会重复 count。
- `same event naturally repeated`（第二次完整看同一 variant）：当前会走「同 Fingerprint 只更新 LastWatchedAt」→ 合并为一条；语义上应产生第二条 HistoricalEventRecord。这是当前模型与目标模型的核心差异。
- `mods that manipulate eventsSeen`：`CommitPending` 判定 `eventsSeen.Contains(EventId)`。若 mod 提前塞入 eventsSeen，可能误判「完整看过」（见 §17 KnownSeen 独立）。

结论：当前机制足以「去重出 ObservedVariant」，但**不足以可靠地在不改 Replay/不引入观察状态机的情况下区分每次 natural instance**。Phase 4 通过**领域模型拆分 + 旧数据 adapter** 确立边界，instance 级精确记录留待后续（可能随 Replay 边界重构 / Phase 5 SQLite 一起处理）。任务书明确「不要扩大到重新设计 Replay」。

---

## 7. VariantObservation 是否需要单独一层（完整分析）

- 现状维度：`ObservedVariant`（内容版本去重）+ `HistoricalEventRecord`（完整自然经历实例）+ `KnownSeen`（eventsSeen 证据）。三者已覆盖 Phase 4 可观察的主力。
- 未来 `CP1 passive observed variants`：会观察到「variant 存在」但玩家未完整观看。这需要「观察」与「完整历史记录」分开。但 CP1 尚未到 Phase。

推荐：**Phase 4 建 ObservedVariant + HistoricalEventRecord 两层；不建第三层 VariantObservation**。理由：

1. 当前唯一确定的观察源是 natural capture（且仅在完整看完后 commit），观察与完整观看在当前机制下几乎重叠，拆三层暂无收益。
2. `VariantObservation` 的价值在 CP1 passive discovery（观察 ≠ 完整观看）。届时再引入，可基于已有的 ObservedVariant（含 aggregate observed metadata）。
3. 避免 Phase 4 过度设计；不为尚未存在的 CP1 通道提前建模。

因此 Phase 4 的 `ObservedVariant` 可含 `FirstObservedAt / LastObservedAt` 作为「观察聚合」元数据（对应 legacy `FirstWatchedAt / LastWatchedAt` 的语义迁移），而 `HistoricalEventRecord` 才是 instance 级。这样方案 A 也能表达观察性，不需第三张表。

---

## 8. HistoricalPlaybackBundle 边界

当前 `HistoricalPlaybackBundle` 已包含：RootScript、EventAssets、Translations、Locale、PlaybackHash。

审计结论：

- 它已经是「冻结内容、用于精确历史回放的 payload」。**保留为独立对象，不要重新塞回 ObservedVariant 的一堆平铺字段**。ObservedVariant 持有 `HistoricalPlaybackBundle Playback`，而非复制字段。
- **collection 防 mutation**：当前 `EventAssets` / `Translations` 是 `Dictionary`，`HistoricalPlaybackBundle` 暴露为只读接口但底层仍是原 Dictionary 引用。建议 Phase 4 在 adapter/领域边界做防御性复制（`new Dictionary(...)` 或只读快照），或在 record 定义处保证传入后不被改。任务书倾向「immutable-ish」——达成方式：ObservedVariant / bundle 构造时复制 collections，避免外部持有引用后 mutation。
- **PlaybackHash 是否完整覆盖 bundle**：`[CHAIN]` `GetSnapshotFingerprint(rootScript, eventAssets, translations)` 遍历 rootScript + 所有 eventAssets（按 asset.key 排序）+ 所有 translations（按 key 排序）。它**未包含**：Locale、LocationName、EventKey、EventId、AssetName。因此 PlaybackHash 覆盖「root script + fragments + translations」的播放内容，但**不含 locale**。两段同内容但不同 locale 的 bundle 会得出相同 PlaybackHash——但 locale 影响实际播放文本。这是已知局限：PlaybackHash 是「内容指纹」而非「含语言的完整 replay identity」。若要求含语言，需把 locale 纳入 hash（改变旧 Fingerprint 语义 / 破坏兼容）。Phase 4 保持现状，但文档标注此语义缺口（§22 决策点）。
- **LocationName / EventKey 是否进入 bundle**：默认**不进入**。bundle 是播放内容；LocationName/EventKey 是 launch / definition metadata，属于 ObservedVariant（RawEventKey）与 HistoricalEventRecord / launch（LocationName）。把它们从 bundle 中剥离，避免 playback content 与 launch metadata 混淆。

---

## 9. Hash / Identity 矩阵

已核实三个 hash 的定义 `[CHAIN]`：

| | 定义 | 输入 |
| --- | --- | --- |
| RootScriptHash | `EventHashes.RootScript(script)` | RootScript |
| RootDefinitionHash | `EventHashes.RootDefinition(rawKey, rootScript)` | rawKey + '\0' + rootScript |
| PlaybackHash | `EventKey.GetSnapshotFingerprint(rootScript, eventAssets, translations)` | rootScript + ordered eventAssets + ordered translations |

用途矩阵：

| 用途 | RootScriptHash | RootDefinitionHash | PlaybackHash |
| --- | --- | --- | --- |
| 用于 EventIdentity？ | NO | NO | NO |
| exact root definition comparison？ | 部分（root script） | YES | 部分 |
| root script content comparison？ | YES | YES（含 root） | 部分 |
| exact historical playback variant？ | NO | NO | **YES（推荐）** |
| display fingerprint（12 字符日志）？ | 可 | 可 | 可 |
| persistence dedup？ | NO | NO | YES（现状 `Fingerprint` 即 PlaybackHash） |
| UI version count？ | 用 PlaybackHash 聚合 | — | 用 PlaybackHash 去重计数 |

结论：

- `EventIdentity`：只用时 `AssetName + EventId`。三个 hash 均不进入 EventIdentity。
- `RootDefinitionHash`：用于「raw key + root script 的定义级比较」。
- `RootScriptHash`：用于「root script 内容比较」。
- `PlaybackHash`：用于「完整可回放 variant 的 identity / dedup」。推荐作为 ObservedVariant 唯一内容 identity。
- **避免把 `Fingerprint` 旧名称带进新 domain**。新领域统一用 `PlaybackHash`（64 字符全量）；12 字符短值只用于日志/展示。

---

## 10. 旧存档持久化兼容（迁移原则）

现状 `[REPO]`：

- save key：`watched-event-versions`
- 格式：gzip + base64 + JSON `List<WatchedEventSnapshot>`
- Phase 1 承诺旧 11-field schema 兼容（Checks 已锁定）。

迁移原则：

- 旧 `WatchedEventSnapshot` 可靠转换为：`ObservedVariant`（含 FirstWatchedAt/LastWatchedAt 作为 variant 观察聚合）+ 一个「已知该 variant 至少被完整看过一次」的 KnownSeen 证据。
- **不能**把旧 `FirstWatchedAt/LastWatchedAt` 凭空转换成多个 `HistoricalEventRecord`。中间可能看过多次，也可能从未在 A/B 各看一次——旧 Add 行为只表达「同 variant 聚合」。因此旧数据**不产生具体 instance 记录**，只产生「该 variant 存在 + 观察聚合时间」+「该 EventId 至少被完整看过（KnownSeen）」。
- Phase 4 尽量不改现有 save bytes / schema：旧 schema（WatchedEventSnapshot）继续可读可写；领域层把 `WatchedEventSnapshot` **降级为 legacy persistence DTO / adapter**，新增 domain model，由 adapter 转换。

方案（推荐）：

- **保留 legacy DTO 作为 persistence adapter**：`WatchedEventSnapshot` 不再作为领域核心概念，改名为 legacy DTO 或标记，用于读写旧 save key。
- **新建 domain model**（ObservedVariant / HistoricalEventRecord / KnownSeenEvidence），由 adapter 从 `WatchedEventSnapshot` 转换。
- **Phase 4 是否继续写旧 schema**：建议 Phase 4 继续写 `watched-event-versions`（保持兼容与回放/UI 工作），领域拆分只发生在内存/adapter 层。真正改变持久化（versioned JSON 或迁移到新 key）推迟到 Phase 5 SQLite 一起决策。
- **是否引入 versioned JSON**：不引入，除非明确要改持久化；本阶段不改 save bytes。
- **是否等 Phase 5 再改持久化**：是。Phase 4 尽量不动 `watched-event-versions` 的写入格式。

推荐落地：`WatchedEventSnapshot` 保留为 legacy DTO；新增 `ObservedVariant` / `HistoricalEventRecord` / `KnownSeenEvidence` 领域模型；提供 `HistoryAdapter.FromLegacy(WatchedEventSnapshot) -> ObservedVariant + KnownSeenEvidence`（不产生 HistoricalEventRecord）。

---

## 11. Legacy Adapter 方案

任务书 §19 问 `WatchedEventSnapshot` 最终定位。推荐：

- `WatchedEventSnapshot` 降级为 **legacy persistence DTO**（名称可保留，标注 @Obsolete 或重命名为 `LegacyWatchedEventSnapshot`——但为避免改动现有 JSON 字段名与 Checks，Phase 4 若保留原名则用备注说明其为 legacy DTO）。
- 它不再作为领域核心概念；领域层使用 `ObservedVariant` / `HistoricalEventRecord` / `KnownSeenEvidence`。
- Adapter：`LegacyHistoryAdapter.ToObservedVariant(snapshot)`（含 Playback bundle）、`ToKnownSeen(snapshot)`。
- `HistoricalPlaybackBundle`: **保留**（复用），作为 ObservedVariant 的播放内容成员。
- `WatchedEventHistory`：逐步拆职责——`Load/Save/Get`（persistence + 读取）保留；`TryCapture/CommitPending/Add` 迁移到新 service（variant 去重 → ObservedVariant；instance 记录 → HistoricalEventRecord）；replay 排除与 eventsSeen 判断保持。

---

## 12. Natural capture 语义

目标：natural gameplay 才能产生 `HistoricalEventRecord`。当前 capture 已隐含：

- `CommitPending` 需 `eventsSeen.Contains(EventId)` 才 commit。
- `replayActive` 时 `Update` 清空并 return（回放排除）。
- `skippable` 被跳过不点亮 eventsSeen → 不 commit。

Phase 4 需固定的 natural capture 边界：

- 只有 natural gameplay（非 replay / 非 preview）能产生 `HistoricalEventRecord`。
- `ObservedVariant`（版本去重）可在 capture 成功时产生（即使该次是否是「完整观看」有不确定性）。
- `KnownSeen` 独立于 instance 记录。

---

## 13. Replay / Preview 排除边界

固定原则 `[REPO]`：

- Natural gameplay：可产生 HistoricalEventRecord。
- Current replay：不能产生 HistoricalEventRecord（`Update(replayActive=true)` 清空并 return；`TryStart` 走 `Game1.PlayEvent(..., checkPreconditions:false, checkSeen:false)`，不经过 natural commit）。
- Historical replay：不能产生（走 `StartHistoricalEvent` + `HistoricalReplayAssets` 注入，replayActive=true）。
- 未来 Preview：不能产生（Phase 4 仅定义原则，不实现）。
- CP passive observation：可产生 ObservedVariant / VariantObservation，但不能产生 HistoricalEventRecord。

设计避免混淆：capture 入口需要明确一个 "source" 信号（natural / replay / preview / passive）。Phase 4 不引入新枚举实现，但文档固定该语义；`Update(bool replayActive)` 已有 replay 信号——Preview / CP 未来复用「非 natural 即排除 HistoricalEventRecord」的原则。Phase 7 PreviewState / Phase 9 CP1 再实现具体注入。

---

## 14. UI Compatibility

审计 `GalleryCharacterMenu`（`[REPO]`）：

- `watchedVersions(entry)` 返回 `List<WatchedEventSnapshot>`，按 `LastWatchedAt` 降序。
- UI「历史版本」按钮循环选择 `versions[selected]`，默认显示 newest（当前版本用 null 表示 current）。
- `event.version-watched`（selected+1/total）、`event.version-current`（Current）。
- 计数 `seen events` 用 `Game1.player.eventsSeen.Contains(EventId)`，与 history 列表无关。

判断：

- 目前 UI 表达的是 **Observed versions / variant list**（同 identity 的多个 PlaybackHash 变体），**不是** chronological instance timeline。
- `timestamp ordering` 实际按 `LastWatchedAt`（variant 聚合时间）排序，非每次观看时间。

Phase 4 默认不大改 UI。领域模型不再把 variant list 称为「historical instances」。兼容策略：

- 为了现有 Gallery 继续工作，UI 仍接收一个「版本列表」；该列表的底层改为 ObservedVariant 集合（按 `PlaybackHash` 去重，排序可用 `LastWatchedAt` 或引入的 `FirstObservedAt`）。
- 未来才引入「history timeline」视图（HistoricalEventRecord 时间线）。
- Phase 4 仅确立语义边界 + adapter，不重写 UI。

---

## 15. Future CP1 Boundary

未来 CP1（passive observed variants）可能产生：

- ObservedVariant（被观察到但玩家未完整观看）。
- VariantObservation（观察记录）。
- 但**不能**产生 HistoricalEventRecord。

Phase 4 只预留语义：`ObservedVariant` 与 `HistoricalEventRecord` 分离；`CP1` 的 observation 通道未来可在 ObservedVariant 上附「observed aggregate」或单独 VariantObservation，不强制此时建模型。

---

## 16. Future SQLite Requirements（仅记录需求，不实现）

Phase 5 SQLite 未来需保存的概念（Phase 4 只回答「需要保存哪些概念」，不建 schema / 不引入 package）：

- save profile（区分不同存档 / 玩家）。
- event identity（AssetName + EventId）。
- observed variant（内容版本，PlaybackHash 去重）。
- variant observation（观察记录，可选，取决于 CP1）。
- historical event record（完整自然经历实例）。

Phase 4 不创建数据库、不写 migration SQL、不引入 SQLite、不把 domain record 设计成数据库 row（record 保持领域对象，未来才有 persistence mapping）。

---

## 17. Failure Semantics

| 情况 | 结果（Phase 4 目标语义） |
| --- | --- |
| capture root definition 找不到（commands 与 asset 候选不一致） | 不产生 ObservedVariant / HistoricalEventRecord；仅 log。可能仍属 KnownSeen（若 eventsSeen 已点亮） |
| commands 与 current event 不匹配 | 不 capture（TryCapture false），不产生 variant |
| fragment 缺失 | TryCapture false，不产生 variant |
| translation 缺失 | TryCapture false，不产生 variant |
| history save 无法读取 | canSave=false，不覆盖原记录；不再写入（保持不破坏） |
| history save 无法写入 | 同 Load 失败路径，尽力而为；DB error 不属本阶段 |
| event 中途退出 | 不 commit（eventsSeen 未点亮 / pending 未落盘），不产生 HistoricalEventRecord |
| event seen flag 未设置 | CommitPending 跳过，不产生 HistoricalEventRecord；ObservedVariant 是否产生取决于 capture 时机 |
| duplicate variant（同 PlaybackHash） | 1 个 ObservedVariant（去重），不重复 |
| duplicate natural occurrence（同 variant 多次完整看） | 产生多条 HistoricalEventRecord（Phase 4 语义）；当前机制可能只更新 LastWatchedAt，见 §6.2 / §7 |

明确：ObservedVariant 与 HistoricalEventRecord 的产生条件不同；KnownSeen 独立；某些失败只记录 KnownSeen 或什么都不记。

---

## 18. Automated Checks 计划（Phase 4）

保持 BCL-only，不引入 SQLite / test framework / game runtime。至少覆盖：

- legacy `WatchedEventSnapshot` → domain adapter → ObservedVariant。
- identity 语义（AssetName 规范化 / EventId case）。
- PlaybackHash variant identity。
- same EventIdentity + same PlaybackHash → same ObservedVariant。
- same EventIdentity + different PlaybackHash → different ObservedVariant。
- same root script + different fragment/translation bundle → different PlaybackHash variant。
- same PlaybackHash repeated natural watches → one Variant + multiple HistoricalEventRecord（领域逻辑，用构造的 records 断言）。
- old FirstWatchedAt/LastWatchedAt → 不伪造多个 historical instances（adapter 断言只产 KnownSeen + ObservedVariant）。
- KnownSeen → 不自动变 HistoricalEventRecord。
- Replay/Preview observation flags → 不进入 natural history（用 source 信号断言；Phase 4 若未实现 flag，则用 `Update(replayActive:true)` 空状态断言）。
- old 11-field JSON round-trip compatibility（保留 Phase 1 Checks）。
- 保留全部现有 Phase 1/2/3 Checks。

（注：`HistoricalPlaybackBundle.From(WatchedEventSnapshot)` 已存在，Checks 已覆盖 legacy→bundle。Phase 4 增 adapter 层测试。）

---

## 19. File-level Implementation Plan

Phase 4 建议新增：

- `Domain/ObservedVariant.cs`
- `Domain/HistoricalEventRecord.cs`
- `Domain/KnownSeenEvidence.cs`
- `History/LegacyHistoryAdapter.cs`（WatchedEventSnapshot → ObservedVariant / KnownSeenEvidence）
- `History/...` 相关 service（variant 去重 / instance 记录拆分）

`WatchedEventSnapshot` 最终定位：**legacy persistence DTO / adapter**（保留 JSON 字段名；领域层不再视为核心概念）。

`HistoricalPlaybackBundle`：**保留**，作为 ObservedVariant 播放内容成员，不复用平铺字段。

`WatchedEventHistory`：逐步拆职责——persistence 读取保留；capture/dedup/instance 拆分到新 service / domain。

**不要为了目录整洁移动现有大量文件。** 只新增少量 domain + adapter 文件，最小化 diff。Phase 4 不实现（仅计划）。

---

## 20. Compatibility Risks

- old save data（watched-event-versions）：必须可读；Phase 4 不改 schema。
- historical replay：继续用 legacy WatchedEventSnapshot adapter（Phase 4 不重构 ReplayCoordinator）。
- Gallery version counts：改成 ObservedVariant 集合后需保证计数一致（按 PlaybackHash 去重）。
- version sorting：从 LastWatchedAt 排序（现状）迁移到 ObservedVariant 观察时间排序，需保持 UI 顺序。
- natural capture：capture 到 ObservedVariant 去重，避免重复。
- eventsSeen：作为 KnownSeen 证据，不冒充历史记录。
- modded event IDs：EventIdentity 保留 mod 前缀；PlaybackHash 去重兼容。
- same identity multiple variants：ObservedVariant 按 PlaybackHash 区分。
- locale changes：PlaybackHash 不含 locale，同内容不同语言的 bundle hash 相同（§8 语义缺口；影响「语言变体」区分，文档标注）。
- fragment changes / translation changes：PlaybackHash 会变化（新 variant）。
- save reload：Load 重新读取；ObservedVariant 去重不重复。
- replay exclusion：Update(replayActive) 排除。
- future CP1：ObservedVariant / HistoricalEventRecord 分离，为 passive observed variants 预留。
- future SQLite：Phase 4 不落盘 schema；domain 概念已分离。

---

## 21. Phase 4 Out-of-scope

明确禁止：

- SQLite。
- PreviewState / PreviewPlan / StateInjector。
- ReplayCoordinator 大重构。
- unified EventLauncher。
- CP passive discovery implementation。
- ConditionIR UI integration。
- Gallery UI 大改版。
- planner / solver / route planning。
- manifest / version / config / release changes。
- Phase 5 及之后。

---

## 22. Unresolved Codex Decisions

1. **ObservedVariant identity**：确认用 `EventIdentity + PlaybackHash`，而非其它 hash 组合。推荐确认。
2. **是否建第三层 VariantObservation**：本设计推荐 Phase 4 不建（方案 A 变体），把观察聚合并入 ObservedVariant（FirstObservedAt/LastObservedAt），到 CP1 再决定。是否接受？
3. **PlaybackHash 是否纳入 locale**：当前不含，同内容不同语言 hash 相同。是否 Phase 4 保持现状（推荐）还是未来改变 hash 语义（会破坏旧 Fingerprint 兼容）？
4. **HistoricalEventRecord 是否嵌 RawEventKey / Playback bundle**：本设计推荐只存 `PlaybackHash` 引用 + `EventIdentity`，不嵌（由 ObservedVariant 提供）。是否确认？
5. **SaveProfileId**：Phase 4 是否作为领域占位概念（不持久化），还是完全推迟到 Phase 5 SQLite？推荐作为占位，但不能进入任何序列化。
6. **WatchedEventSnapshot 命名**：Phase 4 是否保留原名（作 legacy DTO 备注），还是重命名为 `LegacyWatchedEventSnapshot`（不改 JSON，只改类型名）？推荐保留原名避免大 diff，仅备注为 legacy DTO。
7. **natural instance 精确记录**：是否接受「Phase 4 确立 ObservedVariant + HistoricalEventRecord 语义边界，但 instance 级精确记录（区分每次自然观看）推迟到后续 / Phase 5」？推荐接受（不重做 Replay）。
8. **Current 与 Observed variant 关系**：确认 Current ResolvedEvent（当前管线）与 ObservedVariant（曾观察的版本）分离，Phase 4 不合并 Index 与 History。推荐确认。

---

## 23. 结论

Phase 4 的核心是把连在一体的 `WatchedEventSnapshot` 拆成三个正交概念：`ObservedVariant`（内容版本，PlaybackHash 去重）、`HistoricalEventRecord`（自然完整经历实例）、`KnownSeenEvidence`（eventsSeen 证据）。`HistoricalPlaybackBundle` 保留为独立播放内容对象；旧 `WatchedEventSnapshot` 降级为 legacy persistence DTO / adapter；Phase 4 不改 save schema、不重构 Replay、不实现 SQLite / Preview / CP1。UI 继续保持 variant-list 语义，领域层不再把 variant list 当成 historical instances。
