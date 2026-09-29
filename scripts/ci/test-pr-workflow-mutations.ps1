Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'test-pr-workflow.ps1'
$original = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../.github/workflows/pull-request.yml') -Raw
$cases = @(
    @{ Name = 'missing gate'; From = '      - name: Scan secrets with pinned Gitleaks'; To = '      - name: Removed secret scanner' },
    @{ Name = 'weakened filter'; From = 'Category!=CV05_FOCUSED&Category!=FRONT_BROWSER'; To = 'Category!=CV05_FOCUSED&Category!=FRONT_BROWSER&Category!=SECURITY' },
    @{ Name = 'missing dependency'; From = 'needs: [controls, server, browser]'; To = 'needs: controls' },
    @{ Name = 'missing always'; From = 'always() && github.event_name'; To = 'success() && github.event_name' },
    @{ Name = 'write authority'; From = '      contents: read'; To = '      contents: write' },
    @{ Name = 'modified manual'; From = 'HU-035 / isolated network A-B experiment (not acceptance)'; To = 'HU-035 / altered experiment' },
    @{ Name = 'persistent credentials'; From = '          persist-credentials: false'; To = '          persist-credentials: true' }
)
foreach ($case in $cases) {
    if (-not $original.Contains($case.From, [StringComparison]::Ordinal)) { throw 'PR_WORKFLOW_MUTATION_TARGET_MISSING' }
    $rejected = $false
    try { & $validator -WorkflowText $original.Replace($case.From, $case.To) | Out-Null }
    catch { $rejected = $_.Exception.Message.StartsWith('PR_WORKFLOW_', [StringComparison]::Ordinal) }
    if (-not $rejected) { throw "PR_WORKFLOW_MUTATION_ACCEPTED:$($case.Name)" }
}
Write-Output "PASS: $($cases.Count) weakened workflow mutations rejected without filesystem or external effects."
