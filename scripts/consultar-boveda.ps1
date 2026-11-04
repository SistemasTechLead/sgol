[CmdletBinding()]
param(
    [ValidateSet('Query', 'Update', 'Rebuild', 'Status', 'Explain', 'Path')][string]$Action = 'Query',
    [string]$Query,
    [string]$Target,
    [ValidateRange(100, 4000)][int]$Budget = 600,
    [ValidateRange(1, 3)][int]$Depth = 1,
    [ValidateRange(1000, 16000)][int]$MaxChars = 5000,
    [switch]$Relations,
    [string]$Vault = (Join-Path $env:USERPROFILE 'Documents/Obsidian Vault/SistemaSGOL'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../.local/graphify/boveda'),
    [string]$Graphify = (Join-Path $env:USERPROFILE '.local/bin/graphify.exe')
)

$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Vault -PathType Container)) {
    throw "Boveda no disponible: $Vault. Consultar docs/boveda/ e informar la alternativa; no usar el indice anterior."
}
if (!(Test-Path -LiteralPath $Graphify -PathType Leaf)) {
    throw "Graphify no instalado en $Graphify. Ver docs/boveda/docs/desarrollo/consulta-boveda-graphify.md."
}
if ($Action -in @('Query', 'Explain', 'Path') -and [string]::IsNullOrWhiteSpace($Query)) { throw 'Falta -Query.' }
if ($Action -eq 'Path' -and [string]::IsNullOrWhiteSpace($Target)) { throw 'Falta -Target.' }
$Vault = (Resolve-Path -LiteralPath $Vault).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($OutputDirectory.StartsWith($Vault + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $OutputDirectory -eq $Vault) {
    throw 'El indice debe quedar fuera de la boveda.'
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$graphPath = Join-Path $OutputDirectory 'graph.json'
$manifestPath = Join-Path $OutputDirectory 'manifest.json'

function Assert-FreshGraph {
    if (!(Test-Path -LiteralPath $graphPath) -or !(Test-Path -LiteralPath $manifestPath)) { throw 'Falta el grafo o su manifiesto.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $graph = Get-Content -LiteralPath $graphPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $rootMarker = (Get-Content -LiteralPath (Join-Path $OutputDirectory '.graphify_root') -Raw -Encoding UTF8).Trim()
    if ([IO.Path]::GetFullPath($rootMarker) -ne $Vault) { throw 'El indice pertenece a otra boveda.' }
    # El alcance configurado son notas Markdown visibles; no adjuntos ni .obsidian.
    $notes = @(Get-ChildItem -LiteralPath $Vault -Recurse -File -Filter '*.md')
    $expected = @{}
    foreach ($note in $notes) {
        $relative = $note.FullName.Substring($Vault.Length + 1).Replace('\', '/')
        $expected[$relative] = $true
        $entry = $manifest.PSObject.Properties[$relative]
        $hash = (Get-FileHash -LiteralPath $note.FullName -Algorithm MD5).Hash.ToLowerInvariant()
        if (!$entry -or $entry.Value.ast_hash -ne $hash) { throw "Indice incompleto o desactualizado: $relative. No se consultara." }
    }
    $pages = @($graph.nodes | Where-Object { $_.node_kind -eq 'page' })
    foreach ($page in $pages) {
        if (!$expected.ContainsKey($page.source_file)) { throw "Pagina retirada aun presente en el indice: $($page.source_file)." }
    }
    foreach ($relative in $expected.Keys) {
        if (!($pages | Where-Object { $_.source_file -eq $relative })) { throw "Nota sin nodo de pagina: $relative." }
    }
    return [pscustomobject]@{ Notes = $notes.Count; Nodes = @($graph.nodes).Count; Edges = @($graph.links).Count }
}

# Serializa actualizacion + comprobacion + consulta entre chats que usan este comando.
$lock = $null
$deadline = [DateTime]::UtcNow.AddSeconds(30)
while (!$lock) {
    try { $lock = [IO.File]::Open((Join-Path $OutputDirectory 'sgol-query.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Indice ocupado por otra consulta; reintentar.' }
        Start-Sleep -Milliseconds 200
    }
}
$names = @('GRAPHIFY_OUT', 'GRAPHIFY_BACKEND', 'GRAPHIFY_NO_TIPS', 'PYTHONUTF8', 'PYTHONIOENCODING')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:GRAPHIFY_OUT = $OutputDirectory
    $env:GRAPHIFY_BACKEND = 'none'
    $env:GRAPHIFY_NO_TIPS = '1'
    $env:PYTHONUTF8 = '1'
    $env:PYTHONIOENCODING = 'utf-8'
    if ($Action -ne 'Status') {
        $updateArgs = @('update', $Vault, '--no-cluster')
        if ($Action -eq 'Rebuild') { $updateArgs += '--force' }
        $update = @(& $Graphify @updateArgs 2>&1)
        $code = $LASTEXITCODE
        $update | Set-Content -LiteralPath (Join-Path $OutputDirectory 'ultima-actualizacion.log') -Encoding UTF8
        if ($code -ne 0) { throw "Graphify no pudo actualizar (codigo $code). Ver $OutputDirectory/ultima-actualizacion.log. No se consulta el indice anterior." }
    }
    $stats = Assert-FreshGraph
    Write-Output "Boveda: $Vault | Indice estructural verificado: $($stats.Notes) notas, $($stats.Nodes) nodos, $($stats.Edges) relaciones."
    if ($Action -in @('Update', 'Rebuild', 'Status')) { return }
    $cliArgs = switch ($Action) {
        'Query' { @('query', $Query, '--depth', "$Depth", '--budget', "$Budget", '--graph', $graphPath) }
        'Explain' { @('explain', $Query, '--graph', $graphPath) }
        'Path' { @('path', $Query, $Target, '--graph', $graphPath) }
    }
    $result = @(& $Graphify @cliArgs 2>&1)
    $code = $LASTEXITCODE
    # Una edicion durante la consulta invalida el resultado en vez de certificarlo.
    $null = Assert-FreshGraph
    $text = $result -join [Environment]::NewLine
    if ($Action -eq 'Query' -and !$Relations -and $code -eq 0) {
        $compact = @($result | Where-Object { "$_" -match '^(Graph:|NODE |\[!\]|\.\.\.)' })
        if ($compact.Count -gt 0) {
            $text = ($compact -join [Environment]::NewLine) + "`n[Vista de ubicaciones; usar -Relations o -Action Explain para relaciones. Leer la nota fuente para conocer sus reglas.]"
        }
    }
    if ($text.Length -gt $MaxChars) {
        $text = $text.Substring(0, $MaxChars)
        $lastLine = $text.LastIndexOf([Environment]::NewLine)
        if ($lastLine -gt 0) { $text = $text.Substring(0, $lastLine) }
        $text += "`n[RECORTADO a $MaxChars caracteres; no es cobertura completa. Acotar -Query o usar -Action Explain; ampliar -MaxChars solo si hace falta.]"
    }
    Write-Output $text
    if ($code -eq 3) { Write-Output '[AMBIGUO: repetir con un identificador exacto del resultado.]' }
    elseif ($code -eq 4) { Write-Output '[SIN COINCIDENCIAS: buscar terminos exactos con rg en la boveda; no implica ausencia de la regla.]' }
    elseif ($code -ne 0) { throw "Consulta Graphify fallida (codigo $code)." }
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if ($lock) { $lock.Dispose() }
}
