[CmdletBinding()]
param(
    [ValidateSet('Automated', 'RegressionDiagnostic')][string] $Mode = 'Automated',
    [ValidateRange(2, 2)][int] $Cycles = 2,
    [string] $DotnetPath = 'C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$expectedSdk = [IO.Path]::GetFullPath('C:/Users/siste/.codex/tmp/sgol-sdk-10.0.400/dotnet.exe')
if ([IO.Path]::GetFullPath($DotnetPath) -ne $expectedSdk) { throw 'TECH_FRONT005_SDK_REJECTED' }
$sha = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sha -notmatch '^[a-f0-9]{40}$') { throw 'TECH_FRONT005_HEAD_REJECTED' }
$status = & git -C $repositoryRoot status --porcelain
if ($LASTEXITCODE -ne 0 -or $status) { throw 'TECH_FRONT005_REQUIRES_COMMITTED_HEAD' }
$runRoot = Join-Path $repositoryRoot ('.artifacts/tech-front005/' + [Guid]::CreateVersion7().ToString('N'))
$startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
$saved = @{}
foreach ($name in @('PATH', 'DOTNET_CLI_UI_LANGUAGE', 'SGOL_TECH_FRONT005_CYCLE', 'SGOL_TECH_FRONT005_OUTPUT')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
$captureVariables = @(Get-ChildItem Env: | Where-Object Name -Like 'SGOL_FRONT*_CAPTURE_DIR')
$phases = [Collections.Generic.List[object]]::new()
$browserProject = Join-Path $repositoryRoot 'tests/Sgol.FrontendBrowserTests/Sgol.FrontendBrowserTests.csproj'
$apiProject = Join-Path $repositoryRoot 'tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj'
$evidenceProject = Join-Path $repositoryRoot 'tests/Sgol.EvidenceIntegrationTests/Sgol.EvidenceIntegrationTests.csproj'
$unitProject = Join-Path $repositoryRoot 'tests/Sgol.UnitTests/Sgol.UnitTests.csproj'
# Closed, predeclared selection. Existing fixtures prove backend facts and never stand in for R1..R9.
$apiFilter = @(
    'FullyQualifiedName~PersonAdministrationPersistenceTests',
    'FullyQualifiedName~AccountAdministrationPersistenceTests',
    'FullyQualifiedName~RoleAdministrationPersistenceTests',
    'FullyQualifiedName~AvailabilityAdministrationPersistenceTests',
    'FullyQualifiedName~HostedAuthenticationPostgreSqlTests',
    'FullyQualifiedName~HostedAuthenticationKestrelSmokeTests',
    'FullyQualifiedName~GenerationRequestPersistenceTests',
    'FullyQualifiedName~RecurringGenerationPersistenceTests',
    'FullyQualifiedName~EligibilityEvaluationPersistenceTests',
    'FullyQualifiedName~AutomaticAssignmentPersistenceTests',
    'FullyQualifiedName~AssignmentCorrectionPersistenceTests',
    'FullyQualifiedName~ActiveLoadPersistenceTests',
    'FullyQualifiedName~PlanPublicationPersistenceTests.Front015',
    'FullyQualifiedName~WorkPlanPersistenceTests',
    'FullyQualifiedName~ObligationQueryPersistenceTests.Front017',
    'FullyQualifiedName~ObligationQueryPersistenceTests.Front018',
    'FullyQualifiedName~ObligationConclusionPersistenceTests',
    'FullyQualifiedName~AuditQueryPersistenceTests',
    'FullyQualifiedName~ContinuityPersistenceTests.Front020'
) -join '|'

function Invoke-SelectedTests([string] $project, [string] $filter, [string] $phase, [int] $cycle) {
    # Native output is held only in memory: browser exceptions can contain credentials or signed URLs.
    $discovery = @(& $DotnetPath test $project --no-build --configuration Release --list-tests --filter $filter 2>&1)
    $discoveryExit = $LASTEXITCODE
    $expected = @($discovery | Where-Object { "$_" -match '^    Sgol\.' }).Count
    if ($discoveryExit -ne 0 -or $expected -eq 0) { throw 'TECH_FRONT005_DISCOVERY_FAILED' }
    Write-Output "TECH_FRONT005 cycle=$cycle phase=$phase expected=$expected START"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $native = @(& $DotnetPath test $project --no-build --configuration Release --filter $filter --logger 'console;verbosity=minimal' 2>&1)
    $testExit = $LASTEXITCODE
    $summary = $native | Where-Object { "$_" -match 'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)' } | Select-Object -Last 1
    $valid = $summary -and "${summary}" -match 'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)'
    $failed = if ($valid) { [int]$Matches[1] } else { -1 }
    $passed = if ($valid) { [int]$Matches[2] } else { -1 }
    $skipped = if ($valid) { [int]$Matches[3] } else { -1 }
    $total = if ($valid) { [int]$Matches[4] } else { -1 }
    # Retain only code identifiers and closed diagnostics, never native messages, arguments or values.
    $diagnostics = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $native) {
        $entry = "$line"
        $test = [regex]::Match($entry, '^\s*Failed (Sgol\.[A-Za-z0-9_.]+)')
        if ($test.Success) { [void]$diagnostics.Add('FAILED_TEST ' + $test.Groups[1].Value) }
        $source = [regex]::Match($entry, 'tests[/\\]Sgol\.[A-Za-z.]+[/\\]([A-Za-z0-9.]+\.cs):line\s+(\d+)')
        if ($source.Success) { [void]$diagnostics.Add('SOURCE ' + $source.Groups[1].Value + ':' + $source.Groups[2].Value) }
        $cleanup = [regex]::Match($entry, 'Browser fixture cleanup failed: ([A-Z][A-Za-z0-9_,]{0,512})\s*$')
        if ($cleanup.Success) { [void]$diagnostics.Add('CLEANUP ' + $cleanup.Groups[1].Value) }
        $primary = [regex]::Match($entry, '\b((?:TECH_FRONT005|FRONT007) (?:PRIMARY_FAILURE|BROWSER_FAILURE) [A-Za-z0-9_]+)\s*$')
        if ($primary.Success) { [void]$diagnostics.Add($primary.Groups[1].Value) }
        $location = [regex]::Match($entry, '\b(FRONT007 FAILURE_LINE \d+|TECH_FRONT005 FAILURE_SOURCE [A-Za-z0-9.]+\.cs:\d+)\s*$')
        if ($location.Success) { [void]$diagnostics.Add($location.Groups[1].Value) }
        $exception = [regex]::Match($entry, '\b((?:System|Npgsql|Microsoft\.Playwright|Xunit\.Sdk)\.[A-Za-z]+Exception)\b')
        if ($exception.Success) { [void]$diagnostics.Add('TYPE ' + $exception.Groups[1].Value) }
    }
    $closedDiagnostics = @($diagnostics | Sort-Object)
    $phases.Add([ordered]@{ cycle = $cycle; phase = $phase; project = [IO.Path]::GetRelativePath($repositoryRoot, $project);
        filter = $filter; command = 'dotnet test --no-build --configuration Release --filter <recorded filter>'; sha = $sha;
        expected = $expected; passed = $passed; failed = $failed; skipped = $skipped; total = $total;
        exit = $testExit; durationMs = $timer.ElapsedMilliseconds; failureDiagnostics = $closedDiagnostics })
    $phases | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'phases.json') -Encoding utf8NoBOM
    Write-Output "TECH_FRONT005 cycle=$cycle phase=$phase passed=$passed failed=$failed skipped=$skipped exit=$testExit"
    if ($testExit -ne 0 -or -not $valid -or $failed -ne 0 -or $skipped -ne 0 -or $passed -ne $expected -or $total -ne $expected) {
        foreach ($diagnostic in $closedDiagnostics) { Write-Output "TECH_FRONT005 $diagnostic" }
        throw "TECH_FRONT005_${phase}_INCOMPLETE"
    }
}

try {
    $env:PATH = (Split-Path $DotnetPath) + [IO.Path]::PathSeparator + $env:PATH
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    $env:SGOL_TECH_FRONT005_OUTPUT = $runRoot
    foreach ($variable in $captureVariables) { [Environment]::SetEnvironmentVariable($variable.Name, $null) }
    if ($Mode -eq 'RegressionDiagnostic') {
        Invoke-SelectedTests $browserProject 'Category=FRONT_BROWSER' 'BROWSER_DIAGNOSTIC' 0
        Write-Output "TECH_FRONT005 DIAGNOSTIC_ONLY $runRoot"
        return
    }
    for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
        $env:SGOL_TECH_FRONT005_CYCLE = "$cycle"
        Invoke-SelectedTests $browserProject 'Category=TECH_FRONT005' 'INTEGRAL' $cycle
        Invoke-SelectedTests $browserProject 'Category=FRONT_BROWSER' 'BROWSER_REGRESSION' $cycle
        Invoke-SelectedTests $apiProject $apiFilter 'POSTGRESQL_API' $cycle
        Invoke-SelectedTests $evidenceProject 'FullyQualifiedName~PrivateStorageAndRealScannerKeepNonCleanContentInQuarantine' 'PRIVATE_S3_CLAMAV' $cycle
        Invoke-SelectedTests $unitProject 'FullyQualifiedName~IndicatorApiEndpointTests|FullyQualifiedName~DirectionOverviewApiEndpointTests|FullyQualifiedName~AuditApiEndpointTests|FullyQualifiedName~Front020PresentationTests|FullyQualifiedName~EvidenceReviewEvaluatorTests|FullyQualifiedName~StructuredEvidencePayloadValidatorTests|FullyQualifiedName~ManualGenerationInputTests|FullyQualifiedName~RecurringGenerationTests' 'CONTRACTS_AND_FORMULAS' $cycle
    }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $sha) { throw 'TECH_FRONT005_HEAD_CHANGED' }
    foreach ($cycle in 1..2) {
        foreach ($profile in @('desktop', 'mobile')) {
            $report = Get-Content -LiteralPath (Join-Path $runRoot "cycle-$cycle/$profile/report.json") -Raw | ConvertFrom-Json
            if ($report.sha -ne $sha -or $report.cycle -ne $cycle -or $report.profile -ne $profile -or
                $report.failure -ne 'NONE' -or -not $report.cleanup -or -not $report.https -or $report.steps.Count -ne 9 -or
                (($report.steps.route -join ',') -ne 'R1,R2,R3,R4,R5,R6,R7,R8,R9') -or @($report.steps | Where-Object state -ne 'PASS').Count -ne 0) {
                throw 'TECH_FRONT005_REPORT_INCOMPLETE'
            }
        }
    }
    [ordered]@{ task = 'TECH-FRONT-005'; sha = $sha; cycles = 2; profiles = @('desktop', 'mobile');
        cells = 36; state = 'PASS'; sdk = '10.0.400'; startedAtUtc = $startedAtUtc;
        endedAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); operationTimeZone = 'America/Mexico_City';
        runId = [IO.Path]::GetFileName($runRoot); captureDirectoriesUnset = $true; phases = $phases } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding utf8NoBOM
    Write-Output "TECH_FRONT005 COMPLETE $runRoot"
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    foreach ($variable in $captureVariables) { [Environment]::SetEnvironmentVariable($variable.Name, $variable.Value) }
}
