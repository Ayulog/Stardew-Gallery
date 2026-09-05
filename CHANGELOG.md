# Changelog / 更新日志

## 2.0.2 — Condition Details / 条件详情

- Event cards now use the existing ConditionIR parser instead of the legacy condition whitelist. / 事件卡片改用现有 ConditionIR 解析器，不再使用旧条件白名单。
- Conditions now show met, missing, or unknown state, plus current/required values where available. / 条件会显示满足、缺失或无法判断状态，并在可用时显示当前值与要求值。
- Unsupported mod conditions preserve their original text instead of displaying a generic “other condition” label. / 不支持的 Mod 条件会保留原文，不再笼统显示“其他条件”。

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
