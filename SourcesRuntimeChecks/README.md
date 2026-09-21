# SMAPI runtime source checks

This optional .NET 6 executable links the production source observer and invokes the installed SMAPI 4.5.2 pipeline with in-memory content managers, fake mod identities and real operation delegates. It does not launch the game, write to the game directory, read saves or install the MOD. Build outputs remain under this directory's ignored `bin/` and `obj/` folders; no game assemblies are source artifacts or release contents.

Run from the repository root with .NET SDK 8 and the .NET 6 runtime. Set both the build property and executable argument to your SMAPI-enabled game directory:

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley"
```

The 36 assertions exercise real `LoadExact`, `GetAssetOperations`, `ApplyLoader`, `ApplyEditors`, `AssetDataForObject`, cache access and public `AssetReady` dispatch. They cover exactly-once execution, object-to-dictionary forwarding, accepted replacements, exception mutations, type rollback, cached operation reuse, content-pack delegation, invalidation, incomplete-load cleanup, nested loads, shared dictionary ambiguity, out-of-pipeline mutation detection and collection before `AssetReady`. A deterministic failure fixture nulls an in-memory tracking list to fail after cache insertion; this confirms cleanup of requests which never reach `AssetReady`.

The localized regressions verify zh-CN content-pack delegation, preservation of already-cached evidence at SaveLoaded, rejection after session reset and recovery after natural invalidation. SaveLoaded must not clear evidence: a cache hit does not raise AssetReady again. These are isolated runtime checks, not game/UI acceptance. A real Chinese Town event XNB is read from the supplied game Content directory to validate the RawLoad baseline. Full content packs, arbitrary third-party Harmony combinations and private framework pipelines remain untested. The same-name premature completion test invokes the observer while a real outer load is active; it does not read an XNB through SMAPI's recursive fallback branch.

A separate optional probe demonstrates the local Harmony generic-sharing hazard using this executable's own generic method. It adds no production hook and patches no SMAPI generic method:

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --generic-probe
```

On the verified Harmony 2.2.2 / .NET 6.0.36 installation, patching the `object` specialization changed the dictionary and string calls' `typeof(T)` results to `System.Object`. The production observer uses two non-generic hooks and public events instead.

## Built DLL compatibility / 构建产物加载检查

On Windows, validate the built Gallery DLL through the installed SMAPI 4.5.2 assembly loader before packaging. Run in a fresh process; the loader keeps rewriting enabled and does not ignore incompatibility. It does not call Mod.Entry or launch the game. It also invokes the loaded DLL's JSON normalizer with permissive object/array fixtures.

Windows 本机打包前，可用真实 SMAPI 4.5.2 加载器检查 DLL；重写开启且不忽略兼容错误，不执行模组入口或启动游戏，并验证加载后的宽松 JSON 对象/数组读取。此检查不能复现所有第三方模组先前加载的依赖组合，不代替游戏内验收。

```powershell
dotnet run --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -p:GamePath="C:/Games/Stardew Valley" -- "C:/Games/Stardew Valley" --assembly "bin/Release/net6.0/StardewGallery.dll"
```