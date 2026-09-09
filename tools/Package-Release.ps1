param(
    [Parameter(Mandatory = $true)]
    [string] $Destination
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $repoRoot 'manifest.json') -Raw | ConvertFrom-Json).Version
$builtZip = Join-Path $repoRoot "bin/Release/net6.0/StardewGallery $version.zip"
$outputZip = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
if (Test-Path -LiteralPath $outputZip) { throw "Archive the existing destination first: $outputZip" }

$documents = @('README.md', 'CHANGELOG.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')
$documents += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'licenses') -File | ForEach-Object { 'licenses/' + $_.Name })
foreach ($document in $documents) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $document) -PathType Leaf)) { throw "Missing document: $document" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = [IO.Compression.ZipFile]::OpenRead($builtZip)
try {
    foreach ($entry in $source.Entries) {
        if (-not $entry.FullName.StartsWith('StardewGallery/', [StringComparison]::Ordinal) -or
            $entry.FullName.Contains('..') -or $entry.FullName.Contains('\') -or
            $entry.FullName -match '(?i)(^|/)(config\.json|event-photos|user-data|diagnostics|backups|catalog-latest\.json|\.env)(/|$)' -or
            $entry.FullName -match '(?i)\.(cs|pdb|db|sqlite)$') {
            throw "Unexpected build entry: $($entry.FullName)"
        }
    }
    if (@($source.Entries | Where-Object { $_.FullName -match '^StardewGallery/i18n/[^/]+\.json$' }).Count -ne 12) {
        throw 'The archive must contain all 12 maintained locales.'
    }
    $manifestEntry = $source.GetEntry('StardewGallery/manifest.json')
    if ($null -eq $manifestEntry) { throw 'Missing packaged manifest.' }
    $reader = [IO.StreamReader]::new($manifestEntry.Open())
    try { $packagedVersion = ($reader.ReadToEnd() | ConvertFrom-Json).Version } finally { $reader.Dispose() }
    if ($packagedVersion -ne $version) { throw 'Build archive version does not match source.' }
} finally { $source.Dispose() }

New-Item -ItemType Directory -Path (Split-Path $outputZip -Parent) -Force | Out-Null
Copy-Item -LiteralPath $builtZip -Destination $outputZip
$archive = [IO.Compression.ZipFile]::Open($outputZip, [IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($document in $documents) {
        $entryName = 'StardewGallery/' + $document
        if ($null -ne $archive.GetEntry($entryName)) { throw "Duplicate document entry: $entryName" }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $repoRoot $document), $entryName) | Out-Null
    }
} finally { $archive.Dispose() }

Get-FileHash -LiteralPath $outputZip -Algorithm SHA256
