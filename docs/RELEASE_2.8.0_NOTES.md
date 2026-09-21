# Stardew Gallery 2.8.0

2026-09-21

## 中文

2.8.0 自动识别事件的原始来源，新增来源查询与默认开启的名单排除，并修复此前的来源未知和 SMAPI 加载兼容问题。

- **按来源查询**：在事件查询 → 筛选 → 来源选择原版、具体模组、未知或多个候选；也可直接搜索模组名称或 Mod ID。
- **自动识别原始来源**：扫描已加载 Content Patcher 模组与支持的 AliveNpcs 场景，按资源路径＋事件 ID 匹配。事件详情可查看提供者及匹配文件，无需开启 GMCM 追踪。
- **名单排除默认开启**：每次启动、读档异步下载 [AI Mod Exclusion](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion.json?action=raw)。等待期间及下载失败时使用发布包内置名单，不使用历史下载缓存。
- **避免误排**：只按已确认原始来源的完整 Mod ID（或名单明确给出的通配规则）匹配；原版、未知、来源冲突和名单外提供者保留。角色、地点、翻译或修改者不作为额外排除依据。
- **修复来源与加载**：移除被 SMAPI 拦截的内部目录反射，修正不兼容的 JSON 方法引用。移除失效的 Nexus 更新键，保留 GitHub 更新。
- **查询与刷新保护**：名单变化后刷新画廊，阻止隐藏剧情重新出现在前置列表；回放或确认期间延后应用，保留存档进度、照片与自定义名称。

**独立开关：**模组根目录首次运行生成 `ai-mod-exclusion.json`，默认 `{ "Enabled": true }`。改为 `false` 并重新读档或重启，可关闭排除与名单下载。它不在 `config.json` 或 GMCM 中。GMCM 的“追踪事件修改（重启生效）”仍只控制可选的运行时修改记录，默认关闭。

**安装/更新：**需要 Stardew Valley 1.6.15 和 SMAPI 4.5.2 或更新兼容版本。退出游戏，将 ZIP 中的 `StardewGallery` 文件夹合并到 `Mods`，保留 `config.json`、`ai-mod-exclusion.json`、`event-photos/` 和 `user-data/`。目标框架仍为 .NET 6；本版没有新增 Harmony 接入点。

**验证与范围：**构建、全量逻辑/存储检查和真实 SMAPI 加载器检查通过。12 语言各 538 键；本机生产代码隔离审计覆盖 1645 条事件，全部识别来源，名单过滤保留全部 176 条原版及名单外来源。下载失败、超时、旧会话响应和独立开关均有回归检查。实际游戏界面、长名称换行及手柄仍待实机确认。尚未适配的纯 C# 动态事件或自定义令牌可能保持未知；文件匹配不代表完整修改历史。

## English

2.8.0 identifies original event providers automatically, adds source queries and an exclusion list enabled by default, and fixes unknown sources and SMAPI loading compatibility.

- **Query by source:** choose the base game, a specific mod, unknown or ambiguous sources in Event Query → Filters → Source. Search also accepts provider names and Mod IDs.
- **Automatic original providers:** scan loaded Content Patcher definitions and supported AliveNpcs scenes by asset path plus event ID. Event details show providers and matching files without enabling runtime tracing.
- **Exclusion enabled by default:** startup and each save load fetch the [AI Mod Exclusion list](https://stardewmodding.wiki.gg/wiki/AI_Mod_Exclusion.json?action=raw) asynchronously. The bundled release list applies while waiting and on failure; previous downloads are never a fallback.
- **Conservative matching:** only confirmed original provider IDs matching complete ModId entries or explicitly declared wildcard rules are excluded. Vanilla, unknown, ambiguous and unlisted providers remain visible. NPCs, locations, translators and modifying packs do not imply exclusion.
- **Compatibility fixes:** replace blocked internal directory reflection, fix the incompatible JSON method reference, and retain GitHub updates after removing the unavailable Nexus update key.
- **Safe catalog refresh:** update open gallery pages, prevent hidden stories reappearing as prerequisite markers, and defer list changes during replay or confirmation. Save progress, photos and custom names are preserved.

**Separate setting:** the mod creates `ai-mod-exclusion.json` with `{ "Enabled": true }`. Set it to `false` and reload the save or restart to disable both exclusion and downloads. This setting is outside config.json and GMCM. GMCM's optional runtime modification tracking remains off by default.

**Updating:** requires Stardew Valley 1.6.15 and SMAPI 4.5.2 or a compatible newer version. Close the game and merge the ZIP's `StardewGallery` folder into `Mods`, preserving `config.json`, `ai-mod-exclusion.json`, `event-photos/` and `user-data/`. The target remains .NET 6; this version adds no Harmony hooks.

**Validation and limits:** the build, full logic/persistence suites and actual SMAPI assembly-loader checks pass. All 12 locales contain 538 matching keys. An isolated production-code audit identifies all 1645 events in the local snapshot and retains all 176 vanilla events and all unlisted providers during exclusion. Failure, timeout, stale-session and separate-setting regressions pass. In-game UI, long-name wrapping and physical controller input still need confirmation. Unsupported dynamic C# events or custom tokens may remain unknown; file matching is not a complete edit history.
