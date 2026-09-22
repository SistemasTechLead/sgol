[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunnerTemp,
    [Parameter(Mandatory = $true)][string]$ReportDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedSha
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cv05-evidence-inventory.ps1')
$entries = @(Get-Cv05EvidenceInventory -RunnerTemp $RunnerTemp `
    -ReportDirectory $ReportDirectory -ExpectedSha $ExpectedSha)
$jsonPath = Join-Path $ReportDirectory 'TECH-E2E-CV-05-report.json'
$markdownPath = Join-Path $ReportDirectory 'TECH-E2E-CV-05-report.md'
if ((Get-Item -LiteralPath $jsonPath).Length -gt 1048576 -or
    (Get-Item -LiteralPath $markdownPath).Length -gt 1048576) {
    throw 'CV05_REPORT_TOO_LARGE'
}
$report = Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json -AsHashtable
if ($report.schemaId -cne 'sgol.tech-e2e-cv05.report' -or $report.schemaVersion -ne 1 -or
    $report.task -cne 'TECH-E2E-CV-05' -or $report.cut -cne 'CV-05' -or
    $report.commit -cne $ExpectedSha -or
    $report.baseCommit -cne '00da83ca26f67e523d0d6067af737f38315e62b0' -or
    $report.seed -cne 'CV05-SEED-V1' -or
    $report.browser -cne 'NO_APLICA' -or $report.accessibility -cne 'NO_APLICA' -or
    $report.executionState -cne 'PASSED' -or $report.reportState -cne 'PASSED' -or
    $report.cleanupState -cne 'PASSED' -or $report.finalState -cne 'PASSED' -or
    $report.ociImageDigest -cnotmatch '^sha256:[0-9a-f]{64}$' -or
    @($report.cycles).Count -ne 2) { throw 'CV05_REPORT_CONTRACT_INVALID' }

$expectedCatalog = @(1..22 | ForEach-Object { 'S{0:D2}' -f $_ })
if (@(Compare-Object $expectedCatalog @($report.scenarios | ForEach-Object { $_.id })).Count -ne 0) {
    throw 'CV05_CATALOG_INVALID'
}
$fingerprints = @($report.cycles | ForEach-Object { $_.functionalFingerprint })
if ($fingerprints.Count -ne 2 -or $fingerprints[0] -cnotmatch '^[0-9a-f]{64}$' -or
    $fingerprints[0] -cne $fingerprints[1]) { throw 'CV05_FINGERPRINT_MISMATCH' }
foreach ($cycle in @($report.cycles)) {
    if ($cycle.cycle -notin @(1, 2)) { throw 'CV05_CYCLE_INVALID' }
    $expected = if ($cycle.cycle -eq 1) { @($expectedCatalog[0..19]) + @('S22') }
                else { $expectedCatalog }
    $observed = @($cycle.evidence | Where-Object { $_.scenario -cmatch '^S(0[1-9]|1[0-9]|2[0-2])$' } |
        ForEach-Object { $_.scenario } | Sort-Object -Unique)
    if (@(Compare-Object $expected $observed).Count -ne 0) { throw 'CV05_SCENARIOS_INCOMPLETE' }
    foreach ($item in @($cycle.evidence)) {
        if ($item.phase -cnotmatch '^[A-Z_]+$' -or $item.scenario -cnotmatch '^(NONE|S[0-9]{2})$' -or
            $item.code -cne 'CV05_NONE' -or $item.exit -ne 0 -or $item.state -cne 'PASSED' -or
            $item.durationMs -lt 0) { throw 'CV05_EVIDENCE_FIELD_INVALID' }
    }
}

$forbidden = '(?i)(password\s*=|secretkey|accesskey|connectionstrings|AGE-SECRET-KEY|s3://|signed[ -]?url|private key|recoverycode|totpcode|csrf.token)'
foreach ($entry in $entries | Where-Object { $_.Name -ne 'oci-layout' }) {
    if (Select-String -LiteralPath $entry.Path -Pattern $forbidden -Quiet) {
        throw "CV05_PUBLIC_EVIDENCE_UNSAFE:$($entry.Name)"
    }
}
$inventory = [ordered]@{
    schemaId = 'sgol.tech-e2e-cv05.inventory'
    schemaVersion = 1
    headSha = $ExpectedSha
    entries = @($entries | ForEach-Object {
        [ordered]@{ name = $_.Name; bytes = $_.Bytes; sha256 = $_.Sha256; required = $_.Required }
    })
}
$inventoryPath = Join-Path $ReportDirectory 'TECH-E2E-CV-05-inventory.json'
[IO.File]::WriteAllText($inventoryPath, ($inventory | ConvertTo-Json -Depth 6 -Compress),
    [Text.UTF8Encoding]::new($false))
Write-Output 'CV05_EVIDENCE_VALIDATED'
