[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$id = [Guid]::NewGuid().ToString('N')
$vault = Join-Path $repo ".local/graphify/pruebas/fuentes/$id"
$output = Join-Path $repo ".local/graphify/pruebas/indices/$id"
New-Item -ItemType Directory -Path $vault -Force | Out-Null
$wrapper = Join-Path $PSScriptRoot 'consultar-boveda.ps1'
$options = @{ Vault = $vault; OutputDirectory = $output }
$results = [Collections.Generic.List[string]]::new()
function Assert-Case([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL: $Name" }
    $results.Add("PASS: $Name")
}
function Write-Note([string]$Name, [string]$Body) {
    [IO.File]::WriteAllText((Join-Path $vault $Name), $Body, [Text.UTF8Encoding]::new($false))
}
Write-Note '.graphifyignore' "*`n!*/`n!*.md`n.obsidian/`n"
Write-Note 'Inicio.md' "# Inicio`n[Reglas](reglas.md)`n"
Write-Note 'reglas.md' "# ReglasAlpha`n## CierreAlpha`n[[Inicio]]`n"
$r = & $wrapper @options -Query CierreAlpha
Assert-Case (($r -join '') -match 'NODE CierreAlpha') 'Construccion y consulta'
$stamp = (Get-Item "$output/graph.json").LastWriteTimeUtc
& $wrapper @options -Action Update | Out-Null
Assert-Case ((Get-Item "$output/graph.json").LastWriteTimeUtc -eq $stamp) 'Sin cambios no reescribe'
Write-Note 'reglas.md' "# ReglasBeta`n## CierreBeta`n[[Inicio]]`n"
$rejected = $false
try { & $wrapper @options -Action Status | Out-Null } catch { $rejected = "$_" -match 'Indice incompleto o desactualizado' }
Assert-Case $rejected 'Rechaza indice obsoleto'
$r = & $wrapper @options -Query CierreBeta
Assert-Case (($r -join '') -match 'NODE CierreBeta' -and ($r -join '') -notmatch 'NODE CierreAlpha') 'Actualiza y retira encabezados'
Write-Note 'nueva.md' "# NuevaNota`n[Reglas](reglas.md)`n"
& $wrapper @options -Action Update | Out-Null
$r = & $wrapper @options -Action Explain -Query nueva.md
Assert-Case (($r -join '') -match 'NuevaNota') 'Alta de nota'
Rename-Item -LiteralPath "$vault/nueva.md" -NewName renombrada.md
& $wrapper @options -Action Update | Out-Null
$g = Get-Content "$output/graph.json" -Raw | ConvertFrom-Json
Assert-Case ($g.nodes.source_file -contains 'renombrada.md' -and $g.nodes.source_file -notcontains 'nueva.md') 'Renombrado'
# Solo elimina el archivo creado por esta ejecucion, bajo su directorio exclusivo.
$removed = [IO.Path]::GetFullPath((Join-Path $vault 'renombrada.md'))
if (!$removed.StartsWith($vault + [IO.Path]::DirectorySeparatorChar)) { throw 'Destino fuera de fixture' }
Remove-Item -LiteralPath $removed
& $wrapper @options -Action Update | Out-Null
$g = Get-Content "$output/graph.json" -Raw | ConvertFrom-Json
Assert-Case ($g.nodes.source_file -notcontains 'renombrada.md') 'Retirada'
Write-Note 'ignorado.json' '{"dato":"fixture"}'
& $wrapper @options -Action Update | Out-Null
$g = Get-Content "$output/graph.json" -Raw | ConvertFrom-Json
Assert-Case ($g.nodes.source_file -notcontains 'ignorado.json') 'Excluye adjuntos'
$r = & $wrapper @options -Query zzzNoExiste
Assert-Case (($r -join '') -match 'SIN COINCIDENCIAS') 'Fallback sin coincidencias'
$r = & $wrapper @options -Action Path -Query Inicio.md -Target reglas.md
Assert-Case (($r -join '') -match 'EXTRACTED') 'Ruta por enlace Markdown'
& $wrapper @options -Action Rebuild | Out-Null
Assert-Case ($LASTEXITCODE -eq 0) 'Reconstruccion explicita'
$rejected = $false
try { & $wrapper -Vault "$vault/no-existe" -OutputDirectory $output -Query prueba | Out-Null } catch { $rejected = "$_" -match 'Boveda no disponible' }
Assert-Case $rejected 'Falla sin boveda'
$results | Set-Content -LiteralPath (Join-Path $output 'resultados.txt') -Encoding UTF8
$results
Write-Output "Evidencia: $output/resultados.txt"
