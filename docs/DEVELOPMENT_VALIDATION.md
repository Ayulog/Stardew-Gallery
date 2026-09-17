# 开发环境验证记录

验证日期：2026-09-18。以下结果来自本机实际执行，用于记录开发环境准备情况；游戏功能验收仍按对应版本的测试说明进行。

## 环境

| 项目 | 本次实际版本 |
| --- | --- |
| 系统 | Windows x64 |
| .NET SDK | 8.0.425，由 `global.json` 选择 |
| 检查程序运行时 | Microsoft.NETCore.App 6.0.36 |
| MOD 目标框架 | net6.0 |
| Stardew Valley | 1.6.15 |
| SMAPI | 4.5.2 |
| Harmony | 2.2.2.0，来自 SMAPI 的 `smapi-internal` |
| MOD 版本 | 2.6.0 |

游戏与 SMAPI 来自本机 Steam 安装目录，构建时使用命令行 `GamePath` 指定；工程中未保存本机路径。

## 检查结果

| 检查 | 实际命令 | 结果 |
| --- | --- | --- |
| 逻辑与资源检查 | `dotnet run --project Checks/StardewGallery.Checks.csproj -c Release` | 通过；包括 63 项外观检查、512 种九卡片解锁组合、搜索、查询、剧情权限、条件与本地化等已有检查 |
| 存储检查 | `dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release` | 通过；SQLite、事件照片、事件名称等已有检查使用临时测试数据 |
| 完整 MOD 构建 | `dotnet build StardewGallery.csproj -c Release -p:GamePath="<已确认的游戏目录>"` | 通过，0 个警告、0 个错误；生成 DLL 和构建 ZIP |
| 正式格式打包 | `pwsh -NoProfile -File ./tools/Package-Release.ps1 -Destination ./release/StardewGallery-2.6.0-Nexus.zip` | 通过；版本、12 种语言、玩家数据排除及许可文档校验完成 |

两个独立检查工程显示了 `NETSDK1138` 提示，因为 .NET 6 已结束官方支持；这与完整 MOD 构建的零警告结果分别记录。目标框架沿用当前游戏兼容要求。

## 本地产物

- DLL：`bin/Release/net6.0/StardewGallery.dll`。
- 构建 ZIP：`bin/Release/net6.0/StardewGallery 2.6.0.zip`。
- 含说明与许可的验证包：`release/StardewGallery-2.6.0-Nexus.zip`，25,675,759 字节。
- 验证包 SHA256：`AD2C2539502BC87F01FAEAAE5A2BF723821044199C0B15E79EBC45321A16E88F`。

这些产物由 Git 忽略。本次生成验证包，没有安装到游戏或上传发布。存储照片检查保留的合成样例位于被忽略的 `PersistenceChecks/bin/` 中。

## 自动检查与验收范围

`.github/workflows/checks.yml` 已配置在 main 推送及面向 main 的 PR 上运行两套检查，使用 Windows runner、.NET 8 SDK 和 .NET 6 运行时。工作流已做静态核对；本次记录不代表 GitHub 上已运行成功。

本轮没有进入游戏，因此不包含菜单交互、剧情回放、玩家存档、截图封面和第三方美化 MOD 组合的实机验收。游戏内验证使用测试存档并检查 SMAPI 日志。
