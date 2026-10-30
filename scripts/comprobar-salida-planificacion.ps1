param(
    [Parameter(Mandatory=$true)][string]$Assembly,
    [string]$EvidenceDirectory = '.local/qa-planning-exit'
)
$ErrorActionPreference = 'Stop'
foreach ($mode in @('assertion', 'diagnostic')) {
    $output = Join-Path $EvidenceDirectory $mode
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $arguments = @($Assembly, '--planning-failure-probe', $output)
    if ($mode -eq 'diagnostic') { $arguments += '--diagnostic' }
    & dotnet @arguments *> (Join-Path $output 'process.log')
    $processExit = $LASTEXITCODE
    $evidence = Get-Content (Join-Path $output 'planning-results.json') -Raw | ConvertFrom-Json
    if ($evidence.samples.Count -ne 1 -or $evidence.samples[0].passed -ne $false) { throw 'Missing deliberately failed sample.' }
    if (($mode -eq 'assertion' -and $processExit -eq 0) -or ($mode -eq 'diagnostic' -and $processExit -ne 0)) {
        throw "Unexpected $mode exit: $processExit"
    }
    Write-Output "PASS $mode : exit=$processExit; failed sample retained."
}
