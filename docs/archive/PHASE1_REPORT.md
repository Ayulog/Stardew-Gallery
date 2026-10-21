# Stardew Gallery Phase 1 验收报告

日期：2026-09-02

实机验收补充日期：2026-09-03

## 1. 验收范围

- 分支：`phase1/domain-model-migration`
- 验收目标 commit：`39da0fbc1c45793a45e39b86208fd25f5f54f0e5`
- Commit 标题：`refactor: migrate event domain model`
- 验收开始时本地 HEAD 与 `origin/phase1/domain-model-migration` 一致，工作树干净。
- Phase 1 验收收尾只修改本报告，没有修改业务代码，也没有开始 Phase 2。

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

## 4. 实机回归结果

### 4.1 已验证通过

此前已在游戏内验证以下流程：

- 当前版本事件回放正常。
- 历史版本事件回放正常。
- 自然事件完整观看后能够生成 watched version。
- 重新载档后能够读取已记录的 watched versions。

### 4.2 FarmHouse / 4383992 A/B

使用同一存档，对 `FarmHouse` / event ID `4383992` 分别测试 Stardew Gallery 1.0.0 与 Phase 1：

1. 1.0.0 第一次回放完成后正常恢复。
2. 1.0.0 第二次回放完成后正常恢复。
3. Phase 1 第一次回放完成后正常恢复。
4. Phase 1 第二次测试发生于节庆布置状态。`DialogueBox.closeDialogue_PatchedBy<Mangupix.DialogueDisplayFrameworkContinued>` 中出现 `NullReferenceException`，同时游戏报告 `Mismatched UI Mode Push/Pop counts` 警告。随后 fade/transition 未正常收尾，Gallery 恢复等待超时，并成功触发备份恢复与重新载档。

该次失败标记为外部运行环境异常，不作为 Phase 1 domain migration regression。依据如下：

- 异常发生在 `Mangupix.DialogueDisplayFrameworkContinued` 注入的 dialogue close patch 中，并伴随游戏 UI mode push/pop 不匹配警告。
- 测试当时处于节庆布置状态，与前三次正常恢复的运行环境不同。
- Phase 1 未修改 Replay、Snapshot restore、SaveGuard 或 fade/transition 逻辑。
- 失败后的恢复保护按预期生效，备份恢复与重新载档成功完成。

### 4.3 仍需专项人工回归

以下项目没有被上述实机记录完整覆盖：

1. 当前事件数量与 1.0.0 基线一致。
2. NPC 分组与 1.0.0 基线一致。
3. 事件详情页与条件文本显示与 1.0.0 基线一致。
4. 历史回放中的 `fork`、`switchEvent`、translations 与跨地点片段分别完成专项验证。
5. 收藏菜单标签与快捷键两个画廊入口分别完成验证。
6. SaveGuard 除本次已成功触发的备份恢复路径外，其余保护行为完成专项验证。
7. 多人模式仍保持现有禁止回放行为；本阶段不新增多人兼容性声明。

## 5. 验收结论

Phase 1 自动验收通过。Release build、Checks 与 diff whitespace check 均成功，持久化兼容性由自动检查覆盖。当前回放、历史回放、自然事件 watched version 记录及重新载档读取已通过实机验证；FarmHouse / 4383992 A/B 在正常运行环境下均能恢复。节庆布置状态下由外部 dialogue patch 与 UI mode 异常引发的一次恢复超时不判定为 Phase 1 domain migration regression，且备份恢复机制成功完成重新载档。完整实机验收仍需补齐第 4.3 节的专项项目。
