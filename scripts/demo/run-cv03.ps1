[CmdletBinding()]
param(
    [ValidateSet('Automated')]
    [string] $Mode = 'Automated'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path

if ($repositoryRoot -match '(?i)\\OneDrive\\') {
    throw 'CV-03 demo refuses OneDrive paths.'
}

$requiredRootFiles = @('SGOL.slnx', 'F07_BACKLOG_DE_IMPLEMENTACION.md')
$hasExpectedRootName = [StringComparer]::OrdinalIgnoreCase.Equals(
    (Split-Path -Leaf $repositoryRoot),
    'SGOL')
$hasRequiredRootFiles = -not ($requiredRootFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf)
    })

if (-not $hasExpectedRootName -or -not $hasRequiredRootFiles) {
    throw 'CV-03 demo must run from the canonical SGOL checkout.'
}

$resolvedGitRoot = (& rtk git -C $repositoryRoot rev-parse --show-toplevel).Trim().Replace('/', '\')
if (-not [StringComparer]::OrdinalIgnoreCase.Equals($resolvedGitRoot, $repositoryRoot)) {
    throw 'Git resolved a different worktree.'
}

$previousEicar = $env:SGOL_EVIDENCE_EICAR_TESTS
$previousCommit = $env:CV03_COMMIT
$previousTarget = $env:SGOL_TARGET_ENVIRONMENT
$demoExitCode = 0

try {
    $env:SGOL_EVIDENCE_EICAR_TESTS = 'true'
    $env:CV03_COMMIT = (& rtk git -C $repositoryRoot rev-parse HEAD).Trim()
    $env:SGOL_TARGET_ENVIRONMENT = 'cv03-demo'
    & rtk dotnet run --project (Join-Path $repositoryRoot 'tests\Sgol.Cv03Demo\Sgol.Cv03Demo.csproj') --configuration Release --no-build -- --mode $Mode
    $demoExitCode = $LASTEXITCODE
}
finally {
    $env:SGOL_EVIDENCE_EICAR_TESTS = $previousEicar
    $env:CV03_COMMIT = $previousCommit
    $env:SGOL_TARGET_ENVIRONMENT = $previousTarget
}

if ($demoExitCode -ne 0) {
    [Console]::Error.WriteLine("CV-03 demo failed with exit code $demoExitCode.")
    exit $demoExitCode
}
