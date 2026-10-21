# Stardew Gallery Phase 5：SQLite Persistence 实施任务书

日期：2026-09-03

## 0. 基线与性质

- 工作分支：`phase5/sqlite-persistence`
- 审计依据：`docs/PHASE5_ANALYSIS.md`
- 实施基线：`31a78ee664403038814e0b8f13c22f527eed9bd0`（Phase 5 analysis commit）
- 本任务书以 Codex 最终决议覆盖分析文档的 unresolved decisions。不开始 Phase 6。

## 1. 固定领域语义

```text
EventIdentity = normalized AssetName + case-sensitive EventId
ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash
ObservedVariant != HistoricalEventRecord
KnownSeenEvidence != HistoricalEventRecord
Current ResolvedEvent != ObservedVariant
Replay / Preview != Natural HistoricalEventRecord
```

SQLite 只保存历史领域数据，不保存 ResolvedEventIndex.Current。

## 2. Codex 覆盖分析文档的调整点（权威）

1. **SaveProfileKey = FarmUniqueId + PlayerUniqueId**（farm-only 不接受）。`FarmUniqueId = Game1.uniqueIDForThisGame`；`PlayerUniqueId = Game1.player.UniqueMultiplayerID`。Gallery 读当前 Game1.player 的 eventsSeen/friendship/natural capture，WatchedEventHistory.Update() 无 multiplayer guard，host / farmhand / split-screen 不应共享同一 profile history。SaveFolderName / display name 仅 metadata。schema `UNIQUE(farm_unique_id, player_unique_id)`。copied save 保相同 IDs 视为同一 lineage，不做 clone detection。
2. **AssetName equality = custom `ORDINAL_NOCASE` collation**（不用 ToUpperInvariant canonical folding）。用 `connection.CreateCollation("ORDINAL_NOCASE", (left,right) => StringComparer.OrdinalIgnoreCase.Compare(left,right))`；`asset_name` 存 `EventIdentity.AssetName`（slash+trim 后保留 casing），`COLLATE ORDINAL_NOCASE`；`event_id` BINARY case-sensitive。不新增 asset_name_raw。所有 connection 在 schema/query 前注册 collation。
3. **shared playback_json 不存 Locale**。只存 EventAssets + Translations；RootScript 单独存。Locale 只存 variant_observation_summaries.last_observed_locale 与 historical_event_records.locale。Repro materialization 时 Locale 来自 summary。不改 PlaybackHash。
4. **不主动 set synchronous=NORMAL**。默认 durability。只 `PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;`，不启 WAL。
5. **优先只显式引用 Microsoft.Data.Sqlite**，先验证其 transitive SQLitePCLRaw / e_sqlite3 runtime 是否被 ModBuildConfig 带入 Release output 与 mod zip；不一开始重复显式引用 bundle_e_sqlite3；仅当 packaging 证明缺失时最小补充。

## 3. 固定领域语义对照

```text
EventIdentity = normalized AssetName + case-sensitive EventId
ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash
ObservedVariant != HistoricalEventRecord
KnownSeenEvidence != HistoricalEventRecord
Current ResolvedEvent != ObservedVariant
Replay / Preview != Natural HistoricalEventRecord
```

## 4. SaveProfileKey

```csharp
internal readonly record struct SaveProfileKey(ulong FarmUniqueId, long PlayerUniqueId);
```

- FarmUniqueId = Game1.uniqueIDForThisGame
- PlayerUniqueId = Game1.player.UniqueMultiplayerID
- schema `UNIQUE(farm_unique_id, player_unique_id)`
- SaveFolderName / display name 只 metadata

## 5. DB 位置

`Constants.DataPath/StardewGallery/gallery.sqlite3`。单 DB + save_profiles，不做 per-save DB。

## 6. Source of truth

方案 C：SQLite primary persistence；legacy watched-event-versions = compatibility + bootstrap。继续维护 save key `watched-event-versions`、gzip+base64、JSON 11 fields。用途：downgrade compatibility / DB 丢失 bootstrap / 跨机器 bootstrap / historical replay compatibility / degraded fallback。不删除 legacy blob。

## 7. Provider

首选 Microsoft.Data.Sqlite。先只显式引用 meta package，验证 transitive SQLitePCLRaw / e_sqlite3 runtime 打包。检查最终 zip native runtime assets；不因 Windows build 成功就宣称跨平台完成。

## 8. Connection lifecycle

```
SaveLoaded → resolve SaveProfileKey → open/init DB session → upsert profile metadata → idempotent legacy import → 使用 repository
ReturnedToTitle → dispose repository/close connection → clear runtime state
```

一个 save-session connection；同步、小事务；无 background worker。

## 9. EventIdentity SQLite equality：custom collation

- `connection.CreateCollation("ORDINAL_NOCASE", (l,r) => StringComparer.OrdinalIgnoreCase.Compare(l,r))`
- `asset_name` 存 `EventIdentity.AssetName`（slash normalization + trim 后保留 casing），`COLLATE ORDINAL_NOCASE`
- `event_id` COLLATE BINARY case-sensitive
- 保证：`Data/Events/Town + abc == data/events/town + abc`；`Data/Events/Town + abc != Data/Events/Town + ABC`
- 不依赖 SQLite built-in NOCASE；不新增 asset_name_raw
- 所有 connection 在 schema/query 前注册 collation

## 10. Schema v1

`PRAGMA user_version = 1`。表：save_profiles、events、observed_variants、variant_observation_summaries、historical_event_records。字段见本任务书与 analysis §8。现在就创建 historical_event_records，但 production 与 legacy import 保持 0 rows。

## 11. ulong FarmUniqueId

SQLite INTEGER 是 signed 64-bit。bit-preserving round-trip：

```csharp
long stored = unchecked((long)farmUniqueId);
ulong restored = unchecked((ulong)stored);
```

参数化用 long；读回 unchecked cast。PersistenceChecks 覆盖 0 / long.MaxValue / long.MaxValue+1 / ulong.MaxValue。不使用 decimal TEXT。

## 12. playback_json

只存 EventAssets + Translations（PlaybackPayload DTO）。RootScript 单独 root_script 列。不存 Locale / LocationName / profile / timestamps。不改 PlaybackHash。

## 13. PlaybackPayload DTO

```csharp
internal sealed record PlaybackPayload(
    Dictionary<string, Dictionary<string, string>> EventAssets,
    Dictionary<string, string> Translations
);
```

不直接序列化整个 ObservedVariant / HistoricalPlaybackBundle。malformed JSON：skip row / 适当 degrade / log once / gameplay 不崩 / 不重写 DB key / 不加 64-char hex-only hard constraint。

## 14. Hash 语义

BINARY exact：root_definition_hash、playback_hash、root_script_hash、event_id。legacy fixture 允许 `abc`/`same-play`。raw_event_key 原样，不进 uniqueness。

## 15. Timestamp

UTC Unix milliseconds INTEGER。`ToUnixTimeMilliseconds()` / `FromUnixTimeMilliseconds()`。summary upsert：first=min、last=max；仅 incoming last >= existing last 时更新 last location/locale。旧 legacy import 不覆盖 newer DB metadata。

## 16. Pragmas

`PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;`，不启 WAL，不主动 synchronous=NORMAL。

## 17. Schema version / corrupt DB

open: register collation → open → pragmas → read user_version。0 → tx create v1 → user_version=1；1 → normal；>1 → disable SQLite / fallback legacy / 不 overwrite/downgrade/delete。Corrupt DB：log、fallback legacy、不 delete/truncate/rename/recreate over path。future migration 预留；v1 新库不需 backup manager。

## 18. Indexes

```
idx_observed_variants_event(observed_variants.event_fk)
idx_variant_summaries_profile_last(variant_observation_summaries.profile_fk, last_observed_at DESC)
idx_history_profile_watched(historical_event_records.profile_fk, watched_at DESC)
idx_history_variant(historical_event_records.variant_fk)
```

UNIQUE 自带 index，不重复。

## 19. 文件设计

新增 `Persistence/`：SaveProfileKey.cs、GalleryDatabase.cs、GallerySchema.cs、HistoryRepository.cs、LegacyHistoryStore.cs、PlaybackPayload.cs、HistoryPersistenceMapper.cs（可选）。禁止 ORM/EF Core/Dapper/generic repository framework。

## 20. GalleryDatabase

DB path/directory、connection、custom collation、pragmas、schema version/create、availability/degraded、dispose。DB path 构造函数注入，tests 用 temp file；production 传 Constants.DataPath/StardewGallery/gallery.sqlite3。尽量不依赖 Stardew runtime（便于 PersistenceChecks）。

## 21. HistoryRepository

绑定 GalleryDatabase + SaveProfileKey。API：

- UpsertProfileMetadata(...)
- ImportLegacy(IEnumerable<WatchedEventSnapshot>)
- UpsertObservation(ObservedVariant variant, VariantObservationSummary summary)
- IReadOnlyList<WatchedEventSnapshot> GetCompatibilityVersions(EventIdentity identity)
- void AddHistoricalEventRecord(HistoricalEventRecord record)（当前无 production caller）

UpsertObservation 单 transaction 完成 event + variant + summary。不让 WatchedEventHistory 分三次独立写。

## 22. LegacyHistoryStore

把 WatchedEventHistory 中 ReadSaveData/base64/GZipStream/JsonSerializer/WriteSaveData 移出。继续旧 save key / 11 fields。错误在 store boundary 捕获并报告；不得从 CommitPending 冒泡到 UpdateTicked。SMAPI save-data 对 remote farmhand 有限制 → legacy write failure 不回滚 SQLite、不标记 corrupt、只作 compatibility store unavailable；不做 multiplayer sync protocol。

## 23. Legacy import

每次 SaveLoaded：LegacyHistoryStore.Load → LegacyHistoryAdapter.From → repository idempotent merge。只 import ObservedVariant + VariantObservationSummary；不 import KnownSeenEvidence table、HistoricalEventRecord。重复 N 次结果稳定。旧 legacy summary 不覆盖 newer DB location/locale。

## 24. Dual-write

```
TryCapture → WatchedEventSnapshot → LegacyHistoryAdapter.From
→ SQLite UpsertObservation [primary]
→ update session compatibility state
→ LegacyHistoryStore.TrySave [compatibility]
```

Failure matrix：

- SQLite success / legacy success → 正常
- SQLite success / legacy fail → DB 保留；UI session 可见；log once；不影响 gameplay；SQLite 不 degraded
- SQLite fail / legacy success → SQLite session degraded；legacy 保留；fallback UI；下次 DB 恢复再 import
- both fail → log；gameplay 不崩；不 throw

CommitPending 不得让 persistence exception 穿透到 SMAPI tick。

## 25. WatchedEventHistory

保留：Update、pending lifecycle、TryCapture、same-script selector、fragments、replay exclusion、completion evidence、Get(EventIdentity)、Get(GalleryEvent)。移出：raw JSON persistence、SQL/schema。保留小型 session cache，但不再作为主要持久化 source of truth。

## 26. Compatibility projection

UI 行为不变：full variants → same PlaybackHash collapse → latest LastObservedAt → LastObservedAt descending。condition-only variants DB 都保留，UI 仍 collapse。DB→WatchedEventSnapshot 映射见任务书 §24；返回 mutable Dictionary defensive copy。

## 27. HistoricalEventRecord

表存在但 production writes=0、legacy import writes=0。绝不从 FirstWatchedAt / LastWatchedAt / eventsSeen / Replay / Preview 伪造 row。REPORT 明确：table exists != chronology implemented。

## 28. KnownSeenEvidence

不建表。eventsSeen 属 Stardew save。

## 29. ModEntry lifecycle

最小修改：SaveLoaded → dispose previous defensively → create SaveProfileKey → init SQLite → load legacy → import → attach session → 保持 catalog/unlock。ReturnedToTitle → dispose repository/DB、watchedHistory clear、historicalAssets clear、保持原行为。ModEntry 不含 SQL。

## 30. Degraded mode

以下任一 DB 错误禁用本 save session SQLite：directory/open、native runtime、schema、future schema、corrupt、query、write transaction。fallback：legacy + in-memory compatibility。首次 Error 明确，后续避免 tick spam。不 delete/repair/overwrite DB。malformed single payload：skip row + log；整体不可信则 degrade/fallback。

## 31. PersistenceChecks

新增独立 project：`PersistenceChecks/StardewGallery.PersistenceChecks.csproj`、`PersistenceChecks/Program.cs`。现有 Checks 保持 BCL-only 不引用 Microsoft.Data.Sqlite。PersistenceChecks：net6.0 + Microsoft.Data.Sqlite，不依赖 Stardew/SMAPI，include 必需 Domain/Persistence source。至少测试见任务书列举的 Schema / SaveProfileKey / EventIdentity collation / Variant / Payload / Summary / Legacy import / Compatibility / Transaction / Future schema / Reopen 各组。

## 32. Existing Checks

继续 `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`。Phase 1-4 checks 不删除/削弱。

## 33. Build / package validation

运行 restore / build / Checks / PersistenceChecks / git diff --check。main Release 0 warnings/0 errors；现有 Checks 仅 NETSDK1138；PersistenceChecks 输出 success。检查 mod zip inventory（Microsoft.Data.Sqlite.dll、SQLitePCLRaw assemblies、e_sqlite3 native Windows x64/Linux x64/Linux arm64(若有)/macOS x64/macOS arm64）。报告实际存在内容。Linux/macOS 未运行不称 runtime-tested。Windows 环境至少 PersistenceChecks 完成 open DB/schema/insert/read/reopen。若 ModBuildConfig 没带 runtime assets，允许最小 csproj/build item 修复；不硬编码 NuGet cache、不只打 win-x64、不引入复杂脚本。

## 34. Manual smoke 计划

OpenCode 环境无法启动游戏时明确 pending，不伪称通过。后续人工 P5-1 / P5-2 / P5-3。

## 35. Remote farmhand 限制

REPORT 记录：SMAPI save-data API 对 remote farmhand 有限制。SQLite 可作该设备当前 player profile 的 primary persistence，但 legacy dual-write 对 remote farmhand 不保证。legacy failure 不回滚 SQLite、不 crash natural event、不标记 DB corrupt。不实现 multiplayer sync。

## 36. Out of scope

禁止：exact natural HistoricalEventRecord capture、historical instance inference、ReplayCoordinator refactor、unified EventLauncher、Preview、StateInjector、CP1、ConditionIR UI、Planner/Solver、Gallery redesign、history timeline、删除 legacy save key、manifest/version/config/release changes、Phase 6+。

## 37. 验证命令

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
git diff --check
```

## 38. Commit / push

review final diff；focused implementation commit（必要时 package 第二个 focused commit）；push phase5/sqlite-persistence；local/origin sync；clean tree；不开始 Phase 6。
