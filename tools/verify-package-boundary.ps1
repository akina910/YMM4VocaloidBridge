param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Package root does not exist: $root"
}

$allowedPaths = @(
    '^YMM4VocaloidBridge/(README\.md|INSTALLATION\.md|LICENSE|THIRD-PARTY-NOTICES\.md)$',
    '^YMM4VocaloidBridge/(J2N|Lucene\.Net|Lucene\.Net\.Analysis\.Common|Lucene\.Net\.Analysis\.Kuromoji|Microsoft\.Extensions\.Configuration\.Abstractions|Microsoft\.Extensions\.Primitives)\.dll$',
    '^YMM4VocaloidBridge/YMM4VocaloidBridge\.(Automation|Core|Plugin)\.dll$',
    '^YMM4VocaloidBridge/YMM4VocaloidBridge\.Plugin\.deps\.json$',
    '^YMM4VocaloidBridge/tools/YMM4VocaloidBridge\.Cli\.exe$',
    '^YMM4VocaloidBridge/licenses/[A-Za-z0-9.-]+\.txt$'
)
$forbiddenExtensions = @(
    '.wav', '.mp3', '.flac', '.ogg', '.mid', '.midi', '.vpr', '.vsqx',
    '.png', '.jpg', '.jpeg', '.webp', '.bmp', '.gif', '.psd', '.ymmt',
    '.pfx', '.p12', '.pem', '.key', '.kdbx', '.env'
)
$secretPatterns = @(
    '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----',
    'github_pat_[A-Za-z0-9_]{20,}',
    'gh[pousr]_[A-Za-z0-9]{30,}',
    'AKIA[0-9A-Z]{16}'
)
$absolutePathPatterns = @(
    '[A-Za-z]:\\Users\\[^\\\x00]+\\',
    '/Users/[^/\x00]+/',
    '/home/[^/\x00]+/'
)

$unexpected = [System.Collections.Generic.List[string]]::new()
$files = Get-ChildItem -LiteralPath $root -Recurse -File
$rootPrefix = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) `
    + [System.IO.Path]::DirectorySeparatorChar
foreach ($file in $files) {
    if (-not $file.FullName.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package file resolved outside the package root: $($file.FullName)"
    }
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
    if (-not ($allowedPaths | Where-Object { $relative -match $_ })) {
        $unexpected.Add("not allow-listed: $relative")
    }

    if ($forbiddenExtensions -contains $file.Extension.ToLowerInvariant()) {
        $unexpected.Add("forbidden extension: $relative")
    }

    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [System.Text.Encoding]::Unicode.GetString($bytes)
    foreach ($pattern in $secretPatterns + $absolutePathPatterns) {
        if ($ascii -match $pattern -or $unicode -match $pattern) {
            $unexpected.Add("forbidden content pattern in: $relative")
            break
        }
    }
}

if ($unexpected.Count -gt 0) {
    throw "Package boundary violations:`n$($unexpected -join [Environment]::NewLine)"
}

Write-Output "Package boundary PASS ($($files.Count) allow-listed files)."
