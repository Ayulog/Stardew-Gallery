# Stardew Gallery 2.7.0

2026-09-21

## 中文

2.7.0 新增可选的事件来源追踪，在事件详情页直接查看当前剧情来自哪个模组，以及被哪些模组修改。

- 地点下方新增“来源”入口。点击查看主事件提供者、执行框架、按顺序记录的修改和证据状态；再次点击返回条件。
- GMCM 新增“事件来源追踪（重启生效）”。默认关闭，开启并保存后需要重启 SMAPI；未安装 GMCM 时，可在 `config.json` 中设置 `EnableEventSourceDiagnostics=true`。
- 修复读档完成时清空来源记录、导致已加载事件全部显示未知的问题。
- 新增界面和设置文本覆盖现有 12 种语言；来源详情支持独立滚动、长文本及键鼠/手柄导航。

**兼容与范围：**需要 Stardew Valley 1.6.15、SMAPI 4.5.2 或更新兼容版本，目标框架保持 .NET 6。来源追踪目前仅适配 SMAPI 程序集版本 **4.5.2.0**；其他兼容版本仍可使用画廊，来源追踪会停用。GMCM 为可选依赖。来源功能共使用两个非泛型 Harmony 接入点，详情页与设置未增加补丁。

来源追踪仍为实验功能，仅记录当前 `Data/Events` 主事件定义在本次加载中可观察到的来源与修改。缺少或不匹配的证据显示“未知”，不会自动认定为原版；它不代表完整创作历史，也不追踪剧情分支或纯 C# 临时事件。

**更新：**退出游戏后，将发布包中的 `StardewGallery` 文件夹合并到 `Mods` 并覆盖程序文件，保留原有 `config.json`、`event-photos/` 和 `user-data/`。已有设置不会因更新自动开启来源追踪。

**验证：**主项目 Release 构建通过，0 警告、0 错误；全量逻辑、持久化检查及 36 项基于真实 SMAPI 的内存管线/XNB 检查通过。修复后的游戏内来源显示，以及 Content Patcher/SVE 组合仍待实机确认。

## English

2.7.0 adds optional event source tracking, so you can see which mod provides the current story and which mods have changed it directly from event details.

- A source row now appears below the location. Click it to view the main event provider, executing framework, ordered changes and evidence status; click again to return to requirements.
- GMCM adds “Track event sources (restart required)”. It is off by default. Enable it, save, and restart SMAPI. Without GMCM, set `EnableEventSourceDiagnostics=true` in `config.json`.
- Fixed source records being cleared after loading a save, which made already-loaded events appear unknown.
- New interface and setting text is available in all 12 supported languages. Source details support separate scrolling, long text, keyboard/mouse and controller navigation.

**Compatibility and scope:** Requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or a compatible newer version; the mod still targets .NET 6. Source tracking currently supports only SMAPI assembly version **4.5.2.0**. On other compatible versions, the gallery remains available with source tracking disabled. GMCM is optional. Source tracking uses two non-generic Harmony hooks in total; the detail page and setting add no hooks.

Source tracking is experimental. It records observable providers and edits for the current main `Data/Events` definitions during the current load. Missing or mismatched evidence stays “Unknown” and is never assumed to be vanilla. This is not a complete authorship history and does not track story branches or temporary events created entirely in C#.

**Updating:** Close the game, then merge the package's `StardewGallery` folder into `Mods`, replacing program files while preserving `config.json`, `event-photos/` and `user-data/`. Updating does not automatically enable source tracking in your existing settings.

**Validation:** The Release build passed with zero warnings and errors. All logic/persistence checks and 36 checks using real SMAPI in-memory loading/XNB data passed. In-game source display after the fix and Content Patcher/SVE combinations still need in-game confirmation.
