# 2.1.1 Delta：搜索性能

状态：开发与自动验证完成，开发包已安装；待单人实测，未 CLOSED。基线 `61eb6a3740e5bede0d637ffa44e4c44116b02bba`，分支 `fix/2.1.1-search-performance`。

## 相对基线改什么

- 首页搜索只在文本、目录实例、locale、匹配文化或中英文排序规则变化时重新筛选；空闲帧复用结果。
- `GallerySearchFilter` 承担结果刷新与缓存失效；`GalleryMenu` 继续负责滚动、焦点和打开人物。结果人物序列变化时才重建点击组件。
- 保留原有 NPC 内部名/显示名和 Event ID 的大小写、子串、owner 匹配及排序规则。回车打开前刷新文本，避免同帧输入读取旧结果。
- 版本更新至 2.1.1；更新日志同步。不新增配置、依赖或玩家文本。

## 不改什么

- 搜索结果仍为人物；事件直达、前置事件跳转、截图和额外关键词不在本轮范围。
- 目录仍由既有 `GalleryCatalogCache.Get()` 生成新快照；不修改 candidate/current selection、AST、evaluator、NativePreconditionProbe、unlock、Replay、History 或 Persistence。
- 无 UI 布局或素材变化；不升级游戏、框架或最低支持版本。

## 验收

- 使用现有 Checks 入口：覆盖空闲帧不重算、输入变化、目录实例替换（包括值相同的新实例）、locale/文化/排序切换、无结果与清空恢复、与 2.1.0 筛选结果逐项一致。
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --benchmark-search`：同一合成大目录、同一输入序列比较 2.1.0 逻辑和新实现的耗时、分配及重算次数；数据只代表搜索处理，不代表游戏帧率。
- Release build、版本/包内容和 diff whitespace 检查；既有 Checks 中的 locale parity 复用，不另跑无关存储审计。
- 单人实测待完成：大目录输入 NPC 名/Event ID、粘贴后回车、清空、滚动及返回、键鼠/手柄焦点；重新打开画廊后目录与语言刷新正确。

本轮不以自动检查代替游戏实测，不自动合并或正式发布。

## 验证结果（2026-09-07）

- Checks PASS，包括新增搜索回归和既有 locale parity；检查工程仅有目标 net6.0 的既有 NETSDK1138 提示。Release build 为 0 warnings / 0 errors。
- 对照使用 120 NPC、1800 事件、300 次调用；计时仅覆盖合成搜索处理，含首次计算，不包含渲染与原生输入。

| 场景 | 2.1.0 耗时 / 重算 | 2.1.1 耗时 / 重算 |
| --- | --- | --- |
| 空搜索 | 13.29 ms / 300 | 0.13 ms / 1 |
| NPC 内部名 | 653.20 ms / 300 | 2.06 ms / 1 |
| Event ID | 910.98 ms / 300 | 3.04 ms / 1 |
| 无匹配 | 549.05 ms / 300 | 1.96 ms / 1 |
| 10 次文本变化 | 475.24 ms / 300 | 15.84 ms / 10 |

- 五种场景缓存命中后再各调用 300 次，刷新函数分配均为 0 bytes。将 LINQ 闭包限制在缓存失效路径，避免早返回前仍分配闭包对象。
- 包 manifest 为 2.1.1，12 locales 完整；无测试、工具、源码或 config 混入 runtime 包。安装 DLL 与构建 DLL SHA-256 一致；config 已保留，原 2.1.0 安装已归档。
- 本轮只改变搜索 presentation 路径；AST、evaluator、unlock、replay、history、persistence、candidate/current selection 均未改。
