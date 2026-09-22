[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $RunnerTemp,
    [Parameter(Mandatory = $true)][string] $ExpectedSha
)

$ErrorActionPreference = 'Stop'
if ($ExpectedSha -cnotmatch '^[0-9a-f]{40}$') { throw 'CV04_OPERATIONS_SHA_INVALID' }
$files = [ordered]@{
    'oci-metadata' = Join-Path $RunnerTemp 'sgol-oci-metadata.json'
    'oci-layout' = Join-Path $RunnerTemp "sgol-$ExpectedSha.oci.tar"
    'tech-ops-summary' = Join-Path $RunnerTemp 'sgol-tech-ops-amd64/evidence-public/tech-ops-summary.json'
    'tech-ops-inspect' = Join-Path $RunnerTemp 'sgol-tech-ops-amd64/evidence-public/image-inspect.json'
    'hu-035-result' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-result.json'
    'hu-035-summary' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-summary.json'
    'hu-035-diagnostic' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-server-diagnostic.json'
    'replica-stream-trx' = Join-Path $RunnerTemp 'sgol-replica-stream-public/replica-stream.trx'
}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('## CV-04 — huellas de gates operativos')
$lines.Add('')
$lines.Add("Commit exacto: ``$ExpectedSha``. Todos los gates previos terminaron correctamente.")
$lines.Add('La cuota impide adjuntar el layout OCI completo; su huella no equivale al archivo descargable.')
$lines.Add('')
$lines.Add('| Evidencia pública | Bytes | SHA-256 |')
$lines.Add('|---|---:|---|')
foreach ($entry in $files.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "CV04_OPERATIONS_EVIDENCE_MISSING:$($entry.Key)"
    }
    $file = Get-Item -LiteralPath $entry.Value
    if ($file.Length -le 0) { throw "CV04_OPERATIONS_EVIDENCE_EMPTY:$($entry.Key)" }
    $digest = (Get-FileHash -LiteralPath $entry.Value -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add("| $($entry.Key) | $($file.Length) | ``$digest`` |")
}
$summary = $lines -join "`n"
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary -Encoding utf8
}
Write-Output $summary
Write-Output 'CV04_OPERATIONS_DIGESTS_PUBLISHED'
