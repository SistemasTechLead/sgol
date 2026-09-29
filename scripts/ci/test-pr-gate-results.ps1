Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'assert-pr-gate-results.ps1'
$sha = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
$jobs = @('controls', 'server', 'browser', 'operations')
$cases = 0

function New-SuccessfulResults {
    $results = @{}
    foreach ($job in $jobs) {
        $results[$job] = @{ result = 'success'; outputs = @{ validated_sha = $sha } }
    }
    return $results
}

function Assert-Rejected([string]$Json, [string]$Code, [string]$ExpectedSha = $sha) {
    $rejected = $false
    try { & $validator -ExpectedSha $ExpectedSha -NeedsJson $Json | Out-Null }
    catch { $rejected = $_.Exception.Message -ceq $Code }
    if (-not $rejected) { throw "PR_GATE_NEGATIVE_CASE_FAILED:$Code" }
    $script:cases++
}

& $validator -ExpectedSha $sha -NeedsJson ((New-SuccessfulResults) | ConvertTo-Json -Depth 4) | Out-Null
$cases++
foreach ($job in $jobs) {
    foreach ($state in @('failure', 'cancelled', 'skipped', 'pending', '', 'SUCCESS')) {
        $results = New-SuccessfulResults
        $results[$job].result = $state
        Assert-Rejected ($results | ConvertTo-Json -Depth 4) "PR_GATE_JOB_NOT_SUCCESS:$job"
    }
    $results = New-SuccessfulResults
    $results[$job].outputs.validated_sha = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
    Assert-Rejected ($results | ConvertTo-Json -Depth 4) "PR_GATE_SHA_MISMATCH:$job"
    $results = New-SuccessfulResults
    $results[$job].outputs.Remove('validated_sha')
    Assert-Rejected ($results | ConvertTo-Json -Depth 4) "PR_GATE_SHA_MISMATCH:$job"
    $results = New-SuccessfulResults
    $results[$job].Remove('outputs')
    Assert-Rejected ($results | ConvertTo-Json -Depth 4) "PR_GATE_SHA_MISMATCH:$job"
    $results = New-SuccessfulResults
    $results.Remove($job)
    Assert-Rejected ($results | ConvertTo-Json -Depth 4) 'PR_GATE_JOB_SET_INVALID'
}
$results = New-SuccessfulResults
$results['unexpected'] = @{ result = 'success' }
Assert-Rejected ($results | ConvertTo-Json -Depth 4) 'PR_GATE_JOB_SET_INVALID'
foreach ($json in @('{', 'null', '[]', '"success"')) {
    Assert-Rejected $json 'PR_GATE_RESULTS_INVALID'
}
Assert-Rejected ((New-SuccessfulResults) | ConvertTo-Json -Depth 4) 'PR_GATE_EXPECTED_SHA_INVALID' 'not-a-sha'
Write-Output "PASS: $cases PR gate consolidation cases; failures, cancellations, skips and mismatched SHAs rejected."
