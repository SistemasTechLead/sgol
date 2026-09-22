[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cv05-evidence-inventory.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('sgol-cv05-inventory-' + [guid]::NewGuid().ToString('N'))
$runner = Join-Path $root 'runner'
$report = Join-Path $root 'repository/.artifacts/cv05/latest'
$sha = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
try {
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $paths = @(
        (Join-Path $report 'TECH-E2E-CV-05-report.json'),
        (Join-Path $report 'TECH-E2E-CV-05-report.md'),
        (Join-Path $root 'repository/.artifacts/cv04/latest/TECH-E2E-CV-04-report.json'),
        (Join-Path $root 'repository/.artifacts/cv04/latest/TECH-E2E-CV-04-report.md'),
        (Join-Path $runner 'sgol-oci-metadata.json'),
        (Join-Path $runner "sgol-$sha.oci.tar"),
        (Join-Path $runner 'sgol-tech-ops-amd64/evidence-public/tech-ops-summary.json'),
        (Join-Path $runner 'sgol-tech-ops-amd64/evidence-public/image-inspect.json'),
        (Join-Path $runner 'sgol-hu-035-amd64/evidence-public/hu-035-result.json'),
        (Join-Path $runner 'sgol-hu-035-amd64/evidence-public/hu-035-summary.json'),
        (Join-Path $runner 'sgol-replica-stream-public/replica-stream.trx')
    )
    foreach ($path in $paths) {
        [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null
        [IO.File]::WriteAllText($path, 'synthetic')
    }
    $withoutOptional = @(Get-Cv05EvidenceInventory -RunnerTemp $runner -ReportDirectory $report -ExpectedSha $sha)
    if ($withoutOptional.Count -ne 11 -or @($withoutOptional | Where-Object { -not $_.Required }).Count -ne 0) {
        throw 'CV05_OPTIONAL_ABSENT_CASE_FAILED'
    }
    $diagnostic = Join-Path $runner 'sgol-hu-035-amd64/evidence-public/hu-035-server-diagnostic.json'
    [IO.File]::WriteAllText($diagnostic, 'synthetic')
    $withOptional = @(Get-Cv05EvidenceInventory -RunnerTemp $runner -ReportDirectory $report -ExpectedSha $sha)
    if ($withOptional.Count -ne 12 -or @($withOptional | Where-Object { -not $_.Required }).Count -ne 1) {
        throw 'CV05_OPTIONAL_PRESENT_CASE_FAILED'
    }
    [IO.File]::Delete($paths[0])
    $missingRequiredRejected = $false
    try { $null = Get-Cv05EvidenceInventory -RunnerTemp $runner -ReportDirectory $report -ExpectedSha $sha }
    catch { $missingRequiredRejected = $_.Exception.Message -ceq 'CV05_REQUIRED_EVIDENCE_MISSING:cv05-json' }
    if (-not $missingRequiredRejected) { throw 'CV05_REQUIRED_MISSING_CASE_FAILED' }
    Write-Output 'CV05_INVENTORY_PURE_TESTS_PASSED'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ((Split-Path $resolved -Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) -cne $expectedParent -or
        (Split-Path $resolved -Leaf) -cnotmatch '^sgol-cv05-inventory-[0-9a-f]{32}$') {
        throw 'CV05_TEST_CLEANUP_SCOPE_INVALID'
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
