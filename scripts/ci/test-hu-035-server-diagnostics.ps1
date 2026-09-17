[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$gatePath = Join-Path $PSScriptRoot '../operations/invoke-hu-035-amd64-gate.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($gatePath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'GATE_PARSE_FAILED' }
$script:assertions = 0
function Assert-True([bool]$Condition, [string]$Code) {
    if (-not $Condition) { throw $Code }
    $script:assertions++
}
function Assert-Keys($Value, [string[]]$Expected) {
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    Assert-True (($actual -join '|') -ceq (($Expected | Sort-Object) -join '|')) 'PUBLIC_PROPERTIES_CHANGED'
}
$names = @('Initialize-Hu035LogCapture', 'Read-Hu035ServerLogs', 'New-Hu035ServerSummary',
    'ConvertTo-Hu035ServerSummary', 'Write-Hu035ServerSummary', 'Invoke-Hu035Reference', 'Invoke-Hu035Finalization')
foreach ($name in $names) {
    $definition = @($ast.FindAll({ param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $false))
    Assert-True ($definition.Count -eq 1) 'DIAGNOSTIC_FUNCTION_MISSING'
    . ([scriptblock]::Create($definition[0].Extent.Text))
}
$mainTry = @($ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.TryStatementAst]
})[-1]
$finalCalls = @($mainTry.Finally.FindAll({ param($node)
    $node -is [Management.Automation.Language.CommandAst] -and
    $node.GetCommandName() -eq 'Invoke-Hu035Finalization'
}, $false))
Assert-True ($finalCalls.Count -eq 1) 'FINALIZATION_NOT_INTEGRATED'
Assert-True ($finalCalls[0].InvocationOperator -eq [Management.Automation.Language.TokenKind]::Dot) 'FINALIZATION_SCOPE_CHANGED'
# No added Join-Path or indexing expressions may run before the finalizer guard.
Assert-True (($finalCalls[0].CommandElements[1..3].Extent.Text -join '|') -eq
    '-State|$referenceDiagnostic|-Cleanup') 'UNSAFE_FINALIZATION_ARGUMENTS'
Assert-True ($finalCalls[0].CommandElements.Count -eq 5) 'UNEXPECTED_FINALIZATION_ARGUMENT'
$cleanupAst = $finalCalls[0].CommandElements[4].ScriptBlock
$existingCleanup = [scriptblock]::Create($cleanupAst.EndBlock.Extent.Text)
$referenceCalls = @($mainTry.Body.FindAll({ param($node)
    $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-Hu035Reference'
}, $false))
Assert-True ($referenceCalls.Count -eq 1) 'REFERENCE_NOT_INTEGRATED'
Assert-True ($referenceCalls[0].InvocationOperator -eq [Management.Automation.Language.TokenKind]::Dot) 'REFERENCE_SCOPE_CHANGED'

$since = [DateTimeOffset]::Parse('2026-09-17T19:41:00Z')
$until = [DateTimeOffset]::Parse('2026-09-17T19:44:00Z')
$sentinel = 'PRIVATE_SENTINEL'
$chunk = 'putToFiler: chunked upload failed: upload chunk: upload PRIVATE_SENTINEL 20 bytes to http://PRIVATE_SENTINEL/object: PRIVATE_SENTINEL'
$entry = 'putToFiler: CreateEntry returned error: AccessKey=PRIVATE_SENTINEL'
$entryOuter = 'putToFiler: failed to create entry for PRIVATE_SENTINEL: PRIVATE_SENTINEL'
function New-Line([string]$Stamp, [string]$Body) {
    return "$Stamp E0917 19:43:11.123456     1 s3api_object_handlers_put.go:650] $Body"
}
function New-Capture([string]$Stdout = '', [string]$Stderr = '', [string]$Status = 'OK', [bool]$Truncated = $false) {
    return [pscustomobject]@{
        Status = $Status; ExitCode = if ($Status -eq 'OK') { 0 } else { $null }
        Truncated = $Truncated; Stdout = $Stdout; Stderr = $Stderr
    }
}
foreach ($fraction in @('', '.1', '.1234567', '.123456789')) {
    $line = New-Line "2026-09-17T19:43:11${fraction}Z" $chunk
    $result = ConvertTo-Hu035ServerSummary (New-Capture -Stderr "$line`n") $since $until
    Assert-True ($result.events.Count -eq 1) 'TIMESTAMP_REJECTED'
    $expectedFraction = $fraction.TrimStart('.').PadRight(9, '0')
    Assert-True ($result.events[0].occurredAtUtc -eq "2026-09-17T19:43:11.${expectedFraction}Z") 'TIMESTAMP_PRECISION_LOST'
    Assert-True ($result.events[0].channel -eq 'STDERR') 'CHANNEL_LOST'
}
$edge = [DateTimeOffset]::Parse('2026-09-17T19:43:11.1234567Z')
$line = New-Line '2026-09-17T19:43:11.123456789Z' $chunk
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stdout "$line`n") $since $edge
Assert-True ($result.events.Count -eq 0 -and $result.outsideWindowLines -eq 1) 'NANOSECOND_WINDOW_ROUNDED'
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stdout "$line`r`n" -Stderr "$line`n") $since $until
Assert-True ($result.events.Count -eq 1) 'CROSS_CHANNEL_DUPLICATE'
Assert-True ($result.events[0].channel -eq 'BOTH' -and $result.duplicateEvents -eq 1) 'DUPLICATE_CHANNEL_LOST'
Assert-True ($result.events[0].correlation -eq 'TIME_WINDOW_ONLY') 'EXACT_CORRELATION_INVENTED'
$secondLine = New-Line '2026-09-17T19:43:12.000000001Z' $entry
$thirdLine = New-Line '2026-09-17T19:43:13.000000001Z' $entryOuter
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stdout "$line`n" -Stderr "$secondLine`n$thirdLine`n") $since $until
Assert-True ($result.events.Count -eq 3) 'DISTINCT_EVENTS_LOST'
Assert-True ($result.events[1].eventCode -eq 'CREATE_ENTRY_FAILED' -and
    $result.events[2].eventCode -eq 'CREATE_ENTRY_FAILED') 'CREATE_ENTRY_PATTERN_MISSING'
$json = $result | ConvertTo-Json -Depth 5
foreach ($private in @($sentinel, 'http://', 'AccessKey', 's3api_object_handlers_put', 'fingerprint')) {
    Assert-True (-not $json.Contains($private)) 'SENSITIVE_DATA_PUBLISHED'
}
$summaryKeys = @('schemaVersion', 'captureStatus', 'captureExitCode', 'windowStartedAtUtc', 'windowEndedAtUtc',
    'observation', 'emptyCapture', 'truncated', 'oversizedLines', 'unrecognizedLines', 'outsideWindowLines',
    'discardedPartialLines', 'duplicateEvents', 'eventLimitReached', 'parserLimitReached', 'events')
$eventKeys = @('occurredAtUtc', 'channel', 'component', 'eventCode', 'correlation')
$public = $json | ConvertFrom-Json
Assert-Keys $public $summaryKeys
foreach ($event in $public.events) { Assert-Keys $event $eventKeys }
$result = ConvertTo-Hu035ServerSummary (New-Capture) $since $until
Assert-True ($result.observation -eq 'EMPTY') 'EMPTY_NOT_DISTINCT'
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stderr "PRIVATE_SENTINEL`n") $since $until
Assert-True ($result.observation -eq 'NO_RECOGNIZED_EVENTS') 'UNRECOGNIZED_NOT_DISTINCT'
$lookalike = $line.Replace('s3api_object_handlers_put.go', 'unrelated.go')
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stderr "$lookalike`n") $since $until
Assert-True ($result.events.Count -eq 0) 'UNRELATED_EMITTER_MATCHED'
foreach ($status in @('TIMEOUT', 'START_FAILED', 'READ_FAILED', 'NONZERO_EXIT')) {
    $result = ConvertTo-Hu035ServerSummary (New-Capture -Status $status) $since $until
    Assert-True ($result.observation -eq 'INCOMPLETE') 'FAILED_CAPTURE_AS_EMPTY'
    Assert-Keys (($result | ConvertTo-Json -Depth 5) | ConvertFrom-Json) $summaryKeys
}
$result = ConvertTo-Hu035ServerSummary (
    New-Capture -Stderr "$line`n$secondLine" -Status 'VOLUME_LIMIT' -Truncated $true
) $since $until
Assert-True ($result.events.Count -eq 1 -and $result.discardedPartialLines -eq 1) 'PARTIAL_LINE_PARSED'
Assert-True ($result.observation -eq 'INCOMPLETE') 'TRUNCATION_HIDDEN'
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stdout (('x' * 16385) + "`n")) $since $until
Assert-True ($result.oversizedLines -eq 1 -and $result.observation -eq 'INCOMPLETE') 'OVERSIZED_LINE_HIDDEN'
$many = ((1..33 | ForEach-Object { $line }) -join "`n") + "`n"
$result = ConvertTo-Hu035ServerSummary (New-Capture -Stdout $many) $since $until
Assert-True ($result.events.Count -eq 32 -and $result.eventLimitReached) 'EVENT_LIMIT_NOT_ENFORCED'

# Descriptor assignments must survive the actual dot-sourced wrapper.
& {
    $referenceDiagnostic = @{}
    $descriptor = $null
    . Invoke-Hu035Reference -State $referenceDiagnostic -Action {
        $descriptor = [pscustomobject]@{ kind = 'SYNTHETIC'; cases = @(1, 2) }
    }
    Assert-True ($descriptor.kind -eq 'SYNTHETIC' -and $descriptor.cases.Count -eq 2) 'DESCRIPTOR_SCOPE_LOST'
    Assert-True (-not $referenceDiagnostic.Failed) 'REFERENCE_SUCCESS_MARKED_FAILED'
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('sgol-hu035-diagnostic-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testDirectory) | Out-Null
try {
    foreach ($fault in @('NONE', 'READER', 'PARSER', 'WRITER', 'BEFORE_REFERENCE', 'AFTER_REFERENCE', 'CLEANUP_EXIT', 'SUCCESS')) {
        & {
            $referenceDiagnostic = @{ Failed = $false; StartedAt = $null; EndedAt = $null }
            $observed = @{ Reads = 0; Summary = $null; Calls = [Collections.Generic.List[string]]::new() }
            $original = [InvalidOperationException]::new('ORIGINAL_FAILURE')
            $reader = { param($container, $start, $end)
                $observed.Reads++
                $observed.Calls.Add('CAPTURE')
                if ($fault -eq 'READER') { throw 'PRIVATE_SENTINEL' }
                New-Capture
            }
            $parser = { param($capture, $start, $end)
                if ($fault -eq 'PARSER') { throw 'PRIVATE_SENTINEL' }
                ConvertTo-Hu035ServerSummary $capture $start $end
            }
            $writer = { param($path, $summary)
                if ($fault -eq 'WRITER') { throw 'PRIVATE_SENTINEL' }
                $observed.Summary = $summary
            }
            # Exercise the verbatim cleanup ScriptBlock extracted from the gate.
            # Docker is simulated; filesystem targets are fresh, test-owned paths.
            function docker {
                $observed.Calls.Add(($args -join ' '))
                $exit = if ($fault -eq 'CLEANUP_EXIT' -and $args -contains 'rm') { 1 } else { 0 }
                Set-Variable -Name LASTEXITCODE -Value $exit -Scope 1
            }
            $allCleanupContainers = @('synthetic-source', 'synthetic-destination')
            $networkName = 'synthetic-current'
            $bootstrapNetworkName = 'synthetic-old'
            $runtimeDirectory = Join-Path $testDirectory 'absent-runtime'
            $privateDirectory = Join-Path $testDirectory 'absent-private'
            $publicDirectory = $testDirectory
            $environmentNames = @()
            $startedAt = $since
            $ImageRef = 'synthetic-image'
            $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
            $stage = if ($fault -eq 'BEFORE_REFERENCE') { 'PROVISION' } elseif ($fault -eq 'SUCCESS') { 'COMPLETE' } else { 'REFERENCE' }
            $result = if ($fault -eq 'SUCCESS') { 'SUCCEEDED' } else { 'FAILED' }
            $cleanupFailed = 'UNTOUCHED'
            # The original environment cleanup is executed and then restored.
            $envNames = @('SGOL_HU035_DESCRIPTOR_PATH', 'SGOL_HU035_PUBLIC_RESULT_PATH',
                'SGOL_HU035_AMD64_TESTS', 'SGOL_HU035_PHASE', 'SGOL_HU035_CASE',
                'SGOL_HU035_REFERENCE_MANIFEST_URI', 'SGOL_HU035_RESTORE_EVIDENCE_PATH',
                'Continuity__ReplicaManifestUri')
            $saved = @{}
            foreach ($envName in $envNames) { $saved[$envName] = [Environment]::GetEnvironmentVariable($envName, 'Process') }
            try {
                $console = @(& {
                    try {
                        try {
                            if ($fault -eq 'BEFORE_REFERENCE') { throw $original }
                            . Invoke-Hu035Reference -State $referenceDiagnostic -Action {
                                if ($fault -notin @('AFTER_REFERENCE', 'SUCCESS')) { throw $original }
                            }
                            if ($fault -ne 'SUCCESS') { throw $original }
                        }
                        finally {
                            . Invoke-Hu035Finalization -State $referenceDiagnostic -Cleanup $existingCleanup `
                                -Reader $reader -Parser $parser -Writer $writer
                            # These are scalar variables written by the original cleanup.
                            $observed.Stage = $stage
                            $observed.Result = $result
                            $observed.CleanupFailed = $cleanupFailed
                        }
                    }
                    catch { $observed.Exception = $_.Exception }
                } *>&1)
                if ($fault -eq 'SUCCESS') { Assert-True ($null -eq $observed.Exception) 'SUCCESS_BECAME_FAILURE' }
                else { Assert-True ([object]::ReferenceEquals($original, $observed.Exception)) 'ORIGINAL_FAILURE_REPLACED' }
                Assert-True (-not (($console | Out-String).Contains($sentinel))) 'CAPTURE_FAILURE_LEAKED'
                $expectCapture = $fault -notin @('BEFORE_REFERENCE', 'AFTER_REFERENCE', 'SUCCESS')
                Assert-True ($observed.Reads -eq [int]$expectCapture) 'WRONG_REFERENCE_CORRELATION'
                $expectedCalls = @('container inspect synthetic-source', 'container rm --force synthetic-source',
                    'container inspect synthetic-destination', 'container rm --force synthetic-destination',
                    'network inspect synthetic-current', 'network rm synthetic-current',
                    'network inspect synthetic-old', 'network rm synthetic-old')
                if ($expectCapture) { $expectedCalls = @('CAPTURE') + $expectedCalls }
                Assert-True (($observed.Calls -join '|') -eq ($expectedCalls -join '|')) 'EXISTING_CLEANUP_CHANGED'
                $expectedResult = if ($fault -eq 'SUCCESS') { 'SUCCEEDED' } else { 'FAILED' }
                Assert-True ($observed.Result -eq $expectedResult) 'CLEANUP_RESULT_SCOPE_LOST'
                Assert-True ($observed.CleanupFailed -is [bool]) 'CLEANUP_VARIABLE_SCOPE_LOST'
                Assert-True ($observed.CleanupFailed -eq ($fault -eq 'CLEANUP_EXIT')) 'CLEANUP_FAILURE_CHANGED'
                $expectedStage = if ($fault -eq 'CLEANUP_EXIT') { 'CLEANUP' } elseif ($fault -eq 'BEFORE_REFERENCE') {
                    'PROVISION'
                } elseif ($fault -eq 'SUCCESS') { 'COMPLETE' } else { 'REFERENCE' }
                Assert-True ($observed.Stage -eq $expectedStage) 'CLEANUP_STAGE_SCOPE_LOST'
                $persisted = Get-Content -Raw (Join-Path $testDirectory 'hu-035-summary.json') | ConvertFrom-Json
                Assert-True ($persisted.stage -eq $expectedStage -and $persisted.result -eq $expectedResult) 'CLEANUP_SUMMARY_CHANGED'
                if ($null -ne $observed.Summary) {
                    Assert-Keys (($observed.Summary | ConvertTo-Json -Depth 5) | ConvertFrom-Json) $summaryKeys
                }
                if ($fault -eq 'READER') { Assert-True ($observed.Summary.captureStatus -eq 'CAPTURE_FAILED') 'READER_FAILURE_NOT_REPORTED' }
                if ($fault -eq 'PARSER') { Assert-True ($observed.Summary.captureStatus -eq 'PARSER_FAILED') 'PARSER_FAILURE_NOT_REPORTED' }
            }
            finally {
                foreach ($envName in $envNames) { [Environment]::SetEnvironmentVariable($envName, $saved[$envName], 'Process') }
            }
        }
    }

    Initialize-Hu035LogCapture
    $pwsh = (Get-Process -Id $PID).Path
    $capture = [Hu035LogCapture]::Run($pwsh,
        @('-NoProfile', '-Command', '[Console]::Out.Write("OUT"); [Console]::Error.Write("ERR")'), 5000, 1024)
    Assert-True ($capture.Status -eq 'OK' -and $capture.Stdout -eq 'OUT' -and $capture.Stderr -eq 'ERR') 'CHANNEL_CAPTURE_FAILED'
    foreach ($mode in @('TIMEOUT', 'VOLUME_LIMIT')) {
        $command = if ($mode -eq 'TIMEOUT') { 'Start-Sleep -Seconds 30' } else {
            'while ($true) { [Console]::Out.Write("x" * 4096); [Console]::Error.Write("y" * 4096) }'
        }
        $limitMs = if ($mode -eq 'TIMEOUT') { 500 } else { 5000 }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $capture = [Hu035LogCapture]::Run($pwsh, @('-NoProfile', '-Command', $command), $limitMs, 1024)
        Assert-True ($capture.Status -eq $mode) 'CAPTURE_LIMIT_FAILED'
        Assert-True ($watch.Elapsed.TotalSeconds -lt 10) 'CAPTURE_BLOCKED_CALLER'
        Assert-True ($capture.ProcessExited) 'READER_PROCESS_NOT_TERMINATED'
        Assert-True ($null -ne $capture.ProcessId) 'READER_PID_MISSING'
        Assert-True ($null -eq (Get-Process -Id $capture.ProcessId -ErrorAction SilentlyContinue)) 'READER_PROCESS_STILL_RUNNING'
        Assert-True ($capture.Stdout.Length -le 1024 -and $capture.Stderr.Length -le 1024) 'CAPTURE_VOLUME_EXCEEDED'
        if ($mode -eq 'VOLUME_LIMIT') { Assert-True ($capture.Truncated) 'TRUNCATION_NOT_REPORTED' }
    }
    $capture = [Hu035LogCapture]::Run($pwsh, @('-NoProfile', '-Command', 'exit 17'), 5000, 1024)
    Assert-True ($capture.Status -eq 'NONZERO_EXIT' -and $capture.ExitCode -eq 17) 'EXIT_CODE_LOST'
    $capture = [Hu035LogCapture]::Run((Join-Path $testDirectory 'nonexistent-executable'), @(), 500, 1024)
    Assert-True ($capture.Status -eq 'START_FAILED') 'START_FAILURE_NOT_REPORTED'
    $summary = ConvertTo-Hu035ServerSummary (New-Capture -Stderr "$line`n") $since $until
    $output = Join-Path $testDirectory 'diagnostic.json'
    Write-Hu035ServerSummary $output $summary
    $published = [IO.File]::ReadAllText($output)
    Assert-True (-not $published.Contains($sentinel)) 'PUBLISHED_SECRET'
    $document = $published | ConvertFrom-Json
    Assert-Keys $document $summaryKeys
    foreach ($event in $document.events) { Assert-Keys $event $eventKeys }
}
finally {
    # Only these two named files can have been created by this test.
    foreach ($fileName in @('hu-035-summary.json', 'diagnostic.json')) {
        $testFile = Join-Path $testDirectory $fileName
        if ([IO.File]::Exists($testFile)) { [IO.File]::Delete($testFile) }
    }
    [IO.Directory]::Delete($testDirectory, $false)
}
Write-Output "PASS: HU-035 server diagnostics ($script:assertions assertions); gate syntax, privacy, process exit and integrated cleanup."
