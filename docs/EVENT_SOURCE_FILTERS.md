# 2.8.0 来源查询与名单排除

2026-09-21。基于原始事件定义匹配，随2.8.0发布。

## 查询

GalleryCatalog.Origins记录每个可见事件的EventOriginMatch。查询筛选使用独立键game / mod:UniqueID / unknown / ambiguous；同名模组通过ID区分，字符串查询同时匹配提供者名和ID。来源候选不会冒充已确认提供者。合成Data/TriggerActions前置没有直接来源证据时为未知。

## 排除边界

完整事件分类之后，GalleryCatalogVisibility按asset+ID过滤Events和ExcludedEvents，再重算仍有可见好感剧情的角色；不改变分类、游戏数据、已看记录、照片或名称。只有Identified且Provider.IsGameBase=false，且Provider.UniqueId完整命中名单ModId时隐藏。名字、NPC、Items、Locations、翻译包/修改者或任意candidate不是排除依据。原版即使ModId出现在名单也保留；未知、歧义及证据不足均保留。

PrerequisiteCatalog保留过滤前的story-ID去重集合：被隐藏剧情不能因MarkEventSeen再生为前置行。前置内部事件来源和依赖从过滤后的目录计算；不同asset的同ID名单外事件仍保留。无法溯源的独立TriggerActions规则不根据NPC或下游模组推断来源。

## 下载及开关

AiModExclusionService在GameLaunched、每次SaveLoaded读取独立ai-mod-exclusion.json（缺失首次创建Enabled=true），先载入assets/ai-mod-exclusion.seed.json，然后异步获取固定地址：

https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion.json?action=raw

默认12秒超时，响应/文件最多1MiB，严格验证JSON的ModId记录；完整ID忽略大小写，支持明确的*和?，拒绝全通配符或畸形数据。成功远程名单替换内置规则，不合并；失败保留发布内置名单，完全不读写历史下载缓存。种子也损坏时使用空规则并记录警告，避免猜测。独立设置错误时默认启用并保留原文件，Enabled=false时不排除且不下载。设置变更在下次读档/启动生效，不加入GMCM或ModConfig。

后台只处理HTTP/不可变规则。主线程UpdateTicked应用完成结果并刷新整个GalleryViewContext；消失的相册/详情/前置目标回退到可见页面。回放或确认框存在时延后应用，以免干扰保存恢复。返回标题取消请求并更换会话编号，过期结果不能污染后续存档。

## 发布与验证

发布包必须包含内置名单及来源时间/哈希记录。运行时开关及exclusion-cache不进入包，更新时保留玩家开关。

本轮：完整主项目构建0警告0错误，全量Checks和PersistenceChecks通过；新增名单与生命周期72项、目录过滤12项、来源查询11组检查。真实SMAPI 4.5.2加载器接受构建DLL，JSON回归通过。真实.NET6默认HttpClient成功下载52个去重ModId规则，与内置种子一致。12语言538键。

生产DLL真实目录审计直接调用解析器、规则与目录投影：1645条事件全部来源已识别，6599断言通过，隐藏824、保留821，176原版全部保留，名单外提供者无一被隐藏。来源查询沿用现有可滚动筛选器与手柄路径；最终真实游戏页面、实际输入和第三方组合仍待进入游戏验收。具体用法与公开验证范围见 RELEASE_2.8.0_NOTES.md；本机审计和安装记录留在未提交的任务资料与diagnostics中。
