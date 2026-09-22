[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Repository,
    [Parameter(Mandatory = $true)][long]$RunId,
    [Parameter(Mandatory = $true)][string]$HeadSha,
    [Parameter(Mandatory = $true)][string]$Cv04Id,
    [Parameter(Mandatory = $true)][string]$Cv04Digest,
    [Parameter(Mandatory = $true)][string]$TechOpsId,
    [Parameter(Mandatory = $true)][string]$TechOpsDigest,
    [Parameter(Mandatory = $true)][string]$Cv05Id,
    [Parameter(Mandatory = $true)][string]$Cv05Digest
)

$ErrorActionPreference = 'Stop'
if ($Repository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or
    $HeadSha -cnotmatch '^[0-9a-f]{40}$' -or $RunId -le 0 -or
    [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { throw 'CV05_ARTIFACT_PREFLIGHT_FAILED' }
$headers = @{
    Authorization = "Bearer $env:GH_TOKEN"
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
$uri = "https://api.github.com/repos/$Repository/actions/runs/$RunId/artifacts?per_page=100"
$response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
$expected = @(
    @{ Name = "tech-e2e-cv-04-$HeadSha"; Id = $Cv04Id; Digest = $Cv04Digest },
    @{ Name = "tech-ops-$HeadSha"; Id = $TechOpsId; Digest = $TechOpsDigest },
    @{ Name = "tech-e2e-cv-05-$HeadSha"; Id = $Cv05Id; Digest = $Cv05Digest }
)
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('## CV-05 — artifacts originales verificados')
$lines.Add('')
$lines.Add("SHA exacto: ``$HeadSha``. Retención comprobada: 14 días.")
$lines.Add('')
$lines.Add('| Artifact | ID | Bytes | SHA-256 del ZIP | Vence UTC |')
$lines.Add('|---|---:|---:|---|---|')
foreach ($item in $expected) {
    if ($item.Id -cnotmatch '^[0-9]+$' -or $item.Digest -cnotmatch '^(?:sha256:)?[0-9a-f]{64}$') {
        throw 'CV05_ARTIFACT_OUTPUT_INVALID'
    }
    $artifact = @($response.artifacts | Where-Object { [string]$_.id -ceq $item.Id })
    if ($artifact.Count -ne 1 -or $artifact[0].name -cne $item.Name -or
        $artifact[0].expired -ne $false -or [long]$artifact[0].size_in_bytes -le 0 -or
        $artifact[0].workflow_run.head_sha -cne $HeadSha) {
        throw "CV05_ARTIFACT_API_MISMATCH:$($item.Name)"
    }
    $digest = $item.Digest -replace '^sha256:', ''
    if ($artifact[0].digest -cne "sha256:$digest") {
        throw "CV05_ARTIFACT_DIGEST_MISMATCH:$($item.Name)"
    }
    $created = [DateTimeOffset]::Parse([string]$artifact[0].created_at)
    $expires = [DateTimeOffset]::Parse([string]$artifact[0].expires_at)
    if ([Math]::Abs(($expires - $created - [TimeSpan]::FromDays(14)).TotalSeconds) -gt 120) {
        throw "CV05_ARTIFACT_RETENTION_MISMATCH:$($item.Name)"
    }
    $url = "https://github.com/$Repository/actions/runs/$RunId/artifacts/$($item.Id)"
    $lines.Add("| [$($item.Name)]($url) | $($item.Id) | $($artifact[0].size_in_bytes) | ``$digest`` | ``$($expires.ToUniversalTime().ToString('u'))`` |")
}
$summary = $lines -join "`n"
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary -Encoding utf8
}
Write-Output $summary
Write-Output 'CV05_ARTIFACTS_VERIFIED'
