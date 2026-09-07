# Stardew Gallery 2.1.0 — Event Album + Event Detail

## 目标与边界

本版把角色事件页定型为缩略图相册，并提供逐条件详情：Layer 1 人物相册不改；Layer 2 左页保留人物大图与固定六行资料，右页显示 2×3 Event Card；Layer 3 只替换右页为事件信息和条件状态。

保留 `ConditionDisplayItem`、`ConditionPresentationBuilder`、AST、Evaluation、Gap、Raw、Source、Negated、Unknown taxonomy、当前事件脚本回放及返回位置。没有新增截图系统、PossibilityIndex、Solver、runtime truth provider、历史功能、多人回放、解锁规则、存储或 schema。

## 操作流程与实现

- Layer 2 每屏两列三行，超过六个事件按行连续滚动。卡片只显示心数门槛、Event ID、`详情 >`、默认缩略图；不显示地点、条件摘要或诊断 tooltip。
- 所有事件都能打开详情。已解锁事件点击缩略图或 `▶` 进入既有 `RequestReplay → ReplayCoordinator`；锁定事件隐藏回放入口，不能绕过 Gallery unlock。
- `EventThumbnailAsset.For(EventIdentity)` 是薄 provider 边界；2.1.0 对任意事件都返回 `assets/EventPlaceholder.png`，不扫描文件、不建缓存或 metadata。
- Layer 3 条件保持声明顺序且不排序、不去重。每个条件一个可变高度 row：左侧显示要求，可靠时附当前值；右侧严格映射 Known True=`✓`、Known False=`✗`、Unknown=`?`。
- Unknown 包括 MissingData、Unsupported、Invalid、Error，只在第二行显示准确的轻量原因；普通条件不显示 Raw、Source、Knowledge、Gap 标签，底层结构化字段仍保留。
- 长条件完整换行，状态图标在对应 row 垂直居中；条件区继续使用像素滚动和裁剪。

## UI 与坐标交互

- 画布继续使用 1672×941 和 `GalleryDetail-alpha-v2.png`。两层左页都由 `GalleryCharacterPanel` 绘制，人物图 `(240,120,380,270)`、姓名/心数及四条资料坐标和含义均沿用旧实现，共固定六行。
- Layer 2 卡片从 `(755,140)` 开始，列距 365、行距 225，单卡 `345×205`；缩略图 `265×149`。滚动条 `(1508,180,24,600)`，返回按钮 `(360,842,280,52)`。
- Layer 3 右页标题/心数与 ID/地点/缩略图位于顶部；条件区 `(755,365,720,420)`，滚动条 `(1508,365,24,420)`；已解锁回放 `(795,842,280,52)`，返回 `(1160,842,280,52)`。
- Layer 2 支持滚轮、滚动条点击/拖动、DPad/左摇杆和缩放重排；焦点按 row-left、row-right 的视觉顺序，详情和回放使用基于事件索引的稳定 component ID。
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
- 状态使用游戏已有字体绘制符号与颜色，避免引入额外图标资源；Unknown 绝不降级成红叉。
- 不调用 Random、SendMail、GSQ 或自定义条件 callback。多人模式未实测；回放仍沿用既有多人禁用规则。
- 超长翻译、不同 UI scale、手柄边界滚动和占位图视觉需进游戏实测。

## 验收

- 自动：Layer 2 locked/unlocked action、2×3 顺序与无重叠、条件三态、Friendship/Time row、无 CurrentValue row、Unknown reason、共享 placeholder、12 locale key/token parity、既有 Condition/Replay/Persistence 回归。
- 运行 `Checks`、`PersistenceChecks`、Release build、`git diff --check b589a9b...HEAD`。
- 实机 U21-1～U21-12：左页一致、2×3 与 >6 滚动、placeholder、锁定/解锁、逐条件 row、current、unknown、长文本、footer/navigation、返回位置与多语言；交付前均标记待实测。
