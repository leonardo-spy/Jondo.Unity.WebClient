param(
    [string]$CacheRoot = (Join-Path $env:LOCALAPPDATA 'npm-cache/_npx'),
    [ValidateRange(64, 1024)]
    [int]$LimitMiB = 512,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$pattern = '(?<prefix>(?:const )?BINARY_BYTES = 1024 \* 1024 \* 1024;\r?\n(?://[^\r\n]*\r?\n)?(?:const |[\t ]+)OUTPUT_BYTES = )(?<limit>\d+)(?<suffix> \* 1024 \* 1024;)'
$targets = @(
    foreach ($entry in Get-ChildItem -LiteralPath $CacheRoot -Directory) {
        $root = Join-Path $entry.FullName 'node_modules/rea-agents'
        $manifest = Join-Path $root 'package.json'
        if (-not (Test-Path -LiteralPath $manifest)) { continue }
        $package = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        if ($package.version -ne '6.8.0') { continue }
        foreach ($relative in @('dist/il2cpp/Il2cppMetadataProvider.js', 'dist/cli-runtime/cli.js')) {
            $path = Join-Path $root $relative
            $text = [System.IO.File]::ReadAllText($path)
            $matches = [regex]::Matches($text, $pattern)
            if ($matches.Count -ne 1) { throw "Expected one IL2CPP report allowance in $path; found $($matches.Count)" }
            [pscustomobject]@{
                Path = $path
                Text = $text
                CurrentMiB = [int]$matches[0].Groups['limit'].Value
                Backup = "$path.pre-il2cpp-${LimitMiB}mib.bak"
            }
        }
    }
)
if ($targets.Count -eq 0) { throw "No supported REA 6.8.0 installations found in $CacheRoot" }
if ($CheckOnly) {
    foreach ($target in $targets) {
        if ($target.CurrentMiB -ne $LimitMiB) { throw "Unexpected IL2CPP report allowance $($target.CurrentMiB) MiB in $($target.Path); expected $LimitMiB" }
    }
}
$results = @(
    foreach ($target in $targets) {
        $changed = $target.CurrentMiB -ne $LimitMiB
        if ($changed -and -not $CheckOnly) {
            if (-not (Test-Path -LiteralPath $target.Backup)) {
                Copy-Item -LiteralPath $target.Path -Destination $target.Backup
            }
            $replacement = [regex]::Replace($target.Text, $pattern, {
                param($match)
                $match.Groups['prefix'].Value + $LimitMiB + $match.Groups['suffix'].Value
            })
            [System.IO.File]::WriteAllText($target.Path, $replacement, [System.Text.UTF8Encoding]::new($false))
        }
        $originalHash = $null
        $originalLimit = $null
        if (Test-Path -LiteralPath $target.Backup) {
            $originalHash = (Get-FileHash -LiteralPath $target.Backup -Algorithm SHA256).Hash.ToLowerInvariant()
            $originalMatch = [regex]::Match([System.IO.File]::ReadAllText($target.Backup), $pattern)
            if ($originalMatch.Success) { $originalLimit = [int]$originalMatch.Groups['limit'].Value }
        }
        [pscustomobject]@{
            path = $target.Path
            packageVersion = '6.8.0'
            reportLimitBytes = [long]$LimitMiB * 1024 * 1024
            originalLimitMiB = $originalLimit
            backup = $target.Backup
            originalSha256 = $originalHash
            patchedSha256 = (Get-FileHash -LiteralPath $target.Path -Algorithm SHA256).Hash.ToLowerInvariant()
            changed = $changed -and -not $CheckOnly
        }
    }
)
[pscustomobject]@{
    schemaVersion = 1
    checkOnly = [bool]$CheckOnly
    installations = $targets.Count / 2
    files = $results
} | ConvertTo-Json -Depth 5