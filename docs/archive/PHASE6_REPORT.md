# Stardew Gallery Phase 6：Unified Exact-Script EventLauncher 实施报告

日期：2026-09-03

## 0. 基线

- 工作分支：`phase6/exact-script-event-launcher`
- 基线：`fa469339774653f7fbb758f2f9bdebcb032f3527`
- 依据：`docs/PHASE6_ANALYSIS.md`

## 1. 实现目标

彻底移除 Gallery current replay 对 `Game1.PlayEvent(entry.EventId, ...)` 的依赖，把 current replay 与 historical replay 统一经过 `EventPlayback → EventLauncher → new StardewValley.Event(exactRootScript, assetName, eventId, Game1.player)`。

严格遵守 `Event selection != Event launch`：EventLauncher 不按 EventId 查 Data/Events、不 checkEventPrecondition、不选 raw event key、不调用 `Game1.PlayEvent`、不知道 current/historical 区别。

## 2. EventPlayback

新增 `EventPlayback.cs`：

```csharp
internal sealed record EventPlayback(
    EventIdentity Identity,
    string LocationName,
    string RootScript
)
{
    internal string AssetName => Identity.AssetName;
    internal string EventId => Identity.EventId;

    internal static EventPlayback ForCurrent(ResolvedEvent resolved)
        => new(resolved.Identity, resolved.LocationName, resolved.ResolvedScript);

    internal static EventPlayback ForHistorical(WatchedEventSnapshot snapshot)
        => new(snapshot.Identity, snapshot.LocationName, snapshot.RootScript);
}
```

- 纯数据类型，只做字段映射，不做任何 Game1/content 查询。
- current：`RootScript = resolved.ResolvedScript`。
- historical：`RootScript = snapshot.RootScript`。

## 3. EventLauncher

新增 `EventLauncher.cs`：

```csharp
internal enum EventLaunchFailureKind { InvalidPlayback, LocationMissing, ConstructionFailed, SchedulingFailed }
internal sealed record EventLaunchFailure(EventLaunchFailureKind Kind, string Detail);
internal sealed record EventLaunchResult(bool Accepted, Event? Event, EventLaunchFailure? Failure);

internal sealed class EventLauncher
{
    internal EventLaunchResult TryLaunch(EventPlayback playback);
}
```

行为：

- validate playback（AssetName / EventId / LocationName / RootScript 任一为空 → InvalidPlayback）。
- resolve target location（缺失 → LocationMissing）。
- `new Event(rootScript, assetName, eventId, Game1.player)`（异常 → ConstructionFailed）。
- same-location：`Game1.globalFadeToBlack(() => { forceSnapOnNextViewportUpdate=true; currentLocation.startEvent(replayEvent); globalFadeToClear(); })`。
- cross-location：`LocationRequest.OnLoad += () => currentLocation.currentEvent = replayEvent` + `getDefaultWarpLocation` + `warpFarmer`。
- 调度异常 → SchedulingFailed。

Launcher 不接收 `WatchedEventSnapshot` / `HistoricalPlaybackBundle` / `HistoricalReplayAssets`。

## 4. same-location behavior

保持现有已验证语义（未改变）：`globalFadeToBlack` 内 `forceSnapOnNextViewportUpdate=true` → `startEvent(replayEvent)` → `globalFadeToClear()`。仅抽入 EventLauncher。

## 5. cross-location behavior

本轮实现保守保持 vanilla-compatible 当前语义（未改为 `startEvent`）：`getLocationRequest(location.Name)` → `OnLoad` 设 `currentLocation.currentEvent = replayEvent` → `getDefaultWarpLocation` → `warpFarmer`。与 `Game1.PlayEvent` 历史路径一致；现有 historical replay 亦为 direct assignment。

`startEvent` normalization 记录为 future hardening candidate，本轮不实现。

## 6. HistoricalReplayAssets

NO CHANGE。historical：`HistoricalReplayAssets.Activate(snapshot)` → `EventLauncher.TryLaunch(EventPlayback.ForHistorical(snapshot))`；Clear 沿用现有 ReplayCoordinator 生命周期。

## 7. ReplayCoordinator 修改

`TryStart` 修改：

- 移除 `Game1.PlayEvent(entry.EventId, ...)`。
- 移除 `StartHistoricalEvent(...)`。
- 统一构建 `EventPlayback`：current → `EventPlayback.ForCurrent(entry.Resolved)`；historical → `HistoricalReplayAssets.Activate(watchedVersion)` + `EventPlayback.ForHistorical(watchedVersion)`。
- `eventLauncher.TryLaunch(playback)`；Accepted → 成功；否则按 failure kind → 现有 error 映射 + Restore。
- `MapLaunchFailure`：InvalidPlayback → `replay.not-found`；LocationMissing → `replay.location-missing`；ConstructionFailed → `replay.failed`；SchedulingFailed → `replay.not-started`。
- 未重写 backup / snapshot / restore / FailSafe / speed / auto-advance / secondary detection / UI reopen / diagnostics。

保留 TryStart 开头的 location 预校验（与 launcher 的 LocationMissing 一致，冗余但保留）。

## 8. exact-script invariant

launcher 使用的脚本永久来自 `ResolvedEvent.ResolvedScript`（current）/ `snapshot.RootScript`（historical）。绝不 `ResolvedEvent → EventId → Data/Events → re-select`。EventLauncher 不接触 Game1.PlayEvent、selection、precondition。

## 9. 测试（BCL-only contract）

新增 `EventPlayback` 到 Checks，覆盖：

- P6-A current mapping：`ResolvedEvent(same EventId, RawEventKey=A, ResolvedScript=ScriptA)` → `EventPlayback.RootScript == ScriptA`。
- P6-B selection independence：存在 same EventId 候选 B（ScriptB），`ForCurrent(resolvedA)` 的 `RootScript == ScriptA`（不因 EventId 重选），候选 B script 不被使用。
- P6-C historical mapping：`snapshot.RootScript=HistoricalRoot` → `EventPlayback.RootScript == HistoricalRoot`。
- P6-D same abstraction：current/historical 都产生 `EventPlayback`（同一模型）。

EventLauncher 依赖 game runtime 无法进 BCL Checks；按任务书仅测纯 mapping/invariant，不引入大框架。

## 10. Regression boundary

未修改：HistoricalReplayAssets / ResolvedEventIndex / ConditionIR / EventFragments / SQLite / history persistence / ReplayBackup / ReplaySnapshot semantics / UI / manifest / version。current nested switch/fork 继续读 live content；historical nested 继续经 HistoricalReplayAssets frozen content。

## 11. Validation

- `dotnet build -c Release`：成功，0 warnings，0 errors。
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release`：`Stardew Gallery checks passed.`（仅既有 NETSDK1138）。
- `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release`：`Stardew Gallery persistence checks passed.`（仅既有 NETSDK1138）。
- `git diff --check`：无 whitespace errors。

## 12. Manual runtime tests still required

- P6-1 current replay：普通 vanilla/modded event 正常启动、结束、恢复。
- P6-2 historical replay：已有历史版本正常 exact replay。
- P6-3 same EventId multi-definition fixture：确认点已选 resolved variant 播放 exact selected script，不是当前状态重选的另一个 definition。
- P6-4 custom command：含 mod/custom event command 事件手工 new Event 后仍执行。
- P6-5 switch/fork：含 nested fragment 事件 current 与 historical 各验，无回归。

上述需实机确认（OpenCode 环境无法启动游戏，未伪称通过）。

## 13. Non-goals

未开始 Phase 7（Preview / StateInjector / SafetyFirewall expansion / CP1 / Planner / Solver）。未实现 cross-location startEvent normalization（future hardening candidate）。未扩展 ResolvedEvent 保存完整可注入 graph。
