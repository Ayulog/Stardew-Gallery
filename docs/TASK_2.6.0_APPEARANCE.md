# 2.6.0 角色外观兼容 Delta Task

基线：2.5.0 `c4435f0`。当前开发版：2.6.0-dev.4。Mud及Overgrown／Earthy当前组合已获用户正常反馈；本次恢复修复待实测。

## 当前范围调整

用户已明确取消多人回放和独立美化选择/冲突协调器，不再作为后续待办。Portraiture读取已实现；Overgrown／Earthy记为当前安装组合实测正常，不等同于制作了专用皮肤。当前只做画廊能够负责的读取、异常隔离及显示回退。

dev.2 只补失败路径：当前肖像读取失败时尝试游戏基础肖像，小人缺失时使用可用肖像；资源缺失、已释放或绘制抛错时用自带画廊图标占位。失败后两秒重试或在资产失效后重新准备，避免永久留空及逐帧刷错。占位图同样服从未认识角色的遮罩。

这不保证修复游戏外部美化框架自身的黑屏、缺失服装或错误贴图，也不为被覆盖的美术自动还原历史资源。成功路径继续复用dev.1验证。

## 目标

新增独立角色外观模块，统一首页肖像和相册小人的获取、缩放、缓存及失效。首批覆盖普通 CP 替图、Seasonal Outfits 5450，以及用户提供的 Mud 29979 1.3 包使用的 DDFC/Scale Up Unofficial。保持原有页面尺寸、导航、未认识角色遮罩与无美化时的外观。

## 边界

- 框架按玩家安装情况可选接入，画廊不携带第三方立绘、不更改其他 Mod 文件。
- 不改变事件分类、目录扫描、条件判断、回放权限、存档保护及照片持久化。
- 本次不制作画册皮肤，不改回放环境或模拟框架生命周期事件。dev.3新增Portraiture专用适配，其他边界保持。
- 普通 CP 通过游戏当前外观读取；DDFC 通过公开数据资产取得帧区域；Scale Up 继承框架绘制，必要的尺寸/锚点处理集中在适配器。
- 角色外观模块不引用事件目录/条件/回放模块。页面提供身份与绘制区域，组合入口负责创建及注入。

## 验证

- 构建和针对性外观检查：64/1000 像素帧、CopyFrom、异常/缺失回退、缓存失效、放大小人锚点。
- 隔离 UI 渲染：原版与合成高清素材、缩小窗口、首页与相册/详情入口；保留12语言布局。
- 用户实测：分别启用5450和Mud美化，查看首页肖像、相册小人、换装刷新及回放后返回。框架组合实测前不声明全面兼容，不合并main或发布Release。

参考：项目工作区 `drafts/星露谷画廊/research/20260910-appearance-compatibility.md`。

## 接入依据与限制

- 目标安装版本：DDFC 0.7.5、Scale Up Unofficial 2.7.0、Mud 29979 1.3。框架 DLL 只复制到工作区辅助验证，不进入画廊成品。
- DDFC 的 GetSpeakerDisplayData 会写入框架共享的对话缓存，不适合相册批量探测；改用 `GameContent.Load<object>` 读取其公开字典，再投影公开字段为独立快照。复用资源名与 CopyFrom 协议，不调用条件查询，不改变真实 NPC 或对话状态。
- 按地点/当前外观/可确认海滩贴图/角色/default 选择；CopyFrom 按整个 Portrait 部件回退。不能从公开状态确认的海滩别名、更新版自定义条件，以及框架全局禁用但仍保留旧资源的热切换不声明完整兼容；框架设置变化后可重启游戏确认。
- Scale Up 没有 GetApi，绘制使用其公开静态 `ScalesByAsset`。适配器通过反射读取该公开索引和公开元数据，以匹配实际绘制规则；不读取私有状态、不新加 Harmony 补丁。接口形状失配会回退，无硬依赖。
- 首页固定 96x96 槽位，相册逻辑布局维持不变。缓存最多保留40份外观引用；只借用游戏/框架贴图，不创建高清副本，不销毁借用资源。
- 当前真实 NPC 的资源切换可即时反映；画廊不主动调用 ChooseAppearance 重新求值，也不替 CP 模拟 DayStarted。季节/地点选择以游戏已生效外观为准。

源码依据：[DDFC 数据协议](https://github.com/MangusuPixel/DialogueDisplayFrameworkContinued/blob/main/docs/api.md)、[DDFC 选择及缓存](https://github.com/MangusuPixel/DialogueDisplayFrameworkContinued/blob/main/Framework/DialogueBoxInterface.cs)、[Scale Up 小人绘制](https://github.com/Arborsm/ScaleUpUnofficial/blob/master/ScaleUpUnofficial/HarmonyPatches.SpriteInDetail.cs)。两框架以 GPLv3 开源；本包不捆绑其 DLL 或美术。

## 已完成验证

- Release 构建通过，0警告/0错误；目标游戏1.6.15、SMAPI4.5.2与net6.0保持不变。
- `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --appearance`：51项外观检查通过。
- 工作区已有 StoryUiQa 新增 appearance 模式：9项集成断言通过，包括真实 DDFC 0.7.5 数据类型、Mud CopyFrom 配置、缓存复用、当前肖像切换和真实 Scale Up 2.7.0 Draw 补丁的最终像素边界/居中。
- 12种语言、960宽窗口首页与相册，加上1280宽首页/相册/详情，共29张完整界面截图，29项非空界面检查通过。纹理采用合成色块，不包含第三方立绘美术；截图及报告在工作区 `assets/星露谷画廊/2.6.0-appearance-qa/dev1-final/`。
- 测试夹具对非目标角色仍使用原版尺寸，触发3种预期的尺寸不匹配回退；目标角色无警告。第一次隔离运行缺少框架贴图尺寸缓存，补齐测试夹具后通过，未当作生产故障。
- 所有检查针对本轮外观，事件真值、回放生命周期及持久化未改，不重复旧版完整审计。

## 最小实测

1. 启用 Mud 美化与对应框架，检查首页六位角色立绘完整，相册小人居中且没有越框。
2. 在正常游戏换装/换季后重新打开画廊，确认跟随当前外观；有5450时单独验证该组合。
3. 回放后返回相册，检查小人、肖像、返回位置和封面仍正常。此项只验证返回外观，不代表新增了历史服装重建。

未实测部分保持待验收；本次不合并main、不发布Release。

## dev.2 兜底验证

- 构建0警告/0错误；缺图、失败限频、资源恢复后重试、小人回退肖像、绘制间纹理释放、第三方Draw抛错、日志限频和两路都失败共8项隔离检查通过。
- 首页和相册故障截图检查通过，缺失头像用画廊书本图标占位，其他角色和菜单仍正常。证据：工作区 `assets/星露谷画廊/2.6.0-appearance-qa/dev2-final/`。
- dev.1 已验证的尺寸算法及12语言布局未变，复用既有结果；新故障回退仍待用户实测。本轮未开发GMCM美化选择器或新Mod。

## dev.3 Portraiture Delta

- 独立 `PortraiturePortraits` 适配器识别实际NPC返回的Portraiture包装纹理，通过公开的STexture、Scale和ForcedSourceRectangle取得真实图像和帧区域。按现有卡片尺寸绘制，避免Above Box模式改变画廊中的位置，且不叠加DDFC裁图。
- 动态肖像调用框架公开Tick，遵守其同一update计数防重、暂停和循环规则；逐次检查当前帧及缩放/指定区域变化，防止复用旧帧缓存。
- 不读取或更改TextureLoader、玩家选包配置、NPC preset、对话框位置或其他私有状态；不增加Harmony补丁、不复制GPU图集、不强制安装Portraiture。
- 无效缩放、越界帧、已释放底图及不支持的包装组合采用dev.2的占位与限频重试，不能宣称兼容任意其他高清框架混装。
- 目标协议依据作者仓库固定提交 `94255f11414f349f83fc7cff260bde398a6c298a`，对应作者提供的Portraiture 1.12.1-alpha.20240304测试包。该包仅在工作区隔离验证，没有安装到玩家游戏；不将旧alpha包冒称为全面验证的最新稳定版。
- 验证目标：真实包装类型、真实Draw补丁的Above Box对照、帧区域/选包切换、动画帧刷新、DDFC同时存在时不重复处理、故障兜底，以及现有首页/相册入口。

源码依据：[包装纹理](https://github.com/Platonymous/Stardew-Valley-Mods/blob/94255f11414f349f83fc7cff260bde398a6c298a/Portraiture/ScaledTexture2D.cs)、[动画](https://github.com/Platonymous/Stardew-Valley-Mods/blob/94255f11414f349f83fc7cff260bde398a6c298a/Portraiture/AnimatedTexture2D.cs)、[绘制补丁](https://github.com/Platonymous/Stardew-Valley-Mods/blob/94255f11414f349f83fc7cff260bde398a6c298a/Portraiture/OvSpritebatchNew.cs)。作者Platonymous，GPLv3；画廊包不包含其DLL、源码或DCBurger美术。

### dev.3 验证与实测

- 构建0警告/0错误，57项外观检查通过（含6项新增Portraiture区域规则）。
- 14项隔离集成检查通过：真实作者DLL包装类型及Draw补丁，Above Box未适配时离开卡片的对照、完整帧/指定区域/缩放改变、动态帧与暂停、同一tick不重复推进、肖像替换刷新、DDFC不重复裁图、坏区域非空回退、框架配置和借用纹理所有权保持。
- 首页、肖像作为小人回退和中文960宽窗口共3张完整界面截图通过非空检查并目视核对；详细图案/报告在工作区 `assets/星露谷画廊/2.6.0-appearance-qa/dev3-portraiture/`。布局及其余语言未变，复用此前12语言证据。
- 隔离测试直接提供NPC.Portrait；不等于真实存档的框架选包UI、preset和DCBurger美术已测试。本次本机未发现Portraiture安装，未安装或改写第三方模组。
- 最小实测：在正常游戏已能显示的Portraiture/DCBurger组合中打开画廊，检查肖像完整和位置；打开/关闭Above Box以及在框架中切换肖像包后再打开画廊；若使用动画包，再检查动画刷新。遇到缺图确认画廊仍可操作且有占位。

## dev.4 Delta：缓存与尺寸回退

- 已确认代码缺陷：DDFC在读取成功前写入entries，失败后留下空/部分缓存；外围又将临时fallback记为成功外观。现改为局部快照完整构建后提交，并为元数据/自定义TexturePath失败保留两秒重试；到期先清掉临时外观，避免命中成功缓存跳过恢复。
- Emily/Maru警告来源证据：12:27日志显示SCC加载 `Portraits/Emily_Spring` 和 `Portraits/Maru_Spring`；当前SCC配置仍开启两角色。作者PNG头尺寸分别128x256、128x320，而Mud经CopyFrom给这两角色设置1000x1000的DDFC帧。旧09:41和09:42日志记录两角色尺寸回退，新日志再次记录Emily回退。
- 画廊按当前最终图片处理：仅宽64或128、高为64正整数倍时认定标准肖像，回退到64x64帧并记录一次Debug说明。真正未知布局仍Warn，给出资源名、实际尺寸与所要求的帧，并用占位图/重试，不通过删日志掩盖错误，也不切换玩家美化配置。
- 完整Mud高清图、Portraiture缩放/动画、小人、原页面布局和玩家文本均不改变；不执行第三方条件、不改事件/回放/存档模块。
- 修复前19项针对性检查中11项失败，覆盖元数据/投影/类型/覆盖图恢复及两种季节肖像；修复后使用同一夹具验证。辅助资源均为合成诊断图案，只读取作者PNG尺寸与配置，不复制美术到成品。

### dev.4 验证结果

- 最终构建0警告/0错误，63项外观规则检查通过。
- 同一恢复夹具19项全部通过，修复前的11项失败已消除；截图和 `recovery-report.json` 在工作区 `assets/星露谷画廊/2.6.0-appearance-qa/dev4-final/`。
- 共用缓存的相关回归通过：DDFC/Scale Up 9项、Portraiture 14项、失败兜底8项；三个运行目录分别为 `dev4-framework-regression`、`dev4-portraiture-regression`、`dev4-fallback-regression`。既有入口自动覆盖12语言与首页/相册/详情，未扩展到事件或回放真值审计。
- 每次渲染报告findings均为空。合成图案验证选帧、异常恢复和非空显示，不等同最终用户美术组合在实机已验收。
- 最小实测：正常开启现有Mud/SCC组合查看Emily与Maru，确认能显示角色肖像且不再出现这两个标准贴图尺寸的WARN；返回相册和重新打开正常。实际美化包相互覆盖仍由玩家配置决定。
