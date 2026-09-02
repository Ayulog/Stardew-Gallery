# Stardew Gallery Phase 1 验收报告

日期：2026-09-02

## 1. 验收范围

- 分支：`phase1/domain-model-migration`
- 验收目标 commit：`39da0fbc1c45793a45e39b86208fd25f5f54f0e5`
- Commit 标题：`refactor: migrate event domain model`
- 验收开始时本地 HEAD 与 `origin/phase1/domain-model-migration` 一致，工作树干净。
- 本次收尾只执行验收命令并新增本报告，没有修改业务代码，也没有开始 Phase 2。

## 2. 实际命令与结果

### 2.1 Release build

命令：

```powershell
dotnet build -c Release
```

结果：成功，进程退出码为 0。

- `StardewGallery.dll` 成功生成到 `bin/Release/net6.0/`。
- ModBuildConfig 成功生成 `StardewGallery 1.0.0.zip`。
- 编译警告：0。
- 编译错误：0。

### 2.2 Checks

命令：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
```

结果：成功，进程退出码为 0，最终输出：

```text
Stardew Gallery checks passed.
```

检查期间出现 `NETSDK1138` 警告，原因是 Checks 项目仍以已停止支持的 `net6.0` 为目标。该警告未导致检查失败，本阶段未修改目标框架。

自动 Checks 覆盖并通过：

- `EventIdentity` 路径分隔符规范化与输入裁剪。
- AssetName 使用大小写不敏感语义，EventId 使用大小写敏感语义。
- 不同 AssetName、相同 EventId 不会合并，包括相同 LocationName 的 resolved events。
- `RootScriptHash` 与 `RootDefinitionHash` 使用完整 64 字符 SHA-256，并符合规定输入公式。
- `GalleryEvent` compatibility adapter 保留原属性面。
- 旧 `WatchedEventSnapshot` JSON 字段可反序列化，计算属性不会写入 JSON。
- 既有 EventKey、snapshot fingerprint、Ownership、Fragments、Layout、UI rules 与 Replay lifecycle checks。

### 2.3 Diff whitespace check

命令：

```powershell
git diff --check
```

结果：成功，进程退出码为 0，无输出。

## 3. 兼容性确认

- `EventIdentity` 的正式语义为 normalized AssetName + case-sensitive EventId；LocationName、raw event key 和 content hash 均不作为 identity。
- `GalleryEvent` 继续提供 `Identity`、`LocationName`、`AssetName`、`EventId`、`EventKey`、`Script` 与 `Fragments` 兼容属性。
- `WatchedEventSnapshot` 的 11 个持久化字段名和含义未改变：`LocationName`、`AssetName`、`EventId`、`EventKey`、`RootScript`、`EventAssets`、`Translations`、`Locale`、`Fingerprint`、`FirstWatchedAt`、`LastWatchedAt`。
- `Identity` 与 `Playback` 是 `[JsonIgnore]` 计算属性，不进入旧 save data。
- Save key 仍为 `watched-event-versions`，GZip + Base64 + JSON 列表格式未改变；typed dictionary key 不写入存档。
- `Fingerprint` 在 persisted DTO 中未重命名，只在 `HistoricalPlaybackBundle` 中映射为 `PlaybackHash`。
- `ReplayCoordinator` 未重构，`ReplaySnapshot`、`ReplaySaveGuard`、`ReplayLifecycleRules`、`ReplaySpeedPatches` 和 historical replay asset 注入逻辑未改变。
- `EventFragments` 与 `EventOwnership` 未重写。
- UI 布局、显示文本、操作流程、配置项、manifest、版本号与发布材料未改变。
- 未引入 SQLite、ConditionIR、PreviewState、StateInjector 或任何 Phase 2 及后续架构。

## 4. 仍需人工游戏内回归

以下项目未在本次命令行验收中执行，不能由 build 或 Checks 通过代替：

1. 画廊可从收藏菜单标签和快捷键正常打开。
2. 当前事件数量与修改前一致。
3. NPC 分组与修改前一致。
4. 事件详情页与条件文本显示正常且无变化。
5. 当前版本事件回放正常。
6. 历史版本事件回放正常。
7. 历史回放中的 `fork`、`switchEvent`、translations 与跨地点片段正常。
8. 既有 `watched-event-versions` 存档可在实际游戏存档中正常读取。
9. 新自然事件完整观看后仍会生成 watched version。
10. 回放结束后 Snapshot restore 行为不变。
11. SaveGuard 行为不变。
12. 多人模式仍保持现有禁止回放行为，没有新增兼容性声明。

## 5. 验收结论

Phase 1 自动验收通过。Release build、Checks 与 diff whitespace check 均成功；持久化兼容性由自动检查覆盖。最终用户流程仍需按上一节完成实际游戏内回归后，才能声明完整实机验收通过。
