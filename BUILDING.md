# Building / 构建

## Environment / 环境

- Install a stable .NET 8 SDK. `global.json` selects the latest installed .NET 8 feature band; it does not upgrade the project's `net6.0` target.
  安装正式版 .NET 8 SDK。`global.json` 在已安装的 .NET 8 中选择较新的功能版本，项目目标框架仍为 `net6.0`。
- Install the .NET 6 runtime to execute the two check programs. The .NET 8 SDK alone does not supply that runtime.
  运行两个检查程序还需要 .NET 6 运行时，仅安装 .NET 8 SDK 不包含该运行时。
- A full MOD build needs Stardew Valley 1.6.15 and SMAPI 4.5.2, or later compatible versions. `smapi-internal/0Harmony.dll` must exist under the selected game directory.
  完整 MOD 构建需要《星露谷物语》1.6.15、SMAPI 4.5.2 或更高兼容版本，所选游戏目录中应有 `smapi-internal/0Harmony.dll`。

Run all commands from this repository root. / 以下命令均从本仓库根目录执行。

```powershell
dotnet --version
dotnet --list-runtimes
```

The selected SDK should be `8.0.x`; the runtime list should include `Microsoft.NETCore.App 6.0.x`.

所选 SDK 应为 `8.0.x`，运行时列表应包含 `Microsoft.NETCore.App 6.0.x`。检查程序可能显示 `NETSDK1138`，提示 .NET 6 已结束支持；该提示不代表检查失败，也不应因此自行更改游戏兼容目标。

## Checks / 自动检查

These standalone programs use repository fixtures and temporary data; they do not require the game or SMAPI installation.

两个检查程序使用仓库样例和临时数据，不依赖游戏或 SMAPI 安装：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release
dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release
```

For appearance-only changes: / 只涉及外观逻辑时可运行：

```powershell
dotnet run --project Checks/StardewGallery.Checks.csproj -c Release -- --appearance
```

These are executable checks, not `dotnet test` projects. A nonzero exit code means failure. GitHub Actions runs both suites for pull requests and pushes to `main`; it does not build or deploy the MOD into a game installation.

它们是可执行检查程序，不是 `dotnet test` 工程；非零退出码表示失败。GitHub Actions 在 PR 和 `main` 推送时运行两套检查，不负责游戏目录中的安装或实机验证。

## Build / 完整构建

Replace the example path with your own SMAPI-enabled game directory. Keep machine-specific paths out of tracked project files.

将示例路径替换为已安装 SMAPI 的实际游戏目录，不把本机路径写入受版本管理的工程文件：

```powershell
dotnet build StardewGallery.csproj -c Release -p:GamePath="C:\Games\Stardew Valley"
```

The output DLL and build ZIP are written to `bin/Release/net6.0/`. `EnableModDeploy=false` prevents automatic installation into the game. A successful build still needs in-game testing with a test save and SMAPI logs before release.

DLL 和构建 ZIP 位于 `bin/Release/net6.0/`。`EnableModDeploy=false` 关闭自动安装；构建成功后，正式发布前仍需用测试存档进入游戏验收并检查 SMAPI 日志。

## Package / 打包

After building, package the current manifest version together with the README, changelog and license notices:

构建后，从 manifest 读取当前版本，并附带使用说明、更新日志和许可声明打包：

```powershell
$modVersion = (Get-Content -LiteralPath ./manifest.json -Raw | ConvertFrom-Json).Version
./tools/Package-Release.ps1 -Destination "./release/StardewGallery-$modVersion-Nexus.zip"
```

The destination must not already exist. The packaging script checks the version, all 12 locales, excluded player data and required documents, then reports the ZIP's SHA256. Creating a ZIP does not upload or publish it.

目标文件必须尚不存在。打包脚本会核对版本、12 种语言、玩家数据排除和必需文档，并输出 ZIP 的 SHA256；生成 ZIP 不会上传或发布。

