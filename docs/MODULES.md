# 模块维护入口

| 模块 | 维护入口 | 依赖与职责 |
| --- | --- | --- |
| 事件来源 | `Sources/ModDirectoryDiscovery`、`PackFiles`、`ContentPackEventScanner`、`AliveNpcEventScanner`、`EventDefinitionIndex`、`EventDefinitionSources`、`EventOriginPresentation`、`EventSourceDetails` | 只读扫描已加载 CP、AliveNpcs 场景与原版 XNB，按资产＋ID匹配原始来源，列出候选文件和其他声明者。`PackFiles` 按 SMAPI 方式解析包内路径（非 Windows 不区分大小写；包目录可以是链接，包内链接不跟随）并记录文件指纹，供只重扫变化的包。扫描不完整的包使覆盖包的匹配降级为“证据不完整”。来源只供显示与查询，不影响收录、解锁和回放。 |
| 当前事件资料 | `GalleryCatalogCache`、`Catalog/EventAssetCatalog`、`NativeGlobalEventCopies`、`ResolvedEventIndex`、`GalleryCatalogBuilder`、`GalleryCatalog` | 从当前游戏读取并分类；供其他模块取资料，不创建菜单或回放。只有多个候选的事件做原生前置检查；候选选择、角色和配偶都没变时复用上次的目录。 |
| 条件与前置 | `GalleryConditionPresentation`、`ConditionParser`、`ConditionEvaluator`、`RuntimeStateReader`、`ConditionStateReader`、`StoryDependencyLookup`、`PrerequisiteCatalog` | 解析与判定按游戏 1.6.15 的语义（`SourcesRuntimeChecks --conditions` 与游戏代码对拍）。当前TriggerActions标记和明确内部解锁依赖单独建资料；只有正向引用算前置，否定引用只作跳转链接。不生成可播放事件。条件与动作、已获得状态分离。 |
| 自助搜索 | `StorySearchIndex`、`QueryFilter`、`GallerySearchFilter`、`PinyinTable` | 预计算角色/地点/原始来源，按类别、角色、地点、来源 Mod ID 和进度筛选，并支持来源名称搜索；条件只在详情页展示。中文界面下 `PinyinTable` 让拉丁字母按拼音匹配名称、地点和角色（每个汉字可取任一读音的开头，须是连续的字）；读音表 `assets/pinyin.txt` 由 `tools/Generate-PinyinTable.ps1` 从 Unihan 生成，不手工编辑。 |
| 查询名称 | `GalleryLocationNames`、`GalleryCharacterNames`、`GalleryNameText` | 优先现成译名，识别缺译诊断串；角色别名按剧情解析，地点旧名和已核实副本只在查询层分组。 |
| 玩家命名 | `EventNameStore`、`GalleryRenameMenu` | 本机存档共用的用户别名，按资产+去空格的ID定位；落盘后原子替换，损坏时回退到最近的备份，备份最多 5 份。所有分屏共用一个实例，不写游戏进度。 |
| 页面与共用绘制 | `GalleryMenu`、`GalleryCharacterMenu`、`GalleryQueryMenu`、`GalleryEventDetailMenu`、`GalleryDrawing` | 按资料画界面，写`GalleryPageState`，通过`IGalleryNavigation`发出操作。每帧绘制只用缓存的结果：角色每秒按名称汇总一次，回放权限里的存档目录与节日每天读一次，条件文字的换行按文本和宽度缓存。 |
| 文字输入 | `GalleryTextBox`、`SteamTextInput`、`GalleryTyping`、`GalleryTypingKeys` | 手柄选中输入框时先请 Steam 显示键盘（Steam Deck、大屏幕模式，可输入中文），Steam 显示不了再用游戏键盘。提交的文字替换输入框内容，等手柄按键松开再按回车，免得同一按键落到页面上。`Steamworks.NET` 由游戏提供，只在 `SteamKeyboard` 里引用；分屏时不用 Steam 键盘。搜索框的回车只列出候选（`FinishSearch`）。实体键盘：输入框被选中时，`GalleryTyping` 在 SMAPI 的 `UpdateTicking` 把新按下的键交给游戏的 `InputState.IgnoreKeys`（游戏在窗口重新获得焦点时也这样做），SMAPI 读键盘时经过它，所以这些键直到松开前都不进 SMAPI 的输入事件、其他 Mod 的快捷键和游戏；文字走窗口文本输入，不受影响。游戏的粘贴要在按键状态里找 Ctrl+V，因此由画廊用游戏的 `DesktopClipboard` 粘贴；Esc 结束输入。不补丁 SMAPI。 |
| 导航 | `GalleryApplication`、`GalleryPageHistory` | 唯一菜单构造入口，保存返回链和页面状态；不持有一串菜单闭包。 |
| 回放 | `ReplayService`、`ReplayAccessPolicy`、`ReplayCoordinator`、`ReplaySaveGuard`、`ReplaySnapshot` | 统一权限、确认、启动、运行保护、恢复；不认识相册/查询具体菜单。 |
| 截图封面 | `GalleryPhotos`、`EventPhotoStore`、`PhotoCapturePatches` | 明确事件身份和存档身份，独立捕获、缓存、存储；照片页的返回交给导航。100% 缩放时只在抓拍那一帧让世界画到 `Game1.screen`。目录备份每个事件最多 5 份，移除的照片不删除。 |
| 根接线与配置 | `ModEntry`、`ModConfig` | 构建模块、订阅SMAPI、注册GMCM；普通回放开关是配置，一键解锁是当前存档状态（联机农场工只在本次会话）。分屏时每位玩家各有 `GalleryApplication`、`GalleryPhotos` 和解锁状态（`PerScreen`）。开启调试诊断时，`GalleryFrameProbe` 每 5 秒记录画廊页面的帧耗时。 |

相册与查询器共用资料、条件及回放结果；页面不调用其他页面获取资料。`GalleryApplication`统一创建菜单；`GalleryDrawing`不依赖某个具体菜单。纯模块通过对外方法验证，游戏/图形部分按需用隔离或实机证据。

修改分类、权限和副作用处理分别在对应模块完成，不能通过调整某页的按钮可见性代替权限判断，也不能用NPC归属代替剧情分类。

dev.4已按用户要求移除定义来源功能及数据表。地点分组保留所有原始资产/ID身份；Kenneth OFF/ON按地理位置归山脊、AlissaDate归山脊崖边，不能据此替换实际场景。图片场景和公主屯不按入口房屋强行归并。临时演员只凭明确命令/已核查别名归一，无法确认为人物的动画标识不占角色选项。本地名称证据见`drafts/星露谷画廊/research/20260909-query-corrections/names-and-locations.md`。

dev.5在查询索引中进一步聚合相同地点显示名，保留独立事件身份；注册的隐藏不可社交圆点分身在Key查找前先归到本体。`LocationNameFallbacks`只提供已核对地点的可读描述，使用原地点/角色名称和本地化后缀组合，不参与执行。当前全量盘点与回退依据见`drafts/星露谷画廊/research/20260909-query-name-followup/REPORT.md`。

dev.6的`NativeGlobalEventCopies`只识别游戏TryGetLocationEvents注入的两段爷爷剧情，匹配原FarmHouse完整键与脚本后省略异地副本，避免每个地图虚增同一剧情。不得将该规则泛化到不同键/脚本或其它ID；输入原字典保持只读。原版地名、Cellar显示组及SVE替代人物归并继续位于名称层。2.10.0 起它也处理游戏把 Trailer 合并进 Trailer_Big 的副本：这些事件只收录一次，身份固定为 `Data/Events/Trailer`，按房主是否有 `pamHouseUpgrade` 从 Trailer 或 Trailer_Big 启动；旧版重复条目上的照片和名称在打开画廊时并入。

2.7.0 的事件来源功能基于运行时操作调研，独立于上述历史 dev.4 已移除的实现；其中的运行时修改追踪已在 2.10.0 删除，说明见 `archive/EVENT_MOD_SOURCE_IMPLEMENTATION.md`。

已发布 2.8.0 将静态原始定义与运行时修改记录分开：来源扫描始终可用，返回标题或事件资产刷新清空索引，控制台 rescan 可刷新；不改写游戏资产，不使用完整脚本相同作为原始来源的必要条件。

2.10.0 按 `docs/REQUIREMENTS.md` 删除 AI 名单（`Exclusions/`、`GalleryCatalogVisibility`）、运行时修改追踪（`SmapiEventSourceObserver`、`EventSourceIndex` 等）和查询中的条件/季节/天气/时间/心数筛选（`QueryWeather`）；不要重新引入。阶段文档已移入 `docs/archive/`。

2.9.0 已删除停用的历史采集/冻结回放、历史 SQLite 仓库、执行轨迹观察和预览状态注入。`Replay/` 保留当前回放权限、确认与环境解析；`Conditions/CurrentStateSnapshot`、`RuntimeStateReader` 继续提供只读条件状态。`Persistence/` 仅维护 `SaveProfileKey`、`EventNameStore`，照片仍由 `Screenshots/EventPhotoStore` 持久化。玩家已有数据库与归档文档保持原样。当前检查聚焦有效功能，不再为已删除历史功能编译 SQLite 或预览注入夹具。
