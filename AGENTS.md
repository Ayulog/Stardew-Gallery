# Stardew Gallery 项目协作规则

## 项目与事实来源

- 本仓库为 `Ayulog/Stardew-Gallery`，以 C# / SMAPI 实现星露谷剧情画廊、条件查询、剧情回放及截图封面。
- 以当前代码、`StardewGallery.csproj`、`manifest.json` 和最新发布说明为准；`docs/` 内旧任务记录与验收结论仅适用于对应版本，不能视为本次任务授权或验证结果。
- 默认用中文沟通，维护 README 的中英文说明；保留现有命名与模块风格，长期规则写在本文件，任务进度放在对应文档。
- 当前主项目目标框架为 `net6.0`，游戏最低版本为 1.6.15，SMAPI 最低版本为 4.5.2；版本变化时同步核对项目文件和 manifest。

## 环境与命令

- 构建使用 .NET SDK 8，`global.json` 通过 `latestFeature` 选择已安装的正式版 8.0 SDK；目标框架仍保持 `net6.0`，运行两个检查程序需要 .NET 6 运行时。
- 环境、检查、构建和打包步骤见 `BUILDING.md`；打包文件名从 `manifest.json` 的实际版本生成。
- 完整 Mod 构建需要安装了 SMAPI 的游戏目录，`0Harmony.dll` 从该目录的 `smapi-internal/` 引用；先确认实际路径，不假设其他机器的路径存在。
- 以下命令从仓库根目录执行，`/path/to/Stardew Valley` 必须替换为已确认的游戏安装路径。

```sh
dotnet build StardewGallery.csproj -c Release -p:GamePath="/path/to/Stardew Valley"
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

- 仅外观逻辑修改可运行已提供的定向入口：`dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --appearance`。
- 两个 Checks 项目是可执行检查程序，按需使用上述 `dotnet run`；不要以 `dotnet test` 没报错代替它们。
- `.github/workflows/checks.yml` 对 PR 和 main 推送运行两套独立检查；完整 MOD 构建仍需带 SMAPI 的游戏目录，CI 通过不等于实机验收通过。
- Release 产物位于 `bin/Release/net6.0/`；项目设置 `EnableModDeploy=false`，构建不会自动安装到游戏。

## 代码组织

- `ModEntry.cs` / `ModConfig.cs`：SMAPI 入口、配置和模块接线；详细职责见 `docs/MODULES.md`。
- `Catalog/` / `Domain/`：当前事件资料、分类与身份；`Conditions/`：条件解析、说明与前置依赖。
- `Navigation/` / `UI/` 及 `Gallery*Menu.cs`：页面状态、导航和界面；菜单通过统一导航创建，页面不借另一页面读取业务数据。
- `Replay/`、`ReplayCoordinator.cs`、`ReplaySaveGuard.cs`、`ReplaySnapshot.cs`：当前回放权限、生命周期、保存保护与恢复。`Conditions/CurrentStateSnapshot` 与 `RuntimeStateReader` 为当前条件读取，不做预览注入。
- `Appearance/`：角色外观适配与缓存；`Screenshots/`：截图封面；`Persistence/`：本地存储；`i18n/`：12 种语言。
- `Checks/`：逻辑检查；`PersistenceChecks/`：存储检查；`tools/`：素材和发布工具。

## 业务与玩家数据边界

- 画廊读取当前实际生效的事件内容；回放使用当前内容和当前状态，不恢复为历史冻结剧情。
- 好感剧情进入主相册；普通剧情在查询中展示，回放需开启对应设置；前置事件仅供查询，纯转场及内部流程不能因一键解锁而变成可回放剧情。
- 一键解锁只影响画廊权限，不能修改实际好感、已看事件或任务进度；权限集中在回放策略中，不能仅靠隐藏按钮实现。
- 条件与前置分析保持只读；未知条件明确标为未知，不能执行 TriggerActions 或第三方动作来探测结果，也不能把当前满足条件等同于已经获得标记。
- 事件身份包含资产与 ID；同名地点、同 ID 或演员别名的查询分组不能覆盖真实身份、原始脚本和回放地点。
- 回放仅支持单人。保留回放期间禁止保存、备份、正常结束/跳过/异常时恢复玩家及环境的机制。
- 原版快照不能保证恢复第三方私有状态、外部文件或任意回调；研究和验证结论要写清实际覆盖范围。
- `config.json`、`event-photos/`、`user-data/` 为玩家数据；更新与测试必须保留它们。照片按存档区分，事件自定义名称在本机存档间共用。AI 名单排除统一使用 `config.json` 的 `EnableAiModExclusion`，默认关闭；按已授权的配置替换范围，运行时清理已废弃的 `ai-mod-exclusion.json`，不迁移其旧值。
- 存储变更保持已有数据兼容，验证迁移、失败恢复与未来 schema 保护；使用临时数据或测试副本验证，不把真实存档用作可丢弃夹具。
- 已删除旧历史采集、冻结回放、预览注入及 SQLite 源码和依赖；当前持久化只保留照片、名称和必要的存档身份。不要重新引入死功能，也不要删除玩家已有历史数据库。
- GMCM、Portraiture、DDFC、Scale Up 均为可选依赖；缺失或读取失败时保留可用回退、恢复重试与日志限频，不能令基础画廊不可用。

## 验证与逆向研究

- 修改前查看 Git 状态，保留已有工作；按受影响模块选择检查，普通文档修改无需重复构建或全量回归。
- 逻辑或权限修改运行相关 Checks；数据库、照片及名称存储修改运行 PersistenceChecks；生产代码修改在环境具备时构建主项目。
- 回放相关修改重点验证自然结束、跳过、启动失败和恢复异常，以及金钱、邮件、任务、已看记录、位置和时间恢复。
- UI 修改检查受影响页面的分辨率、缩放、长文本及键鼠/手柄导航；本地化修改核对 12 语言的键和占位符。
- 外观兼容按具体框架版本及组合验证；隔离渲染结果不能直接当作真实游戏全部组合验收。
- 参考 `docs/RELEASE_2.5.0_STORY_TESTING.md` 中相关场景，区分历史验收和本次验证；缺少 SDK、游戏或实机条件时如实记录未验证项。
- 研究游戏或其他 Mod 的 DLL 时，优先看对应版本的公开源码；需要反编译时记录程序集版本、哈希和关键方法，区分直接证据与推断。
- 游戏 DLL、其他 Mod 的 DLL 和逆向样本仅用于引用或隔离研究，不覆盖游戏原件，不随代码或发布包收录；项目正常依赖按许可和现有打包规则处理。

## 打包、发布与归档

- 版本调整同步 `StardewGallery.csproj`、`manifest.json`、更新日志及相关发布说明，保留稳定的 `UniqueID` 和 `EntryDll`。
- 构建成功后用 `./tools/Package-Release.ps1 -Destination ./release/StardewGallery-<版本>-Nexus.zip` 打包；替换 `<版本>` 为 manifest 实际版本，目标文件必须尚不存在。
- 打包脚本读取构建 ZIP、检查版本与 12 语言、排除玩家数据，并附带 README、更新日志和许可声明，最后输出 SHA256；不要绕过它的内容检查。
- 遵循项目 GPL-3.0 和 `THIRD-PARTY-NOTICES.md`；今后增加需分发许可的依赖时再维护 `licenses/`。不捆绑第三方 Mod 或立绘。
- `bin/`、`obj/`、`release/`、诊断输出、玩家配置和存档不提交；正式发布只包含预期的程序、资源、翻译与说明。
- 提交、推送、安装和发布按用户当前任务已经授权的范围执行；交付说明实际检查、结果与待实测场景。
- 历史归档以 `Stardew-Gallery` 独立文件夹保存，按日期时间新增快照，记录 Git 提交、版本和校验信息；快照保存在仓库外，不覆盖旧归档。
