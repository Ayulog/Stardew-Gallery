# Stardew Gallery 2.1.0 — Event Detail Page

## 目标与边界

本版为每条当前事件增加完整详情页，逐条展示原始声明顺序、当前分析结果、缺口、无法判断原因、来源与原始条件，并允许从详情安全回放已解锁事件。

不实现 PossibilityIndex、Solver、事件命令语义、新 runtime truth provider、历史功能、多人回放、截图相册或解锁规则变化。

## 操作流程与实现

- 角色事件卡保留简短摘要，并为所有事件增加“详情”；未解锁事件可查看但不能回放。
- `ConditionPresentationBuilder` 在压缩摘要前保留 AST、Evaluation、Gap、Raw、Source 与 Negated；详情按声明顺序展示且不去重，卡片摘要继续去重。
- 已知条件显示满足/未满足；未知条件区分 MissingData、Unsupported、Invalid、Error。Gallery 不安装 GSQ provider，因此 GSQ 明确显示 Unsupported，Random、SendMail 与自定义条件仍不执行。
- Friendship 缺口显示对应 NPC；时间范围两端使用游戏时间格式，不显示原始 `1800..2200`。
- 详情回放复用 `RequestReplay` 与既有 `ReplayCoordinator`，结束后返回原角色、原事件列表滚动和当前事件焦点。

## UI 与坐标交互

- 复用 1672×941 画廊画布与 `GalleryDetail-alpha-v2.png`，不增加图片资源。
- 顶部约 105–200 显示角色、心数、Event ID、地点、解锁状态和 AssetName；210 起显示当前分析免责声明。
- 条件阅读区为 `(175,275,1300,520)`，按实际换行计算 block 高度并进行像素滚动；滚动条位于 `(1510,275,24,520)`。
- 鼠标滚轮每次 60px，支持轨道翻页与拖动；DPad 上下每次 60px。窗口尺寸变化后重新适配并限制滚动范围。
- 返回按钮 `(350,842,280,52)`；已解锁时回放按钮 `(1042,842,280,52)`。Escape、右键、手柄 B 返回事件列表；Gallery 快捷键关闭详情。
- 角色卡右侧“详情”使用独立 component ID 100–103；回放继续使用 0–3，保持回放返回焦点语义，锁定事件回退到详情按钮。

## 模块职责、配置与存储

- `ConditionPresentation` 负责纯展示模型和格式化；`GalleryCharacterMenu` 构建并复用 presentation；`GalleryEventDetailMenu` 只负责阅读、滚动和导航。
- 12 个官方 locale 同步新增详情文本并保持 key/token parity。
- 无配置、存档或 schema 变化；`gallery-state` 和已停用的历史数据保持不变。

## 兼容风险

- 复杂 Mod 条件仍诚实显示未知，不猜测也不调用第三方执行入口。
- 超长条件依赖像素滚动与裁剪；不同语言排版需实机抽查。
- 多人模式未实测，current replay 仍沿用原有多人禁用规则。

## 验收

- 自动检查 presentation 顺序/重复、known/unknown、Friendship subject、时间范围、raw/source/negation、GSQ 安全策略及 12 locale parity。
- 运行 Checks、PersistenceChecks、Release build 与基线 diff check。
- 实机 D21-1～D21-7：锁定/解锁详情、长条件、unknown、gap、鼠标/手柄/缩放导航及多语言；均待实测。
