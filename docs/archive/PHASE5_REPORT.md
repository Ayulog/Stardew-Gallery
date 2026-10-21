# Stardew Gallery Phase 5：SQLite Persistence 实施报告

日期：2026-09-03

## 0. 基线与 commit

- 工作分支：`phase5/sqlite-persistence`
- 分析基线：`31a78ee664403038814e0b8f13c22f527eed9bd0`
- 基线：`985928477bddbb4b2670668b7eadcf7011ea47ed`
- 实施依据：`docs/PHASE5_TASK.md`（覆盖 analysis unresolved 的 Codex 决议）
- 任务书：`docs/PHASE5_TASK.md`

## 1. 已实现（Phase 5 SQLite persistence 主体）

### 新增文件（`Persistence/`）

- `SaveProfileKey.cs` —— `SaveProfileKey(ulong FarmUniqueId, long PlayerUniqueId)`；`StoredFarmUniqueId`（unchecked long 转换）+ `RestoreFarmUniqueId`。
- `PlaybackPayload.cs` —— `PlaybackPayload(EventAssets, Translations)` persistence DTO，不含 Locale。
- `GalleryDatabase.cs` —— 连接管理、`CREATE COLLATION ORDINAL_NOCASE`、pragmas（foreign_keys=ON / busy_timeout=5000）、schema version 检查（0→create v1；1→ok；>1→reject 不 overwrite/delete）、degraded 状态、Dispose。DB path 构造函数注入。
- `GallerySchema.cs` —— schema v1 DDL（5 表 + 4 索引），`asset_name COLLATE ORDINAL_NOCASE`、`event_id COLLATE BINARY`、无 KnownSeen/Current 表、无 CHECK length 约束。
- `HistoryRepository.cs` —— `UpsertObservation`（单事务 event+variant+summary）、`ImportLegacy`（用 `LegacyHistoryAdapter`，不产生 HistoricalEventRecord）、`GetCompatibilityVersions`（collapse by PlaybackHash 最新 LastWatchedAt 降序 + defensive copies）、`AddHistoricalEventRecord`（当前无 production caller）、`EnsureProfile`。
- `LegacyHistoryStore.cs` —— 旧 `watched-event-versions` gzip/base64/11-field JSON 读写；错误在 store boundary 捕获并报告。

### 新增 `PersistenceChecks/`

独立项目（net6.0 + Microsoft.Data.Sqlite，不依赖 Stardew/SMAPI），覆盖 schema / SaveProfileKey ulong roundtrip / EventIdentity collation（asset case-insensitive、event_id case-sensitive 用非数字 id "abc" vs "ABC"）/ variant composite dedup / payload roundtrip + Locale 缺失 + malformed / summary first=min last=max / 旧 import 不覆盖新 metadata / 旧 import 0 history rows / compat 11 字段 collapse / transaction / future schema 999 拒绝 / reopen。

### 修改文件

- `WatchedEventHistory.cs` —— natural capture、same-script selector、pending lifecycle、replay exclusion、compat Get 保留；raw JSON persistence 移出到 `LegacyHistoryStore`；dual-write（SQLite primary 后 legacy 兼容）且 persistence 异常不冒泡到 tick；`AttachPersistence`/`DetachPersistence`。
- `ModEntry.cs` —— `SaveLoaded` 初始化 SQLite session（resolve SaveProfileKey、open DB、upsert profile、import legacy、attach）；`ReturnedToTitle` dispose + attach 分离；degraded 分支 attach `LegacyHistoryStore`（null repo）保证 legacy fallback；ModEntry 不含 SQL。
- `StardewGallery.csproj` —— `PackageReference Microsoft.Data.Sqlite 8.0.10`；`CopyLocalLockFileAssemblies=true`；排除 `PersistenceChecks\**`.
- `docs/PHASE5_TASK.md`（新增）。

## 2. 固定领域语义保持

- `EventIdentity = normalized AssetName + case-sensitive EventId`。
- `ObservedVariantKey = EventIdentity + RootDefinitionHash + PlaybackHash`。
- `ObservedVariant != HistoricalEventRecord`；`KnownSeenEvidence != HistoricalEventRecord`。
- `Current ResolvedEvent != ObservedVariant`。
- SQLite 只保存历史领域；不保存 ResolvedEventIndex.Current。
- KnownSeenEvidence 不建表（eventsSeen 属 Stardew save）。

## 3. Codex 调整点落地

1. SaveProfileKey = FarmUniqueId + PlayerUniqueId（含 player 维）。
2. AssetName equality = custom `ORDINAL_NOCASE`（不用 ToUpperInvariant folding），event_id COLLATE BINARY。
3. shared playback_json 不存 Locale（PlaybackPayload 只 EventAssets+Translations；root_script 单独列）。
4. 不主动 synchronous=NORMAL；只 foreign_keys=ON + busy_timeout=5000；不启 WAL。
5. 优先只显式引用 Microsoft.Data.Sqlite，验证 transitive bundle 打包成功（无需再显式引用 bundle_e_sqlite3）。

## 4. 验证结果

### Build / Checks

- `dotnet build -c Release`：成功，0 warnings，0 errors。
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`：`Stardew Gallery checks passed.`（仅既有 NETSDK1138，Phase 1-4 checks 保留）。
- `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release`：`Stardew Gallery persistence checks passed.`（仅 NETSDK1138）。
- `git diff --check`：无输出（干净）。

### Windows runtime 结果

PersistenceChecks 在当前 Windows 环境实际完成：open in-memory/file DB、create schema、insert/read、reopen persistence，全部通过。Windows runtime 加载 SQLite native 正常。

### mod zip runtime inventory

zip 内含：
- `Microsoft.Data.Sqlite.dll`
- `SQLitePCLRaw.batteries_v2.dll`
- `SQLitePCLRaw.core.dll`
- `SQLitePCLRaw.provider.e_sqlite3.dll`
- native `e_sqlite3` runtimes：win-x64 / win-x86 / win-arm64 / win-arm、linux-x64 / linux-x86 / linux-arm / linux-arm64 / linux-musl-x64 / linux-musl-arm / linux-musl-arm64 / linux-ppc64le / linux-s390x、osx-x64 / osx-arm64、maccatalyst-x64 / maccatalyst-arm64、browser-wasm。

Linux/macOS 未在环境实跑，**不在报告中声称 runtime-tested**；仅从 NuGet runtime assets / packaging 证明设计可支持。

## 5. SaveProfileKey / schema version

- SaveProfileKey：`(ulong FarmUniqueId, long PlayerUniqueId)`；SQLite `UNIQUE(farm_unique_id, player_unique_id)`；farm id bit-preserving unchecked roundtrip。
- Schema version：`PRAGMA user_version`，v1。

## 6. Remote farmhand 限制

- SMAPI save-data API 对 remote farmhand 有限制。
- SQLite 可作该设备当前 player profile 的 primary persistence，但 legacy dual-write 对 remote farmhand 不保证。
- legacy failure 不回滚 SQLite、不 crash natural event、不标记 DB corrupt。
- 不实现 multiplayer sync protocol。

## 7. 已确认 / 已实现的边界

- `HistoricalEventRecord` 表存在，但 production writes=0、legacy import writes=0；不从 FirstWatchedAt / LastWatchedAt / eventsSeen / Replay / Preview 伪造 row。**table exists != chronology implemented。**
- 不删除 legacy `watched-event-versions`；schema 11 fields 不变；Phase 1 compat Checks 保留。
- UI behavior 不变（collapse by PlaybackHash）；full ObservedVariantKey variants 在 DB 保留，UI 暂 collapse。
- 未实现 Phase 6（Preview / StateInjector / CP1 / ConditionIR UI / planner/solver / ReplayCoordinator refactor / unified EventLauncher）。

## 8. Manual smoke 状态（OpenCode 环境无法启动游戏，pending）

- P5-1：旧 save 有 watched-event-versions → load → DB create/import → Gallery history 可见 → historical replay works。（pending 人工）
- P5-2：自然新事件 → SQLite variant/summary + legacy blob 都更新 → reload 仍存在。（pending 人工）
- P5-3：移走 gallery.sqlite3 → 保留 legacy → reload → DB bootstrap → history 返回。（pending 人工）

自动验证（build + core Checks + PersistenceChecks）已通过，但不代表上述实机/序列已执行。

## 9. documented limitations

- Linux/macOS runtime 未实跑（仅 packaging 证据；Windows runtime 已实机验证）。
- SQLite-primary 读路径已接入 `WatchedEventHistory.Load`：SQLite available → legacy idempotent import → SQLite 全量（未 collapse）hydrate 进 session cache → UI 读 cache。SQLite degraded → legacy hydrate。
- degraded 模式（SQLite 不可用）已 attach legacy store 保证 fallback（含 catch 分支）。
- `AddHistoricalEventRecord` 无 production caller（表空）。
- 未实现 multiplayer sync；未开始 Phase 6。

## 10. Correction（本轮）：SQLite-primary read

修复 code review blocker：SQLite 现在作为实际 read source-of-truth。

- `HistoryRepository.LoadAllSnapshotsForProfile()` 新增：当前 profile 全部 observed variants + summaries，不按 PlaybackHash collapse，每 `ObservedVariantKey` 一条 compatibility snapshot，defensive dictionaries，malformed playback_json row skip + logger，SQL/connection/integrity 错误抛给上层（不吞成空 list）。
- `LegacyHistoryCodec`（BCL-only，base64/gzip/json）拆出，供 LegacyHistoryStore 委托；`LegacyHistoryStore.TryLoad(out ...)` 安全失败（no payload → success+empty；malformed → failure；异常不穿透）。
- `WatchedEventHistory.Load` 最终行为：`entries.Clear` → TryLoad legacy → (SQLite healthy) import legacy + `LoadAllSnapshotsForProfile` hydrate → return；否则 fallback legacy hydrate。SQLite healthy 时 DB 为主；legacy 仅供 bootstrap/merge；DB read failure → degrade fallback legacy；legacy corrupt 不阻断 valid SQLite read。
- condition-only variants 在 DB/cache/legacy full projection 保留；UI 仍 collapse same PlaybackHash。

新增 PersistenceChecks：
- full hydrate（2 condition-only variants）count==2；compat collapse count==1。
- legacy codec corrupt → SQLite hydrate 仍恢复 history。
- non-ASCII collation fixture（"Ünïcode" vs "ünicode"）经真实 SQLite table/UNIQUE index 合并为一行。
- 强制 transaction rollback（`fail_summary_insert` trigger）→ UpsertObservation 抛 → event/variant/summary 均 0 新增。
- future schema 加强：user_version 保持 999、v1 table 未创建（no downgrade/overwrite）。

## 11. Runtime hardening（本轮）

### SQLite native probing（root cause）

实机确认：SMAPI 动态加载 Mod 时未自动从 `runtimes/<rid>/native` probe `e_sqlite3`，导致嵌套 native 在 native-only 情况下 type initializer 失败 → fallback legacy → gallery.sqlite3 不出现；手工复制 `e_sqlite3.dll` 到 Mod 根目录才正常。手工复制不再是发布方案。

修复：新增 `Persistence/SqliteNativeBootstrap.cs` + `Persistence/SqliteNativeResolver.cs`。

- `SqliteNativeResolver`（BCL-only，可测）：RID 选择 —— `ResolveNativePath(modDirectory, runtimeIdentifier, os, arch)` 优先 exact `runtimes/<rid>/native/` 存在，fallback OS + ProcessArchitecture（win-x64/x86/arm64/arm、osx-x64/arm64、linux-x64/x86/arm/arm64；musl exact RID 保留）；native filename Windows e_sqlite3.dll / Linux libe_sqlite3.so / macOS libe_sqlite3.dylib。
- `SqliteNativeBootstrap.TryInitialize(modDirectory, logger)`：幂等（同 process 不重复注册），`Assembly.Load("SQLitePCLRaw.provider.e_sqlite3")` 后 `NativeLibrary.SetDllImportResolver`，resolver 只处理 e_sqlite3/libe_sqlite3（其它返回 IntPtr.Zero），`NativeLibrary.Load(fullPath)`；failure → false + 完整日志；不修改 PATH / 不复制到系统目录 / 不扁平复制 native。
- 接入：`ModEntry.InitSqliteSession` 最先行 `SqliteNativeBootstrap.TryInitialize(Helper.DirectoryPath)`，failure → legacy fallback（degrade）。
- `GalleryDatabase` 的 open/schema 失败日志改为完整异常（`\n{error}`，含 type/inner/stack），不逐 tick spam。
- PersistenceChecks：RID/path 选择测试（win-x64 exact 优先、win-x86、osx-arm64、linux-arm64、linux-musl-x64 exact 缺失 → OS+arch fallback、unsupported → failure），ReplayBackupRetention 测试。

### ReplayBackup 无界增长（confirmed）

实机确认 `backups-archive` 永不删除 → 约 1.2GB / 414 files / 73 folders。

修复：ReplayBackup 生命周期从「5 active + 无限 archive」改为：

- `Create()`：先 `Prune(save)`（backups + backups-archive 合并，保留最新 2 个（`ReplayBackupRetention.MaxStale=2`），其余删除；archive 内保留项 migrate 回 `backups/<save>`，删空 archive/<save>），再 create current（最多 3）。
- `FinishRestore` 成功后 `ReplayBackup.Delete(backupPath)` 删除当前临时备份；删除失败 → log warn（保留 stale 后续 prune），不把成功 replay 变失败。FailSafe 保留 backup 供人工 recovery（`Clear()` 不删）。
- `Delete`/`Prune` 只在 `Constants.DataPath/StardewGallery/backups*` 内路径操作（path boundary check），IO/Unauthorized 捕获并 log，cleanup failure 不影响 gameplay。
- 正常使用下 backups ≈ 0（成功 replay 后删除）；只保留紧急恢复用途。
- 旧 `backups-archive`：per-save migration/prune（不删其它 save 的 archive、不整目录删除 backups-archive 根）。

PersistenceChecks 增加 ReplayBackupRetention 测试；核心 Checks 增加 ReplayBackupRetention（0/1/2/3/10 stale → newest 2；discard old）。

### 运行时验证

- Windows runtime 已实机验证（PersistenceChecks open/schema/insert/read/reopen）。
- 移除 Mod 根目录手工 e_sqlite3.dll、仅保留 runtimes/win-x64/native 后 SQLite 正常。
- Linux/macOS 仅 resolver/path/package coverage + 报告注明，未声称 runtime-tested。
