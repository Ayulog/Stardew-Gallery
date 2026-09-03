# Stardew Gallery Phase 3：ConditionIR + Evaluator 实施任务书

日期：2026-09-03

## 0. 基线与性质

- 工作分支：`phase3/condition-ir-evaluator`
- 设计依据：`docs/PHASE3_ANALYSIS.md`
- 实施基线：`c423c03c68661f444e74a87481c3d3641d9e6c3a`（Phase 2 最终）
- 本任务书覆盖 analysis 中 §19 的 10 项开放决策。全部采用下文的 Codex 决议，实施时不再逐项询问。

Phase 3 目标：建立

```text
raw event conditions
    → ConditionIR
    → ConditionEvaluator
    → ConditionEvaluation
    → 玩家可读条件 / 当前进度差距
```

只加入「解释与评估」的只读分析层；不修改游戏状态、不启动模拟、不预览、不求解。

---

## 1. Codex 已确认决议（权威）

以下 23 条为已确认约束，实施时不得改变：

1. `ConditionTruth` 只保留 `True` / `False` / `Unknown`。
2. 新增 `ConditionKnowledge` = `Known` / `MissingData` / `Unsupported` / `Invalid` / `Error`；当 `Knowledge != Known` 时 `Truth` 必须为 `Unknown`。
3. `Source`、`Truth`、`Knowledge` 是三个正交维度。
4. `ConditionSource` = `LegacyEventPrecondition` / `GameStateQuery` / `OpaqueEventPrecondition` / `Synthetic`；不把未知来源猜成 `Mod`。
5. 使用 typed leaf nodes；共同基类保存 `Source` + `RawSegment` + `Negated`。
6. Phase 3 使用 `ConditionSet` 表示 ordered implicit-AND conditions；不实现通用 `AndCondition` / `OrCondition` / `NotCondition`。
7. 不修改 `ResolvedEvent`。
8. Condition analysis 必须 candidate-scoped；Phase 3 production 只分析 selected Current。
9. 不修改 `ResolvedEventIndex` candidate selection。
10. Core parser 不重新实现 Stardew raw-key / grammar。生产环境必须通过 `Event.SplitPreconditions(rawKey)`（或等价的注入 delegate）先得到 ordered segments，再交给 BCL-only parser。
11. Parser 对 unknown/malformed 必须保留完整 `RawSegment`，不丢失。
12. 不使用容易丢失 raw condition 的 `TryParse` API；`Parser` 应**始终**产生一个 leaf：known typed / `NativeQuery` / `Opaque`。
13. 增加 `ConditionPlayerScope`，至少保留 `LocalPlayer` / `HostPlayer` / `HostOrLocal` / `World` 语义；不得把 host condition 当 local condition。
14. `ConditionEvaluationContext` 是只读 snapshot，不持有 `Game1` / `GameLocation` / live object。
15. 暂不引入 `IConditionContextProvider`；生产组合用薄 factory / `Func`。
16. Typed deterministic conditions 从 snapshot 求值。
17. `NativeQueryCondition` 整段委托 Stardew `GameStateQuery` 原生 evaluator，不解析内部 GSQ。
18. Native GSQ evaluator 通过 delegate 注入；成功 → `Known + True/False`；异常 → `Error + Unknown`。不要套用 Index 的 exception→false policy。
19. `ConditionEvaluation` 不重复保存 `Source` / `RawToken`，通过 `Condition.Source/RawSegment` 获取。
20. `Gap` 只描述 state difference，不做 solver。Friendship 可 `NumericGap`；Time 用 `RequiredRange`，不计算 HHMM 数字差；Seen/Mail 用 `MissingState/OverState`；其余不确定项 `Unavailable`。
21. Friendship IR 保留原始 `points`；Readable projection 可同时暴露 `points`/`hearts`，由 UI 决定显示。
22. Phase 3 不修改现有 Gallery formatter / UI / i18n，不加 i18n parity 检查。
23. 不在 IR 中加入 `ConditionMutationClass`。Preview 可注入性以后由独立 capability / policy 层决定。

---

## 2. 第一批 typed MVP

唯一被 typed 解析并求值的条件：

| 语义（含 Negated 变体） | IR 节点 |
| --- | --- |
| Season / NotSeason | `Season` |
| DayOfMonth | `DayOfMonth` |
| Year | `Year` |
| Time | `Time` |
| Weather | `Weather` |
| Friendship | `Friendship` |
| SawEvent / NotSawEvent | `SawEvent` |
| LocalMail / NotLocalMail / HostMail / HostOrLocalMail | `Mail` |
| Dating | `Dating` |
| Spouse / NotSpouse | `Spouse` |
| Roommate | `Roommate` |
| DaysPlayed | `DaysPlayed` |
| GameStateQuery | `NativeQuery` |

`WorldState` 允许 typed parse；但只有存在明确可靠的 read path 时才求值，否则 `Unknown + MissingData`。

所有其他条件 → `OpaqueCondition`，`Truth = Unknown` + `Knowledge = Unsupported`。

---

## 3. 领域模型（BCL-only）

```csharp
internal enum ConditionTruth { True, False, Unknown }
internal enum ConditionKnowledge { Known, MissingData, Unsupported, Invalid, Error }
internal enum ConditionSource { LegacyEventPrecondition, GameStateQuery, OpaqueEventPrecondition, Synthetic }
internal enum ConditionPlayerScope { World, LocalPlayer, HostPlayer, HostOrLocal }
internal enum ConditionGapKind { None, NumericGap, RequiredRange, MissingState, OverState, Unavailable }
```

### 3.1 基本叶子

```csharp
internal abstract record ConditionExpression(ConditionSource Source, string RawSegment, bool Negated);
```

typed leaves 全部通过 `ConditionExpression` 基类挂 `Source` / `RawSegment` / `Negated`：

- `SeasonCondition(string[] Seasons, ...)`
- `DayOfMonthCondition(int[] Days, ...)`
- `YearCondition(int Min, ...)`
- `TimeCondition(int? Min, int? Max, ...)`
- `WeatherCondition(string Weather, ...)`
- `FriendshipCondition(string Npc, int Points, ConditionPlayerScope Scope, ...)`
- `SawEventCondition(string EventId, ...)`
- `MailCondition(string MailId, ConditionPlayerScope Scope, ...)`
- `DatingCondition(string Npc, ...)`
- `SpouseCondition(string Npc, ...)`
- `RoommateCondition(...)`
- `DaysPlayedCondition(int Min, int? Max, ConditionPlayerScope Scope, ...)`
- `WorldStateCondition(string Id, ...)`
- `NativeQueryCondition(string Query, ConditionPlayerScope Scope, ...)`
- `OpaqueCondition(...)`
- `ConditionSet(IReadOnlyList<ConditionExpression> Conditions)`

### 3.2 Evaluation / Knowledge / Gap

```csharp
internal sealed record ConditionEvaluation(
    ConditionExpression Condition,
    ConditionTruth Truth,
    ConditionKnowledge Knowledge,
    ConditionGap Gap
);
```

`ConditionGap`：

```csharp
internal sealed record ConditionGap(
    ConditionGapKind Kind,
    string? Target = null,
    string? Current = null,
    string? Detail = null
);
```

### 3.3 Context

```csharp
internal sealed record ConditionEvaluationContext(
    string? Season,
    int? DayOfMonth,
    int? Year,
    int? Time,
    string? Weather,
    IReadOnlyDictionary<string, int>? Friendship,
    IReadOnlySet<string>? EventsSeen,
    IReadOnlySet<string>? LocalMail,
    IReadOnlySet<string>? HostMail,
    IReadOnlySet<string>? HostOrLocalMail,
    IReadOnlySet<string>? Dating,
    IReadOnlySet<string>? Spouse,
    bool? Roommate,
    int? DaysPlayed,
    IReadOnlySet<string>? WorldState
);
```

所有字段可空；任何 null 都不能当 false。

---

## 4. Parser / Evaluator / Describer

### 4.1 Parser

- 输入：单个 raw segment（来自生产环境的 `Event.SplitPreconditions(rawKey)` 注入，或多个由 `ConditionSet` 组合)。
- 对每 segment 输出一个 leaf：known typed / `NativeQuery` / `Opaque`。
- `negated`（`!` 前缀）记录到 `Negated`。
- `ConditionSet` 表示 implicit AND 与顺序。

### 4.2 Evaluator

`ConditionEvaluationContext` 缺数据时：

| 情况 | Truth | Knowledge |
| --- | --- | --- |
| snapshot 字段 null | Unknown | MissingData |
| known typed 但未满足 | False | Known |
| 满足 | True | Known |
| Native query 成功 | True/False | Known |
| Native query 异常 | Unknown | Error |
| Opaque | Unknown | Unsupported |
| WorldState 无可靠 read | Unknown | MissingData |

### 4.3 Describer

- 输出可本地化 key + 参数（包括 points/hearts），不带 raw parser 泄漏。
- 未知条件保留 `RawSegment`。
- `Negated` 通过前缀词表达。
- 本阶段不接入 UI。

### 4.4 生产组合点

- `Event.SplitPreconditions(rawKey)` 注入 parser。
- `GameStateQuery.CheckConditions(query)` 注入 native evaluator。
- `ConditionEvaluationContext` 快照由薄 factory 提供（当前为空，只留组合点）。

---

## 5. 允许文件范围

新增（建议 `Conditions/`）：

- `Conditions/ConditionTruth.cs`
- `Conditions/ConditionKnowledge.cs`
- `Conditions/ConditionSource.cs`
- `Conditions/ConditionPlayerScope.cs`
- `Conditions/ConditionGapKind.cs`
- `Conditions/ConditionExpression.cs`
- `Conditions/ConditionGap.cs`
- `Conditions/ConditionEvaluation.cs`
- `Conditions/ConditionEvaluationContext.cs`
- `Conditions/ConditionParser.cs`
- `Conditions/ConditionEvaluator.cs`
- `Conditions/ConditionDescriber.cs`

修改：

- `Checks/Program.cs`
- `Checks/StardewGallery.Checks.csproj`
- `docs/PHASE3_TASK.md`
- `docs/PHASE3_REPORT.md`

若有极小 adapter 改动，需在最终报告说明。

**不得修改：** `ResolvedEvent`、`ResolvedEventIndex`、`GalleryCatalogCache`、`GalleryCatalogBuilder`、`EventOwnership`、`Replay*`、`WatchedEventHistory`、`GalleryCharacterMenu`、`GalleryMenu`、`ModEntry`、UI 资产、i18n、manifest、config、release materials。

---

## 6. Checks 最低要求

- Parser：known token（长名 + 短码 `f`/`e`）、negation、malformed args、unknown→Opaque、raw preserved、ordering、multiple=AND、dotted/modded IDs、case rules。
- Evaluator：satisfied / unsatisfied / unknown / unsupported / error；friendship gap；date/time gap；seen/not seen；mail/not mail；relationship；missing data；provider exception；WorldState 未读→MissingData；GSQ 委托成功/异常。
- Describer：deterministic；no raw leakage；unknown fallback；nullable points/hearts。
- Integration：`RawEventKey → parser → IR → context → evaluator → evaluation`；不改变 raw key / identity / hash / dedup / ordering；unknown ≠ false；不触发 `Game1`/`GameLocation`；Index selection 不受影响。
- 保持 BCL-only source-link；不加 test framework。

本阶段不新增 i18n parity 检查（决议 22）。

---

## 7. Phase 3 明确 out-of-scope

- PreviewState / PreviewPlan / StateInjector。
- SaveGuard 扩展。
- Replay refactor / unified EventLauncher（Phase 6）。
- SQLite。
- ObservedVariant / HistoricalEventRecord。
- CP passive / active discovery。
- future variant enumeration / variant explorer。
- planner / solver / route planning / 自动求解。
- simulated replay。
- UI 大改版 / formatter 重写 / i18n 改动。
- 修改 Phase 2 candidate selection。
- 重写 Stardew GSQ。
- 任何 `ConditionMutationClass` 或注入安全策略。

---

## 8. 验证命令

实现完成后运行：

```powershell
dotnet build -c Release
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
git diff --check
```

---

## 9. 报告

创建 `docs/PHASE3_REPORT.md`，记录：实施基线与最终 commit；新增/修改文件；领域模型；parser/evaluator/describer；source/scope 处理；GSQ 边界；build/check/diff 结果；未修改模块；尚需人工验证内容。
