[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $repo '.local/graphify/boveda/graph.json'
$destination = Join-Path $repo '.local/graphify/visualizacion'
& (Join-Path $PSScriptRoot 'consultar-boveda.ps1') -Action Update
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $destination 'graph.json') -Force
$previous = $env:GRAPHIFY_BACKEND
try {
    $env:GRAPHIFY_BACKEND = 'none'
    & (Join-Path $env:USERPROFILE '.local/bin/graphify.exe') export html --graph (Join-Path $destination 'graph.json')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la exportacion HTML de Graphify.' }
    $html = Join-Path $destination 'graph.html'
    if (!(Test-Path -LiteralPath $html)) { throw 'Graphify no produjo graph.html.' }
    Write-Output "Visualizacion lista: $html"
    Write-Output 'Es una instantanea; repetir este comando para actualizarla. La biblioteca visual requiere Internet.'
}
finally { $env:GRAPHIFY_BACKEND = $previous }
