# Stardew Gallery 2.1.0 — Event Album + Event Detail

> 下方第一阶段记录保留为历史上下文；当前实现以本文末尾的 **UI Integration Redesign Phase 2** 为准。V21 实机门尚未完成，不能宣布 UI CLOSED。

## 目标与边界

本版把角色事件页定型为缩略图相册，并提供逐条件详情：Layer 1 人物相册不改；Layer 2 左页保留人物大图与固定六行资料，右页显示 2×3 Event Card；Layer 3 只替换右页为事件信息和条件状态。

保留 `ConditionDisplayItem`、`ConditionPresentationBuilder`、AST、Evaluation、Gap、Raw、Source、Negated、Unknown taxonomy、当前事件脚本回放及返回位置。没有新增截图系统、PossibilityIndex、Solver、runtime truth provider、历史功能、多人回放、解锁规则、存储或 schema。

## 操作流程与实现

- Layer 2 每屏两列三行，超过六个事件按行连续滚动。卡片只显示心数门槛、Event ID、`详情 >`、默认缩略图；不显示地点、条件摘要或诊断 tooltip。
- 所有事件都能打开详情。已解锁事件点击缩略图或 `▶` 进入既有 `RequestReplay → ReplayCoordinator`；锁定事件隐藏回放入口，不能绕过 Gallery unlock。
- `EventThumbnailAsset.For(EventIdentity)` 是薄 provider 边界；2.1.0 对任意事件都返回 `assets/EventPlaceholder.png`，不扫描文件、不建缓存或 metadata。
- Layer 3 条件保持声明顺序且不排序、不去重。每个条件一个可变高度 row：左侧显示要求，可靠时附当前值；多人好感缺口通过 `GapSubject` 标明当前值对应 NPC；右侧严格映射 Known True=原版 checked sprite、Known False=原版 close/cancel sprite、Unknown=`?`。
- Unknown 包括 MissingData、Unsupported、Invalid、Error，只在第二行显示准确的轻量原因；普通条件不显示 Raw、Source、Knowledge、Gap 标签，底层结构化字段仍保留。
- 长条件完整换行，状态图标在对应 row 垂直居中；条件区继续使用像素滚动和裁剪。

## UI 与坐标交互

- 画布继续使用 1672×941 和 `GalleryDetail-alpha-v2.png`。两层左页都由 `GalleryCharacterPanel` 绘制，人物图 `(240,120,380,270)`、姓名/心数及四条资料坐标和含义均沿用旧实现，共固定六行。
- Layer 2 卡片从 `(755,140)` 开始，列距 365、行距 225，单卡 `345×205`；缩略图 `265×149`。滚动条 `(1508,180,24,600)`，返回按钮 `(360,842,280,52)`。
- Layer 3 右页标题/心数与 ID/地点/缩略图位于顶部；条件区 `(755,365,720,420)`，滚动条 `(1508,365,24,420)`；已解锁回放 `(795,842,280,52)`，返回 `(1160,842,280,52)`。
- Layer 2 支持滚轮、滚动条点击/拖动、DPad/左摇杆和缩放重排；焦点按 row-left、row-right 的视觉顺序，详情和回放使用基于事件索引的稳定 component ID。已解锁卡为 Details↓Replay、Replay↑Details；Replay 跨行保持 action family，目标锁定时回退 Details。
- Layer 3 支持滚轮、滚动条点击/拖动、DPad/左摇杆。Escape、右键、Controller B 返回 Layer 2；Gallery 快捷键 G 关闭整个 Gallery。
- 从详情返回或回放结束都恢复原角色、原 event scroll 和当前 Event focus；回放结束回 Layer 2，不回详情。

## 模块职责、配置与存储

- `GalleryCharacterPanel`：两层共用的左页绘制。
- `GalleryCharacterMenu`：2×3 相册、滚动、卡片交互和条件 presentation 构建。
- `GalleryEventDetailMenu`：右页头部、逐条件 row、滚动与 footer。
- `ConditionRowPresentation`：纯三态、row 文本和 unknown reason 映射。
- `GalleryUiRules`：纯 2×3 坐标、卡片交互与返回位置规则。
- 配置与存储：不适用；无配置、存档、历史数据或 schema 变化。

## 素材与兼容风险

- `assets/EventPlaceholder.png` 为本项目生成的原创暖色像素画，无 NPC、Event 内容、文字、第三方素材或游戏原资产；最终为 640×360 PNG。
- True 使用 Stardew `OptionsCheckbox.sourceRectChecked`，False 使用 `Game1.mouseCursors` 的原版 close sprite `(337,494,12,12)`；Unknown 没有匹配的统一原版状态 sprite，保留字体问号。Unicode `✓/✗` 运行时依赖已移除，Unknown 绝不降级成红叉。
- 不调用 Random、SendMail、GSQ 或自定义条件 callback。多人模式未实测；回放仍沿用既有多人禁用规则。
- 超长翻译、不同 UI scale、手柄边界滚动和占位图视觉需进游戏实测。

## 验收

- 自动：Layer 2 locked/unlocked action、2×3 顺序与无重叠、条件三态、单/多人 Friendship 与 Time row、多人 `GapSubject`、无 CurrentValue row、Unknown reason、共享 placeholder、12 locale key/token parity、既有 Condition/Replay/Persistence 回归。
- 运行 `Checks`、`PersistenceChecks`、Release build、`git diff --check b589a9b...HEAD`。
- 新增实机 C21-1～C21-3：纯手柄到达同卡 Replay/跨行混排、原版三态图标视觉、多人 Friendship subject；完成后再跑 U21-1～U21-12，交付前均标记待实测。

## UI Integration Redesign Phase 2

### 基线与范围

- 分支：`feature/2.1.0-event-detail`。
- 精确基线：`dcfac2c3ec8a86e8e845ae2d0eb7becc99d6d198`；另检查 `b589a9b` 至最终提交的 whitespace diff。
- 版本仍为 `2.1.0`，manifest、项目版本均不变。
- Layer 1 绘制不变，`GalleryMenu` 仅传递 Layer 2 所需的新 replay texture。
- Condition AST/evaluator、三态分类、GapSubject、unlock、RequestReplay、EventPlayback、Catalog、History、Persistence 全部不变。
- 不新增条件探测或 callback，不开始 PossibilityIndex、Solver、截图系统、Historical Replay 或下一版本。

### 视觉归属

采用 A：Background owns structure。一种视觉边框只有一个 owner。

| 元素 | Owner |
| --- | --- |
| book border / page texture / title slot | background |
| album slot corners | album background |
| detail header/condition heading/footer 分区线 | detail background |
| scrollbar channel | background |
| thumbnail 内容 | runtime underlay，继续 `EventThumbnailAsset.For` -> placeholder |
| 条件 row separator | runtime，单条淡棕线 |
| 文字 / 状态图标 / Replay glyph | runtime |
| Details underline / thumbnail corner focus | runtime |
| scroll thumb / footer buttons | runtime，复用 Layer 1 renderer |

Layer 2 删除每卡 `drawTextureBox`；Layer 3 删除条件区外框与逐行 `drawTextureBox`。不再叠加旧橙色容器。

### 坐标与资源

`GallerySpreadLayout` 集中维护 1672×941 逻辑画布、book/spine anchors、两页 bounds、标题、六行左页、两层右页/scroll/footer/icon source。

- 左页 portrait `(240,120,380,270)`，六行 `(195,432+63*i,445,48)` 完全保持原坐标；`GalleryCharacterPanel` 仅将字面常量替换为共享 bounds。
- Layer 2 为 `345×205` 六卡，起点 `(755,140)`，列距365，行距225；header / Details / thumbnail 是互不重叠的子区域。
- Details 是 smallFont 文本 link，hitbox 按实际测量文字加4px padding，各 locale 在98×36区域内 fitted；无厚按钮。
- thumbnail 265×149，unlocked 图像可点击回放；locked 使用轻度暗化且隐藏播放标记，不禁用 Details。
- Layer 3 header：左侧425px元信息，右侧265×149封面；条件标题固定，正文 viewport `(755,365,710,420)`。
- 两层 scrollbar X=1508；footer 共享 `(755,810,710,40)`，文字 baseline 位于842。
- Layer 3 row 测量与绘制共用622px宽度，保持原 structured row 文本与 Unknown reason，变量高度/像素滚动/scissor，图标垂直居中。
- 状态 atlas：`assets/ConditionStatusIcons.png`，48×16，check/cross/question 顺序，16×16源、3倍绘制、PointClamp。
- 播放图标：`assets/ReplayGlyph.png`，16×16原创像素三角，仅用于 Layer 2 thumbnail；Layer 3 footer Replay 使用普通文字按钮。
- 背景：`assets/GalleryEventAlbum-v3.png`、`assets/GalleryEventDetail-v3.png`，各1672×941。
- 资源生产说明见 `docs/GALLERY_SPREAD_ASSETS.md`；`tools/Generate-GallerySpreadAssets.ps1 -VerifyOnly` 验证原左页逐像素及透明 portrait 孔位、布局与图标。素材只重用本项目已有美术并原创绘制右页/图标，无第三方 Mod 或 icon sheet。

### Controller 规则

`EventCardFocus(EventIndex, Details|Replay)` 为纯 helper，ID采用 `2000 + 2*eventIndex + action`，不以可视slot作为长期身份，避免两组base ID在大目录中冲突。

- Details 左/右：同视觉行另一列 Details；第一列 Left 进入 Back。
- Details Down：unlocked进入同卡Replay；locked进入下一行同列Details。
- Details Up：上一行同列Details。
- Replay Up：同卡Details；左右及Down保留Replay family，目标locked退到Details。
- 可视第三行Replay Down才滚一行，真实事件索引/列/action一起保留；目录末尾Down进入Back。
- Back记住卡片action并返回；在Back焦点下滚动后，将记忆焦点限制到新可见行，避免跳回目录首行。
- `applyMovementKey` 是两层的唯一方向调度入口；删除 Layer 2 的 `ScrollController` 拦截，Layer 3 上下像素滚动、左右footer。
- A按逻辑component激活对应Details/Replay，不依赖旧鼠标位置；重排/滚轮后同步Snappy光标，拖动期间不抢光标、release后同步。
- native输入分发、按住重复及一次A是否恰好调用一次仍必须实机验证，不以纯helper通过替代。

### 自动检查与兼容性

- `Checks/EventCardFocusChecks.cs` 覆盖F21-1到5、前三行同卡Details↓Replay、第四行滚动、锁定回退、奇数尾行、footer焦点恢复、10000个稳定ID，以及全部512种九卡解锁排列的可达性。
- `Checks/GallerySpreadChecks.cs` 覆盖六卡与子区域无重叠、Layer 3各分区、scroll/footer、左页不变、资源路径/PNG尺寸/atlas边界；这些文件显式link进Checks。
- 12 locale同步加入自然否定、地点与安全 fallback 文案；key/token parity和条件三态/多好感subject/time/无current回归继续执行。
- 检查工程一度触发CS8785：SDK8的C#12 tuple alias触发net6 JSON generator旧语法扫描。基线复现对比后，移除测试中的tuple alias解决；没有禁用analyzer或增加依赖。
- 构建继续 `EnableModDeploy=false` / `EnableModZip=true`；本轮修正完成后按任务授权手动更新本项目对应 Mods 子目录，没有修改其他 Mods。
- ZIP已检查包含两张新背景、状态atlas、ReplayGlyph和既有EventPlaceholder；新tools/test/source未作为runtime文件打包。

### 手工门与截图

以下均为 **MANUAL REQUIRED**，本次没有获得运行中游戏截图，不宣布2.1.0 UI CLOSED：

| 项目 | 状态 / 所需证据 |
| --- | --- |
| V21-1 背景融合 | MANUAL REQUIRED：Layer2 100%截图无双框 |
| V21-2 对齐 | MANUAL REQUIRED：100%/90%/80% UI scale |
| V21-3 左页 | MANUAL REQUIRED：两层左页对比；自动像素/坐标检查已通过 |
| V21-4 每行Details | MANUAL REQUIRED：第一/二/三及滚后第四行A打开 |
| V21-5 Replay | MANUAL REQUIRED：左右列/各行/locked fallback |
| V21-6 Layer3 | MANUAL REQUIRED：专属右页整体截图 |
| V21-7 图标 | MANUAL REQUIRED：三态同屏；原创PNG已静态查看 |
| V21-8 长条件 | MANUAL REQUIRED：multi Friendship、GSQ、opaque换行/滚动 |
| V21-9 Unknown | MANUAL REQUIRED：问号与轻量原因，不变红叉 |
| V21-10 >6事件 | MANUAL REQUIRED：滚动后槽位/滑块 |
| V21-11 Mouse | MANUAL REQUIRED：link/thumbnail/drag/Back hitbox |
| V21-12 locale | MANUAL REQUIRED：EN/DE/RU/ZH截图 |

交付截图清单：Layer2 100%、Layer3 100%、Layer2 >6滚后、Layer3长条件、row2 Details焦点、row3 Replay焦点。全部待用户实机取得；没有实现截图功能。

本轮构建已更新到游戏 Mods 目录且保留 `config.json`，安装 DLL 与 Release build SHA-256 一致；旧安装与旧发布包已归档。仍需完成 VP21/WT21 实机验收，不自动merge/tag/release或开始2.1.1。

## UI 融合与条件文案修正

- 修正基线：`d850388e115028596d8c45d36dd9f102bb286e2d`；版本保持 `2.1.0`。
- Layer 2/3 缩略图改为先画图片、再画带透明孔位的书页背景；孔位边框由背景收口，条件文字仍直接画在羊皮纸上。
- Layer 2/3 返回与回放统一复用 `GalleryMenu.DrawButton`；三层滚动条统一复用 `GalleryMenu.DrawScrollbar` 和固定 40px thumb。
- 右页纸张生成移除 RGB 偏移，并验证孔位透明比例、边框、左页 RGBA 与补纸边界连续性。
- Condition 主文案只描述 requirement；Known True / Known False / Unknown 仍只由右侧图标表达。常见否定改为自然语言，天气、季节、时间、NPC 与地点等展示值在 UI 边界本地化。
- AST、evaluator、NativePreconditionProbe、unlock、replay、history 与 persistence 均未改变。

### Future / Backlog（本轮不实现）

- Seen-event quick jump：从“已看过事件”条件跳到目录中对应 Event Detail；使用 navigation stack 保存来源 `EventIdentity`、角色/owner 与 detail scroll，Back 后原样恢复。
- Search performance / fast locate：仅在 query/catalog 变化时搜索；为每个 catalog 建不可变索引，覆盖 NPC 内部名/显示名和 Event ID，并为未来直接定位 Event 预留结果目标；避免每帧重复扫描与拼接字符串。
