# Stardew Gallery 2.8.0 审计

审计日期：2026-09-21。范围：已发布 `v2.8.0`，提交 `f30e6019be8adf7ee460f70d9a7520cdae43936f`。

本次审计发现 **6 项需要修复的功能缺陷（2 项 P1、4 项 P2）**，以及旧历史／预览代码、依赖和文档的维护遗留。没有修改生产代码、玩家配置、存档或游戏安装，没有提交或发布。本报告中的游戏影响由具体代码路径和隔离证据支持，不表示已经启动真实游戏逐项复现。

P1 表示应优先处理的真实进度或回放可用性风险；P2 表示条件、演出或交互错误；P3 表示维护及文档债务。所有行号均针对上述提交的源码。

## 1. 需要修复的 bug

### B1 — P1：快照改变大小写语义，恢复时可能删除真实进度

**位置：** [ReplaySnapshot.cs:87](../../ReplaySnapshot.cs#L87)、`:91`、`:95–98`，恢复在 `:138–146`；同类只读问题在 [Preview/RuntimePreviewState.cs:31](../../Preview/RuntimePreviewState.cs#L31)。

游戏的事件、邮件及部分字典键区分大小写；快照却使用 `StringComparer.OrdinalIgnoreCase`。

- 若原集合同时有 `Author.Event` 与 `author.event`，捕获时两项合成一项。成功回放后恢复逻辑清空原集合，再写回快照，另一项事件／邮件标记丢失。这会改变真实内存进度，并可能随之后正常保存写入存档。
- 对话或配方字典同时有这两个合法键时，`ToDictionary` 直接抛出 `ArgumentException`，回放无法启动。
- 当前条件页使用的 `RuntimeStateReader` 也把事件／邮件转成忽略大小写集合。只存在 `Author.Event` 时，要求 `author.event` 的条件可能被错误显示为满足；这段虽然放在 Preview 目录，仍是活跃代码。

**证据：** 使用游戏 1.6.15 的实际 `NetStringHashSet`、`NetStringDictionary` 创建独立集合，执行当前捕获／恢复表达式。结果分别为 `2 → 1` 和 `ArgumentException`。未加载存档、未运行整个 ReplaySnapshot 生命周期。见本地 `diagnostics/audit-2.8.0-2026-09-21/replay/diagnostics/replay-case-probe/evidence.txt` 及同目录 `Program.cs`。

**修复方向：** 保持每个原生集合的键比较语义；回放快照和当前条件读取一起核对。补充实际捕获／恢复的大小写回归，不能只检查纯权限策略。现有普通 Checks 没有编译实际 `ReplaySnapshot`，因此通过并不能覆盖此问题。

### B2 — P1：爷爷蜡烛事件可修改真实农场评分，回放结束不恢复

**位置：** [ReplayEffectPolicy.cs:8](../../ReplayEffectPolicy.cs#L8)、[ReplaySaveGuard.cs:48](../../ReplaySaveGuard.cs#L48)、[ReplaySnapshot.cs:64](../../ReplaySnapshot.cs#L64)。

效果保护已抑制 `GrandpaEvaluation` 和 `GrandpaEvaluation2`，但遗漏 `GrandpaCandles`。原版 `Data/Events/Farm` 的 `2146991/y 3/H` 包含 `grandpaCandles`，当前目录把这条演出归为普通剧情。

开启普通事件回放与一键解锁后，在尚未正常评估、或当前评分高于上次评估时回放它，原生命令会重新计算并写入 `Farm.grandpaScore`、设置蜡烛。该命令没有 `isMemory` 保护，画廊快照也没有农场评分字段，因此正常结束后的恢复不会撤销这次写入。

**证据：** 本次从安装的原版 XNB 读到上述事件；游戏 1.6.15 已有反编译 `diagnostics/event-source-research/game/StardewValley.Event.decompiled.cs:3210` 明确写入 `Game1.getFarm().grandpaScore.Value`。`Catalog/GalleryCatalogBuilder.cs:190` 的分类及现有保护调用链确认可达。没有在玩家游戏中执行该事件。

**修复方向：** 把该原版持久结算纳入拦截或明确的状态恢复，区分临时演出和真实评分；补一条实际命令的前后评分不变检查，并核查同类原版世界状态命令。无需以扩大第三方通用拦截代替这项具体修复。

### B3 — P2：绿雨被误识别为不支持的自定义天气

**位置：** [Preview/ReplaySceneEnvironment.cs:41](../../Preview/ReplaySceneEnvironment.cs#L41)。

`NormalizeWeather` 没有 `GreenRain` 分支，但 `QueryWeather.Standard` 和 `ReplaySceneEnvironmentScope.WeatherSnapshot.For` 都将绿雨视为原版天气。合法的 `Weather GreenRain` 条件在非绿雨时回放，会保留当前天气并提示“不支持自定义天气要求”。此外，`rainy` 分支只保留 Rain／Storm，没有保留当前绿雨。

**证据：** 解析器、查询天气表、环境应用器支持绿雨，但解析到环境的映射遗漏；本次为静态调用链确认，未做游戏天气切换。

**修复方向：** 补齐绿雨映射和 rainy 语义，用全部六种原版天气覆盖环境解析及恢复检查。

### B4 — P2：名单下载完成时会丢失未保存的改名、筛选草稿

**位置：** [Navigation/GalleryApplication.cs:42](../../Navigation/GalleryApplication.cs#L42)，触发入口 [ModEntry.cs:135](../../ModEntry.cs#L135)。

读档后名单仍在下载时，玩家进入筛选页修改条件，或给原版事件输入新名字；如果下载完成且规则与种子不同，`RefreshCatalog` 会释放输入并创建新菜单。此时 `GalleryFilterMenu` 的草稿只在私有 `draft` 字段，`GalleryRenameMenu` 的未保存文本只在旧输入框里，重建后全部回到此前已保存状态。

`ReplayService.IsBusy` 只包含回放与回放确认，没有保护正在编辑的工具菜单。即使名单变化不影响任何已加载模组，也会触发重建；用原版事件改名即可排除“目标被隐藏所以关闭”的解释。

**证据：** 静态核对 `AiModExclusionService.cs:82` → `ModEntry.cs:135` → `GalleryApplication.cs:42–45,131,133`；草稿和输入分别见 `UI/GalleryFilterMenu.cs:19,80`、`UI/GalleryRenameMenu.cs:22,45`。尚未运行真实 UI。

**修复方向：** 为未提交的编辑状态提供保存／恢复机制，或延后会重建编辑页的操作；同时确保新的排除规则不会因保存旧页面而被绕过。

### B5 — P2：消失的角色／地点筛选显示“任意”，实际仍在筛选

**位置：** [UI/GalleryFilterMenu.cs:41](../../UI/GalleryFilterMenu.cs#L41)，选项生成 `:99–102`；实际筛选 [Navigation/StorySearchIndex.cs:75](../../Navigation/StorySearchIndex.cs#L75)。

已应用某个模组专属角色／地点筛选后，新名单隐藏该值对应的全部事件。再次打开筛选页时，选项只从新目录生成，找不到旧值就显示“任意”，但 `draft.Npc`／`draft.Location` 仍保留旧值，再应用依旧得到空列表。

同文件 `SourceChoices:117` 已专门保留失效的来源选项，角色／地点缺少同等处理。

**证据：** 页面历史保留已应用筛选，新目录只改变选项，显示回退没有改变实际过滤值；本次为静态调用链确认。

**修复方向：** 保留可辨认的失效选项，或明确清空失效过滤值并反馈。与 B4 分开处理：延后编辑中的刷新，仍不能解决已经保存的筛选值失效。

### B6 — P2：静态来源已匹配时，修改记录隐藏“证据不完整”状态

**位置：** [Sources/EventOriginPresentation.cs:43](../../Sources/EventOriginPresentation.cs#L43)。

开启运行时追踪，某编辑器失败且最终没有留下键值差异时，索引会正确给出 `Partial / ScriptMatches=true / 0 mutations`。如果静态来源已识别或存在多个候选，展示层只截取“修改记录”部分，丢掉 `source.partial`、`source.partial-help` 和范围说明，于是只显示“未观察到修改”，没有告知这次观察失败或不完整。静态来源 Unknown 的分支却能正确保留这些提示。

**证据：** 独立夹具调用当前真实索引与展示逻辑，执行 `RecordBaseline → ObserveEdit(failed: true) → Complete`，已复现上述差异。见 `diagnostics/audit-2.8.0-2026-09-21/sources/diagnostics/partial-runtime-display/Program.cs`。

**修复方向：** 静态原始提供者与运行时记录完整性分别显示；不要通过截取翻译后的段落文本丢弃状态。此缺陷影响详情可信度，不是名单匹配会误排原版的证据。

## 2. 规划中尚未实现的内容

不能把所有旧计划累加成当前待办。以下按现行范围分类。

| 内容 | 2.8.0 状态 | 依据与后续定位 |
| --- | --- | --- |
| fork／switchEvent／switchEventFull 分支来源、完整修改链 | 未实现完整覆盖 | `docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md:97,105` 限定主事件；`EventFragments.cs` 负责收集剧情，不等于分支作者追踪。属于后续来源覆盖工作 |
| 更多纯 C# 动态事件、其他框架、自定义令牌 | 未实现通用支持 | 目前适配 CP 和指定 AliveNpcs 格式；未知保留。属于已声明覆盖缺口，没有已承诺的通用完成版本 |
| Data/TriggerActions 前置标记的来源归属 | 未实现 | 前置资料本身已能展示，但来源扫描以 Data/Events 为主；不能根据下游角色或依赖强行推断提供者 |
| DDFC 新版条件及任意美化混装／热切换 | 未完成全面兼容 | `docs/TASK_2.6.0_APPEARANCE.md:39` 明确限制；目前有指定协议的读取与回退，不代表任意组合已支持 |
| 回放时使用原版素材 | 仅调研，未实现 | 这是 2.8.0 发布后的本会话需求；见 `REPLAY_VANILLA_APPEARANCE_RESEARCH.md`，不是 2.8.0 漏交付 |
| 高级条件求解、路线规划、跨事件进度规划 | 旧版未来想法，未实现 | `docs/RELEASE_2.0.0_SCOPE.md:40` 明确列为 Future Ideas；目前没有现行开发承诺，不应默认为下一版本任务 |
| 2.8.0 来源页面长名称、真实手柄、完整游戏内组合验证 | 待验收，不是未实现代码 | `docs/RELEASE_2.8.0_NOTES.md:20` 已披露；现有自动检查不能替代真实游戏验收 |

以下已经取消或明确排除，**不应列成需要补齐的漏项**：

- 历史冻结内容回放、自然事件采集、历史 SQLite 会话：2.0.6 明确停用。
- 旧 Preview 玩家入口与条件状态注入：2.0.1 移除，统一当前内容回放。
- Phase 7D／7E／7F 的历史自动分支、选择采集及精确选择回放：旧路线未全部实现，后来随历史功能退出产品范围。
- 多人回放、美化选择器与冲突协调器：2.6 收尾明确取消。
- 婚礼、节日主流程、夜间 FarmEvent、影院专用放映：当前范围明确排除。
- Overgrown／Earthy 专用画廊皮肤：只有既有组合实测记录，没有专用皮肤的交付承诺。

已实现但旧阶段文档仍保留的内容，也不应误报：条件 IR 已接入 UI；TriggerActions 前置查询已实现；原始来源扫描、来源筛选、独立名单开关、GMCM 修改追踪设置均已进入 2.8.0。`EVENT_MOD_SOURCE_IMPLEMENTATION.md` 中“来源筛选尚未实现”属于明确标注的 2.7 历史章节。

## 3. 旧功能仍遗留在代码中的情况

### L1 — 历史采集／历史回放源码仍编译，但没有产品接线

主要范围：`WatchedEventHistory.cs`、`History/*`、`Domain/Historical*`、`ObservedVariant*`、`KnownSeenEvidence`，以及 `Persistence/` 下的历史库、迁移和 codec。

- `History/ExecutionTraceObserver.cs:16` 的 Apply 没有当前产品调用者，其四组历史 Harmony 观察点没有安装。没有发现自动 PatchAll 重新启用它们。
- `WatchedEventHistory.cs:466` 的 `HistoricalReplayAssets.OnAssetRequested` 没有创建或事件订阅。
- `EventPlayback.cs:16` 的 `ForHistorical` 仅被 Checks 使用。
- `ModEntry` 没有创建 WatchedEventHistory／GalleryDatabase／HistoryRepository，也没有自然事件历史采集回调。

这符合 `docs/RELEASE_2.0.6_DEVELOPMENT.md:5,11–20` 的“停用但暂留源码”决定。它是可清理的维护遗留，不能据此声称 2.8.0 仍在偷偷记录自然剧情。

### L2 — 旧 Preview 分支和测试仍在

`ReplayCoordinator.TryStartPreview:45` 无产品调用；唯一现行启动路径是 `ReplayService → TryStart → TryStartCore(null)`。`PreviewPlanner.Analyze/Plan`、`PreviewInjectionScope`、`RuntimePreviewStateAccessor`、`EventCardStateResolver` 属于旧方案或仅测试可达；`IPreviewSaveGuard` 只剩定义。

两套 Checks 仍显式编译旧历史／预览类型，因此“这些测试通过”不能证明已取消功能有玩家入口，也不能代替当前回放协调器的真实恢复检查。

### L3 — P3：不用的 SQLite 依赖仍占用约一半发布包

`StardewGallery.csproj:17` 保留 `Microsoft.Data.Sqlite`，旧历史源码默认参与生产编译，随之携带 SQLite 托管程序集及多个平台原生运行库。实际 2.8.0 ZIP 中相关项（含配套文档）统计：

| 项目 | 字节数 |
| --- | ---: |
| SQLite 相关项数量 | 27 项 |
| 解压体积 | 25,621,799 |
| 压缩体积 | 12,821,360 |
| 整个发布 ZIP | 25,774,104 |

压缩占比约 **49.75%**。真实 SMAPI 加载器检查也读取了 SQLitePCLRaw.core／Microsoft.Data.Sqlite 的程序集引用，但这不代表当前产品打开了历史数据库。

可把旧历史检查留在独立工程，解除生产代码与 SQLite 的依赖后再减包。清理源码不需要删除玩家磁盘中的旧数据库、照片或名字文件。

### L4 — 活跃代码和旧代码混放，不能整目录删除

以下仍须保留或先拆分：

| 代码 | 当前用途 |
| --- | --- |
| Domain/EventIdentity、ResolvedEvent、EventHashes | 当前事件目录、来源匹配和照片路径身份 |
| Persistence/SaveProfileKey、EventNameStore | 截图按农场＋玩家隔离，以及本机共用的自定义名称 |
| Preview/RuntimePreviewState 内 RuntimeStateReader | 当前条件页的实际状态读取；B1 的同类大小写问题也在这里 |
| PreviewPlanner 内 CurrentStateSnapshot、ResolveSceneEnvironment | 当前条件分析、季节／时间／天气回放环境 |
| Preview/ReplayService、ReplayAccess、ReplaySceneEnvironment | 当前回放的权限、确认和环境解析 |
| EventFragments | 当前剧情分支与角色分析，不只是旧历史回放资料 |

当前 `SmapiEventSourceObserver` 的两个可选观察点仍有用途；不能把它们与已停用的历史 ExecutionTraceObserver 混为一谈。截图和名字也不是旧 SQLite 功能。

### L5 — P3：当前维护说明有陈旧表述

- `docs/MODULES.md:30` 仍将原始来源扫描称为“当前工作区（未发布）”，应标成已发布 2.8.0。
- `Persistence/HistoryRepository.cs:13` 仍说连接生命周期由 ModEntry 管理；当前已断开，应标注历史／停用状态。
- 历史阶段文档有归档价值，不建议抹掉；应在现行索引明确哪些记录已被后续版本取代。不能把所有旧“待验收”字样都认定为当前功能缺失。

## 4. 本次验证与边界

已完成：

- 确认 HEAD、v2.8.0、manifest、项目版本为同一发布基线；生产工作区无改动。
- 12 种语言各 538 个键，与默认语言键集合一致，无缺失或多余键。
- 两套常规检查均通过。目录符号链接专项因 IOException 跳过；不能声称该项通过。
- 按 BUILDING.md 完整 Release 构建通过，0 警告、0 错误；EnableModDeploy=false，未安装。
- 对重新构建 DLL 和实际发布 ZIP 内 DLL，分别通过真实 SMAPI 4.5.2 加载器及 JSON 规范化检查。检查不调用 Mod.Entry。输出 warnings=4 是 `PatchesGame` 位标志，不是四条兼容性错误。
- 大小写问题使用实际原生集合做了内存复现；爷爷命令读取原版 XNB 并核对对应游戏反编译；运行时 Partial 提示丢失使用真实纯逻辑做了独立复现。
- UI 草稿／失效筛选和绿雨问题完成静态调用链核验，没有运行真实游戏界面。

实际发布 ZIP SHA256：`F648BFC029476E1C87C888A695D9ED72CDEB5B7FAF6C3B0206A3CF2E548B4C29`。

实际发布 DLL SHA256：`2587CD2D21C44A65EF96924ACD7C04F8F239D43CFB5716001091BFE3CAD053F3`。

游戏依据为 1.6.15.24356、SMAPI 4.5.2；游戏 DLL SHA256 与既有研究一致。未运行玩家存档中的回放，未将任意第三方私有状态、所有美化混装、未核实的 farmerEat buff 或组合文件权限故障列为确定缺陷。其余名单匹配、旧会话取消和隐藏剧情前置投影暂未发现新的高信心缺陷；这不等于穷尽任意模组组合。

## 5. 建议修复顺序

1. 先修 B1、B2，增加实际快照往返和原版持久效果的回归，优先保护真实进度。
2. 修 B3–B6，覆盖六种天气、新名单到达时的编辑状态、失效筛选和 Partial 修改记录。
3. 单独清理旧历史／预览分支及生产 SQLite 依赖，先拆活跃复用类型，再调整检查工程与包内容；保留所有玩家数据。
4. 更新现行模块文档，逐项完成实际游戏内验收。来源扩展和原版素材回放另列后续功能，不把已取消的旧路线重新作为默认待办。