# Stardew Gallery Phase 6：Unified Exact-Script EventLauncher 分析

日期：2026-09-03

## 0. 文档属性

- 工作分支：`phase6/exact-script-event-launcher`
- 基线：`9848093d1a0af302a9713e092b178409c62f69ec`
- 性质：只读分析。不实现代码；不新增 EventLauncher / 不修改 ReplayCoordinator launch path / 不删除 Game1.PlayEvent / 不改 HistoricalReplayAssets / EventFragments / ResolvedEvent / UI / SQLite / backup / manifest。
- 证据等级：`[REPO]` 仓库已证实；`[NATIVE]` 公共反编译 1.6-era 源码镜像 + 官方 wiki 证实（行号为 1.6 时代，最终以安装版 1.6.15 实机为准）；`[RUNTIME]` 需实机确认。

---

## 1. Current problem

- current replay 走 `Game1.PlayEvent(entry.EventId, location, out validEvent, checkPreconditions:false, checkSeen:false)` `[REPO] ReplayCoordinator.cs:67`。
- `Game1.PlayEvent` 会按 EventId 重新从 `Data/Events/<location>` 选择事件定义并构造 Event（还涉及 precondition/seen gating 与 location transition）。
- 然而 `ReplayCoordinator` 收到的 `GalleryEvent.Resolved` 已包含最终选中定义：EventIdentity、LocationName、RawEventKey、ResolvedScript、RootDefinitionHash、RootScriptHash `[REPO] GalleryCatalog.cs / Domain/ResolvedEvent.cs`。
- 因此 selection 已完成，launch 不应再按 EventId re-resolve。
- historical replay 已经是 exact root script：`new Event(snapshot.RootScript, snapshot.AssetName, snapshot.EventId, Game1.player)` `[REPO] ReplayCoordinator.cs:94`。

目标：Current / Historical 统一转为 exact playback spec → 同一 EventLauncher → Stardew native Event engine。

---

## 2. Existing launch paths（实测）

### 2.1 Current replay（ReproCoordinator.TryStart → same-diff location）

```text
Game1.PlayEvent(entry.EventId, location, out validEvent,
                checkPreconditions:false, checkSeen:false)   [REPO:67]
```

背后（Stardew 1.6 source）`Game1.cs:11792-11859`：

- 读取 location 事件资产 `Data/Events/<location>`。
- 匹配 `key.Split('/')[0] == eventId`。
- `checkSeen` → 命中 eventsSeen/eventsSeenSinceLastLocationChange 则 false。
- `checkPreconditions` → `location.checkEventPrecondition(key, false)`，空或 "-1" → false。
- same-location → `globalFadeToBlack(...)` → `forceSnapOnNextViewportUpdate=true` → `currentLocation.startEvent(new Event(locationEvents[key], eventAssetName, id))` → `globalFadeToClear()`。
- different-location → `LocationRequest.OnLoad += () => currentLocation.currentEvent = new Event(locationEvents[key], eventAssetName, id)` + `warpFarmer(...)`（**direct assignment，无 startEvent**）。

### 2.2 Historical replay（ReplayCoordinator.TryStart → historicalStart path）

```text
same-location:  globalFadeToBlack(() => { forceSnapOnNextViewportUpdate=true;
                   currentLocation.startEvent(replayEvent); globalFadeToClear(); });
different-location: request.OnLoad += () => currentLocation.currentEvent = replayEvent; warpFarmer(...);
```

`replayEvent = new Event(snapshot.RootScript, snapshot.AssetName, snapshot.EventId, Game1.player)` `[REPO:92-113]`。

所以仓库 current 与 historical 的 same/different-location 实现**已经高度相似**，只差 current 用了 `Game1.PlayEvent`（选定义）而 historical 用 exact `new Event(rootScript,...)`。

---

## 3. `Game1.PlayEvent` 行为（问题 A）

`Game1.PlayEvent(EventId, location, ...)` 完成：

1. Event definition selection：`Data/Events/<location>` 里 `key.Split('/')[0]==eventId` 匹配。
2. Seen checking：`checkSeen:false` 传入则跳过 seen gate。
3. Precondition checking：`checkPreconditions:false` 传入则跳过 precondition gate。
4. Event object construction：`new Event(locationEvents[key], eventAssetName, id)`。
5. Location transition：same-location → startEvent；different-location → OnLoad 设 currentEvent + warpFarmer。
6. `startEvent`/`currentEvent` setup：见 §4。

对 unified launcher 而言，selection（1）不应存在；seen/precondition（2,3）在 replay 场景下均应绕过（与现有 `checkPreconditions:false, checkSeen:false` 一致）。launcher 只需负责 (4)(5)(6)。

---

## 4. `new Event` + `startEvent` vs `Game1.PlayEvent`（问题 B）

### 4.1 `startEvent(Event)` 完整初始化（各版本一致）

`NATIVE` GameLocation.cs:16045-16096 `startEvent(evt)` 顺序：

1. 若 `Game1.eventUp || Game1.eventOver` → early return。
2. `currentEvent = evt`。
3. `ResetForEvent(evt)`：重置 eventPositionTileOffset，outdoor 重置 ambientLight。
4. 设 `evt.exitLocation = getLocationRequest(NameOrUniqueName, isStructure)`。
5. 下马（含碰撞重检）、清空 `textAboveHead`。
6. `Game1.eventUp=true; Game1.displayHUD=false; Game1.player.CanMove=false;` `showNotCarrying()`; 清 `critters`。
7. `player.autoGenerateActiveDialogueEvent("eventSeen_"+currentEvent.id)`。

direct `currentLocation.currentEvent = evt`（GameLocation.cs:386 字段）只设引用——不执行 (3)-(7)。

### 4.2 Event 驱动

`GameLocation.UpdateWhenCurrentLocation`（:4102-4123）在 `currentEvent != null` 时调用 `currentEvent.Update(...)`（不 gate eventUp）。直接赋值的 Event 也会开始 tick，但缺 HUD/eventUp/exitLocation/dismount/freeze/dialogue 序列初始化。

viewport 初始化不在 startEvent——来自 scene 第 3 组命令（music, <x><y>, actor setup）与 `forceSnapOnNextViewportUpdate` + `viewport` 事件命令。

### 4.3 结论（问题 B）

相对 `Game1.PlayEvent`，`new Event(script)+startEvent`：

- 缺：事件定义 selection（launcher 不需要）、seen/precondition gating（replay 应绕过）。
- 多：无（launcher 直接构造指定 script）。
- 需要 launcher 自己补：same-location 的 `forceSnapOnNextViewportUpdate=true` + fade orchestration（现有实现已带）；location transition（跨地图 warp）。

结论：**保留 `Game1.PlayEvent` 的去 selection 语义，用 `new Event(exactScript, assetName, eventId, player)` + `startEvent` / OnLoad 赋值实现 unified launcher，行为与历史 exact replay 一致。**

---

## 5. same-location launch（问题 C）

现有 current & historical same-location 都用 `startEvent(replayEvent)`（historical）或 PlayEvent 内部的 startEvent（current）。historical 路径：

```csharp
Game1.globalFadeToBlack(() => {
    Game1.forceSnapOnNextViewportUpdate = true;
    Game1.currentLocation.startEvent(replayEvent);
    Game1.globalFadeToClear();
});
```

验证：

- startEvent 正确设置 `currentEvent` 与 `eventUp=true` `[NATIVE-4.1]`。
- 执行 Event 原生初始化（ResetForEvent、exitLocation、dismount、freeze、dialogue auto）`[NATIVE-4.1]`。
- 保留 custom command handler（全局 registry，见 §7）。
- `markEventSeen / player / asset metadata` 由 `new Event(...)` 构造的 fromAssetName/eventId 携带；`autoGenerateActiveDialogueEvent("eventSeen_"+id)` 由 startEvent 调用 `[NATIVE-4.1]`——需注意 replay 不希望在 natural eventsSeen 中标记，`CheckRemark`：`markEventSeen` 默认；launcher 若需抑制可传可选 flag，但 Phase 6 第一版建议沿用现有（历史 replay 已走该路径且不产生 historical record）。

结论：**KEEP（保留为统一 launcher 的 same-location 实现）**。它是现有历史 replay 已验证路径，语义正确。REFACTOR 只是把它抽为共享方法。

---

## 6. cross-location launch（问题 C）

现有 cross-location：

```csharp
request = Game1.getLocationRequest(location.Name)
request.OnLoad += () => Game1.currentLocation.currentEvent = replayEvent
Game1.warpFarmer(...)
```

对比 `startEvent` vs `direct currentEvent assignment` 在 OnLoad 后：

- `currentEvent = evt` 只设引用，跳过 startEvent 的全部初始化 `[NATIVE-4.1]`。尤其 `eventUp` 保持 false、HUD 不清、exitLocation 未设、dismount/freeze/dialogue 未执行。
- 《Event 驱动》显示 `currentEvent != null` 时仍会 tick，但缺 HUD/eventUp/exit 初始化。
- **这正是 `Game1.PlayEvent` 自身 cross-location 路径采用的**（Game1.cs:11832-11843 也是 OnLoad 直接设 currentEvent，无 startEvent）。

因此仓库 cross-location 直接赋值与 `Game1.PlayEvent` 底层行为**一致**（vanilla 也这么走）。

但是：直接赋值会在 `eventUp=false` 下事件开始 `UpdateWhenCurrentLocation` tick；`ReplayCoordinator.cs:155` 用 `Game1.CurrentEvent is not null || Game1.eventUp` 判断 running，覆盖了直接赋值场景。

推荐：**A/B 二选一，倾向 B（cross-location OnLoad 后调用 `startEvent(replayEvent)`）**，但需标注风险：

- A. 跨地图继续 direct `currentEvent` assignment —— 与 vanilla `PlayEvent` 路径一致，但与 same-location `startEvent` 初始化不一致（差 eventUp/HUD/exitLocation）。
- B. 跨地图 OnLoad 后调用 `startEvent(replayEvent)` —— 统一初始化语义；需 `forceSnapOnNextViewportUpdate` + fade，且注意 `startEvent` 的 early-return（若 eventUp 已置真）与可能需要的 exitLocation（getLocationRequest(当前) 已正确）与 `resetForEvent`。
- C. 其他明确流程。

**推荐 B**，理由：统一 same/diff-location 初始化语义，避免 cross-location 事件缺 eventUp/HUD 导致 UI 状态异常；且与 same-location `startEvent` 保持一致。但跨地图 `startEvent` 在 `OnLoad` 回调时机（location 就绪后）执行是可行路径。标记 **CODE EVIDENCE + RUNTIME CONFIRMATION REQUIRED**——需实机 smoke 确认跨地图 startEvent 的 viewport/fade 时序（P6-2）。

当前修复保守建议：若实机确认 B 有回归，回退到 A（vanilla 语义），并在 P6 报告说明。最终给 B。

---

## 7. custom event commands（问题 D）

证据：

- Stardew 全局静态 `Event.Commands`（`StringComparer.OrdinalIgnoreCase`）与 `CommandAliases`，`RegisterCommand`/`RegisterCommandAlias`，`SetupEventCommandsIfNeeded`（首次反射 `Event.DefaultCommands` 静态方法），`tryEventCommand` 在每次命令 dispatch 时经 `Commands.TryGetValue(commandName)` 解析 `[NATIVE Event.cs:4181/4184/4432/4446/4580/4689/4732]`。
- `new Event(eventString, fromAssetName, eventID, farmer)` 只解析脚本 + 加 farmer actor，不影响 command resolution `[NATIVE:4732]`。
- 因此 custom/mod event commands 是 **进程级全局 registry**，任何 Event 实例（含手工构造）都走同一 `Commands` dispatch，天然兼容。

SMAPI 侧：无独立注册 API；标准做法是调用游戏静态 `Event.RegisterCommand`/`RegisterCommandAlias`（生命周期在 mod Entry，全局生效）`[DOC wiki §Using C#]`。

结论：**全局 registry，exact `new Event` launch 天然兼容 custom commands**。例外：若某 mod 的 command 依赖特定的 location/world 状态（而非仅命令名），同一命令在 replay 上下文可能表现不同——这是行为差异，不是 registry 兼容问题；属 `[RUNTIME]` 需实机验证（P6-4）。

---

## 8. switchEvent / fork（问题 E）

- `Fork`（`Event.cs:2002-2089`）：非 festival 读 `assetName = "Data\\Events\\" + Game1.currentLocation.Name` → `Game1.content.Load<Dictionary<string,string>>(assetName).TryGetValue(newKey, out raw)` → `@event.ReplaceAllCommands(commands)`；translation 路径用 `Game1.content.LoadStringReturnNullIfNotFound(newKey)`。
- `SwitchEvent`（`:2092-2142`）：同样从该 location asset 加载 → ReplaceAllCommands。
- `ChangeLocation`（`:1096`）：跨地图继续剩余脚本。

因此 fork/switchEvent 在运行时从 **live content asset**（`Data/Events/<location>` + translation）解析嵌套脚本，**不存于 root Event 实例内**：

1. exact root `Event` 启动后，fork/switchEvent 依然从 `Data/Events/<location>` 源读取——即**读取当前 live resolved content**。
2. current replay 若只 freeze exact root script 而不 freeze nested graph，switch/fork 会读 live content。
3. historical replay 的 `HistoricalReplayAssets`（`WatchedEventHistory.cs:315-369`）通过 `OnAssetRequested` 对 `Data/Events/...` 及 translation asset 做 late-edit 注入 `snapshot.EventAssets`/`Translations` —— 即 root + nested event asset + translation 都被冻结 `[REPO]`。
4. 统一 launcher 后：**Current replay 第一版只 freeze root exact script，nested 用当前 live resolved content 允许**；**Historical replay 维持 `HistoricalReplayAssets.Activate(snapshot)`（需在 launcher 外围完成）**。launcher 本身知道 current/historical。

结论（问题 E 对应）：
- fork/switch 从 live asset 解析——root-only freeze 让 nested graph 保持 live（符合 Phase 6 scope 允许）。`[NATIVE]`
- HistoricalReplayAssets 已足够保证 root+nested asset+translation 冻结。`[REPO]`
- 统一 launcher 后 HistoricalReplayAssets 应 **NO CHANGE**（仍由 ReplayCoordinator 在外围 Activate/Clear）。`[REPO]`
- 不设计完整 current graph freeze；仅当分析证明无法实现统一 launcher 才扩展 ResolvedEvent。本分析判断：**不需要扩展**（current graph freeze 用当前 live 内容即可）。

---

## 9. Phase 6 第一版 scope 锁定

- Current replay：root script exact；nested switch/fork 用当前 live resolved content。
- Historical replay：root exact；nested 用 captured frozen EventAssets + Translations。
- 不扩展 ResolvedEvent 保存完整可注入 graph（除非必要；当前判断不必要）。
- 统一 launcher 输入只含 `Identity / LocationName / RootScript`；historical frozen assets 由 `HistoricalReplayAssets.Activate(snapshot)` 在 ReplayCoordinator 外围（launcher 之外）完成。

---

## 10. EventPlayback proposed model（问题 F）

最小统一输入：

```csharp
internal sealed record EventPlayback(
    EventIdentity Identity,
    string LocationName,
    string RootScript
)
{
    internal string AssetName => Identity.AssetName;
    internal string EventId => Identity.EventId;
}
```

分析：

- `Identity` 提供 assetName + eventId（`new Event` 构造与 `exitLocation`/metadata 需要）。
- `LocationName` 提供 launch 目标（same/diff-location）。
- `RootScript` 提供 exact script（selection 已完成，launcher 不再按 EventId re-resolve）。

是否足够：

- 足够。launcher 不接触 `HistoricalPlaybackBundle`/`WatchedEventSnapshot`/`GalleryEvent`；这些只在 ReplayCoordinator 外围用（historical 时 `HistoricalReplayAssets.Activate(snapshot)`）。
- 印证 `ResolvedEvent.RootScriptHash/RootDefinitionHash` 只是诊断 metadata，不进 launch spec。

结论：**最小 `EventPlayback(Identity, LocationName, RootScript)` 足够**。若 future 需要 nested graph freeze 才扩展——本阶段不做。

---

## 11. EventLauncher proposed API（问题 G）

推荐：

```csharp
internal sealed record EventLaunchFailure(
    EventLaunchFailureKind Kind,
    string Detail
);

internal enum EventLaunchFailureKind
{
    LocationMissing,
    InvalidPlayback,
    ConstructionFailed,
    LaunchFailed
}

internal sealed class EventLauncher
{
    internal EventLaunchResult TryLaunch(EventPlayback playback);
}

internal sealed record EventLaunchResult(
    bool Success,
    Event? Event,
    EventLaunchFailure? Failure
);
```

设计要点：

- 不照搬 `Game1.PlayEvent` 的 `bool started, bool validEvent`（launcher 不再负责 selection）。
- `LocationMissing`：目标 location 解析失败。
- `InvalidPlayback`：playback.Identity assetName/eventId 空或 rootScript 空。
- `ConstructionFailed`：`new Event(...)` 异常。
- `LaunchFailed`：`startEvent` 或 OnLoad 路径异常 / 未实际启动。

同放一层：`TryLaunch(EventPlayback)` → `EventLaunchResult`，内部 same/diff-location 分支。失败分类稳定供 ReplayCoordinator 映射到恢复/报错。

---

## 12. ReplayCoordinator minimal integration（问题 H）

目标结构：

```text
TryStart(...)
  → build EventPlayback
      current:   FromResolved(entry.Resolved)
      historical: FromSnapshot(snapshot)
  → backup
  → snapshot
  → if historical: HistoricalReplayAssets.Activate(snapshot)
  → eventLauncher.TryLaunch(playback)
  → existing lifecycle / restore / reopen
```

保留在 ReplayCoordinator：

- backup / ReplayBackup
- ReplaySnapshot capture/restore
- speed / auto-advance / lifecycle
- UI reopen
- HistoricalReplayAssets Activate/Clear
- secondary event / transition handling

只把「current + historical event launch implementation」抽到 EventLauncher。ReplayCoordinator 不重写成新的大型状态机。

映射：

```csharp
EventPlayback.ForCurrent(ResolvedEvent resolved)
    => new(resolved.Identity, resolved.LocationName, resolved.ResolvedScript);

EventPlayback.ForHistorical(LegacyProjection/... snapshot)
    => new(snapshot.Identity(...), snapshot.LocationName..., snapshot.RootScript);
```

（Phase 6 只需 `EventPlayback` 两个 factory 语义，不绑死实现。）

---

## 13. Failure model（问题 I）

| 情况 | Kind | 行为 |
| --- | --- | --- |
| 目标 location 不存在 | LocationMissing | ReplayCoordinator 报「位置缺失」→ 不启动 |
| playback identity/rootScript 非法（空） | InvalidPlayback | 不启动，报「无效播放规格」 |
| `new Event(...)` 抛异常 | ConstructionFailed | 捕获，报错，走恢复 |
| startEvent/OnLoad 路径异常或事件未实际启动 | LaunchFailed | 捕获，报错，走现有 restore/backup |

保持：location missing → 现有 `replay.location-missing`；其他失败 → 现有 replay 报错 + restore。不要把 select/resolve 失败包进 launcher（launcher 不 selection）。

---

## 14. Automatic test plan（P6 tests）

- **P6-A exact selection independence**：纯规则/adapter seam。构造两个候选 same EventId / different RawEventKey / different Script，selector 选 Script A；断言 launcher input（`EventPlayback.RootScript`）/（BCL seam）构造的 script 是 A，不因当前状态 re-resolve。抽取 `EventPlayback.ForCurrent(ResolvedEvent)` 为 BCL-only（或用 `ResolvedEvent` 纯构造）测 script 保留。锁：selected script → launch spec，不发生 EventId re-resolution。
- **P6-B current/historical shared launcher**：断言 current 与 historical 走同一 `EventLauncher.TryLaunch`/`EventPlayback` 映射（结构/契约 seam）；current 用 `ResolvedEvent`，historical 用 `snapshot.RootScript`+identity。
- **P6-C historical overlay unchanged**：锁 `HistoricalReplayAssets.Activate/Clear/EventAssets/Translations` 行为不回归（现有 Phase 5 checks 已覆盖 snapshot/dedup；补 Activate/Clear 既有行为确认）。
- **P6-D launch failure mapping**：LocationMissing / InvalidPlayback / ConstructionFailed / LaunchFailed 映射稳定（必要时用 `EventPlayback` 纯验证 + observer 注入——因 Stardew runtime，尽量 BCL seam）。

BCL-only 边界：`EventLauncher` 依赖 `Game1`/`GameLocation`/`Event`，不能进 BCL Checks；把「构造 `EventPlayback`」「failure 判定」「location 命中」抽 BCL-only helper，Checks 测这些；runtime 部分靠 P6 实机 smoke。

---

## 15. Manual smoke plan（P6 runtime）

- **P6-1 current replay**：普通 vanilla / modded event → 正常启动、结束、恢复。
- **P6-2 historical replay**：已有历史版本 → 正常 exact replay。
- **P6-3 same EventId multi-definition fixture**：用可证明不同 raw key/script 的案例，点已选 resolved variant → 播放 exact selected script，不是当前状态重新选出的另一 definition。
- **P6-4 custom command**：含 mod/custom event command 的事件 → 手工 new Event 后仍执行。
- **P6-5 switch/fork**：含 nested fragment 的事件，current 与 historical 各验证，无回归。

---

## 16. Known limitations

- `Game1.PlayEvent` selection/precondition/seen 是 vanilla 路径；launcher 去除这些，current replay 需自行负责 precondition/seen bypass（现有 `checkPreconditions:false, checkSeen:false` 等价）。
- cross-location 直接赋值（vanilla `PlayEvent` 亦如此）缺 eventUp/HUD 初始化；统一为 startEvent 需实机确认时序（P6-2/P6-5）。
- fork/switch/changeLocation 在第一版 current replay 中嵌套内容保持 live（允许）；若未来需 freeze current graph，需扩展（不在 Phase 6）。
- Stardew native 行为以安装版 1.6.15 为准；本分析引用 1.6-era 反编译镜像与官方 wiki，最终需实机确认。
- `EventLauncher` 依赖 game runtime，无法进 BCL Checks；仅抽 BCL-only seam 可测。

---

## 17. Explicit non-goals

- 不删除 `Game1.PlayEvent`（保留其作为 vanilla 选择的对照，unified launcher 仅不再依赖它完成 replay selection）。
- 不重构 ReplayCoordinator 为大型状态机；只抽 launch。
- 不改 `HistoricalReplayAssets`（Phase 6 建议 NO CHANGE）。
- 不改 `EventFragments` / `ResolvedEvent` / UI / SQLite / backup / manifest / version。
- 不扩展 ResolvedEvent 保存完整可注入 graph（本阶段判断不必要）。
- 不 start Preview / StateInjector / SafetyFirewall expansion / CP1 / Planner / Solver / Phase 7。

---

## 18. 结论

统一 EventLauncher 可行：

- `Event select ≠ Event launch`；selection 由 ResolvedEventIndex/Catalog，launcher 只播放选定 exact script。
- `new Event(RootScript, AssetName, EventId, player)` 与 `startEvent` 可覆盖 same-location；cross-location 倾向改用 `startEvent`（统一初始化），但需实机确认（保守可回退 direct assignment）。
- custom/mod 命令为全局 registry，天然兼容。
- fork/switch 运行时读 live asset，root-only freeze 是允许的 Phase 6 第一版；historical 由 HistoricalReplayAssets 保证冻结。
- 最小输入 `EventPlayback(Identity, LocationName, RootScript)`；API `EventLauncher.TryLaunch(playback)` → `EventLaunchResult`；失败模型 4 类。
- ReplayCoordinator 最小改法：抽 launch 到 EventLauncher，其余保留。
- 边界：`[RUNTIME]` 需实机确认 cross-location startEvent 时序与 custom command 在 replay 上下文行为。
