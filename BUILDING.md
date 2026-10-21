# Building / 构建

## Environment / 环境

- Install a stable .NET 8 SDK. `global.json` selects the latest installed .NET 8 feature band; it does not upgrade the project's `net6.0` target.
  安装正式版 .NET 8 SDK。`global.json` 在已安装的 .NET 8 中选择较新的功能版本，项目目标框架仍为 `net6.0`。
- Install the .NET 6 runtime to execute the two check programs. The .NET 8 SDK alone does not supply that runtime.
  运行两个检查程序还需要 .NET 6 运行时，仅安装 .NET 8 SDK 不包含该运行时。
- A full MOD build needs Stardew Valley 1.6.15 and SMAPI 4.5.2, or later compatible versions. `smapi-internal/0Harmony.dll` and the game's `Steamworks.NET.dll` must exist under the selected game directory.
  完整 MOD 构建需要《星露谷物语》1.6.15、SMAPI 4.5.2 或更高兼容版本，所选游戏目录中应有 `smapi-internal/0Harmony.dll` 和游戏自带的 `Steamworks.NET.dll`。

Run all commands from this repository root. / 以下命令均从本仓库根目录执行。

```powershell
dotnet --version
dotnet --list-runtimes
```

The selected SDK should be `8.0.x`; the runtime list should include `Microsoft.NETCore.App 6.0.x`.

所选 SDK 应为 `8.0.x`，运行时列表应包含 `Microsoft.NETCore.App 6.0.x`。检查程序可能显示 `NETSDK1138`，提示 .NET 6 已结束支持；该提示不代表检查失败，也不应因此自行更改游戏兼容目标。

## One-command verification / 一键验证

On Windows, `tools/Verify-Local.ps1` runs the full local sequence: the MOD build, both check programs, every SMAPI runtime check mode, then packaging with `Package-Release.ps1` and its SHA256. It stops at the first failure. Without `-Destination` the package is written to a new temporary folder, so `release/` is untouched. It reads the game directory only; it does not install the MOD, launch the game or read saves.

Windows 上可用 `tools/Verify-Local.ps1` 一次完成：完整构建、两个检查程序、全部 SMAPI 运行时检查，再用 `Package-Release.ps1` 打包并输出 SHA256；任何一步失败即停止。不传 `-Destination` 时，包写到新的临时文件夹，不动 `release/`。脚本只读取游戏目录，不安装 MOD、不启动游戏、不读存档。

```powershell
pwsh tools/Verify-Local.ps1 -GamePath "C:/Games/Stardew Valley"
```

It does not replace in-game acceptance; see the latest `docs/RELEASE_*_ACCEPTANCE.md`.

它不代替实机验收，见最新的 `docs/RELEASE_*_ACCEPTANCE.md`。

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

Static file-origin scanning, matching and presentation checks are included in the regular Checks command. A focused run is also available:
静态文件来源扫描、匹配及显示逻辑检查已接入原有全量 Checks，也可定向执行：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --event-sources
```

Runtime modification tracking was removed in 2.10.0, so there is no separate source runtime harness. Event sources come only from read-only static file matching.
2.10.0 已移除运行时修改追踪，不再有单独的来源运行时检查；事件来源只来自只读的静态文件匹配。

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

The weather mode checks six native weather types, default-world flags and restoration. The speed mode exercises native recursive commands through the installed Harmony patch. The drafts mode exercises menu drafts and selection rebuilding in a headless fixture. Run the regular Checks suite too: it covers weather resolution, GSQ relationship classification, canonical character filters and static source presentation.
天气入口检查六种原生天气、默认世界标记及恢复；倍速入口通过真实Harmony补丁执行原版递归命令；草稿入口在无图形夹具中验证菜单草稿与选项重建。同时运行常规Checks，覆盖天气解析、GSQ正向关系分类、角色别名筛选及静态来源显示。

## Condition parity checks / 条件判定对拍

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --conditions "bin/Release/net6.0/StardewGallery.dll"
```

The 75 assertions run the game's own precondition code (`Event.CheckPrecondition`, `Event.SplitPreconditions`, `ArgUtility.UnsplitQuoteAware`, `GameStateQuery`) and the gallery's production parser, state reader and evaluator on the same headless player: event IDs, the single `!` prefix, quoted GSQ text, multi-season lists, `D`/`R`/`O`, `w` and `WEATHER Here`. Graphics-dependent player setup is skipped; no save, map or content is loaded.

75 项检查在同一个无图形玩家上同时调用游戏自己的前置条件代码和画廊的生产解析、状态读取与求值，逐项比对：事件 ID、单个 `!` 前缀、加引号的 GSQ、多季节写法、`D`/`R`/`O`、`w` 和 `WEATHER Here`。只跳过依赖图形的玩家初始化，不读存档、地图或游戏内容。

## Save data checks / 存档数据检查

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --save-data "bin/Release/net6.0/StardewGallery.dll"
```

Drives the built ModEntry's unlock read and toggle as a host and as a remote farmhand, with a fake SMAPI data helper: a farmhand never touches save data (SMAPI would throw) and keeps the unlock for the session; the host reads and writes its save; a SMAPI error is logged, not thrown.

用假的 SMAPI 数据接口驱动构建产物中的解锁读取与切换：远程农场工不读写存档数据（SMAPI 会抛异常），解锁只在本次会话生效；房主正常读写；SMAPI 报错时只记日志不抛出。

## Controller dispatch checks / 手柄分派检查

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --controller-input "bin/Release/net6.0/StardewGallery.dll"
```

The 62 assertions follow the native fresh-button dispatch order through actual menu handlers and temporary photo storage. They cover snappy on/off, one action per A press, B/X/shoulders and keyboard/mouse controls, Enter in the search box showing results without opening one, and the Steam keyboard: falling back to the game's keyboard when Steam can't show it, and pressing Enter only after the controller's buttons are released. The fixture substitutes graphics and navigation boundaries; it does not run the complete game update loop, physical controller input or Steam itself. See SourcesRuntimeChecks/README.md for scope.

62 项检查按原版新按键分派顺序执行真实菜单方法及临时照片存储，覆盖 snappy 开关、A 单次操作、B/X/肩键及键鼠、搜索框回车只列出结果不进入，以及 Steam 键盘：Steam 显示不了时回退到游戏键盘、手柄按键松开后才按回车。夹具替代图形和导航外围，不运行完整游戏更新循环、物理手柄或 Steam 本身；边界见 SourcesRuntimeChecks/README.md。

## Keyboard typing checks / 键盘输入检查

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --keyboard-input "bin/Release/net6.0/StardewGallery.dll"
```

The 7 assertions type on a simulated physical keyboard, frame by frame, through SMAPI's real input state and the real query page: typed letters never reach SMAPI's input state (other mods' keybinds) or the game; the Enter that ends typing doesn't also open the first result, and a control run without the gallery's key hiding shows that it otherwise would; Escape ends typing without leaving the page; Ctrl+V pastes once. See SourcesRuntimeChecks/README.md for scope.

7 项检查逐帧模拟实体键盘，经过 SMAPI 真实的输入状态和真实的查询页：打出的字母不进 SMAPI 输入状态（其他 Mod 的快捷键）也不进游戏；结束输入的回车不会再打开第一个结果，并用不隐藏按键的对照证明否则会打开；Esc 结束输入而不离开页面；Ctrl+V 只粘贴一次。边界见 SourcesRuntimeChecks/README.md。

## Assets / 素材

`tools/Optimize-Assets.ps1` (PowerShell 7 on Windows) derives the Collections-page button icon (104x80) and the portrait placeholder (640x490) from `assets/GalleryTabIcon-horizontal-v5.png`, and losslessly re-encodes the shipped PNGs: decoded pixels are compared before writing, and all other PNG chunks are kept. Files with C2PA content credentials (`caBX`) are left byte-for-byte unchanged, because the credential is bound to the file bytes. Use `-Check` to report without writing. The source icon `GalleryTabIcon-horizontal-v5.png` is used only by this tool and is excluded from the package by `IgnoreModFilePaths` in `StardewGallery.csproj`. The album and detail backgrounds have no generator: since 2.10.0 the shipped PNGs are the source (the 2.1.0 generator could no longer reproduce them and was removed; see `docs/archive/GALLERY_SPREAD_ASSETS.md`).

`tools/Optimize-Assets.ps1`（Windows 上的 PowerShell 7）从 `assets/GalleryTabIcon-horizontal-v5.png` 生成收藏页按钮图标（104x80）和肖像占位图（640x490），并对发布的 PNG 做无损重编码：写入前比对解码后的像素，保留其他所有 PNG 数据块。带 C2PA 内容凭证（`caBX`）的文件保持原样，因为凭证绑定了文件字节。加 `-Check` 只报告不写入。源图标 `GalleryTabIcon-horizontal-v5.png` 只供该工具使用，通过 `StardewGallery.csproj` 的 `IgnoreModFilePaths` 排除在发布包外。相册和详情页背景图没有生成器：2.10.0 起以发布的 PNG 为准（2.1.0 的生成脚本已无法重现这些图片，已删除，说明见 `docs/archive/GALLERY_SPREAD_ASSETS.md`）。

```powershell
pwsh tools/Optimize-Assets.ps1
```

### Pinyin table / 拼音表

`assets/pinyin.txt` holds toneless pinyin for CJK Unified Ideographs (U+4E00-U+9FFF) and is generated, not edited by hand. To regenerate it, download `Unihan.zip` from https://www.unicode.org/Public/UCD/latest/ucd/, extract `Unihan_Readings.txt` into its own folder, and pass that path. Readings come from `kMandarin`, `kTGHZ2013` and `kXHC1983`; output is sorted, so the same input gives the same file. Use `-Check` to compare without writing. When the Unicode version changes, update the version in `THIRD-PARTY-NOTICES.md` too. `Checks` reads the committed table and checks matching against it.

`assets/pinyin.txt` 是 CJK 统一汉字（U+4E00–U+9FFF）的无调拼音表，由脚本生成，不手工编辑。重新生成时，从 https://www.unicode.org/Public/UCD/latest/ucd/ 下载 `Unihan.zip`，把 `Unihan_Readings.txt` 解压到单独的文件夹并传入路径。读音取自 `kMandarin`、`kTGHZ2013` 和 `kXHC1983`，输出有序，同样的输入得到同样的文件。加 `-Check` 只比对不写入。Unicode 版本变化时同步更新 `THIRD-PARTY-NOTICES.md` 中的版本。`Checks` 会读取仓库里的拼音表并据此检查匹配。

```powershell
pwsh tools/Generate-PinyinTable.ps1 -Readings "<folder>/Unihan_Readings.txt"
```

## Clean outputs after dependency removal / 依赖清理后的构建

The retired history/preview code and Microsoft.Data.Sqlite/SQLitePCLRaw dependencies are no longer built. Use a clean output directory when switching from 2.8.0, so stale DLLs and native runtime folders are not bundled. Package-Release.ps1 rejects SQLite libraries and database files. Do not remove player config, photos, names or existing historical databases as part of build cleanup.

旧历史／预览及 SQLite 依赖已移除。从 2.8.0 切换时使用干净的构建输出，避免旧 DLL 和原生运行时被带入新包。Package-Release.ps1 会拒绝 SQLite 程序库及数据库文件。构建清理不应删除玩家配置、照片、名称或以前的历史数据库。

The standalone persistence suite now covers active photo/name storage, save identity and backup retention; the removed historical database and preview-injection tests are no longer compiled. Current replay environment types live under Replay/, and current read-only state under Conditions/.

独立存储检查现覆盖照片／名称、存档身份与备份保留，已停止编译废弃历史数据库和预览注入测试。当前回放环境类型在 Replay/，只读状态在 Conditions/。
