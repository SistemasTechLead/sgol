$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $taskRoot 'src/Modules/Execution'
$project = Join-Path $projectRoot 'Sgol.Execution.csproj'
$fixtureName = 'sgol-build-input-check-' + [Guid]::NewGuid().ToString('N') + '.cs'
$fixtures = @()
try {
    # Real generated files inside a project must stay excluded in both output modes.
    foreach ($directory in @('artifacts', 'bin', 'obj')) {
        $fixtureDirectory = Join-Path $projectRoot $directory
        New-Item -ItemType Directory -Force -Path $fixtureDirectory | Out-Null
        $fixture = Join-Path $fixtureDirectory $fixtureName
        '#error Generated output must never be compiled as source.' | Set-Content -LiteralPath $fixture
        $fixtures += $fixture
    }
    foreach ($mode in @('default', 'artifacts')) {
        $arguments = @('msbuild', $project, '-nologo', '-getItem:Compile')
        if ($mode -eq 'artifacts') {
            $arguments += @('-p:UseArtifactsOutput=true', ('-p:ArtifactsPath=' + (Join-Path $taskRoot 'artifacts/build-input-check')))
        }
        $output = & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "MSBuild no pudo evaluar el modo $mode." }
        $items = ($output | Out-String | ConvertFrom-Json).Items.Compile
        $unexpected = @($items | Where-Object { $_.Identity -match '(^|[\\/])(artifacts|bin|obj)[\\/]' })
        if ($unexpected.Count) { throw "El modo $mode incluye archivos generados: $($unexpected.Identity -join ', ')" }
        if (!($items | Where-Object { $_.Identity -match '^Contracts[\\/].*\.cs$' })) {
            throw "El modo $mode perdió los contratos reales del proyecto."
        }
        Write-Host "PASS $mode : archivos generados excluidos y contratos conservados."
    }
} finally {
    foreach ($fixture in $fixtures) {
        if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture }
    }
}
