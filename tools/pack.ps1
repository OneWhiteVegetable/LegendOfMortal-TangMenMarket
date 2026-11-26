[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$DotnetPath = 'dotnet',
    [string]$GameRoot = '',
    [string]$ManagedPath = '',
    [string]$BepInExCorePath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Get-RelativePath {
    param(
        [string]$Root,
        [string]$FullPath
    )
    $prefix = $Root.TrimEnd([char]92, [char]47) + [char]92
    Assert-Condition ($FullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) "Path is outside its expected root: $FullPath"
    return $FullPath.Substring($prefix.Length).Replace([char]92, [char]47)
}

function Publish-FileAtomic {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )
    if (Test-Path -LiteralPath $DestinationPath -PathType Leaf) {
        $backupPath = "$DestinationPath.$([Guid]::NewGuid().ToString('N')).backup"
        [IO.File]::Replace($SourcePath, $DestinationPath, $backupPath)
        if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
            Remove-Item -LiteralPath $backupPath -Force
        }
    }
    else {
        [IO.File]::Move($SourcePath, $DestinationPath)
    }
}

function Publish-PackageDirectory {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$BackupPath
    )
    Assert-Condition (Test-Path -LiteralPath $SourcePath -PathType Container) "Package staging is missing: $SourcePath"
    Assert-Condition (-not (Test-Path -LiteralPath $DestinationPath) -or
        (Test-Path -LiteralPath $DestinationPath -PathType Container)) "Package destination is not a directory: $DestinationPath"
    Assert-Condition (-not (Test-Path -LiteralPath $BackupPath)) "Package backup path already exists: $BackupPath"

    $hadPrevious = Test-Path -LiteralPath $DestinationPath -PathType Container
    if ($hadPrevious) {
        [IO.Directory]::Move($DestinationPath, $BackupPath)
    }
    try {
        [IO.Directory]::Move($SourcePath, $DestinationPath)
    }
    catch {
        if ($hadPrevious -and
            -not (Test-Path -LiteralPath $DestinationPath) -and
            (Test-Path -LiteralPath $BackupPath -PathType Container)) {
            [IO.Directory]::Move($BackupPath, $DestinationPath)
        }
        throw
    }
    if ($hadPrevious) {
        Remove-Item -LiteralPath $BackupPath -Recurse -Force
    }
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $projectRoot 'XingShangMod.csproj'
$pluginSourcePath = Join-Path $projectRoot 'src/Plugin.cs'
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Runtime'))
$manifestPath = Join-Path $runtimeRoot 'build_manifest.json'
$distRoot = Join-Path $projectRoot 'dist'
$stagingId = [Guid]::NewGuid().ToString('N')
$stagingRoot = Join-Path $distRoot ".pack-$stagingId"
$packageRoot = Join-Path $stagingRoot 'pkg'
$finalPackageRoot = Join-Path $distRoot 'pkg'
$packageBackupRoot = Join-Path $distRoot ".pkg-$stagingId.backup"

Assert-Condition (Test-Path -LiteralPath $projectPath -PathType Leaf) "Missing project: $projectPath"
Assert-Condition (Test-Path -LiteralPath $pluginSourcePath -PathType Leaf) "Missing plugin source: $pluginSourcePath"
Assert-Condition (Test-Path -LiteralPath $runtimeRoot -PathType Container) "Missing runtime directory: $runtimeRoot"
Assert-Condition (Test-Path -LiteralPath $manifestPath -PathType Leaf) "Missing runtime manifest: $manifestPath"

$versionMatch = [regex]::Match(
    (Get-Content -LiteralPath $pluginSourcePath -Raw -Encoding UTF8),
    'PluginVersion\s*=\s*"([^"]+)"')
Assert-Condition $versionMatch.Success 'Could not read PluginVersion from src/Plugin.cs.'
$sourceVersion = $versionMatch.Groups[1].Value
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $sourceVersion
}
Assert-Condition ($Version -match '^[0-9A-Za-z][0-9A-Za-z._-]*$') "Invalid package version: $Version"
Assert-Condition ($Version -eq $sourceVersion) "Package version '$Version' does not match PluginVersion '$sourceVersion'."
$projectDocument = [xml](Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8)
$projectVersionNode = $projectDocument.SelectSingleNode('/Project/PropertyGroup/Version')
Assert-Condition ($null -ne $projectVersionNode) 'XingShangMod.csproj has no Version property.'
$projectVersion = $projectVersionNode.InnerText
Assert-Condition ($Version -eq $projectVersion) "Package version '$Version' does not match csproj Version '$projectVersion'."

$dotnet = Get-Command $DotnetPath -ErrorAction SilentlyContinue
Assert-Condition ($null -ne $dotnet) "dotnet executable not found: $DotnetPath"
$dotnetExecutable = $dotnet.Source
if ([string]::IsNullOrWhiteSpace($dotnetExecutable)) {
    $dotnetExecutable = $dotnet.Path
}

$zipName = "XingShangMod-v$Version.zip"
$zipPath = Join-Path $distRoot $zipName
$hashPath = "$zipPath.sha256"
$temporaryZipPath = Join-Path $distRoot ".$zipName.$stagingId.tmp"
$temporaryHashPath = "$temporaryZipPath.sha256"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

try {
$buildArguments = @('build', $projectPath, '--configuration', 'Release', '--nologo')
if (-not [string]::IsNullOrWhiteSpace($GameRoot)) {
    $buildArguments += "-p:GameRoot=$GameRoot"
}
if (-not [string]::IsNullOrWhiteSpace($ManagedPath)) {
    $buildArguments += "-p:ManagedPath=$ManagedPath"
}
if (-not [string]::IsNullOrWhiteSpace($BepInExCorePath)) {
    $buildArguments += "-p:BepInExCorePath=$BepInExCorePath"
}

Write-Host "Building XingShangMod $Version in Release configuration..."
& $dotnetExecutable @buildArguments
Assert-Condition ($LASTEXITCODE -eq 0) "Release build failed with exit code $LASTEXITCODE."
$dllPath = Join-Path $projectRoot 'bin/Release/XingShangMod.dll'
Assert-Condition (Test-Path -LiteralPath $dllPath -PathType Leaf) "Release build did not produce $dllPath."

Write-Host 'Validating Runtime/build_manifest.json...'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert-Condition ($manifest.schemaVersion -eq 1) 'Unsupported runtime manifest schema.'
Assert-Condition ($null -ne $manifest.files -and $manifest.files.Count -gt 0) 'Runtime manifest has no files.'
Assert-Condition ($manifest.counts.runtimePayloadFiles -eq $manifest.files.Count) 'runtimePayloadFiles does not match the manifest file count.'

$expectedCounts = @{
    items = 54
    itemIcons = 54
    newsPeriodsPerItem = 12
    newsDocuments = 648
    news = 6329
    newsUp = 3175
    newsDown = 3154
    selectionStocks = 6008
    selectionIndices = 11
    stockWeeklyFiles = 6024
    stockDataFiles = 6028
    uiBundles = 1
    uiPrefabs = 15
    uiFonts = 4
}
foreach ($name in $expectedCounts.Keys) {
    Assert-Condition ($manifest.counts.$name -eq $expectedCounts[$name]) "Runtime manifest count mismatch: $name"
}

$checkCount = 0
foreach ($check in $manifest.checks.PSObject.Properties) {
    $checkCount++
    Assert-Condition ($check.Value -eq $true) "Runtime manifest check failed: $($check.Name)"
}
Assert-Condition ($checkCount -gt 0) 'Runtime manifest has no validation checks.'

$expectedFiles = @{}
$forbiddenRuntimeParts = @('bin', 'obj', 'tools', 'gamedll')
foreach ($entry in $manifest.files) {
    $relative = [string]$entry.path
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($relative)) 'Runtime manifest contains an empty path.'
    Assert-Condition ($relative.IndexOf([char]92) -lt 0) "Runtime path must use forward slashes: $relative"
    Assert-Condition (-not [IO.Path]::IsPathRooted($relative)) "Runtime path must be relative: $relative"
    $segments = $relative.Split([char]47)
    foreach ($segment in $segments) {
        Assert-Condition ($segment -ne '' -and $segment -ne '.' -and $segment -ne '..') "Unsafe runtime path: $relative"
        Assert-Condition ($forbiddenRuntimeParts -notcontains $segment.ToLowerInvariant() -and $segment -notmatch '^decompiled') "Forbidden runtime directory in path: $relative"
    }
    Assert-Condition (-not $expectedFiles.ContainsKey($relative)) "Duplicate runtime manifest path: $relative"
    Assert-Condition ($relative -notlike '*_4x.png' -and $relative -notmatch 'demo') "Forbidden runtime artifact: $relative"

    $filePath = [IO.Path]::GetFullPath((Join-Path $runtimeRoot ($relative.Replace([char]47, [char]92))))
    $runtimePrefix = $runtimeRoot.TrimEnd([char]92, [char]47) + [char]92
    Assert-Condition ($filePath.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase)) "Runtime path escapes Runtime: $relative"
    Assert-Condition (Test-Path -LiteralPath $filePath -PathType Leaf) "Runtime file listed in manifest is missing: $relative"
    $file = Get-Item -LiteralPath $filePath
    Assert-Condition ($file.Length -eq [long]$entry.bytes) "Runtime file size mismatch: $relative"
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition ($actualHash -eq ([string]$entry.sha256).ToLowerInvariant()) "Runtime file SHA256 mismatch: $relative"
    $expectedFiles[$relative] = $entry
}

$actualFiles = @{}
foreach ($file in Get-ChildItem -LiteralPath $runtimeRoot -Recurse -File) {
    if ($file.FullName.Equals($manifestPath, [StringComparison]::OrdinalIgnoreCase)) {
        continue
    }
    $relative = Get-RelativePath -Root $runtimeRoot -FullPath $file.FullName
    $actualFiles[$relative] = $file.FullName
    Assert-Condition ($expectedFiles.ContainsKey($relative)) "Unlisted file in Runtime: $relative"
}
Assert-Condition ($actualFiles.Count -eq $expectedFiles.Count) 'Runtime file set differs from build_manifest.json.'
Assert-Condition ($null -ne $manifest.ui -and $null -ne $manifest.documents) 'Runtime manifest has no UI/document declarations.'

$uiBundleRelative = [string]$manifest.ui.bundlePath
$fontLicenseRelative = [string]$manifest.ui.fontLicensePath
$installGuideRelative = [string]$manifest.documents.installGuidePath
$installGuideName = -join ([char[]](0x5B89, 0x88C5, 0x8BF4, 0x660E))
$expectedInstallGuideRelative = 'docs/' + $installGuideName + '.txt'
Assert-Condition ($uiBundleRelative -eq 'bundles/xingshang_ui.bundle') "Unexpected UI bundle path: $uiBundleRelative"
Assert-Condition ($fontLicenseRelative -eq 'licenses/OFL.txt') "Unexpected font license path: $fontLicenseRelative"
Assert-Condition ($installGuideRelative -eq $expectedInstallGuideRelative) "Unexpected install guide path: $installGuideRelative"
foreach ($relative in @($uiBundleRelative, $fontLicenseRelative, $installGuideRelative)) {
    Assert-Condition ($expectedFiles.ContainsKey($relative)) "Required runtime file is not listed: $relative"
}
Assert-Condition (([string]$manifest.ui.bundleSha256) -eq ([string]$expectedFiles[$uiBundleRelative].sha256)) 'UI bundle SHA256 declaration differs from the file manifest.'
Assert-Condition (([string]$manifest.ui.fontLicenseSha256) -eq ([string]$expectedFiles[$fontLicenseRelative].sha256)) 'Font OFL SHA256 declaration differs from the file manifest.'
Assert-Condition (([string]$manifest.documents.installGuideSha256) -eq ([string]$expectedFiles[$installGuideRelative].sha256)) 'Install guide SHA256 declaration differs from the file manifest.'
Assert-Condition (([string]$manifest.ui.bindingSha256) -match '^[0-9a-f]{64}$') 'UI binding SHA256 is missing or invalid.'
Assert-Condition (-not [string]::IsNullOrWhiteSpace([string]$manifest.ui.unityVersion)) 'UI Unity version is missing.'
$expectedUiPrefabs = @(
    'GoodsCard', 'CartEntry', 'InventorySlot', 'RankingRow', 'ChartHost', 'NewsTicker',
    'Toast', 'Tooltip', 'Modal', 'ItemDetail', 'AssetsPage', 'InventoryWindow', 'MarketWindow',
    'FlyToken', 'DealStamp')
$expectedUiFonts = @(
    'NotoSansCJKsc-Regular', 'NotoSansCJKsc-Bold',
    'NotoSerifSC-Regular', 'BarlowCondensed-SemiBoldItalic')
Assert-Condition ($manifest.ui.prefabs.Count -eq $expectedUiPrefabs.Count) 'UI manifest must declare 15 prefabs.'
Assert-Condition (@($manifest.ui.prefabs | Select-Object -Unique).Count -eq $expectedUiPrefabs.Count) 'UI prefab assets must be unique.'
foreach ($asset in $expectedUiPrefabs) {
    Assert-Condition ($manifest.ui.prefabs -contains $asset) "UI prefab asset is missing: $asset"
}
Assert-Condition ($manifest.ui.sprites.Count -gt 0 -and $manifest.ui.sprites.Count -eq $manifest.counts.uiSprites) 'UI sprite declaration count mismatch.'
Assert-Condition (@($manifest.ui.sprites | Select-Object -Unique).Count -eq $manifest.ui.sprites.Count) 'UI sprite assets must be unique.'
Assert-Condition ($manifest.ui.fonts.Count -eq $expectedUiFonts.Count) 'UI manifest must declare regular, bold, serif and display fonts.'
Assert-Condition (@($manifest.ui.fonts | Select-Object -Unique).Count -eq $expectedUiFonts.Count) 'UI font assets must be unique.'
foreach ($asset in $expectedUiFonts) {
    Assert-Condition ($manifest.ui.fonts -contains $asset) "UI font asset is missing: $asset"
}

foreach ($relative in $actualFiles.Keys) {
    Assert-Condition (-not $relative.StartsWith('ui/', [StringComparison]::Ordinal) -and
        -not $relative.StartsWith('items/', [StringComparison]::Ordinal)) "Legacy loose UI/item asset in Runtime: $relative"
    Assert-Condition (-not $relative.EndsWith('.png', [StringComparison]::OrdinalIgnoreCase)) "Runtime PNG must be embedded in the UI bundle: $relative"
}
$bundleHeader = New-Object byte[] 7
$bundleStream = [IO.File]::OpenRead($actualFiles[$uiBundleRelative])
try {
    $headerBytesRead = $bundleStream.Read($bundleHeader, 0, $bundleHeader.Length)
}
finally {
    $bundleStream.Dispose()
}
Assert-Condition ($headerBytesRead -eq 7 -and [Text.Encoding]::ASCII.GetString($bundleHeader) -eq 'UnityFS') 'Runtime UI bundle is not UnityFS.'
$fontLicenseText = Get-Content -LiteralPath $actualFiles[$fontLicenseRelative] -Raw -Encoding UTF8
$requiredFontLicenseMarkers = @(
    'Noto Sans CJK SC Regular and Bold',
    "Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'",
    'Noto Serif SC Regular',
    '(c) 2017-2024 Adobe (http://www.adobe.com/).',
    'Copyright 2012 Google Inc. All Rights Reserved.',
    'Barlow Condensed SemiBold Italic',
    'Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow)')
foreach ($marker in $requiredFontLicenseMarkers) {
    Assert-Condition ($fontLicenseText.Contains($marker)) "Runtime font OFL is missing a family/copyright marker: $marker"
}
Assert-Condition ([regex]::Matches($fontLicenseText, [regex]::Escape('SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007')).Count -eq 3) 'Runtime font OFL must contain three complete OFL 1.1 texts.'
foreach ($section in @('PREAMBLE', 'DEFINITIONS', 'PERMISSION & CONDITIONS', 'TERMINATION', 'DISCLAIMER')) {
    Assert-Condition ([regex]::Matches($fontLicenseText, [regex]::Escape($section)).Count -eq 3) "Runtime font OFL section must occur once per family: $section"
}
Assert-Condition ((Get-Content -LiteralPath $actualFiles[$installGuideRelative] -Raw -Encoding UTF8).Contains('xingshang_ui.bundle')) 'Runtime install guide does not name the UI bundle.'
Write-Host "Validated $($expectedFiles.Count) runtime payload files, UI bundle, OFL and install guide; sizes and SHA256 hashes match."

$pluginsRoot = Join-Path $packageRoot 'BepInEx/plugins'
$modRoot = Join-Path $pluginsRoot 'XingShangMod'
$runtimeDestination = Join-Path $modRoot 'Runtime'
New-Item -ItemType Directory -Path $modRoot -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $pluginsRoot 'XingShangMod.dll')
Copy-Item -LiteralPath $runtimeRoot -Destination $runtimeDestination -Recurse

$allowedDll = 'BepInEx/plugins/XingShangMod.dll'
$allowedRuntimePrefix = 'BepInEx/plugins/XingShangMod/Runtime/'
$forbiddenPackageParts = @('bin', 'obj', 'tools', 'gamedll')
$expectedPackageFiles = @{}
foreach ($file in Get-ChildItem -LiteralPath $packageRoot -Recurse -File) {
    $relative = Get-RelativePath -Root $packageRoot -FullPath $file.FullName
    Assert-Condition ($relative -eq $allowedDll -or $relative.StartsWith($allowedRuntimePrefix, [StringComparison]::Ordinal)) "Unexpected file in package staging: $relative"
    if ($relative.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) {
        Assert-Condition ($relative -eq $allowedDll) "Unexpected DLL in plugin-only package: $relative"
    }
    foreach ($segment in $relative.Split([char]47)) {
        Assert-Condition ($forbiddenPackageParts -notcontains $segment.ToLowerInvariant() -and $segment -notmatch '^decompiled') "Forbidden package path: $relative"
    }
    $expectedPackageFiles[$relative] = $true
}
Assert-Condition ($expectedPackageFiles.Count -eq ($manifest.files.Count + 2)) 'Package staging file count is incorrect.'
Assert-Condition (Test-Path -LiteralPath (Join-Path $runtimeDestination 'build_manifest.json') -PathType Leaf) 'Staged runtime manifest is missing.'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory(
    $packageRoot,
    $temporaryZipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

$archive = [IO.Compression.ZipFile]::OpenRead($temporaryZipPath)
try {
    $zipFiles = @{}
    foreach ($entry in $archive.Entries) {
        $relative = $entry.FullName.Replace([char]92, [char]47)
        if ($relative.EndsWith('/')) {
            continue
        }
        Assert-Condition (-not $zipFiles.ContainsKey($relative)) "Duplicate ZIP entry: $relative"
        Assert-Condition ($expectedPackageFiles.ContainsKey($relative)) "Unexpected ZIP entry: $relative"
        $zipFiles[$relative] = $true
    }
    Assert-Condition ($zipFiles.Count -eq $expectedPackageFiles.Count) 'ZIP file set differs from clean staging.'
}
finally {
    $archive.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $temporaryZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($temporaryHashPath, "$zipHash  $zipName`r`n", [Text.Encoding]::ASCII)

Publish-FileAtomic -SourcePath $temporaryZipPath -DestinationPath $zipPath
Publish-FileAtomic -SourcePath $temporaryHashPath -DestinationPath $hashPath
Publish-PackageDirectory -SourcePath $packageRoot -DestinationPath $finalPackageRoot -BackupPath $packageBackupRoot
$zipFile = Get-Item -LiteralPath $zipPath
Write-Host "Package created: $zipPath"
Write-Host "Package directory refreshed: $finalPackageRoot"
Write-Host "SHA256: $zipHash"
[PSCustomObject]@{
    Version = $Version
    Files = $expectedPackageFiles.Count
    Bytes = $zipFile.Length
    MiB = [Math]::Round($zipFile.Length / 1MB, 2)
    Zip = $zipFile.FullName
    Sha256File = $hashPath
}
}
finally {
    foreach ($temporaryFile in @($temporaryZipPath, $temporaryHashPath)) {
        if (Test-Path -LiteralPath $temporaryFile -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
    if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}
