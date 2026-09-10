# Stardew Gallery 2.6.0

2026-09-10。正式版本，基于已验收的2.6.0-dev.4实现。

## 中文

2.6.0 改善画廊对角色美化的读取和显示，并在缺图或读取失败时提供回退。

- 跟随当前角色肖像与服装；支持DDFC大立绘、Scale Up小人和Portraiture高清/动态肖像的已核查格式。
- 高清肖像完整放入画廊槽位，避免对话框上方立绘模式改变画廊位置；小人按框架最终尺寸与锚点居中。
- 修复临时元数据/图片读取失败后不能恢复的问题；普通季节肖像与大图配置不匹配时使用明确的标准帧规格。
- 缺失或异常外观尝试回退到可用肖像，最终显示画廊占位图；限频记录并重试。

继续使用现有好感相册、独立剧情/前置查询、筛选、自定义名称、截图及封面。婚礼仪式、节日主流程、夜间特殊事件和电影放映不加入目录；多人回放和独立美化协调器不在项目范围内。

需要游戏1.6.15、SMAPI4.5.2或更新兼容版本。GMCM及美化框架均可选；不捆绑第三方模组或立绘。更新前退出游戏并备份，保留config.json、event-photos/和user-data/。本版本不解决美化包之间的覆盖冲突。

验证：63项外观规则、19项缓存恢复、9项DDFC/ScaleUp、14项Portraiture、8项兜底及相关12语言隔离界面检查通过。用户已确认单个美化模组支持正常，Mud与Overgrown/Earthy有正常反馈。多个美化模组互相覆盖的组合请自行实测；不声明所有框架版本或操作系统均已实测。

## English

2.6.0 improves how character cosmetics appear in the gallery and adds fallback handling for missing or unreadable images.

- Follow current NPC portraits and outfits, with reviewed DDFC, Scale Up and Portraiture HD/animated formats.
- Fit portraits and sprites within gallery slots, including Portraiture Above Box mode and Scale Up sprite origins.
- Recover from temporary metadata/image errors and use standard frame dimensions when HD settings no longer match an active native-size portrait.
- Fall back to an available portrait or gallery icon, with throttled diagnostics and retries.

Existing story albums, prerequisite search, filters, personal names, replay photos and covers remain available. Wedding ceremonies, festival systems, special overnight flows and movie screenings are outside the collection scope. Multiplayer replay and cosmetic conflict management are outside this project.

Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or compatible newer versions. GMCM and cosmetic frameworks are optional. Close the game and back up before updating; preserve config.json, event-photos/ and user-data/. No third-party mod or portrait artwork is bundled, and this update does not resolve conflicts between cosmetic packs.

Focused logic and isolated rendering checks passed. Tested individual cosmetic mods have received user acceptance, with positive feedback for Mud and Overgrown/Earthy. Combinations of overlapping cosmetic mods require your own testing; this is not a guarantee for every framework version or operating system.
