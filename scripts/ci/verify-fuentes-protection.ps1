[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$BaseSha,
    [string]$HeadSha
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "Git fallo al ejecutar: git $($Arguments -join ' ')"
    }

    return $output
}

function Assert-AcceptedFile {
    param(
        [Parameter(Mandatory)][object]$Baseline,
        [Parameter(Mandatory)][string]$RepositoryRootPath
    )

    $filePath = Join-Path $RepositoryRootPath ([string]$Baseline.path)
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "No existe la fuente rebaselizada esperada: $($Baseline.path)"
    }

    $actualSha256 = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
    if ($actualSha256 -ne [string]$Baseline.acceptedSha256) {
        throw "La huella SHA-256 de $($Baseline.path) no coincide con la rebaselizacion aprobada."
    }
}

$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$manifestPath = Join-Path $PSScriptRoot 'fuentes-approved-rebaseline.json'
$baseline = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($BaseSha) -xor [string]::IsNullOrWhiteSpace($HeadSha)) {
    throw 'BaseSha y HeadSha deben proporcionarse juntos.'
}

Push-Location $repositoryRootPath
try {
    if ([string]::IsNullOrWhiteSpace($BaseSha)) {
        $statusLines = @(Invoke-Git -Arguments @('status', '--porcelain=v1', '--', 'Fuentes'))
        if ($statusLines.Count -eq 0) {
            Write-Output 'PASS: Fuentes/ no tiene cambios locales.'
            exit 0
        }

        $changedPaths = @($statusLines | ForEach-Object {
            if ($_.Length -lt 4) { throw "Salida inesperada de git status: $_" }
            $statusPath = $_.Substring(3)
            if ($statusPath.StartsWith('"', [StringComparison]::Ordinal)) {
                $statusPath | ConvertFrom-Json
            }
            else {
                $statusPath
            }
        })

        if ($changedPaths.Count -ne 1 -or $changedPaths[0] -ne [string]$baseline.path) {
            throw "Fuentes/ contiene cambios no aprobados: $($changedPaths -join ', ')"
        }

        $indexEntry = @(Invoke-Git -Arguments @('ls-files', '-s', '--', [string]$baseline.path))
        if ($indexEntry.Count -ne 1) {
            throw "No se pudo resolver el blob registrado de $($baseline.path)."
        }

        $indexBlob = ($indexEntry[0] -split '\s+')[1]
        if ($indexBlob -ne [string]$baseline.previousGitBlobSha1) {
            throw "El blob registrado de $($baseline.path) no coincide con el origen de la transicion aprobada."
        }

        Assert-AcceptedFile -Baseline $baseline -RepositoryRootPath $repositoryRootPath
        $workingBlobOutput = @(Invoke-Git -Arguments @('hash-object', '--', [string]$baseline.path))
        $workingBlob = $workingBlobOutput[0]
        if ($workingBlob -ne [string]$baseline.acceptedGitBlobSha1) {
            throw "El blob local de $($baseline.path) no coincide con la rebaselizacion aprobada."
        }

        Write-Output "PASS_REBASELINE_PENDING: cambio local exacto y autorizado en $($baseline.path)."
        exit 0
    }

    $currentHeadOutput = @(Invoke-Git -Arguments @('rev-parse', 'HEAD'))
    if ($currentHeadOutput[0] -ne $HeadSha) {
        throw 'El checkout actual no corresponde a HeadSha; no se puede validar la huella del archivo del pull request.'
    }

    $changedPaths = @(Invoke-Git -Arguments @('diff', '--name-only', $BaseSha, $HeadSha, '--', 'Fuentes'))
    if ($changedPaths.Count -eq 0) {
        Write-Output 'PASS: Fuentes/ no cambia en el pull request.'
        exit 0
    }

    if ($changedPaths.Count -ne 1 -or $changedPaths[0] -ne [string]$baseline.path) {
        throw "El pull request modifica rutas no aprobadas en Fuentes/: $($changedPaths -join ', ')"
    }

    $baseBlobOutput = @(Invoke-Git -Arguments @('rev-parse', "${BaseSha}:$($baseline.path)"))
    $headBlobOutput = @(Invoke-Git -Arguments @('rev-parse', "${HeadSha}:$($baseline.path)"))
    $baseBlob = $baseBlobOutput[0]
    $headBlob = $headBlobOutput[0]
    if ($baseBlob -ne [string]$baseline.previousGitBlobSha1) {
        throw 'La rebaselizacion excepcional ya no parte del blob anterior aprobado.'
    }
    if ($headBlob -ne [string]$baseline.acceptedGitBlobSha1) {
        throw 'El pull request no contiene el blob nuevo aprobado.'
    }

    Assert-AcceptedFile -Baseline $baseline -RepositoryRootPath $repositoryRootPath
    Write-Output "PASS_REBASELINE: transicion excepcional exacta aprobada para $($baseline.path)."
    exit 0
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
finally {
    Pop-Location
}
