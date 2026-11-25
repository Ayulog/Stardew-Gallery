# Stardew Gallery 2.12.0

2026-11-25 发布。需求依据：[REQUIREMENTS.md](REQUIREMENTS.md)。

## 中文

2.12.0 聚焦实机画面质感、剧场立绘对比与大型整合包分卷归类体验：全面重制角色相册左页 `96×68` 四季昼夜原生像素景窗与无压缩 `4x` 原版角色动画，新增回放剧场实时立绘对比（单按切换与长按保持分屏），推出三级模组户口与去壳归并分卷书架、后台控制事件双重折叠，并补齐 1.6 条件可读化与字体缺字防崩保护。

### 核心新特性

- **四季昼夜 `96×68` 原生像素景窗与 `4x` 真实比例角色散步**：
  - 角色相册左窗升级为「春夏秋冬 × 白昼（`<17:00`）/ 黄昏（`17:00–19:50`）/ 星夜（`≥20:00`）」共 12 幅手绘 `96×68` 原生像素远山草坪地平线底图（与 `16×32` 角色严格 `1:1` 同像素粒度），移除旧伪像素大图与右上角天气预览按钮。
  - 景窗裁剪区内脱离外层书本的小数 `menuScale` 缩放矩阵，按游戏原版 `4x`（`Game1.pixelZoom`，极小视口安全退至 `3x / 2x` 整数倍率）与整像素坐标绘制底图、角色与粒子，彻底消除不同分辨率与 UI 缩放下的五官挤压变形。
  - 直接复用原版 `AnimatedSprite` 行走/站立动画方法、脚底阴影与 `Data/AnimationDescriptions` 安全直立专属休闲动作（非标准贴图与越界帧安全回退第 0 帧静止），并复用原版 `StardewValley.WeatherDebris` 落樱/夏叶/红枫/雪花物理、`4x` `Game1.rainTexture` 下落雨丝与草地水花、1.6 绿雨苔绿孢子及晴朗夏夜萤火虫微光。
- **剧场立绘实时对比（单按切换、长按保持左右分屏）**：
  - 回放对话期间单按快捷键（默认键盘 `V`）或右上角立绘对比按钮全幅切换“美化 / 原版”立绘；长按超过 `220ms` 切入并保持“左原版｜右美化”半屏分割同框对比（金色中轴分割线，松手后保持分屏供从容细看，再次单按退回美化立绘），按住状态直接读取底层硬件输入而不受 SMAPI 输入抑制干扰。
  - 通过受控只读原生 `ContentManager` 直读未打补丁的原版 `.xnb` 立绘（优先匹配季节/海滩变体如 `Portraits/Abigail_Winter`，越界表情帧回退第 0 帧，原创扩展角色无原版立绘时弹出轻提示）；走未补丁原生 `isPortraitBox` 逆向桩判定对话肖像，兼容 Content Patcher、`HD Portraits`、`Portraiture` 与 `DDFC`，零第三方内部类依赖；恢复简洁的 `2x / 4x` 快进“自动继续普通对话”开关（默认关闭，遇选项必停）。
- **三级模组户口、去壳归并与首页动态分卷书架**：
  - 新增 `ModProvenanceService`（原版底包锁定 → `Data/Characters` 角色户口继承 → `DependsOn` 依赖树根包折叠）与 `CleanModName` 去壳归并（剥离 `[CP]`、`[NPC]` 等前导标签与 `(Content Patcher)`、`C#` 等技术后缀，合并同作者子包且绝不吞并独立人物附加包）。
  - 当存档含非原版模组角色时，村民相册首页右页顶部呈现内嵌羊皮纸手账**分卷选择器**（支持中英文与拼音检索，纯原版存档自动隐藏），与故事索引“来源”筛选口径一致。
- **后台控制事件双重折叠**：
  - 自动识别既无对白/消息/提问指令、也无非黑屏可见舞台演出动作的静默后台控制脚本（`IsControllerEvent`），默认折叠隐藏且不计入角色收集进度分母、禁止回放；可在 GMCM 开启 `ShowControllerEvents` 于故事索引中只读查看。
- **1.6 条件可读化与字体防崩保护**：
  - 只读查询 `Data/mail` 区分真实信件（提取 `[#]` 可读信件标题，无标题信件保留 ID 防剧透）与后台隐藏剧情节点；补齐物品（`ItemRegistry.QualifyItemId`）、制造/烹饪配方、`Data/Buildings` 建筑、`Data/Monsters` 怪物、`Data/Quests` 任务名称解析及 `Custom_` 驼峰地点名美化，第三方自定义 GSQ 自动剥离冗长 `UniqueID` 前缀。
  - 新增 `SpriteFont` 缺字安全降级（缺失字形替换为 `'?'`）防止第三方特殊字符引发渲染崩溃；同步全部 12 种官方语言（共 643 键）。

### 更新说明

退出游戏并备份原 `Mods/StardewGallery`，将新版解压为干净的 `StardewGallery` 目录后拷回 `config.json`、`event-photos/` 与 `user-data/` 即可无缝继承所有配置、剧照与收藏记录。

---

## English

2.12.0 focuses on native visual crispness, real-time theater portrait comparisons, and large-modpack organization: it rebuilds the character album scenic window with 12 native `96×68` pixel-art horizons and unscaled `4x` villager animations, adds tap-to-toggle & hold-to-split portrait comparison during replays, introduces three-tier mod provenance with dynamic volume shelves, folds silent background controller scripts, and enhances readable 1.6 conditions plus missing-glyph font safety.

### Highlights

- **12-State `96×68` Native Pixel Scenic Windows & Unscaled `4x` Villager Strolling**:
  - Replaced legacy scenic cutouts and the weather toggle button with 12 hand-crafted `96×68` native pixel horizon backdrops covering `Spring / Summer / Fall / Winter × Day (<17:00) / Dusk (17:00–19:50) / Night (≥20:00)` at strict `1:1` pixel parity with `16×32` character sprites.
  - Detaches the scenic scissor pass from the outer fractional `menuScale` matrix to render backdrops, villagers, and weather at Stardew's native integer `4x` zoom (`Game1.pixelZoom`, stepping down cleanly to `3x / 2x` on tiny viewports) aligned to whole screen pixels—eliminating squashed faces and uneven pixel widths.
  - Reuses vanilla `AnimatedSprite` walk/stand methods, shadows, and safe upright `Data/AnimationDescriptions` hobby animations (falling back cleanly to frame 0 on non-standard sheets), paired with vanilla `StardewValley.WeatherDebris` petals/leaves/snow, `4x` `Game1.rainTexture` drops and ground splashes, 1.6 Green Rain spores, and summer night fireflies.
- **Real-Time Theater Portrait Comparison (Tap Toggle & Sticky Split)**:
  - During replay dialogue, tap the compare hotkey (`V` by default) or top-right HUD button to toggle full-frame Modded vs. Vanilla portraits, or hold for `> 220ms` to enter sticky side-by-side Split compare (`Vanilla | Modded` with a highlighted divider that stays visible hands-free until tapped again), polling physical hardware input state directly so SMAPI input suppression never cuts holds short.
  - Reads unpatched `.xnb` portraits read-only via a scoped native `ContentManager` (preferring seasonal/beach variants such as `Portraits/Abigail_Winter`, clamping out-of-range frames to 0, and showing a gentle toast for custom NPCs without base assets). Uses the unpatched `isPortraitBox` reverse-patch stub so third-party portrait mods remain compatible without touching any internal mod classes, and restores the clean optional `2x / 4x` **Auto-advance normal dialogue** toggle (off by default, always pausing at choices).
- **Three-Tier Mod Provenance, Name Cleaning & Dynamic Volume Shelf**:
  - Adds `ModProvenanceService` (vanilla lock → `Data/Characters` character-origin inheritance → `DependsOn` root-pack folding) and `CleanModName` normalization (stripping `[CP]`/`[NPC]` prefixes and `(Content Patcher)`/`C#` suffixes while keeping independent character add-ons distinct).
  - When modded villager volumes are present, an embedded parchment **Volume Selector** (with bilingual & pinyin search) appears on the overview spread and stays consistent with Story Index source filters (auto-hidden on pure vanilla saves).
- **Dual-Check Background Controller Event Folding**:
  - Detects silent background controller scripts lacking both dialogue/text commands and visible non-blackout stage actions (`IsControllerEvent`), hiding them by default from collection denominators and blocking replay (optionally viewable read-only in Story Index via GMCM `ShowControllerEvents`).
- **Readable 1.6 Conditions & Missing-Glyph Font Safety**:
  - Queries `Data/mail` read-only to separate real letters (`[#]` titles parsed cleanly without leaking spoilers) from hidden story flags, resolves readable names for items (`ItemRegistry.QualifyItemId`), crafting/cooking recipes, `Data/Buildings`, `Data/Monsters`, `Data/Quests`, and `Custom_` camel-case locations, and trims verbose `UniqueID` prefixes on custom GSQs.
  - Adds `SpriteFont` missing-glyph `'?'` fallback so special third-party characters never crash rendering, and syncs all 12 official languages (643 keys).

### Updating

Close the game, back up `Mods/StardewGallery`, extract 2.12.0 into a clean `StardewGallery` folder, and copy back `config.json`, `event-photos/`, and `user-data/`.
