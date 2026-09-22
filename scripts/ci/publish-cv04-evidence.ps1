[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ReportDirectory,
    [Parameter(Mandatory = $true)][string] $ExpectedSha
)

$ErrorActionPreference = 'Stop'
if ($ExpectedSha -cnotmatch '^[0-9a-f]{40}$') { throw 'CV04_EVIDENCE_SHA_INVALID' }
$jsonPath = Join-Path $ReportDirectory 'TECH-E2E-CV-04-report.json'
$markdownPath = Join-Path $ReportDirectory 'TECH-E2E-CV-04-report.md'
if (-not (Test-Path -LiteralPath $jsonPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $markdownPath -PathType Leaf)) {
    throw 'CV04_EVIDENCE_REPORT_MISSING'
}
if ((Get-Item -LiteralPath $jsonPath).Length -gt 1048576 -or
    (Get-Item -LiteralPath $markdownPath).Length -gt 1048576) {
    throw 'CV04_EVIDENCE_REPORT_TOO_LARGE'
}
$report = Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json -AsHashtable
if ($report.schemaId -cne 'sgol.tech-e2e-cv04.report' -or $report.schemaVersion -ne 1 -or
    $report.task -cne 'TECH-E2E-CV-04' -or $report.cut -cne 'CV-04' -or
    $report.commit -cne $ExpectedSha -or $report.seed -cne 'CV04-SEED-V1' -or
    $report.browser -cne 'NO_APLICA' -or $report.accessibility -cne 'NO_APLICA' -or
    $report.reportState -cne 'PASSED' -or $report.cleanupState -cne 'PASSED' -or
    $report.finalState -cne 'PASSED' -or @($report.cycles).Count -ne 2) {
    throw 'CV04_EVIDENCE_CONTRACT_INVALID'
}
$fingerprints = @($report.cycles | ForEach-Object { $_.functionalFingerprint })
if ($fingerprints.Count -ne 2 -or $fingerprints[0] -cnotmatch '^[0-9a-f]{64}$' -or
    $fingerprints[0] -cne $fingerprints[1]) { throw 'CV04_EVIDENCE_CYCLES_DIFFER' }

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('## TECH-E2E-CV-04 — evidencia sin cuota de artifacts')
$lines.Add('')
$lines.Add("Commit exacto: ``$ExpectedSha``. Estado: ``PASSED``. Cleanup: ``PASSED``. Ciclos: 2.")
$lines.Add('Navegador y accesibilidad: `NO_APLICA`.')
$lines.Add('')
$lines.Add('| Ciclo | Fase | Escenario | Exit | Código | Conteos | Duración ms | Estado |')
$lines.Add('|---:|---|---|---:|---|---|---:|---|')
foreach ($cycle in @($report.cycles)) {
    if ($cycle.cycle -notin @(1, 2)) { throw 'CV04_EVIDENCE_CYCLE_INVALID' }
    $scenarios = @($cycle.evidence | Where-Object { $_.scenario -cmatch '^S(0[1-9]|1[0-9]|2[0-4])$' } |
        ForEach-Object { $_.scenario } | Sort-Object -Unique)
    $expectedScenarios = if ($cycle.cycle -eq 1) {
        @(1..22 | ForEach-Object { 'S{0:D2}' -f $_ }) + @('S24')
    } else {
        @(1..24 | ForEach-Object { 'S{0:D2}' -f $_ })
    }
    if (@(Compare-Object $expectedScenarios $scenarios).Count -ne 0 -or -not (@($cycle.evidence | Where-Object {
        $_.phase -ceq 'CLEANUP' -and $_.scenario -ceq 'S24' -and $_.state -ceq 'PASSED'
    }).Count -eq 1)) { throw 'CV04_EVIDENCE_SCENARIOS_INCOMPLETE' }
    foreach ($item in @($cycle.evidence)) {
        if ($item.phase -cnotmatch '^[A-Z_]+$' -or $item.scenario -cnotmatch '^(NONE|S[0-9]{2})$' -or
            $item.code -cnotmatch '^CV04_[A-Z0-9_]+$' -or $item.exit -ne 0 -or
            $item.state -cne 'PASSED' -or $item.durationMs -lt 0) {
            throw 'CV04_EVIDENCE_FIELD_INVALID'
        }
        $counts = [Collections.Generic.List[string]]::new()
        foreach ($key in @($item.counts.Keys | Sort-Object)) {
            if ($key -cnotmatch '^[a-z][a-zA-Z0-9]*$' -or $item.counts[$key] -lt 0) {
                throw 'CV04_EVIDENCE_COUNT_INVALID'
            }
            $counts.Add("${key}=$($item.counts[$key])")
        }
        $lines.Add("| $($cycle.cycle) | $($item.phase) | $($item.scenario) | $($item.exit) | $($item.code) | $($counts -join ', ') | $($item.durationMs) | $($item.state) |")
    }
}
$lines.Add('')
$lines.Add("JSON SHA-256: ``$((Get-FileHash -LiteralPath $jsonPath -Algorithm SHA256).Hash.ToLowerInvariant())``")
$lines.Add("Markdown SHA-256: ``$((Get-FileHash -LiteralPath $markdownPath -Algorithm SHA256).Hash.ToLowerInvariant())``")
$summary = $lines -join "`n"
if ([Text.Encoding]::UTF8.GetByteCount($summary) -gt 1048576) { throw 'CV04_EVIDENCE_SUMMARY_TOO_LARGE' }
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary -Encoding utf8
}
Write-Output $summary
Write-Output 'CV04_EVIDENCE_PUBLISHED'
