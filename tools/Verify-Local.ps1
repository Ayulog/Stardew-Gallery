# Runs the local release checks in order: mod build, logic and persistence checks, the SMAPI runtime checks,
# packaging (with Package-Release.ps1's content checks) and SHA256. Windows PowerShell 5.1 or PowerShell 7.
# Requires .NET SDK 8, the .NET 6 runtime and a game folder with SMAPI 4.5.2.
# It reads the game folder only: it never installs the mod, launches the game or touches saves.
# Without -Destination the package goes to a new temporary folder, so release/ is left alone.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $GamePath,
    [string] $Destination
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GamePath).Path
if (-not (Test-Path -LiteralPath (Join-Path $game 'StardewModdingAPI.dll') -PathType Leaf)) {
    throw "SMAPI was not found in $game"
}

function Invoke-Step([string] $Name, [scriptblock] $Action) {
    Write-Host "== $Name" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

Push-Location $root
try {
    Invoke-Step 'Build the mod' { dotnet build StardewGallery.csproj -c Release "-p:GamePath=$game" }
    Invoke-Step 'Logic checks' { dotnet run --project Checks/StardewGallery.Checks.csproj -c Release }
    Invoke-Step 'Persistence checks' { dotnet run --project PersistenceChecks/StardewGallery.PersistenceChecks.csproj -c Release }
    Invoke-Step 'Build the runtime checks' { dotnet build SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release "-p:GamePath=$game" }
    $dll = Join-Path $root 'bin/Release/net6.0/StardewGallery.dll'
    # Each mode loads the built DLL in a fresh process.
    foreach ($mode in @('--assembly', '--replay', '--replay-effects', '--replay-weather', '--replay-speed', '--controller-input', '--keyboard-input', '--gallery-drafts', '--conditions', '--save-data')) {
        Invoke-Step "Runtime checks $mode" { dotnet run --no-build --project SourcesRuntimeChecks/StardewGallery.SourcesRuntimeChecks.csproj -c Release -- $game $mode $dll }
    }
    $version = (Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json).Version
    if (-not $Destination) {
        $folder = Join-Path ([IO.Path]::GetTempPath()) ('StardewGallery-verify-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $Destination = Join-Path $folder "StardewGallery-$version-Nexus.zip"
    }
    Invoke-Step 'Package and hash' { & (Join-Path $PSScriptRoot 'Package-Release.ps1') -Destination $Destination }
    Write-Host "All checks passed for $version. Package: $Destination" -ForegroundColor Green
    Write-Host 'In-game acceptance is still required; see docs/RELEASE_2.10.0_ACCEPTANCE.md.'
}
finally { Pop-Location }
