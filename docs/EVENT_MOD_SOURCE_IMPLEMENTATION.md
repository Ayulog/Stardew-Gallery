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
