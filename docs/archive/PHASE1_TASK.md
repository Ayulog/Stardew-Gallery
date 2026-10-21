# Stardew Gallery Phase 1：Domain Model Migration 实施任务书

## 一、任务目标

基于当前 `StardewGallery 1.0.0` 源码进行第一阶段重构。

本阶段只做：

> 引入新的领域模型，并让现有代码开始使用新的 Event Identity / Resolved Event 模型。

本阶段必须保证：

- Gallery UI 行为不变；
- 当前事件回放行为不变；
- 历史事件回放行为不变；
- 已有 watched-event history 存档数据继续兼容；
- 不引入 SQLite；
- 不实现 ConditionIR；
- 不实现 PreviewState / StateInjector；
- 不拆 ReplayCoordinator；
- 不改变用户可见功能。

这是一个“零行为变化”的底层数据模型迁移。

---

# 二、当前架构背景

当前项目已有成熟能力，不允许推倒重写：

- `ReplayCoordinator`
- `ReplaySnapshot`
- `ReplaySaveGuard`
- `ReplayLifecycleRules`
- `ReplaySpeedPatches`
- `HistoricalReplayAssets`
- `WatchedEventHistory`
- `EventFragments`
- `EventOwnership`
- `GalleryMenu`
- `GalleryCharacterMenu`

尤其已有历史回放能力会捕获：

- root event script；
- `fork` / `switchEvent` 引用的其他 Event asset entries；
- translations；
- historical playback fingerprint。

这些能力必须保留。

---

# 三、正式领域语义

## 1. Event Identity

逻辑事件身份正式定义为：

```csharp
EventIdentity
{
    AssetName,
    EventId
}
```

不是：

```text
LocationName + EventId
```

不是：

```text
RawEventKey
```

不是：

```text
ContentHash
```

示例：

```text
AssetName = Data/Events/Town
EventId   = SomeMod.Event42
```

同一个 Asset 中：

```text
SomeMod.Event42/Season Summer
SomeMod.Event42/Season Winter
```

属于同一个 `EventIdentity` 的不同定义/版本。

---

# 四、新增文件

建议新增目录：

```text
Domain/
```

新增：

```text
Domain/EventIdentity.cs
Domain/ResolvedEvent.cs
Domain/EventHashes.cs
Domain/HistoricalPlaybackBundle.cs
```

---

# 五、EventIdentity.cs

实现一个 typed value object。

要求：

```csharp
internal readonly struct EventIdentity : IEquatable<EventIdentity>
```

字段：

```csharp
string AssetName
string EventId
```

构造时：

```text
AssetName:
- '\' 统一替换为 '/'
- Trim
- 比较时 OrdinalIgnoreCase

EventId:
- Trim
- 比较时 Ordinal
```

也就是说：

```text
Data\Events\Town
Data/Events/Town
data/events/town
```

视为同一个 Asset。

但：

```text
abc
ABC
```

两个 EventId 必须保持不同。

提供临时兼容属性：

```csharp
string StorageKey
```

格式可以继续使用当前项目习惯的分隔符，例如：

```csharp
$"{AssetName}\u001f{EventId}"
```

并：

```csharp
ToString() => StorageKey
```

注意：

`StorageKey` 只是兼容旧 UI / Dictionary 的临时字符串，不是未来正式领域 Identity 类型。

---

# 六、EventHashes.cs

不要直接使用现有只截取 12 字符的 fingerprint 作为业务 Hash。

新增完整 SHA256：

```csharp
EventHashes.RootScript(string script)

EventHashes.RootDefinition(
    string rawEventKey,
    string rootScript
)
```

规则：

```text
RootScriptHash
= SHA256(exact resolved root script)

RootDefinitionHash
= SHA256(rawEventKey + '\0' + rootScript)
```

输出完整：

```text
64 hex characters
```

日志/UI 如需短值，再：

```csharp
hash[..12]
```

不要让短 Hash 进入业务 identity。

---

# 七、非常重要：Hash 命名要求

本阶段不要把它简单命名为：

```text
DefinitionHash
ScriptHash
```

请明确叫：

```text
RootDefinitionHash
RootScriptHash
```

原因：

当前 Stardew Gallery 已经支持历史事件中的：

```text
fork
switchEvent
translations
```

这些外部依赖也可能改变用户最终看到的剧情。

例如：

```text
RootScript:
fork BranchA
```

RootScript 没变，但 BranchA 的脚本变了。

此时：

```text
RootDefinitionHash
RootScriptHash
```

都可能不变，但实际完整播放内容已经变化。

因此未来还会有：

```text
PlaybackHash
```

用于代表完整历史播放 Bundle。

本阶段不要把 RootDefinitionHash 错误固化成未来 Variant 唯一键。

---

# 八、ResolvedEvent.cs

新增：

```csharp
internal sealed record ResolvedEvent(
    EventIdentity Identity,
    string LocationName,
    string RawEventKey,
    string ResolvedScript,
    EventFragments Fragments,
    string RootDefinitionHash,
    string RootScriptHash
)
```

并提供：

```csharp
AssetName => Identity.AssetName
EventId   => Identity.EventId
```

语义：

```text
ResolvedEvent
= 当前内容管线已经解析后的一个事件定义
```

它是领域事实对象，不属于 UI。

---

# 九、GalleryEvent 改成兼容 Adapter

不要删除 `GalleryEvent`。

当前 UI、Replay、Ownership 等大量代码依赖它。

把 `GalleryEvent` 改成：

```csharp
internal sealed record GalleryEvent(
    ResolvedEvent Resolved,
    EventOwnership Ownership
)
```

然后提供兼容属性：

```csharp
Identity
LocationName
AssetName
EventId
EventKey
Script
Fragments
```

分别代理到：

```text
Resolved.Identity.StorageKey
Resolved.LocationName
Resolved.AssetName
Resolved.EventId
Resolved.RawEventKey
Resolved.ResolvedScript
Resolved.Fragments
```

目标：

现有代码仍然可以继续：

```csharp
entry.EventId
entry.EventKey
entry.Script
entry.Identity
entry.LocationName
```

但未来新代码可以：

```csharp
entry.Resolved
```

---

# 十、GalleryCatalogCache 改动

只修改 Event 创建处，不进行 Catalog 架构拆分。

当前类似：

```csharp
string identity =
    EventKey.GetIdentity(locationName, id);

GalleryEvent entry = new(...);
```

改成：

```csharp
EventIdentity identity =
    new(assetName, id);

ResolvedEvent resolved =
    new(
        Identity: identity,
        LocationName: locationName,
        RawEventKey: key,
        ResolvedScript: script,
        Fragments: fragments,
        RootDefinitionHash:
            EventHashes.RootDefinition(key, script),
        RootScriptHash:
            EventHashes.RootScript(script)
    );

GalleryEvent entry =
    new(
        resolved,
        initialOwnership
    );
```

本阶段：

```text
GalleryCatalogCache 其他扫描逻辑尽量不动。
```

尤其不要顺手实现：

```text
EventAssetCatalog
ResolvedEventReader
ResolvedEventIndex
AssetReady
AssetsInvalidated 新架构
CP1
```

这些属于 Phase 2。

---

# 十一、当前候选 Dictionary 可继续用 string

例如当前：

```csharp
Dictionary<string, List<...>>
```

不用全部改成 `EventIdentity`。

第一阶段可继续：

```csharp
identity.StorageKey
```

尽量控制改动范围。

---

# 十二、EventKey.cs 处理方式

这些继续保留：

```text
TryGetId
SelectVariantIndex
IsPlaceholderScript
GetSnapshotFingerprint
```

其中：

```text
GetSnapshotFingerprint
```

目前实际代表完整 historical playback bundle 的 fingerprint，暂时不要重写。

这些停止新代码继续使用：

```text
GetIdentity
GetScriptFingerprint
```

可以：

- 保留方法；
- 标记 obsolete；
- 或只是停止调用。

不要为了“清理代码”在 Phase 1 删除它们。

---

# 十三、WatchedEventSnapshot 必须保持存档兼容

现有 `WatchedEventSnapshot` JSON schema 不允许破坏。

字段名保持：

```text
LocationName
AssetName
EventId
EventKey
RootScript
EventAssets
Translations
Locale
Fingerprint
FirstWatchedAt
LastWatchedAt
```

不要 rename：

```text
EventKey → RawEventKey
Fingerprint → PlaybackHash
```

至少当前 persisted DTO 不能 rename。

否则可能破坏旧 save data：

```text
watched-event-versions
```

---

# 十四、WatchedEventSnapshot 增加计算属性

可以新增：

```csharp
[JsonIgnore]
internal EventIdentity Identity
    => new(AssetName, EventId);
```

以及：

```csharp
[JsonIgnore]
internal HistoricalPlaybackBundle Playback
    => HistoricalPlaybackBundle.From(this);
```

这些属性不能进入旧 JSON。

---

# 十五、HistoricalPlaybackBundle.cs

新增领域模型：

```csharp
internal sealed record HistoricalPlaybackBundle(
    string RootScript,
    IReadOnlyDictionary<
        string,
        Dictionary<string, string>
    > EventAssets,
    IReadOnlyDictionary<string, string> Translations,
    string Locale,
    string PlaybackHash
);
```

提供：

```csharp
HistoricalPlaybackBundle.From(
    WatchedEventSnapshot snapshot
)
```

映射：

```text
RootScript
EventAssets
Translations
Locale
Fingerprint → PlaybackHash
```

这里正式把现有 `Fingerprint` 在领域语义里解释为：

```text
PlaybackHash
```

但旧 Persisted DTO 仍叫：

```text
Fingerprint
```

---

# 十六、WatchedEventHistory.entries 改成 typed key

当前：

```csharp
Dictionary<
    string,
    List<WatchedEventSnapshot>
>
```

改为：

```csharp
Dictionary<
    EventIdentity,
    List<WatchedEventSnapshot>
>
```

这是安全修改。

原因：

当前持久化时只序列化：

```csharp
entries.Values
    .SelectMany(...)
```

Dictionary key 并不会写入 save data。

因此：

```text
string key
→ EventIdentity key
```

不会破坏旧数据。

---

# 十七、WatchedEventHistory.Add()

当前如有：

```csharp
EventKey.GetIdentity(
    snapshot.LocationName,
    snapshot.EventId
)
```

必须改成：

```csharp
snapshot.Identity
```

这是一个重要 bug/语义修复。

旧逻辑身份：

```text
LocationName + EventId
```

新逻辑：

```text
AssetName + EventId
```

---

# 十八、WatchedEventHistory.Get()

正式 API 改成：

```csharp
Get(EventIdentity identity)
```

可以增加兼容 overload：

```csharp
Get(GalleryEvent entry)
    => Get(entry.Resolved.Identity);
```

UI 不应该继续知道 identity string 的内部格式。

---

# 十九、GalleryMenu delegate

如果当前是：

```csharp
Func<
    string,
    IReadOnlyList<WatchedEventSnapshot>
>
```

改为：

```csharp
Func<
    GalleryEvent,
    IReadOnlyList<WatchedEventSnapshot>
>
```

---

# 二十、GalleryCharacterMenu

当前所有：

```csharp
watchedVersions(entry.Identity)
```

改为：

```csharp
watchedVersions(entry)
```

但：

```csharp
Dictionary<string, int>
    selectedVersions
```

本阶段不要改。

继续用：

```csharp
entry.Identity
```

作为临时 UI state key 即可。

---

# 二十一、ModEntry

尽量不改结构。

如果现有：

```csharp
watchedHistory.Get
```

method group 能匹配新的：

```csharp
Get(GalleryEvent)
```

则继续直接传。

不要顺手重构 composition root。

---

# 二十二、本阶段明确禁止修改 Replay

以下文件除非出现编译兼容问题，否则不要主动改逻辑：

```text
ReplayCoordinator.cs
ReplaySnapshot.cs
ReplaySaveGuard.cs
ReplayLifecycleRules.cs
ReplaySpeedPatches.cs
HistoricalReplayAssets
```

尤其不要：

```text
拆 ReplayCoordinator
统一 EventLauncher
改回放流程
改 Snapshot 流程
改 disk backup
```

这些属于后续阶段。

---

# 二十三、本阶段明确禁止修改 EventFragments

`EventFragments` 当前已经支持：

```text
fork
switchEvent
changeLocation
translation dependencies
```

本阶段不重写。

它是成熟能力。

---

# 二十四、本阶段明确禁止实现的功能

不要实现：

```text
ConditionIR
ConditionEvaluator
ConditionGap
PreviewState
PreviewPlan
StateInjector
WeatherInjector
RelationshipInjector
SQLite
ObservedVariantStore
HistoricalEventRecord 数据库
EventAssetCatalog
ResolvedEventIndex
AssetReady refresh
CP1 passive discovery
CP2
CP3
Route Planner
Solver
```

即使觉得“顺手”，也不要做。

---

# 二十五、Checks 必须新增

至少覆盖以下测试。

## EventIdentity path normalization

```text
Data\Events\Town
==
Data/Events/Town
```

## AssetName case-insensitive

```text
Data/Events/Town
==
data/events/town
```

## EventId case-sensitive

```text
abc
!=
ABC
```

## 不同 Asset，同 Event ID

```text
Data/Events/Town + 123
!=
Data/Events/Beach + 123
```

## 相同 Location 不代表相同 identity

构造一个案例：

```text
同一个 LocationName
不同 AssetName
同 EventId
```

必须：

```text
Identity !=
```

## RootScriptHash

相同 script：

```text
Hash ==
```

不同 script：

```text
Hash !=
```

## RootDefinitionHash

相同 script，不同 RawEventKey：

```text
Hash !=
```

例如：

```text
123/Season Summer

123/Season Winter
```

即使脚本一样也必须不同。

---

# 二十六、旧 History JSON 兼容测试

必须模拟旧版数据反序列化。

例如旧 JSON：

```json
{
  "LocationName": "Town",
  "AssetName": "Data\\Events\\Town",
  "EventId": "123",
  "EventKey": "123/f Haley 1000",
  "RootScript": "...",
  "EventAssets": {},
  "Translations": {},
  "Locale": "zh",
  "Fingerprint": "abc",
  "FirstWatchedAt": "...",
  "LastWatchedAt": "..."
}
```

反序列化后：

```csharp
snapshot.Identity
```

必须等于：

```csharp
new EventIdentity(
    "Data/Events/Town",
    "123"
)
```

同时：

```text
所有原字段正常读取。
```

---

# 二十七、编译要求

Phase 1 完成后：

```text
整个 solution 必须成功编译。
```

如果项目已有：

```text
Checks
Tests
```

全部运行。

不要只做到“代码看起来能编译”。

---

# 二十八、功能回归要求

本阶段完成后必须人工/自动确认：

```text
1. Gallery 能正常打开。

2. 当前事件数量与修改前一致。

3. NPC 分组与修改前一致。

4. 事件详情页正常。

5. 条件文本显示与修改前一致。

6. 当前版本 Replay 正常。

7. 历史版本 Replay 正常。

8. 历史 Replay 的 fork/switchEvent 仍正常。

9. 已有 watched-event-versions 能正常读取。

10. 新自然 Event 看完后仍能生成 watched version。

11. Replay 结束后 Snapshot restore 行为不变。

12. SaveGuard 行为不变。
```

如果任何一项发生行为变化：

> 优先修复，不继续 Phase 2。

---

# 二十九、代码风格要求

不要进行与任务无关的大规模格式化。

不要：

```text
全项目 rename
全项目 namespace 重构
顺手改 nullable
顺手改 UI
顺手优化 Replay
顺手重写 Ownership
```

尽量让 diff 集中在：

```text
4 个新 Domain 文件
+
GalleryCatalog
GalleryCatalogCache
WatchedEventHistory
GalleryMenu
GalleryCharacterMenu
Checks
```

---

# 三十、Phase 1 最终验收状态

修改前：

```text
Data/Events
    ↓
GalleryEvent
    ↓
UI / Replay
```

修改后：

```text
Data/Events
    ↓
ResolvedEvent
    ↓
GalleryEvent compatibility adapter
    ↓
原 UI / 原 Replay
```

History：

修改前：

```text
Natural Event
    ↓
WatchedEventSnapshot
```

修改后：

```text
Natural Event
    ↓
WatchedEventSnapshot
        │
        ├─ Identity
        └─ HistoricalPlaybackBundle
```

但旧 Persisted JSON 不变。

---

# 三十一、提交完成后请输出报告

不要只说“完成”。

请输出：

## A. 修改文件列表

例如：

```text
Added:
...

Modified:
...
```

## B. 每个文件具体改了什么

简短说明。

## C. 编译结果

包括：

```text
build command
success / failure
warnings/errors
```

## D. Checks / Tests 结果

逐项说明。

## E. 兼容性确认

确认：

```text
旧 watched history schema 未改变
ReplayCoordinator 未重构
ReplaySnapshot 未改变
EventFragments 未改变
```

## F. 遗留问题

如果实现中发现：

```text
某处 LocationName 与 AssetName 关系特殊
历史数据 AssetName 缺失
某个 Identity 调用仍使用旧 EventKey.GetIdentity
```

请列出来，不要擅自扩大范围解决。

---

# 三十二、特别提醒

这不是最终架构。

Phase 1 完成之后，后续计划才是：

```text
Phase 2
GalleryCatalogCache
↓
EventAssetCatalog
ResolvedEventReader
ResolvedEventIndex

Phase 3
ConditionIR + Evaluator

Phase 4
ObservedVariant / HistoricalEventRecord

Phase 5
SQLite

Phase 6
统一 EventLauncher

Phase 7
PreviewPlan + StateInjector

Phase 8
Snapshot / SafetyFirewall 补强

Phase 9
AssetReady + CP1

Phase 10
UI 接入完整新能力
```

因此当前所有设计都要服务于：

> Phase 1 最小改动、零行为变化、为后续重构建立稳定 Domain Model。

不要提前实施 Phase 2 及之后的内容。