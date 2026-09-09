# Third-party notices / 第三方说明

- Stardew Valley is created by ConcernedApe. This project is an unofficial fan-made mod and is not affiliated with or endorsed by ConcernedApe.
- SMAPI is required at runtime.
- Generic Mod Config Menu is an optional runtime integration.
- Harmony is supplied by SMAPI and is not bundled as a separate library in this mod package.
- The release includes project-created UI and promotional assets. No code or assets from other user-created mods are redistributed.

- 《星露谷物语》由 ConcernedApe 创作。本项目是非官方玩家 Mod，与 ConcernedApe 无隶属或背书关系。
- 运行时必须安装 SMAPI。
- Generic Mod Config Menu 是可选的运行时集成。
- Harmony 由 SMAPI 提供，本 Mod 发布包不单独捆绑该库。
- 发布内容包含本项目制作的 UI 与宣传素材，不重新分发其他玩家 Mod 的代码或素材。

## Bundled libraries / 随包依赖

These existing runtime dependencies are distributed unmodified. Their licenses are included in `licenses/` in the source tree and release archive. / 以下既有运行时依赖未经修改，许可文本随源码和安装包放在 `licenses/` 内。

The upstream SQLitePCLRaw notice is preserved in full and also describes optional providers. This package uses e_sqlite3; it does not include SQLCipher or OpenSSL. / SQLitePCLRaw上游声明全文保留，其中还介绍了可选提供程序。本包使用e_sqlite3，不包含SQLCipher或OpenSSL。

| Component / 组件 | Version / 版本 | Copyright / 版权 | License / 许可 |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite | 8.0.10 | .NET Foundation and Contributors; Microsoft Corporation | MIT, `licenses/Microsoft.Data.Sqlite-LICENSE.txt` |
| SQLitePCLRaw.core, bundle_e_sqlite3, provider.e_sqlite3, lib.e_sqlite3 | 2.1.6 | Copyright 2014-2023 SourceGear, LLC | Apache-2.0, `licenses/SQLitePCLRaw-LICENSE.txt` and `licenses/SQLitePCLRaw-NOTICE.txt` |
| SQLite native engine / 原生引擎 | Supplied by SQLitePCLRaw.lib.e_sqlite3 2.1.6 | SQLite authors / SQLite 作者 | Public domain / 公有领域 |

Sources / 来源:

- Microsoft.Data.Sqlite: https://github.com/dotnet/efcore/tree/4315fa43c9573671f8f5be21497d59f9c99cd829
- SQLitePCLRaw: https://github.com/ericsink/SQLitePCL.raw/tree/v2.1.6
- SQLite public-domain dedication: https://www.sqlite.org/copyright.html

Stardew Gallery source for this release is available at https://github.com/Ayulog/Stardew-Gallery/tree/v2.5.0 under the included GNU GPL v3.0. / 本版本星露谷画廊源码可由上述版本标签取得，项目使用随包 GNU GPL v3.0 许可。

