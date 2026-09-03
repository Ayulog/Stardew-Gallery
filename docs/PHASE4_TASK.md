# Stardew Gallery Phase 4：History / Variant Semantic Split 实施任务书

日期：2026-09-03

## 0. 基线与性质

- 工作分支：`phase4/history-variant-semantics`
- 审计依据：`docs/PHASE4_ANALYSIS.md`
- 实施基线：`3c0d9cb9c9c5e6f2d0709d69486d1d098df510d7`（Phase 4 analysis commit）
- 本任务书以 Codex 最终决议覆盖 analysis 的 §22 开放题；实施时不再逐项询问。
- 不引入 SQLite，不开始 Phase 5。

## 1. 永久领域不变量

```text
ObservedVariant     != HistoricalEventRecord
Current ResolvedEvent != ObservedVariant
KnownSeenEvidence   != HistoricalEventRecord
Replay / Preview    != Natural History
```

正式区分：`EventIdentity`、`Current ResolvedEvent`、`ObservedVariant`、`VariantObservationSummary`、`HistoricalEventRecord`、`KnownSeenEvidence`、`HistoricalPlaybackBundle`、legacy `WatchedEventSnapshot`。

## 2. ObservedVariant identity 决议

采用 `ObservedVariantKey(Identity, RootDefinitionHash, PlaybackHash)`，不采用 `EventIdentity + PlaybackHash`。

理由：

- `RootDefinitionHash = RawEventKey + RootScript`。
- `PlaybackHash = RootScript + fragments + translations`。
- 条件改变但播放内容相同 → RootDefinitionHash 不同 → 必须不同 ObservedVariant。
- fragment / translation 改变 → PlaybackHash 不同 → 必须不同 ObservedVariant。
- `EventIdentity` 只表示逻辑事件本身。
- `RootScriptHash` 不进入 ObservedVariantKey，只作为 comparison / diagnostics metadata。

新增：

```csharp
internal readonly record struct ObservedVariantKey(
    EventIdentity Identity,
    string RootDefinitionHash,
    string PlaybackHash
);

internal sealed record ObservedVariant(
    ObservedVariantKey Key,
    string RawEventKey,
    string RootScriptHash,
    HistoricalPlaybackBundle Playback
);
```

ObservedVariant 不包含 FirstObservedAt / LastObservedAt / LocationName / SaveProfileId——这些不是内容定义。

## 3. VariantObservationSummary

第三层「观察聚合」，不建每次 observation instance。它表示「当前 legacy 数据能证明的、某个 variant 的观察时间范围」，而非「观察了两次」。

```csharp
internal sealed record VariantObservationSummary(
    ObservedVariantKey Variant,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastObservedAt,
    string? LastObservedLocationName,
    string? LastObservedLocale
);
```

映射：FirstWatchedAt→FirstObservedAt；LastWatchedAt→LastObservedAt；LocationName→LastObservedLocationName；Locale→LastObservedLocale。禁止把 First+Last 转成两个 observation；禁止声称观察次数。未来 CP1 需要每次 passive observation 时再新增 instance-level VariantObservationRecord；Phase 4 不实现。

## 4. HistoricalEventRecord

表示「玩家自然游戏中真正完整经历的一次事件实例」。

```csharp
internal sealed record HistoricalEventRecord(
    ObservedVariantKey Variant,
    DateTimeOffset WatchedAt,
    string? LocationName,
    string? Locale
);
```

不嵌 RawEventKey、不嵌 HistoricalPlaybackBundle、不只引 PlaybackHash、不加 SaveProfileId（推迟 Phase 5）。`ObservedVariantKey` 已含 EventIdentity，故不复制 EventIdentity 字段（可加 computed property 供 ergonomics）。

## 5. 不伪造 HistoricalEventRecord

legacy `WatchedEventSnapshot` 不能转换为 `HistoricalEventRecord`。旧数据只能可靠转换为 ObservedVariant + VariantObservationSummary + KnownSeenEvidence，HistoricalEventRecord = 0 条。

原因：First/LastWatchedAt 是 variant 聚合边界，不是两次 occurrence。Phase 4 建立 HistoricalEventRecord domain type，但不声称现有 legacy 数据具备 chronological history。本阶段不重写 natural event completion state-machine；instance-level 精确 capture 为后续明确工作。若 production 尚不能可靠证明一次 occurrence，宁可不生成，不得生成可能错误的历史实例。

## 6. KnownSeenEvidence

```csharp
internal enum KnownSeenSource
{
    SaveEventsSeen,
    LegacyCapturedVariant
}

internal sealed record KnownSeenEvidence(
    string EventId,
    EventIdentity? Identity,
    KnownSeenSource Source
);
```

- `Game1.player.eventsSeen` → 只能证明 EventId seen，Identity 可为 null。
- legacy `WatchedEventSnapshot` → 已捕获 AssetName+EventId，可生成具体 EventIdentity evidence。
- 任何 KnownSeenEvidence 都不能自动生成 HistoricalEventRecord。

## 7. HistoricalPlaybackBundle

保留。PlaybackHash 算法不变，不把 Locale 加入 PlaybackHash。

正式语义：PlaybackHash = captured playback content hash，覆盖 RootScript、EventAssets、Translations；不覆盖 Locale、LocationName、RawEventKey、EventIdentity。

不要再称 PlaybackHash 为「所有 bundle 字段的 exact identity」。`HistoricalPlaybackBundle.Locale` 暂时保留（兼容现有 DTO），语义为 capture/replay metadata，不进入 variant identity。

在 domain / adapter 构造边界做 defensive copy（EventAssets、Translations）。构造后外部修改 legacy Dictionary 不得改变已构造 domain variant 内容。不要因此修改 historical replay 行为。

## 8. WatchedEventSnapshot 定位

保留类型名 `WatchedEventSnapshot`，不 rename（避免大 diff）。但在 docs / code comment / adapter 中明确其为 legacy persistence DTO + compatibility projection，不是 Phase 4 核心 domain model。

保持：save key `watched-event-versions`，gzip+base64+JSON `List<WatchedEventSnapshot>`，11-field schema 完全不变。Phase 1 legacy JSON round-trip Checks 必须继续通过。

## 9. LegacyHistoryAdapter

新增 `History/LegacyHistoryAdapter.cs`，pure/BCL-friendly conversion：

```csharp
internal sealed record LegacyHistoryProjection(
    ObservedVariant Variant,
    VariantObservationSummary Observation,
    KnownSeenEvidence Seen
);

internal static LegacyHistoryProjection From(WatchedEventSnapshot snapshot);
```

`From` 计算 EventIdentity、RootScriptHash、RootDefinitionHash，把 `snapshot.Fingerprint` 作为 PlaybackHash。不得含 HistoricalEventRecord。不再把 Fingerprint 名称传播进新 domain——新 domain 一律叫 PlaybackHash。

## 10. Persistence dedup 不再丢 condition-only variant

当前 legacy Add 按 `EventIdentity + Fingerprint` 聚合，会导致 RawEventKey 改变、root/playback 不变时两个 definition variant 被合并且旧 EventKey 被覆盖。Phase 4 内部 variant dedup 使用 `ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash`。

- same Identity + same RootDefinitionHash + same PlaybackHash → same ObservedVariant
- same Identity + different RootDefinitionHash + same PlaybackHash → different
- same Identity + same RootDefinitionHash + different PlaybackHash → different

保持旧 JSON schema，不新增字段（RootDefinitionHash 可从 EventKey+RootScript 重算）。Save 允许 `List<WatchedEventSnapshot>` 中存在相同 Fingerprint 但不同 RootDefinitionHash 的两条；Load 不得再按 Fingerprint 合并。

## 11. UI / Replay compatibility

- 不修改 Gallery UI、不修改 ReplayCoordinator。
- 当前 Gallery「历史版本」UI 仍保持旧 playback-version 体验。
- domain / persistence：保留完整 ObservedVariantKey variants。
- 现有 watchedVersions / Gallery compatibility projection：可继续按 PlaybackHash collapse，避免出现「播放内容相同但 RawEventKey 不同」的重复版本按钮。
- collapse 规则：同 EventIdentity + PlaybackHash → 选择 LastObservedAt 最新的 compatibility snapshot；排序保持当前 LastWatchedAt 降序。
- 这是 UI compatibility projection，不是 domain dedup。未来 Variant Explorer UI 才消费完整 ObservedVariantKey。
- Historical replay 继续使用 WatchedEventSnapshot compatibility DTO，不改 ReplayCoordinator historical path。

## 12. Current 与 Observed 永久分离

`ResolvedEventIndex.Current` 表示当前 content pipeline 定义；`ObservedVariant` 表示曾被 capture 的 definition + playback variant。禁止把 ObservedVariant 塞进 ResolvedEventIndex、把 Current 自动当 ObservedVariant、把 History 与 Index 合并 cache。Phase 4 不修改 Phase 2 selection。

## 13. observation 与 natural history 产生原则

- Natural gameplay：可产生 ObservedVariant、可更新 VariantObservationSummary；未来有可靠 completion evidence 时可产生 HistoricalEventRecord。
- Current Replay / Historical Replay / Future Preview：不能产生 HistoricalEventRecord。
- Future CP passive observation：可产生 ObservedVariant / observation，不能产生 HistoricalEventRecord。
- 现有 `replayActive` exclusion 保持不变。本阶段不重构 Replay。

## 14. 文件范围

新增：`Domain/ObservedVariantKey.cs`、`Domain/ObservedVariant.cs`、`Domain/VariantObservationSummary.cs`、`Domain/HistoricalEventRecord.cs`、`Domain/KnownSeenEvidence.cs`、`History/LegacyHistoryAdapter.cs`。

允许修改：`Domain/HistoricalPlaybackBundle.cs`、`WatchedEventHistory.cs`、`Checks/Program.cs`、`Checks/StardewGallery.Checks.csproj`、`docs/PHASE4_TASK.md`、`docs/PHASE4_REPORT.md`。必要时可新增一个极小 History domain/service 文件，但不大规模移动现有文件。

禁止修改：ResolvedEventIndex candidate selection、EventAssetCatalog、GalleryCatalogBuilder ownership semantics、ReplayCoordinator、ReplaySnapshot、SaveGuard、GalleryCharacterMenu layout/behavior、GalleryMenu、i18n、manifest、config、version、release materials、ConditionIR、SQLite。

## 15. Automated Checks

保持 BCL-only。至少覆盖：

- Legacy adapter：Identity 正确、RootDefinitionHash 正确、RootScriptHash 正确、PlaybackHash == legacy Fingerprint、RawEventKey 保留。
- ObservedVariantKey：三种情况（same/varies by RootDefinitionHash/varies by PlaybackHash）的 equal/different。
- condition-only variant：same EventIdentity + same RootScript + same fragments/translations + different RawEventKey → PlaybackHash 相同、RootDefinitionHash 不同、ObservedVariantKey 不同（必须的 regression）。
- playback-only variant：same EventIdentity + same RawEventKey/root + different fragment/translation → RootDefinitionHash 相同、PlaybackHash 不同、ObservedVariantKey 不同。
- observation summary：First/LastWatchedAt → 一个 VariantObservationSummary，不产生两个 observations、不产生 HistoricalEventRecord；LocationName/Locale 解释为 latest legacy snapshot metadata。
- KnownSeen：legacy snapshot → concrete EventIdentity；save-style EventId-only → Identity null；KnownSeen 不生成 HistoricalEventRecord。
- defensive copy：构造 domain playback / ObservedVariant 后修改原 legacy EventAssets / Translations Dictionary，domain 内容不变。
- persistence compatibility：old 11-field JSON read + reserialize schema parity；同 PlaybackHash + 不同 RootDefinitionHash 可同时保存在 legacy list，load 后不被 domain dedup 合并。
- UI compatibility projection：同 PlaybackHash + 不同 RootDefinitionHash → domain 两个 ObservedVariant，但 legacy UI projection collapse 为一个 playback version（选 LastObservedAt 最新项）。
- Current separation：ResolvedEventIndex checks 原样；Phase 4 不影响 Current selection。
- 保留全部 Phase 1/2/3 Checks。

不引入 SQLite / test framework / game runtime dependency。

## 16. Phase 4 不做

不实现：SQLite、SaveProfileId persistence、new JSON schema、versioned JSON、exact chronological history persistence、natural completion state-machine rewrite、ReplayCoordinator refactor、unified EventLauncher、Preview、Planner、Solver、CP passive discovery、ConditionIR UI integration、Gallery 大改版、manifest/version/config/release changes、Phase 5+。

## 17. docs

创建 `docs/PHASE4_TASK.md`、`docs/PHASE4_REPORT.md`。

REPORT 必须明确区分：

已实现：ObservedVariant domain split、ObservedVariantKey、VariantObservationSummary、KnownSeenEvidence、legacy adapter、composite dedup、compatibility projection。

尚未实现：精确 chronological HistoricalEventRecord capture/persistence、SQLite、CP1、Preview、UI history timeline。

不得声称当前已有每次观看的精确历史实例。

## 18. 验证

运行 `dotnet build -c Release`、`dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`、`git diff --check`。要求：Release build 0 warnings/0 errors；Checks 通过（仅 NETSDK1138）；diff --check 无错误。随后 review diff、创建 focused commit、push，确认同步且工作树干净。
