# 来源查询与名单排除

基于 2.8.0 的原始事件定义匹配；2.9.0 将名单开关移入 GMCM，默认关闭。

## 查询

GalleryCatalog.Origins记录每个可见事件的EventOriginMatch。查询筛选使用独立键game / mod:UniqueID / unknown / ambiguous；同名模组通过ID区分，字符串查询同时匹配提供者名和ID。来源候选不会冒充已确认提供者。合成Data/TriggerActions前置没有直接来源证据时为未知。

## 排除边界

完整事件分类之后，GalleryCatalogVisibility按asset+ID过滤Events和ExcludedEvents，再重算仍有可见好感剧情的角色；不改变分类、游戏数据、已看记录、照片或名称。只有Identified且Provider.IsGameBase=false，且Provider.UniqueId完整命中名单ModId时隐藏。名字、NPC、Items、Locations、翻译包/修改者或任意candidate不是排除依据。原版即使ModId出现在名单也保留；未知、歧义及证据不足均保留。

PrerequisiteCatalog保留过滤前的story-ID去重集合：被隐藏剧情不能因MarkEventSeen再生为前置行。前置内部事件来源和依赖从过滤后的目录计算；不同asset的同ID名单外事件仍保留。无法溯源的独立TriggerActions规则不根据NPC或下游模组推断来源。

## 下载及开关

ModConfig.EnableAiModExclusion 默认 false，由标准 config.json 保存，并在 GMCM 中注册。AiModExclusionService 在 GameLaunched、每次 SaveLoaded 接收该值：关闭时使用空规则且不下载；开启时先载入 assets/ai-mod-exclusion.seed.json，再异步获取固定地址：

https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion.json?action=raw

默认12秒超时，响应/文件最多1MiB，严格验证JSON的ModId记录；完整ID忽略大小写，支持明确的*和?，拒绝全通配符或畸形数据。成功远程名单替换内置规则，不合并；失败保留发布内置名单，完全不读写历史下载缓存。种子也损坏时使用空规则并记录警告，避免猜测。旧 ai-mod-exclusion.json 在启动/读档时删除，不再读取、生成或迁移其中的 Enabled 值；删除受阻只记录警告，旧文件不参与决策。GMCM 保存回调先写标准配置，再捕获已保存的开关值；主线程 UpdateTicked 在无回放/确认时应用变更并刷新目录。未保存的编辑不改变服务状态，保存未变的设置不重启下载；关闭会取消旧请求并清空规则，重置后保存保持默认关闭。

后台只处理HTTP/不可变规则。主线程UpdateTicked应用完成结果并刷新整个GalleryViewContext；消失的相册/详情/前置目标回退到可见页面。回放或确认框存在时延后应用，以免干扰保存恢复。返回标题取消请求并更换会话编号，过期结果不能污染后续存档。

## 发布与验证

发布包必须包含内置名单及来源时间/哈希记录。标准 config.json、废弃的 ai-mod-exclusion.json 及 exclusion-cache 均不进入包；旧文件仍由打包规则拦截，避免残留误带入。

2.9.0 覆盖默认关闭、旧文件删除、关闭/重开及超限种子恢复；名单及生命周期 89 项通过，构建 DLL 的 GMCM 注册、保存、重置及安全延期 18 项通过，12 语言各 533 键。GMCM 的实际画面、键鼠/手柄操作仍待游戏内验收。

以下是 **2.8.0 发布时的历史验证**，不视为本次远程下载或实机检查：完整主项目构建0警告0错误，全量Checks和PersistenceChecks通过；新增名单与生命周期72项、目录过滤12项、来源查询11组检查。真实SMAPI 4.5.2加载器接受构建DLL，JSON回归通过。真实.NET6默认HttpClient成功下载52个去重ModId规则，与内置种子一致。12语言538键。

生产DLL真实目录审计直接调用解析器、规则与目录投影：1645条事件全部来源已识别，6599断言通过，隐藏824、保留821，176原版全部保留，名单外提供者无一被隐藏。来源查询沿用现有可滚动筛选器与手柄路径；最终真实游戏页面、实际输入和第三方组合仍待进入游戏验收。具体用法与公开验证范围见 RELEASE_2.8.0_NOTES.md；本机审计和安装记录留在未提交的任务资料与diagnostics中。
