# 模块维护入口

| 模块 | 维护入口 | 依赖与职责 |
| --- | --- | --- |
| 事件来源 | `Sources/ModDirectoryDiscovery`、`ContentPackEventScanner`、`AliveNpcEventScanner`、`EventDefinitionIndex`、`EventDefinitionSources`、`EventOriginPresentation`、`EventSourceDetails` | 自动扫描已加载 CP、AliveNpcs 场景与原版 XNB，按资产＋ID匹配原始来源、列出候选文件；`SmapiEventSourceObserver`/`EventSourceIndex`仅补充可选修改证据，GMCM开关只控制追踪，仍仅两个观察点。来源元数据也供查询与名单展示过滤使用，不改数据库或游戏事件。 |
| 当前事件资料 | `GalleryCatalogCache`、`Catalog/GalleryCatalogBuilder`、`GalleryCatalog` | 从当前游戏读取并分类；供其他模块取资料，不创建菜单或回放。 |
| 条件与前置 | `GalleryConditionPresentation`、`ConditionStateReader`、`StoryDependencyLookup`、`PrerequisiteCatalog` | 当前TriggerActions标记和明确内部解锁依赖单独建资料；不生成可播放事件。条件与动作、已获得状态分离。 |
| 自助搜索 | `StorySearchIndex`、`QueryFilter`、`QueryWeather` | 预计算角色/地点/原始来源，按来源Mod ID、类别、进度、已知条件筛选，并支持来源名称搜索。 |
| 来源排除 | `Exclusions/AiModExclusionRules`、`AiModExclusionService`、`Catalog/GalleryCatalogVisibility` | GMCM/config.json 的 EnableAiModExclusion 默认关闭；启用时启动与读档异步下载公开 ModId 名单，失败使用内置种子。按确认原始来源投影画廊目录，保留原版/未知/歧义，保护前置去重和进行中的回放。 |
| 查询名称 | `GalleryLocationNames`、`GalleryCharacterNames`、`GalleryNameText` | 优先现成译名，识别缺译诊断串；角色别名按剧情解析，地点旧名和已核实副本只在查询层分组。 |
| 玩家命名 | `EventNameStore`、`GalleryRenameMenu` | 本机存档共用的用户别名，按资产+ID定位，独立原子保存，不写游戏进度。 |
| 页面与共用绘制 | `GalleryMenu`、`GalleryCharacterMenu`、`GalleryQueryMenu`、`GalleryEventDetailMenu`、`GalleryDrawing` | 按资料画界面，写`GalleryPageState`，通过`IGalleryNavigation`发出操作。 |
| 导航 | `GalleryApplication`、`GalleryPageHistory` | 唯一菜单构造入口，保存返回链和页面状态；不持有一串菜单闭包。 |
| 回放 | `ReplayService`、`ReplayAccessPolicy`、`ReplayCoordinator`、`ReplaySaveGuard`、`ReplaySnapshot` | 统一权限、确认、启动、运行保护、恢复；不认识相册/查询具体菜单。 |
| 截图封面 | `GalleryPhotos`、`EventPhotoStore` | 明确事件身份和存档身份，独立捕获、缓存、存储；照片页的返回交给导航。 |
| 根接线与配置 | `ModEntry`、`ModConfig` | 构建模块、订阅SMAPI、注册GMCM；普通回放开关是配置，一键解锁仍是当前存档状态。 |

相册与查询器共用资料、条件及回放结果；页面不调用其他页面获取资料。`GalleryApplication`统一创建菜单；`GalleryDrawing`不依赖某个具体菜单。纯模块通过对外方法验证，游戏/图形部分按需用隔离或实机证据。

修改分类、权限和副作用处理分别在对应模块完成，不能通过调整某页的按钮可见性代替权限判断，也不能用NPC归属代替剧情分类。

dev.4已按用户要求移除定义来源功能及数据表。地点分组保留所有原始资产/ID身份；Kenneth OFF/ON按地理位置归山脊、AlissaDate归山脊崖边，不能据此替换实际场景。图片场景和公主屯不按入口房屋强行归并。临时演员只凭明确命令/已核查别名归一，无法确认为人物的动画标识不占角色选项。本地名称证据见`drafts/星露谷画廊/research/20260909-query-corrections/names-and-locations.md`。

dev.5在查询索引中进一步聚合相同地点显示名，保留独立事件身份；注册的隐藏不可社交圆点分身在Key查找前先归到本体。`LocationNameFallbacks`只提供已核对地点的可读描述，使用原地点/角色名称和本地化后缀组合，不参与执行。当前全量盘点与回退依据见`drafts/星露谷画廊/research/20260909-query-name-followup/REPORT.md`。

dev.6的`NativeGlobalEventCopies`只识别游戏TryGetLocationEvents注入的两段爷爷剧情，匹配原FarmHouse完整键与脚本后省略异地副本，避免每个地图虚增同一剧情。不得将该规则泛化到不同键/脚本或其它ID；输入原字典保持只读。原版地名、Cellar显示组及SVE替代人物归并继续位于名称层。

2.7.0 的事件来源功能基于运行时操作调研，独立于上述历史 dev.4 已移除的实现；当前已有详情页展示与GMCM开关，并保留开发者诊断，不恢复旧数据表或旧来源推断。范围、启用方式及验收限制见 `EVENT_MOD_SOURCE_IMPLEMENTATION.md`。

已发布 2.8.0 将静态原始定义与运行时修改记录分开：来源扫描始终可用，返回标题或事件资产刷新清空索引，控制台 rescan 可刷新；不改写游戏资产，不使用完整脚本相同作为原始来源的必要条件。

2.9.0 已删除停用的历史采集/冻结回放、历史 SQLite 仓库、执行轨迹观察和预览状态注入。`Replay/` 保留当前回放权限、确认与环境解析；`Conditions/CurrentStateSnapshot`、`RuntimeStateReader` 继续提供只读条件状态。`Persistence/` 仅维护 `SaveProfileKey`、`EventNameStore`，照片仍由 `Screenshots/EventPhotoStore` 持久化。玩家已有数据库与归档文档保持原样。当前检查聚焦有效功能，不再为已删除历史功能编译 SQLite 或预览注入夹具。
