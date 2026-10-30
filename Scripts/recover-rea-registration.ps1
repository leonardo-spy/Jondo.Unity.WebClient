param(
    [Parameter(Mandatory = $true)]
    [string]$ReaPackageRoot,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$package = Get-Content -LiteralPath (Join-Path $ReaPackageRoot 'package.json') -Raw | ConvertFrom-Json
if ($package.name -ne 'rea-agents' -or $package.version -ne '6.8.0') {
    throw 'Expected an existing rea-agents 6.8.0 package'
}
$node = (Get-Command node.exe -ErrorAction Stop).Source
$launcher = Join-Path $ReaPackageRoot 'scripts/rea.mjs'
if (-not (Test-Path -LiteralPath $launcher)) { throw "Public REA launcher is missing: $launcher" }
$overview = Get-Content -LiteralPath (Join-Path $repo 'Generated/gameassembly_rea.json') -Raw | ConvertFrom-Json
$addresses = Get-Content -LiteralPath (Join-Path $repo 'Generated/output/boot_addresses_view_rea.json') -Raw | ConvertFrom-Json
$methods = Get-Content -LiteralPath (Join-Path $repo 'Generated/output/boot_methods_view_rea.json') -Raw | ConvertFrom-Json
$types = Get-Content -LiteralPath (Join-Path $repo 'Generated/output/boot_types_view_rea.json') -Raw | ConvertFrom-Json
$owner = @($types.normalized_result.items | Where-Object { $_.name -eq 'GameCompositor' -and $_.namespace -eq 'Core.Engine.Compositor' })
if ($owner.Count -ne 1) { throw 'Expected one original GameCompositor owner' }
$method = @($methods.normalized_result.items | Where-Object { $_.name -eq 'bkqa' -and $_.declaring_type -eq $owner[0].index })
if ($method.Count -ne 1) { throw 'Expected one original registration method' }
$address = @($addresses.normalized_result.items | Where-Object index -eq $method[0].index)
if ($address.Count -ne 1 -or $address[0].state -ne 'code') { throw 'Original registration has no resolved code address' }
if ($types.normalized_result.parent_evidence_id -ne $methods.normalized_result.parent_evidence_id -or
    $methods.normalized_result.parent_evidence_id -ne $addresses.normalized_result.parent_evidence_id) {
    throw 'Boot selections do not refer to the same parent Evidence'
}
$binary = $overview.subject.local_path
$digest = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()
if ($digest -ne $overview.subject.digest.sha256 -or $digest -ne $addresses.normalized_result.binary_source.artifact.sha256) {
    throw 'Current GameAssembly does not match the retained boot evidence'
}
if ($CheckOnly) {
    [pscustomobject]@{Status='ready';Version=$package.version;Address=$address[0].address;BinarySha256=$digest;Launcher=$launcher} | ConvertTo-Json
    return
}
$config = Get-Content -LiteralPath (Join-Path $HOME '.claude.json') -Raw | ConvertFrom-Json
foreach ($name in @('GHIDRA_INSTALL_DIR','JAVA_HOME','REA_GHIDRA_STARTUP_TIMEOUT_MS','REA_MCP_MAX_RESPONSE_BYTES')) {
    $value = $config.mcpServers.rea.env.$name
    if ($null -ne $value) { [Environment]::SetEnvironmentVariable($name, [string]$value, 'Process') }
}
$env:NODE_OPTIONS = '--max-old-space-size=8192'
$env:REA_GHIDRA_STARTUP_TIMEOUT_MS = '18000000'
$runId = [guid]::NewGuid().ToString('N')
$run = New-Item -ItemType Directory -Path (Join-Path $repo "Generated/output/registration-recovery-$runId")
$stdout = Join-Path $run.FullName 'summary.json'
$stderr = Join-Path $run.FullName 'stderr.log'
$bundle = Join-Path $run.FullName 'function-evidence.json'
$snapshot = Join-Path $run.FullName 'snapshot.json'
$reportPath = Join-Path $run.FullName 'run.json'
$report = [ordered]@{
    schemaVersion=1; status='starting'; startedAt=[DateTime]::UtcNow.ToString('o');
    launcher=$launcher; launcherSha256=(Get-FileHash -LiteralPath $launcher -Algorithm SHA256).Hash.ToLowerInvariant();
    binarySha256=$digest; address=$address[0].address; operation='function';
    stdout=$stdout; stderr=$stderr; evidenceBundle=$bundle; snapshot=$snapshot
}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output "REA recovery journal: $reportPath"
$arguments = @($launcher,'function',$binary,$address[0].address,'--provider','ghidra','--snapshot',$snapshot,
              '--detail','summary','--evidence_output',$bundle,'--format','json')
$quotedArguments = @($arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' })
try {
    $process = Start-Process -FilePath $node -ArgumentList $quotedArguments -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru -Wait -NoNewWindow
    $report['exitCode'] = $process.ExitCode
    $report['finishedAt'] = [DateTime]::UtcNow.ToString('o')
    $report['status'] = 'failed'
    if ($process.ExitCode -eq 0 -and (Test-Path -LiteralPath $bundle)) {
        $evidence = Get-Content -LiteralPath $bundle -Raw | ConvertFrom-Json
        $records = @($evidence.records | Where-Object operation -eq 'analyze_function')
        if ($records.Count -eq 1) {
            $report['status'] = 'captured'
            $report['evidenceId'] = $records[0].evidence_id
            $report['evidenceBundleSha256'] = (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
} catch {
    $report['status'] = 'failed'
    $report['error'] = $_.Exception.Message
    throw
} finally {
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding utf8
}
if ($report['status'] -ne 'captured') { throw "REA recovery did not produce function Evidence; inspect $reportPath and $stderr" }
$report | ConvertTo-Json -Depth 4