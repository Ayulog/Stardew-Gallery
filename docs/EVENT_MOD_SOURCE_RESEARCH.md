# 事件 MOD 来源查询：源码调研与实现建议

日期：2026-09-20。画廊基线：434319dc78dcc1107fdb7a0c5d98ebbade0ddc18，2.6.0。本文保留实现前的调研结论；2.7.0 的实际实现、验证和限制见 `EVENT_MOD_SOURCE_IMPLEMENTATION.md`。

## 结论

可以实现可靠的“当前事件定义由谁提供、哪些 MOD 对它产生了可观察修改”。推荐在 SMAPI 的实际资源加载过程记录逐键变化，优先使用操作中的 OnBehalfOf 内容包身份，其次使用 Mod 身份；CP 2.9.1 已传递这项信息。无需为了查询来源重新实现 CP 的条件、Include、令牌与补丁合并。

这不是任意 MOD、任意剧情的完整作者证明。源码证明接入信息存在，但本轮没有编写或运行追踪原型，尚不能承诺兼容率、性能或全部 Harmony 接入均可用。界面必须区分已观察来源、候选来源和未知。

当前任务仅做研究。没有修改运行时代码、版本、依赖或游戏安装，也没有恢复旧 AI 排除实现；来源展示不改变事件收录、解锁或回放权限。

## 1. 证据和版本

官方公开源码均已在本轮读取并固定提交，开发分支不等同本机发布版本：

| 项目 | 调研源码提交或本机版本 |
| --- | --- |
| Content Patcher 官方仓库 | Pathoschild/StardewMods，76565e83ede4bc8b3c293f1c659032ba9c39c213 |
| SMAPI 官方仓库 | Pathoschild/SMAPI，fa45e7de7214709bb8cf327b831408a25a72a0ac |
| SpaceCore 官方仓库 | spacechase0/StardewValleyMods，c1f7c3f8d8ce6e4d79425ab97ac011008883553d |
| Stardew Valley Expanded | FlashShifter/StardewValleyExpanded，7481ed586043802876d047d09aeaa3a6b7834858，CP manifest 1.15.11 |
| Event Repeater | MissCoriel/Event-Repeater，cff06484607f66210d2e680889a2c0d779fa95cb；根目录 manifest 6.5.8，子目录另有旧 6.4.8，不能混用 |
| 游戏本机程序集 | Stardew Valley 1.6.15.24356，托管 PE32+ AMD64 |
| SMAPI 本机程序集 | 4.5.2.0，托管 PE32+ AMD64 |
| CP / SpaceCore 本机程序集 | 2.9.1.0 / 1.28.4.0；已用 ILSpy 对关键类型还原并比对 |
| MH Event List 本地样本 | MHEventsList 程序集 1.1.0.0；历史记录称发布包 1.2.0，本轮以 DLL 身份为准 |

游戏部分来自本机合法安装 DLL 的 ILSpy 9.1.0.7988 反编译结果，不声称是官方原始源码。MH 部分同样是反编译样本。其他项目使用作者官方仓库，不依赖第三方源码镜像。

SHA256：

- Stardew Valley.dll：7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE
- StardewModdingAPI.dll：5E4C51BB3CD6616F5ADCB804769DBB7C6872B2478FAB786FB4CA00C72C7456BF
- ContentPatcher.dll：5203AF255C6A826A598470A642E0EB488FD943196354F79FE05FCD08BF6C941F
- SpaceCore.dll：ECF3140F9C96B56A06C1A67D140D88153EBDD84C636F4F9C10904A8F9658B3A6
- MHEventsList.dll：FA8A585AA9BA64F4265F5843D3104A023FFB79EF53008A0394C6185C25DCCADB

下载源码及本轮还原文件保存在被忽略的 diagnostics/event-source-research/，不纳入源码提交或发布包。

## 2. Content Patcher：内容包身份已经进入加载管线

[公开 API](https://github.com/Pathoschild/StardewMods/blob/76565e83ede4bc8b3c293f1c659032ba9c39c213/ContentPatcher/IContentPatcherAPI.cs) 提供条件解析、令牌字符串解析和令牌注册，没有公开的“按事件查询来源／列出已应用补丁链”方法。

[PatchManager](https://github.com/Pathoschild/StardewMods/blob/76565e83ede4bc8b3c293f1c659032ba9c39c213/ContentPatcher/Framework/PatchManager.cs) 的关键行为：

- GetCurrentEditors 按目标资产、补丁类型、IsReady 和语言选择可用补丁。
- ApplyPatchesToAsset 选择 Load 补丁后，向 e.LoadFrom 传入 loader.ContentPack.Manifest.UniqueID 作为 onBehalfOf。
- SortAndGroupEditPatches 将连续的同一内容包编辑分组；每组 e.Edit 同样携带内容包 UniqueID。
- ApplyEdits 执行分组内补丁；补丁错误会被捕获，因此“候选补丁存在”“IsApplied 标志”都不能代替最终键值变化证据。

本机 CP 2.9.1 的 PatchManager 反编译结果确认上述 onBehalfOf 行为，不只是开发分支新增功能。

这意味着第一版若只展示 MOD 名称，通常不需要深入 CP 私有补丁对象。若要展示具体文件、LogName、单条补丁操作和组内被撤销的写入，再增加 CP 专用适配器；SMAPI 分组层不能给出这些细节。

[EditDataPatch](https://github.com/Pathoschild/StardewMods/blob/76565e83ede4bc8b3c293f1c659032ba9c39c213/ContentPatcher/Framework/Patches/EditDataPatch.cs) 支持 Entries、Fields、TextOperations、FromFile 等路径。只扫描 JSON 的 Entries 会漏掉已有事件的文本修改、动态目标、导入文件和条件切换。反过来，全盘扫描 Mods 文件夹又会把未加载包、未引用文件或未激活分支算进去。

## 3. SMAPI：有操作身份，没有公开逐键来源服务

[AssetRequestedEventArgs](https://github.com/Pathoschild/SMAPI/blob/fa45e7de7214709bb8cf327b831408a25a72a0ac/src/SMAPI/Events/AssetRequestedEventArgs.cs) 的 Edit/LoadFrom 是公开接口，但 LoadOperations/EditOperations 集合是 internal；操作保存 Mod、OnBehalfOf、Priority 和执行委托。

[GameContentManager](https://github.com/Pathoschild/SMAPI/blob/fa45e7de7214709bb8cf327b831408a25a72a0ac/src/SMAPI/Framework/ContentManagers/GameContentManager.cs) 的实际流程：

1. 缓存命中直接返回；未命中才查询操作。
2. ApplyLoader 选择真正采用的加载器；冲突、异常、无效结果可能回退到原始游戏资源。
3. ApplyEditors 按优先级逐个调用编辑委托，之后校验资产类型。
4. 缓存最终对象，再通知资产完成加载。

[AssetReadyEventArgs](https://github.com/Pathoschild/SMAPI/blob/fa45e7de7214709bb8cf327b831408a25a72a0ac/src/SMAPI/Events/AssetReadyEventArgs.cs) 只携带资产名。单独订阅 AssetReady 能看到最终内容，却不能还原每一步由谁修改；同一地图有多个编辑者，也不能据此断言每条事件都由这些 MOD 修改。

[ContentCoordinator.GetAssetOperations](https://github.com/Pathoschild/SMAPI/blob/fa45e7de7214709bb8cf327b831408a25a72a0ac/src/SMAPI/Framework/ContentCoordinator.cs) 会缓存操作集合，DoesAssetExist 也可能访问它。因此，查询到操作不代表已经执行，包装委托必须幂等且只在真正调用时记录。本机 SMAPI 4.5.2 已核对相同行为。

获取 MOD 显示名、UniqueID 和版本可使用公开 IModRegistry；这只能解释一个已知身份，不能凭清单推导某条事件的提供者。

## 4. 游戏：事件字典没有作者字段，且加载之后仍会加工

本轮还原文件：diagnostics/event-source-research/game/StardewValley.GameLocation.decompiled.cs 与 StardewValley.Event.decompiled.cs。

GameLocation.TryGetLocationEvents（本轮文件约 15468 行）：

- 读取 Dictionary<string,string>；键为事件完整键，值为脚本，没有 MOD 来源字段。
- 玩家住宅可能映射到 Data/Events/FarmHouse。
- 把 FarmHouse 的 558291/558292 事件复制到其他地点；画廊已有 NativeGlobalEventCopies 处理这两个副本。
- Trailer_Big 会合并 Trailer 字典，并对 35/36 事件做位置和指令替换。这说明“资产完成加载时的脚本”与“地点最终返回的脚本”可能不同，不能简单把哈希不匹配都解释成未知 MOD 修改。

Event.DefaultCommands.Fork / SwitchEvent（约 1796 / 1868 行）会再次读取当前地点的事件字典；fork 也可从翻译资源读取脚本。根事件来源不自动覆盖分支来源。

Event.RegisterCommand（约 4007 行）保存指令委托，不保存使用它的剧情作者。找到某条指令来自 SpaceCore 的程序集，只能证明指令实现来源。

没有观察到加载器并不天然等于原版：追踪可能启用太晚，内容也可能来自直接替换的 XNB。完整观察到正常 RawLoad 基线时可标为“游戏基础内容”，并注明无法识别人工替换 XNB 的作者。

## 5. SpaceCore 和其他事件模组

| 对象 | 实际发现 | 对来源功能的含义 |
| --- | --- | --- |
| SpaceCore | 注册 damageFarmer、setDating、switchEventFull 等命令；PlayEvent 触发动作调用 Game1.PlayEvent；送礼事件配置也可触发已有事件 | 提供执行／触发能力不等于提供剧情；来源仍应追踪脚本定义 |
| SVE | CP 包通过 EditData 写入 Data/Events；有 When 和 i18n，C# 包另行注册 SVE_BadlandsDeath | CP 剧情归 FlashShifter.StardewValleyExpandedCP；C# 指令依赖单列，不能仅看前缀或登场 NPC |
| Event Repeater | 根目录 6.5.8 代码维护重复事件资源和已看事件移除逻辑 | 管理重复触发的框架不因此成为该剧情的作者；公开样本没有逐事件作者追踪服务 |
| MH Event List | 递归扫描 JSON，分析 EditData/Entries，向上查找 manifest，写入 ModUniqueId；多个索引只用 EventId | 可借鉴来源展示，但静态结果只能作候选，不能替代真实修改链 |

SpaceCore 证据：[注册和切换事件代码](https://github.com/spacechase0/StardewValleyMods/blob/c1f7c3f8d8ce6e4d79425ab97ac011008883553d/framework/SpaceCore/SpaceCore.cs)、[送礼触发代码](https://github.com/spacechase0/StardewValleyMods/blob/c1f7c3f8d8ce6e4d79425ab97ac011008883553d/framework/SpaceCore/VanillaAssetExpansion/VanillaAssetExpansion.cs)、[API](https://github.com/spacechase0/StardewValleyMods/blob/c1f7c3f8d8ce6e4d79425ab97ac011008883553d/framework/SpaceCore/Api.cs)。本机 1.28.4 的 SpaceCore 类型也已还原确认命令注册和切换逻辑。switchEventFull 会读当前地点资源，现有画廊 EventFragmentCollector 尚未识别它。

SVE 证据：[CP 剧情实例](https://github.com/FlashShifter/StardewValleyExpanded/blob/7481ed586043802876d047d09aeaa3a6b7834858/Stardew%20Valley%20Expanded/%5BCP%5D%20Stardew%20Valley%20Expanded/code/OtherEvents/GrandpasShed.json)、[C# 注册入口](https://github.com/FlashShifter/StardewValleyExpanded/blob/7481ed586043802876d047d09aeaa3a6b7834858/Stardew%20Valley%20Expanded/StardewValleyExpanded/ModEntry.cs)。数字事件 ID 2554902 由 CP 包定义，数字本身不携带 MOD 身份。

Event Repeater 证据：[AssetManager](https://github.com/MissCoriel/Event-Repeater/blob/cff06484607f66210d2e680889a2c0d779fa95cb/Framework/AssetManager.cs)、[ModEntry](https://github.com/MissCoriel/Event-Repeater/blob/cff06484607f66210d2e680889a2c0d779fa95cb/ModEntry.cs)。此结论限于抽查的公开版本，不代表最新发行包完全一致。

MH 证据：本轮重新还原的 diagnostics/event-source-research/installed/MHEventsList.Core.EventRegistry.decompiled.cs，ScanContentPatcherEvents、ParseChangeElement、ModPathByEventId。其发行页为 https://www.nexusmods.com/stardewvalley/mods/39885；该地址仅标识样本，技术结论来自本地 DLL。

## 6. 三种方案比较

| 方案 | 优点 | 局限 | 建议 |
| --- | --- | --- | --- |
| 静态扫描 CP JSON | 容易做出处候选、文件链接 | 重写条件上下文成本高；漏 Load/文本编辑/C#；可能把未生效内容误算进去 | 仅补充诊断 |
| 只适配 CP 私有补丁对象 | 能取得包、文件、补丁名 | 与 CP 内部结构耦合；仍漏其他框架与 C#，候选补丁不代表实际改动 | 需要文件级信息时再加 |
| SMAPI 运行时操作追踪 | 使用真实应用顺序和结果；已有 OnBehalfOf 能归到 CP 包；覆盖标准 C# 资源编辑 | 需要内部反射／Harmony，晚启用和旁路写入无法保证完整 | 推荐主线 |

## 7. 推荐实现轮廓（待原型验证）

### 7.1 观察操作，而不重放操作

在画廊 Entry 尽早安装版本受控的观察适配器。候选接入是 ContentCoordinator.GetAssetOperations 返回的内部操作集合：为目标字典的 GetData / ApplyEdit 添加观察包装，保留身份、顺序、优先级、返回值和异常语义。集合缓存会重用，不能重复包装；“检测存在”不应制造来源记录。

还需要加载周期的开始、基线和最终成功边界：候选为 GameContentManager.LoadExact<T>、ApplyLoader<T> 和 ApplyEditors<T>。这些是具体研究目标，不是已验证的 Harmony 补丁清单；泛型共享、object 请求转具体字典、嵌套资源请求都需原型验证。避免一开始使用大范围 IL 改写。

每次实际编辑前复制原始键值，之后比较新增、改变和删除的键。string 不可变，可按字典浅拷贝保存值。不能仅保存原字典引用。来源优先取 OnBehalfOf.Manifest，缺省才取 Mod.Manifest；框架名可作为执行者附注。

加载器候选必须等 SMAPI 接受结果后才能认定为提供者；抛异常、无效类型、独占冲突回退都不能留下“成功提供”的假记录。编辑抛异常也可能已经部分改动：保留 SMAPI 的处理方式，记录残留差异和不完整状态，不能自行吞掉或重试原委托。

只观察本来发生的操作，绝不为了取证把编辑器再次执行。追踪自身异常应限频记录并降级为未知，不影响原资源内容和画廊回放。

### 7.2 来源的准确语义

建议展示“提供者”和“修改者”，不承诺版权作者或最早历史作者。

- 游戏基线中的键：提供者为游戏基础内容。
- Load 整张字典：提供者是实际采用的内容包，即使文件中复制了原版文本，也不能凭文本相同推断历史作者。
- 新增键：提供者是当前加载周期首次引入该键的操作身份。
- 改变已有键：保留提供者，追加修改者与顺序；覆盖、删除、再新增应保留可审查历史，删除后新定义开启新世代。
- 相同值写入、同一 CP 分组内改后又还原：前后差异无法发现，不能称为所有写入历史。
- A 改写后被 B 全文覆盖：A 曾修改不代表 A 的文本仍在最终结果中；界面写“本次加载修改记录”，不能写“所有当前有效作者”。

事件身份沿用现有 AssetName + EventId；来源索引需更细，增加实际资产语言、RawEventKey、脚本指纹和加载世代。同资产同 ID 不同条件键不能串来源。出处映射与游玩地点分开保存，用已核实的游戏规则处理 Trailer_Big 和全局副本。

可采用独立 EventSourceInfo / EventSourceIndex，不把 CP 对象塞进菜单或持久化数据库。没有完整证据时返回 Unknown 或 Partial；不要给未知事件默认贴“原版”。

### 7.3 作用域与分支

第一步覆盖当前画廊已经收录的地点事件根定义，清楚标注“当前主事件来源”。后续补齐分支关系：保留每个分支的资产、键和来源，支持原生 fork、switchEvent 和已核实的 SpaceCore switchEventFull。

当前 EventFragments 只有脚本列表和缺失键，丢失成功分支的资产身份；仅把 Scripts 拼在一起无法展示分支出处。fork 翻译分支可能需要追踪 Strings 等具体字典，不能假设所有脚本都在 Data/Events。

前置事件是另一个目录类型，可能来自 Data/TriggerActions。若“每个事件”包含查询器中的前置标记，也必须独立覆盖其定义来源，不能把解锁标记写入者直接当成后续剧情作者。

纯 C# 临时 new Event、Harmony 直接改地点返回结果、直接修改缓存字典、夜间 FarmEvent 和自定义框架私有数据不一定走这条资源管线。可为确有需求的框架提供来源登记接口／专门适配；无证据时保持未知，不通过堆栈或 NPC 名称猜成确定来源。

### 7.4 生命周期、性能和现有代码入口

- 入口接线：ModEntry；目录接合：EventAssetCatalog、ResolvedEventReader、ResolvedEventIndex；展示：GalleryEventDetailMenu。无需重写回放服务。
- 只观察目标资产和实际字典，不在菜单绘制时扫描全盘或遍历所有补丁。
- 记录需要按实际加载实例／世代隔离；保存指纹而不是永久保存全部剧情文本。估算开销为各编辑组所处理字典条目数之和，实际耗时待量测。
- 用 AssetsInvalidated、LocaleChanged、读档和返回标题等边界失效证据。对更早加载的缓存只能显示证据缺失；不能打开详情就自动清空所有游戏资源缓存。
- 比对当前脚本与已记录最终快照，发现无法解释的旁路变化后降级；这只能检测部分变化，不能恢复未知作者。
- 尽管回放仅限单人，画廊读取和资源追踪仍要考虑屏幕／存档上下文，不能把玩家 A 的条件分支记录直接用于玩家 B。
- 内部方法签名检查失败时停用来源追踪，保留原功能。不因查询来源改变解锁、执行条件、存档或回放内容。

### 7.5 建议界面

详情页先显示一行来源，可展开进一步信息。例如以下仅为交互示例：

> 提供者：Stardew Valley Expanded
>
> 本次加载修改者：某翻译包、某兼容补丁
>
> 状态：已追踪主事件；分支来源未全部确认

展开后显示 MOD 名称、UniqueID、版本、资产路径、完整键和证据状态。默认把框架放在技术详情中；文件路径只在确实获取到 CP 补丁证据时展示。后续可加来源筛选，筛选规则需要区分“提供者”与“任一修改者”。

## 8. 实施前验证清单

本轮没有执行以下原型／游戏检查，它们是进入实现后的验收要求：

1. 无 CP 的原版事件；CP EditData 新增事件；CP Load 整张事件字典。
2. A 新增、B 修改、C 覆盖；删除再新增；相同值覆盖；同一包组内改后还原。
3. Include、FromFile、i18n、When、动态 Target、Fields/TextOperations；配置和时间变化触发资源失效。
4. 相同地图不同事件分别由不同 MOD 修改；不同资产同 ID；同资产同 ID 不同完整条件键。
5. C# e.Edit、e.LoadFrom、替换整个字典；两个 Exclusive loader 冲突；加载器无效返回；编辑中途异常。
6. 嵌套加载、重复 GetAssetOperations、DoesAssetExist、缓存命中、晚初始化；确认原委托只执行一次。
7. 原生 fork/switchEvent、SpaceCore switchEventFull、翻译分支、缺失分支、循环引用。
8. FarmHouse 全局副本、Trailer_Big 原生转换、直接缓存修改；不能误标来源。
9. 返回标题、换档、换语言、不同屏幕上下文；确认没有跨上下文串记录。
10. 标准 CP + SpaceCore + SVE 的测试副本，与 CP patch summary/已知样例互证；summary 仅为辅助，不代替逐键差异。
11. 关闭观察器前后目录、脚本、资源调用次数保持一致；测量首次读档及大事件包的 CPU／内存开销。
12. 追踪降级时画廊仍可打开、正常回放；来源查询不修改玩家进度或游戏资源。

## 9. 本轮交付与未验证项

已完成官方源码、当前画廊源码、本机游戏／SMAPI／CP／SpaceCore 关键类型和 MH 样本的只读调研。关键结论已用本机发布版本交叉确认；没有源码实现、编译、游戏运行、性能测量或跨版本兼容测试。

下一步建议先做隔离的“实际新增／修改来源记录”原型，验证操作包装、加载成功判定和生命周期；验证通过后再接入详情页。先把已观察的来源做准确，再扩展分支和文件级诊断。
