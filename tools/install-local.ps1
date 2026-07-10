param(
    [Parameter(Mandatory = $true)]
    [string]$Ymm4DirPath,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$target = [System.IO.Path]::GetFullPath((Join-Path $Ymm4DirPath "user\plugin\YMM4VocaloidBridge"))
$expectedRoot = [System.IO.Path]::GetFullPath($Ymm4DirPath)
if (-not $target.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Resolved plugin path is outside the selected YMM4 directory."
}

$dotnet = Get-Command dotnet -ErrorAction Stop
$publish = Join-Path $root "artifacts\local-plugin"
if (Test-Path -LiteralPath $publish) {
    Remove-Item -LiteralPath $publish -Recurse -Force
}
& $dotnet.Source publish (Join-Path $root "src\YMM4VocaloidBridge.Plugin\YMM4VocaloidBridge.Plugin.csproj") `
    -c $Configuration -o $publish --no-self-contained "-p:YMM4DirPath=$Ymm4DirPath"
if ($LASTEXITCODE -ne 0) { throw "Plugin publish failed." }

if (Test-Path -LiteralPath $target) {
    Remove-Item -LiteralPath $target -Recurse -Force
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $publish "*") -Destination $target -Recurse -Force
Get-ChildItem -Path $target -Recurse -File | Where-Object {
    $_.Name -like "*.pdb" -or
    $_.Name -like "YukkuriMovieMaker.*" -or
    $_.Name -like "System.*.dll" -or
    $_.Name -eq "Microsoft.Windows.SDK.NET.dll" -or
    $_.Name -eq "WinRT.Runtime.dll"
} | Remove-Item -Force

Write-Output $target
