# Stardew Gallery Phase 4：History / Variant Semantic Split 实施报告

日期：2026-09-03

## 1. 实施基线与 commit

- 工作分支：`phase4/history-variant-semantics`
- 审计基线：`3c0d9cb9c9c5e6f2d0709d69486d1d098df510d7`（Phase 4 analysis commit）
- 任务书：`docs/PHASE4_TASK.md`

## 2. 已实现

拆分当前连成一体的 `WatchedEventSnapshot` 为三个正交领域概念，并引入 composite variant dedup 与 UI compatibility projection。

### 新增文件

- `Domain/ObservedVariantKey.cs` —— `readonly record struct ObservedVariantKey(EventIdentity Identity, string RootDefinitionHash, string PlaybackHash)`。
- `Domain/ObservedVariant.cs` —— `ObservedVariant(Key, RawEventKey, RootScriptHash, Playback)`，不含 FirstObservedAt / LastObservedAt / LocationName / SaveProfileId。
- `Domain/VariantObservationSummary.cs` —— `VariantObservationSummary(Variant, FirstObservedAt, LastObservedAt, LastObservedLocationName, LastObservedLocale)`。
- `Domain/HistoricalEventRecord.cs` —— `HistoricalEventRecord(Variant, WatchedAt, LocationName, Locale)` + computed `Identity => Variant.Identity`。
- `Domain/KnownSeenEvidence.cs` —— `KnownSeenEvidence(EventId, Identity?, Source)`，`KnownSeenSource { SaveEventsSeen, LegacyCapturedVariant }`。
- `History/LegacyHistoryAdapter.cs` —— `LegacyHistoryAdapter.From(WatchedEventSnapshot)` 返回 `LegacyHistoryProjection(Variant, Observation, Seen)`；计算 Identity / RootScriptHash / RootDefinitionHash，把 `snapshot.Fingerprint` 作为 `PlaybackHash`；对 EventAssets / Translations 做 defensive copy。

### 修改文件

- `Domain/HistoricalPlaybackBundle.cs` —— 明确 `WatchedEventSnapshot` 为 legacy persistence DTO + compatibility projection；PlaybackHash 语义为 captured playback content hash（覆盖 RootScript / EventAssets / Translations，不覆盖 Locale / LocationName / RawEventKey / EventIdentity）。11-field JSON schema 不变。
- `WatchedEventHistory.cs` —— 内部存储改为 `EventIdentity → Dictionary<ObservedVariantKey, WatchedEventSnapshot>`；`Add` 用 composite key（Identity + RootDefinitionHash + PlaybackHash）去重，不再按 Fingerprint 合并 condition-only variant；`Load` 不按 Fingerprint 合并；`Save` 写入全部 variants；`Get`/compatibility projection`CollapseForCompatibility` 按 PlaybackHash collapse（选 LastWatchedAt 最新）、排序 LastWatchedAt 降序。
- `Checks/StardewGallery.Checks.csproj`、`Checks/Program.cs` —— 新增 Phase 4 BCL-only Checks。

## 3. 已建立的领域不变量

- ObservedVariant 不等于 HistoricalEventRecord。
- Current ResolvedEvent 不等于 ObservedVariant。
- KnownSeenEvidence 不等于 HistoricalEventRecord。
- Replay / Preview 不等于 Natural History。

## 4. Composite dedup 行为

内部 variant dedup 使用 `ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash`：

- 同 Identity + 同 RootDefinitionHash + 同 PlaybackHash → 同 ObservedVariant。
- 同 Identity + 不同 RootDefinitionHash + 同 PlaybackHash → 不同（condition-only variant 不再被合并）。
- 同 Identity + 同 RootDefinitionHash + 不同 PlaybackHash → 不同（playback-only variant）。

旧 JSON schema 不新增字段，RootDefinitionHash 由 EventKey + RootScript 重算。Save 允许 List 中存在同 Fingerprint 不同 RootDefinitionHash 的两条；Load 不再按 Fingerprint 合并。

## 5. UI / Replay compatibility projection

- 不修改 Gallery UI / ReplayCoordinator。
- domain / persistence 保留完整 ObservedVariantKey variants。
- 现有 watchedVersions / Gallery compatibility projection 按 PlaybackHash collapse，避免「播放内容相同但 RawEventKey 不同」的重复版本按钮；collapse 取 LastObservedAt 最新项，排序 LastWatchedAt 降序。
- Historical replay 继续使用 WatchedEventSnapshot compatibility DTO，未改 ReplayCoordinator historical path。

## 6. Legacy adapter 语义

`From(WatchedEventSnapshot)` 只产生 ObservedVariant + VariantObservationSummary + KnownSeenEvidence，**不产生 HistoricalEventRecord**。旧 FirstWatchedAt / LastWatchedAt 被解释为 variant 观察聚合边界（FirstObservedAt / LastObservedAt），不是两次 occurrence；LocationName / Locale 解释为最新 legacy snapshot 的 metadata。adapter 对 EventAssets / Translations 做 defensive copy，构造后修改原 Dictionary 不改变 domain variant 内容。

## 7. 验证结果

- `dotnet build -c Release`：成功，0 warnings，0 errors。
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`：`Stardew Gallery checks passed.`（仅既有 NETSDK1138）。
- `git diff --check`：无输出（干净；仅 LF→CRLF 提示，无 whitespace errors）。

Checks 覆盖：legacy adapter（Identity / hashes / PlaybackHash==Fingerprint / RawEventKey），ObservedVariantKey equality（same / diff RootDefinition / diff Playback），condition-only variant（same PlaybackHash diff RootDefinition diff Key），playback-only variant（same RootDefinition diff Playback diff Key），observation summary（single First/Last，不产生两个 observation、不产生 HistoricalEventRecord），KnownSeen（legacy → concrete Identity；save-style EventId-only → Identity null；不生成 HistoricalEventRecord），defensive copy，persistence compatibility（同 PlaybackHash 不同 RootDefinition 两条保留，load 不合并），Current separation（Index checks 原样）。保留全部 Phase 1/2/3 Checks。

## 8. 尚未实现（本阶段明确不声称）

- 精确 chronological HistoricalEventRecord capture / persistence：本阶段只建立 HistoricalEventRecord domain type 与不变量，不实现 natural completion state-machine；当前 production 不生成 HistoricalEventRecord 实例（宁缺毋滥，不伪造历史实例）。
- SQLite / SaveProfileId persistence / versioned JSON / new JSON schema。
- CP1 passive discovery（ObservedVariant / observation 通道，未来才建 instance-level VariantObservationRecord）。
- Preview / Planner / Solver。
- ConditionIR UI integration、Gallery history timeline UI。
- ReplayCoordinator refactor / unified EventLauncher。

不得声称当前已有每次观看的精确历史实例。

## 9. documented limitations

- 本阶段不产生 HistoricalEventRecord 实例；精确「每次自然观看」的 instance 记录是后续明确工作。
- `CollapseForCompatibility` 位于 `WatchedEventHistory.cs`（SMAPI 依赖），无法进入 BCL-only Checks；BCL Checks 只覆盖 domain side（两个 distinct ObservedVariants），collapse 行为经源码审查确认。
- `KnownSeenSource.SaveEventsSeen` 的 null-Identity 断言已加入 Checks。
- legacy FirstWatchedAt / LastWatchedAt 语义不清（可能看过多次），不作为多次历史实例。

## 10. Semantic correction（第二轮）

本轮修正 Phase 4 natural capture 对 condition-only variants 的 definition resolution。不引入 SQLite，不修改 UI / ReplayCoordinator / ResolvedEventIndex selection / ConditionIR。

### 问题

`WatchedEventHistory.TryCapture` 原先通过 `EventId 相同 + Event.ParseCommands(script).SequenceEqual(current.eventCommands)` 后 `FirstOrDefault` 恢复正在运行事件的 raw EventKey。当多个 definition 拥有 same EventIdentity、same root script/eventCommands、different RawEventKey 时（如 `123/Friendship Haley 1000` 与 `123/Friendship Haley 2000` 脚本相同），FirstOrDefault 可能错误记录第一个 EventKey，导致错误的 RootDefinitionHash，condition-only variant 仍被错误归类。

composite key 本身正确，问题只在 capture definition resolution。

### 修正

新增 `History/ObservedVariantSelector.cs`（BCL-only）：

```csharp
internal static bool TrySelect(
    IReadOnlyList<string> candidateRawKeys,
    Func<string, string?> checkPrecondition,
    out int selectedIndex);
```

语义：

- candidate count == 1 → 直接选 index 0。
- 多个 candidate → 按原始顺序调用 `checkPrecondition(rawKey)`，用与 Phase 2 characterization 一致的结果判定（null/""/"-1" false；其他非空 true），第一个满足者选中。
- 某 candidate 的 precondition evaluator 抛异常 → 该 candidate 视为 false，继续后续。
- 没有任何候选被确认 → selection failure（返回 false）。

`TryCapture` 改为两阶段 definition resolution：

1. 收集所有 `EventId == current.id && ParseCommands(script) == current.eventCommands` 的候选。
2. 0 个 → capture failure（保持现状）。
3. 1 个 → 直接使用该 definition。
4. 多个 → `ObservedVariantSelector.TrySelect(candidateRawKeys, key => location.checkEventPrecondition(key, check_seen: false), out idx)`；失败则 capture failure（reason + debug diagnostic），**不 fallback 到第一个**，不生成错误 ObservedVariant。

原则：宁可漏记一次 ambiguous natural capture，也不给 ObservedVariant 写入错误 RawEventKey / RootDefinitionHash。

### Checks

新增 BCL-only Checks 覆盖：single candidate（index 0）、two candidates first false second true（second）、first true second true（first）、first throws second true（second）、all false（failure）、empty（failure）、`null`/`""`/`"-1"` false、`"0"`/whitespace/other nonempty true、以及完整语义 fixture（candidate A `123/Friendship Haley 1000`、candidate B `123/Friendship Haley 2000`、A false、B true → 选 B，RootDefinitionHash == `EventHashes.RootDefinition("123/Friendship Haley 2000", "same")`，不等于 A 的）。

### 修正后声明

condition-only variant 在 capture 可辨识的情况下不会被 Fingerprint-only dedup 丢失（现在定义先经 current-state precondition disambiguation，再进入 composite ObservedVariantKey）。

### 保留限制

若运行时已无法根据当前 precondition 状态可靠确认 ambiguous raw definition，该次 capture 会安全跳过（capture failure），而不是猜测。先前 reserved 的 documented limitations 仍成立（不产生 HistoricalEventRecord 实例等）。
