# 模块维护入口

| 模块 | 维护入口 | 依赖与职责 |
| --- | --- | --- |
| 当前事件资料 | `GalleryCatalogCache`、`Catalog/GalleryCatalogBuilder`、`GalleryCatalog` | 从当前游戏读取并分类；供其他模块取资料，不创建菜单或回放。 |
| 条件与前置 | `GalleryConditionPresentation`、`ConditionStateReader`、`StoryDependencyLookup`、`PrerequisiteCatalog` | 当前TriggerActions标记和明确内部解锁依赖单独建资料；不生成可播放事件。条件与动作、已获得状态分离。 |
| 自助搜索 | `StorySearchIndex`、`QueryFilter` | 预计算名称，按精确角色/地点/来源与类别/进度/已知条件筛选；不改收藏或权限。 |
| 地点与来源 | `GalleryLocationNames`、`EventSourceCatalog` | 使用有明确地点绑定的现成译名；来源只用已核查身份资料，未知不猜测。 |
| 玩家命名 | `EventNameStore`、`GalleryRenameMenu` | 本机存档共用的用户别名，按资产+ID定位，独立原子保存，不写游戏进度。 |
| 页面与共用绘制 | `GalleryMenu`、`GalleryCharacterMenu`、`GalleryQueryMenu`、`GalleryEventDetailMenu`、`GalleryDrawing` | 按资料画界面，写`GalleryPageState`，通过`IGalleryNavigation`发出操作。 |
| 导航 | `GalleryApplication`、`GalleryPageHistory` | 唯一菜单构造入口，保存返回链和页面状态；不持有一串菜单闭包。 |
| 回放 | `ReplayService`、`ReplayAccessPolicy`、`ReplayCoordinator`、`ReplaySaveGuard`、`ReplaySnapshot` | 统一权限、确认、启动、运行保护、恢复；不认识相册/查询具体菜单。 |
| 截图封面 | `GalleryPhotos`、`EventPhotoStore` | 明确事件身份和存档身份，独立捕获、缓存、存储；照片页的返回交给导航。 |
| 根接线与配置 | `ModEntry`、`ModConfig` | 构建模块、订阅SMAPI、注册GMCM；普通回放开关是配置，一键解锁仍是当前存档状态。 |

相册与查询器共用资料、条件及回放结果；页面不调用其他页面获取资料。`GalleryApplication`统一创建菜单；`GalleryDrawing`不依赖某个具体菜单。纯模块通过对外方法验证，游戏/图形部分按需用隔离或实机证据。

修改分类、权限和副作用处理分别在对应模块完成，不能通过调整某页的按钮可见性代替权限判断，也不能用NPC归属代替剧情分类。

来源基础表`assets/event-sources.json`目前14条：本体已有2个剧情身份及8个原版标记，SVE 1.15.11与East Scarp NPCs 3.0.9各2条已核查身份。扩展包版本不匹配则未知。表中不保存或分发剧情脚本，只记录身份和定义包事实；不代表当前补丁链或原创版权归属。新增资料需核对准确资产、ID与定义包版本，不能只匹配ID前缀。本地证据`drafts/星露谷画廊/research/20260909-query-development/source-metadata/README.md`。
