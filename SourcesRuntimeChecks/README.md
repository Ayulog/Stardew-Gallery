# SMAPI runtime source checks

This optional .NET 6 executable links the production source observer and invokes the installed SMAPI 4.5.2 pipeline with in-memory content managers, fake mod identities and real operation delegates. It does not launch the game, write to the game directory, read saves or install the MOD. Build outputs remain under this directory's ignored `bin/` and `obj/` folders; no game assemblies are source artifacts or release contents.

Run from the repository root with .NET SDK 8 and the .NET 6 runtime. Set both the build property and executable argument to your SMAPI-enabled game directory:

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley"
```

The 36 assertions exercise real `LoadExact`, `GetAssetOperations`, `ApplyLoader`, `ApplyEditors`, `AssetDataForObject`, cache access and public `AssetReady` dispatch. They cover exactly-once execution, object-to-dictionary forwarding, accepted replacements, exception mutations, type rollback, cached operation reuse, content-pack delegation, invalidation, incomplete-load cleanup, nested loads, shared dictionary ambiguity, out-of-pipeline mutation detection and collection before `AssetReady`. A deterministic failure fixture nulls an in-memory tracking list to fail after cache insertion; this confirms cleanup of requests which never reach `AssetReady`.

The localized regressions verify zh-CN content-pack delegation, preservation of already-cached evidence at SaveLoaded, rejection after session reset and recovery after natural invalidation. SaveLoaded must not clear evidence: a cache hit does not raise AssetReady again. These are isolated runtime checks, not game/UI acceptance. A real Chinese Town event XNB is read from the supplied game Content directory to validate the RawLoad baseline. Full content packs, arbitrary third-party Harmony combinations and private framework pipelines remain untested. The same-name premature completion test invokes the observer while a real outer load is active; it does not read an XNB through SMAPI's recursive fallback branch.

A separate optional probe demonstrates the local Harmony generic-sharing hazard using this executable's own generic method. It adds no production hook and patches no SMAPI generic method:

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --generic-probe
```

On the verified Harmony 2.2.2 / .NET 6.0.36 installation, patching the `object` specialization changed the dictionary and string calls' `typeof(T)` results to `System.Object`. The production observer uses two non-generic hooks and public events instead.

## Built DLL compatibility / 构建产物加载检查

On Windows, validate the built Gallery DLL through the installed SMAPI 4.5.2 assembly loader before packaging. Run in a fresh process; the loader keeps rewriting enabled and does not ignore incompatibility. It does not call Mod.Entry or launch the game. It also invokes the loaded DLL's JSON normalizer with permissive object/array fixtures.

Windows 本机打包前，可用真实 SMAPI 4.5.2 加载器检查 DLL；重写开启且不忽略兼容错误，不执行模组入口或启动游戏，并验证加载后的宽松 JSON 对象/数组读取。此检查不能复现所有第三方模组先前加载的依赖组合，不代替游戏内验收。

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --assembly "bin/Release/net6.0/StardewGallery.dll"
```
## Replay snapshot and side effects / 回放快照及副作用

Build the MOD first, then run the `--replay` and `--replay-effects` commands from the replay section of `BUILDING.md`. Each mode loads the supplied production DLL in a fresh process. Neither mode runs Mod.Entry, launches the game, accesses saves or installs files. Verified with game 1.6.15.24356 and SMAPI 4.5.2.
先完整构建 MOD，再按 BUILDING.md 的回放章节执行 `--replay` 和 `--replay-effects`，每个命令使用独立进程加载传入的生产 DLL。均不执行 Mod.Entry、不启动游戏、不访问存档、不安装文件；已验证游戏 1.6.15.24356 及 SMAPI 4.5.2。

- `--replay`: 25 assertions call the real ReplaySnapshot.Capture/RestorePlayer and RuntimeStateReader.Capture. Case-distinct event/mail/dialogue sets survive both initial and repeated restoration; dialogue/recipe/friendship dictionaries preserve independent values and remove replay additions; current-state lookups stay case-sensitive. Real Farmer, NetWorldState and collection constructors are used. The fixture bypasses graphics-dependent Farmer initialization, sprite loading and player-status textures, supplies an empty computed buff state, and creates an uninitialized Game1 shell. It does not exercise item cloning, nonempty quests, position/presentation restoration or the full coordinator lifecycle.
- `--replay-effects`: 7 assertions install real ReplaySaveGuard.Apply and invoke native GrandpaCandles. An owned active replay preserves Farm.grandpaScore and advances CurrentCommand once. Inactive replay and unowned-event controls retain native scoring. The fixture supplies an in-memory farm and fixed world evaluation input, and bypasses texture loading, candle rendering and delayed audio; the native command, candle-count conversion, NetInt write and production protection remain real. Harmony patches are removed in finally.

`--replay` 的 25 项检查覆盖真实捕获、恢复、重复恢复及当前状态大小写语义；替代的是无图形环境所需的玩家初始化与纹理依赖，并提供空增益状态，不覆盖物品克隆、非空任务、位置/画面恢复或完整协调器生命周期。`--replay-effects` 的 7 项检查覆盖真实保护安装及原版蜡烛命令；固定世界评分输入并替代农场定位、渲染与音效外围，保留命令、分数换算和 NetInt 写入。二者均不能代替自然结束、跳过、启动失败、恢复异常的游戏内验收。

## P2 runtime regressions / P2运行时回归

The additional `--replay-weather`, `--replay-speed` and `--gallery-drafts` modes use the same built-DLL and game-directory arguments; see BUILDING.md for full commands. Run each in a fresh process. The harness references local game/MonoGame assemblies solely for these isolated checks; they remain outside the MOD package and ordinary CI.
新增三个入口使用相同的游戏目录和构建DLL参数，完整命令见BUILDING.md；每个入口使用独立进程。检查工程对本机游戏/MonoGame程序集的引用仅用于隔离验证，不进入MOD发布包或普通CI。

- Weather: 36 assertions cover native LocationWeather values and all default-world flags, applying each of Sun/Rain/Storm/Snow/Wind/GreenRain from Sun and GreenRain baselines, restoring and restoring again. The fixture invokes production ApplyWeather/Restore with a captured in-memory scope, bypassing RefreshWeather rendering/audio and scope construction's location discovery. Resolver regression is separately covered by regular Checks; this does not prove live map rendering or a complete replay exit.
- Speed: the fixture invokes real Event.Update, native jump/showFrame command dispatch and the production Harmony patches. It substitutes actor/graphics updates and error UI, checks nested/next-frame scaling and exception cleanup, and covers choices and transition/lifecycle speed constraints. No saved game or rendered cutscene is used.
- Drafts: the fixture exercises production menu construction/rebuild and application refresh logic with in-memory context, bypassing graphics/input infrastructure. It verifies persistence of unsaved state and explicit apply/cancel behavior; visual layout and physical keyboard/controller input remain game acceptance items.

天气检查保留真实天气字段应用和恢复，跳过图形/音效刷新；倍速检查保留原版递归命令及生产补丁，替代人物/图形更新外围；草稿检查保留菜单/应用刷新业务逻辑，替代图形与输入设施。均不启动游戏或访问玩家存档，不替代真实转场、渲染和键鼠/手柄验收。


## GMCM exclusion callbacks / GMCM 名单开关回调

After building the MOD, run `--gmcm-exclusion <mod-dll>` with the same game-directory argument. The 18 assertions invoke the production GMCM registration, Save/Reset callbacks and safe-tick exclusion path. The fixture supplies GMCM/SMAPI boundary proxies, writes only a temporary config, replaces the HTTP transfer and rendered gallery refresh, and fixes the replay-busy predicate. It verifies the registered default-off option, persisted saved choice, no effect from unsaved edits, deferred application, unchanged-setting no-op, asynchronous refresh, disabling, reset and optional-GMCM absence. It does not execute Mod.Entry, launch the game, access saves, install files or validate GMCM rendering and physical input.

构建后可使用 `--gmcm-exclusion <mod-dll>` 入口。18 项检查保留真实注册、保存/重置回调和安全更新业务逻辑；替代的是 GMCM/SMAPI 接口边界、HTTP 请求、图形刷新及回放忙碌状态，只在临时目录写配置。实际 GMCM 页面、键鼠与手柄操作仍需游戏内验收。

## Controller dispatch / 手柄分派

After the MOD build, run `--controller-input <mod-dll>` with the same game-directory argument (full command in BUILDING.md). The 36 assertions use actual detail/tool/photo menu handlers and temporary EventPhotoStore data. They cover snappy on/off, one source/filter toggle per A press, opening an NPC picker without selecting immediately, exactly one archived photo per fresh press, setting a cover without an unintended Back action, and preserved B/X/shoulder/Enter/mouse behavior.

The dispatcher follows the decompiled Stardew Valley 1.6.15.24356 fresh-button order: receiveGamePadButton, native A/X mouse emulation when custom gamepad controls are not implemented, then native key mapping. It does not invoke the complete Game1.updateActiveMenu loop. Graphics, font measurement, layout, cursor positioning, textures, audio and return navigation are substituted; menu actions and persistent photo changes remain real. Physical input, held-button repetition, rendering, resolution and UI scaling remain in-game acceptance items. A pre-fix negative control failed 16 of the 36 assertions (including consequential failures), demonstrating detection of the duplicate activation.

构建后使用 `--controller-input <mod-dll>`，完整命令见 BUILDING.md。36 项检查使用真实详情／工具／照片菜单操作及临时照片数据，覆盖 snappy 开关、A 单次展开、picker 不自动选择、一次归档一张、设封面不误退出，以及 B/X/肩键/Enter/鼠标。

分派依据同版本游戏反编译证据，未运行完整 Game1.updateActiveMenu。图形、字体、布局、光标、纹理、音效及返回导航外围被替代；真实菜单动作和存储仍执行。修复前负对照有 16 项断言失败（含前序状态的连锁失败）；真实手柄、按住重复、渲染和缩放仍需游戏内验证。
