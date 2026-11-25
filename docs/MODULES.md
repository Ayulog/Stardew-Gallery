# 模块维护入口

| 模块 | 维护入口 | 依赖与职责 |
| --- | --- | --- |
| 事件来源 | `Sources/ModDirectoryDiscovery`、`PackFiles`、`ContentPackEventScanner`、`TriggerActionDeclarations`、`AliveNpcEventScanner`、`EventDefinitionIndex`、`EventDefinitionSources`、`EventOriginPresentation`、`EventSourceDetails` | 只读扫描已加载 CP、AliveNpcs 场景与原版 XNB（含 `Data/Events/*`、`Data/TriggerActions` 规则声明及 `fork` / `switchEvent` 分支子键），按资产＋ID匹配原始来源，列出候选文件、分支子脚本提供者和其他声明者。`PackFiles` 按 SMAPI 方式解析包内路径（非 Windows 不区分大小写；包目录可以是链接，包内链接不跟随）并记录文件指纹，供只重扫变化的包。扫描不完整的包使覆盖包的匹配降级为“证据不完整”。来源只供显示与查询，不影响收录、解锁和回放。 |
| 当前事件资料与目录分析 | `GalleryCatalogCache`、`Catalog/EventAssetCatalog`、`NativeGlobalEventCopies`、`ResolvedEventIndex`、`GalleryCatalogBuilder`、`ParallelBranchAnalyzer`、`StoryQuoteExtractor`、`EventMusicExtractor`、`GalleryCatalog` | 从当前游戏读取并分类；识别单周目互斥平行分支（If 线）并按节点统计收集进度（`CharacterStoryProgress`，配偶婚礼留念卡不计入好感分母）；静态提取防剧透主角首句代表台词与过场 BGM；并为自然游玩自动留影提供单资产轻量身份核查（`ResolveCapturableStoryIdentity`）。 |
| 条件、1.6 GSQ 与偶遇线索 | `GalleryConditionPresentation`、`ConditionParser`、`GameQuerySplit`、`SafeGameQuery`、`SafeQueryStateReader`、`ConditionEvaluator`、`RuntimeStateReader`、`ConditionStateReader`、`EncounterClue`、`StoryDependencyLookup`、`PrerequisiteCatalog` | 覆盖 41 种传统前置（含 `a X Y` 入口格坐标展示）与原版 1.6.15 纯只读确定性内置 GSQ（含带引号参数、递归 `ANY` / `!ANY` 复合子句及社区中心/姜岛/博物馆/矿井/农舍/子女/特别任务/讨伐怪物等，由 `SourcesRuntimeChecks --conditions` 与游戏代码对拍；绝不执行外部自定义委托）。按长期条件与当日季节/星期/天气划分 `AvailableToday` / `ReadyForTiming` 双级偶遇线索（含未知项时不标为可偶遇）。当前 `Data/TriggerActions` 标记和明确内部解锁依赖单独建资料并关联静态规则来源。 |
| 自助搜索 | `StorySearchIndex`、`QueryFilter`、`GallerySearchFilter`、`PinyinTable` | 预计算角色/地点/原始来源/可读卡片标题（`DisplayTitle`）与偶遇线索，按类别、角色、地点、来源 Mod ID、进度（书页内嵌手账下拉列表浮层，长列表自带拼音/关键字搜索）和“可偶遇线索（`EncounterOnly`）”筛选；保留按原始 `EventId` 精确/模糊检索。中文界面下 `PinyinTable` 让拉丁字母按拼音匹配名称、可读标题、地点和角色；读音表 `assets/pinyin.txt` 由 `tools/Generate-PinyinTable.ps1` 从 Unihan 生成，不手工编辑。 |
| 查询名称与配乐译名 | `GalleryLocationNames`、`GalleryCharacterNames`、`GalleryMusicNames`、`GalleryNameText`、`SafeTranslationLoader` | 优先现成译名，识别缺译诊断串；角色别名按剧情解析，地点旧名和已核实副本只在查询层分组。 |
| 玩家命名与收藏 | `EventNameStore`、`FavoriteEventStore`、`IStorageFileSystem`、`GalleryRenameMenu` | 本机存档共用的自定义事件名（`event-names.json`）与喜爱收藏标记（`favorite-events.json`），按资产+去空格 ID 定位；落盘后原子替换，损坏时回退到最近的备份并保留只读保护，备份最多 5 份；`GalleryRenameMenu` 为叠在暗化皮质书本中央的 `720×340` 手账铭牌改名小浮窗。所有分屏共用实例，不写游戏进度。 |
| 书本页面与共用绘制 | `GalleryMenu`、`GalleryCharacterMenu`、`GalleryQueryMenu`、`UI/GalleryMomentsMenu`、`GalleryEventDetailMenu`、`UI/GalleryPrerequisiteMenu`、`Screenshots/GalleryPhotoMenu`、`UI/GalleryFilterMenu`、`UI/GalleryBookChrome`、`UI/GalleryUiSkin`、`GalleryCharacterPanel`、`Scenic/ScenicWindowRules`、`Scenic/ScenicBackdropRenderer`、`Scenic/ScenicWeatherParticles`、`ScenicWindowStyle`、`GalleryCardTitleFormatter`、`GalleryPagination`、`GallerySpreadLayout`、`UI/SocialPageBridge` | 全页面统一承载于 `1672×941` 皮质手账大书：左下悬挂铭牌＋长短双态三色丝带书签切换“村民相册 / 故事索引 / 珍藏时刻”（切换至后两章栈底保留首页），主书页采用双态木刻翻页器左右整页翻书（仅详情页与前置页保留局部滚动条）；标题通过组合矩阵以 `x=0, width=999999` 调用 `SpriteText` 彻底杜绝高缩放折行；角色左窗脱离外层书本小数缩放矩阵，按严格屏幕整数倍率（标准视口 `4x` = `Game1.pixelZoom`）绘制 12 幅 `96×68` 四季昼夜原生像素地平线底图、原版 `AnimatedSprite` 漫步与 `Data/AnimationDescriptions` 安全专属休闲动作，并复用原版 `WeatherDebris` 与 `Game1.rainTexture` `4x` 天气粒子（移除右上角天气切换按钮）；未真实观看且无封面的故事统一采用红/金火漆信封卡面并区分“未观看”与“未解锁”；`SocialPageBridge` 只读识别原版“社交”页悬停/聚焦村民，并支持绘制偶遇像素角标与点击角标直达相册（零原版控件注入）。 |
| 外观适配 | `Appearance/CharacterAppearance`、`Appearance/AppearanceGeometry`、`PortraiturePortraits`、`DialoguePortraits`、`HdPortraits`、`HdPortraitSelection`、`ScaleUpSprites` | 按需读取并缓存当前生效肖像与左窗原版 `4x` 角色漫步及 `Data/AnimationDescriptions` 安全休闲动作（越界帧或非标准 `4×4` 网格安全回退第 0 帧静止），兼容 CP、Portraiture、DDFC、HD Portraits（`tlitookilakin.HDPortraits`）与 Scale Up；缺失或读取异常时限频记录日志并回退默认资源。 |
| 文字输入 | `GalleryTextBox`、`SteamTextInput`、`GalleryTyping`、`GalleryTypingKeys` | 手柄选中输入框时先请 Steam 显示键盘（Steam Deck、大屏幕模式，可输入中文），Steam 显示不了再用游戏键盘。提交的文字替换输入框内容，等手柄按键松开再按回车，免得同一按键落到页面上。`Steamworks.NET` 由游戏提供，只在 `SteamKeyboard` 里引用；分屏时不用 Steam 键盘。搜索框的回车只列出候选（`FinishSearch`）。实体键盘通过游戏的 `InputState.IgnoreKeys` 隔离快捷键，不补丁 SMAPI。 |
| 导航 | `GalleryApplication`、`GalleryPageHistory` | 唯一菜单构造入口，管理三书签切换（`SwitchTab`，`StoryIndex`/`Moments` 栈底保留 `Home`）、返回链与页面状态，以及从原版 `SocialPage` 直达后的单步返回回调（`OpenFromSocialPage`）。 |
| 回放与剧场控制 | `ReplayService`、`ReplayAccessPolicy`、`ReplaySceneEnvironment`、`ReplayBranchControl`、`PortraitComparisonController`、`PortraitComparisonLogic`、`TheaterHudState`、`ReplayCoordinator`、`ReplaySaveGuard`、`ReplaySnapshot` | 统一权限（含已开放平行 If 线，禁回放婚礼卡与内部流程）、正向传统前置与 GSQ 环境提取、本事件已答 `$q` 临时遮蔽与同事件 `fork` 路线分发、无操作自动淡出 HUD、暂停隐框定格观景、原版/美化立绘实时对比（单按切换、长按 >220ms 保持左右分屏）、`2x/4x` 可选自动继续普通对话、运行保护与快照恢复。 |
| 剧照、自动留影与婚礼纪念 | `GalleryPhotos`、`EventPhotoStore`、`AutoMementoTracker`、`AutoMementoRules`、`WeddingMemento`、`PhotoFolderOpener`、`PhotoFolderRules`、`GalleryPhotoMenu`、`PhotoCapturePatches` | 按存档身份独立存储剧照与配偶婚礼留念卡（`Wedding/<SpouseName>`）；`GalleryPhotoMenu` 为 `1672×941` 对称皮质书本双页界面；支持自然游玩与无封面首播时的静默自动留影，以及桌面端打开本地剧照子目录（掌机游戏模式 / Steam 大屏幕模式自动回退路径提示，桌面会话如 KDE/GNOME 正常唤起文件管理器）。 |
| 根接线与配置 | `ModEntry`、`ModConfig` | 构建模块、订阅 SMAPI、注册 GMCM（含静默自动留念、每日清晨偶遇轻提示 `ShowMorningEncounterHint`、原版社交页偶遇角标 `ShowSocialPageClueBadges`、暂停观景按键等）；普通回放开关是配置，一键解锁是当前存档状态（联机农场工只在本次会话）。分屏时每位玩家各有 `GalleryApplication`、`GalleryPhotos`、`AutoMementoTracker`、`TheaterHudState`、`SocialClueCache` 和解锁状态（`PerScreen`）。 |

相册、故事索引与珍藏时刻共用资料、条件及回放结果；页面不调用其他页面获取资料。`GalleryApplication` 统一创建菜单；`GalleryDrawing` 与 `GalleryBookChrome` 不依赖某个具体菜单。纯模块通过对外方法验证，游戏/图形部分按需用隔离或实机证据。

修改分类、权限和副作用处理分别在对应模块完成，不能通过调整某页的按钮可见性代替权限判断，也不能用 NPC 归属代替剧情分类。

dev.4 已按用户要求移除定义来源功能及数据表。地点分组保留所有原始资产/ID 身份；Kenneth OFF/ON 按地理位置归山脊、AlissaDate 归山脊崖边，不能据此替换实际场景。图片场景和公主屯不按入口房屋强行归并。临时演员只凭明确命令/已核查别名归一，无法确认为人物的动画标识不占角色选项。本地名称证据见 `drafts/星露谷画廊/research/20260909-query-corrections/names-and-locations.md`。

dev.5 在查询索引中进一步聚合相同地点显示名，保留独立事件身份；注册的隐藏不可社交圆点分身在 Key 查找前先归到本体。`LocationNameFallbacks` 只提供已核对地点的可读描述，使用原地点/角色名称和本地化后缀组合，不参与执行。当前全量盘点与回退依据见 `drafts/星露谷画廊/research/20260909-query-name-followup/REPORT.md`。

dev.6 的 `NativeGlobalEventCopies` 只识别游戏 `TryGetLocationEvents` 注入的两段爷爷剧情，匹配原 `FarmHouse` 完整键与脚本后省略异地副本，避免每个地图虚增同一剧情。不得将该规则泛化到不同键/脚本或其它 ID；输入原字典保持只读。原版地名、`Cellar` 显示组及 SVE 替代人物归并继续位于名称层。2.10.0 起它也处理游戏把 `Trailer` 合并进 `Trailer_Big` 的副本：这些事件只收录一次，身份固定为 `Data/Events/Trailer`，按房主是否有 `pamHouseUpgrade` 从 `Trailer` 或 `Trailer_Big` 启动；旧版重复条目上的照片、名称和收藏标记在打开画廊时自动并入。

2.7.0 的事件来源功能基于运行时操作调研，独立于上述历史 dev.4 已移除的实现；其中的运行时修改追踪已在 2.10.0 删除，说明见 `archive/EVENT_MOD_SOURCE_IMPLEMENTATION.md`。

已发布 2.8.0 将静态原始定义与运行时修改记录分开：来源扫描始终可用，返回标题或事件资产刷新清空索引，控制台 `rescan` 可刷新；不改写游戏资产，不使用完整脚本相同作为原始来源的必要条件。

2.10.0 按 `docs/REQUIREMENTS.md` 删除 AI 名单（`Exclusions/`、`GalleryCatalogVisibility`）、运行时修改追踪（`SmapiEventSourceObserver`、`EventSourceIndex` 等）和查询中的多维条件交叉筛选框（`QueryWeather`）；不要重新引入。阶段文档已移入 `docs/archive/`。

2.9.0 已删除停用的历史采集/冻结回放、历史 SQLite 仓库、执行轨迹观察和预览状态注入。`Replay/` 保留当前回放权限、确认、分支沙盒与环境解析；`Conditions/CurrentStateSnapshot`、`RuntimeStateReader` 继续提供只读条件状态。`Persistence/` 维护 `SaveProfileKey`、`EventNameStore`、`FavoriteEventStore` 与 `IStorageFileSystem`，照片与婚礼留念卡由 `Screenshots/EventPhotoStore` 持久化。玩家已有数据库与归档文档保持原样。当前检查聚焦有效功能，不再为已删除历史功能编译 SQLite 或预览注入夹具。
