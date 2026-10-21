# SMAPI runtime checks

This optional .NET 6 executable loads a built Gallery DLL next to the installed game and SMAPI 4.5.2 assemblies and runs isolated checks against real production code. It does not launch the game, write to the game directory, read saves or install the MOD. Build outputs remain under this directory's ignored `bin/` and `obj/` folders; no game assemblies are source artifacts or release contents.

Run from the repository root with .NET SDK 8 and the .NET 6 runtime. Every mode takes the SMAPI-enabled game directory, a mode and the built mod DLL; build with the same `-p:GamePath`. Available modes: `--assembly`, `--replay`, `--replay-effects`, `--replay-weather`, `--replay-speed`, `--controller-input`, `--keyboard-input`, `--gallery-drafts`, `--conditions`, `--save-data`. `tools/Verify-Local.ps1` runs all of them. Full commands are in `BUILDING.md`.

可选的 .NET 6 检查程序，把构建好的画廊 DLL 与本机游戏、SMAPI 4.5.2 程序集一起加载，针对真实生产代码做隔离检查；不启动游戏、不写游戏目录、不读存档、不安装 MOD。每个入口都需要游戏目录、模式和构建 DLL，完整命令见 `BUILDING.md`。2.10.0 移除了运行时修改追踪和 AI 名单，对应的来源管线、泛型补丁演示和 GMCM 名单检查已一并删除。

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
- Drafts: the fixture exercises production menu construction/rebuild with in-memory context, bypassing graphics/input infrastructure. It verifies persistence of unsaved filter/rename state and explicit apply/cancel behavior; visual layout and physical keyboard/controller input remain game acceptance items.

天气检查保留真实天气字段应用和恢复，跳过图形/音效刷新；倍速检查保留原版递归命令及生产补丁，替代人物/图形更新外围；草稿检查保留菜单重建业务逻辑，替代图形与输入设施。均不启动游戏或访问玩家存档，不替代真实转场、渲染和键鼠/手柄验收。


## Condition parity / 条件判定对拍

`--conditions` runs 75 assertions that call the game's own precondition and GSQ code next to the gallery's production parser, `RuntimeStateReader`, `ConditionStateReader` and evaluator, on one headless Farmer with in-memory weather contexts. It compares event IDs (`Event.SplitPreconditions`), the single `!` prefix, `ArgUtility.UnsplitQuoteAware` query text, multi-season `Season`/`z`, `D` for dating/engaged/married, `R` for a pending roommate, `O`, `w` and `WEATHER Here/Target` at the event location. A pre-fix negative control (the 2.9.0 DLL) fails 31 of them. Graphics-dependent Farmer initialization is skipped; no map, content or save is loaded.

`--conditions` 的 75 项检查在同一个无图形玩家上，同时调用游戏自己的前置条件与 GSQ 代码和画廊的生产解析、状态读取、求值，逐项比对事件 ID、单个 `!`、引号重组、多季节、`D`/`R`/`O`、`w` 及事件地点的 `WEATHER Here/Target`。2.9.0 的 DLL 有 31 项不通过。只跳过依赖图形的玩家初始化，不加载地图、游戏内容或存档。

## Save data / 存档数据

`--save-data` creates the built ModEntry without running Entry, gives it a fake SMAPI data helper, and drives its unlock read and toggle in a headless Game1 with `multiplayerMode` set to remote client and then single player. Six assertions cover: no save-data access and a session-only unlock for a remote farmhand, the original `gallery-state` key for the host, and SMAPI exceptions logged rather than thrown. Split-screen key separation is not exercised (it needs live game instances).

`--save-data` 不执行 Entry，直接创建构建产物中的 ModEntry，接上假的 SMAPI 数据接口，在无图形的 Game1 中分别以远程客户端和单人模式驱动解锁读取与切换。6 项检查覆盖：远程农场工不访问存档数据、解锁只在本次会话；房主使用原有的 `gallery-state` 键；SMAPI 异常只记日志。分屏的键分离需要真实的游戏实例，未覆盖。

## Controller dispatch / 手柄分派

After the MOD build, run `--controller-input <mod-dll>` with the same game-directory argument (full command in BUILDING.md). The 62 assertions use actual detail/tool/photo/query menu handlers and temporary EventPhotoStore data. They cover snappy on/off, one source toggle or filter picker per A press, opening an NPC picker without selecting immediately, exactly one archived photo per fresh press, setting a cover without an unintended Back action, preserved B/X/shoulder/Enter/mouse behavior, Enter in the search box focusing the first result without opening it, and the Steam keyboard. For the Steam keyboard, this process has no Steam client, so selecting a field must fall back to the game's keyboard (the output shows the caught error); the keyboard's closing is then simulated to check that the text replaces the field, Enter waits until A is released, cancelling keeps the field selected, text for a deselected field is dropped, and a keyboard that never closes isn't relied on again. Steam's real keyboard needs Steam Deck or Big Picture mode and is an in-game acceptance item.

The dispatcher follows the decompiled Stardew Valley 1.6.15.24356 fresh-button order: receiveGamePadButton, native A/X mouse emulation when custom gamepad controls are not implemented, then native key mapping. It does not invoke the complete Game1.updateActiveMenu loop. Graphics, font measurement, layout, cursor positioning, textures, audio and return navigation are substituted; menu actions and persistent photo changes remain real. Physical input, held-button repetition, rendering, resolution and UI scaling remain in-game acceptance items. A pre-fix negative control failed 16 of the 36 assertions (including consequential failures), demonstrating detection of the duplicate activation.

构建后使用 `--controller-input <mod-dll>`，完整命令见 BUILDING.md。62 项检查使用真实详情／工具／照片／查询菜单操作及临时照片数据，覆盖 snappy 开关、A 单次展开来源或打开筛选项、picker 不自动选择、一次归档一张、设封面不误退出、B/X/肩键/Enter/鼠标、搜索框回车只选中第一个结果而不进入，以及 Steam 键盘。检查进程没有 Steam 客户端，选中输入框必须回退到游戏键盘（输出里会显示捕获的错误）；随后模拟键盘关闭，检查文字替换输入框、松开 A 之后才按回车、取消后保持选中、已取消选中的输入框不接收文字、从未关闭的键盘不再使用。真正的 Steam 键盘需要 Steam Deck 或大屏幕模式，属于实机验收项。

分派依据同版本游戏反编译证据，未运行完整 Game1.updateActiveMenu。图形、字体、布局、光标、纹理、音效及返回导航外围被替代；真实菜单动作和存储仍执行。修复前负对照有 16 项断言失败（含前序状态的连锁失败）；真实手柄、按住重复、渲染和缩放仍需游戏内验证。

## Keyboard typing / 键盘输入

`--keyboard-input <mod-dll>` types on a simulated physical keyboard, frame by frame, in the order SMAPI 4.5.2 and Stardew Valley 1.6.15 run it (both decompiled): SMAPI's real `SInputState.TrueUpdate` reads the keys the game captured the frame before and derives what mods and the game see; UpdateTicking runs the built `GalleryTyping`; the game then captures this frame's keys, its keyboard dispatcher delivers the typed text (here, `\r` for Enter), and new key presses that SMAPI shows the game go to the real query page. The 7 assertions cover a typed letter that never reaches SMAPI's input state (other mods' keybinds read it) or the game, the same key working as a shortcut once typing ended and it was released, Enter listing the results without also opening the first one, a second Enter opening it, Escape ending typing without leaving the page, and Ctrl+V pasting once with neither key visible. A control run without the key hiding shows the Enter that ended typing opening the first result a frame later. The key states the game captures are set directly rather than read from a device, the cursor tile SMAPI computes after the key states is skipped (it needs a player), and the game's own text delivery is reproduced with the text box's public input method; IME composition and real devices remain in-game acceptance items.

`--keyboard-input <mod-dll>` 按 SMAPI 4.5.2 和游戏 1.6.15 的实际顺序（均已反编译核实）逐帧模拟实体键盘：SMAPI 真实的 `SInputState.TrueUpdate` 读取游戏上一帧捕获的按键，算出 Mod 和游戏看到的状态；UpdateTicking 运行构建产物中的 `GalleryTyping`；随后游戏捕获本帧按键，键盘分发器交付本帧打的文字（回车即 `\r`），SMAPI 交给游戏的新按键再送进真实的查询页。7 项检查覆盖：打出的字母不进 SMAPI 输入状态（其他 Mod 的快捷键读它）也不进游戏；输入结束并松键后同一键恢复为快捷键；回车列出结果而不顺带打开第一个；第二次回车才打开；Esc 结束输入而不离开页面；Ctrl+V 只粘贴一次且两个键都不可见。不隐藏按键的对照中，结束输入的回车会在下一帧打开第一个结果。游戏捕获的按键状态直接设定，不读设备；SMAPI 在按键状态之后计算的光标格需要玩家，已跳过；游戏的文字交付用输入框的公开方法重现。输入法组字和真实设备仍需游戏内验证。
