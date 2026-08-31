[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$outputPath = Join-Path $repositoryRootPath 'docs\INDICE_IDS.md'
$idPattern = '(?:F\d{2}-)?JP-\d{3}|(?:F\d{2}-)?DEF-\d{3}|TECH-[A-Z]+(?:-[A-Z]+)*-\d{3}|HU-\d{3}|CAP-\d{3}|TAR-\d{4}|CA-\d{3}|CP-\d{3}-[PN]|CAT-\d{3}|NFR-\d{3}|ADR-\d{3}|RN-\d{3}|CV-\d{2}|EP-\d{2}'
$idRegex = [regex]::new("(?<![A-Z0-9-])(?:$idPattern)(?![A-Z0-9-])")
$headingRegex = [regex]::new("^(?<marks>#{1,6})\s+`?(?<id>$idPattern)`?(?=\s|—|–|:|$)")
$canonicalFiles = @{
    ADR  = 'F06_REGISTRO_ADR.md'
    CA   = 'F05_CRITERIOS_DE_ACEPTACION.md'
    CAP  = 'F02_CATALOGO_FUNCIONAL.md'
    CAT  = 'F05_CRITERIOS_DE_ACEPTACION.md'
    CP   = 'F05_CRITERIOS_DE_ACEPTACION.md'
    CV   = 'F07_BACKLOG_DE_IMPLEMENTACION.md'
    EP   = 'F07_BACKLOG_DE_IMPLEMENTACION.md'
    HU   = 'F05_ESPECIFICACION_FUNCIONAL_MVP.md'
    NFR  = 'F06_ARQUITECTURA.md'
    RN   = 'F05_ESPECIFICACION_FUNCIONAL_MVP.md'
    TAR  = 'F05_ESPECIFICACION_FUNCIONAL_MVP.md'
    TECH = 'F07_BACKLOG_DE_IMPLEMENTACION.md'
}

function Get-IdPrefix {
    param([Parameter(Mandatory)][string]$Id)

    if ($Id -match '^(?:F\d{2}-)?JP-') { return 'JP' }
    if ($Id -match '^(?:F\d{2}-)?DEF-') { return 'DEF' }
    if ($Id -match '^TECH-') { return 'TECH' }
    return ($Id -split '-')[0]
}

function Add-Candidate {
    param(
        [System.Collections.Generic.List[object]]$Candidates,
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$File,
        [Parameter(Mandatory)][int]$StartLine,
        [Parameter(Mandatory)][int]$EndLine,
        [Parameter(Mandatory)][string]$Kind
    )

    $prefix = Get-IdPrefix -Id $Id
    $canonicalPriority = if ($canonicalFiles.ContainsKey($prefix) -and $canonicalFiles[$prefix] -eq $File) { 0 } else { 10 }
    $kindPriority = if ($Kind -eq 'Heading') { 0 } else { 1 }
    $Candidates.Add([pscustomobject]@{
        Id       = $Id
        Prefix   = $prefix
        File     = $File
        Start    = $StartLine
        End      = $EndLine
        Priority = $canonicalPriority + $kindPriority
    })
}

$candidates = [System.Collections.Generic.List[object]]::new()
$mentionedIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$markdownFiles = Get-ChildItem -LiteralPath $repositoryRootPath -File -Filter '*.md' | Sort-Object -Property Name

foreach ($file in $markdownFiles) {
    $lines = @(Get-Content -LiteralPath $file.FullName)

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        foreach ($mention in $idRegex.Matches($line)) {
            [void]$mentionedIds.Add($mention.Value)
        }
        $headingMatch = $headingRegex.Match($line)
        if ($headingMatch.Success) {
            $level = $headingMatch.Groups['marks'].Value.Length
            $endLine = $lines.Count
            for ($next = $index + 1; $next -lt $lines.Count; $next++) {
                if ($lines[$next] -match '^(?<marks>#{1,6})\s+' -and $Matches['marks'].Length -le $level) {
                    $endLine = $next
                    break
                }
            }
            while ($endLine -gt ($index + 1) -and [string]::IsNullOrWhiteSpace($lines[$endLine - 1])) {
                $endLine--
            }

            Add-Candidate -Candidates $candidates -Id $headingMatch.Groups['id'].Value -File $file.Name -StartLine ($index + 1) -EndLine $endLine -Kind 'Heading'
            continue
        }

        if (-not $line.TrimStart().StartsWith('|')) { continue }

        $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim().Trim('`').Trim('*') })
        if ($cells.Count -lt 2 -or $cells[0] -match '^:?-{3,}:?$') { continue }

        $firstCellMatch = $idRegex.Match($cells[0])
        $firstCellRemainder = if ($firstCellMatch.Success) { $cells[0].Substring($firstCellMatch.Length).Trim() } else { '' }
        $isSingleDefinitionCell = $firstCellRemainder -eq '' -or $firstCellRemainder.StartsWith('/') -or $firstCellRemainder.StartsWith('—') -or $firstCellRemainder.StartsWith('–')
        if ($firstCellMatch.Success -and $firstCellMatch.Index -eq 0 -and $isSingleDefinitionCell) {
            Add-Candidate -Candidates $candidates -Id $firstCellMatch.Value -File $file.Name -StartLine ($index + 1) -EndLine ($index + 1) -Kind 'Table'

            if ((Get-IdPrefix -Id $firstCellMatch.Value) -eq 'CA') {
                foreach ($match in $idRegex.Matches($line)) {
                    if ((Get-IdPrefix -Id $match.Value) -eq 'CP') {
                        Add-Candidate -Candidates $candidates -Id $match.Value -File $file.Name -StartLine ($index + 1) -EndLine ($index + 1) -Kind 'Table'
                    }
                }
            }
            continue
        }

        if ($cells[0] -match '^\d+$') {
            $secondCellMatch = $idRegex.Match($cells[1])
            if ($secondCellMatch.Success -and $secondCellMatch.Index -eq 0 -and (Get-IdPrefix -Id $secondCellMatch.Value) -eq 'CV') {
                Add-Candidate -Candidates $candidates -Id $secondCellMatch.Value -File $file.Name -StartLine ($index + 1) -EndLine ($index + 1) -Kind 'Table'
            }
        }

        if ($file.Name -eq $canonicalFiles['TECH'] -and $cells.Count -ge 3) {
            $secondCellMatch = $idRegex.Match($cells[1])
            $secondCellRemainder = if ($secondCellMatch.Success) { $cells[1].Substring($secondCellMatch.Length).Trim() } else { '' }
            if ($secondCellMatch.Success -and $secondCellMatch.Index -eq 0 -and $secondCellRemainder -eq '' -and (Get-IdPrefix -Id $secondCellMatch.Value) -eq 'TECH') {
                Add-Candidate -Candidates $candidates -Id $secondCellMatch.Value -File $file.Name -StartLine ($index + 1) -EndLine ($index + 1) -Kind 'Table'
            }
        }
    }
}

$selected = foreach ($group in ($candidates | Group-Object -Property Id)) {
    $ordered = @($group.Group | Sort-Object -Property Priority, File, Start)
    $best = @($ordered | Where-Object Priority -eq $ordered[0].Priority)
    if ($best.Count -ne 1) {
        $locations = ($best | ForEach-Object { "$($_.File):$($_.Start)" }) -join ', '
        throw "No se pudo elegir una definición única para $($group.Name): $locations"
    }
    $best[0]
}

$sorted = @($selected | Sort-Object -Property @{ Expression = 'Prefix' }, @{ Expression = {
    if ($_.Id -match '^(F\d{2})-JP-') { $Matches[1] }
    elseif ($_.Id -match '^(TECH-[A-Z]+(?:-[A-Z]+)*)-\d{3}$') { $Matches[1] }
    else { '' }
} }, @{ Expression = {
    if ($_.Id -match '-(?<number>\d+)(?:-[PN])?$') { [int]$Matches['number'] } else { [int]::MaxValue }
} }, @{ Expression = {
    if ($_.Id -match '-P$') { 0 } elseif ($_.Id -match '-N$') { 1 } else { 0 }
} }, @{ Expression = 'Id' })

$generatedDate = [DateTime]::Now.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
$content = [System.Collections.Generic.List[string]]::new()
$content.Add('# Índice de identificadores')
$content.Add('')
$content.Add("Fecha de generación: $generatedDate")
$content.Add('')
$content.Add('Comando de regeneración: `pwsh -NoProfile -File scripts/ci/generate-id-index.ps1`')
$content.Add('')
$content.Add('El índice apunta a la definición canónica de cada identificador, no a sus menciones.')
$content.Add('')
$content.Add('| ID | Archivo | Línea inicial | Línea final |')
$content.Add('|---|---|---:|---:|')
foreach ($entry in $sorted) {
    $content.Add("| ``$($entry.Id)`` | ``$($entry.File)`` | $($entry.Start) | $($entry.End) |")
}
$content.Add('')

$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}
[IO.File]::WriteAllLines($outputPath, $content, [Text.UTF8Encoding]::new($false))
Write-Output "Índice generado: $($sorted.Count) identificadores en $outputPath"
$selectedIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $sorted) { [void]$selectedIds.Add($entry.Id) }
$unmappedMentions = @($mentionedIds | Where-Object { -not $selectedIds.Contains($_) } | Sort-Object)
if ($unmappedMentions.Count -gt 0) {
    Write-Warning "Menciones sin definición indexable: $($unmappedMentions -join ', ')"
}
