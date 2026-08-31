[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-FilesByteEqual {
    param(
        [Parameter(Mandatory)][string]$FirstPath,
        [Parameter(Mandatory)][string]$SecondPath
    )

    $first = [IO.File]::OpenRead($FirstPath)
    $second = [IO.File]::OpenRead($SecondPath)
    try {
        if ($first.Length -ne $second.Length) { return $false }

        $firstBuffer = [byte[]]::new(65536)
        $secondBuffer = [byte[]]::new(65536)
        while (($firstRead = $first.Read($firstBuffer, 0, $firstBuffer.Length)) -gt 0) {
            $secondRead = $second.Read($secondBuffer, 0, $secondBuffer.Length)
            if ($firstRead -ne $secondRead) { return $false }
            for ($index = 0; $index -lt $firstRead; $index++) {
                if ($firstBuffer[$index] -ne $secondBuffer[$index]) { return $false }
            }
        }
        return $true
    }
    finally {
        $first.Dispose()
        $second.Dispose()
    }
}

$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$fuentesPath = Join-Path $repositoryRootPath 'Fuentes'
if (-not (Test-Path -LiteralPath $fuentesPath -PathType Container)) {
    throw "No existe el directorio esperado: $fuentesPath"
}

$compared = 0
$divergences = [System.Collections.Generic.List[string]]::new()
foreach ($rootDocument in (Get-ChildItem -LiteralPath $repositoryRootPath -File -Filter '*.md' | Sort-Object -Property Name)) {
    $sourceDocument = Join-Path $fuentesPath $rootDocument.Name
    if (-not (Test-Path -LiteralPath $sourceDocument -PathType Leaf)) { continue }

    $compared++
    if (-not (Test-FilesByteEqual -FirstPath $rootDocument.FullName -SecondPath $sourceDocument)) {
        $divergences.Add($rootDocument.Name)
    }
}

if ($divergences.Count -gt 0) {
    Write-Error "Copias divergentes de Fuentes/: $($divergences -join ', ')" -ErrorAction Continue
    exit 1
}

Write-Output "Espejo verificado: $compared documentos Markdown idénticos byte por byte."
exit 0
