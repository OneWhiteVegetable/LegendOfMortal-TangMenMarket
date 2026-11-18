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

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $projectRoot 'XingShangMod.csproj'
$pluginSourcePath = Join-Path $projectRoot 'src/Plugin.cs'
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Runtime'))
$manifestPath = Join-Path $runtimeRoot 'build_manifest.json'
$distRoot = Join-Path $projectRoot 'dist'
$packageRoot = Join-Path $distRoot 'pkg'

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

$dotnet = Get-Command $DotnetPath -ErrorAction SilentlyContinue
Assert-Condition ($null -ne $dotnet) "dotnet executable not found: $DotnetPath"
$dotnetExecutable = $dotnet.Source
if ([string]::IsNullOrWhiteSpace($dotnetExecutable)) {
    $dotnetExecutable = $dotnet.Path
}

$zipName = "XingShangMod-v$Version.zip"
$zipPath = Join-Path $distRoot $zipName
$hashPath = "$zipPath.sha256"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
foreach ($oldArtifact in @($zipPath, $hashPath)) {
    if (Test-Path -LiteralPath $oldArtifact) {
        Remove-Item -LiteralPath $oldArtifact -Force
    }
}

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
    uiSprites = 164
    uiFiles = 274
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
Write-Host "Validated $($expectedFiles.Count) runtime payload files; sizes and SHA256 hashes match."

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
    $zipPath,
    [IO.Compression.CompressionLevel]::Optimal,
    $false)

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
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

$zipFile = Get-Item -LiteralPath $zipPath
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($hashPath, "$zipHash  $zipName`r`n", [Text.Encoding]::ASCII)

Write-Host "Package created: $zipPath"
Write-Host "SHA256: $zipHash"
[PSCustomObject]@{
    Version = $Version
    Files = $expectedPackageFiles.Count
    Bytes = $zipFile.Length
    MiB = [Math]::Round($zipFile.Length / 1MB, 2)
    Zip = $zipFile.FullName
    Sha256File = $hashPath
}
