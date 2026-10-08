# Building / 构建

## Environment / 环境

- Install a stable .NET 8 SDK. `global.json` selects the latest installed .NET 8 feature band; it does not upgrade the project's `net6.0` target.
  安装正式版 .NET 8 SDK。`global.json` 在已安装的 .NET 8 中选择较新的功能版本，项目目标框架仍为 `net6.0`。
- Install the .NET 6 runtime to execute the two check programs. The .NET 8 SDK alone does not supply that runtime.
  运行两个检查程序还需要 .NET 6 运行时，仅安装 .NET 8 SDK 不包含该运行时。
- A full MOD build needs Stardew Valley 1.6.15 and SMAPI 4.5.2, or later compatible versions. `smapi-internal/0Harmony.dll` must exist under the selected game directory.
  完整 MOD 构建需要《星露谷物语》1.6.15、SMAPI 4.5.2 或更高兼容版本，所选游戏目录中应有 `smapi-internal/0Harmony.dll`。

Run all commands from this repository root. / 以下命令均从本仓库根目录执行。

```powershell
dotnet --version
dotnet --list-runtimes
```

The selected SDK should be `8.0.x`; the runtime list should include `Microsoft.NETCore.App 6.0.x`.

所选 SDK 应为 `8.0.x`，运行时列表应包含 `Microsoft.NETCore.App 6.0.x`。检查程序可能显示 `NETSDK1138`，提示 .NET 6 已结束支持；该提示不代表检查失败，也不应因此自行更改游戏兼容目标。

## Checks / 自动检查

These standalone programs use repository fixtures and temporary data; they do not require the game or SMAPI installation.

两个检查程序使用仓库样例和临时数据，不依赖游戏或 SMAPI 安装：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

For appearance-only changes: / 只涉及外观逻辑时可运行：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --appearance
```

These are executable checks, not `dotnet test` projects. A nonzero exit code means failure. GitHub Actions runs both suites for pull requests and pushes to `main`; it does not build or deploy the MOD into a game installation.

它们是可执行检查程序，不是 `dotnet test` 工程；非零退出码表示失败。GitHub Actions 在 PR 和 `main` 推送时运行两套检查，不负责游戏目录中的安装或实机验证。

## Build / 完整构建

Replace the example path with your own SMAPI-enabled game directory. Keep machine-specific paths out of tracked project files.

将示例路径替换为已安装 SMAPI 的实际游戏目录，不把本机路径写入受版本管理的工程文件：

```powershell
dotnet build StardewGallery.csproj -c Release -p:GamePath="C:\Games\Stardew Valley"
```

The output DLL and build ZIP are written to `bin/Release/net6.0/`. `EnableModDeploy=false` prevents automatic installation into the game. A successful build still needs in-game testing with a test save and SMAPI logs before release.

DLL 和构建 ZIP 位于 `bin/Release/net6.0/`。`EnableModDeploy=false` 关闭自动安装；构建成功后，正式发布前仍需用测试存档进入游戏验收并检查 SMAPI 日志。

## Package / 打包

After building, package the current manifest version together with the README, changelog and license notices:

构建后，从 manifest 读取当前版本，并附带使用说明、更新日志和许可声明打包：

```powershell
$modVersion = (Get-Content -LiteralPath ./manifest.json -Raw | ConvertFrom-Json).Version
./tools/Package-Release.ps1 -Destination "./release/StardewGallery-$modVersion-Nexus.zip"
```

The destination must not already exist. The packaging script checks the version, all 12 locales, excluded player data and required documents, then reports the ZIP's SHA256. Creating a ZIP does not upload or publish it.

目标文件必须尚不存在。打包脚本会核对版本、12 种语言、玩家数据排除和必需文档，并输出 ZIP 的 SHA256；生成 ZIP 不会上传或发布。


## Event source checks / 事件来源检查

Pure source-chain, static file-origin and presentation checks are included in the regular Checks command. A focused run is also available:
来源链、静态文件匹配及显示逻辑检查已接入原有全量 Checks，也可定向执行：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --event-sources
```

The optional runtime harness requires the local game/SMAPI 4.5.2 DLLs. It uses in-memory fixtures and reads a vanilla Chinese event XNB without launching the game, installing files, or reading player saves. Replace GamePath with the confirmed installation path:
可选运行时检查需要本机游戏及 SMAPI 4.5.2 DLL，使用内存夹具并只读加载原版中文事件 XNB，不启动游戏、不安装文件、不读取玩家存档；请替换实际路径：

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:\Games\Stardew Valley" -- "C:\Games\Stardew Valley"
```

This harness is excluded from the MOD build/package and ordinary CI, which has no game binaries. Runtime check success is not in-game acceptance. See `docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md` for scope and remaining scenarios.
此检查程序不进入 MOD 构建/发布包，也不加入没有游戏 DLL 的普通 CI。检查通过不等于游戏内验收，范围和待验场景见 `docs/EVENT_MOD_SOURCE_IMPLEMENTATION.md`。

## Built DLL compatibility / 构建产物加载检查

On Windows, validate the built Gallery DLL through the installed SMAPI 4.5.2 assembly loader before packaging. Run in a fresh process; the loader keeps rewriting enabled and does not ignore incompatibility. It does not call Mod.Entry or launch the game. It also invokes the loaded DLL's JSON normalizer with permissive object/array fixtures.

Windows 本机打包前，可用真实 SMAPI 4.5.2 加载器检查 DLL；重写开启且不忽略兼容错误，不执行模组入口或启动游戏，并验证加载后的宽松 JSON 对象/数组读取。此检查不能复现所有第三方模组先前加载的依赖组合，不代替游戏内验收。

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --assembly "bin/Release/net6.0/StardewGallery.dll"
```
## Replay state regressions / 回放状态回归检查

After the full MOD build, run each command in a fresh process against the resulting DLL. These optional checks require the installed game and SMAPI DLLs and use in-memory state; they do not read saves or install the MOD.
完整构建后，用生成的 DLL 分别运行以下命令。可选检查依赖本机游戏及 SMAPI DLL，只使用内存状态，不读取存档或安装 MOD。

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --replay "bin/Release/net6.0/StardewGallery.dll"
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --replay-effects "bin/Release/net6.0/StardewGallery.dll"
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --replay-weather "bin/Release/net6.0/StardewGallery.dll"
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --replay-speed "bin/Release/net6.0/StardewGallery.dll"
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --gallery-drafts "bin/Release/net6.0/StardewGallery.dll"
```

The snapshot checks call the built Capture/RestorePlayer and RuntimeStateReader with real case-sensitive game collections. The effects checks install ReplaySaveGuard and invoke the actual GrandpaCandles command, checking replay suppression, exactly-once advancement and unaffected normal/unowned events. Fixture substitutions and limits are documented in SourcesRuntimeChecks/README.md. In-game natural completion, skipping, startup failure and recovery failure still need a test save and SMAPI logs.
快照检查使用真实游戏集合，调用产物中的 Capture/RestorePlayer 及 RuntimeStateReader；命令检查安装真实 ReplaySaveGuard 并执行原版 GrandpaCandles，验证回放保护、命令仅推进一次及正常/非所属事件不受影响。夹具替代范围见 SourcesRuntimeChecks/README.md。自然结束、跳过、启动失败和恢复异常的游戏内流程仍需测试存档及 SMAPI 日志验收。

The weather mode checks six native weather types, default-world flags and restoration. The speed mode exercises native recursive commands through the installed Harmony patch. The drafts mode exercises menu drafts and selection rebuilding in a headless fixture. Run the regular Checks suite too: it covers weather resolution, GSQ relationship classification, canonical character heart filters and independent source-evidence status.
天气入口检查六种原生天气、默认世界标记及恢复；倍速入口通过真实Harmony补丁执行原版递归命令；草稿入口在无图形夹具中验证菜单草稿与选项重建。同时运行常规Checks，覆盖天气解析、GSQ正向关系分类、角色别名心数筛选及独立来源证据状态。

## GMCM exclusion checks / GMCM 名单开关检查

完整构建后运行以下检查，验证默认关闭、实际注册/保存/重置回调，以及回放或确认期间延后刷新。夹具替代范围见 SourcesRuntimeChecks/README.md；不会启动游戏、安装 MOD 或访问玩家存档。

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --gmcm-exclusion "bin/Release/net6.0/StardewGallery.dll"
```

## Controller dispatch checks / 手柄分派检查

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --controller-input "bin/Release/net6.0/StardewGallery.dll"
```

The 36 assertions follow the native fresh-button dispatch order through actual menu handlers and temporary photo storage. They cover snappy on/off, one action per A press, B/X/shoulders and keyboard/mouse controls. The fixture substitutes graphics and navigation boundaries; it does not run the complete game update loop or physical controller input. See SourcesRuntimeChecks/README.md for scope.

36 项检查按原版新按键分派顺序执行真实菜单方法及临时照片存储，覆盖 snappy 开关、A 单次操作、B/X/肩键及键鼠。夹具替代图形和导航外围，不运行完整游戏更新循环或物理手柄；边界见 SourcesRuntimeChecks/README.md。

## Clean outputs after dependency removal / 依赖清理后的构建

The retired history/preview code and Microsoft.Data.Sqlite/SQLitePCLRaw dependencies are no longer built. Use a clean output directory when switching from 2.8.0, so stale DLLs and native runtime folders are not bundled. Package-Release.ps1 rejects SQLite libraries and database files. Do not remove player config, photos, names or existing historical databases as part of build cleanup.

旧历史／预览及 SQLite 依赖已移除。从 2.8.0 切换时使用干净的构建输出，避免旧 DLL 和原生运行时被带入新包。Package-Release.ps1 会拒绝 SQLite 程序库及数据库文件。构建清理不应删除玩家配置、照片、名称或以前的历史数据库。

The standalone persistence suite now covers active photo/name storage, save identity and backup retention; the removed historical database and preview-injection tests are no longer compiled. Current replay environment types live under Replay/, and current read-only state under Conditions/.

独立存储检查现覆盖照片／名称、存档身份与备份保留，已停止编译废弃历史数据库和预览注入测试。当前回放环境类型在 Replay/，只读状态在 Conditions/。
