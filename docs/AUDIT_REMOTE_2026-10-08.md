# Stardew Gallery 远端功能审计（2026-10-08）

后续状态：B1–B11 的修复纳入 [2.9.0](RELEASE_2.9.0_NOTES.md)，过程详见 [整合与验证记录](INTEGRATION_2026-10-08.md)。下文保留远端 v2.8.0 的原始审计结论，不代表最新版本仍存在这些缺陷。

## 审计基线与结论

重新 fetch 并独立 clone 了 https://github.com/Ayulog/Stardew-Gallery；远端 main、v2.8.0 均为 `f30e6019be8adf7ee460f70d9a7520cdae43936f`。最新正式版发布于 2026-09-21 12:53:34（北京时间），截至本轮审计 GitHub open issues 返回空列表。没有 issue 不代表没有缺陷。

原工作区有大量未提交修改，已保留。本报告审计远端正式基线和实际发布包；本地 Unreleased 的修复不计入正式版。审计用副本在 `D:/Workspace/Projects/StardewMods/.audit-workspaces/Stardew-Gallery-20261008`，生产源码保持干净；其他三个模块副本仅用于隔离审计夹具。没有修改产品代码、安装 MOD、提交、推送或发布，没有读取玩家存档或启动游戏。

目前确认 **11 项远端缺陷：2 项 P1、8 项 P2、1 项 P3**。P1 涉及真实进度或回放安全，应先修；P2 涉及播放、分类、查询或交互；P3 为少见异常文件的恢复缺陷。下文给出触发条件与证据层级，不能将隔离检查写成游戏内验收。

## 未解决的 bug

| ID | 优先级 | 远端正式版问题 | 玩家影响 | 本轮证据 |
| --- | --- | --- | --- | --- |
| B1 | P1 | 回放快照改变标记/字典大小写语义 | 回放结束丢失部分真实进度；部分存档无法启动回放；条件可能误判 | 重建及发布 DLL 的 Capture/RestorePlayer 与状态读取隔离复现 |
| B2 | P1 | 漏保护 GrandpaCandles | 普通事件回放改写真实爷爷评分，快照未覆盖该字段 | 原版 XNB、目录分类及原生命令＋真实保护补丁隔离复现 |
| B3 | P2 | 嵌套 Event.Update 重复应用倍速 | 2x/4x 指数放大，后续暂停/动作时间异常 | 原版 jump/showFrame 递归，重建及发布 DLL 均复现 |
| B4 | P2 | 回放天气解析遗漏绿雨 | 绿雨剧情使用错误天气；rainy 条件把已有绿雨改成普通雨 | 真实环境解析隔离复现 |
| B5 | P2 | GSQ 正向关系条件漏参与剧情分类 | 好感剧情被归为普通剧情，不进主相册，回放受普通剧情开关限制 | 生产分类器最小夹具复现 |
| B6 | P2 | 条件筛选与解析结果不一致 | GSQ 心数/日期/天气及角色别名组合产生漏查或错误结果 | 生产查询索引最小夹具复现 |
| B7 | P2 | 名单刷新丢失编辑草稿 | 未保存改名、筛选和筛选子页状态丢失 | 静态完整调用链确认，未操作实际 UI |
| B8 | P2 | 失效角色/地点筛选显示“任意” | 实际仍按隐藏旧值筛选，列表为空且原因不可见 | 静态选项生成与过滤链确认 |
| B9 | P2 | 静态来源匹配后隐藏运行时 Partial 提示 | 不完整修改证据被显示为“未观察到修改”而缺少完整性说明 | 生产来源展示函数夹具复现 |
| B10 | P3 | 超限本地名单文件异常未兜底 | 会话名单初始化中断、没有启动下载，首次可能无排除规则 | 临时文件调用真实 StartSession 复现 |
| B11 | P2 | 手柄 A 键重复执行菜单操作 | 来源/更多筛选先开后关，照片可能一次归档两张 | 原版游戏分派与生产菜单静态交叉确认 |

### B1：回放快照破坏大小写不同的合法键

位置：[ReplaySnapshot.cs:87](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/ReplaySnapshot.cs#L87)、[RuntimePreviewState.cs:31](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Preview/RuntimePreviewState.cs#L31)。

原生集合可同时保留 `Author.Event` 与 `author.event`，快照使用 OrdinalIgnoreCase 后合并，再清空原集合写回。因此已看事件、邮件、明日邮件、对话回答、地点已看记录可能丢一项；后续正常保存会把丢失状态写入存档。好感、对话主题或配方字典同时含仅大小写不同的键时，ToDictionary 可能直接抛异常并阻止回放启动。当前条件页 RuntimeStateReader 也使用该错误比较语义，会把不同事件/邮件标记当成同一项。

本轮分别调用远端源码构建 DLL 和实际发布 DLL 的 Capture/RestorePlayer/RuntimeStateReader，16 项针对键语义的断言全部失败；没有加载存档。应保持原生集合比较规则，同时覆盖回放恢复和只读条件判定。

### B2：爷爷蜡烛事件改写真实评分

位置：[ReplayEffectPolicy.cs:8](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/ReplayEffectPolicy.cs#L8)、[ReplaySnapshot.cs:64](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/ReplaySnapshot.cs#L64)。

屏蔽清单有 GrandpaEvaluation/GrandpaEvaluation2，没有 GrandpaCandles。原版 `Data/Events/Farm` 的 `2146991/y 3/H` 包含该命令，生产分类器将其列为 Ordinary。开启普通剧情回放并满足解锁权限后可到达。原生命令会写 Farm.grandpaScore，当前快照未保存/恢复该字段。

本轮读取实际 XNB 并核实目录分类；安装真实 ReplaySaveGuard.Apply 到隔离进程后，画廊所属事件的 GrandpaCandles 仍把夹具评分从 1 改为 4。非回放事件对照正常执行。应只保护画廊所属回放，并保留自然事件行为。

### B3：嵌套调用重复放大倍速

位置：[ReplaySpeedPatches.cs:31](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/ReplaySpeedPatches.cs#L31)。

Prefix 每次进入 Event.Update 都乘一次倍率；部分原版即时命令继续用已经缩放的 context.Time 递归 Update。原版 jump/showFrame 夹具以 16ms 为起点，2x 得到 `[32,64,128]ms`，4x 得到 `[64,256,1024]ms`，正确值应分别一直保持 32ms/64ms。12 项断言中 4 项失败，1x 对照正常。应按同一轮外层更新只缩放一次，并在异常退出时清理嵌套状态。

### B4：绿雨环境解析遗漏

位置：[ReplaySceneEnvironment.cs:41](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Preview/ReplaySceneEnvironment.cs#L41)。

NormalizeWeather 只映射 Sun/Rain/Storm/Snow/Wind，把原版 GreenRain 当成未支持的自定义天气；rainy 也只保留 Rain/Storm。本轮实际调用结果：要求 GreenRain、当前 Sun → null；要求 rainy、当前 GreenRain → Rain。底层六种天气写入/恢复的 36 项检查通过，缺陷位于条件到环境之间的解析桥接，不应误报成整个天气恢复器坏掉。

### B5：GSQ 好感剧情被归为普通剧情

位置：[GalleryCatalogBuilder.cs:108](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Catalog/GalleryCatalogBuilder.cs#L108)。

分类器只取 FriendshipCondition/DatingCondition/SpouseCondition 等旧式直接条件，没有提取 NativeQueryCondition 内已支持的 GSQ 关系证据。相同演出中 `f Sam 500` 为 Heart；`G PLAYER_HEARTS Current Sam 2`、`G PLAYER_FRIENDSHIP_POINTS Current Sam 500`、`G PLAYER_NPC_RELATIONSHIP Current Sam Dating` 均为 Ordinary。结果是好感相册漏收、收藏统计不准，而且本来应按好感剧情允许的回放被普通剧情设置挡住。应共用明确且正向的关系证据，不把任意包含 NPC 的查询当成好感条件。

### B6：筛选漏掉 GSQ 和别名语义

位置：[StorySearchIndex.cs:106](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Navigation/StorySearchIndex.cs#L106)、[StorySearchIndex.cs:121](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Navigation/StorySearchIndex.cs#L121)。

- 2 心筛选只提取 FriendshipCondition，漏掉 GSQ PLAYER_HEARTS/PLAYER_FRIENDSHIP_POINTS。
- 角色查询先规范化别名，心数条件仍比较原始 NPC 名。例：Marlon 归并为 MarlonFay 后，单选角色有结果，再加正确 2 心门槛变为零。
- 季节/天气也只读取直接类型条件。`G SEASON_DAY spring 5` 仍出现在 winter 结果，`!G WEATHER Here Rain` 仍出现在 Rain 结果。

这些均用生产 StorySearchIndex 复现。应在不执行任意 GSQ/第三方逻辑的前提下共享可确认约束，并保持未知与已知冲突分开。

### B7：目录刷新清空未保存编辑

位置：[ModEntry.cs:135](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/ModEntry.cs#L135)、[GalleryApplication.cs:42](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Navigation/GalleryApplication.cs#L42)、[GalleryFilterMenu.cs:19](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/UI/GalleryFilterMenu.cs#L19)。

读档后远端名单尚在下载，玩家打开筛选页或改名页。若下载规则变化，RefreshCatalog 直接构造新菜单；筛选 draft/更多/picker 只在菜单私有字段，未保存名字只在旧输入框，均未纳入 GalleryPageState。即使正在修改原版事件、目标不会被排除，也会丢输入。回放和确认期间的延后保护没有覆盖编辑操作。静态调用链已确认，本轮没有操作游戏内输入框。

### B8：失效筛选显示与实际值不符

位置：[GalleryFilterMenu.cs:41](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/UI/GalleryFilterMenu.cs#L41)、[GalleryFilterMenu.cs:99](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/UI/GalleryFilterMenu.cs#L99)。

先应用模组专有角色或地点筛选，新名单再隐藏该值对应的全部事件。选项从新目录生成，找不到当前值便显示“任意”，但 draft.Npc/draft.Location 原值并未清空，继续过滤成空列表。SourceChoices 对失效来源已有保留逻辑，角色/地点没有。此问题即使保存了草稿仍存在，应保留可识别的失效选项或明确清空它。

### B9：来源详情隐去证据不完整状态

位置：[EventOriginPresentation.cs:43](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Sources/EventOriginPresentation.cs#L43)。

静态来源已识别或有多个候选，运行时记录为 Partial 且脚本仍匹配时，展示层从翻译后的“修改记录”标题开始截取，丢掉 source.partial-help 等完整性说明。夹具中运行时独立展示包含警告，合并后警告消失，并出现 source.no-modifications。应分别呈现原始提供者和运行时证据质量；这不等于已证明名单会误排原版。

### B10：本地名单文件超限会中断初始化（P3）

位置：[AiModExclusionService.cs:137](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Exclusions/AiModExclusionService.cs#L137)、[AiModExclusionService.cs:151](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Exclusions/AiModExclusionService.cs#L151)、[AiModExclusionService.cs:231](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Exclusions/AiModExclusionService.cs#L231)。

ReadBoundedFile 对超过 4096 字节的独立设置文件、超过 1MiB 的内置名单抛 InvalidDataException，但两个调用方 catch 过滤器没有覆盖该类型（它不是 IOException 的子类）。用合法设置 JSON 加空白超过长度限制即可复现 StartSession 异常逸出；这次初始化在创建下载任务前中断，首次启动规则为空，后续会话也可能保留旧规则。损坏/超限文件本应走日志及保守回退。本项是异常输入恢复缺陷；普通发布包中的种子并未超限，不宣称正常安装必然失败或整个游戏崩溃。


### B11：手柄 A 被处理两次

位置：[GalleryEventDetailMenu.cs:223](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/GalleryEventDetailMenu.cs#L223)、[GalleryToolMenu.cs:125](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/UI/GalleryToolMenu.cs#L125)、[GalleryPhotoMenu.cs:123](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/Screenshots/GalleryPhotoMenu.cs#L123)。

这些菜单在 receiveGamePadButton 内自行响应 A，但继承的 areGamePadControlsImplemented() 仍返回 false。实际游戏 1.6.15 的 Game1.updateActiveMenu 先调用 receiveGamePadButton，然后在菜单仍活动、该标志为 false 时，将同次 A 转成 receiveLeftClick。ModEntry 没有在这些菜单非编辑状态下抑制 A。

开启手柄及吸附菜单后，来源行/“更多筛选”的第一次操作保留同一菜单实例和焦点，模拟点击又操作一次，表现为先展开后关闭。照片页有至少两张照片时，第一次 Archive 后同一菜单重读集合、焦点仍是 103（移除按钮），第二次点击会归档另一张。这里是移入归档，不是永久删除。

本轮以 ILSpy 反编译确认当前游戏的实际分派顺序，并逐步核对生产菜单操作后的实例、焦点及按钮位置；未接入实体手柄或运行 UI。应统一手柄事件与鼠标模拟的职责，修复后实测来源展开、更多筛选、照片操作，确保一次输入只执行一次。
## 尚未实现与覆盖缺口

| 功能 | 正式版状态 | 定位 |
| --- | --- | --- |
| fork / switchEvent / switchEventFull 等分支的完整来源与修改链 | 未完整实现；现有来源主要针对 Data/Events 主定义 | [来源实现说明:43](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md#L43) 明确留待后续 |
| Data/TriggerActions 前置标记的来源归属 | 前置查询已实现，来源追踪未覆盖其提供者 | 同文档历史范围与当前扫描入口；不能按下游角色推断来源 |
| 任意纯 C# 动态事件、私有框架、自定义令牌和未生效 CP 分支的通用发现 | 没有通用支持；当前 CP/指定 AliveNpcs 适配不等于任意框架支持 | 已声明范围缺口；未知应保留未知 |
| DDFC 更新版条件、任意美化混装/禁用热切换的全面支持 | 指定协议可用；这些组合未完整适配或验证 | [外观范围:39](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/docs/TASK_2.6.0_APPEARANCE.md#L39) |
| 高级条件求解、路线规划、跨事件进度规划 | 未实现 | [2.0.0 Future Ideas](https://github.com/Ayulog/Stardew-Gallery/blob/f30e6019be8adf7ee460f70d9a7520cdae43936f/docs/RELEASE_2.0.0_SCOPE.md#L40)，只是历史构想，无现行交付承诺 |
| 回放期间使用原版 NPC 外观 | 远端未实现，本地只有可行性研究 | 本地 REPLAY_VANILLA_APPEARANCE_RESEARCH.md 明确“没有实现功能”；不是 2.8.0 漏交付 |
| AI 名单排除集成 GMCM、默认关闭并即时应用 | 正式版没有；本地已有未发布实现 | 2.8.0 使用独立 ai-mod-exclusion.json，默认开启，改设置需重新读档/重启；这是版本差异，不单列为 bug |

以下属于已取消/明确不做，不能累加成必须补齐的功能：历史冻结回放与自然剧情历史采集、旧独立 Preview 入口、精确历史选择回放、多人回放、美化来源选择/冲突协调器、婚礼/节日主流程/夜间 FarmEvent/影院专用放映、Overgrown/Earthy 专用画廊皮肤。现行普通事件回放、来源筛选、TriggerActions 前置查询均已实现，旧文档里的“尚未实现”不能脱离版本引用。

## 维护遗留与待验收

- 旧历史/预览源码仍编译，但 ModEntry 已断开历史采集和数据库接线。不要据此声称仍在记录玩家自然剧情。
- 实际发布 ZIP 有 58 个文件项，其中 SQLite 相关 27 项，压缩占 12,821,360 / 25,774,104 字节，约 **49.75%**。当前产品未接入历史数据库，这是可拆分的依赖和减包工作，非新增玩家功能。
- docs/MODULES.md:30 仍把 2.8.0 原始来源扫描称为“未发布”，属于 P3 文档债务。历史文档应标注被替代关系，不宜删除其历史证据。
- 来源页长模组名、小窗口/缩放、真实手柄导航、CP/SpaceCore/SVE 组合刷新、多美化组合及完整回放结束/跳过/异常/恢复失败流程仍需测试存档实机验收。现有隔离结果不能替代这些场景。

B1–B4 的关键夹具也已在实际下载的发布 DLL 上执行，结果一致，日志为 replay-probes/published-*-evidence.txt。

## 本轮验证与证据

| 验证 | 结果 |
| --- | --- |
| 远端 main、tag、manifest、项目版本、发布包版本 | 一致为 v2.8.0 / f30e601 |
| Checks 常规逻辑/外观/目录/来源/本地化 | 通过；目录符号链接专项因 IOException 跳过，不计为通过 |
| PersistenceChecks 名称/截图/存储 | 通过，使用临时数据 |
| 完整 Release 构建 | 0 警告、0 错误；EnableModDeploy=false |
| 实际 SMAPI 来源管线 | 36 项断言通过 |
| 重建 DLL 与下载包 DLL 的实际 SMAPI AssemblyLoader | 均通过；不调用 Mod.Entry |
| 12 种语言 | 各 538 键，键集合及已有占位符检查通过 |
| 新增审计夹具 | 独立复现上列缺陷；常规检查通过不覆盖这些新增边界 |
| 真实游戏/物理手柄/玩家存档 | 本轮未执行 |

环境：.NET SDK 8.0.425，CLR 6.0.36，Stardew Valley 1.6.15.24356，SMAPI 4.5.2.0，Harmony 2.2.2.0。Checks 的 NETSDK1138 为既定 net6.0 目标提示；不据此改变游戏兼容框架。加载器输出 warnings=4 为 PatchesGame 标志值，不是四条加载错误。

发布包 SHA256：`F648BFC029476E1C87C888A695D9ED72CDEB5B7FAF6C3B0206A3CF2E548B4C29`，与发布说明一致。
发布 DLL SHA256：`2587CD2D21C44A65EF96924ACD7C04F8F239D43CFB5716001091BFE3CAD053F3`。
本轮构建 DLL SHA256：`7013096335159EB3321CA8B89CBE6C7379F3F9B98A450F5164B36ACF55410959`；不以非可复现构建的字节差异认定源码不一致。
游戏 DLL SHA256：`7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE`。

本地证据根目录（均在本项目集合的独立副本内）：

- `.audit-workspaces/Stardew-Gallery-20261008/diagnostics/remote-audit-20261008/`：release.json、verification.json、构建及全部基础检查日志、实际发布包。
- `.audit-workspaces/Stardew-Gallery-replay-20261008/diagnostics/replay-probes/`：实际快照、爷爷命令、倍速、天气及 XNB/目录可达性夹具和输出。
- `.audit-workspaces/Stardew-Gallery-catalog-20261008/Checks/AuditCatalogChecks.cs`：分类、筛选、来源完整性、超限文件夹具；定向命令为 `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --audit-catalog`。

分类专项输出保存于 `.audit-workspaces/Stardew-Gallery-catalog-20261008/diagnostics/catalog-probes/run.log`；UI 调用链、游戏版本/哈希和反编译证据保存于 `.audit-workspaces/Stardew-Gallery-ui-20261008/diagnostics/ui-audit.md` 及同目录 ui-audit/。已经排除 AnyDateable 关系条件和“首页不能用手柄浏览第 19 位角色”的疑点，不计入缺陷。

主工作区的 Unreleased 中已有 B1–B5、B7–B9 的对应修复改动，B6 心数/别名部分也有改动，但没有进入远端或 Release。本轮没有验证这些本地修复是否全部正确。B6 的 GSQ 季节/天气分支仍未见修复；B10 的种子文件超限 catch 仍保留原实现；B11 对应菜单也未见本地修复。下一步应先完成 P1 修复验收，再处理剩余筛选/输入/来源问题，最后做实机回归并按授权发布。
