# Stardew Gallery Phase 5：SQLite Persistence 审计与设计

日期：2026-09-03

## 0. 文档属性

- 工作分支：`phase5/sqlite-persistence`
- 基线：`985928477bddbb4b2670668b7eadcf7011ea47ed`
- 性质：只读源码审计 + SQLite 架构/schema/迁移/失败/兼容设计 + 实施计划。
- 本阶段不实现任何业务代码、不引入 SQLite package。不开始 Phase 6。

证据分级：`[REPO]` 仓库已证实；`[NATIVE]` Stardew/SMAPI 官方文档证实；`[SUM]` 沿现有调用链推导。

---

## 1. 当前 Persistence Pipeline（实测）

### Natural capture → legacy blob

```text
Game1.UpdateTicked
  → WatchedEventHistory.Update(replayActive)
  → TryCapture (same-script definition resolution via ObservedVariantSelector)
  → WatchedEventSnapshot
  → pendingSnapshot
  → CommitPending (需 eventsSeen.Contains(EventId))
  → Add (composite ObservedVariantKey dedup)
  → entries: EventIdentity → Dictionary<ObservedVariantKey, WatchedEventSnapshot>
  → Save()  gzip + base64 + JsonSerializer(List<WatchedEventSnapshot>)
  → helper.Data.WriteSaveData("watched-event-versions", ...)
```

### Load

```text
SaveLoaded
  → WatchedEventHistory.Load()
  → ReadSaveData("watched-event-versions") → base64 → gzip → JSON List<WatchedEventSnapshot>
  → for each: Add(snapshot, save:false)  (composite dedup)
  → in-memory entries
```

### Phase 4 domain（adapter）

`LegacyHistoryAdapter.From(WatchedEventSnapshot)` → `LegacyHistoryProjection(ObservedVariant, VariantObservationSummary, KnownSeenEvidence)`。ObservedVariant 的 Key = `ObservedVariantKey(Identity, RootDefinitionHash, PlaybackHash)`；Playback 为 `HistoricalPlaybackBundle(RootScript, EventAssets, Translations, Locale, PlaybackHash)`。

### Phase 5 插入层

SQLite 应成为历史领域（ObservedVariant / VariantObservationSummary / HistoricalEventRecord）的持久化层，替换 `entries` 内存 Dictionary 作为这些领域的 source-of-truth 存储。natural capture orchestration / same-script resolution / pending lifecycle / replay exclusion / compatibility API 保留在 `WatchedEventHistory`；gzip/base64 JSON 路径降级为 `LegacyHistoryStore`（详见 §14、§18）。

---

## 2. Phase 5 Goals / Invariants

目标：

- 为 Phase 4 领域语义（SaveProfile、EventIdentity、ObservedVariant、VariantObservationSummary、HistoricalEventRecord）建立稳定持久化层。
- 保持旧 `watched-event-versions` 可读、旧 historical replay 可用、当前 Gallery UI 行为不变、natural gameplay 不因 DB 故障受影响。
- 不重新定义 Phase 4 domain。

固定不变量：

```text
EventIdentity = normalized AssetName + case-sensitive EventId
ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash
ObservedVariant != HistoricalEventRecord
KnownSeenEvidence != HistoricalEventRecord
Current ResolvedEvent != ObservedVariant
```

---

## 3. Source-of-truth 策略（推荐）

比较三个方案：

- A. legacy save data 继续 source-of-truth，SQLite 只是 mirror/index。
- B. SQLite 成为 source-of-truth，legacy save data 只用于首次 migration。
- C. SQLite 成为主要 source-of-truth，但 Phase 5 继续同步维护 legacy `watched-event-versions` 作为 downgrade compatibility / 跨机器 bootstrap / historical replay compatibility fallback。

推荐 **方案 C**。理由：

1. **数据一致性**：SQLite 作为主存储能承载 Phase 4 拆分后的完整领域（含 condition-only variants、observation summaries、将来 instance records）；legacy blob 是降级投影。
2. **downgrade 兼容**：保留 legacy blob（Phase 1 兼容承诺）可让旧版 Stardew Gallery 读取历史版本，不因升级丢失。
3. **Steam Cloud / 换电脑 / SQLite 丢失**：legacy blob 仍在 save 内，跨机器同步时 DB 丢失可由 legacy 重新 bootstrap。
4. **historical replay compatibility**：ReplayCoordinator 需要 `WatchedEventSnapshot`，SQLite repository 生成 compatibility projection（§19）。
5. **future Phase 6/9**：SQLite 为 variant explorer / CP1 observation / planner 提供 queryable 基础。

关键约束：

- 不因 SQLite 迁移成功就立即删除 legacy `watched-event-versions`。
- dual-write 顺序与 failure policy 见 §16、§23、§27。

---

## 4. SaveProfile Identity

不得默认 `SaveProfileId = SaveFolderName`。

Stardew / SMAPI 可用字段：

| 字段 | 类型 | 性质 | 改名稳定性 |
| --- | --- | --- | --- |
| `Constants.SaveFolderName` | string | 存档文件夹名（save 目录 short id） | 重命名文件会变（metadata） |
| `Constants.CurrentSavePath` | string | 当前存档路径 | 移动会变（metadata） |
| `Game1.uniqueIDForThisGame` | ulong | farm unique ID（save 内持久化） | 稳定 |
| `Game1.player.UniqueMultiplayerID` | ulong/long | player unique MP ID | 稳定（host 固定，farmhand 每连接分配） |

比较：

- A. SaveFolderName：改名后不稳定，不适合作唯一 identity，仅 metadata。
- B. farm unique ID（uniqueIDForThisGame）：稳定标识农场；适用于所有存档。
- C. player UniqueMultiplayerID：host 稳定，farmhand 非持久（每加入可能不同）。
- D. farm ID + player ID composite：能区分「同一农场的不同 local profile」；适合 host vs farmhand / split-screen。
- E. 其他：无更优稳定 identifier。

推荐 **SaveProfileKey = (FarmUniqueId, ProfileCoordinate)**，其中：

- `FarmUniqueId` = `Game1.uniqueIDForThisGame`（farm 稳定 id）。
- `ProfileCoordinate` = 一个 player-profile 维度，**当前 Gallery 只服务 host/local player（single-player 或 host 本地玩家）**：`[REPO]` `Context.IsMultiplayer` 时 replay 被禁止，Gallery 使用 `Game1.player`（本地玩家）的 eventsSeen / friendship。因此 Phase 5 MVP 定位为**单一 local profile per farm**（`SaveProfile = FarmUniqueId`，player 维恒为 `Local`），未来需要 host/farmhand 分开时再加 `ProfileCoordinate`。

理由：

- SaveFolderName 是 mutable metadata，**不进 identity**。
- 同一农场不同本地玩家在 Stardew 中共享同一 save 文件；Gallery 读的是本地玩家状态。Phase 5 先用 farm id 作为 profile identity 足够。
- copied/duplicated save：`uniqueIDForThisGame` 复制时不同（各自产生新 farm id），视为不同 SaveProfile，正确。
- 未来 split-screen / host-farmhand：需再加 player 坐标；Phase 5 用 minimal profile 结构 + 预留维度。

推荐：

```csharp
internal readonly record struct SaveProfileKey(ulong FarmUniqueId);
```

（未来加 `string ProfileCoordinate` 扩展为 `(FarmUniqueId, ProfileCoordinate)`；Phase 5 保持 farm-scoped。）

> 注：`SaveFolderName` 存入 `save_profiles` 的 `save_folder_name metadata` 列。

---

## 5. DB 文件位置

比较：

- A. Mod folder：mod 更新可能覆盖 / 权限差。
- B. `Constants.DataPath/StardewGallery/`：SMAPI 数据目录，稳定、权限友好（与现有 backups/diagnostics 同目录）。`[REPO]` `GalleryDiagnostics.DirectoryPath = Path.Combine(Constants.DataPath, "StardewGallery", "diagnostics")`；`ReplayBackup` 也用 `Constants.DataPath\StardewGallery\backups`。
- C. 当前 save folder：随存档走，但跨 save 无法共享；改存档/云同步到其它机器需随 save。
- D. 每 save 一个 DB：与 C 类似，跨 save 查询困难。
- E. 所有 save 共用一个 DB + save_profiles：可跨 save 查询，符合 future planner；DB 损坏影响全体（有 legacy fallback 兜底）。

推荐 **方案 B 基础 + E 形态**：`Constants.DataPath/StardewGallery/gallery.sqlite3`，单 DB + `save_profiles` 表区分存档。

理由：

- 与现有 mod 数据目录一致（diagnostics/backups 同目录）。
- 不被 mod 更新覆盖（DataPath 是 SMAPI 数据目录，非 mod 文件目录）。
- 单 DB + save_profiles 支持跨 save 查询（future planner），DB 丢失可由 legacy blob 按 save 重新 bootstrap。
- Steam Cloud 只同步 `Constants.DataPath` 下 Stardew 数据目录；gallery.sqlite3 位于其中，可被同步（但需注意：DB 位于 DataPath 而非 save folder，可能不被所有云同步覆盖——这是一项已记录限制，跨机器 bootstrap 依赖 legacy blob 兜底）。

---

## 6. Provider / Package 推荐（只分析，不加包）

推荐 **Microsoft.Data.Sqlite**（core）作为 ADO.NET provider。

- 它是微软官方、跨平台（Windows/Linux/macOS Intel+ARM），使用 SQLitePCLRaw 原生运行时。
- Stardew 目标：`.NET 6`（项目 `TargetFramework net6.0` `[REPO]`）；Microsoft.Data.Sqlite 支持 net6.0 / netstandard2.0，兼容。
- native runtime：需要 `SQLitePCLRaw.bundle_e_sqlite3`（或 bundle_green）随包打包，Microsoft.Data.Sqlite 默认依赖 bundle_e_sqlite3。
- 打包：ModBuildConfig 生成 zip 时会把程序集打包；需确保 `SQLitePCLRaw.bundle_e_sqlite3` 的 native（runtimes/win-x64, linux-x64, osx-x64, osx-arm64, linux-arm64）被复制进发布目录并包含在 mod zip。SMAPI 的 `BundleExtraAssemblies`/build 配置需确认 native 是否携带（Phase 5 实施时验证 `bin/Release/net6.0` 下是否有对应 runtimes）。
- 初始化调用：`SQLitePCL.raw.SetProvider(new SQLite3Provider_e_sqlite3())`（bundle 可自动初始化），`Microsoft.Data.Sqlite` 通常无需手动（bundle 自带 init）。实施时需在 DB 打开前确认。
- license：Microsoft.Data.Sqlite 为 MIT；SQLitePCLRaw 为 Apache-2.0（e_sqlite3 为 public domain）。
- 体积：bundle 增加约 1-2 MB（含各平台 native）。

推荐：**Microsoft.Data.Sqlite + SQLitePCLRaw.bundle_e_sqlite3**。若 zip native 打包出问题，备选为分离 provider（如 Mono.Data.Sqlite / sqlite-net-pcl + e_sqlite3），但 Microsoft.Data.Sqlite 是最稳首选。

> 本轮只分析，不改 csproj / 不加 package。

---

## 7. Domain-to-Schema 映射

| Domain | 表 | 说明 |
| --- | --- | --- |
| SaveProfileKey | `save_profiles` | farm id + metadata |
| EventIdentity | `events` | AssetName + EventId |
| ObservedVariant | `observed_variants` | event_fk + root_definition_hash + playback_hash + content |
| VariantObservationSummary | `variant_observation_summaries` | profile_fk + variant_fk + first/last + metadata |
| HistoricalEventRecord | `historical_event_records` | profile_fk + variant_fk + watched_at + location/locale |
| KnownSeenEvidence | （不建表） | eventsSeen 属于 Stardew save state，见 §6 |
| HistoricalPlaybackBundle | observed_variants 内（root_script + playback payload） | 见 §10 |
| Current ResolvedEventIndex | （不建表） | runtime index，不属于历史 DB |

---

## 8. Schema v1 提案（仅设计）

```sql
PRAGMA user_version = 1;

CREATE TABLE save_profiles (
    profile_pk          INTEGER PRIMARY KEY,
    farm_unique_id      INTEGER NOT NULL UNIQUE,
    save_folder_name    TEXT,
    farmer_name         TEXT,
    created_at          INTEGER NOT NULL,
    last_seen_at        INTEGER NOT NULL
);

CREATE TABLE events (
    event_pk            INTEGER PRIMARY KEY,
    asset_name          TEXT NOT NULL,
    event_id            TEXT NOT NULL,
    UNIQUE(asset_name, event_id)
);

CREATE TABLE observed_variants (
    variant_pk          INTEGER PRIMARY KEY,
    event_fk            INTEGER NOT NULL REFERENCES events(event_pk) ON DELETE CASCADE,
    root_definition_hash TEXT NOT NULL,
    playback_hash       TEXT NOT NULL,
    root_script_hash    TEXT NOT NULL,
    root_script         TEXT NOT NULL,
    raw_event_key       TEXT NOT NULL,
    playback_json       TEXT NOT NULL,
    UNIQUE(event_fk, root_definition_hash, playback_hash)
);

CREATE TABLE variant_observation_summaries (
    summary_pk          INTEGER PRIMARY KEY,
    profile_fk          INTEGER NOT NULL REFERENCES save_profiles(profile_pk) ON DELETE CASCADE,
    variant_fk          INTEGER NOT NULL REFERENCES observed_variants(variant_pk) ON DELETE CASCADE,
    first_observed_at   INTEGER NOT NULL,
    last_observed_at    INTEGER NOT NULL,
    last_observed_location_name TEXT,
    last_observed_locale TEXT,
    UNIQUE(profile_fk, variant_fk)
);

CREATE TABLE historical_event_records (
    record_pk           INTEGER PRIMARY KEY,
    profile_fk          INTEGER NOT NULL REFERENCES save_profiles(profile_pk) ON DELETE CASCADE,
    variant_fk          INTEGER NOT NULL REFERENCES observed_variants(variant_pk) ON DELETE CASCADE,
    watched_at          INTEGER NOT NULL,
    location_name       TEXT,
    locale              TEXT
);
```

说明：`asset_name` / `event_id` 的 equality 语义见 §9；`observed_variants` 复合唯一即 `ObservedVariantKey`；`historical_event_records` 为 Phase 4 HistEventRecord 预留（当前 0 条，见 §13）。`KnownSeenEvidence` 不建表（eventsSeen 属 Stardew save）。

---

## 9. EventIdentity Equality 实现

关键：必须保证

```text
Data/Events/Town + abc == data/events/town + abc   (AssetName OrdinalIgnoreCase)
abc != ABC                                          (EventId Ordinal)
```

SQLite 默认 `COLLATE NOCASE` 只做 ASCII case-insensitive，且用 `LOWER()`（仅 ASCII）比较，**不等价于 .NET `StringComparer.OrdinalIgnoreCase`**（后者做 Unicode 大小写折叠）。因此不能依赖 SQLite NOCASE 对 AssetName 做 equality。

比较方案：

- A. SQLite `COLLATE NOCASE`（asset_name）——不等价（ASCII-only；Unicode 大小写不同行为）。
- B. 存 canonical folded AssetName —— 在写库前用 `EventIdentity`（已 `Replace('\\','/')` + Trim）规范化并折叠大小写为统一存储值。
- C. Microsoft.Data.Sqlite custom collation —— 可行但增加复杂度+注册开销。
- D. 额外 persistence key —— 增加冗余列。

推荐：**方案 B（canonical folded AssetName）+ 应用层保证**。具体：

- 应用层在写入 `events` 前把 AssetName 规范化为 `EventIdentity.AssetName`（已斜杠化+Trim），并统一折叠大小写（如 `ToUpperInvariant()`）作为**可比较的 canonical key**，存进 `asset_name`。
- 查询时同样折叠。这样 SQLite binary comparison 即等价于 `OrdinalIgnoreCase`（因为我们预先折叠）。
- `event_id` 存**原样，不做任何大小写折叠**，用 binary（默认 BINARY）比较 → 满足 `Ordinal case-sensitive`。
- 演示 / debug 需要展示原始 casing 时，用 `raw_asset_name` 或从别处取——但 Phase 5 MVP 中 `asset_name` 直接存 canonical folded 值，并保留 `event_id` 原样。若需还原原 casing，可加 `asset_name_raw` 列（可选）。`[SUM]` 现有 `EventIdentity.AssetName` 本就已 `'\\'→'/'` + Trim，大小写折叠是 Persistence 层额外加的 canonical 化，不改变 Phase 1 语义（事件扫描仍用原值做索引，Persistence 仅存 canonical）。
- 结论：**AssetName 存 canonical（folded）值，EventId 存原样**；不在 SQL 表上做不等价的 NOCASE。

hash 列：

- `root_definition_hash` / `playback_hash` / `root_script_hash`：存精确 64-hex 字符串，binary 比较（Ordinal）。不做大小写折叠（hash 是 content hash，case-distinct 不应合并）。
- `raw_event_key`：**不是 identity**，存原样（可 null 或 TEXT）；不参与唯一键。
- `playback_hash`：case 应视为 exact stored value——legacy `Fingerprint` 可能是任意字符串（如 tests 用 "abc"），不是严格 64-hex。因此 schema **不得加 `CHECK(length(playback_hash)=64)` 之类过严约束**，否则旧 migration 失败。存储原字符串。

---

## 10. Playback Payload 存储

`HistoricalPlaybackBundle` 含 RootScript、EventAssets、Translations、Locale、PlaybackHash（PlaybackHash 只覆盖 RootScript+EventAssets+Translations，不含 Locale）。

比较：

- A. observed_variants 一行直接存 root_script TEXT + event_assets JSON + translations JSON（三列）。
- B. 一个 JSON/BLOB playback payload。
- C. 额外 normalize：playback_bundles / event_asset_entries / translation_entries 表。
- D. 压缩 BLOB。

推荐：**A 为主，B 辅助**——`observed_variants` 存 `root_script TEXT` + `playback_json TEXT`（一个紧凑 JSON 包含 EventAssets + Translations + Locale），保留 queryable metadata 列（hash / raw_event_key / root_script）。

决定理由：

- 不为「数据库正规化」过度拆碎 fragments/translations（增加 join 与迁移复杂度，而 exact historical replay 是一次性读整包）。
- exact historical replay / future variant explorer 需要完整 bundle——单列 JSON 一次性恢复最简。
- 数据量：fragments/translations 为事件级，量小，单行 JSON 上界小。
- debug/inspectability：JSON payload 可读；可选压缩 BLOB（D）留待数据量证明必要。
- duplicate content：同一 variant 多 profile 共享 variant 行（variant 表无 profile 维度），仅 summary 按 profile 拆（见 §12）→ 减少重复。
- locale 语义：locale 存入 playback_json 作为 bundle 片段（不进 uniqueness），同时出现在 summary（§11、§12）。`[SUM]` 保持 PlaybackHash 不变。

推荐矩阵：variant row（root_script + playback_json）承载 bundle；`ObservedVariantKey` 唯一键不含 locale。

---

## 11. Locale 语义

重要事实 `[REPO]`：

- PlaybackHash = getSnapshotFingerprint(rootScript, eventAssets, translations)，**不含 Locale**。
- ObservedVariantKey 不含 Locale。
- VariantObservationSummary.LastObservedLocale、HistoricalEventRecord.Locale 各自带 locale。

因此 SQLite 设计不得让 Locale 进入 variant identity。

方案：

- A. variant row 上存 capture locale metadata —— 建议：variant 的 `playback_json` 内含 capture locale（作为 bundle 非 key 字段），供 exact replay materialize。
- B. observation summary 上存 locale，materialize replay bundle 时组合 —— summary 的 `last_observed_locale` 是「最近观察时的语言」。
- C. payload 内继续带 Locale，不参与 uniqueness —— 推荐：`playback_json` 内含 locale，但 `observed_variants` 唯一键（event+root_def+playback_hash）不含 locale。

跨不同 profile/语言捕获同一 variant 时：同一 `ObservedVariantKey`（playback_hash 不含 locale）→ 唯一 variant 行合并；不同语言的观察在 summary 层按 profile 分别记录 `last_observed_locale`。materialize exact replay 时用 variant 的 playback_json 内 locale 作为默认，或按需要说明「语言随观察」语义。

**PlaybackHash 语义不变，不得因 schema convenience 改 hash 输入。**

---

## 12. variant_observation_summaries 表

per-profile/per-variant aggregate。唯一 `(profile_fk, variant_fk)`。

Upsert 语义：

- `first_observed_at = min(existing, incoming)`。
- `last_observed_at = max(existing, incoming)`。
- `last_observed_location_name / last_observed_locale`：取**真正较新的观察**（按 incoming time 是否 ≥ existing last 决定），不得用旧 import 覆盖更新数据。

Timestamp 存储：统一规则见 §14。

---

## 13. HistoricalEventRecord 表

即使当前 production 不产生 HistoricalEventRecord `[REPO]`（Phase 4 明确「不伪造历史行」），**推荐 Phase 5 现在就创建 `historical_event_records` 表**。

理由：

- Phase 4 已定义 `HistoricalEventRecord` 领域类型；表是持久化它的自然落点。
- 提前建表避免未来 add-column migration；表空即可（0 行）。
- 字段来自 Phase 4：`(Variant, WatchedAt, LocationName, Locale)` + `profile_fk`。
- legacy import：**0 historical_event_records**（迁移规则 + Checks 固定）。

若推迟建表，需要额外一次 schema migration；鉴于 Phase 4 类型已存在，推荐现在建。

---

## 14. Timestamp Storage

domain 用 `DateTimeOffset`。SQLite 无原生 DateTimeOffset。

推荐：**统一 UTC Unix 毫秒 INTEGER**（`int64`）。

理由：

- exact instant：UTC 毫秒无损。
- sorting：数值排序稳定（timezone 无关）。
- inspectability：中等（需换算）。
- 跨表一致：所有时间列统一 int64 毫秒 UTC。
- legacy conversion：`DateTimeOffset.ToUnixTimeMilliseconds()`；读回 `DateTimeOffset.FromUnixTimeMilliseconds(millis)` 转 UTC。
- future UI：展示时按需 localize。

备选对比：

- UTC ticks INTEGER：精度过高、无必要。
- ISO-8601 TEXT normalized UTC：可读但排序/比较依赖字符串规范化，易错。
- ISO-8601 TEXT 保留原 offset：保留 offset 但失去 exact instant 一致性，跨时区排序易错。

**统一 int64 UTC 毫秒**，不接受不同表不同格式。

---

## 15. Legacy Migration

watched-event-versions → SQLite。

要求：idempotent、non-destructive、可重复运行。

每个 legacy `WatchedEventSnapshot` 经 `LegacyHistoryAdapter.From`：

```text
WatchedEventSnapshot
  → LegacyHistoryProjection(ObservedVariant, VariantObservationSummary, KnownSeenEvidence)
  → SQLite import:
       ObservedVariant       (upsert by composite key)
       VariantObservationSummary (upsert by profile+variant)
       KnownSeenEvidence     (不持久化 / 不建行)
```

不得：

- 产生 HistoricalEventRecord。
- 把 FirstWatchedAt / LastWatchedAt 拆成多条 observation/history。

实现策略：推荐**每次 SaveLoaded 做 idempotent merge**（而非一次性 legacy_imported flag）。

理由：

- DB 丢失后可自动重建。
- 跨机器拥有 legacy save data 时可 bootstrap。
- 不引入额外状态列。

但关键约束：

- 不得让旧 legacy 数据覆盖 DB 中更新的 `LastObservedAt` / `Location` / `Locale`。merge 采用「min first / max last」+「仅当 legacy 时间 ≥ existing last 才更新 last metadata」，否则保留 DB 现有值（见 §12 upsert 规则）。

---

## 16. Dual-write Strategy

推荐 Phase 5 继续 dual-write（SQLite 为主 + legacy blob 兼容）。

natural capture 顺序（建议）：

```text
capture
  → domain (ObservedVariant + VariantObservationSummary)
  → SQLite transaction (event + observed_variant + observation_summary)   [主存储，先写]
  → legacy compatibility write (WatchedEventSnapshot → watched-event-versions)  [兼容投影]
```

Failure matrix：

| 情况 | 策略 |
| --- | --- |
| SQLite 成功 / legacy 失败 | DB 已持久化；legacy 写失败仅 log（下次 load/merge 由 DB 兜底），不影响 gameplay |
| SQLite 失败 / legacy 成功 | SQLite 失败 → Degraded 模式；legacy blob 已更新，可在 DB 恢复后重新 merge 进 DB |
| 两者都失败 | 该次观察丢弃（log 一次）；不崩溃 |
| Load 时 DB 不可用 | Degraded → fallback legacy（若有）；否则空 |
| DB schema 版本过新 | 禁用 SQLite（不 overwrite），fallback legacy，log |
| DB corrupt | 错误 → fallback legacy，不自动删除 |

原则：**数据库错误永远不能影响正常 Stardew gameplay**；natural event 不得因 database exception 崩溃。SQLite failure 时进入 Degraded persistence mode（log + fallback legacy）。不静默吞错误，需一次清晰日志避免每 tick spam。

---

## 17. Repository / Storage API

`WatchedEventHistory` 当前混合 runtime capture / persistence / compatibility projection。Phase 5 设计明确 persistence boundary，但**不引入复杂 repository/unit-of-work 框架**。

最小接口（concise）：

```csharp
internal sealed class HistoryRepository(
    GalleryDatabase database,
    SaveProfileKey profile)
{
    IReadOnlyList<ObservedVariant> LoadObservedVariants(EventIdentity identity);
    void UpsertObservedVariant(ObservedVariant variant);
    void UpsertObservationSummary(VariantObservationSummary summary);
    void AddHistoricalEventRecord(HistoricalEventRecord record);   // 当前可能无调用
    IReadOnlyList<WatchedEventSnapshot> GetCompatibilityVersions(EventIdentity identity);
    void ImportLegacy(IEnumerable<WatchedEventSnapshot> legacySnapshots);
}
```

特性：

- 不含 SQL 散落（SQL 集中在 `GallerySchema`/`HistoryRepository`）。
- 薄、同步、每操作小事务。
- `GetCompatibilityVersions` 生成 `WatchedEventSnapshot`（§19）。

---

## 18. WatchedEventHistory Phase 5 定位

保留：

- natural capture orchestration。
- same-script definition resolution（ObservedVariantSelector）。
- pending event lifecycle。
- replay exclusion。
- compatibility API：`Get(EventIdentity)` / `Get(GalleryEvent)`（返回 `IReadOnlyList<WatchedEventSnapshot>`，供 UI / Replay）。

移出：

- raw persistence implementation（gzip/base64 JSON 位于 `LegacyHistoryStore`/`LegacyHistoryAdapter`）。
- 内存 `entries` 持久化 → 由 `HistoryRepository`（SQLite）承担 source-of-truth。

不重写 natural capture state machine。`LegacyHistoryAdapter` 继续复用（migration 转换）。

---

## 19. Historical Replay Compatibility

`ReplayCoordinator` 当前需要 `WatchedEventSnapshot`；Phase 5 不重构 ReplayCoordinator。

SQLite repository 必须能生成 `WatchedEventSnapshot` compatibility projection（至少旧 11 字段）：`LocationName, AssetName, EventId, EventKey, RootScript, EventAssets, Translations, Locale, Fingerprint, FirstWatchedAt, LastWatchedAt`。

当前 UI `WatchedEventHistory.Get → IReadOnlyList<WatchedEventSnapshot>` 可继续保留。内部流程：

```text
SQLite domain rows
  → compatibility snapshot (WatchedEventSnapshot)
  → existing UI / Replay
```

不为 SQLite 修改 Replay API。Phase 6 才统一 EventLauncher。

---

## 20. Connection Lifecycle

比较：

- one connection per operation：开销高。
- one connection per save session：`SaveLoaded` open，`ReturnedToTitle` close，save switching 重开。推荐。
- application-lifetime connection：跨 save 保持，需处理 save 切换 / profile 变更。

推荐 **Save-loaded session connection**：

- `SaveLoaded` → open / init repository（`SaveProfileKey` 从当前 farm）。
- `ReturnedToTitle` → close / dispose repository。
- 不引入 background DB worker；Phase 5 默认同步、小事务。

考虑：

- SMAPI main loop 单线程：同步小事务安全，无并发问题。
- save switching：每次 Save 重开（profile 变化）。
- multiplayer：`Context.IsMultiplayer` 时 replay 禁 `[REPO]`；DB 仍按 host farm + local profile 写（MVP）。farmhand 不写历史。
- database lock：单连接 + busy_timeout 防边缘锁。

---

## 21. Transactions

一次 variant observation 写入至少涉及 event / observed_variant / observation_summary，须为一个事务。不能出现 variant 已写、summary 未写。

Recommended boundary：

```text
BeginTransaction
  INSERT OR IGNORE events
  UPSERT observed_variants
  UPSERT variant_observation_summaries
Commit
```

`HistoricalEventRecord` 将来单次 append 可在同一 capture transaction 中加入。legacy compatibility save 属 SQLite transaction 外部，明确 failure policy（§16）。

---

## 22. SQLite Pragmas

分析：

- `foreign_keys = ON`：**推荐启用**（保证 FK 完整性，ON DELETE CASCADE 生效）。每连接 `PRAGMA foreign_keys = ON`。
- `journal_mode = WAL`：**不默认开启**。理由：
  - 单进程顺序写入（SMAPI main loop），rollback journal 足够。
  - WAL 产生 `.wal`/`.shm` sidecar，增加 portability/backup 复杂度。
  - rollback journal 在单写场景 crash safety 足够。
  - 若未来并发（Phase 6+）再评估 WAL。
- `synchronous`：默认 `NORMAL`（rollback journal 下安全）或 `FULL`。推荐 `NORMAL`（单写 + 小事务，crash safety 足够）。
- `busy_timeout`：设 `5000ms` 防边缘锁（单连接下少见，保险）。

最小可靠配置：`PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;`（journal_mode 默认 rollback；synchronous NORMAL）。

---

## 23. Schema Versioning / Migrations

推荐 `PRAGMA user_version`（轻量、标准）：

- schema v1：本文 §8。
- 升级：打开 → 读 `user_version` → 若 < current 执行迁移（逐 version）→ 设为 current。迁移在事务内。
- 若 DB `user_version > current`：**禁用 SQLite for session**（fallback legacy），log error，不 downgrade / overwrite / 删除未知新版 DB。

是否需要 `schema_metadata` 表：MVP 用 `user_version` 足够；future 需记录更多元数据时再加（Phase 5 先用 user_version）。

---

## 24. Indexes / Query Patterns（MVP）

| 查询 | 索引 |
| --- | --- |
| event → variants（UI versions） | `observed_variants(event_fk)` |
| variant key lookup | 由 `UNIQUE(event_fk, root_definition_hash, playback_hash)` 覆盖 |
| profile 下全部 variants | `variant_observation_summaries(profile_fk)` |
| last observed ordering | `variant_observation_summaries(profile_fk, last_observed_at DESC)` |
| event identity lookup | `events` 的 `UNIQUE(asset_name, event_id)` |
| historical timeline by profile/event | `historical_event_records(profile_fk)` + `(variant_fk)` |

MVP index list（不过度建）：

- `events`: UNIQUE(asset_name, event_id)。
- `observed_variants`: UNIQUE(event_fk, root_definition_hash, playback_hash)；(event_fk)。
- `variant_observation_summaries`: UNIQUE(profile_fk, variant_fk)；(profile_fk, last_observed_at)。
- `historical_event_records`: (profile_fk), (variant_fk)。
- `save_profiles`: UNIQUE(farm_unique_id)。

---

## 25. Failure / Degraded Mode

| 情况 | 行为 |
| --- | --- |
| DB directory create fail | Degraded（无 persist）；log |
| DB open fail | Degraded；fallback legacy（若有） |
| schema create fail | Degraded；fallback legacy |
| schema migration fail | Degraded；fallback legacy；不破坏原 DB |
| unsupported future schema | 禁用 SQLite；fallback legacy；log |
| DB corrupt | Degraded；log；（可选备份）不自动 delete |
| read query fail | 该次读空（或 legacy）；log 一次 |
| write transaction fail | Degraded；log；该次观察丢弃 |
| legacy import fail | 跳过该条；log；继续其它 |
| legacy save write fail | 保留已有；DB 已持久化；log |
| serialization fail | 该次写入丢弃；log |
| DB row malformed / playback JSON malformed | 该条视为缺失；log；不整库失败 |

目标：normal gameplay 不崩；SQLite failure → Degraded mode；legacy 可用时 fallback；不自动删除/覆盖损坏 DB；一次清晰日志避免每 tick spam（Degraded 状态只 log 一次，之后静默降级）。

---

## 26. Corruption / Backup Policy

Phase 5 不需要复杂备份，但分析：

- 是否 migration 前复制 DB：**推荐在 schema migration 前复制 `gallery.sqlite3` → `gallery.sqlite3.bak-v{n}`**（一次，避免迁移失败破坏旧库）。
- 是否依赖 SQLite transaction：迁移在事务内；写事务保证 variant/summary 原子性。
- 是否 schema upgrade 前备份：是（如上）。
- 是否允许自动 rename corrupt DB：**不建议自动 delete / rename**；corrupt 时 log + fallback legacy，保留原文件由用户/备份处理。

默认倾向：不自动破坏旧 DB；migration 前可备份；corrupt → log + fallback legacy，不 delete。

---

## 27. Exact Legacy Compatibility

继续保证：

- save key `watched-event-versions`。
- gzip + base64。
- JSON 11 字段：`LocationName, AssetName, EventId, EventKey, RootScript, EventAssets, Translations, Locale, Fingerprint, FirstWatchedAt, LastWatchedAt`。
- Phase 1 compatibility Check 不删除。
- SQLite migration 不修改旧 JSON schema。

---

## 28. UI Compatibility

Gallery UI 继续只看到 playback-version compatibility list。即：

```text
full ObservedVariantKey variants
  → collapse same PlaybackHash
  → latest observation representative
  → LastObservedAt descending
```

SQLite 不改变 version count / ordering / Replay button / Current 行为。完整 condition-only variants 仍存在 DB，仅当前 UI compatibility projection collapse。

---

## 29. Automated Checks 策略

现有 Checks 继续保持 BCL-only core coverage。SQLite 引入后推荐**独立 project**：

- `Checks/`（现有）→ 保持 BCL-only。
- 新增 `PersistenceChecks/`（或 `StardewGallery.SqliteChecks`）→ SQLite-specific，不依赖 Stardew runtime，引用 Microsoft.Data.Sqlite。

至少规划测试：

- schema create from empty DB。
- schema version。
- event identity case behavior（AssetName folded / EventId case-sensitive）。
- profile insert。
- ObservedVariant insert。
- same composite key dedup。
- condition-only variant both survive。
- playback-only variant both survive。
- observation upsert：first=min、last=max。
- 较旧 migration 不覆盖 newer location/locale。
- legacy import idempotent。
- legacy import creates variant + summary。
- legacy import creates 0 HistoricalEventRecord。
- DB reopen persistence。
- compatibility projection parity。
- transaction rollback on failure。
- future schema version rejection。
- corrupt/malformed payload read failure policy。

保留 Phase 1/2/3/4 Checks。

---

## 30. Package / Build Validation Plan

implementation 后至少：

- `dotnet restore`。
- `dotnet build -c Release`。
- 现有 Checks。
- SQLite-specific Checks。
- `git diff --check`。
- 检查最终 mod zip 内容，确认 SQLite runtime dependencies 正确打包。

当前环境（Windows）可验证：Windows runtime 加载 SQLite、open in-memory/file DB、create schema、insert/read。Linux/macOS 无法实跑则至少从 NuGet runtime assets / packaging 证明设计可支持，不写「已实机验证」。

---

## 31. Automated Checks 策略（补充：独立 project 理由）

推荐方案 B——新增独立 `StardewGallery.SqliteChecks`（PersistenceChecks），因为：

- 现有 `Checks/` 用 source-link 保持 BCL-only，无 package。
- Microsoft.Data.Sqlite 需引入 package / native runtime，放进现有 Checks 会引入 SMAPI 兼容性负担（现有 Checks 是 net6.0 无外部引用）。
- 独立 project 不依赖 Stardew runtime（SQLite 本身跨平台）。

---

## 32. In-game Migration Smoke 计划（Phase 5 implementation 后）

- P5-1：已有带 watched-event-versions 旧 save → 启动 → SQLite DB 创建 → legacy history 自动 import → Gallery 原历史版本仍可见。
- P5-2：自然触发新事件 → SQLite variant/summary 更新 → legacy compatibility blob 仍更新 → reload 后仍存在。
- P5-3：SQLite 不可用/移走 DB → legacy 数据可重新 bootstrap 或按 failure policy fallback。

本轮不要求执行。

---

## 33. File-level Implementation Plan（设计，不在本轮实现）

建议新增（`Persistence/`）：

- `Persistence/GalleryDatabase.cs` —— DB 打开/关闭、pragma、migration 入口、degraded 状态。
- `Persistence/GallerySchema.cs` —— schema DDL / 迁移脚本、schema version。
- `Persistence/HistoryRepository.cs` —— 领域持久化（§17）。
- `Persistence/SaveProfileKey.cs` —— SaveProfile identity。
- `Persistence/LegacyHistoryStore.cs` —— 旧 gzip/base64 JSON 读写（从 WatchedEventHistory 移出）。
- `Persistence/AutoPatcher?` 不需要。

修改：

- `WatchedEventHistory.cs` —— natural capture / resolution / pending / replay exclusion / compatibility API 保留；raw persistence 移出到 `LegacyHistoryStore` + `HistoryRepository`。
- `LegacyHistoryAdapter.cs` —— 继续复用（migration 转换）。
- `HistoricalPlaybackBundle.cs` —— 基本不改（仅确认可作为 repository 存储/恢复对象）。
- `ModEntry.cs` —— SaveLoaded / ReturnedToTitle 接 repository 生命周期（open/init/close）。
- Checks：新增 `PersistenceChecks/` project；现有 `Checks/` 保持。

不要为了目录整洁移动现有大量代码。

---

## 34. Compatibility Risks

- 旧 save data：legacy blob 保留 + idempotent merge。
- historical replay：ReplayCoordinator 仍用 WatchedEventSnapshot；repository 生成 projection。
- Gallery version counts：collapse by PlaybackHash 不变。
- version sorting：LastObservedAt descending 不变。
- natural capture：复合 dedup 不变。
- eventsSeen：作为 KnownSeen，不持久化。
- modded event IDs：EventIdentity 保 mod 前缀。
- same identity multiple variants：composite 区分。
- locale 变化：playback_hash 不含 locale，语言变体不 collapse；summary 记 locale。
- fragment 变化：playback_hash 变化 → 新 variant。
- translation 变化：同。
- save reload：repository 重开 + idempotent merge。
- replay exclusion：保留。
- future CP1：SQLite 为 observation 预留。
- future SQLite（Phase 5 本身）：见 §3、§16。
- 跨平台 native runtime：见 §30。
- DB 丢失：legacy blob bootstrap。

---

## 35. Out-of-scope

本项目（Phase 5 设计）明确禁止：精确 natural HistoricalEventRecord capture state-machine、伪造 historical rows、ReplayCoordinator refactor、unified EventLauncher、PreviewState/PreviewPlan/StateInjector、CP passive discovery、ConditionIR UI integration、Planner/Solver/Route planning、Gallery UI 大改版、删除 legacy watched-event-versions、manifest/version/config/release changes、Phase 6+。

---

## 36. Unresolved Codex Decisions

1. SaveProfileKey：推荐 `(FarmUniqueId)` 单维（MVP farm-scoped，player 恒 Local）；future split-screen/host-farmhand 时再加 `ProfileCoordinate`。是否接受 MVP 不用 player 维？
2. DB 位置 `Constants.DataPath/StardewGallery/gallery.sqlite3` 单 DB + save_profiles：是否确认？还是 per-save DB？
3. Provider：确认 Microsoft.Data.Sqlite + SQLitePCLRaw.bundle_e_sqlite3；native 打包需验证。
4. source-of-truth：确认方案 C（SQLite 主 + legacy 兼容 dual-write）。
5. schema v1 是否现在建 `historical_event_records` 表（0 行）：推荐现在建。
6. EventId equality：推荐 AssetName canonical folded + EventId 原样；是否加 `asset_name_raw` 列保留原 casing？
7. playback payload：推荐 single `playback_json`（含 EventAssets+Translations+Locale）+ root_script 列；是否接受不拆 fragments/translations 表？
8. timestamps：确认 UTC Unix 毫秒 INTEGER。
9. pragma：确认 foreign_keys ON + busy_timeout 5000 + 默认 rollback journal（不用 WAL）。
10. KnownSeenEvidence 不建表：确认（eventsSeen 属 Stardew save）。
11. 独立 PersistenceChecks project：确认方案 B。
12. degraded mode：确认「SQLite 不可用 → fallback legacy / 不删 DB / 一次日志」。

---

## 37. 结论

Phase 5 为 Phase 4 拆分后的历史领域建立持久化层：单 DB（`Constants.DataPath/StardewGallery/gallery.sqlite3`）+ save_profiles，Microsoft.Data.Sqlite provider，方案 C source-of-truth（SQLite 主 + legacy 兼容 dual-write）。SaveProfileKey 首选 FarmUniqueId（MVP farm-scoped），EventIdentity equality 由「AssetName canonical folded + EventId 原样」保证，timestamp 统一 UTC 毫秒 INTEGER，legacy 迁移 idempotent 且不伪造 HistoricalEventRecord，DB 故障进入 Degraded 模式且不影响 gameplay。schema v1 覆盖 save_profiles / events / observed_variants / variant_observation_summaries / historical_event_records。本轮仅设计，不实现、不加包。
