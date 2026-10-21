# Stardew Gallery 2.0.4 — Safety Boundary & Replay Lifecycle

## 目标与边界

本版只收紧两个高风险边界：所有仅用于目录或历史判定的原生事件条件检查统一经过只读安全策略；回放恢复、清理和返回标题生命周期保证异常后解除 active/save guard。同步修正两个未实际命中目标路径的 Preview scope 测试。

不改 Gallery UI、41 条条件解析/说明、解锁规则、回放场景能力、历史存储、SQLite schema、多人回放或 CurrentStateAccuracy。

## 操作流程与实现

- `NativePreconditionProbe` 先解析完整 Event key。仅当全部条件来自 legacy Event precondition，且不含 Random 或 SendMail 时调用原生 checker；GSQ、未知、自定义、malformed、Random 和 SendMail 返回 `NotSafelyEvaluated`，原生回调零次。原生 false、match 和异常分别映射为 `NotMatched`、`Matched`、`Error`。
- 当前目录按原顺序选择 variant：安全 false 继续；match 立即选择；unsafe/error 停在第一个无法排除的候选；全部安全 false 时沿用候选 0。`ResolvedEventGroup` 记录 `NativeMatch`、`IndeterminateFallback` 或 `NoMatchFallback`，暂不展示到 UI。
- 历史同脚本候选共用 probe：安全 false 后可继续到 match；遇 unsafe/error 立即放弃本次 capture，不猜测 variant，也不迁移历史数据。
- `ReplayCoordinator.Update` 用单一异常边界包住 active update。FailSafe 对备份恢复、菜单、scope/assets 清理和存档重载分别 best-effort，内部字段无条件重置；重载失败退回标题。
- `ReturnedToTitle` 只释放 Mod 内 replay 状态与资源引用，不 warp、不 reopen、不恢复或删除失败备份；重复调用安全。

## 模块职责、配置与存储

- 新 probe 只负责原生检查的安全分类及结果映射；Parser 恢复为纯 AST 解析职责。
- Catalog/History 只消费 probe result 并保留各自的顺序语义。
- ReplayCoordinator 负责 replay teardown；save guard 继续只依赖 `IsActive`。
- 无配置、存档、数据库或玩家文本变化。UI 原型和坐标交互不适用。

## 兼容风险

- 保守跳过 GSQ、自定义条件和 Random，可能使当前目录选中更早的 indeterminate variant，但不会越过无法安全排除的高优先级候选，也不会因探测执行未知副作用。
- 历史采集遇无法安全判定的同脚本 variant 时跳过该次 snapshot。
- 回放仍仅支持单人；多人模式未实测。

## 验收

- 自动检查覆盖 probe 状态与回调次数、目录顺序/来源、历史选择、Preview partial apply 和同一 scope 二次 Dispose。
- 运行 Checks、PersistenceChecks、Release build 与基线 diff whitespace check。
- 实机 R24-1～R24-5：正常/跳过/跨地点回放、回放期间返回标题、含 Random/GSQ/custom 的只读目录 smoke；核对状态恢复、save guard、备份及 SMAPI 日志。
