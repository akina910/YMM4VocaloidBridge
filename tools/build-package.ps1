param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0-beta.6",
    [string]$Ymm4DirPath = $env:YMM4_DIR
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $root "artifacts"))
$publishRoot = Join-Path $artifacts "publish"
$stagingRoot = Join-Path $artifacts "ymme-staging"
$standaloneStagingRoot = Join-Path $artifacts "standalone-staging"
$pluginFolder = Join-Path $stagingRoot "YMM4VocaloidBridge"
$packagePath = Join-Path $artifacts "YMM4VocaloidBridge.v.$Version.ymme"
$standalonePath = Join-Path $artifacts "MikuRobotSpeech.v.$Version.win-x64.zip"
$zipPath = [System.IO.Path]::ChangeExtension($packagePath, ".zip")
$sourceRevision = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceRevision -notmatch "^[0-9a-f]{40}$") {
    throw "Could not determine the source revision for the package."
}
$worktreeChanges = & git -C $root status --porcelain=v1 --untracked-files=all
if ($LASTEXITCODE -ne 0) {
    throw "Could not determine whether the package source is clean."
}
if ($worktreeChanges) {
    throw "Refusing to package an uncommitted worktree. Commit the exact source first."
}

if ([string]::IsNullOrWhiteSpace($Ymm4DirPath)) {
    $localYmm4 = Join-Path $root "..\runtime\YMM4-v4.54.0.1"
    if (Test-Path (Join-Path $localYmm4 "YukkuriMovieMaker.Plugin.dll")) {
        $Ymm4DirPath = [System.IO.Path]::GetFullPath($localYmm4)
    }
}

if ([string]::IsNullOrWhiteSpace($Ymm4DirPath) -or -not (Test-Path (Join-Path $Ymm4DirPath "YukkuriMovieMaker.Plugin.dll"))) {
    throw "Set YMM4_DIR or -Ymm4DirPath to a YMM4 directory containing YukkuriMovieMaker.Plugin.dll."
}

foreach ($path in @($publishRoot, $stagingRoot, $standaloneStagingRoot)) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    if (-not $resolved.StartsWith($artifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the artifacts directory: $resolved"
    }
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$dotnet = Get-Command dotnet -ErrorAction Stop
$pluginPublish = Join-Path $publishRoot "plugin"
$cliPublish = Join-Path $publishRoot "cli"
& $dotnet.Source clean (Join-Path $root "src\YMM4VocaloidBridge.Plugin\YMM4VocaloidBridge.Plugin.csproj") `
    -c $Configuration "-p:YMM4DirPath=$Ymm4DirPath"
if ($LASTEXITCODE -ne 0) { throw "Plugin clean failed." }
& $dotnet.Source clean (Join-Path $root "src\YMM4VocaloidBridge.Cli\YMM4VocaloidBridge.Cli.csproj") `
    -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "CLI clean failed." }
& $dotnet.Source publish (Join-Path $root "src\YMM4VocaloidBridge.Plugin\YMM4VocaloidBridge.Plugin.csproj") `
    -c $Configuration -o $pluginPublish --no-self-contained "-p:YMM4DirPath=$Ymm4DirPath" `
    "-p:Version=$Version" "-p:SourceRevisionId=$sourceRevision"
if ($LASTEXITCODE -ne 0) { throw "Plugin publish failed." }

& $dotnet.Source publish (Join-Path $root "src\YMM4VocaloidBridge.Cli\YMM4VocaloidBridge.Cli.csproj") `
    -c $Configuration -o $cliPublish -r win-x64 --self-contained true `
    "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" `
    "-p:EnableCompressionInSingleFile=true" "-p:Version=$Version" "-p:SourceRevisionId=$sourceRevision"
if ($LASTEXITCODE -ne 0) { throw "CLI publish failed." }

New-Item -ItemType Directory -Path $pluginFolder -Force | Out-Null
Copy-Item -Path (Join-Path $pluginPublish "*") -Destination $pluginFolder -Recurse -Force

Get-ChildItem -Path $pluginFolder -Recurse -File | Where-Object {
    $_.Name -like "*.pdb" -or
    $_.Name -like "YukkuriMovieMaker.*" -or
    $_.Name -like "System.*.dll" -or
    $_.Name -eq "Microsoft.Windows.SDK.NET.dll" -or
    $_.Name -eq "WinRT.Runtime.dll"
} | Remove-Item -Force

$toolsFolder = Join-Path $pluginFolder "tools"
New-Item -ItemType Directory -Path $toolsFolder -Force | Out-Null
Copy-Item -Path (Join-Path $cliPublish "*") -Destination $toolsFolder -Recurse -Force
Get-ChildItem -Path $toolsFolder -Recurse -File -Filter "*.pdb" | Remove-Item -Force

Copy-Item (Join-Path $root "README.md") $pluginFolder
Copy-Item (Join-Path $root "LICENSE") $pluginFolder
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") $pluginFolder
Copy-Item (Join-Path $root "docs\INSTALLATION.md") $pluginFolder
Copy-Item (Join-Path $root "licenses") $pluginFolder -Recurse

New-Item -ItemType Directory -Path $standaloneStagingRoot -Force | Out-Null
Copy-Item -Path (Join-Path $cliPublish "*") -Destination $standaloneStagingRoot -Recurse -Force
Move-Item `
    -LiteralPath (Join-Path $standaloneStagingRoot "YMM4VocaloidBridge.Cli.exe") `
    -Destination (Join-Path $standaloneStagingRoot "MikuRobotSpeech.exe")
Copy-Item (Join-Path $root "README.md") $standaloneStagingRoot
Copy-Item (Join-Path $root "LICENSE") $standaloneStagingRoot
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") $standaloneStagingRoot
Copy-Item (Join-Path $root "docs\ROBOT_SPEECH.md") $standaloneStagingRoot
Copy-Item (Join-Path $root "licenses") $standaloneStagingRoot -Recurse

& (Join-Path $PSScriptRoot "verify-package-boundary.ps1") -PackageRoot $stagingRoot
if ($LASTEXITCODE -ne 0) { throw "Package boundary verification failed." }

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
Compress-Archive -Path $pluginFolder -DestinationPath $zipPath -CompressionLevel Optimal
Move-Item -LiteralPath $zipPath -Destination $packagePath

if (Test-Path -LiteralPath $standalonePath) { Remove-Item -LiteralPath $standalonePath -Force }
Compress-Archive -Path (Join-Path $standaloneStagingRoot "*") -DestinationPath $standalonePath -CompressionLevel Optimal

Write-Output $packagePath
Write-Output $standalonePath
