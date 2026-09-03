# Stardew Gallery Phase 3：ConditionIR + Evaluator 审计与设计

日期：2026-09-03

## 0. 文档属性

- 工作分支：`phase3/condition-ir-evaluator`
- Phase 2 基线：`c423c03c68661f444e74a87481c3d3641d9e6c3a`
- 性质：只读源码审计 + 语义调查 + 最小架构设计 + 实施与测试计划。
- 本阶段不实现任何一个 ConditionIR / Evaluator，不修改业务代码。

### 证据分级

文档对每条语义都标注来源等级：

- `[REPO]` 本仓库源码 / 现有 fixture 已证实。
- `[NATIVE]` 本机 Stardew 1.6 官方 wiki / 游戏 API 文档证实，仓库样本未直接包含。
- `[SAMPLE]` 仓库或本地安装中可直接观察到的实际内容。
- `[SYNTH]` 仅用于测试结构的合成样例，不代表真实游戏语义。
- `[MOD]` 属于 mod / GSQ 扩展，仓库未使用。

---

## 1. 当前 condition 数据流

```text
GameLocation.checkEventPrecondition(rawKey, check_seen:false)
        ├─── EventAssetCatalog (Index candidate selection, live bool)
        ├─── GalleryCharacterMenu.FormatConditions (UI textual whitelist)
        └─── GalleryCatalogBuilder.ParseEvidence (ownership heuristic)

rawKey → EventKey.TryGetId → EventId（仅取首个 '/' 前缀，非条件解析）
```

即同一个 raw key 目前被「游戏原生意图」「UI 展示」和「ownership 归属」三套逻辑分别消费，彼此独立，没有共享的解析表示。Phase 3 引入的 ConditionIR 是新增的第四层，只服务于「解释与评估」，不替换上面任何一条现有路径。

### 1.1 各层读取什么

| 层 | 输入 | 输出 | 是否只读 |
| --- | --- | --- | --- |
| Index candidate selection | `location.checkEventPrecondition(rawKey, false)` | bool | 是 |
| UI formatter | `Event.SplitPreconditions(rawKey)` | localized 文本 | 是 |
| Ownership parser | `Event.SplitPreconditions(rawKey)`, quote-aware split | `EventEvidence` | 是 |
| Replay | 仅 `EventId` + `LocationName` | `Game1.PlayEvent(...)`，不重新判条件 | 否（启动） |

### 1.2 关键事实

- 当前收藏菜单条件文本始终基于 selected `GalleryEvent.EventKey`。选择历史版本不会切换为其 `WatchedEventSnapshot.EventKey` `[REPO]`。
- Current replay 通过 `Game1.PlayEvent(entry.EventId, location, ..., checkPreconditions: false, checkSeen: false)`，不重跑 Index selection，也不传 raw key / script `[REPO]`。因此 replay 与当前 candidate 定义并不绑定。
- 目前没有任何代码用「是否满足条件」来 launch 事件；Phase 3 也禁止这样做。

---

## 2. 当前 formatter / parser / evaluator 盘点

### 2.1 UI formatter（`GalleryCharacterMenu.FormatConditions`）

输入：`Event.SplitPreconditions(entry.EventKey).Skip(1)`，然后按空格 split（非 quote-aware）`[REPO]`。

| token | 特殊解释 | i18n key（EN / ZH） |
| --- | --- | --- |
| `f npc points` | 首对；整数；`ceil(points/250)` 心 | `condition.hearts` |
| `e id` | 首个 id | `condition.seen` |
| `t from to` | 整数→HH:mm；否则原样 | `condition.time` |
| `w value` | 本地化已知 weather，否则原样 | `condition.weather` |
| `z season` | “季节不是” | `condition.season-not` |
| `y year` | “第 N 年或以后” | `condition.year` |
| `d day` | 原样 | `condition.day` |
| `p npc` | 原样 | `condition.present` |
| `M id` / `m id` | 原样 | `condition.mail` |
| 其他 | 一律 `condition.other` | “其他原始条件” |

已知本地化值：四个季节、weather 的 `sunny/rainy/stormy/snowy/wind`。

**要点：**
- 该 formatter 是**有损白名单**，不是 evaluator，也不是语法解析器。
- 短 token 大小写敏感；`Friendship`/`SawEvent` 长名、`!f` 取反、畸形已知条件、无效整数，全走 `condition.other`。
- 结果去重（`CurrentCulture`），所有未知条件折叠成一个“其他原始条件”。
- 空条件列表时仍输出 `location · `（空后缀）。

### 2.2 Ownership parser（`GalleryCatalogBuilder.ParseEvidence`）

- 仅识别 `f`/大小写不敏感 `Friendship`；消费所有完整 `npc points` 对，取每个 exact-case NPC 名最大值。
- 仅识别 `e`/大小写不敏感 `SawEvent`；将其余参数作为 prerequisite id。
- 任何以 `!` 开头的 segment 直接跳过。
- 其他条件一律忽略。
- 拥有 friendship evidence → Direct；无符合条件的 subject → Excluded（而非 Inferred）；predecessor 需唯一；ambiguous 同 EventId 不继承。

**警告：ParseEvidence 是给 ownership 用的启发式，绝不能当作完整 condition parser。** 它只是「读友谊和前置」的子集。

### 2.3 Index selection（`EventAssetCatalog` + `ResolvedEventIndex`）

- `location.checkEventPrecondition(fullRawKey, check_seen: false)` `[REPO]`。
- 结果转换：`null/""/"-1"` → false，其余（含 `"0"`、空白、`"-1 "`）→ true。
- exception → false；第一个 true 入选；全 false → candidate 0；dedup 后不重复 evaluator。
- 完整 Index 仅保留 `ResolvedEvent`，callbacks / GameLocation / fragment-root 均丢弃。

### 2.4 没有全功能 evaluator 的现状

仓库中不存在把「条件」整体换算成「满足 / 不满足 / 未知」的组件。`MatchesCurrentState` 只判断一个游戏返回 string；formatter 只做展示；ownership 只取子集。因此 Phase 3 的 `ConditionEvaluator` 是一个**真正新增**的概念。

---

## 3. 真实 raw event condition taxonomy

基于 event precondition 官方全词表 `[NATIVE]` 与 deprecated 别名表 `[NATIVE]` 整理。

### 3.1 事件 key 结构

```text
<event ID>/[preconditions]
```

- 以 `/` 分隔；每个 segment 是一个 precondition。
- 任何 precondition 可加 `!` 前缀取反。
- key 大小写不敏感区，但**参数大小写可能敏感**。
- 参数可含空格/斜杠，用引号包裹（如 `/GameStateQuery "SEASON Spring"/`）。
- 无 precondition 的事件尾部必须有斜杠，以区分 fork。

### 3.2 旧式 precondition 分组（完整词表 `[NATIVE]`）

**GameStateQuery / 查询式：**
- `GameStateQuery <query>`。

**世界 / 上下文（非玩家特定）：**
- `ActiveDialogueEvent <ID>`
- `DayOfMonth <number>+`
- `DayOfWeek <day>+`
- `FestivalDay`
- `GoldenWalnuts <number>`
- `InUpgradedHouse [level]`
- `NPCVisible <name>`
- `NpcVisibleHere <name>`
- `Random <number>`
- `Season <season>+`
- `Time <min> <max>`
- `UpcomingFestival <number>`
- `Weather <weather>`
- `WorldState <ID>`
- `Year <year>`

**当前玩家（Current player）：**
- `ChoseDialogueAnswers <dialogue ID>+`
- `Dating <name>`
- `EarnedMoney <number>`
- `FreeInventorySlots <number>`
- `Friendship <name> <points>+`
- `Gender <gender>`
- `HasItem <item ID>`
- `HasMoney <number>`
- `LocalMail <letter ID>`
- `MissingPet [pet]`
- `ReachedMineBottom [number]`
- `Roommate`
- `SawEvent <event ID>+`
- `SawSecretNote <number>`
- `Shipped <item ID> <number>+`
- `Skill <name> <level>`
- `Spouse <name>`
- `SpouseBed`
- `Tile <x> <y>+`

**主机玩家（Host player）：**
- `CommunityCenterOrWarehouseDone`
- `DaysPlayed <number>`
- `HostMail <letter ID>`
- `HostOrLocalMail <letter ID>`
- `IsHost`
- `JojaBundlesDone`

**Deprecated 别名（旧式短码，大小写敏感，不建议新用）`[NATIVE]`：**

| 别名 | 替代 | 别名 | 替代 |
| --- | --- | --- | --- |
| `a` | Tile | `n` | LocalMail |
| `b` | ReachedMineBottom | `O` | Spouse |
| `B` | SpouseBed | `p` | NpcVisibleHere |
| `C` | CommunityCenterOrWarehouseDone | `q` | ChoseDialogueAnswers |
| `c` | FreeInventorySlots | `r` | Random |
| `D` | Dating | `R` | Roommate |
| `e` | SawEvent | `S` | SawSecretNote |
| `f` | Friendship | `s` | Shipped |
| `G` | GameStateQuery | `t` | Time |
| `g` | Gender | `u` | DayOfMonth |
| `H` | IsHost | `v` | NPCVisible |
| `h` | MissingPet | `w` | Weather |
| `Hn` | HostMail | `y` | Year |
| `i` | HasItem | `*` | WorldState |
| `j` | DaysPlayed | `*n` | HostOrLocalMail |
| `J` | JojaBundlesDone | `l` | NotLocalMail |
| `L` | InUpgradedHouse | `k` | NotSawEvent |
| `m` | EarnedMoney | `z` | NotSeason |
| `M` | HasMoney | `o` | NotSpouse |
| `N` | GoldenWalnuts | …（尚有其他 NotX / X 格式） | |

> 注：本仓库真实数据里只会出现 `f`、`e`（以及作为 raw-key 示例的 `Season`），全部对应上述词表。仓库没有一处出现 `GameStateQuery` 或 `GSQ` 拼写 `[REPO]`；`GameStateQuery` 为官方命名，目录中仅在本分析文档出现。

### 3.3 本仓库实际出现的 raw keys `[SAMPLE]`

```text
75160185/f Alissa 500            (Checks/Program.cs:4)
123/f Haley 1000                 (Checks/Program.cs:97)
root/f Alissa 1000               (Checks/Program.cs:347)
root/f Bert 1000                 (Checks/Program.cs:356)
child/e root                     (Checks/Program.cs:357)
```

```text
SomeMod.Event42/Season Summer     (docs/PHASE1_TASK.md，键仅示例)
SomeMod.Event42/Season Winter
```

结论：仓库样本只覆盖 `Friendship`（短码 `f`）与 `SawEvent`（短码 `e`）两类 precondition。`Season` 只作为 raw-key 示例出现（未在 UI 分支处理，落 `condition.other`）。

### 3.4 合成样例（不视为游戏语义）`[SYNTH]`

`mod.event/id/condition`、`" /condition"`、`invalid`、`placeholder`、`alpha`/`beta`、`multi/a`、`exception/a`、`failure` 等，仅用于校验 `TryGetId`、过滤、dedup、selection 逻辑，**不代表**真实 precondition 语法。

### 3.5 分级结论

- 仓库**确定用到**：`Friendship`（f）、`SawEvent`（e）。
- 官方原生支持但**当前样本未确认**：`Season`、`Time`、`Weather`、`Year`、`DayOfMonth`、`DayOfWeek`、`GameStateQuery`、`DaysPlayed`、`LocalMail`、`Spouse`/`Dating`/`Roommate`、`WorldState` 等。
- Mod / GSQ 扩展：`GameStateQuery` 内嵌自定义查询、`GameStateQuery.Register`/`RegisterAlias` 注册的 query、以及任意未知 token（一律 Opaque）。

---

## 4. ConditionIR 推荐模型

### 4.1 方案 A：typed nodes

```csharp
internal abstract record ConditionExpression;
internal sealed record AndCondition(IReadOnlyList<ConditionExpression> Children) : ConditionExpression;
internal sealed record OrCondition(IReadOnlyList<ConditionExpression> Children) : ConditionExpression; // 未来保留，Phase 3 仅 And
internal sealed record NotCondition(ConditionExpression Inner) : ConditionExpression;
internal sealed record SeasonCondition(IReadOnlyList<string> Seasons, bool Negated) : ConditionExpression;
internal sealed record DayOfMonthCondition(IReadOnlyList<int> Days, bool Negated) : ConditionExpression;
internal sealed record TimeCondition(int? Min, int? Max, bool Negated) : ConditionExpression;
internal sealed record WeatherCondition(string Weather, bool Negated) : ConditionExpression;
internal sealed record FriendshipCondition(string Npc, int Points, bool Negated) : ConditionExpression;
internal sealed record EventSeenCondition(string EventId, bool Negated) : ConditionExpression;
internal sealed record MailCondition(string MailId, bool Negated) : ConditionExpression;
internal sealed record RelationshipCondition(string Npc, string Kind, bool Negated) : ConditionExpression;
internal sealed record DaysPlayedCondition(int Min, int? Max, bool Negated) : ConditionExpression;
internal sealed record WorldStateCondition(string Id, bool Negated) : ConditionExpression;
internal sealed record NativeQueryCondition(string RawQuery, bool Negated) : ConditionExpression;
internal sealed record OpaqueCondition(string RawToken, bool Negated) : ConditionExpression;
```

### 4.2 方案 B：统一 atom

```csharp
internal enum ConditionKind { Season, DayOfMonth, Time, Weather, Friendship, EventSeen, Mail, Relationship, DaysPlayed, WorldState, NativeQuery, Opaque, And, Or, Not }
internal sealed record ConditionAtom(ConditionKind Kind, IReadOnlyList<string> Arguments, bool Negated, ConditionSource Source);
```

### 4.3 评估

| 维度 | A typed nodes | B unified atom |
| --- | --- | --- |
| 可维护性 | 强；每种语义一个类型，属性和校验独立 | 弱；一个泛化 record 全塞 `Arguments`，字段语义散落 |
| evaluator dispatch | 强；`switch(expr)` / pattern match 每类型独立、可验证 | 弱；需要依 `Kind` 再解析 `Arguments`，难静态保证参数正确 |
| UI readable | 强；Describer 按类型生成，无二次解析 | 弱；Describer 必须把参数再解释一遍 |
| future PreviewPlan | 强；可给类型加 override 字段而影响面小 | 中；需在 atom 上扩展大量可选字段 |
| future SQLite | 中；typed 节点可序列化为结构化行 | 中；atom 也行，但字段含义靠编码 |
| modded/unknown | 好；`OpaqueCondition` 独立承载原始 token | 好；`ConditionKind.Opaque` |
| forward compatibility | 好；新增类型=新增 record，旧代码可忽略 | 中；`ConditionKind` 枚举扩展会触碰所有 switch |
| 对未知条件的保真 | 好；`OpaqueCondition.RawToken` 原样保留 | 好；`Arguments` 需额外存原文 |

### 4.4 明确推荐

**采用方案 A：typed nodes。**

理由：

1. 本阶段核心是「IR 描述语义，不是 UI 文本」。typed nodes 让语义直接落在类型上，Describer / Evaluator / 未来 Preview 不再需要做「猜参数含义」的二次解析。
2. ownership 解析器已经证明：用统一 string + 手工 split 做语义很容易出现「同一条件两种解释」（formatter 与 ownership 就分叉了）。typed 类型把「是什么」固化，避免两套解释再次漂移。
3. 未来 PreviewPlan 需要对**个别**条件注入 override。typed 类型可各自扩展，不会触碰全枚举。
4. 你只需要在 typed nodes 之上加一层统一的 `ConditionExpression`，仍能整体遍历、求值、描述，不损失集中化能力。

同时为所有节点**统一补 `bool Negated`**（方案 A 每个节点都带 Negated，而非只靠外层 `NotCondition`）。理由见 §4.5。

### 4.5 Negation 表达

两难：用 `NotCondition(ConditionExpression)` 包裹，还是每个原子节点带 `Negated`？

推荐：**每个叶节点自带 `Negated`，不使用外层 `NotCondition` 包装叶条件**。原因：

- event precondition 的 `!` 作用于单个 `segment`（如 `!Season Winter`），语义落在单条条件上。
- 让叶节点自己带 `Negated`，Describer 无需处理「NOT 包裹复杂表达式」的递归形态，Evaluator 也更简单。
- 复杂逻辑表达式（`And`/`Or`）目前只有隐式 And（多个 segment），无显式 Or；故不建议 Phase 3 引入通用 boolean 组合算子。`AndCondition` 可作为容器。
- 若未来出现显式 OR（某 token 本身或 mod），再引入 `OrCondition`；Phase 3 只保留 `AndCondition`。

---

## 5. ConditionSource（来源显式建模）

### 5.1 为什么需要

同一 token 可能来自三条完全不同的来源路径，影响「能否评估」与「能否接受注入」。必须显式区分：

```csharp
internal enum ConditionSource
{
    LegacyEventPrecondition, // 传统短码 / 长名 precondition
    GameStateQuery,          // GameStateQuery <query>，Stardew 原生查询
    ModdedOpaque,            // 自定义 token / mod GSQ，无 provider
    Synthetic                // 派生 / 测试来源
}
```

### 5.2 来源进入 IR node 吗？

**推荐：是。** 每个叶节点可含 `ConditionSource Source`（或由 `OpaqueCondition`/`NativeQueryCondition` 隐含）。理由：

- `NativeQueryCondition` 与 `OpaqueCondition` 天生需要 `Source`。
- 来源决定「可信度」和「可注入性」：Legacy 里我们自评；GameStateQuery 委托原生；Modded 未知。
- Phase 3 只需要**记录**来源，不实现任何 override。

### 5.3 进入 evaluation result 吗？

**推荐：是。** `ConditionEvaluation` 带 `Knowledge` 与来源，UI 才能表达「这是 mod 条件，无法判断」。见 §7、§8。

### 5.4 UI 需要暴露来源吗？

- 玩家可读文本：**不暴露**来源（减少杂讯）。
- 但要在未来「debug / raw view」暴露 raw source 与原始 token。
- Phase 3 只设计 API，不实现 UI 改造。

### 5.5 未来 Preview 需要来源吗？

需要。来源决定某条件是否 `injectable`（§13）。Phase 3 只在 metadata / 分类建议里记录倾向，不实现注入。

---

## 6. Truth / Knowledge / Error 语义

### 6.1 Truth（单条件结果）

推荐三态 + 两个异常态，共五态：

```csharp
internal enum ConditionTruth
{
    True,        // 满足
    False,       // 不满足
    Unknown,     // 无法判断（缺失数据 / 无 provider / 不可读）
    Unsupported, // parser 明确不认识，且判定为不支持
    Error        // 解析/求值抛异常
}
```

**明确：Unknown 与 Unsupported 必须区分。**

- `Unknown`：parser 认识该条件，但当前上下文缺数据（如 NPC 不存在、mail key 不存在）、或 provider 未注册、或语义上无法读取（mod 条件无提供方）。是「暂时无法判定」。
- `Unsupported`：parser 明确不认识该 token，也不打算支持。是「固定不支持其语义」。
- `Error`：求值过程中真的抛出异常。必须记录，不能吞成 Unknown 或 False。
- 三者对 UI 都显示「无法判断」，但对未来 planner/preview 处理不同：Unknown 可能未来变可解，Unsupported 基本不可解，Error 是 bug 信号。

### 6.2 简化为「Satisfied / Unsatisfied / Unknown」够吗？

**不够。** 简化版把 Unsupported / Error 都压进 Unknown，会丢失「为什么无法判断」，正是本阶段要避免的（§15「无法判断 == 不满足」）。

但为了 UI 稳定输出，可提供聚合视图 `Knowledge`：

```csharp
internal enum ConditionKnowledge
{
    Reliable,      // 结果可信（True/False 且来源可靠）
    Unknown,       // 结果不明确
    Unfetchable    // 因某项中断而无法给出结论
}
```

### 6.3 默认规则

- parser 不认识 token → `Unsupported`（保留原文）。
- parser 认识但无法读当前状态 → `Unknown`。
- native evaluator 抛异常 → `Error`（除非是 Index selection 的 exception→false 路径，那属于 selection，不由本层处理）。
- mod 条件无 provider → `Unknown`（来源 ModdedOpaque）。

---

## 7. ConditionEvaluation / ConditionGap 设计

Phase 3 不只给 bool，要给 UI 与未来 planner 一个稳定结果。推荐：

```csharp
internal sealed record ConditionEvaluation(
    ConditionExpression Condition,
    ConditionTruth Truth,
    ConditionKnowledge Knowledge,
    string? Detail,          // 人类可读补充（可选）
    string? RawToken,        // 原始 token（debug / raw view）
    ConditionGap? Gap        // 差距描述（可选）
);

internal sealed record ConditionGap(
    ConditionGapKind Kind,   // 缺多少 / 差在哪
    string? Target,          // 目标值
    string? Current         // 当前值
);
```

`ConditionGapKind`：

```csharp
internal enum ConditionGapKind
{
    None,
    NumericGap,     // Friendship/Time/Year/DaysPlayed 等差多少
    MissingState,   // 需要某状态（SawEvent/Mail/WorldState/Relationship）未达成
    OverState,      // 需要「未」某种状态但已达成
    Unavailable     // 无法提供差距
}
```

示例映射：

| 条件 | Truth | Gap Detail |
| --- | --- | --- |
| `Friendship Haley >= 2500`，实际 1750 | False | NumericGap，Target=2500，Current=1750 |
| `Time >= 1800`，实际 1420 | False | NumericGap，Target=1800，Current=1420 |
| `SawEvent 123`，未看过 | False | MissingState，Target=123，Current=false |
| 未知 mod 条件 | Unknown | Unavailable |

**本阶段不做自动求解**，只评估和描述差距，不填 gap、不改状态。

对复合 And / 多 segment：可为每条条件生成一个 `ConditionEvaluation`，由 UI/查询侧聚合（Phase 3 只提供 per-node evaluation，不强制做聚合文本）。

---

## 8. EvaluationContext（读取当前存档状态的边界）

不要让 evaluator 到处直接访问 `Game1.player`。设计一个只读 context。

### 8.1 Snapshot vs live provider

推荐：**快照式只读 context**，在打开 Catalog / 详情页时捕获一次。

```csharp
internal sealed record ConditionEvaluationContext(
    string? Season,
    int? DayOfMonth,
    int? Year,
    int? TimeMinutes,          // 26 小时制，或在 Date 层解析
    string? Weather,
    IReadOnlyDictionary<string, int>? FriendshipPoints,  // npc → points
    IReadOnlySet<string>? SeenEvents,
    IReadOnlySet<string>? Mail,
    IReadOnlyDictionary<string, string>? Relationships,  // npc → status
    int? DaysPlayed,
    IReadOnlyDictionary<string, bool>? WorldStateFlags,
    bool HasGameSave
);
```

理由：

- UI 打开时 snapshot 更合理：详情页是「此刻」的状态快照，不需要每 tick 变化。
- 避免 evaluator 持有 live game objects，便于未来单元测试与 future PreviewState 复用。
- 若某字段不可读，置 null → evaluator 判 `Unknown`，而不是 false。

### 8.2 Provider 接口（可选，未来扩展）

Phase 3 可只实现简单 `Func<ConditionEvaluationContext>` 生产组合；是否抽 `IConditionContextProvider` 交给 Codex 决定（见 §19）。默认方向是留一个薄组合点，不强制接口。

### 8.3 边界

- Multiplayer：当前只以 host/本机玩家快照为准；Phase 3 只读，不做 farmhand 语义协议。
- 无存档：`HasGameSave=false` → 全部未知。
- 未来 PreviewState：应能复用同一 `ConditionEvaluationContext` 作为基础再叠加 override（Phase 7）。

```text
默认方向：
ConditionEvaluationContext = 当前 catalog/detail 打开时的 read-only snapshot
```

---

## 9. Handler / Parser / Evaluator / Describer 架构

### 9.1 边界问题

本阶段核心是区分：

- **自有 evaluator**：负责需要详细 gap 的常用条件、能稳定读 save state 的条件、未来 Preview 可能要构造 override 的条件。
- **Stardew native evaluator**：负责 GSQ、复杂原生条件、mod 扩展条件、我们不该重写语义的条件。

### 9.2 推荐：三接口分离

```csharp
internal interface IConditionParser  // 纯，可 BCL-only 测试
{
    bool TryParse(string rawSegment, out ConditionExpression expression);
}

internal interface IConditionEvaluator  // 需要 context
{
    ConditionEvaluation Evaluate(ConditionExpression condition, ConditionEvaluationContext context);
}

internal interface IConditionDescriber  // 纯，可 BCL-only 测试
{
    string Describe(ConditionExpression condition);   // 返回可本地化 key + 参数，或直接文本
}
```

**不推荐单一 `IConditionHandler { Parse; Evaluate; Describe; }`**：

- Parse 是纯函数，Evaluate 需要 context，Describe 是纯且面向 UI。三者依赖与测试面差异大，塞进一个接口会强耦合。
- BCL-only Checks 需要能 source-link parser 与 describer，却不能引用游戏；单一接口会把 bound 拉高。

因此**Parser / Evaluator / Describer 分离**（三接口）。Implementations 通过 registry / dictionary 按 `ConditionExpression.GetType()` 或 `Kind` 分发。

### 9.3 分发

- `ConditionParserRegistry`: `raw segment` → `(parser, expression)`。
- `ConditionEvaluatorRegistry`: `expression.GetType()` → `evaluator`。
- `ConditionDescriberRegistry`: `expression.GetType()` → `describer`。

未知 token：parser 返回 `OpaqueCondition(rawToken, negated)`，并标 `Source=ModdedOpaque`（或 Legacy 未知）。

---

## 10. GSQ / Native Evaluator 边界

### 10.1 现状

Stardew 1.6 提供 `GameStateQuery.CheckConditions(query)`、`GameStateQuery.Exists(name)`、`GameStateQuery.Register(...)`、`GameStateQuery.RegisterAlias(...)` `[NATIVE]`。Event precondition 里的 `GameStateQuery <query>` 正是走原生查询。

### 10.2 推荐

**不要在 Phase 3 重写 GSQ parser。**

- `GameStateQuery <query>` → 解析为 `NativeQueryCondition(rawQuery, negated)`。
- Evaluator 对 `NativeQueryCondition` 委托 Stardew 原生：调用 `GameStateQuery.CheckConditions(rawQuery)`（或生产注入的 delegate）。
- 只保存 raw query string + native result。即使该 query 在语义上可被拆解（如 `SEASON Spring`），Phase 3 也不做；保留原生边界，避免与 Stardew 行为漂移。

之所以这样：

- GSQ 语法复杂、大小写不一致、参数可嵌套、可 mod 扩展；全文解析成本高且难以对齐 Stardew。
- 我们只关心「这个事件是否满足」，而 Stardew 已给了可靠答案。
- 未来 Preview 若需要注入 override，再针对**特定** query 走定制，不重写整个系统。

因此默认倾向：**保留 `NativeQueryCondition(raw)`，由 Stardew 原生 evaluator 给 truth。**

### 10.3 注入点

生产组合时，`GameStateQuery` 调用通过委托注入（`Func<string, bool> checkGameStateQuery`）。这样：
- BCL-only Checks 可用 fake 覆盖「call count、参数保真、异常策略」。
- 生产环境绑定到 `GameStateQuery.CheckConditions`。

---

## 11. ResolvedEventIndex 集成边界

### 11.1 Phase 2 已建立

`ResolvedEventIndex` 的 `Current` / `Candidates` 已把同 identity 的多条 raw key 按 order 分组，且保留「全 false → candidate 0」语义。这是 Phase 3 解释条件时的天然素材。

### 11.2 关键约束

- **candidate selection 继续使用 Stardew live precondition**，`ConditionIR` 不能替代它。
- Phase 3 用 ConditionIR 解释 selected / current event 的条件。
- 未来 variant explorer 才可能对 `Candidates` 分别分析条件；Phase 3 不做。

所以：现有 `EventAssetCatalog()` 的 `CheckPrecondition` live path 保持不变；ConditionIR 是并行的新增分析层。

### 11.3 与 EventIdentity / candidate 的关系

```text
EventIdentity
    1
    ↓
Current ResolvedEvent
    1
    ↓
ParsedConditions (analysis / projection)
```

条件实际来自 `RawEventKey`，而不是 identity 或 ResolvedEvent 本身。因此：

- **每个 Candidate 或 Current 的 raw key 都可单独解析出 ConditionIR**。
- 同 identity 的不同 candidate 可能有不同条件 → 条件分析必须 candidate-scoped，不是 identity-scoped。

### 11.4 不修改 ResolvedEvent

**推荐：不要修改 `ResolvedEvent` domain record，不要在其中存 ConditionIR。** 原因：

1. `ResolvedEvent` 被序列化进 diagnostics（`Catalog` graph）。新增可序列化字段会改变 `catalog-latest.json` 结构，违反 Phase 2 diagnostics 兼容约束。
2. `ResolvedEvent` 是「resolved content definition」，不是「interpretation」。条件解析是解释性 projection。
3. 未来缓存 / 失效策略会与 catalog snapshot 生命周期不同，混入 domain record 会造成双 cache 纠缠。

因此增加独立 **analysis / projection 层**：

```text
ConditionAnalysis
    ResolvedEvent Resolved
    IReadOnlyList<ParsedCondition> Conditions
```

或由 UI / 查询层在需要时调用 parser 临时生成，未来按需缓存。

---

## 12. readable condition 策略

### 12.1 目标

玩家不需要理解 raw precondition code。设计 readable model，供 Describer 生成本地化文本。

```csharp
internal sealed record ReadableCondition(
    string LocalizationKey,        // 如 "condition.hearts"
    IReadOnlyDictionary<string, string> Arguments,  // 如 { npc, hearts }
    string? RawFallback            // 无法本地化时的原始文本
);
```

### 12.2 要求 / 考虑

- localization：key 由 Describer 产出，文案仍在 i18n。
- NPC display name：`Npc` 用内部名，文本用 `NPC.getDisplayName`；Describer 输出参数，不直接拼（保持 BCL-only），由 UI 层翻译。
- heart vs friendship points：Describer 把 points 转 heart（`ceil(points/250)`）或保留 points，取决于 key；Phase 3 只提供参数。
- exact raw values：Opaque/Unsupported 保 `RawToken`。
- unknown/modded 回退：显示原文 + “无法翻译”。
- NOT：叶节点带 Negated，Describer 可按前缀词（如 “未”）生成。
- AND：多个 segment 用分隔符。
- 未来 OR：保留扩展点。
- debug / raw view：保留原始 token 视图（`RawToken`），不实现 UI。

---

## 13. 条件与「世界状态迁移」分离

Phase 3 只**记录**分类建议，不实现 override。把条件按「能否被 Future Preview 注入」分级：

| 条件 | 分类 |
| --- | --- |
| Friendship | future injectable（可直接改 points） |
| Time | future injectable |
| Weather | future injectable |
| EventSeen / NotSeen | future injectable with caution（会改变 seen 状态） |
| GameStateQuery 任意原生 query | analyze / native only |
| 自定义 mod GSQ | analyze / native only |
| 未知 token / 外部自定义状态 | unknown |

Phase 3 只在 `ConditionExpression` 或 metadata 上记录一个 `ConditionMutationClass`（可选），不实现 `PreviewOverride`。

```csharp
internal enum ConditionMutationClass
{
    Injectable,       // 未来可安全注入
    InjectableCautious, // 需谨慎（如 seen event）
    AnalyzeOnly,      // 只读分析，不注入
    UnknownInjection // 未知
}
```

---

## 14. Failure semantics

定义并固定：

| 情况 | Truth / Knowledge | 行为 |
| --- | --- | --- |
| Parse failure（畸形已知条件） | Unsupported / RawFallback | 记录原始 token，不崩，不误判 false |
| Unsupported condition（不认识 token） | Unsupported | 保留原文 |
| Evaluation failure（求值抛异常） | Error | 记录，不吞成 Unknown / False |
| Missing referenced NPC | Unknown | Friendship / Relationship 无法读 → Unknown |
| Missing mail / world key | Unknown | 无法读 → Unknown |
| Native evaluator exception | Error（或委托层捕获为 Unsupported/Unknown，见 §10） | 由委托层决定，保留原文 |
| No save loaded | Unknown（全部） | 整项未知 |
| Multiplayer ambiguity | Unknown（部分） | 只读 host/本机快照，无法确认的判 Unknown |

**总目标：**
- 不崩 Gallery。
- 不把 unknown 当 false。
- 不影响事件正常 gameplay。
- 不影响 Phase 2 Index。
- UI 能明确显示“无法判断”。

**Index selection 例外：** `EventAssetCatalog.CheckPrecondition` 的 exception→false 属候选选择逻辑，由 `ResolvedEventIndex.MatchesCurrentState` 独立处理，**不经** `ConditionEvaluator`，也不改变。

---

## 15. Automated Checks 设计

保持 BCL-only core。建议新增 pure source-link 文件，并维持 package-free Checks。

### 15.1 Parser

- 已知 token parsing（长名 + 短码 `f`/`e` 等）。
- negation（`!x` → Negated）。
- malformed args（缺参 / 超参 / 非整数）。
- unknown token → Opaque。
- raw preserved（`OpaqueCondition.RawToken` 原文）。
- ordering（多 segment 顺序保留）。
- multiple conditions = implicit And（`AndCondition`）。
- dotted / modded IDs（`SomeMod.Event42` 作为 EventId 保留，不因 `/` 拆错）。
- case rules（Token 大小写不敏感识别，但参数可能大小写敏感）。

### 15.2 Evaluator

- satisfied / unsatisfied / unknown / unsupported / error。
- exact friendship gap（NumericGap 目标/当前）。
- date/time gap。
- seen / not seen。
- mail / not mail。
- relationship。
- missing data → Unknown。
- provider exception → 由委托层测试。
- negated 条件（`!Season Winter` 等）。

### 15.3 Describer

- deterministic readable model（同输入同输出）。
- no raw parser leakage（只输出 key + 参数）。
- unknown fallback 保留 raw text。
- NPC / heart / time 转换。

### 15.4 Integration

```text
RawEventKey
    → ConditionParser
    → ConditionIR
    → EvaluationContext
    → ConditionEvaluator
    → ConditionEvaluation
```

- 测试从仓库真实 raw key（`75160185/f Alissa 500` 等）出发。
- 断言：ConditionIR 解析不改变 raw key、identity、hash、dedup、ordering。
- 断言：unknown 不当作 false。
- 断言：不触发 `GameLocation`/`Game1`，核心全 BCL。
- 断言：Index selection 的 live precondition 仍由 `ResolvedEventIndex` 独立处理，ConditionIR 不影响其 selection 结果。

### 15.5 说明

自动 Checks 不加载真实游戏 runtime，因此：
- `EventAssetCatalog` 的实际 precondition wiring 仍属 code review + 游戏内 smoke。
- i18n JSON key parity 可作为 BCL-only 检查（`System.Text.Json` 解析两个 json，断言 key set 、placeholder 一致）。但这是可选的 Phase 3+ 检查项。

---

## 16. 文件级 implementation plan（设计，不在本阶段实现）

Phase 3 recommended 新增：

```text
Conditions/ConditionExpression.cs     (abstract + And/Or(预留) + 各 typed record)
Conditions/ConditionSource.cs
Conditions/ConditionTruth.cs
Conditions/ConditionKnowledge.cs
Conditions/ConditionGap.cs
Conditions/ConditionEvaluation.cs
Conditions/ConditionParser.cs
Conditions/ConditionEvaluator.cs
Conditions/ConditionDescriber.cs
Conditions/ConditionEvaluationContext.cs
Conditions/ReadableCondition.cs
Conditions/OpaqueOrNative ... (Opaque / NativeQuery node 放 ConditionExpression)
```

建议放 `Conditions/` 目录（不移动现有文件，避免 rename churn），并保持 BCL-only 便于 Checks source-link。

修改：

- `Checks/Program.cs`、`Checks/StardewGallery.Checks.csproj`：source-link 上述 BCL 文件并新增测试。
- 若未来 UI 需要，`GalleryCharacterMenu` 可在后续阶段接入 Describer——**Phase 3 不实现 UI 改造**。
- 不改 `ResolvedEvent`、`GalleryCatalog`、`ResolvedEventIndex`、`EventOwnership`、`Replay*`、`WatchedEventHistory`、`GalleryCatalogCache`、`ModEntry`。

### 实施顺序建议

1. 定义 `ConditionExpression` typed 节点 + `ConditionSource`。
2. 定义 `ConditionTruth` / `Knowledge` / `Gap` / `Evaluation`。
3. 定义 `ConditionEvaluationContext` 只读 snapshot 模型。
4. 实现 parser（含 Opaque / NativeQuery / And / Negated）。
5. 实现 evaluator（Tier 1 原生确定性读，委托注入 GSQ）。
6. 实现 describer（可本地化 key + 参数 + raw fallback）。
7. BCL-only Checks source-link 并覆盖上述用例。
8. 集成到 production（组合点注入），仍不改 UI / Selection。

---

## 17. Compatibility risks

- **误伤 Index selection**：不能因为「能评估条件」就重写 `ResolvedEventIndex` 的 live precondition。Selection 保持 `EventAssetCatalog.CheckPrecondition` 原样。
- **误改写 `ResolvedEvent`**：加入可序列化字段会改 `catalog-latest.json` 结构与 diagnostics；用 projection 层。
- **把 ownership parser 当全量 parser**：ParseEvidence 只读 friendship + sawEvent，不能复用为 ConditionIR。
- **UI formatter 与 ConditionIR 语义分叉**：Phase 3 不替换 formatter；ConditionIR 是独立分析层。未来迁移 Formatter 到 Describer 需单独立阶段。
- **GSQ 重写陷阱**：不能自写 Stardew 查询引擎；委托原生。
- **unknown == false**：Phase 3 目标正是避免这个；多态 Truth + Knowledge 隔离。
- **ConditionGap 注入**：本阶段只描述，不注入、不改变状态。
- **variance**：条件分析必须 candidate-scoped，不能因 identity 相同就复用同一条 raw key 的分析。

---

## 18. Phase 3 明确 out-of-scope

- PreviewState / PreviewPlan。
- StateInjector / WeatherInjector / RelationshipInjector。
- SaveGuard 扩展。
- Replay refactor / unified EventLauncher（Phase 6）。
- SQLite。
- ObservedVariant / HistoricalEventRecord。
- CP passive / active discovery。
- future variant enumeration / variant explorer。
- planner / solver / route planning。
- 自动世界状态求解。
- simulated replay。
- UI 大改版（含 formatter 重写）。
- 修改 Phase 2 candidate selection。
- 把 Stardew GSQ 全部重新实现。
- 任何「重写 Stardew Query system」的尝试。

---

## 19. 尚需 Codex 决策的问题

1. `ConditionEvaluation` 是否纳入 `ConditionSource`？建议纳入，UI 可据此隐藏/显示来源。
2. 采用方案 A typed nodes，并让叶节点自带 `Negated`（而非通用 `NotCondition`）——是否确认？建议确认。
3. 是否引入 `IConditionContextProvider` 接口，还是先只保留 `Func<ConditionEvaluationContext>` 组合点？建议先保留函数组合，不强制接口。
4. `ConditionGap` 是否在本阶段就提供 per-node gap，还是首批只对 Friendship / Time / Seen / Mail 提供精确 gap，其余 `Unavailable`？建议首批精确 gap 只给 Tier 1，其余 `Unavailable`。
5. GSQ 是否整段委托原生 `GameStateQuery.CheckConditions`，不解析其内部语义？建议是。
6. 是否本次就加 i18n JSON key-parity/placeholder-parity 检查（BCL-only）？可选，建议 Phase 3 加。
7. 新目录名：`Conditions/` vs `Domain/` vs `Catalog/`？建议 `Conditions/`，避免与 domain/catalog 混。
8. readable text 的 heart 转换：Describer 输出 points（供机器）而由 UI 转 heart，还是 Describer 直接输出 heart？建议 Describer 输出参数化数据（含 points 与 hearts），由 UI 选。
9. multiplayer：default snapshot 取 host 还是当前玩家？建议当前本地玩家 + 明确标注无法确认项。
10. `ConditionTruth.Error` 是否需要暴露到 UI，还是 UI 只按 Truth 聚合（Reliable / Unknown）而 Error 仅进日志？建议 UI 聚合，Error 进日志 + raw view。

---

## 20. 结论

Phase 3 的最小架构目标是把「条件」从「被 UI 白名单截断、被 ownership 子集化、被 Index 原生病态处理」提升为**独立的、可解析、可评估、可知晓**的分析层。

推荐结论：

- **ConditionIR = typed nodes**，叶节点带 `Negated`，含 `ConditionSource`。
- **ConditionSource** 显式建模并进入 IR node 与 evaluation result。
- **Truth 五态**（True/False/Unknown/Unsupported/Error）+ **Knowledge** 聚合，明确区分 Unknown / Unsupported / Error。
- **ConditionEvaluation + ConditionGap** 提供稳定结果与差距，但不求解。
- **EvaluationContext = 只读 snapshot**，UI 打开时捕获，不持有 live game objects。
- **GSQ 委托原生**，不重写 parser。
- **Index selection 保持不变**，ConditionIR 是并行新增分析层。
- **不修改 `ResolvedEvent`**，用独立 analysis/projection 层。
- **Parser / Evaluator / Describer 三接口分离**，BCl-only 可测。
- **本项目不实现任何条件代码**，仅记录设计；业务实现与 UI 接入留待 Phase 3 实现/后续阶段。
