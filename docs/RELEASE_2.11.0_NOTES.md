# Stardew Gallery 2.11.0

2026-11-18 发布。需求依据：[REQUIREMENTS.md](REQUIREMENTS.md)。

## 中文

2.11.0 将画廊全面重制为原生复古烫金皮质回忆手账，围绕“收集生命力、偶遇线索、单周目分支鉴赏、无感定格留念与大型整合包条件拆解”完成系统升级，同时严格保持 100% 离线只读与回放存档安全边界。

### 核心新特性

- **全流程皮质手账与左下双态三书签**：
  - 村民相册首页、角色个人相册、故事索引、珍藏时刻、剧情详情页、前置事件详情页、剧照管理页与改名面板全部统一承载于 `1672×941` 皮质手账，彻底移除旧版独立大黄框弹窗。
  - 左下悬挂三枚织物燕尾书签（村民相册 / 故事索引 / 珍藏时刻），选中章节呈现凸起铭牌与长垂丝带，未选中章节收短于叠页夹缝；主书页采用左右整页翻书。
- **四季昼夜漫步景窗与防剧透火漆信封**：
  - 角色相册左页配备 6 幅手绘像素季节与昼夜景窗（春日樱昼、盛夏翠林、秋暮红枫、冬日晴雪、静谧星夜、冬夜弯月），随游戏时间自动流转，雨/雪/绿雨飘落轻量像素微粒；角色小人在草地中央平缓四帧漫步并自然驻足面向玩家。
  - 未真实经历的无封面故事展示红/金爱心火漆信封卡面（防剧透隐藏台词与画面）；已解锁故事采用 `心数/阶段 · 地点` 可读标题，并静态提取主角首句对白签注与过场配乐（BGM）名称。
- **双级“可偶遇”线索、清晨提醒与社交页联动**：
  - 自动比对当前存档进度与当日季节、星期、地点天气，点亮**今日可偶遇**（金章 `!`）与**线索就绪**（绿叶章）。
  - 新增可在 GMCM 自由开关的**每日清晨偶遇轻提示**与**原版 `E` 菜单社交页偶遇角标**（点击徽章或指向村民按 `G` 直达个人相册，按返回无缝回到原社交页槽位与焦点）。
  - 故事索引左页 5 项筛选统一为书页内嵌胡桃木羊皮纸下拉浮层，候选项较多时自动附带中英文与拼音搜索框。
- **平行分支（If 线）、分支重选与剧场定格观景**：
  - 同一角色因前序选择互斥而分岔的平行事件，真实经历任一条后自动开放同组平行路线安全鉴赏（不写入真实 `eventsSeen`），且在收集统计中计为同一节点，单周目不卡满收集金星。
  - 回放期间临时内存遮蔽本事件已答 `$q` 选项以便重新体验不同对话分支，详情页支持切换同事件 `fork` 子路线；新增自动淡出控制条与一键**暂停并隐藏对话框定格观景**（默认键盘 `H` / 手柄右摇杆按下）。
- **静默自动留影、婚礼留念卡与珍藏时刻**：
  - 默认开启**静默自动留念**（可在 GMCM 关闭）：正常游玩触发未留影剧情、举行村民婚礼或首次回放无封面故事时，待画面稳定后在世界层静默抓拍首张无 UI 剧照设为初始封面。
  - 新增配偶置顶**婚礼留念卡**（记录结婚年月日，仅作留影珍藏不提供回放）、跨存档喜爱剧情收藏（汇聚于“珍藏时刻”书签页）及桌面端一键打开本地目录。
- **1.6.15 原生 GSQ 扩充、静态来源与 HD Portraits 兼容**：
  - 扩充近 60 项 Stardew Valley 1.6.15 纯只读确定性内置 `GameStateQuery`（含递归 `ANY` / `!ANY` 复合子句）与正向 GSQ 回放环境提取；`a X Y` 触发格条件明确显示 `(X, Y)` 坐标；补齐 `Data/TriggerActions` 与 `fork` / `switchEvent` 静态来源追溯。
  - 新增 `tlitookilakin.HDPortraits`（含元数据尺寸与动态帧肖像）可选只读兼容；修复高 UI 缩放下 `SpriteText` 右页折行飞字问题，同步全部 12 种官方语言（共 623 键）。

### 更新说明

退出游戏并备份原 `Mods/StardewGallery`，将新版解压为干净的 `StardewGallery` 目录后拷回 `config.json`、`event-photos/` 与 `user-data/` 即可无缝继承所有配置、剧照与收藏记录。

---

## English

2.11.0 reimagines Stardew Gallery as a native leather-bound story journal, adding seasonal strolling scenic windows, two-tier encounter clues with morning & Social-tab integration, single-playthrough parallel branch (If-route) appreciation, silent auto-mementos, spouse wedding keepsakes, and expanded Stardew Valley 1.6.15 `GameStateQuery` coverage—while preserving strict offline read-only analysis and snapshot replay safety.

### Highlights

- **Unified Leather Journal & Three Two-State Bookmarks**: All views (Villager Overview, Character Album, Story Index, Treasured Moments, Story Details, Prerequisite Details, Photo Manager, and Rename Dialog) live inside one `1672×941` leather journal with whole-page turning and active-long / inactive-tucked bottom fabric bookmarks.
- **Seasonal Strolling Scenic Windows & Wax-Seal Envelopes**: Six pixel-art seasonal/day-night backdrops with rain/snow/green-rain particles and gentle 4-frame villager meadow strolling; crimson and amber wax-seal envelopes protect unseen stories from spoilers while unlocked stories show `Hearts/Stage · Location` titles, opening quotes, and BGM track names.
- **Encounter Clues, Morning Hints & Social Tab Badges**: Highlights unseen stories that are **Available Today** or **Ready for Timing**, adds in-book parchment filter dropdowns with pinyin search, and provides optional **Morning Encounter Hints** plus clickable **Social Tab Clue Badges** (`G` shortcut jumps straight to a villager's album and restores Social focus on exit).
- **Parallel Branches (If-Routes), `$q`/`fork` Controls & Scenic Freeze**: Experiencing one mutually exclusive route unlocks sibling parallel branches for safe viewing without altering `eventsSeen` (counting sibling groups as one node toward gold-star completion); adds scoped `$q` choice re-prompting, on-page `fork` route switching, auto-fading HUD controls, and a `H` / Right-Stick **Pause & Hide UI** scenic freeze mode.
- **Silent Auto-Memento, Wedding Mementos & Treasured Moments**: Quietly captures a clean initial cover photo during natural play, villager weddings, or coverless replays; pins dated **Wedding Memento** cards in spouse albums, syncs cross-save favorites in **Treasured Moments**, and opens local photo folders on desktop OSes.
- **Expanded 1.6.15 GSQ, Static Sources & HD Portraits**: Supports nearly 60 deterministic read-only 1.6.15 `GameStateQuery` clauses (including recursive `ANY` / `!ANY`), positive GSQ replay environment extraction, explicit `(X, Y)` entry tile display, `Data/TriggerActions` & branch sub-script source attribution, optional **HD Portraits** support, high-zoom `SpriteText` layout fixes, and all 12 official languages (623 keys).

### Updating

Close the game, back up `Mods/StardewGallery`, extract 2.11.0 into a clean `StardewGallery` folder, and copy back `config.json`, `event-photos/`, and `user-data/`.
