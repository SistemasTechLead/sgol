[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExpectedSha,
    [Parameter(Mandatory)][string]$NeedsJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($ExpectedSha -cnotmatch '^[0-9a-f]{40}$') { throw 'PR_GATE_EXPECTED_SHA_INVALID' }
try { $results = ConvertFrom-Json -InputObject $NeedsJson -AsHashtable -ErrorAction Stop }
catch { throw 'PR_GATE_RESULTS_INVALID' }
if ($results -isnot [Collections.IDictionary]) { throw 'PR_GATE_RESULTS_INVALID' }

$requiredJobs = @('controls', 'server', 'browser', 'operations')
if ($results.Count -ne $requiredJobs.Count) { throw 'PR_GATE_JOB_SET_INVALID' }
foreach ($job in $requiredJobs) {
    if (-not $results.Contains($job)) { throw 'PR_GATE_JOB_SET_INVALID' }
    $result = $results[$job]
    if ($result -isnot [Collections.IDictionary] -or
        -not $result.Contains('result') -or $result['result'] -cne 'success') {
        throw "PR_GATE_JOB_NOT_SUCCESS:$job"
    }
    if (-not $result.Contains('outputs') -or
        $result['outputs'] -isnot [Collections.IDictionary] -or
        -not $result['outputs'].Contains('validated_sha') -or
        $result['outputs']['validated_sha'] -cne $ExpectedSha) {
        throw "PR_GATE_SHA_MISMATCH:$job"
    }
}
Write-Output 'PASS: all required PR gates succeeded for the exact implementation SHA.'
