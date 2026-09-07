# 2.1.2 Delta：输入隔离、UI 文案与返回位置

基线：`812ca9ab2288696921d682738788d23fa2d55ba6`。开发分支：`fix/2.1.2-input-ui-navigation`。状态：开发包已安装，待窗口模式单人实测，未正式发布。

## 本轮范围

- 搜索聚焦时隔离游戏内键盘快捷键，包括窗口模式 F1/F3；保留 Unicode 文字、退格、粘贴和退出编辑。过滤粘贴中的控制字符，功能键不作为搜索文字。
- 放大人物相册/事件详情底部 Back/Replay；允许进入书页下缘区域，不遮挡第三排事件和条件。按真实字体测量预留内边距，检查 12 locale。
- 地点优先取游戏/Mod 提供的可读名称；补齐截图中的 Jenkins House 的 12 语言名称，其他原始 ID 做可读回退。
- 在星期/季节否定集合的补集更短且非空时显示正向要求；仅改描述，不改表达式、三态、求值或事件选择。
- 将首页搜索、人物滚动和焦点的返回回调贯穿详情、回放提示取消、回放结束。核对事件身份及滚动行的返回传递，恢复对应卡片动作焦点。

## 输入补丁依据与边界

现有搜索在 ButtonPressed 调用 Suppress。已安装 CP 的 F3 在更早的 ButtonsChanged 读取 Keybind；SMAPI Suppress 不会即时改变 Keybind 读取的 ButtonStates，因此只提前 Suppress 仍不足。

目标：SMAPI 4.5.2 `StardewModdingAPI.Framework.Input.SInputState.TrueUpdate` Postfix。仅在搜索编辑或其按住按键尚未释放时，从当前键盘按钮字典隔离已捕获键，发生在 ButtonsChanged/ButtonPressed 分发前；原生文本事件继续由 KeyboardDispatcher 接收。保留原生粘贴所需键盘状态，其他快捷键使用 Suppress。没有 Transpiler，不改系统键盘钩子或其它 Mod 配置。

该接口为 SMAPI 内部类型，需检查方法/属性契约；不支持时禁用搜索并明确记录错误，不让无保护的搜索继续工作。补丁不改控制器、鼠标、存档、解锁或回放执行。系统级截屏工具及绕过 SMAPI 直接轮询硬件的外部代码不在此拦截层。

## 验收

- 搜索捕获与释放、粘贴/控制字符、星期和季节补集、地点回退、首页/事件返回位置的 focused checks。
- 12 locale key/token parity、字体测量与静态渲染截图；窗口尺寸 1280×720 和较大窗口下检查按钮、长文案及留白。
- 对新的输入内部补丁做实际 SMAPI 类型契约及按键读取验证；构建、包检查。
- 玩家单人实测：窗口中 F1/F3/其他快捷键、中文及英文输入/粘贴、鼠标/手柄多页往返、按钮点击。未经实际验证的项目保持待实测。

## 已完成验证

- Release build：0 warning / 0 error；Checks PASS，包含搜索缓存、输入捕获、星期/季节全部有效否定集合、地点回退、返回位置、书页布局、12 locale parity。Checks 的 net6.0 EOL 提示为既有工具链提示。
- 独立进程使用本机游戏/SMAPI DLL：Harmony 注册成功，F1/F3/G/Ctrl/V 的 SMAPI 状态已隔离；原生 F1/F3/G 被抑制，Ctrl+V 状态仍保留，鼠标/手柄状态未改。此项不是实际游戏输入或 IME 验收。
- 原版字体与生产按钮绘制方法：12 locale、36 个按钮标签测量通过，1280×720 与 1920×1080 的相册/详情共 48 张静态图；地点使用生产换行方法。已目视核对 12 语言按钮对照图，文字未压边，按钮未覆盖第三排事件。
- 开发包和安装 DLL 与验证构建哈希一致，包内 12 locale；安装前旧版留档，原 config 保留。未改游戏原文件、其他 Mod 或存档。

复测重点：搜索聚焦时尝试 F1/F3、其他已绑定快捷键及组合键；输入/粘贴中文和拉丁文字、退格、Enter、Escape；退出搜索并松键后快捷键应恢复。滚动到后面的角色和事件，进入详情后逐层返回，应保留原位置。

不修改 AST/parser/evaluator、candidate/current selection、NativePreconditionProbe、Replay/save/restore、History、Persistence，不重跑这些 CLOSED 模块的审计。
