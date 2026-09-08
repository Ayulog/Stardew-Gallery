# Changelog / 更新日志

## 2.3.0 - Event Navigation / 事件导航

- Follow event IDs under prerequisite conditions, with a source picker for ambiguous IDs and a message for unavailable targets. Original condition meanings and replay permissions stay unchanged. / 点击前置条件下的事件 ID 跳转详情；同 ID 多来源时选择目标，缺失时提示。保留原条件含义与回放权限。
- Search by NPC, event ID or location and open exact event results. Back navigation preserves the source view, scroll and focus, including multi-hop references and replay return. / 按角色、事件 ID 或地点搜索并直达具体事件；多级跳转与回放返回保留来源页面、滚动与焦点。
- Fit navigation controls for all 12 maintained languages and support mouse, keyboard and controller input. / 导航控件适配现有 12 种语言，支持鼠标、键盘与手柄操作。

## 2.2.0 - Event Photos / 事件截图

- Capture the current world view during Gallery replay with the camera button, F8 or the controller's left shoulder button; bindings are configurable. Dialogue and HUD are excluded. / 回放时使用相机按钮、F8 或手柄左肩拍摄当前场景，支持改键，截图不含对话框和 HUD。
- Manage photos from the event detail thumbnail: browse, select a cover, restore the default, or archive a photo. Covers update in both album and detail. / 从事件详情缩略图进入截图管理，浏览、选择封面、恢复默认或移除归档；相册和详情同步更新。
- Store photos per save and event outside the game save; preserve previous indexes and removed images, and fall back when files are unavailable. / 按存档和事件独立保存，保留旧索引与移除图片，文件不可用时回退默认封面。

## 2.1.2 - Input and UI Corrections / 输入与界面修正

- Isolate keyboard shortcuts before SMAPI input events while editing search; keep native text input and paste, and filter control characters. / 搜索编辑期间在 SMAPI 分发前隔离键盘快捷键，保留原生文字输入和粘贴，过滤控制字符。
- Enlarge album/detail footer buttons with padded, font-measured labels across all 12 locales. / 放大相册与详情底部按钮，按字体测量和内边距适配 12 种语言。
- Show readable fallback location names, including Jenkins' House, and simplify long negated weekday/season lists to shorter equivalent positive descriptions. / 补齐詹金斯家等地点的可读名称，将较长的星期/季节否定列表改为更短的等价正向描述。
- Preserve character search, scroll and focus through detail/replay return paths; return to the selected event's Details action. / 详情与回放往返保留原人物搜索、滚动及焦点；详情返回选中事件的详情入口。

## 2.1.1 - Search Performance / 搜索性能

- Reuse character search results until the query, catalog, or language comparison changes, avoiding repeated filtering, sorting, and result-list allocation on idle frames. / 搜索文本、目录或语言比较规则变化时才重算人物搜索结果，消除空闲帧的重复筛选、排序及结果列表分配。
- Preserve NPC name and event ID matching, character order, locked-character access, and return positions. Enter uses the latest search text immediately. / 保持 NPC 名称与事件 ID 匹配、人物排序、锁定访问及返回位置；按回车时立即使用最新搜索文本。

## 2.1.0 — Event Album + Event Detail / 事件相册与详情

- Reworked each character's event page into a two-column, three-row thumbnail album with continuous scrolling and a project-owned placeholder image. / 将角色事件页改为两列三行、可连续滚动的缩略图相册，并加入项目自有占位图。
- Kept the character panel identical between album and detail; event cards now show only heart threshold, event ID, thumbnail, replay, and a compact details action. / 相册与详情共用完全一致的角色左页；事件卡只显示心数门槛、事件 ID、缩略图、回放与精简详情入口。
- Detail conditions use variable-height rows and original parchment-style check/cross/unknown pixel icons, retaining the affected NPC for multi-friendship gaps. / 详情采用可变高度条件行与原创羊皮纸风格勾/叉/未知像素图标，保留多角色好感缺口的对应角色。
- Unknown rows keep a lightweight reason without exposing raw/source diagnostics in the main UI; unlocked replay and locked-event protection remain unchanged. / 未知条件仅显示轻量原因，不在主界面堆叠原始/来源诊断；已解锁回放与锁定保护保持不变。
- Added complete album/detail localization for all 12 supported languages. / 为全部 12 种支持语言补齐相册与详情文本。
- Fixed controller navigation from each unlocked card's Details action to its Replay thumbnail, including mixed locked cards and scrolling across rows. / 修复手柄从已解锁卡片详情到同卡回放缩略图的导航，并覆盖锁定混排与跨行滚动。
- Redesigned both event spreads with dedicated background-owned layouts, light Details links, original replay glyphs, and shared brown scrollbars; condition rows no longer stack framed panels. / 两层事件书页改用背景主导的独立布局、轻量详情链接、原创播放标记与统一棕色滚动条，条件行不再叠加厚框。
- Unified card navigation around persistent event/action focus, including lower-row Details, locked fallback, scrolling, and footer return. / 以稳定事件索引与动作焦点统一卡片导航，覆盖下排详情、锁定回退、滚动与页底返回。
- Polished album/detail compositing with true thumbnail openings, restored the shared button and scrollbar styles, and separated natural-language requirements from status icons across all locales. / 以真实透明孔位完善相册与详情融合，恢复统一按钮和滚动条样式，并在全部语言中将自然条件文案与状态图标彻底分离。
- Finalized UI polish with larger footer buttons, one pale scrollbar track across all layers, blank parchment for unused event slots, and friendship values consistently shown in hearts. / 完成最终 UI 打磨：放大页底按钮、三层统一淡色滚动轨道、空事件槽保持羊皮纸，并统一用心数显示好感值。

## 2.0.6 — Stop Historical Collection / 停止历史采集

- Disabled historical replay and natural-event collection at runtime; no new historical occurrences, contexts, legacy snapshots, or SQLite sessions are created. / 已在运行时停用历史回放与自然事件采集，不再创建新的历史 occurrence、context、legacy snapshot 或 SQLite 会话。
- Existing historical SQLite and legacy data are preserved untouched. / 已有历史 SQLite 与 legacy 数据保持原样，不读取、不迁移、不重写或删除。
- Current Gallery replay, save protection, speed controls, and Unlock All persistence remain unchanged. / 当前画廊回放、存档保护、速度控制和“一键解锁全部”持久化保持不变。

## 2.0.5 — Current-State Accuracy / 当前状态准确性

- Empty dating and spouse states are now known negatives instead of unknown data. / 没有约会对象或配偶时现会按已知否定判断，不再显示无法判断。
- Each event's weather conditions are evaluated against its own target location. / 每条事件的天气条件现按其目标地点判断。
- Current variants, characters, and spouse ownership refresh whenever the gallery catalog is requested, while parsed event candidate definitions remain cached. / 每次请求画廊目录都会刷新 current variant、角色和配偶归属，同时继续缓存已解析的事件候选定义。

## 2.0.4 — Safety Boundary & Replay Lifecycle / 安全边界与回放生命周期

- Unified catalog and historical variant checks behind a conservative read-only probe that never executes Random, SendMail, Game State Query, malformed, or custom preconditions. / 目录与历史 variant 判定统一经过保守的只读探测，不执行随机、发信、游戏状态查询、错误语法或自定义条件。
- Current variant selection stops at the first unsafe or failed candidate instead of incorrectly skipping to a later match. / 当前 variant 选择遇到首个无法安全判定或出错的候选即停止，不再错误跳到后续匹配项。
- Replay update, cleanup, fail-safe recovery, and return-to-title teardown now guarantee internal state reset and release the save guard. / 回放更新、清理、故障恢复及返回标题流程现会确保内部状态重置并解除存档保护。
- Fixed Preview scope regressions to exercise partial-apply failure and repeated disposal. / 修正 Preview scope 回归检查，使其真正覆盖部分应用失败与重复释放。

## 2.0.3 — Complete Event Preconditions / 完整事件条件

- Audit corrections: fixed seen-event tokens, full weekday names, exact friendship points, rain descriptions, native time display, gender validation, malformed classification, and item-ID fallback. / 审计修正：已看事件占位符、完整星期名、好感点数精度、降雨文案、原版时间显示、性别校验、无效条件分类及物品 ID 回退。
- Recognize legacy SendMail/x for display only; block its native callback during gallery catalog selection. / 旧版 SendMail/x 仅识别展示；画廊目录选择时阻止调用其原版副作用。

- Added typed parsing and descriptions for all 41 vanilla event preconditions and their legacy aliases. / 为原版全部 41 种事件条件及旧别名加入类型化解析与说明。
- Multi-value conditions now preserve their original all/any semantics, including friendship, seen events, dates, shipping, tiles, and dialogue answers. / 多值条件会保留原本的全满足或任一满足语义，包括好感、已看事件、日期、出货、坐标和对话答案。
- Rainy and sunny conditions now use the game's rain predicate, while custom weather IDs remain exact. / 雨天和晴天条件改用游戏的降雨判定，自定义天气 ID 仍精确匹配。
- Condition vocabulary is complete across all 12 supported languages, with game names resolved only at display time. / 12 种支持语言均补齐条件词汇，游戏名称只在显示阶段解析。

## 2.0.2 — Conditions & Localization / 条件与本地化

- Event cards now use the existing ConditionIR parser instead of the legacy condition whitelist. / 事件卡片改用现有 ConditionIR 解析器，不再使用旧条件白名单。
- Conditions now show met, missing, or unknown state, plus current/required values where available. / 条件会显示满足、缺失或无法判断状态，并在可用时显示当前值与要求值。
- Unsupported mod conditions preserve their original text instead of displaying a generic “other condition” label. / 不支持的 Mod 条件会保留原文，不再笼统显示“其他条件”。
- Added German, Spanish, French, Hungarian, Italian, Japanese, Korean, Brazilian Portuguese, Russian, and Turkish translations. / 新增德语、西班牙语、法语、匈牙利语、意大利语、日语、韩语、巴西葡萄牙语、俄语和土耳其语翻译。

## 2.0.1 — Replay Environment / 回放演出环境

- Events are unlocked for replay only after being seen, or while Unlock All is enabled. / 事件仅在已观看或开启“一键解锁全部”后可回放。
- Removed the player-facing Preview action; unlocked events now use one Replay path. / 移除玩家界面的“预览”操作；已解锁事件统一使用单一回放路径。
- Replay now applies explicit season, time, and supported vanilla weather requirements at the target location, then restores the original environment. / 回放会在目标地点应用事件明确要求的季节、时间及受支持的原版天气，并在结束后恢复原环境。
- Environment setup failures no longer block an otherwise playable event and are logged as warnings; launch and restore failures remain errors. / 演出环境设置失败不再阻止可播放事件，并记录为警告；启动或恢复失败仍记录为错误。

## 2.0.0 — Current-State Gallery / 当前状态画廊

- Added the bilingual current-state event gallery and planning tool. / 加入中英双语“当前事件”图鉴与规划工具。
- Current-state replay is canonical: replays always launch from currently resolved content; historical replay is no longer a product feature. / 当前状态回放成为主路径：回放一律从当前解析内容启动；历史回放不再作为产品功能。
- Added readable condition explanation and progress gaps with truth/unknown separation. / 加入可读条件说明与进度缺口，并区分“满足/未满足/无法安全解析”。
- Added safe preview for supportable conditions (friendship, seen events, mail, season, time) with exact restore. / 加入对可模拟条件的安全预览（好感、看过事件、邮件、季节、时间），并精确恢复。
- Added scoped state injection and hardened restore/failure handling. / 加入受作用域约束的状态注入与更强的恢复/失败处理。
- Preserved save backup, state restoration, and save blocking during replay/preview. / 保留回放/预览期间的存档备份、状态恢复与保存保护。
- Added 1x / 2x / 4x replay speed and optional normal-dialogue auto-advance. / 加入 1x / 2x / 4x 快进和普通对话自动继续。
- Added keyboard, mouse, controller, multi-binding, and chord input support. / 加入键鼠、手柄、多快捷键及组合键支持。
- Added adaptive UI fitting and optional GMCM configuration. / 加入自适应界面缩放和可选 GMCM 配置。
- Added opt-in diagnostics with quiet logs by default. / 加入按需诊断，默认保持简洁日志。
