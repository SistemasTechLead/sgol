[CmdletBinding()]
param(
    [string]$WorkflowPath = (Join-Path $PSScriptRoot '../../.github/workflows/pull-request.yml'),
    [string]$WorkflowText
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$workflow = if ($PSBoundParameters.ContainsKey('WorkflowText')) { $WorkflowText }
else { Get-Content -LiteralPath $WorkflowPath -Raw }
$workflow = $workflow.Replace("`r`n", "`n")
$baseline = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pr-gate-step-baseline.json') -Raw | ConvertFrom-Json

function Get-TextHash([string]$Value) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($Value.TrimEnd()))).ToLowerInvariant()
}
function Assert-Contains([string]$Text, [string]$Value) {
    if (-not $Text.Contains($Value, [StringComparison]::Ordinal)) { throw 'PR_WORKFLOW_CONTRACT_MISSING' }
}

$manualStart = $workflow.IndexOf('  hu035-network-ab:', [StringComparison]::Ordinal)
if ($manualStart -lt 0 -or (Get-TextHash $workflow.Substring($manualStart)) -cne $baseline.manualJobSha256) {
    throw 'PR_WORKFLOW_MANUAL_EXPERIMENT_CHANGED'
}
$pr = $workflow.Substring(0, $manualStart)
$actualSteps = @([regex]::Matches($pr, '(?ms)^      - name: ([^\n]+)\n.*?(?=^      - name: |^  [a-z][a-z-]+:|\z)'))
foreach ($expected in $baseline.steps) {
    $matches = @($actualSteps | Where-Object { $_.Groups[1].Value -ceq $expected.name })
    if ($matches.Count -ne $expected.copies) { throw "PR_WORKFLOW_GATE_COUNT_CHANGED:$($expected.name)" }
    foreach ($match in $matches) {
        if ((Get-TextHash $match.Value) -cne $expected.sha256) {
            throw "PR_WORKFLOW_GATE_CHANGED:$($expected.name)"
        }
    }
}
$jobs = @{}
foreach ($match in [regex]::Matches($pr, '(?ms)^  ([a-z][a-z-]+):\n.*?(?=^  [a-z][a-z-]+:|\z)')) {
    $jobs[$match.Groups[1].Value] = $match.Value
}
if ($jobs.Count -ne 5) { throw 'PR_WORKFLOW_JOB_SET_INVALID' }
foreach ($id in @('controls', 'server', 'browser', 'operations', 'verify')) {
    if (-not $jobs.ContainsKey($id)) { throw 'PR_WORKFLOW_JOB_SET_INVALID' }
    Assert-Contains $jobs[$id] 'runs-on: ubuntu-24.04'
    Assert-Contains $jobs[$id] 'contents: read'
    Assert-Contains $jobs[$id] 'Verify exact implementation checkout'
    if ($id -ne 'verify') {
        Assert-Contains $jobs[$id] 'if: github.event_name == ''pull_request'''
        Assert-Contains $jobs[$id] 'validated_sha: ${{ steps.identity.outputs.validated_sha }}'
    }
    if ($id -ne 'operations' -and $jobs[$id].Contains('actions: read', [StringComparison]::Ordinal)) {
        throw 'PR_WORKFLOW_EXCESS_ACTIONS_PERMISSION'
    }
}
if ($pr -match '(?m)^\s+[\w-]+: write\s*$' -or $pr.Contains('${{ secrets.', [StringComparison]::Ordinal)) {
    throw 'PR_WORKFLOW_EXCESS_AUTHORITY'
}
Assert-Contains $jobs.server 'needs: controls'
Assert-Contains $jobs.browser 'needs: controls'
Assert-Contains $jobs.operations 'needs: [controls, server, browser]'
Assert-Contains $jobs.verify 'if: ${{ always() && github.event_name == ''pull_request'' }}'
Assert-Contains $jobs.verify 'needs: [controls, server, browser, operations]'
Assert-Contains $jobs.verify 'name: TECH-BASE-003 / PR gates'
Assert-Contains $jobs.verify 'NEEDS_JSON: ${{ toJSON(needs) }}'
Assert-Contains $jobs.verify './scripts/ci/assert-pr-gate-results.ps1 -ExpectedSha $env:IMPLEMENTATION_SHA -NeedsJson $env:NEEDS_JSON'
Assert-Contains $pr 'group: pr-gates-${{ github.event.pull_request.number || github.run_id }}'
Assert-Contains $pr 'cancel-in-progress: ${{ github.event_name == ''pull_request'' }}'

$previous = -1
foreach ($name in @('TECH-E2E-CV-04 / integral hosted demo',
        'Build immutable OCI image with SBOM and provenance',
        'Scan OCI image for high and critical vulnerabilities',
        'Run integral TECH-OPS gate on native AMD64',
        'Dispose private TECH-OPS resources before HU-035',
        'Run focused replica stream probe on native AMD64',
        'Run integral HU-035 recovery gate on native AMD64')) {
    $position = $jobs.operations.IndexOf("      - name: $name", [StringComparison]::Ordinal)
    if ($position -le $previous) { throw 'PR_WORKFLOW_OPERATION_ORDER_INVALID' }
    $previous = $position
}
Write-Output "PASS: $($baseline.steps.Count) original gates preserved; exact SHA, dependencies, permissions, operations order and manual experiment verified."
