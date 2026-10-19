[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Mensaje
)

$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
function Invoke-SgolGit {
    param([string[]]$GitArguments)
    & git -C $sgolRoot @GitArguments
    if ($LASTEXITCODE -ne 0) { throw "Git fallo: $($GitArguments -join ' ')" }
}

$sgolBranch = (Invoke-SgolGit @('branch', '--show-current')).Trim()
if ($sgolBranch -ne 'master') { throw 'Publica desde master. Revisa primero los cambios de tu rama.' }
$sgolOrigin = (Invoke-SgolGit @('remote', 'get-url', 'origin')).Trim()
if ($sgolOrigin -ne 'https://github.com/SistemasTechLead/sgol.git') {
    throw 'El remoto origin no corresponde a SistemasTechLead/sgol.'
}

Invoke-SgolGit @('fetch', 'origin', 'master')
& git -C $sgolRoot merge-base --is-ancestor origin/master HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'GitHub tiene cambios que faltan aqui. Integralos antes de publicar; no se sobrescribiran.'
}

Invoke-SgolGit @('add', '--all')
& git -C $sgolRoot diff --cached --quiet
if ($LASTEXITCODE -eq 1) {
    Invoke-SgolGit @('commit', '-m', $Mensaje)
} elseif ($LASTEXITCODE -ne 0) {
    throw 'No se pudieron revisar los cambios preparados.'
}
Invoke-SgolGit @('push', 'origin', 'master')
Write-Host 'Proyecto actualizado en https://github.com/SistemasTechLead/sgol'
