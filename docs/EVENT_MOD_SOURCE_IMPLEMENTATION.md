# 2.8.0 原始定义匹配

2026-09-21：用户新增模组后日志仍显示运行时观察资产为 0。当前实现把原始定义查询独立出来：自动扫描已加载模组中的事件文件，与画廊当前事件按资产路径＋事件 ID 匹配；运行时修改链作为可选补充。

## 文件扫描与归属规则

- `EventDefinitionSources` 从公开 `IModRegistry.GetAll()` 获取已加载的 Content Patcher 包、AliveNpcs 与其场景内容包。`ModDirectoryDiscovery` 按 SMAPI 启动参数 `--mods-path`、`SMAPI_MODS_PATH`、默认游戏 Mods 目录的优先级确定扫描根，校验它包含画廊自身目录，再读取 manifest 并与公开清单身份配对。跳过点号目录与链接，遇到模组清单停止深入其资源目录；重复身份不猜路径。不访问 SMAPI 内部属性。
- `ContentPackEventScanner` 从 `content.json` 跟随 Include/FromFile；读取 EditData 的完整字符串 Entries 与 Load 字典（JSON、XNB），跳过仅修改字段/文本或删除的操作。不会把包中未引用的备份 JSON 当作来源。
- 通过 SMAPI 已有 Newtonsoft 兼容单引号、未引号数字键及字面多行字符串；支持注释、尾逗号、BOM、模组 ID、配置值、有限动态令牌分支及 Target/TargetWithoutPath。扫描不执行 When；它确认声明存在，不确认该分支此刻生效。动态路径无法解析、文件损坏或超出扫描上限时记录问题。
- 使用普通 MonoGame `ContentManager` 读取原始 XNB，避开 SMAPI 与当前内容缓存；只读取字典并保留标识/指纹，不修改游戏文件或保留剧情全文。
- `AliveNpcEventScanner` 仅针对 `Lucas.AliveNpcs` 的已核实场景格式，读取内置 `assets/scene_events` 和框架内容包 `alive-npcs-ec.json` 的 `modes[].sceneEventDirectories` 声明目录。使用 schemaVersion、location、eventKey、scene（script 或由 i18n 解析的 scriptKey）匹配完整定义；不执行触发动作，不扫描任意 C# 模组的全部 JSON。
- `EventDefinitionIndex` 按资产＋ID匹配，后续完整条件键或脚本变化不会抹去原始声明。同一 ID 在其他资产中不相互匹配。原版原始字典存在该身份时优先标识游戏基础定义；没有证据不会默认原版。
- 如果多个包声明同一事件，通过单向依赖关系排除依赖原包的翻译/兼容覆盖者；独立或循环依赖候选保留歧义，列出匹配文件。此结果不承诺法律作者或模组发布历史。
- 按已核实的 Stardew 1.6.15 原生合并规则添加回退：Trailer_Big 无直接定义时查 Trailer 同 ID，保留真实文件路径；35/36 的坐标/指令转换不会抹去原始匹配，不泛化到其他地点。
- 首次打开画廊目录时扫描（来源筛选与名单排除共用），每次返回标题或事件资源失效时重置，下一次查看按需重扫；`gallery_event_source rescan` 可手动刷新。不在绘制时重复扫描。

## 界面与配置

详情原有“来源”入口自动显示原始提供者，点击列出定义文件。`EventOriginPresentation` 不允许运行时最后提供者覆盖已有的文件来源或消除文件冲突；只有静态完全未匹配时，明确标注的运行时观察结果才可作为补充回退。

GMCM 的 `EnableEventSourceDiagnostics` 改称“追踪事件修改（重启生效）”，默认仍关闭，仅控制可选修改记录。修改追踪关闭、待重启或 SMAPI 版本不适配时，静态来源仍可用。12 种语言文案同步更新。本轮未新增 Harmony，保留原有两个可选运行时接入点。

## 游戏内模组目录发现修复

2026-09-21 11:20 的真实日志确认：SMAPI 阻止通过反射 API 读取内部 ModMetadata.DirectoryPath，导致0个包/0文件/0定义，122条拒绝提示。先前1645条离线匹配验证仅证明解析和匹配算法，未经过这个游戏内目录获取边界，不能作为实机成功结论。

修复移除该内部反射，改用启动配置对应的单一 Mods 根和公开加载身份配对。未找到已加载包时汇总日志给出实际/预期数量并提升为警告。目录发现24项检查通过；另有2项实际Windows junction检查通过，符号链接创建因本机权限跳过。

生产服务隔离回归通过真实SMAPI加载器读取构建DLL，直接调用 EventDefinitionSources.EnsureScanned/Read：helper只暴露公开目录/注册表，访问Reflection即失败，IModInfo没有DirectoryPath，不注入已解析包路径。旧DLL在该夹具0包/0定义并失败；修复DLL通过9项夹具断言，包括自定义根、嵌套包、同ID不同资产、缓存与重扫。

使用最新日志确认的122个加载身份和真实默认Mods目录，生产服务扫描63/63个事件包、1893文件、2958定义，耗时2120ms；对最近画廊目录1645条事件逐条调用Read，1645条Identified、0未知、0歧义，Reflection调用0。保留11条未解析路径/文件提示，未阻断这份目录的覆盖。DLL SHA256：8A16309CAAE15D70DDD8949584D5FA7450A1031C3209B09216154873637EC7F8。此验证经过实际生产扫描入口，但未执行Mod.Entry、游戏循环或详情UI，不能当作游戏内验收。

## SMAPI 加载兼容修复

2026-09-21 10:39 的真实日志在程序集检查阶段拒绝 `JToken.ToString(Formatting)` 方法引用，画廊被跳过；普通构建与离线扫描此前均未覆盖这个加载边界。规范化改用参数为空的 `JToken.ToString()`，保留对象/数组数据、前导注释处理和字符串语义，仅输出缩进不同。Nexus 页面失效是独立更新问题，manifest 已移除 `Nexus:51593`，保留 GitHub 更新及稳定模组标识。

当前13.0.4游戏依赖的隔离进程未复现旧 DLL 的拒绝；显式使用本机SDK已有Newtonsoft13.0.3的隔离对照准确复现旧调用失败，而新版通过。此对照证明方法版本兼容差异，但未证明真实启动时的依赖选择顺序。新版已通过实际 SMAPI 4.5.2 `AssemblyLoader` 检查（重写开启、不忽略兼容错误），并直接调用加载后 DLL 的 JSON 规范化验证宽松对象/数组格式，未执行模组入口、启动游戏或访问真实存档。

## 验证范围与待测

定向检查覆盖完整声明、修改后匹配、同 ID 不同资产、依赖/循环依赖歧义、Include 图、路径边界、令牌、缓存、原版映射，以及追踪关闭/缺失时的详情显示。本轮 Release 构建通过（0 警告、0 错误），完整逻辑检查通过，其中静态扫描57项、原始来源展示12项；12语言各537键及占位符一致。使用最近画廊快照只读扫描61个CP包、AliveNpcs核心及EC包，1893个引用/场景文件，共2958条模组定义和249条原版候选；1645个画廊事件全部找到唯一来源，0未知、0歧义，耗时约2.2秒。保留11条非事件路径、可选缺失或未解析文件提示；这不保证未来任意模组都可识别。详细报告在本轮diagnostics/交接记录中保存；隔离扫描不等于进入游戏验收。

测试时重启 SMAPI、读档、打开原版与 SVE/RSV/其他新增角色事件详情，确认来源和匹配文件；关闭修改追踪后来源应保留。检查长模组名、小窗口、缩放及手柄导航。当前未完成真实游戏 UI 验收，除已适配 AliveNpcs 外的纯 C# 生成事件、不支持的自定义框架或令牌仍可能未知。XNB 被人工替换时无法还原历史作者。分支和所有修改者的完整追踪留待后续。

## English

Version 2.8.0 scans event declarations in loaded Content Patcher packs, supported AliveNpcs scene files and raw game XNBs. Original providers are matched by asset plus event ID, independently of optional runtime tracing. Reachable Includes/FromFile, full Entries and Load dictionaries provide file evidence; declared dependencies help distinguish overrides, while independent conflicts stay ambiguous. The scan does not evaluate patch conditions or establish complete authorship. No new Harmony hooks were added. Actual installed-pack coverage is a read-only isolated check; in-game UI acceptance is still pending.

---

# 已发布 2.7.0 的运行时实现记录（历史）

以下内容描述已发布的运行时方案。当前原始来源入口与配置语义以上面的2.8.0实现为准；原运行时观察算法未改动。

# 事件来源追踪与详情页 / Event source tracing and details

版本：2.7.0，2026-09-21。基于 `EVENT_MOD_SOURCE_RESEARCH.md` 的运行时调研，提供默认关闭的事件来源追踪与详情查询。发布用法见 `RELEASE_2.7.0_NOTES.md`。

## 本轮范围

实现当前 `Data/Events/*` 主事件定义的提供者、逐键修改记录和证据状态。独立来源索引不依赖游戏，也不改变事件收录、解锁、条件判断、回放或持久化数据。已接入事件详情页：地点下方显示来源摘要，点击可在同页查看主事件提供者、修改记录和证据状态；再次点击返回条件。GMCM 提供重启生效的来源追踪开关。保留开发者控制台命令；来源筛选尚未实现。

`Complete` 表示当前主定义在本次可观察加载中有完整证据，不等于历史作者、所有写入、所有分支或当前文本的全部作者。`Partial` 表示异常编辑或未观察改动造成缺口；`Unknown` 不会被自动标为原版。

## 控制 Harmony 范围

新增来源功能仅有 **两个** 非泛型 Harmony postfix，均在显式启用实验配置后安装：

1. `ContentCoordinator.GetAssetOperations(IAssetInfo)`：在原调用返回后幂等包装实际 `GetData` / `ApplyEdit` 委托；不改变操作集合、优先级、执行顺序或调用次数。存在性检查只取得操作，不产生来源记录。
2. `AssetDataForObject(IAssetInfo, object, Func<string,string>, Reflector, Action<object>)` 构造器：在本机 SMAPI 4.5.2 已接受加载器结果或成功完成 RawLoad 后取得基线；也观察 SMAPI 对错误编辑结果的回退。通过规范化委托的实例目标识别内容管理器。

结束观察使用 SMAPI 公开 `AssetReady` 事件，直接读取对应管理器缓存，不调用资源加载器获取证据。编辑结束时立即复制前后字典，避免把后续 `AssetReady` 监听器的改动算给最后一个编辑者。嵌套同名加载仍处于 `AssetsBeingLoaded` 时不提前完成外层记录。

语言变化和返回标题使用公开事件清空证据；读档完成保留本次加载取得的证据，避免缓存命中不再触发 AssetReady 而永久丢失来源；`AssetsInvalidated` 使对应资产和在途观察失效。公开 `UpdateTicked` 清理未到达完成事件的失败请求，避免持有暂存剧情文本。不主动清空游戏资源缓存；失效前已缓存且未重新自然加载的内容会显示未知。

不补丁 `LoadExact<T>`、`ApplyLoader<T>`、`ApplyEditors<T>`，不使用 transpiler。本机 Harmony 2.2.2 / .NET 6.0.36 的隔离实验发现，对闭合引用类型泛型方法安装 Harmony 补丁会影响共享泛型上下文，不能作为安全接入方式。

适配器只接受程序集版本 `StardewModdingAPI 4.5.2.0` 并校验内部签名。其他版本或观察器自身异常会停用来源证据并限次记录警告；原加载和编辑委托照常执行。

## 代码组织与证据

- `Sources/EventSourceInfo.cs`：资产、实际语言、屏幕/会话/管理器上下文、完整键、SHA256、加载世代、定义世代、提供者/执行框架、顺序修改链。
- `Sources/EventSourceIndex.cs`：独立加载周期、基线、编辑差异、完成发布、失效和当前脚本校验。已完成索引只保存指纹，不保存剧情文本。
- `Sources/SmapiEventSourceObserver.cs`：两个内部接入点和 SMAPI 公开事件观察。
- `Sources/EventSourceDiagnostics.cs`：控制台查询以及既有诊断目录中的 `event-source-latest.json`。
- `Sources/EventSourceDetails.cs` / `EventSourceDetailLookup.cs` / `EventSourcePresentation.cs`：在打开详情时检查实际资产、完整条件键和当前脚本后读取证据，生成12语言的来源摘要与详情；绘制阶段不读取资源。不匹配旧目录快照的内容不能借旧指纹显示来源。
- `GalleryEventDetailMenu`：地点下方的来源入口与同页条件/来源切换，分别保留滚动位置并提供键鼠/手柄导航。
- `Checks/EventSourceChecks.cs`：不依赖游戏的行为检查。
- `SourcesRuntimeChecks/`：依赖本机游戏/SMAPI DLL 的独立内存加载检查；不安装 MOD、不启动游戏、不读取玩家存档，不随发布包分发。

身份优先取实际操作的 `OnBehalfOf` 内容包清单，再取执行 MOD 清单；二者分开存储。新增键建立提供者，覆盖保留提供者并追加修改记录。删除后再新增建立新定义世代，同时保留旧世代来源。相同值写入和同一操作内改后还原不可见，不虚构记录。抛异常编辑只记录实际保留差异并降级。

## 使用与测试

1. 在 GMCM → Stardew Gallery 设置中开启“事件来源追踪（重启生效）”，保存后退出并重新通过 SMAPI 启动游戏。未安装 GMCM 时仍可设置 `config.json` 的 `EnableEventSourceDiagnostics=true`。
2. 读入测试存档，打开任意事件详情。在地点下方直接查看来源，点击该行可展开提供者、修改记录和当前资产/完整事件键，再点击返回声明条件。鼠标和手柄均可操作。
3. 来源追踪未启用、配置等待重启、适配不可用、证据缺失和不完整均有独立说明。未知来源不默认为原版；当前仅标识主事件，尚未确认分支来源。
4. 可继续用 SMAPI 控制台 `gallery_event_source status` 和 `gallery_event_source <location> <event-id>` 输出诊断 JSON。详情页不要求输入控制台命令。
5. 分别检查原版剧情、模组新增剧情及被翻译/兼容包修改的剧情；核查长模组名、小窗口、UI缩放、来源展开滚动和返回条件后的焦点/滚动保留。

配置更改需保存并重启生效，不在 GMCM 切换时重复安装 Harmony；默认关闭。菜单读取证据不强制清空或重载游戏缓存，晚观察的旧缓存可暂时显示未知。
## 当前限制和后续验收

- 仅追踪本次启用后可观察、成功进入缓存的 `Dictionary<string,string>` 主定义。无缓存临时加载、早于观察器的缓存和被绕过的资源路径可能没有证据。
- 未追踪 `fork` / `switchEvent` / `switchEventFull` 分支、翻译分支、`Data/TriggerActions` 前置标记、纯 C# 临时事件或框架私有数据。
- `Trailer_Big` 的原生转换及全局复制尚无专门映射；不匹配的资产、实例、键或脚本会降级，不能把原根定义来源直接当作转换后来源。
- 第三方 Harmony 直接替换 SMAPI 内部操作集合、改写 RawLoad 或私有返回路径不在标准管线保证范围；指纹只能检测部分旁路变更。
- 原始游戏基线只能标识游戏资源加载路径，不能识别人为替换 XNB 的作者。SMAPI 分组层也不能提供 CP 文件名或组内撤销的修改。
- 跨屏幕/会话证据不复用；未以多屏真实游戏验证该边界。
- 后续须在 CP 2.9.1 + SpaceCore + SVE 测试环境验证真实包、动态条件失效、语言切换、加载耗时和内存，继续核查详情页的真实游戏表现。公开检查和内存管线验证不能代替这些实机验收。

## 验证与已修复问题

- 主项目 Release 构建通过，0 警告、0 错误；常规逻辑与持久化检查通过。来源逻辑检查 72 项，来源详情语义检查 18 项，12 种语言各 525 个键及占位符一致。
- `SourcesRuntimeChecks` 包含 36 项真实 SMAPI 管线断言，覆盖委托单次执行、缓存命中、类型转发、替换与异常回退、内容包/执行者身份、嵌套加载、GC 保活、共享实例歧义、失效与失败清理，以及中文资源和只读原版 Town XNB 加载。环境为 SMAPI 4.5.2.0、Harmony 2.2.2.0、.NET 6.0.36。
- 修复读档后来源全部未知：游戏在 SaveLoaded 之前已加载事件资源，原回调随后清空了证据；缓存命中不会再次触发 AssetReady。现在 SaveLoaded 保留本次加载证据，返回标题、语言变化和资源失效继续拒绝旧记录。状态诊断包含观察资产数量和会话编号。
- 中文内容包及真实原版 XNB 回归确认，SaveLoaded 后缓存来源保持可查，且不会重复执行加载/编辑回调。会话清空后不复用旧证据，自然失效重载后恢复追踪。
- 构建和发布包排除诊断、运行时检查夹具、游戏/SMAPI/Harmony DLL、个人配置、截图、名称和存档。来源检查只保存内存证据，不改写玩家数据。
- 当前未完成修复后详情界面、实际手柄输入及真实 CP/SVE 组合的游戏内验收。隔离字体渲染不能当作完整界面验证；嵌套完成测试也不等于游戏递归 XNB 分支已实测。详细覆盖见 `SourcesRuntimeChecks/README.md`。
## English

The 2.7.0 opt-in source tracker records the provider and observable edits of current main `Data/Events` definitions. It adds exactly two non-generic Harmony postfixes; completion and invalidation use public SMAPI events. Generic content methods are deliberately excluded after an isolated test exposed shared-generic-context corruption. The adapter is pinned to SMAPI 4.5.2.0 and fails closed for source evidence while leaving normal content operations available.

Enable source tracing in GMCM (or set `EnableEventSourceDiagnostics=true`), save and restart SMAPI. Open event details or use `gallery_event_source status` and `gallery_event_source <location> <event-id>` for diagnostics. The default is off. Missing or mismatched evidence stays Unknown/Partial; the query never forces cache invalidation. Event details now show a source row below the location; click it to switch between requirements and source details. GMCM includes a source tracing toggle which takes effect after saving and restarting. Branch provenance, TriggerActions provenance and native transformation mapping remain outside the 2.7.0 feature scope. In-memory runtime checks do not establish in-game CP/SVE acceptance or performance.
