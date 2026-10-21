# Stardew Gallery Phase 3：ConditionIR + Evaluator 实施报告

日期：2026-09-03

## 1. 实施基线与 commit

- 工作分支：`phase3/condition-ir-evaluator`
- 设计基线：`48e75e569b023414faa4664c3f9d695accca4608`（Phase 3 analysis commit）
- 设计文档：`docs/PHASE3_ANALYSIS.md`
- 任务书：`docs/PHASE3_TASK.md`（固化 Codex 23 条决议）
- 本报告将随 implementation commit 一起进入当前分支。

## 2. 新增 / 修改文件

### 新增（`Conditions/`）

- `ConditionTruth.cs`
- `ConditionKnowledge.cs`
- `ConditionSource.cs`
- `ConditionPlayerScope.cs`
- `ConditionGapKind.cs`
- `ConditionExpression.cs`
- `ConditionGap.cs`
- `ConditionEvaluation.cs`
- `ConditionEvaluationContext.cs`
- `ConditionParser.cs`
- `ConditionEvaluator.cs`
- `ConditionDescriber.cs`
- `ConditionProduction.cs`
- `docs/PHASE3_TASK.md`
- `docs/PHASE3_REPORT.md`

### 修改

- `Checks/Program.cs`
- `Checks/StardewGallery.Checks.csproj`

### 受保护模块确认未修改

`ResolvedEvent`、`ResolvedEventIndex`、`GalleryCatalogCache`、`GalleryCatalogBuilder`、`EventOwnership`、全部 `Replay*`、`WatchedEventHistory`、`GalleryCharacterMenu`、`GalleryMenu`、`ModEntry`、i18n、manifest、config、release materials —— 均无 diff。

## 3. 实际层次结构

```text
RawEventKey
    → (production: Event.SplitPreconditions injected delegate)
    → ConditionParser (BCL-only)
    → ConditionSet of ConditionExpression (typed leaf nodes)
    → ConditionEvaluator (snapshot) + injected native GSQ delegate
    → ConditionEvaluation (Truth + Knowledge + Gap)
    → ConditionDescriber → ReadableCondition (key + args + raw fallback + Negated)
```

生产组合点：`ConditionProduction.CreateParser(splitPreconditions, splitArguments)`、`CreateEvaluator(checkNativeQuery)`。委托均为薄注入，无 `IConditionContextProvider`；`ConditionEvaluationContext` 由未来 UI / factory 构造（当前不接入 UI）。

## 4. 领域模型实况

### 4.1 Truth / Knowledge / Source

- `ConditionTruth`：`True` / `False` / `Unknown`（三态）。
- `ConditionKnowledge`：`Known` / `MissingData` / `Unsupported` / `Invalid` / `Error`（五态）。
- 不变式：`Knowledge != Known` ⇒ `Truth == Unknown`（Evaluator 所有非 Known 路径返回 Unknown）。
- `ConditionSource`：`LegacyEventPrecondition` / `GameStateQuery` / `OpaqueEventPrecondition` / `Synthetic`。未知 token 一律 `OpaqueEventPrecondition`，不猜测为 Mod。
- `ConditionPlayerScope`：`World` / `LocalPlayer` / `HostPlayer` / `HostOrLocal`。Host 条件不与 Local 混用。

### 4.2 Typed leaves（基类 `ConditionExpression(Source, RawSegment, Negated)`）

实现的 typed 节点：

- `SeasonCondition`（负向 `z`→Negated）
- `DayOfMonthCondition`
- `YearCondition`
- `TimeCondition`
- `WeatherCondition`
- `FriendshipCondition`（Local；短码 `f` 别名）
- `SawEventCondition`（短码 `e`；负向 `k`）
- `MailCondition`（`n`/`LocalMail` Local、`l` 负向、`HostMail` Host、`HostOrLocalMail` HostOrLocal）
- `DatingCondition`（`D`）
- `SpouseCondition`（`O` 正向、`o` 负向）
- `RoommateCondition`（`R`）
- `DaysPlayedCondition`（Host；`j`；单整数，无 Max）
- `WorldStateCondition`（typed parse；无可靠 read path 时 Unknown+MissingData）
- `NativeQueryCondition`（GSQ；`G`；无 PlayerScope，player/world target 委托原生）
- `OpaqueCondition`（未知/畸形，保留完整 RawSegment）
- `ConditionSet`（ordered implicit-AND 容器）

### 4.3 解析规则实况

- `ParseRawKey(rawKey)` 先经 `splitPreconditions(rawKey)` 得到 segments，再 `Skip(1)` 跳过 EventId，交由 `Parse`。`Parse` 假定输入已是纯 condition segments，不再 skip。
- Constructor 注入两个 delegate：`splitPreconditions`（对应 `Event.SplitPreconditions`）与 `splitArguments`（对应 `ArgUtility.SplitBySpaceQuoteAware`）。Parser 自身不实现 quote parser；`Conditions/` 保持 BCL-only。
- `!` 前缀 toggle（`!!Season` → 非 negated）。
- alias 取反语义与 `!` 前缀按 XOR 合并（`!k 123` → SawEvent 正向；`!!k 123` → negated true）。
- 短码为官方 deprecated 别名表（大小写敏感）；长名经 `ToUpperInvariant()` 大小写不敏感匹配；参数原样保留大小写。
- Strict arity：已知 condition 只有在参数形状符合本阶段支持语义时才生成 typed leaf，否则保留完整 `RawSegment` 生成 `OpaqueCondition`（宁可 Unsupported，不允许错误 Known）。具体：Season 1+；DayOfMonth 1+ 合法整数；Time 恰好 2 整数；Year 恰好 1 整数；Weather 恰好 1；Mail 恰好 1；Dating 恰好 1；Spouse 恰好 1；Roommate 0；WorldState 恰好 1；DaysPlayed 恰好 1 整数（无 Max）；Friendship 单 pair；SawEvent 单 EventId；GameStateQuery query 非空。
- 所有 unknown/malformed 路径均保留完整 `RawSegment`，Parser 始终返回 leaf。

### 4.4 Evaluator 边界

- Typed 确定性条件从只读 snapshot 求值；缺数据 → `Unknown + MissingData`。
- Year 原生语义：`Year 1` 仅当 currentYear==1；`Year N (N>1)` 为 `currentYear >= N`。
- Native GSQ：注入 `Func<string, bool>`；成功 → `Known + True/False`；异常 → `Error + Unknown`（**不套用** Index 的 exception→false）。
- Opaque → `Unknown + Unsupported`；`ConditionSet` 整体求值 → `Invalid`（set 由 UI/查询侧逐节点聚合，不在本层求值）。
- Negated：仅当 `Knowledge == Known` 时翻转 Truth；翻转后 True → `NoGap`；翻转后 False（原条件满足）→ `OverState`。

### 4.5 Gap 规则实况

- `NumericGap`：Friendship（原始 points）、Year、DaysPlayed。
- `RequiredRange`：Time（字符串显示 `下界..上界`，不做 HHMM 数字差）。
- `MissingState`：SawEvent、Mail、Dating、Spouse、Roommate、Weather、Season、DayOfMonth、WorldState 等未满足。
- `OverState`：negated 原条件满足时。
- `None`：满足时。
- `Unavailable`：native false、Opaque、Unknown 时。

### 4.6 Describer 实况

- `ReadableCondition(LocalizationKey, Arguments, RawFallback, Negated)`。
- Friendship 同时暴露 `points` 与 `hearts`（`ceil(points/250)`），由 UI 决定显示（决议 21）。
- 未知 / GSQ → `LocalizationKey=null` + `RawFallback=RawSegment`。
- DaysPlayed → key `condition.daysplayed`，仅 `min`（无 Max）。
- Dating / Spouse / Roommate → `RawFallback`（不再复用语义错误的 `condition.present` / `condition.other`；未来 UI integration 阶段再新增专用 i18n key）。

## 5. 验证结果

### Release build

```powershell
dotnet build -c Release
```

结果：成功，0 warnings，0 errors。`StardewGallery.dll` 与 1.0.0 build ZIP 正常生成（Checks 之外的既有 `NETSDK1138` 因 net6.0 目标沿用，未修改 target）。

### Checks

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
```

结果：`Stardew Gallery checks passed.`，仅既有 `NETSDK1138`。

覆盖（新增 phase3 部分）：

- parser：长名 + 短码、alias 负向语义、`!` toggle、`!!` 双取反、case-insensitive 长名、case-sensitive 短码、malformed/unknown→Opaque、RawSegment 保真、多参 `f`/`e`→Opaque、ConditionSet ordering/AND、multi-segment 顺序。
- evaluator：各 semantic Truth、`NumericGap`（friendship exact points）、`RequiredRange`（time）、`MissingState`（seen/mail）、`OverState`（negated satisfied）、`None`、missing data → `Unknown+MissingData`、Opaque → `Unknown+Unsupported`、native 成功/异常/未注入、WorldState MissingData、negated 成功 NoGap。
- describer：deterministic、negated 标记、daysplayed key、opaque raw fallback、points/hearts 并存。
- integration：rawKey → parser → IR → context → evaluator，断言不触碰 Index selection（Index 对应 Checks 无 phase3 附加）、不触发 Game1/GameLocation（Conditions/ 零 Stardew 引用）、unknown ≠ false。

### Diff check

```powershell
git diff --check
```

结果：成功，无 whitespace errors。

## 6. 未修改的重要模块

- `ResolvedEvent`（Domain）—— 条件解析不写入 domain record。
- `ResolvedEventIndex` 与 Phase 2 候选 selection —— 未改；Phase 3 是并行新增分析层。
- `EventOwnership`、`GalleryCatalogBuilder` —— 未改；ownership 仍是独立启发式。
- `Replay*`、`WatchedEventHistory`、UI、i18n、manifest、config、release —— 未改。
- 未实现 ConditionIR 接入 UI（Phase 3 仅 production seam，不接 formatter）。

## 7. 尚需人工验证 / 后续接入

- 生产组合点尚未接 UI：`GalleryCharacterMenu` 的 formatter 仍是原白名单；ConditionIR 结果未展示。
- 未来 UI 接入时需验证 `ReadableCondition` key 与 i18n 文案（可选新增 key）。
- native GSQ 注入点尚未在生产 UI 流程中实例化；`GameStateQuery.CheckConditions` 的真实绑定与调用时机留待 UI 接入阶段。
- 多玩家、HostMail / HostOrLocalMail 实际值来自 snapshot 的时序需要实机确认。

## 8. 非阻塞说明（记录，不扩范围）

- parser/describer 是 BCL-only；Checks 的 `Check()` helper 增加了 message + CallerLineNumber 可选参数（`Program.cs` 尾部），属允许的极小 adapter 改动（任务书 §5「极小 adapter 说明」）。
- Friendship 多 pair 是合法 Stardew native syntax，但本 MVP 继续 Opaque。
- SawEvent 多 ID 是合法 Stardew native syntax，但本 MVP 继续 Opaque。
- negated 非核心类型（Spouse/Dating/Roommate/DaysPlayed）未满足时 OverState 的 detail 可为空（`leafOverReason` 仅覆盖 Seen/Mail/Friendship/Time）；仅显示信息缺省，不影响 Truth 正确性。
- `Season Spring, Summer` 逗号参数被空格切分（`"Spring,"`），属 parser 不重写 Stardew grammar 的边界；该类逗号形式由 GSQ/native 处理，不在本阶段承诺。

## 9. 结论

Phase 3 BCL-only ConditionIR + Evaluator 已按 Codex 23 条决议实施并通过全部自动验证。它作为并行只读分析层，不影响 Phase 2 Index selection、ownership、Replay、History、UI 与持久化。生产 UI 接入与实机语义验证留待后续阶段。

## 10. Semantic-correctness correction（第二轮）

本轮为语义正确性修正，不引入新能力，也未触达 UI / i18n / Replay / History / ResolvedEvent / ResolvedEventIndex / Phase 2 selection。

修正内容：

1. **ParseRawKey EventId**：`ParseRawKey` 现先 `splitPreconditions(rawKey)` 后 `Skip(1)` 跳过 EventId；`Parse` 保持「输入已是纯 condition segments」不变。调用方不再需要预先 Skip。
2. **quote-aware argument boundary**：`ConditionParser` 增加 `splitArguments` 委托注入，Parser 自身不再用 `segment.Split(' ')`，也不实现 quote parser；生产语义对应 `ArgUtility.SplitBySpaceQuoteAware`。`ConditionProduction.CreateParser` 改为接收两个 delegate。
3. **Year==1 语义**：`Year 1` 仅在当前年为 1 时满足；`Year N (N>1)` 为 `currentYear >= N`。
4. **strict arity**：Time/Year/Weather/Mail/Dating/Spouse/Roommate/WorldState/DaysPlayed 参数形状不符时一律 Opaque，禁止静默丢弃参数。
5. **DaysPlayed 无 Max**：删除 `DaysPlayedCondition.Max`；`DaysPlayed <number>` 语义为 host days played >= number；Gap 继续 NumericGap（Target=required minimum，Current=current days）。
6. **NativeQuery 无 Scope**：删除 `NativeQueryCondition.Scope`，不再伪装为 World scope；player/world target 语义完全保留在 raw Query 中并委托 Stardew 原生。
7. **Describer 语义修正**：Dating/Spouse/Roommate 不再错误复用 `condition.present`/`condition.other`，改为 `RawFallback`；未来 UI 阶段再新增专用 i18n key。
8. **回归 Checks**：新增 ParseRawKey 不含 EventId、injected splitArguments 被调用、quoted GSQ 保真、各 arity Opaque、Year 1 语义、Dating/Spouse describer 不再返回 condition.present 等测试；NativeQuery 无 Scope 由类型删除在编译期保证（record 不再含 Scope 字段）。

保留的非阻塞限制（不扩范围）：

- Friendship 多 pair、SawEvent 多 ID 仍是合法 Stardew native syntax，但 MVP 继续 Opaque。
- negated 非核心类型 OverState detail 可为空。
- ConditionEvaluation 不强制构造器 Truth/Knowledge 不变式，Evaluator 保证即可。
- 不接生产 UI；不创建真实 ConditionEvaluationContext factory；不真实绑定 `GameStateQuery.CheckConditions` 到 UI。
- 不实现 Preview、Planner、SQLite、variant discovery 或 Phase 4。

## 11. Final cleanup（第三轮）

本轮为 Phase 3 最后一轮收尾，不引入新能力，未触达 UI / i18n / Replay / History / ResolvedEvent / ResolvedEventIndex / GalleryCatalog / ownership / Phase 2 selection。

内容：

1. **deprecated NotX long names**：补齐 `NotSeason`、`NotSawEvent`、`NotLocalMail`、`NotSpouse`，以及 `NotHostMail`、`NotHostOrLocalMail`、`NotRoommate`，全部映射到现有 typed leaf，`Negated` 与外部 `!` 前缀 XOR，与 `z`/`k`/`l`/`o` 短别名及现代 `!` 语法语义一致。strict arity 继续生效，非法参数仍 Opaque。
2. **empty splitArguments 健壮性**：`splitArguments` 返回空数组时返回保留完整 `RawSegment` 的 `OpaqueCondition`，不再读取 `tokens[0]`，避免 `IndexOutOfRangeException`。
3. **文档同步**：`docs/PHASE3_TASK.md` 修正 `DaysPlayedCondition`（无 Max）、`NativeQueryCondition`（无 Scope）、parser 双 delegate / `ParseRawKey` Skip(1) / strict arity、Year==1 语义、Dating/Spouse/Roommate RawFallback、Friendship multi-pair / SawEvent multi-ID 已知限制；本报告记录此轮 cleanup。

最终 documented limitations（不扩范围）：

- Friendship multi-pair → Opaque（合法 native 语法，MVP 未 typed）。
- SawEvent multi-ID → Opaque（合法 native 语法，MVP 未 typed）。
- negated 非核心类型 OverState detail 可为空。
- `ConditionEvaluation` 构造器不强制 Truth/Knowledge 不变式（Evaluator 保证）。
- 不接 `GalleryCharacterMenu`；不创建真实 `ConditionEvaluationContext` factory；不真实绑定 `GameStateQuery.CheckConditions`。
- 不新增 i18n；不实现 Preview / Planner / SQLite / variant discovery；不开始 Phase 4。
