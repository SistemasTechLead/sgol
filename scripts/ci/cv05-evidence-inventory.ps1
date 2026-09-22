function Get-Cv05EvidenceInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RunnerTemp,
        [Parameter(Mandatory = $true)][string]$ReportDirectory,
        [Parameter(Mandatory = $true)][string]$ExpectedSha
    )

    if ($ExpectedSha -cnotmatch '^[0-9a-f]{40}$') { throw 'CV05_INVENTORY_SHA_INVALID' }
    $required = [ordered]@{
        'cv05-json' = Join-Path $ReportDirectory 'TECH-E2E-CV-05-report.json'
        'cv05-markdown' = Join-Path $ReportDirectory 'TECH-E2E-CV-05-report.md'
        'cv04-json' = Join-Path (Join-Path (Split-Path $ReportDirectory -Parent) '../cv04/latest') 'TECH-E2E-CV-04-report.json'
        'cv04-markdown' = Join-Path (Join-Path (Split-Path $ReportDirectory -Parent) '../cv04/latest') 'TECH-E2E-CV-04-report.md'
        'oci-metadata' = Join-Path $RunnerTemp 'sgol-oci-metadata.json'
        'oci-layout' = Join-Path $RunnerTemp "sgol-$ExpectedSha.oci.tar"
        'tech-ops-summary' = Join-Path $RunnerTemp 'sgol-tech-ops-amd64/evidence-public/tech-ops-summary.json'
        'tech-ops-inspect' = Join-Path $RunnerTemp 'sgol-tech-ops-amd64/evidence-public/image-inspect.json'
        'hu-035-result' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-result.json'
        'hu-035-summary' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-summary.json'
        'replica-stream-trx' = Join-Path $RunnerTemp 'sgol-replica-stream-public/replica-stream.trx'
    }
    $optional = [ordered]@{
        'hu-035-server-diagnostic' = Join-Path $RunnerTemp 'sgol-hu-035-amd64/evidence-public/hu-035-server-diagnostic.json'
    }
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($item in $required.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $item.Value -PathType Leaf)) {
            throw "CV05_REQUIRED_EVIDENCE_MISSING:$($item.Key)"
        }
        $file = Get-Item -LiteralPath $item.Value
        if ($file.Length -le 0) { throw "CV05_REQUIRED_EVIDENCE_EMPTY:$($item.Key)" }
        $entries.Add([pscustomobject]@{
            Name = $item.Key; Path = $file.FullName; Bytes = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Required = $true
        })
    }
    foreach ($item in $optional.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $item.Value -PathType Leaf)) { continue }
        $file = Get-Item -LiteralPath $item.Value
        if ($file.Length -le 0) { throw "CV05_OPTIONAL_EVIDENCE_EMPTY:$($item.Key)" }
        $entries.Add([pscustomobject]@{
            Name = $item.Key; Path = $file.FullName; Bytes = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Required = $false
        })
    }
    return $entries.ToArray()
}
