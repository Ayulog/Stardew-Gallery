# 当前任务

目标：按 `docs/REQUIREMENTS.md`（v2 原生画廊体验升级定稿）完成阶段 1–4 全部代码实现、12 语言本地化、文档同步与无损内存/运行时自动验证。

## 四阶段交付完成情况

### 阶段 1：1.6 只读 GSQ 扩充、正向 GSQ 回放环境提取与静态来源闭环
- [x] 对照 1.6.15 `Stardew Valley.dll` 扩充原版纯只读确定性内置 `GameStateQuery`（GSQ）解析、求值、好感/引用提取与可读描述（支持合法带引号参数拆词与转义，保留整串加引号 `G "..."` 判未知的原版语义；绝不调用外部委托）。
- [x] `Replay/ReplaySceneEnvironment.cs` 支持从正向无冲突的 `NativeQueryCondition`（`SEASON`、`LOCATION_SEASON`、`SEASON_DAY`、`TIME`、`WEATHER`）提取回放演出环境。
- [x] `Sources/`、`PrerequisiteCatalog`、`StorySearchIndex` 与详情/前置页补齐 `Data/TriggerActions` 规则静态提供者与 `fork` / `switchEvent` 分支子脚本来源声明（遵守不完整证据降级，不误把覆盖包当原作者）。

### 阶段 2：平行分支（If 线）、双级可偶遇线索、防剧透台词/BGM 与剧场控制
- [x] `ParallelBranchAnalyzer` 与 `CharacterStoryProgress`：识别同角色前序互斥平行事件（If 线），真实经历任一分支后自动开放同组平行分支安全鉴赏（不写真实 `eventsSeen`），并在收集进度中将互斥组计为同一故事节点，单周目不卡满收集金星。
- [x] `EncounterClueClassifier`：实现 `AvailableToday`（长期条件及当日季节、星期与地点天气均吻合）与 `ReadyForTiming`（长期条件已满足，等待季节/星期/天气）双级可偶遇线索；含未知项绝不标为可偶遇。
- [x] `StoryQuoteExtractor`、`EventMusicExtractor` 与 `SafeTranslationLoader`：防剧透提取主角首句对白签注（未看且未解锁严格隐藏）与可读过场 BGM 曲名；拦截非法翻译资产路径防崩溃。
- [x] `ReplayBranchControl`、`TheaterHudState` 与 `ReplayCoordinator`：回放期间临时内存遮蔽本事件内联 `$q` 已答 ID 以重现选项、支持详情页切换同事件 `fork` 路线、无操作平滑淡出 HUD，以及一键暂停并隐藏对话框与跳过按钮定格观景。

### 阶段 3：静默自动留影、婚礼留念卡、收藏夹、本地目录打开与 HD Portraits 兼容
- [x] `AutoMementoTracker` / `AutoMementoRules` + GMCM `AutoCaptureMemories`：正常游玩（单人/联机/分屏本地端）自然触发故事或村民婚礼，以及单人回放无封面故事时，待画面稳定后在世界渲染层静默抓拍首张无 UI 初始封面（冷启动单资产轻量核查，零包扫描刷屏）。
- [x] `WeddingMemento` 与 `EventPhotoStore`：新增配偶婚礼留念卡（`Wedding/<SpouseName>`，记录结婚年月日，仅留影查看不提供状态机回放，不计入好感分母）。
- [x] `FavoriteEventStore`（`user-data/favorite-events.json`）与 `PhotoFolderOpener` / `PhotoFolderRules`：跨存档喜爱剧情原子持久化与备份恢复；剧照页支持桌面端打开本地文件夹（Steam Deck 游戏模式与 Big Picture 自动回退路径提示，Linux KDE/GNOME 等桌面会话正常唤起文件管理器）。
- [x] `HdPortraits` / `HdPortraitSelection` + `manifest.json`：新增 `tlitookilakin.HDPortraits` 可选只读兼容（含元数据尺寸与帧动画裁剪、独立限频日志归因与超界回退）。

### 阶段 4 与 UI 美术包 v2 实装定稿：皮质手账双态皮肤、左下三书签、六景窗与五大书页版式
- [x] **逐像素 Alpha 净化切图与原件备份**：先将覆盖前的 3 张主底图完整备份至回收归档目录并附 `MANIFEST.txt` SHA-256 清单；对 `01_Book_Backgrounds` 与 `04_Component_Sheets` 执行确定性 Alpha 净化与抗杂边处理（外背景与透明挖空窗清零为 `RGBA(0,0,0,0)`，实心主体锁定 `Alpha=255`），产出 3 张 `1672×941` 主底图、6 张 `760×540` 季节昼夜景窗、2 张 `530×298` 红/金火漆信封与 16 张 `Ui*.png` 双态组件贴图，并通过 `tools/Optimize-Assets.ps1` 无损压缩与校验。
- [x] **五大主页面与剧场 HUD 实装（`UI/GalleryUiSkin`、`GallerySpreadLayout`、`GalleryBookChrome`、`GalleryMenu`、`GalleryCharacterMenu`、`GalleryQueryMenu`、`UI/GalleryMomentsMenu`、`GalleryEventDetailMenu`、`ModEntry`）**：
  - `GalleryMenu`：对称双页（左 3×3 + 右 3×3 = 18 位村民，避开 `X≈836` 中缝），凹陷双态搜索框、四角金 L 聚焦框、`(!)` / 绿叶偶遇章、满收集像素金星、未结识深棕剪影、左下长短双态三书签（绯红长垂）、右下双态木刻翻页器与木纹解锁按钮。
  - `GalleryCharacterMenu` & `GalleryCharacterPanel`：6 张季节昼夜景窗（`<2000` 春/夏/秋暮/冬昼，`>=2000` 冬夜/晴夜）100% `Color.White` 原色绘制（移除旧全屏染色遮罩、冬季白方块与假星星，仅保留克制雨/绿雨/雪微粒），立绘底衬外扩覆盖左窗边缘像素且保持 `PortraitBounds (240,120,380,270)` 100% 兼容 `AppearanceChecks`；左页第 1 行显示 `姓名 · 关系状态`，第 2 行使用新像素实心/空心红心；右页 2×3 卡片采用菱形铆钉标题小铭牌 + 四角红皮金铆钉相框 + 左上状态胶囊签 + 右上红丝带爱心角标，单击整卡统一进入详情页（婚礼卡进剧照页）。
  - `GalleryQueryMenu`：左页双态搜索框 + 5 行双态下拉筛选条 + 1 行高亮「仅显示可偶遇线索」双态勾选栏 + 左侧结果统计与右侧双态 `[重置筛选]` 木按钮；右页每页 5 张宽松双态故事手账卡（左侧 `16:9` 剧照/火漆信封、中部可读标题+副标题、右侧状态胶囊签）+ 右下居中木刻翻页器。
  - `GalleryMomentsMenu`：左 2×2 + 右 2×2 共 8 张拍立得纪念卡（复用菱形标题小铭牌 + 红皮金铆钉相框 + 婚礼金戒胶囊签 / 红丝带爱心角标）+ 左下琥珀金长垂书签 + 右下木刻翻页器与 `[← 返回]` 木按钮。
  - `GalleryEventDetailMenu`（方案 C）：右上紧凑排布 3 行元数据并在 `Y=248..286` 放置双态木按钮 `[♥ 收藏剧情 / 已收藏]` 与 `[自定义事件名]`，右上透明窗背绘剧照/火漆信封并叠像素放映机相机角标；中部双态分支路线条与条件/来源滚动区；右下宽幅双态木按钮 `[← 返回]` 与 `[▶ 回放]`（不可回放呈暗木禁用态并悬停提示原因）。
  - `ModEntry.OnRenderedHud`：接入木纹按钮与复古放映机相机、眼睛暂停、双箭头快进、八分音符 4 枚像素图标。
- [x] `SocialPageBridge`、`GalleryApplication.OpenFromSocialPage`：在原版 `GameMenu` 的 `SocialPage` 按画廊键（默认 `G`）时只读识别鼠标悬停或手柄聚焦的村民（零 UI 控件注入），直达其个人相册，按 `B` / 返回无缝恢复原 `SocialPage`、滚动槽位与手柄焦点。
- [x] **实机体验闭环 14 项改进（玩家实测 9 项 + 自查 5 项）**：
  - 左页景窗村民自然草地漫步（确定性 14.4s 漫步循环：居中正立 → 向右 4 帧散步 → 右驻足 → 向左 4 帧散步 → 左驻足 → 向右归位，按姓名哈希错峰；非标准 `4×4` 贴图自动回退第 `0` 帧站立；保持 `SpritePlacement` 签名与 `PortraitBounds` 裁剪不变）。
  - 卷轴标题 `SpriteText` 独立平移缩放矩阵防视口折行（高 UI 缩放与 `1280×720` 下绝不强制单字折行）、右上角 `X` 关闭按钮让开黄铜书角（`1662, 12`）、移除中文角色相册标题额外字距循环、全量清理星露谷字体缺字特殊符号（`←/→/▶/◀/♪/✓/✗` 及 `zh.json` 的 `「」`；回放按钮改用 `UiHudIcon.FastForward` 像素图标，HUD BGM 改用 `UiHudIcon.Music`）、木纹按钮/铭牌/胶囊签字号放宽与详情页收藏/改名按钮加宽。
  - 故事索引筛选页签内联羊皮纸下拉浮层（胡桃木黄铜镶边、首项固定 `不限`、支持滚轮/滚动条拖拽/手柄肩键翻页，选项 `> 8` 时内嵌中英文及拼音搜索框，彻底告别旧黄框整页跳转）。
  - 书签返回栈（从首页切故事索引/珍藏时刻保留 `[Home, Query]` / `[Home, Moments]` 栈底，按 `Esc/B` 先回首页再退出）、珍藏时刻底部木刻翻页器居中、所有未观看剧情在未抓拍封面时一律显示火漆信封（一键解锁下显示 `pill.unseen`「未观看」而非空白占位图）。
  - `GalleryPrerequisiteMenu`、`GalleryPhotoMenu`、`GalleryRenameMenu` 全量皮质手账化重做；详情页与前置页跳转行改为单行镶边可点击横条，普通事件详情页左页补满全部 6 行铭牌。
  - 画廊外可偶遇联动（GMCM 默认开启 `ShowMorningEncounterHint` 清晨无剧透 HUD 提示与 `ShowSocialPageClueBadges` 原版社交页偶遇徽章点击直达相册，零逐帧重算、零控件注入）。
  - `TileCondition` 明确展示触发格坐标 `(X, Y)`；对照 1.6.15 `Stardew Valley.dll` 新增 `ANY` / `!ANY` 递归逻辑或（深度上限 4，Kleene 三值逻辑）与 18 项原版确定性只读 GSQ（`IS_COMMUNITY_CENTER_COMPLETE`、`IS_JOJA_MART_COMPLETE`、`IS_ISLAND_NORTH_BRIDGE_FIXED`、`FOUND_ALL_LOST_BOOKS`、`IS_MULTIPLAYER`、`IS_HOST`、`IS_VISITING_ISLAND`、`MUSEUM_DONATIONS`、`MINE_LOWEST_LEVEL_REACHED`、`PLAYER_FARMHOUSE_UPGRADE`、`PLAYER_HAS_MET`、`PLAYER_HAS_PET`、`PLAYER_HAS_CHILDREN`、`PLAYER_HAS_TOWN_KEY`、`PLAYER_SPECIAL_ORDER_COMPLETE`、`PLAYER_SPECIAL_ORDER_ACTIVE`、`PLAYER_HAS_HEARD_SONG`、`PLAYER_KILLED_MONSTERS`）。
- [x] 全部 12 种官方语言 `i18n/*.json`（各 **623** 个键，占位符 100% 对齐）、`docs/REQUIREMENTS.md`、`README.md`（中英双语）、`docs/MODULES.md` 与 `StardewGallery.csproj`（排除 `release/**` 原稿入包，保持 `2.10.0` 版本号与稳定 `UniqueID`）同步更新。

## 编译与自动检查结果

- **主项目与检查项目编译**（`.NET SDK 8.0.427` / `net6.0`，`EnableModDeploy=false`）：
  - `StardewGallery.csproj` Release 构建：**0 警告、0 错误**（构建包 `StardewGallery 2.10.0.zip`，确认未混入 `release/` 设计原稿）。
  - `Checks`、`SourcesRuntimeChecks` Release 构建：**0 错误**（仅 SDK 对 `net6.0` 的 EOL 提示）。
- **纯内存独立检查（零创建/零删除磁盘临时目录）**：
  - `Checks --appearance`：**77 / 77** 外观与布局检查通过，且 `[ModuleInitializer]` `GallerySpreadChecks` 通过（含左下双态三书签、五大书页区域不相交、详情页方案 C 加宽按钮区域及 **32 张** PNG 签名与尺寸头校验）。
  - `Checks --stage4`：汇总阶段 1–4 全部纯内存套件与 12 语言 **623** 键全量审计（含 `SourceStage1Checks` 64 项、`Stage2Checks` 98 项、`Stage3Checks` 217 项、`Stage4Checks` **571 项**及全部既有目录/条件/拼音/搜索检查），**100% 通过**。
- **SMAPI 4.5.2 + Stardew Valley 1.6.15 运行时检查（`InMemoryStorageFileSystem`，零磁盘临时目录删除）**：
  - `--assembly`：SMAPI 真实 `AssemblyLoader` 重写与加载通过。
  - `--conditions`：**251 / 251** 原版 1.6.15 条件、`ANY` / `!ANY` 递归组合与全部内置确定性只读 GSQ 对拍通过。
  - `--replay`：**37 + 3** 快照与生命周期检查通过。
  - `--replay-effects`：**15 + 5** 副作用拦截与 100% 缩放截图缓冲检查通过。
  - `--replay-weather`：**42** 天气与局部季节环境恢复检查通过。
  - `--replay-speed`：**36** 倍速与选项/淡入暂停检查通过。
  - `--controller-input`：**68 / 68** 双吸附模式手柄交互检查通过。
  - `--keyboard-input`：**7 / 7** 实体键盘 `IgnoreKeys` 隔离与粘贴检查通过。
  - `--gallery-drafts`：**12 / 12** 筛选与改名草稿状态检查通过。
  - `--save-data`：**6 / 6** 主机与联机农场工解锁存储检查通过。
  - `--stage2`：**32 / 32** `$q` 遮蔽重现、`fork` 路线分发与定格观景暂停检查通过。
  - `--stage3`：**26 / 26** 原生婚礼事件识别、静默自动留影、冷启动单资产核查、`HD Portraits` 运行时适配与详情页方案 C 收藏/改名按钮检查通过。
  - `--stage4`：**104 / 104** 草地自然漫步与非标贴图回退、书签 `[Home, Query/Moments]` 返回栈、故事索引书页内羊皮纸下拉浮层（搜索过滤/手柄选择/Esc 关闭）、皮质手账化前置页/剧照页/改名浮窗、社交页偶遇徽章缓存与点击直达、清晨偶遇 HUD 提示及既有双吸附模式导航检查通过。

## 待实机验收与交接说明（下一棒必读）

### 1. 当前工作区与安装状态（截至 2026-11-17）
- **Git 状态**：仓库当前共有约 `140` 项未提交改动（含阶段 1–4 代码、新 UI 美术包 `assets/*.png` 切片、12 语言 `i18n/*.json` 623 键及文档更新），**尚未执行 `git commit`**，版本号仍保持 `2.10.0`（待实机验收定版后再统一升版与提交）。
- **已部署实测包**：最新 Release 产物已覆盖安装至本机 `<GamePath>/Mods/StardewGallery`（`StardewGallery.dll` SHA-256：`78517AC3B4AB8A03689839BAB8F33A9549EF89027F768D5E79C140CD4C1ACCC1`），玩家数据（`config.json`、`event-photos/`、`user-data/`）完整保留。
- **美术原稿与备份位置**：
  - 设计师交付的原始未切图美术包位于 `release/StardewGallery-UI-Art-Pack-20261117/`（已在 `StardewGallery.csproj` 中排除入包，`release/` 不纳入 Git 提交）。
  - 被替换的 3 张旧版书本主底图原件已按工作区规范归档备份至仓库外回收目录（附 SHA-256 `MANIFEST.txt`）。

### 2. 接手开发与验证注意事项（避坑指南）
- **严禁无参运行整套 `Checks`**：旧套件中的 `EventDefinitionChecks` / `ModDirectoryDiscoveryChecks` 会删除临时测试目录，触发工作区零删除红线。日常验证请使用定向无损参数：
  - `dotnet build StardewGallery.csproj -c Release -p:GamePath="<GamePath>"`
  - `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --appearance`（自动运行 `[ModuleInitializer]` `GallerySpreadChecks` 校验全部 32 张 PNG 头与布局不相交）
  - `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --stage4`（覆盖阶段 1–4 纯内存套件与 12 语言 623 键审计）
  - `dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -- "<GamePath>" <mode> "bin/Release/net6.0/StardewGallery.dll"`（共 13 个 `<mode>`：`--assembly`、`--conditions`、`--replay`、`--replay-effects`、`--replay-weather`、`--replay-speed`、`--controller-input`、`--keyboard-input`、`--gallery-drafts`、`--save-data`、`--stage2`、`--stage3`、`--stage4`）。
- **UI 皮肤与字体约束**：
  - 所有新增/调整的贴图切片集中在 `UI/GalleryUiSkin.cs` 与 `GallerySpreadLayout.cs`；修改 `assets/*.png` 后务必运行 `./tools/Optimize-Assets.ps1`（保留带 C2PA 凭证的 `CharacterScene-day-v2.png` 原字节不变）并同步 `Checks/GallerySpreadChecks.cs` 尺寸表。
  - 书本卷轴标题统一走 `GalleryDrawing.DrawBookTitle`（子矩阵原点 + `width: 999999`），切勿直接在大 `X` 坐标调用 `SpriteText.drawString`（会被原版按 `Game1.uiViewport.Width` 误判越界强行折行）。
  - 严禁在 UI 文本或 `i18n/*.json` 中写入星露谷点阵字库缺失的符号（如 `←`、`→`、`▶`、`◀`、`♪`，以及简体中文 `zh.json` 中的直角引号 `「」`），图标统一走 `GalleryUiSkin` 像素图集。

### 3. 后续收尾与正式发版清单
1. **实机体验反馈微调**：根据玩家在整合包（如 SVE、Visit Mount Vapius 等）及不同分辨率 / UI 缩放 / 手柄下的实机体验，按需微调细节并重新覆盖更新至 `<GamePath>/Mods/StardewGallery`（注意保留 `config.json`、`event-photos/`、`user-data/`）。
2. **定版号与更新日志**：实机确认满意后，确定正式新版本号（如 `2.11.0` 或 `3.0.0`），同步更新 `StardewGallery.csproj`、`manifest.json`、`CHANGELOG.md` 与双语 `README.md`。
3. **打包与归档**：运行 `./tools/Package-Release.ps1 -Destination ./release/StardewGallery-<版本>-Nexus.zip` 生成正式发布包并校验 SHA-256；按用户授权执行 Git 提交（署名 `Ayulog`）与仓库外历史快照归档。
