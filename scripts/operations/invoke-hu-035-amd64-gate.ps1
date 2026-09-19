[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ImageRef,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$GateRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$expectedMigration = '20260914210503_AddRecoveryReconciliation'
$bootstrapNetworkName = 'sgol-staging_private'
$bootstrapContainers = @('sgol-tech-ops-postgres', 'sgol-tech-ops-s3-source', 'sgol-tech-ops-s3-destination')
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)

function Initialize-Hu035LogCapture {
    if ('Hu035LogCapture' -as [type]) { return }
    Add-Type -ErrorAction Stop -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public sealed class Hu035LogCaptureResult {
    public string Status = "START_FAILED";
    public bool Truncated;
    public int? ExitCode;
    public int? ProcessId;
    public bool ProcessExited;
    public string Stdout = "";
    public string Stderr = "";
}

public static class Hu035LogCapture {
    public static Hu035LogCaptureResult Run(
        string executable, string[] arguments, int timeoutMs, int limit) {
        if (timeoutMs < 1 || limit < 1) throw new ArgumentOutOfRangeException();
        var result = new Hu035LogCaptureResult();
        var text = new[] { new StringBuilder(), new StringBuilder() };
        var buffers = new[] { new char[4096], new char[4096] };
        var eof = new bool[2];
        var reads = new Task<int>[2];
        var process = new Process();
        var started = false;
        var watch = Stopwatch.StartNew();
        process.StartInfo = new ProcessStartInfo(executable) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try {
            started = process.Start();
            if (!started) return result;
            result.ProcessId = process.Id;
            result.Status = "TIMEOUT";
            var readers = new[] { process.StandardOutput, process.StandardError };
            for (var i = 0; i < 2; i++)
                reads[i] = readers[i].ReadAsync(buffers[i], 0, 4096);
            while (watch.ElapsedMilliseconds < timeoutMs) {
                for (var i = 0; i < 2; i++) {
                    if (eof[i] || !reads[i].IsCompleted) continue;
                    var count = reads[i].GetAwaiter().GetResult();
                    if (count == 0) { eof[i] = true; continue; }
                    var available = limit - text[i].Length;
                    text[i].Append(buffers[i], 0, Math.Min(count, available));
                    if (count > available) {
                        result.Status = "VOLUME_LIMIT";
                        result.Truncated = true;
                        return result;
                    }
                    reads[i] = readers[i].ReadAsync(buffers[i], 0, 4096);
                }
                if (eof[0] && eof[1] && process.HasExited) {
                    result.ExitCode = process.ExitCode;
                    result.Status = process.ExitCode == 0 ? "OK" : "NONZERO_EXIT";
                    return result;
                }
                System.Threading.Thread.Sleep(10);
            }
        }
        catch { result.Status = started ? "READ_FAILED" : "START_FAILED"; }
        finally {
            if (started) {
                try {
                    if (!process.HasExited) process.Kill(true);
                    result.ProcessExited = process.WaitForExit(500);
                } catch {}
            }
            result.Stdout = text[0].ToString();
            result.Stderr = text[1].ToString();
            try { process.Dispose(); } catch {}
        }
        return result;
    }
}
'@ | Out-Null
}

function Read-Hu035ServerLogs {
    param([string]$Container, [DateTimeOffset]$Since, [DateTimeOffset]$Until)
    Initialize-Hu035LogCapture
    $arguments = @('logs', '--timestamps', '--since', $Since.ToUniversalTime().ToString('O'),
        '--until', $Until.ToUniversalTime().ToString('O'), $Container)
    # Raw streams and process details remain private; never serialize this result.
    return [Hu035LogCapture]::Run('docker', $arguments, 8000, 262144)
}

function Read-Hu035RuntimeState {
    param([string]$Container, [string]$Network)
    Initialize-Hu035LogCapture
    # Raw Docker and SeaweedFS state remains private and is converted only to closed classifications.
    return [pscustomobject]@{
        Network = $Network
        Networks = [Hu035LogCapture]::Run('docker',
            @('inspect', '--format', '{{json .NetworkSettings.Networks}}', $Container), 5000, 65536)
        Topology = [Hu035LogCapture]::Run('docker',
            @('exec', $Container, '/usr/bin/wget', '-qO-', 'http://127.0.0.1:9333/dir/status?pretty=y'),
            5000, 65536)
    }
}

function New-Hu035RuntimeSummary {
    return [ordered]@{
        captureStatus = 'CAPTURE_FAILED'
        networkState = 'UNKNOWN'
        advertisedAddressScope = 'UNKNOWN'
        dataNodeRegistration = 'UNKNOWN'
        writableCapacity = 'UNKNOWN'
    }
}

function ConvertTo-Hu035RuntimeSummary {
    param([object]$Capture)
    $summary = New-Hu035RuntimeSummary
    if ($null -eq $Capture -or $Capture.Networks.Status -ne 'OK' -or
        $Capture.Topology.Status -ne 'OK') { return $summary }
    try {
        $networks = ([string]$Capture.Networks.Stdout) | ConvertFrom-Json
        $topology = ([string]$Capture.Topology.Stdout) | ConvertFrom-Json
        $networkNames = @($networks.PSObject.Properties.Name)
        $finalProperty = $networks.PSObject.Properties[[string]$Capture.Network]
        if ($null -eq $finalProperty) { $summary.networkState = 'FINAL_MISSING' }
        elseif ($networkNames -ccontains 'bridge') { $summary.networkState = 'TRANSIENT_PRESENT' }
        else { $summary.networkState = 'FINAL_ONLY' }

        $nodes = @($topology.Topology.DataCenters | ForEach-Object { $_.Racks } |
            ForEach-Object { $_.DataNodes })
        $summary.dataNodeRegistration = if ($nodes.Count -eq 0) { 'NONE' } else { 'PRESENT' }
        $free = 0L
        if ($null -ne $topology.Topology.Free -and
            [long]::TryParse([string]$topology.Topology.Free, [ref]$free)) {
            $summary.writableCapacity = if ($free -gt 0) { 'POSITIVE' } else { 'ZERO' }
        }

        $finalAddress = if ($null -eq $finalProperty) { '' } else { [string]$finalProperty.Value.IPAddress }
        $finalAliases = if ($null -eq $finalProperty) { @() } else {
            @($finalProperty.Value.Aliases | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        }
        $advertised = @($nodes | ForEach-Object {
            $value = [string]$_.Url
            if ($value -match '^(?<host>[^:]+):\d+$') { $Matches['host'] }
        })
        if ([string]::IsNullOrWhiteSpace($finalAddress) -or $advertised.Count -eq 0) {
            $summary.advertisedAddressScope = 'UNRESOLVED'
        }
        elseif (@($advertised | Where-Object {
                $_ -cne $finalAddress -and $finalAliases -cnotcontains $_
            }).Count -eq 0) {
            $summary.advertisedAddressScope = 'FINAL_NETWORK'
        }
        else { $summary.advertisedAddressScope = 'OUTSIDE_FINAL_NETWORK' }
        $summary.captureStatus = 'OK'
    }
    catch { $summary.captureStatus = 'INVALID' }
    return $summary
}

function New-Hu035ServerSummary {
    param([DateTimeOffset]$Since, [DateTimeOffset]$Until)
    return [ordered]@{
        schemaVersion = 2
        captureStatus = 'CAPTURE_FAILED'
        captureExitCode = $null
        windowStartedAtUtc = $Since.ToUniversalTime().ToString('O')
        windowEndedAtUtc = $Until.ToUniversalTime().ToString('O')
        observation = 'INCOMPLETE'
        emptyCapture = $false
        truncated = $false
        oversizedLines = 0
        unrecognizedLines = 0
        outsideWindowLines = 0
        discardedPartialLines = 0
        duplicateEvents = 0
        eventLimitReached = $false
        parserLimitReached = $false
        events = @()
        runtime = New-Hu035RuntimeSummary
    }
}

function Get-Hu035ServerEventCode {
    param([string]$Body)
    if ($Body.StartsWith('putToFiler: chunked upload failed:', [StringComparison]::Ordinal)) {
        if ($Body.IndexOf('assign volume:', [StringComparison]::Ordinal) -ge 0) { return 'VOLUME_ASSIGNMENT_FAILED' }
        if ($Body.IndexOf('upload chunk:', [StringComparison]::Ordinal) -ge 0) { return 'VOLUME_UPLOAD_FAILED' }
        if ($Body.IndexOf('read chunk at offset', [StringComparison]::Ordinal) -ge 0 -or
            $Body.IndexOf('failed to read small content:', [StringComparison]::Ordinal) -ge 0) {
            return 'REQUEST_BODY_READ_FAILED'
        }
        return 'CHUNK_UPLOAD_FAILED'
    }
    $rules = [ordered]@{
        'putToFiler: CreateEntry returned error:' = 'CREATE_ENTRY_FAILED'
        'putToFiler: failed to create entry for ' = 'CREATE_ENTRY_FAILED'
        'checkConditionalHeaders: error resolving object entry for ' = 'CONDITIONAL_LOOKUP_FAILED'
        'PutObjectHandler: failed to check object lock for bucket ' = 'OBJECT_LOCK_LOOKUP_FAILED'
        'Error checking Object Lock status for bucket ' = 'OBJECT_LOCK_LOOKUP_FAILED'
        'Error checking versioning status for bucket ' = 'VERSIONING_LOOKUP_FAILED'
        'Error re-checking versioning status for bucket ' = 'VERSIONING_LOOKUP_FAILED'
        'Failed to apply bucket default encryption:' = 'ENCRYPTION_LOOKUP_FAILED'
        'PutObjectHandler: putVersionedObject failed with errCode=' = 'VERSIONED_WRITE_FAILED'
    }
    foreach ($prefix in $rules.Keys) {
        if ($Body.StartsWith($prefix, [StringComparison]::Ordinal)) { return $rules[$prefix] }
    }
    return $null
}

function ConvertTo-Hu035ServerSummary {
    param([object]$Capture, [DateTimeOffset]$Since, [DateTimeOffset]$Until)
    $summary = New-Hu035ServerSummary $Since $Until
    $allowed = @('OK', 'START_FAILED', 'READ_FAILED', 'TIMEOUT', 'NONZERO_EXIT', 'VOLUME_LIMIT')
    if ($Capture.Status -notin $allowed) { throw 'INVALID_CAPTURE_STATUS' }
    $summary.captureStatus = $Capture.Status
    if ($null -ne $Capture.ExitCode) { $summary.captureExitCode = [int]$Capture.ExitCode }
    $summary.truncated = [bool]$Capture.Truncated
    $summary.emptyCapture = ([string]$Capture.Stdout).Length -eq 0 -and
        ([string]$Capture.Stderr).Length -eq 0
    $lower = $Since.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff",
        [Globalization.CultureInfo]::InvariantCulture) + '00Z'
    $upper = $Until.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff",
        [Globalization.CultureInfo]::InvariantCulture) + '00Z'
    $pattern = '^(?<second>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})' +
        '(?:\.(?<fraction>\d{1,9}))?Z E\d{4} \d{2}:\d{2}:\d{2}\.\d{6}\s+\d+ ' +
        's3api_object_handlers_put\.go:\d+\] (?<body>.*)$'
    $regex = [regex]::new($pattern, [Text.RegularExpressions.RegexOptions]::CultureInvariant,
        [TimeSpan]::FromMilliseconds(25))
    $events = [Collections.Generic.List[object]]::new()
    $stdoutEvents = @{}
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $incomplete = $Capture.Status -ne 'OK' -or $summary.truncated
    foreach ($channel in @('STDOUT', 'STDERR')) {
        $text = if ($channel -eq 'STDOUT') { [string]$Capture.Stdout } else { [string]$Capture.Stderr }
        $lines = $text.Split([char]10)
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($watch.ElapsedMilliseconds -ge 1000) { $summary.parserLimitReached = $true; break }
            $line = $lines[$index].TrimEnd([char]13)
            if ($line.Length -eq 0) { continue }
            if ($incomplete -and $index -eq $lines.Count - 1 -and
                -not $text.EndsWith("`n", [StringComparison]::Ordinal)) {
                $summary.discardedPartialLines++; continue
            }
            if ($line.Length -gt 16384) { $summary.oversizedLines++; continue }
            $match = $regex.Match($line)
            if (-not $match.Success) { $summary.unrecognizedLines++; continue }
            $second = [datetime]::MinValue
            if (-not [datetime]::TryParseExact($match.Groups['second'].Value,
                "yyyy-MM-dd'T'HH:mm:ss", [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::None, [ref]$second)) {
                $summary.unrecognizedLines++; continue
            }
            $stamp = $match.Groups['second'].Value + '.' +
                $match.Groups['fraction'].Value.PadRight(9, '0') + 'Z'
            if ([string]::CompareOrdinal($stamp, $lower) -lt 0 -or
                [string]::CompareOrdinal($stamp, $upper) -gt 0) {
                $summary.outsideWindowLines++; continue
            }
            $eventCode = Get-Hu035ServerEventCode $match.Groups['body'].Value
            if ($null -eq $eventCode) { $summary.unrecognizedLines++; continue }
            $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
                [Text.Encoding]::UTF8.GetBytes($line)))
            if ($channel -eq 'STDERR' -and $stdoutEvents.ContainsKey($fingerprint)) {
                $stdoutEvents[$fingerprint].channel = 'BOTH'
                $summary.duplicateEvents++; continue
            }
            if ($events.Count -ge 32) { $summary.eventLimitReached = $true; continue }
            $event = [ordered]@{
                occurredAtUtc = $stamp
                channel = $channel
                component = 'S3_PUT'
                eventCode = $eventCode
                correlation = 'TIME_WINDOW_ONLY'
            }
            $events.Add($event)
            if ($channel -eq 'STDOUT') { $stdoutEvents[$fingerprint] = $event }
        }
        if ($summary.parserLimitReached) { break }
    }
    $summary.events = @($events | Sort-Object occurredAtUtc)
    if ($incomplete -or $summary.oversizedLines -gt 0 -or
        $summary.eventLimitReached -or $summary.parserLimitReached) {
        $summary.observation = 'INCOMPLETE'
    } elseif ($summary.emptyCapture) {
        $summary.observation = 'EMPTY'
    } elseif ($events.Count -eq 0) {
        $summary.observation = 'NO_RECOGNIZED_EVENTS'
    } else { $summary.observation = 'RECOGNIZED_EVENTS' }
    return $summary
}

function Write-Hu035ServerSummary {
    param([string]$Path, [object]$Summary)
    [IO.File]::WriteAllText($Path, ($Summary | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}

function Invoke-Hu035Reference {
    param([hashtable]$State, [scriptblock]$Action)
    $State.StartedAt = [DateTimeOffset]::UtcNow
    $State.Failed = $false
    try { . $Action }
    catch {
        $State.Failed = $true
        $State.EndedAt = [DateTimeOffset]::UtcNow
        throw
    }
}

function Invoke-Hu035Finalization {
    param(
        [hashtable]$State,
        [scriptblock]$Cleanup,
        [scriptblock]$Reader = {
            param($container, $since, $until)
            Read-Hu035ServerLogs $container $since $until
        },
        [scriptblock]$Parser = {
            param($capture, $since, $until)
            ConvertTo-Hu035ServerSummary $capture $since $until
        },
        [scriptblock]$Writer = {
            param($path, $summary)
            Write-Hu035ServerSummary $path $summary
        },
        [scriptblock]$RuntimeReader = {
            param($container, $network)
            Read-Hu035RuntimeState $container $network
        },
        [scriptblock]$RuntimeParser = {
            param($capture)
            ConvertTo-Hu035RuntimeSummary $capture
        }
    )
    try {
        if ($State.Failed -eq $true -and $State.StartedAt -is [DateTimeOffset] -and
            $State.EndedAt -is [DateTimeOffset]) {
            $summary = New-Hu035ServerSummary $State.StartedAt $State.EndedAt
            try { $capture = & $Reader $State.Container $State.StartedAt $State.EndedAt }
            catch { $capture = $null }
            if ($null -ne $capture) {
                try { $summary = & $Parser $capture $State.StartedAt $State.EndedAt }
                catch {
                    $summary = New-Hu035ServerSummary $State.StartedAt $State.EndedAt
                    $summary.captureStatus = 'PARSER_FAILED'
                }
            }
            try {
                $runtimeCapture = & $RuntimeReader $State.Container $State.Network
                $summary.runtime = & $RuntimeParser $runtimeCapture
            }
            catch { $summary.runtime = New-Hu035RuntimeSummary }
            try { & $Writer $State.Path $summary | Out-Null }
            catch { Write-Output 'HU035_SERVER_DIAGNOSTIC_WRITE_FAILED' }
        }
    }
    catch { Write-Output 'HU035_SERVER_DIAGNOSTIC_FAILED' }
    finally { . $Cleanup }
}

function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}

function Assert-DockerSuccess([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw $message }
}

function Set-ProcessEnvironmentFromFile([string]$path) {
    foreach ($line in Get-Content -LiteralPath $path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#', [StringComparison]::Ordinal)) { continue }
        $parts = $line.Split('=', 2)
        if ($parts.Count -ne 2) { throw 'Synthetic runtime environment contains an invalid line.' }
        [Environment]::SetEnvironmentVariable($parts[0], $parts[1], 'Process')
    }
}

function Replace-ConnectionHost([string]$name, [string]$hostAddress) {
    $value = [Environment]::GetEnvironmentVariable($name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value) -or $value -notmatch 'Host=sgol-tech-ops-postgres(?:;|$)') {
        throw "Synthetic connection setting is invalid: $name"
    }
    [Environment]::SetEnvironmentVariable($name,
        ($value -replace 'Host=sgol-tech-ops-postgres(?=;|$)', "Host=$hostAddress"), 'Process')
}

function Get-PrivateContainerAddress([string]$container, [string]$network) {
    $address = (& docker inspect --format `
        "{{(index .NetworkSettings.Networks `"$network`").IPAddress}}" $container).Trim()
    Assert-DockerSuccess "Could not resolve the isolated address for $container."
    if ($address -notmatch '^(?:10\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)') {
        throw "$container is not attached to the expected private network."
    }
    return $address
}

function Set-RuntimeStorageEndpoints([string]$path, [string]$sourceAddress,
    [string]$destinationAddress) {
    $endpoints = @{
        'Evidence__Storage__Endpoint' = "http://$sourceAddress`:8333"
        'Backup__Storage__Endpoint' = "http://$destinationAddress`:8333"
        'Replica__Source__Endpoint' = "http://$sourceAddress`:8333"
        'Replica__Destination__Endpoint' = "http://$destinationAddress`:8333"
    }
    $replaced = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $lines = foreach ($line in Get-Content -LiteralPath $path) {
        $parts = $line.Split('=', 2)
        if ($parts.Count -eq 2 -and $endpoints.ContainsKey($parts[0])) {
            [void]$replaced.Add($parts[0])
            "$($parts[0])=$($endpoints[$parts[0]])"
        }
        else { $line }
    }
    if ($replaced.Count -ne $endpoints.Count) {
        throw 'Synthetic runtime environment is missing one or more storage endpoints.'
    }
    [IO.File]::WriteAllLines($path, $lines, $script:utf8WithoutBom)
}

function Format-Hu035ReconcileFailure([string]$CaseName, [int]$ExitCode, [object[]]$Output) {
    $approvedCases = @('positive','identity_missing','identity_additional','link_missing','link_altered',
        'version_changed','count_changed','evidence_missing','evidence_corrupt','evidence_inaccessible',
        'audit_missing','audit_altered','reference_corrupt','rpo_exceeded','rto_exceeded','primary_target',
        'replay_conflict','concurrency')
    $approvedErrors = @('BUILD_IDENTITY_INVALID','RECONCILIATION_ID_INVALID',
        'REFERENCE_ARTIFACT_LOCATION_INVALID','REFERENCE_BACKUP_SNAPSHOT_MISMATCH',
        'REFERENCE_MANIFEST_URI_INVALID','REPLICA_MANIFEST_URI_INVALID','RESTORE_EVIDENCE_INVALID',
        'RESTORE_PRIMARY_TARGET_REJECTED','S3_URI_INVALID','BACKUP_EMPTY','BACKUP_HASH_MISMATCH',
        'BACKUP_MANIFEST_INVALID','CARDINALITY_LIMIT_EXCEEDED','IMMUTABLE_OBJECT_CONFLICT',
        'MANIFEST_INVALID','MANIFEST_VALUE_INVALID','RECONCILIATION_IMMUTABLE_CONFLICT',
        'REFERENCE_CORRUPT','REFERENCE_MISSING','REFERENCE_VERSION_UNSUPPORTED','REPLICA_MANIFEST_INVALID',
        'S3_OBJECT_VERIFICATION_FAILED','S3_WRITE_VERIFICATION_FAILED','S3_OPERATION_FAILED',
        'POSTGRESQL_OPERATION_FAILED','OPERATIONS_COMMAND_FAILED','LOCK_BUSY')
    $safeCase = if ($approvedCases -contains $CaseName) { $CaseName } else { 'UNKNOWN' }
    $safeError = 'UNKNOWN'
    foreach ($line in $Output) {
        $candidate = ([string]$line).Trim()
        if ($approvedErrors -contains $candidate) { $safeError = $candidate }
    }
    return "HU035_RECONCILE_FAILED:CASE=$safeCase`:EXIT=$ExitCode`:ERROR=$safeError"
}

function Invoke-ImageOperation([string[]]$Arguments, [string]$runtimePath, [string]$privatePath,
    [int[]]$ExpectedExitCodes = @(0), [hashtable]$EnvironmentOverrides = @{}) {
    $dockerArguments = @('run','--rm','--platform','linux/amd64','--network',$script:networkName,
        '--env-file',$runtimePath,'--mount',"type=bind,source=$privatePath,target=$privatePath")
    $overridePath = $null
    if ($EnvironmentOverrides.Count -ne 0) {
        $overridePath = Join-Path $privatePath ("override-{0}.env" -f [Guid]::NewGuid().ToString('N'))
        $lines = foreach ($name in ($EnvironmentOverrides.Keys | Sort-Object)) {
            $value = [string]$EnvironmentOverrides[$name]
            if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$' -or $value -match "[`r`n]") {
                throw 'HU-035 environment override is invalid.'
            }
            "$name=$value"
        }
        [IO.File]::WriteAllLines($overridePath, $lines, $script:utf8WithoutBom)
        $dockerArguments += @('--env-file', $overridePath)
    }
    $dockerArguments += @($ImageRef,'dotnet','Sgol.Operations.dll') + $Arguments
    try {
        $output = @(& docker @dockerArguments 2>&1)
        $exitCode = $LASTEXITCODE
        if ($ExpectedExitCodes -notcontains $exitCode) {
            if ($Arguments[0] -eq 'reconcile-functional-restore') {
                throw (Format-Hu035ReconcileFailure `
                    ([Environment]::GetEnvironmentVariable('SGOL_HU035_CASE', 'Process')) $exitCode $output)
            }
            throw "HU-035 image operation failed: $($Arguments[0]); exit=$exitCode"
        }
        return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
    }
    finally {
        if ($null -ne $overridePath -and (Test-Path -LiteralPath $overridePath -PathType Leaf)) {
            Remove-Item -LiteralPath $overridePath -Force
        }
    }
}

function Reset-RestoreDatabase {
    & docker exec $script:containers[0] dropdb --username postgres --force --if-exists sgol_restore *> $null
    Assert-DockerSuccess 'Could not discard the prior isolated restore database.'
    & docker exec $script:containers[0] createdb --username postgres --owner sgol_restore sgol_restore *> $null
    Assert-DockerSuccess 'Could not create a new empty isolated restore database.'
}

function Invoke-Mutation([string]$caseName, [string]$referenceUri, [string]$evidencePath) {
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'mutate', 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_CASE', $caseName, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_REFERENCE_MANIFEST_URI', $referenceUri, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_RESTORE_EVIDENCE_PATH', $evidencePath, 'Process')
    & dotnet test (Join-Path $script:repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-build -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64'
    if ($LASTEXITCODE -ne 0) { throw "HU-035 synthetic mutation failed: $caseName" }
}

function Invoke-ConcurrentReconciliation([object]$caseDescriptor, [string]$runtimePath,
    [string]$privatePath, [string]$evidencePath) {
    $arguments = @('run','--rm','--platform','linux/amd64','--network',$script:networkName,
        '--env-file',$runtimePath,'--mount',"type=bind,source=$privatePath,target=$privatePath",
        $script:ImageRef,'dotnet','Sgol.Operations.dll','reconcile-functional-restore','--reconciliation-id',
        [string]$caseDescriptor.reconciliationId,'--reference-manifest',
        [string]$caseDescriptor.referenceManifestUri,'--restore-evidence',$evidencePath)
    $processes = foreach ($ordinal in 1..2) {
        $info = [Diagnostics.ProcessStartInfo]::new('docker')
        $info.UseShellExecute = $false
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $info
        if (-not $process.Start()) { throw 'Could not start a concurrent HU-035 reconciliation.' }
        $process
    }
    $matched = 0
    $busy = 0
    foreach ($process in $processes) {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = @($stdout.GetAwaiter().GetResult(), $stderr.GetAwaiter().GetResult()) -join "`n"
        if ($process.ExitCode -eq 0 -and $output -match 'FUNCTIONAL_RECOVERY_MATCHED') { $matched++ }
        elseif ($process.ExitCode -eq 1 -and $output -match 'LOCK_BUSY') { $busy++ }
        else { throw 'Concurrent HU-035 reconciliation returned an unexpected result.' }
        $process.Dispose()
    }
    if ($matched -ne 1 -or $busy -ne 1) {
        throw 'Concurrent HU-035 reconciliation did not expose exactly one LOCK_BUSY result.'
    }
    Start-Sleep -Milliseconds (Get-Random -Minimum 100 -Maximum 301)
    $retry = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
        [string]$caseDescriptor.reconciliationId, '--reference-manifest',
        [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $evidencePath) `
        $runtimePath $privatePath
    if ($retry.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
        throw 'HU-035 bounded concurrency retry did not replay the immutable result.'
    }
}

if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64 -or
    -not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)) {
    throw 'AMD64_LINUX_HOST_REQUIRED: HU-035 must run on the native x64 Linux PR runner.'
}
if (-not (Test-AbsolutePath $GateRoot)) { throw 'GateRoot must be an absolute path outside the repository.' }
$gatePath = [IO.Path]::GetFullPath($GateRoot)
if ($gatePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw 'GateRoot must remain outside the repository.'
}
if (Test-Path -LiteralPath $gatePath) {
    if (-not (Test-Path -LiteralPath $gatePath -PathType Container) -or
        (Get-ChildItem -LiteralPath $gatePath -Force | Select-Object -First 1)) {
        throw 'GateRoot must be a new or empty directory.'
    }
}
else { New-Item -ItemType Directory -Path $gatePath | Out-Null }
$identityBytes = [Text.Encoding]::UTF8.GetBytes($gatePath)
$identityHash = [Security.Cryptography.SHA256]::HashData($identityBytes)
$runIdentity = ([Convert]::ToHexString($identityHash)).ToLowerInvariant().Substring(0, 12)
$networkName = "sgol-hu035-$runIdentity-private"
$containers = @("sgol-hu035-$runIdentity-postgres", "sgol-hu035-$runIdentity-s3-source",
    "sgol-hu035-$runIdentity-s3-destination")
$allCleanupContainers = @($bootstrapContainers + $containers)

$inspection = (& docker image inspect $ImageRef | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $inspection.Architecture -ne 'amd64' -or $inspection.Os -ne 'linux') {
    throw 'HU-035 requires an already loaded immutable linux/amd64 image.'
}

$runtimeDirectory = Join-Path $gatePath 'runtime-private'
$privateDirectory = Join-Path $gatePath 'evidence-private'
$publicDirectory = Join-Path $gatePath 'evidence-public'
$descriptorPath = Join-Path $privateDirectory 'descriptor.json'
$restoreEvidencePath = Join-Path $privateDirectory 'restore-evidence.json'
$publicResultPath = Join-Path $publicDirectory 'hu-035-result.json'
$prepareTrx = Join-Path $publicDirectory 'hu-035-prepare.trx'
$verifyTrx = Join-Path $publicDirectory 'hu-035-verify.trx'
$startedAt = [DateTimeOffset]::UtcNow
$stage = 'PROVISION'
$result = 'FAILED'
$referenceDiagnostic = @{ Failed = $false; StartedAt = $null; EndedAt = $null }
$environmentNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
New-Item -ItemType Directory -Path $privateDirectory, $publicDirectory | Out-Null

try {
    & docker network create --driver bridge --internal $networkName *> $null
    Assert-DockerSuccess 'Could not create the run-scoped HU-035 private network.'
    & (Join-Path $PSScriptRoot 'new-tech-ops-synthetic-environment.ps1') `
        -ImageRef $ImageRef -OutputDirectory $runtimeDirectory `
        -StorageNetworkName $networkName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'HU-035 synthetic infrastructure provisioning failed.' }
    $runtimePath = Join-Path $runtimeDirectory 'runtime.env'
    & docker compose --env-file $runtimePath -f (Join-Path $repositoryRoot 'deploy/staging/compose.yaml') `
        rm --force --stop migrate *> $null
    Assert-DockerSuccess 'Could not remove the bootstrap-only migrate container.'
    for ($index = 0; $index -lt $bootstrapContainers.Count; $index++) {
        & docker container rename $bootstrapContainers[$index] $containers[$index]
        Assert-DockerSuccess 'Could not scope a HU-035 synthetic container name.'
        if ($index -eq 0) {
            # Only PostgreSQL moves; SeaweedFS started on the definitive network.
            & docker network connect --alias $bootstrapContainers[$index] $networkName $containers[$index]
            Assert-DockerSuccess 'Could not attach a HU-035 synthetic container to its run-scoped network.'
            & docker network disconnect $bootstrapNetworkName $containers[$index]
            Assert-DockerSuccess 'Could not detach a HU-035 synthetic container from the bootstrap network.'
        }
    }
    & docker network rm $bootstrapNetworkName *> $null
    Assert-DockerSuccess 'Could not remove the bootstrap network.'
    Set-ProcessEnvironmentFromFile $runtimePath
    $containerPrimaryConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Sgol', 'Process')
    foreach ($line in Get-Content -LiteralPath $runtimePath) {
        if ($line -match '^([^#=]+)=') { [void]$environmentNames.Add($Matches[1]) }
    }

    $postgresAddress = Get-PrivateContainerAddress $containers[0] $networkName
    $sourceAddress = Get-PrivateContainerAddress $containers[1] $networkName
    $destinationAddress = Get-PrivateContainerAddress $containers[2] $networkName
    Replace-ConnectionHost 'ConnectionStrings__Sgol' $postgresAddress
    Replace-ConnectionHost 'Backup__PostgreSql__ConnectionString' $postgresAddress
    Replace-ConnectionHost 'Restore__PostgreSql__ConnectionString' $postgresAddress
    Set-RuntimeStorageEndpoints $runtimePath $sourceAddress $destinationAddress
    [Environment]::SetEnvironmentVariable('Evidence__Storage__Endpoint', "http://$sourceAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Backup__Storage__Endpoint', "http://$destinationAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Replica__Source__Endpoint', "http://$sourceAddress`:8333", 'Process')
    [Environment]::SetEnvironmentVariable('Replica__Destination__Endpoint', "http://$destinationAddress`:8333", 'Process')

    $pgDumpWrapper = Join-Path $privateDirectory 'pg-dump-wrapper.sh'
    $ageWrapper = Join-Path $privateDirectory 'age-wrapper.sh'
    $wrapperTemplate = @'
#!/bin/sh
exec docker run --rm --platform linux/amd64 --network __NETWORK__ --env-file '__RUNTIME__' \
  --env PGHOST --env PGPORT --env PGDATABASE --env PGUSER --env PGPASSWORD \
  --env PGCONNECT_TIMEOUT --env PGSSLMODE --mount type=bind,source=/tmp,target=/tmp \
  __IMAGE__ __COMMAND__ "$@"
'@
    $pgDumpContent = $wrapperTemplate.Replace('__NETWORK__', $networkName).Replace(
        '__RUNTIME__', $runtimePath).Replace('__IMAGE__', $ImageRef).Replace(
        '__COMMAND__', '/usr/bin/pg_dump')
    $ageContent = $wrapperTemplate.Replace('docker run --rm --platform',
        'docker run --rm --interactive --platform').Replace('__NETWORK__', $networkName).Replace(
        '__RUNTIME__', $runtimePath).Replace('__IMAGE__', $ImageRef).Replace(
        '__COMMAND__', '/usr/bin/age')
    [IO.File]::WriteAllText($pgDumpWrapper, $pgDumpContent, $utf8WithoutBom)
    [IO.File]::WriteAllText($ageWrapper, $ageContent, $utf8WithoutBom)
    & chmod 700 $pgDumpWrapper $ageWrapper
    Assert-DockerSuccess 'Could not protect HU-035 process wrappers.'
    [Environment]::SetEnvironmentVariable('Backup__PgDumpPath', $pgDumpWrapper, 'Process')
    [Environment]::SetEnvironmentVariable('Backup__AgePath', $ageWrapper, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_DESCRIPTOR_PATH', $descriptorPath, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PUBLIC_RESULT_PATH', $publicResultPath, 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'prepare', 'Process')
    [Environment]::SetEnvironmentVariable('SGOL_HU035_AMD64_TESTS', 'true', 'Process')

    $stage = 'REFERENCE'
    . Invoke-Hu035Reference -State $referenceDiagnostic -Action {
    $referenceDiagnostic.Container = $containers[2]
    $referenceDiagnostic.Network = $networkName
    $referenceDiagnostic.Path = Join-Path $publicDirectory 'hu-035-server-diagnostic.json'
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-restore -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64' `
        --logger "trx;LogFileName=$prepareTrx"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $descriptorPath -PathType Leaf)) {
        throw 'HU-035 reference preparation failed.'
    }
    $descriptor = Get-Content -Raw -LiteralPath $descriptorPath | ConvertFrom-Json
    if ($descriptor.kind -ne 'SGOL_HU035_AMD64_DESCRIPTOR' -or
        $descriptor.expectedMigration -ne $expectedMigration -or $descriptor.cases.Count -ne 18) {
        throw 'HU-035 descriptor is invalid or the approved matrix is incomplete.'
    }
    }

    $stage = 'REFERENCE_READY'
    foreach ($caseDescriptor in $descriptor.cases) {
        $complete = Invoke-ImageOperation @('complete-functional-reference', '--reconciliation-id',
            [string]$caseDescriptor.reconciliationId, '--reference', [string]$caseDescriptor.referenceManifestUri,
            '--backup-manifest', [string]$caseDescriptor.backupManifestUri,
            '--replica-manifest', [string]$caseDescriptor.replicaManifestUri) $runtimePath $privateDirectory
        if ($complete.Output -notcontains 'FUNCTIONAL_REFERENCE_READY') {
            throw "HU-035 reference completion failed: $($caseDescriptor.name)"
        }
        if ($caseDescriptor.name -eq 'positive') {
            $associationReplay = Invoke-ImageOperation @('complete-functional-reference', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference', [string]$caseDescriptor.referenceManifestUri,
                '--backup-manifest', [string]$caseDescriptor.backupManifestUri,
                '--replica-manifest', [string]$caseDescriptor.replicaManifestUri) $runtimePath $privateDirectory
            if ($associationReplay.Output -notcontains 'FUNCTIONAL_REFERENCE_READY') {
                throw 'HU-035 reference association replay failed.'
            }
        }
    }

    $stage = 'MATRIX'
    foreach ($caseDescriptor in $descriptor.cases) {
        Reset-RestoreDatabase
        $caseEvidencePath = Join-Path $privateDirectory "$($caseDescriptor.name)-restore-evidence.json"
        $restore = Invoke-ImageOperation @('verify-postgresql-backup', '--manifest',
            [string]$caseDescriptor.backupManifestUri) $runtimePath $privateDirectory
        $restoreJson = $restore.Output | Where-Object {
            $_ -match '^\{"kind":"SGOL_TECHNICAL_RESTORE_EVIDENCE"'
        } | Select-Object -Last 1
        if ([string]::IsNullOrWhiteSpace($restoreJson)) {
            throw "HU-035 restore evidence is missing: $($caseDescriptor.name)"
        }
        [IO.File]::WriteAllText($caseEvidencePath, $restoreJson, $utf8WithoutBom)
        Invoke-Mutation $caseDescriptor.name $caseDescriptor.referenceManifestUri $caseEvidencePath

        $overrides = @{}
        $expectedExits = @(0)
        if ($caseDescriptor.name -in @('identity_missing','identity_additional','link_missing','link_altered',
            'version_changed','count_changed','evidence_missing','evidence_corrupt','evidence_inaccessible',
            'audit_missing','audit_altered','rpo_exceeded','rto_exceeded')) {
            $expectedExits = @(2)
        }
        elseif ($caseDescriptor.name -in @('reference_corrupt','primary_target')) { $expectedExits = @(1) }
        if ($caseDescriptor.name -eq 'evidence_inaccessible') {
            $overrides['Replica__Destination__Endpoint'] = 'http://127.0.0.1:1'
        }
        elseif ($caseDescriptor.name -eq 'primary_target') {
            $overrides['Restore__PostgreSql__ConnectionString'] = $containerPrimaryConnection
        }

        if ($caseDescriptor.name -eq 'concurrency') {
            Invoke-ConcurrentReconciliation $caseDescriptor $runtimePath $privateDirectory $caseEvidencePath
            continue
        }
        $reconcile = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
            [string]$caseDescriptor.reconciliationId, '--reference-manifest',
            [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
            $runtimePath $privateDirectory $expectedExits $overrides
        if ($expectedExits -contains 2 -and $reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_NOT_MATCHED') {
            throw "HU-035 negative reconciliation was not visible: $($caseDescriptor.name)"
        }
        if ($caseDescriptor.name -eq 'positive') {
            if ($reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 positive reconciliation did not match.'
            }
            $replay = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference-manifest',
                [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
                $runtimePath $privateDirectory
            if ($replay.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 identical reconciliation replay failed.'
            }
        }
        elseif ($caseDescriptor.name -eq 'replay_conflict') {
            if ($reconcile.Output -notcontains 'FUNCTIONAL_RECOVERY_MATCHED') {
                throw 'HU-035 conflict prerequisite did not match.'
            }
            Invoke-Mutation 'replay_conflict' $caseDescriptor.referenceManifestUri $caseEvidencePath
            $conflict = Invoke-ImageOperation @('reconcile-functional-restore', '--reconciliation-id',
                [string]$caseDescriptor.reconciliationId, '--reference-manifest',
                [string]$caseDescriptor.referenceManifestUri, '--restore-evidence', $caseEvidencePath) `
                $runtimePath $privateDirectory @(1)
            if (($conflict.Output -join "`n") -notmatch 'RECONCILIATION_IMMUTABLE_CONFLICT') {
                throw 'HU-035 conflicting replay was not rejected explicitly.'
            }
        }
        if ($caseDescriptor.name -in @('evidence_missing','evidence_corrupt')) {
            Invoke-Mutation 'restore_object' $caseDescriptor.referenceManifestUri $caseEvidencePath
        }
    }

    $stage = 'APPROVE'
    [Environment]::SetEnvironmentVariable('SGOL_HU035_PHASE', 'verify', 'Process')
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --no-build -p:SGOL_HU035_AMD64_TESTS=true --filter 'Category=Hu035Amd64' `
        --logger "trx;LogFileName=$verifyTrx"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $publicResultPath -PathType Leaf)) {
        throw 'HU-035 result verification and approval failed.'
    }

    $forbidden = '(?i)(password\s*=|secretkey|accesskey|connectionstrings|runtime-private|s3://|signed[ -]?url|private key)'
    foreach ($file in Get-ChildItem -LiteralPath $publicDirectory -File) {
        if (Select-String -LiteralPath $file.FullName -Pattern $forbidden -Quiet) {
            throw "HU-035 public evidence contains forbidden material: $($file.Name)"
        }
    }
    $stage = 'COMPLETE'
    $result = 'SUCCEEDED'
}
finally {
    # Only initialized state and a literal block are evaluated before entering the guard.
    . Invoke-Hu035Finalization -State $referenceDiagnostic -Cleanup {
    $cleanupFailed = $false
    foreach ($container in $allCleanupContainers) {
        & docker container inspect $container *> $null
        if ($LASTEXITCODE -eq 0) {
            & docker container rm --force $container *> $null
            if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
        }
    }
    foreach ($network in @($networkName, $bootstrapNetworkName)) {
        & docker network inspect $network *> $null
        if ($LASTEXITCODE -eq 0) {
            & docker network rm $network *> $null
            if ($LASTEXITCODE -ne 0) { $cleanupFailed = $true }
        }
    }
    try {
        if (Test-Path -LiteralPath $runtimeDirectory) { Remove-Item -LiteralPath $runtimeDirectory -Recurse -Force }
        if (Test-Path -LiteralPath $privateDirectory) { Remove-Item -LiteralPath $privateDirectory -Recurse -Force }
    }
    catch { $cleanupFailed = $true }
    if ($cleanupFailed) {
        $stage = 'CLEANUP'
        $result = 'FAILED'
    }
    $summary = [ordered]@{
        kind = 'SGOL_HU035_AMD64_GATE'
        startedAt = $startedAt
        completedAt = [DateTimeOffset]::UtcNow
        imageRef = $ImageRef
        hostArchitecture = 'x86-64'
        syntheticOnly = $true
        stage = $stage
        result = $result
    }
    [IO.File]::WriteAllText((Join-Path $publicDirectory 'hu-035-summary.json'),
        ($summary | ConvertTo-Json -Depth 3), $utf8WithoutBom)
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    foreach ($name in @('SGOL_HU035_DESCRIPTOR_PATH','SGOL_HU035_PUBLIC_RESULT_PATH','SGOL_HU035_PHASE',
        'SGOL_HU035_AMD64_TESTS','SGOL_HU035_CASE','SGOL_HU035_REFERENCE_MANIFEST_URI',
        'SGOL_HU035_RESTORE_EVIDENCE_PATH','Continuity__ReplicaManifestUri')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    }
}

if ($result -ne 'SUCCEEDED') { throw "HU-035 AMD64 gate failed at stage $stage." }
Write-Output "PASS: native AMD64 HU-035 functional recovery gate. PublicEvidence=$publicDirectory"
