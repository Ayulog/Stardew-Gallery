# 回放期间使用原版素材：可行性调研

日期：2026-09-21。画廊基线：2.8.0，`f30e6019be8adf7ee460f70d9a7520cdae43936f`。

本次仅阅读仓库、已有反编译资料、作者源码和本机美化包声明。没有实现功能、构建、运行游戏、修改玩家配置、安装或发布。本文件中的方案是设计建议，不能当作已完成兼容验证。

## 结论与建议范围

**可以研究实现“回放期间让原版 NPC 使用游戏自带立绘和小人”；无法用一个通用开关保证任意模组组合的整个画面全部恢复原版。** 原版贴图可独立读取，难点是让事件始终使用它，并正确处理框架绘制、事件换装及结束恢复。

建议首期范围限定为：已确认原版身份、资源和帧布局的 NPC，在回放期间使用原版肖像与小人；保留游戏自带的冬装、泳装等有效变体。其他角色及不支持的特制演出素材保持当前外观，并明确提示未覆盖。该范围宜称“回放使用原版 NPC 外观”，不宜称“完全阻断美化”。

| 素材范围 | 可行性与边界 |
| --- | --- |
| 原版 NPC 的普通立绘、小人和已确认的原版变体 | 有实现基础；需要管理后续换装和原版帧布局，仍待原型及实机验证 |
| 模组新增 NPC | 游戏没有其原版文件；若要使用内容模组最初提供的美术，需要另做素材来源、条件和依赖分析 |
| 原版 NPC 的模组专用服装、表情、额外动画帧 | 不能直接认定与原版图兼容；原版没有对应内容时应保留当前素材或声明该演出不支持 |
| Portraiture、DDFC、Scale Up 等框架效果 | 单纯替换图片不保证有效，需按框架版本处理选图、裁切、缩放及绘制入口 |
| 玩家 Fashion Sense 外观 | 属于独立外观系统，不随 NPC 贴图替换关闭；已核查 7.5.0 接口，绘制屏蔽和清空装备均不等于无状态恢复原版 |
| 地图、建筑、地形、物品图集、UI、特效 | 范围大且常与内容模组共用；不建议与首期 NPC 外观功能一起承诺 |

如果要求所有画面严格原版，可靠方向是在单独的游戏启动配置中仅加载画廊及必要框架、排除美化，并使用未被替换的游戏文件。这适用于原版剧情；保留扩展内容时，新增地图和 NPC 仍必然需要模组素材。此方案需要重新启动，不能等同于当前进程内的临时切换。

## 已核实的关键机制

### 1. 当前回放没有独立资源环境

`EventLauncher.cs:34` 取得当前世界的实际地点，`:42` 用当前脚本和真实玩家创建游戏 `Event`，再传送或在该地点开始事件。`Appearance/CharacterAppearance.cs:46` 的读取和回退用于画廊菜单，修改它不会接管游戏事件的 `NPC.draw` 或对话框。

`ReplaySceneEnvironmentScope.cs:26` 只管理季节、时间和天气。现有 `ReplaySnapshot.cs:64`、`:170` 没有保存 NPC 的 Sprite、Portrait、Appearance 等外观状态。后续实现需要新增独立外观恢复范围，不能把现有存档备份当成 GPU 纹理或第三方缓存备份。

### 2. 抢先加载原版图片不会阻止后面的美化编辑

SMAPI 的 `GameContentManager.LoadExact` 在缓存命中时直接返回；没有命中时先 `ApplyLoader`，然后执行 `ApplyEditors`。Content Patcher 分别登记加载与编辑操作。因此，使用高优先级乃至 Exclusive 的 `AssetRequested.LoadFrom` 只参与选初始素材，**不能阻止后续 `EditImage`**。

在较晚的编辑回调中重置结果，可以覆盖部分 CP 替图，但编辑此前已经发生；它也不是独占的最终回调，不能约束后续编辑、资产完成后的改动或绘制补丁。这一路影响的是共享资产，而非画廊专用副本。

Content Patcher 已检查的公开 API 提供条件、令牌解析和注册，没有按回放调用方建立独立资源视图、或临时暂停任意指定内容包的接口。可提供“画廊正在回放”的自定义令牌，让愿意适配的美化包在 `When` 中关闭；已有内容包不会自动遵守这个新令牌，条件切换也仍是共享资源更新。

依据：SMAPI `src/SMAPI/Framework/ContentManagers/GameContentManager.cs:105`、`:129`、`:231`；CP `ContentPatcher/Framework/PatchManager.cs:523`、`IContentPatcherAPI.cs`。源码目录与版本见下文。

### 3. 缓存刷新不是局部恢复

SMAPI 的 `ContentCoordinator` 会传播失效资源；`CoreAssetPropagator` 遍历内容管理器，对纹理使用 `CopyFromTexture` 更新旧对象。因此，保存旧 `Texture2D` 引用不等于保存旧像素，刷新共享缓存可能影响回放外的 NPC、地图和框架。

`Sources/EventDefinitionSources.cs:128` 已采用普通 MonoGame `ContentManager` 从游戏 Content 读取原版事件文件，避开正常 SMAPI 内容管线。这个方式可作为独立读取原版图的设计依据，但当前实现读取的是数据，**没有验证原版纹理回放**。纹理还涉及图形设备、所有权、释放时机和是否被其他补丁接管。游戏 Content 文件若曾被 XNB 替换，直接读盘也无法恢复官方原件。

不能用 `Game1.content.CreateTemporary()` 代替独立构造：SMAPI `GameContentManager.cs:147` 仍通过相同的 coordinator 创建 SMAPI 管理器，临时管理器不等于原版资源隔离。

建议优先研究独立持有原版纹理、局部作用于回放对象的方案，避免依赖全局缓存切换。仍需处理真实 NPC 引用和后续的贴图重载。

### 4. 演员不全是临时对象，贴图也会中途改变

游戏 1.6.15 的已有反编译证据，文件 `diagnostics/event-source-research/game/StardewValley.Event.decompiled.cs`：

| 方法与行号 | 观察及影响 |
| --- | --- |
| `addActor:5501`，加载处 `:5522` | 普通演员新建 NPC，但贴图依旧来自当前游戏内容管线 |
| `setUpCharacters:5851` | 部分分支直接使用地点中的真实 NPC，不能一律视为可丢弃的克隆 |
| `Speak:178`、`ChangePortrait:2673` | 查不到事件演员时可回退真实 NPC；仅遍历 `Event.actors` 不一定覆盖对话 |
| `ChangeSprite:2570`、`ChangePortrait:2665` | 中途可加载带后缀的新图，或调用 `ChooseAppearance` 重新选择外观 |
| `ReplaceWithClone:2768` | 克隆仍继承现有 Portrait 引用，不能自动得到原版素材 |
| `AddTemporaryActor:2828` | 脚本可指定任意贴图和帧宽高，并重命名演员；不能只凭名字等于原版 NPC 就替换 |
| `Animate:900`、`ExtendSourceRect:2385`、`ShowKissFrame:2812` | 帧号、尺寸和角色数据会决定动作；原版没有模组额外帧时可能错帧或裁切越界 |
| `Event.draw:11298` | 依旧调用演员或自定义事件绘制，第三方绘制补丁仍可能介入 |

因此，一次性换图不能保证全程原版。还需核查当前版本 `NPC.ChooseAppearance`、`AnimatedSprite` 和 `DialogueBox` 的具体实现，才能确定最少且稳定的介入点。本次不声称已确定补丁数量，也不承诺零 Harmony；应优先 SMAPI 公开生命周期与局部引用管理，仅在必要时使用受回放实例约束的少量适配。

### 5. 当前 DSV 组合存在超出替图的内容

最新 SMAPI 日志记录 Content Patcher 2.9.1、DSV Core / DSV CP 6.1.0，以及 Fashion Sense 7.5.0。该次启动未发现 Portraiture、DDFC、Scale Up 被加载；后三者结合历史研究及本次固定提交的作者源码分析，不能视为当前组合实测。

本机 DSV CP `Code/VanillaNPCs/Abigail/Abigail_Extras_Seasonal.json:7` 修改 `Data/Characters` 的 Appearance；`:15` 为事件 27 指定 `Portraits/Abigail_Roleplay`、`Characters/Abigail_Roleplay`，`:37` 再加载对应美术。安装的游戏 Content 下没有这两个同名 XNB。因此，即使恢复普通 `Portraits/Abigail`，事件仍可能选择另一条美化资源路径。

同包 `Code/Marigold/MarigoldEvents.json:5`、`:18` 还修改事件数据，并使用新增临时演员。这说明不能简单把整个包判为纯图片包后关闭。保持当前剧情、只改角色外观，需要有意识地保留内容和特殊演出资源。

画廊 2.8.0 的事件来源扫描识别的是事件定义提供者，不是每张贴图的作者；它也不能证明某包对角色数据或事件的修改只有美术效果，不能直接用作“美化阻断名单”。

### 6. 地图替换可能破坏演出

已有 `StardewValley.GameLocation.decompiled.cs:1941` 的 `loadMap` 会加载地图和图集；`:1686` 的 `ApplyMapOverride` 可加入图集并扩大其布局。扩展地图可能使用原版图集中不存在的 tile 索引。直接换成较小的原图会丢图，连地图一起换则可能改变事件坐标、碰撞与动作目标。`Event.ChangeToTemporaryMap:2885` 还允许中途换图。

不能把“原版地点的名称”视为“当前地图可无损换回原版”的证据。

### 7. 各外观框架需要单独处理

以下是对应作者源码的直接观察，不是当前游戏内验收：

| 框架 / 研究版本 | 为什么只换原图还不够 |
| --- | --- |
| Portraiture 1.12.1-alpha.20240304 | `NPC.Portrait` getter 后置补丁可再次替换返回值；切包还可能写配置并受 NPC preset 影响。没有在检查的版本发现公开暂停 API |
| DDFC 作者源码 0.7.6（历史验证为 0.7.5） | 自己绘制对话框，`Portrait.TexturePath` 可覆盖 NPC 肖像和剧情 overridePortrait；API 有渲染事件及显示数据读取，没有暂停方法，显示数据还带共享缓存 |
| Scale Up Unofficial 2.7.0 | 按 `Texture2D.Name` 匹配缩放索引，在 `SpriteBatch.Draw` 中改裁切与缩放；原图沿用已登记资源名仍可能受影响。无 GetApi 或公开暂停开关 |
| Fashion Sense 7.5.0 | 提供图层绘制 override，但返回 true 只是跳过该层，不能自动补画原版；装备 FS 模型时原版头发/配饰绘制已经被跳过。ClearAppearance 会修改玩家外观状态，需要完整保存和恢复 |

固定源码证据：

- [Portraiture 的 Portrait getter 补丁](https://github.com/Platonymous/Stardew-Valley-Mods/blob/94255f11414f349f83fc7cff260bde398a6c298a/Portraiture/PortraitsFix.cs#L32)；同提交 `TextureLoader.cs:346` 的切包流程在 `:365` 写配置。
- [DDFC 对话框绘制](https://github.com/MangusuPixel/DialogueDisplayFrameworkContinued/blob/35e61ad3ac9a3ff20b009b2e52873ca5aad882e2/Framework/DialogueBoxRenderer.cs#L96)：先取 overridePortrait / speaker.Portrait，再被 TexturePath 指定图片覆盖；同提交 `IDialogueDisplayApi.cs:8`、`Framework/Patches/DialogueBoxPatches.cs:93` 分别为 API 及绘制接管入口。
- [Scale Up 绘制拦截条件](https://github.com/Arborsm/ScaleUpUnofficial/blob/ad150e8dbc2220fec6f578ae9b17615ff4f81551/ScaleUpUnofficial/HarmonyPatches.cs#L213)：按纹理名查询；同文件 `:28` 列出绘制补丁。
- [Fashion Sense 绘制 override](https://github.com/Floogen/FashionSense/blob/39e9e9e13c9f9a78416172e73a229cf8703ddb59/FashionSense/Framework/Managers/DrawManager.cs#L95) 与 [原版玩家绘制拦截](https://github.com/Floogen/FashionSense/blob/39e9e9e13c9f9a78416172e73a229cf8703ddb59/FashionSense/Framework/Patches/Renderer/FarmerRendererPatch.cs#L33)；[ClearAppearance](https://github.com/Floogen/FashionSense/blob/39e9e9e13c9f9a78416172e73a229cf8703ddb59/FashionSense/Framework/Interfaces/API/Api.cs#L357) 会清除配饰或设为 None，同文件 `:288` 的 SetFarmerAppearance 写玩家 modData。该提交对应作者 `v7.5.0`。

本次未取得与当前 6.1.0 对应的 DSV Core C# 源码，不能据此断言它没有 API；上文 DSV 判断仅来自实际内容包声明。不能用改写框架配置、批量卸载 Harmony 补丁或空绘制回调充当通用回放开关。

## 后续验证要求（尚未执行）

如另行进入开发，先验证普通 CP 替图及当前 DSV 组合下的原版 NPC，再逐个适配框架。至少覆盖：普通对话、原版冬装/泳装、中途换装、额外表情帧、临时演员重名、同地图与跨地图回放、连续回放，以及结束后正常游戏对话。

自然结束、跳过、启动失败、回放异常、返回标题和重新读档都要释放自有资源并清除临时状态。若处理过真实 NPC，应恢复其相关引用、尺寸、覆盖标志与偏移；不得 Dispose 游戏或第三方持有的纹理。无法还原的框架组合应说明未覆盖，不能以空白贴图冒充原版。

## 证据基线

- 游戏：1.6.15.24356，托管 PE32+ AMD64。已有 ILSpy 9.1.0.7988 反编译；本次复核安装 DLL SHA256 为 `7F1E5B8E58D2758B78570BA771BBEB03D33522F62188BF6C32EDF0CF626DEAEE`，与旧研究一致。反编译不是官方源码。
- SMAPI：本机 4.5.2.0；作者源码提交 `fa45e7de7214709bb8cf327b831408a25a72a0ac`。CP：本机 2.9.1；作者源码提交 `76565e83ede4bc8b3c293f1c659032ba9c39c213`。关键加载顺序有既有对应 DLL 还原记录。源码保存在被忽略的 `diagnostics/event-source-research/SMAPI/`、`PathoschildMods/`；详见 [事件来源调研](EVENT_MOD_SOURCE_RESEARCH.md)。
- DDFC 0.7.5、Scale Up Unofficial 2.7.0、Portraiture 1.12.1-alpha.20240304（作者提交 `94255f11414f349f83fc7cff260bde398a6c298a`）仅作为历史协议基线，具体绘制与缓存证据见 [2.6 外观研究](TASK_2.6.0_APPEARANCE.md)。其中 Scale Up 无 GetApi，存在绘制补丁；Portraiture 包装 NPC 肖像并干预 Draw；DDFC 另有选图、帧布局与共享对话缓存。
- DSV 本次只读实际 6.1.0 内容包声明；本次新增框架源码核查的版本和固定提交见第 7 节。Fashion Sense 7.5.0 已确认加载，作者同版本 API 和绘制逻辑已检查，尚未实机验证临时外观恢复。

以上证据支持可行性与风险边界，不构成功能实现或游戏内验收。
