# Stardew Gallery 事件画廊可行性调研

日期：2026-10-15
项目：`Ayulog/Stardew-Gallery`
调研目标：评估事件来源识别、事件收录筛选、回放外观选择、原生回放，以及“游戏启动后、进入存档前选择模组”的可行性。

## 结论先行

### 可以做的部分

- 可以为每个事件建立多层来源证据：原版资产、Content Patcher 文件、AliveNpcs/场景文件、True Rune JSON、已知公开 API 的 C# 框架，以及运行时实际观察到的资源修改。
- 可以让玩家按 Mod ID、事件类别、来源证据状态和事件包配置画廊收录范围。
- 可以读取当前已加载的 HD Portraits、Event Extensions 等公开 API，让回放使用当前游戏中的角色外观、肖像、演员、相机、天气和光照能力。
- 可以提供“当前生效外观、原版外观、已知兼容 API、回退资源”等回放策略。
- 可以在标题菜单或存档选择界面选择 Gallery Profile，并将选择写成待启动配置。

### 当前进程不能做的部分

- SMAPI 进程已经启动后，不能安全地动态启用一个原来未加载的 C# 模组。
- 不能在同一进程中完整禁用已加载 C# 模组并撤销其 Harmony 补丁、事件订阅、缓存和外部状态。
- 不能把存档界面选择直接变成当前游戏进程的完整模组切换。

### 推荐方向

推荐采用“游戏内选择 + 外部重启 SMAPI”的 Profile 路线，同时先实现不依赖重启的来源证据、收录筛选、Event Extensions 和 HD Portraits 适配。用户在存档选择界面选定 Profile 后，Gallery 写入待启动配置，提示退出并由外部启动器使用对应的 `--mods-path` 或 `SMAPI_MODS_PATH` 启动新进程。这样能满足按存档选择画廊规则，同时遵守 SMAPI 的加载边界。

本报告是调研结果，不是开发承诺。当前未修改生产 C# 源码、未安装新模组、未改变玩家存档，也未声称完成游戏内 UI 验收。

## 1. 调研范围与环境

本轮使用本机已安装的模组程序集、用户提供的 ZIP 包、Stardew Valley 和 SMAPI 的托管程序集进行静态检查。研究对象包括事件查询/编辑/回放框架、事件内容包、肖像与外观模组，以及 Stardrop 的 Profile 启动方式。

已确认的基线：

| 项目 | 版本/状态 |
| --- | --- |
| Stardew Valley | 1.6.15.24356 |
| SMAPI | 4.5.2.0 |
| Stardew Gallery | 当前工作区版本，已有 2.8/2.9 调研资料 |
| 研究方式 | 公开源码比对、托管 DLL 反编译、ZIP 静态清单和 manifest 检查 |

REA 证据只用于支持本报告的技术判断。关键证据包括：Event Extensions 的程序集/成员证据 `ev_fc9b6a0f7a3e4e1a41d903a1922ec27795306770435481bf8373cee0c52b6cc3`、True Rune Events 的成员证据 `ev_1c1d00d9e735abf81725b6d02b8157ee0a46827a9e7612d693f384134b0aa97e`、HD Portraits 的程序集证据 `ev_6ddd944768c3676bf58299fe4ccb056c434dec07af453b7e5f34692d115e9698`、Extra Event Commands 的程序集证据 `ev_7bb5dc87c10cad5e10327123f9aa5a9a7b286a043463028278f94401328170d4`。证据 ID 是调研索引，不代表第三方作者的源码授权或完整作者证明。

## 2. ZIP 与已安装模组清单分类

用户说明大部分模组已经放入 Mods 目录，因此本轮以已加载程序集为主，并对缺失项直接读取 ZIP。ZIP 与已安装清单的比较显示，列出的事件、肖像、外观和工具模组大多已有安装版本；缺失项或不能作为普通 SMAPI 模组加载的包单独处理如下。

### 事件定义和事件工具

- Event Extensions：已分析，提供事件预览 API、演员注册、相机、天气、光照、外观快照和场景会话能力。
- True Rune: Events：已分析，JSON 是事件定义的明确来源，支持事件注册、注入、完成监视和自己的 reload 命令。
- Event Lookup、Event Browser：已分析，适合借鉴事件索引、NPC 查询、缓存刷新和来源候选展示；它们的 `InferModId` 或类似逻辑属于推断，不能当作 C# 来源证明。
- Event Repeater、Event Archive、Event Tester、Events Editor、CutsceneMaker、EventBrowser、Map Event Markers、Event Indicators、Event Limiter、Unskippable Events、Skip or Socialize、Immersive Event Endings、Sequential Dateables' Heart Events、More Nightly Events、MH Event List：已读取清单或程序集，适合用于事件发现、测试、重复触发、条件或 Content Patcher 内容研究，但没有一个提供通用、可靠的任意 C# 事件作者证明服务。
- Stardew Local API Developer Tools：ZIP 中确认有本地 Web API、事件构建器、事件运行/结束接口和 Content Patcher 刷新入口；它可以帮助测试事件回放，不能动态加载或卸载 C# DLL。

### 内容包和动态事件来源

- Beach Episode：包含 DLL、`alive-npcs-ec.json`、场景目录和 festival JSON；场景 catalog 直接记录事件 ID、地点、条件、脚本及完成标记，适合文件级来源索引。
- Mobile Phone：事件 JSON 记录名称、地点、事件 ID、夜间条件和邮件，适合建立定义文件来源。
- Sequential Dateables' Heart Events：纯 Content Patcher 内容，使用 `EditData` 修改 `Data/Events`，可按目标资产、entry key、操作顺序和脚本指纹追踪。
- True Rune JSON：事件 JSON 和注入结果可同时记录 owner Mod ID、JSON 路径、event ID、variant 和 numeric ID。
- More Nightly Events：ZIP 中的 `events.json` 为空数组，主要逻辑位于 DLL 的运行时逻辑，不能只靠 JSON 扫描归属。

### 肖像、外观和事件演员

- HD Portraits：提供 `GetEventPortraitFor`、`TryGetPortrait`、`GetTextureAndRegion` 等 API，可以作为正式肖像适配器。
- Portraiture：内部有文件夹和预设切换能力，但本轮没有确认稳定公开的 SMAPI API；不能把内部 `TextureLoader` 当成 Gallery 的稳定依赖。
- Extra Event Commands：有 Farmer 旋转、服装/层级、透明度、临时演员、Fashion Sense 外观和事件命令，适合增强演员外观兼容。
- Cave of Memories、HEART ALBUM、Beach Episode 等内容包可以作为事件/场景素材来源，但其内容是否能在当前回放环境复现，需要逐包验证。

### 两个异常或非普通 SMAPI 包

1. `Extra Event Commands-39626-1-0-5-1765133396.zip` 的文件名扩展名是 ZIP，但文件头为 `Rar!`，实际是 RAR5。7-Zip 可以读取，内容包含 `Extra Event Commands.dll` 和 manifest，UniqueID 为 `tsubasamoon.ExtraEventCommands`，版本 1.0.5。它当前不在已安装 Mods 清单中。报告应把它标记为“RAR5 改名为 ZIP”，不能写成损坏包。
2. `Godly Hand Debug Tool` 包是 HTML/JavaScript 调试工具，不是带 manifest 的普通 SMAPI C# 模组，不能作为事件来源或运行时加载对象。

## 3. 目标一：准确识别事件来源，包括 C# 模组

### 结论

可实现“有证据的来源识别”，不能承诺任意 C# 模组和任意历史作者都能被唯一证明。界面和数据模型必须区分 `Complete`、`Partial`、`Unknown`，并把“当前主事件提供者”“本次加载修改者”“运行时观察到的参与者”分开。

### 建议的三层证据模型

#### 第一层：文件来源

记录以下字段：

- Mod ID、显示名和版本；
- 文件相对路径；
- 目标资产，例如 `Data/Events/<Location>`；
- 原始 event key、event ID、variant 和 numeric ID；
- Content Patcher 的 patch 顺序、Include/FromFile 链和脚本 SHA-256；
- AliveNpcs、Beach Episode、Mobile Phone、True Rune 等框架的专用字段。

这层对 Content Patcher `EditData`/`Load`、场景 catalog、True Rune JSON 和明确事件 JSON 的可靠性最高。但扫描文件只能证明“内容包声明过定义”，不能证明当前条件分支已经生效，也不能覆盖所有 C# 运行时生成内容。

#### 第二层：运行时资产观察

在实际资源加载和编辑发生时，记录 `Data/Events` 资产的新增、修改、删除、操作身份、顺序和最终脚本指纹。Content Patcher 的 `OnBehalfOf` 身份优先于执行框架身份。观察到的资源修改应以实际发生为准，不能因菜单打开或存在候选补丁就虚构证据。

当前画廊已有来源追踪研究，明确了 SMAPI 4.5.2 的内部边界：操作集合和编辑委托是内部实现，AssetReady 只能说明最终资产就绪，不能单独还原完整修改链。因此运行时观察要版本锁定、失败关闭，并在不兼容时退回静态来源或 Unknown。

#### 第三层：C# 动态来源适配器

只为有公开接口或可验证登记点的框架编写适配器：

- True Rune Events：读取其 EventRegistry/JsonEventLoader 的 owner、JSON 路径和注册信息；`rune_event_reload` 只重建 True Rune 自己的 registry，不是 SMAPI 重载。
- Event Extensions：使用其公开 Preview API、EventSession、ActorRegistry 和场景对象记录回放支持情况；不能把它当作所有普通事件的通用来源服务。
- 其他自定义 `GameLocation.startEvent`、直接修改字典、私有事件播放器或 Harmony 旁路：没有证据时显示 Unknown；可以记录“观察到某 Mod 参与”，不能声称它是唯一来源。

Event Browser 的 `InferModId`、Event Lookup 的 Mod 推断、NPC 名称、数字事件 ID 前缀和事件指令程序集，都只能作为候选或技术附注，不能取代上述证据。

## 4. 目标二：自定义画廊收录哪些模组事件

### 可行性：高

画廊可以在现有目录层增加收录策略：

- 按 Mod ID 白名单或黑名单；
- 按事件类别：好感、普通剧情、前置事件、节日、夜间、场景/临时事件；
- 按来源证据状态：只收录有文件证据、允许 Partial、隐藏 Unknown；
- 按事件包或地点/NPC；
- 按事件 ID、名称和依赖是否满足；
- 对独立内容包和兼容覆盖包分别处理，保留冲突而不是静默覆盖。

建议把“是否进入画廊”和“是否允许回放”分成两个开关。收录筛选不能改变事件内容、好感、任务、已看记录或解锁条件；它只控制 Gallery 目录和回放入口。

对于动态 C# 事件，只有在运行时登记或实际观察到来源后才能进入“已确认来源”筛选；没有证据的事件可以显示在 Unknown 分组，不能被默认为原版或自动归入某个 Mod。

## 5. 目标三：自定义回放时使用哪些美化模组

### 可行性：部分可行，取决于模组是否已经加载

Gallery 可以在启动时读取当前已加载的公开 API，并提供回放策略：

| 策略 | 行为 | 可行性 |
| --- | --- | --- |
| 当前生效外观 | 使用当前进程已经加载的肖像、角色图层和资源 | 高 |
| 原版外观 | 绕过已知适配器，读取原版资源或 Gallery 缓存 | 高，但需逐框架验证 |
| 已知兼容 API | 使用 HD Portraits、Event Extensions 等公开接口 | 中高 |
| 未加载外观模组 | 运行时临时加载 Portraiture/Fashion Sense 等 C# 模组 | 不可行 |
| 无公开 API 的模组 | 读取已生效资源，或回退占位/当前外观 | 部分可行 |

HD Portraits 的事件肖像接口适合优先实现。Event Extensions 的场景演员、外观快照、相机、天气、光照和时间控制能力适合做“更原生回放”的正式适配器。Portraiture 先读取当前生效资源，不承诺通过内部类型切换预设。Extra Event Commands 可作为后续可选兼容目标，尤其是 Fashion Sense、Farmer sprite、服装和临时演员能力。

“自定义回放时使用哪个美化模组”应解释为选择已加载 API/资源策略，而不是在回放按钮中改变 SMAPI 的模组集合。若用户选择了当前进程中没有加载的外观模组，Gallery 应明确提示需要 Profile 重启，而不是悄悄失败。

## 6. 目标四：整体回放更加原生

### 可行性：中高，但不是所有事件都能自动转换

当前回放应继续使用游戏原生事件入口和保存保护。增强方向如下：

1. 事件来源和当前资产身份分离，避免把查询脚本拼接成新的伪事件。
2. 对 Event Extensions 存在的事件，使用其公开 Preview API、EventSession、SceneActor、CameraController、SceneLighting、SceneWeatherController 和 AppearanceSnapshot。
3. 对 HD Portraits 使用事件肖像 API；无 API 时保留当前资源或原版回退。
4. 保留游戏的事件条件、地点、分支和完成语义；Gallery 的一键解锁只改变画廊权限，不写入玩家的好感、邮件、任务或已看事件。
5. 对 `fork`、`switchEvent`、`switchEventFull`、翻译资源、FarmHouse 全局副本和 Trailer_Big 原生转换建立独立身份映射，无法确认时显示 Partial/Unknown。

Event Extensions 不能替代 Stardew 原生事件播放器，也不能保证普通事件脚本都能直接转换为场景对象。每个框架应有能力检测和安全回退，适配器失败时不得影响基础画廊和玩家存档。

## 7. 游戏启动后、进入存档前自定义模组：边界与路线

### Stardrop 能做什么

Stardrop 的核心是启动前 Profile 管理：准备不同的模组目录，设置 `SMAPI_MODS_PATH` 或 `--mods-path`，然后启动一个新的 SMAPI 进程。SMAPI 会在进程开始时扫描、排序、解析依赖并加载 DLL。

### Gallery 在当前游戏进程中不能做什么

`GameLaunched`、标题菜单、`SaveLoaded` 都发生在 SMAPI 已建立模组集合之后。此时没有公开 API 可以：

- 让未参与本次启动的 C# 模组突然加入依赖图；
- 卸载已运行的 C# 模组；
- 撤销 Harmony 补丁、事件订阅、缓存、线程和外部状态；
- 在不重启进程的情况下切换一整套模组目录。

因此，“游戏已经启动后，在进入存档前自由启用/禁用任意 C# 模组”在 SMAPI 的正常支持范围内不可行。强行反射或清理私有状态会导致补丁残留、重复订阅、资源不一致和存档风险，不能作为 Gallery 的正式方案。

### 可行替代方案：游戏内 Profile 选择 + 重启

推荐流程：

1. SMAPI 启动后，Gallery 在标题菜单或存档选择界面读取可用 Gallery Profile。
2. 用户为当前存档选择事件来源白名单、收录范围、回放外观策略和可选模组集合。
3. Gallery 写入待启动配置，记录存档 ID 与 Profile ID 的绑定。
4. Gallery 明确提示退出游戏；不能在当前进程立即加载/卸载 C# 模组。
5. 外部启动器或用户脚本按 Profile 准备独立 Mods 目录，使用 `--mods-path`/`SMAPI_MODS_PATH` 启动新的 SMAPI 进程。
6. 新进程启动后，Gallery 按存档 ID读取对应画廊规则，建立来源索引并加载可用外观适配器。

这样可以实现“每个存档有不同画廊进度和规则”，但实际模组集合的生效点仍是下一次 SMAPI 启动。路线 2 不是当前进程动态启停，而是“存档界面选择，退出后按 Profile 重启”。

## 8. 可直接利用的研究对象

| 模组/框架 | 可利用点 | 限制 |
| --- | --- | --- |
| Event Extensions | Preview API、演员、相机、天气、光照、外观快照 | 不是通用事件来源服务；需按 API 版本适配 |
| True Rune: Events | owner、JSON、registry、事件注入和完成状态 | reload 只作用于自身 registry |
| HD Portraits | 事件肖像和纹理区域 API | 只能读取当前已加载实现 |
| Portraiture | 当前资源、文件夹/预设概念 | 内部类型不作为稳定 API |
| Extra Event Commands | Farmer 外观、旋转、服装、临时演员、Fashion Sense | 当前缺少安装；包为 RAR5 改名 ZIP |
| Stardew Local API | 事件构建、运行、结束、CP 刷新、测试 | 不是模组加载器；DLL 成员细节仍需运行时原型 |
| Event Lookup/Browser | 查询 UI、NPC 分组、缓存刷新、候选来源 | 推断式来源，不能证明 C# 作者 |
| Stardrop | Profile、独立 Mods 目录、启动参数 | 作用在新进程启动前 |

## 9. 风险和未验证项

- 纯 C# 临时事件、私有框架数据、Harmony 直接改写地点返回值，可能没有可复原的文件来源。
- Content Patcher 的动态 `When`、令牌、Include、FromFile、文本/字段操作和语言分支需要逐项验证；声明存在不等于当前分支生效。
- 游戏原生事件字典没有作者字段；事件经过地点合并、翻译、分支和原生转换后，最终脚本和原始定义不一定一一对应。
- 运行时观察依赖 SMAPI 内部实现，必须锁定 4.5.2 签名并失败关闭；不能把来源观察器异常传递给游戏内容加载。
- Portraiture、Fashion Sense、其他美化模组没有稳定公开 API 时，只能读取当前生效资源或回退，不能保证切换预设。
- Event Extensions 的 Preview API 需要真实游戏内事件覆盖测试，不能仅凭类型存在就承诺所有事件可原生回放。
- Stardew Local API 的 ZIP UI 文案证明了事件构建/运行能力，但本轮未把它作为 Gallery 依赖，也未声称其 API 稳定。
- 本轮没有执行新的游戏 UI、多人、性能和实际 Profile 重启验收。

## 10. 建议的开发阶段

### 阶段 A：来源与收录证据

先完善独立的 `EventSourceIndex` 和来源详情：文件来源、CP/AliveNpcs/True Rune 适配、运行时观察状态、Unknown/Partial 展示，以及按 Mod ID/证据状态筛选。该阶段不需要动态启停模组，风险最低，也能直接验证用户最关心的“事件来自哪个 Mod”。

### 阶段 B：原生回放适配

接入 Event Extensions Preview API 和 HD Portraits；为 Portraiture/Extra Event Commands 保留可选探测器与回退策略。以原版、True Rune、Beach Episode、Mobile Phone 和至少一个纯 C# 事件为测试矩阵，验证自然结束、跳过、启动失败和恢复异常。

### 阶段 C：Gallery Profile

实现 Profile 数据模型、存档绑定、标题/存档选择界面和待启动配置。先只生成配置与重启提示，再与外部启动器或脚本约定 `--mods-path`。不要尝试在当前进程反射卸载/加载其他 C# 模组。

### 阶段 D：真实游戏验收

用独立测试存档验证多 Profile、不同 Mod 集合、不同外观集合、语言切换、资源失效、回放恢复、手柄导航和大规模事件包性能。只有实机确认后，才把 Profile 功能写入正式发布说明。

## 11. 本轮交付与用户决策

本轮已完成 ZIP 与已安装模组的静态/托管程序集调研，并形成本文；没有修改生产源码、安装模组或改变存档。

下一步请在以下方向中选择：

- **A：Profile 路线**：接受“游戏内选择 Profile，退出后重启 SMAPI”作为模组集合切换方案，继续设计 Profile 与外部启动配合。
- **B：当前进程路线**：暂不做动态 Profile，只实现来源识别、收录筛选、Event Extensions/HD Portraits 和原生回放适配。
- **C：分阶段路线（推荐）**：先做 B，验证来源和回放；随后做 A 的待启动配置与重启提示。


