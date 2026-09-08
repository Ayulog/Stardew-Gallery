# Building / 构建

Requirements: .NET 6 SDK, Stardew Valley 1.6.15, and SMAPI 4.5.2.

要求：.NET 6 SDK、《星露谷物语》1.6.15、SMAPI 4.5.2。

From this directory, run:

```powershell
dotnet build -c Release
```

The ModBuildConfig package creates the distributable ZIP under `bin/Release/net6.0`.

ModBuildConfig 会在 `bin/Release/net6.0` 下生成可发布 ZIP。

For an upload archive including the README, changelog and license notices, run the packaging script after building:

正式上传包需附带使用说明、更新日志和许可声明。构建后运行：

```powershell
./tools/Package-Release.ps1 -Destination ./release/StardewGallery-2.4.0-Nexus.zip
```

The destination must be new; archive an old package before replacing it. / 目标文件必须尚不存在；替换旧包前先留档。

