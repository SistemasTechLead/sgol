using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sgol.Continuity.Contracts;

public static class ContinuityAuthorization
{
    public const string View = "PER-CONTINUIDAD-VER";
}

public static class RecoveryReconciliationStatuses
{
    public const string Requested = "REQUESTED";
    public const string ReferenceCapturing = "REFERENCE_CAPTURING";
    public const string ReferenceReady = "REFERENCE_READY";
    public const string RestoreStarted = "RESTORE_STARTED";
    public const string Reconciling = "RECONCILING";
    public const string Matched = "MATCHED";
    public const string Different = "DIFFERENT";
    public const string Failed = "FAILED";
    public const string Approved = "APPROVED";

    public static bool IsTerminal(string status) => status is Different or Failed or Approved;
}

public static class RecoveryReconciliationEvents
{
    public const string Requested = "RECOVERY_RECONCILIATION_REQUESTED";
    public const string ReferenceCaptureStarted = "RECOVERY_REFERENCE_CAPTURE_STARTED";
    public const string ReferenceReady = "RECOVERY_REFERENCE_READY";
    public const string RestoreStarted = "RECOVERY_RESTORE_STARTED";
    public const string Reconciling = "RECOVERY_RECONCILIATION_STARTED";
    public const string Completed = "RECOVERY_RECONCILIATION_COMPLETED";
    public const string Failed = "RECOVERY_RECONCILIATION_FAILED";
    public const string Viewed = "RECOVERY_RECONCILIATION_VIEWED";
    public const string Approved = "RECOVERY_RECONCILIATION_APPROVED";
}

public static class RecoveryDifferenceKinds
{
    public const string IdentityMissing = "IDENTITY_MISSING";
    public const string IdentityAdditional = "IDENTITY_ADDITIONAL";
    public const string LinkMissing = "LINK_MISSING";
    public const string LinkChanged = "LINK_CHANGED";
    public const string VersionChanged = "VERSION_CHANGED";
    public const string CountChanged = "COUNT_CHANGED";
    public const string ValueChanged = "VALUE_CHANGED";
    public const string EvidenceMissing = "EVIDENCE_MISSING";
    public const string EvidenceCorrupt = "EVIDENCE_CORRUPT";
    public const string EvidenceInaccessible = "EVIDENCE_INACCESSIBLE";
    public const string AuditMissing = "AUDIT_MISSING";
    public const string AuditAltered = "AUDIT_ALTERED";
    public const string UnexpectedPostRecoveryRecord = "UNEXPECTED_POST_RECOVERY_RECORD";
}

public sealed record FunctionalSnapshotField(string Name, string Sha256);

public sealed record FunctionalSnapshotRecord(
    string Table,
    string StableKey,
    IReadOnlyList<FunctionalSnapshotField> Fields,
    string? StateSha256,
    string RowSha256);

public sealed record FunctionalSnapshotTable(
    string Name,
    long Count,
    IReadOnlyDictionary<string, long> StateCounts,
    IReadOnlyList<FunctionalSnapshotRecord> Records,
    string TableSha256);

public sealed record FunctionalSnapshot(
    int SchemaVersion,
    string Kind,
    string Canonicalization,
    Guid ReconciliationId,
    Guid BranchId,
    DateTimeOffset CapturedAt,
    string Revision,
    string ImageDigest,
    string Migration,
    IReadOnlyList<FunctionalSnapshotTable> Tables,
    string RootSha256);

public sealed record RecoveryDifference(
    int Ordinal,
    string Group,
    string ResourceType,
    string StableKey,
    string? Field,
    string Kind,
    string? ExpectedSha256,
    string? ActualSha256);

public sealed record FunctionalReconciliationResult(
    string Status,
    string ExpectedRootSha256,
    string ActualRootSha256,
    int TotalDifferences,
    bool Truncated,
    IReadOnlyList<RecoveryDifference> Differences);

public static class FunctionalSnapshotContract
{
    public const int SchemaVersion = 1;
    public const string Kind = "SGOL-FUNCTIONAL-SNAPSHOT-1";
    public const string Canonicalization = "SGOL-CANON-1";
    public const int MaximumDifferences = 100_000;
    public const int MaximumRowsPerTable = 1_000_000;
    public const long MaximumSnapshotBytes = 512L * 1024 * 1024;
    public const long MaximumObjectBytes = 10L * 1024 * 1024 * 1024;

    public static string HashValue(object? value)
    {
        var canonical = CanonicalValue(value);
        return Hash(canonical);
    }

    public static FunctionalSnapshotRecord CreateRecord(
        string table,
        string stableKey,
        IEnumerable<KeyValuePair<string, object?>> fields)
    {
        ValidateName(table, nameof(table));
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        ArgumentNullException.ThrowIfNull(fields);
        var canonicalFields = fields
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item =>
            {
                ValidateName(item.Key, nameof(fields));
                return new FunctionalSnapshotField(item.Key, HashValue(item.Value));
            })
            .ToArray();
        if (canonicalFields.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != canonicalFields.Length)
            throw new ArgumentException("Snapshot fields must be unique.", nameof(fields));
        var rowCanonical = string.Join('\n', new[] { table, stableKey }
            .Concat(canonicalFields.Select(field => $"{field.Name}:{field.Sha256}")));
        var stateFields = canonicalFields.Where(field => field.Name == "status" ||
            field.Name.EndsWith("_status", StringComparison.Ordinal) || field.Name == "outcome").ToArray();
        var stateSha256 = stateFields.Length == 0 ? null : Hash(string.Join('\n',
            stateFields.Select(field => $"{field.Name}:{field.Sha256}")));
        return new(table, stableKey, canonicalFields, stateSha256, Hash(rowCanonical));
    }

    public static FunctionalSnapshot CreateSnapshot(
        Guid reconciliationId,
        Guid branchId,
        DateTimeOffset capturedAt,
        string revision,
        string imageDigest,
        string migration,
        IEnumerable<FunctionalSnapshotRecord> records)
    {
        if (reconciliationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Snapshot identifiers are required.");
        if (capturedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Snapshot time must be UTC.", nameof(capturedAt));
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDigest);
        ArgumentException.ThrowIfNullOrWhiteSpace(migration);
        ArgumentNullException.ThrowIfNull(records);
        var materialized = records.ToArray();
        if (materialized.Any(record => FunctionalSnapshotSchema.Ordinal(record.Table) == int.MaxValue))
            throw new RecoveryContractException("SNAPSHOT_TABLE_SET_INVALID");

        var tables = FunctionalSnapshotSchema.Tables
            .Select(tableName =>
            {
                var tableRecords = materialized.Where(row => row.Table == tableName)
                    .OrderBy(row => row.StableKey, StringComparer.Ordinal).ToArray();
                if (tableRecords.Select(row => row.StableKey).Distinct(StringComparer.Ordinal).Count() !=
                    tableRecords.Length)
                    throw new RecoveryContractException("SNAPSHOT_STABLE_KEY_DUPLICATE");
                if (tableRecords.Length > MaximumRowsPerTable)
                    throw new RecoveryContractException("CARDINALITY_LIMIT_EXCEEDED");
                var states = tableRecords.Where(row => row.StateSha256 is not null)
                    .GroupBy(row => row.StateSha256!, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.LongCount(), StringComparer.Ordinal);
                var hash = Hash(string.Join('\n', tableRecords.Select(row => $"{row.StableKey}:{row.RowSha256}")));
                return new FunctionalSnapshotTable(tableName, tableRecords.LongLength, states, tableRecords, hash);
            })
            .ToArray();
        var root = Hash(string.Join('\n', tables.Select(table =>
            $"{table.Name}:{table.Count}:{string.Join(',', table.StateCounts.Select(item => $"{item.Key}={item.Value}"))}:{table.TableSha256}")));
        return new(SchemaVersion, Kind, Canonicalization, reconciliationId, branchId, capturedAt, revision,
            imageDigest, migration, tables, root);
    }

    public static void ValidateIntegrity(FunctionalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var table in snapshot.Tables)
        {
            foreach (var row in table.Records)
            {
                var expectedRow = Hash(string.Join('\n', new[] { row.Table, row.StableKey }
                    .Concat(row.Fields.OrderBy(field => field.Name, StringComparer.Ordinal)
                        .Select(field => $"{field.Name}:{field.Sha256}"))));
                var stateFields = row.Fields.Where(field => field.Name == "status" ||
                    field.Name.EndsWith("_status", StringComparison.Ordinal) || field.Name == "outcome").ToArray();
                var expectedState = stateFields.Length == 0 ? null : Hash(string.Join('\n',
                    stateFields.Select(field => $"{field.Name}:{field.Sha256}")));
                if (row.Table != table.Name || row.RowSha256 != expectedRow ||
                    row.StateSha256 != expectedState || row.Fields.Any(field => !IsSha256(field.Sha256)))
                    throw new RecoveryContractException("REFERENCE_CORRUPT");
            }
            var expectedTable = Hash(string.Join('\n', table.Records.OrderBy(row => row.StableKey,
                StringComparer.Ordinal).Select(row => $"{row.StableKey}:{row.RowSha256}")));
            var expectedStates = table.Records.Where(row => row.StateSha256 is not null)
                .GroupBy(row => row.StateSha256!, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.LongCount(), StringComparer.Ordinal);
            if (table.Count != table.Records.Count || table.TableSha256 != expectedTable ||
                !table.StateCounts.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .SequenceEqual(expectedStates.OrderBy(item => item.Key, StringComparer.Ordinal)))
                throw new RecoveryContractException("REFERENCE_CORRUPT");
        }
        var expectedRoot = Hash(string.Join('\n', snapshot.Tables.Select(table =>
            $"{table.Name}:{table.Count}:{string.Join(',', table.StateCounts.Select(item => $"{item.Key}={item.Value}"))}:{table.TableSha256}")));
        if (snapshot.RootSha256 != expectedRoot) throw new RecoveryContractException("REFERENCE_CORRUPT");
    }

    public static string CanonicalValue(object? value) => value switch
    {
        null => "null",
        string text => JsonSerializer.Serialize(text.Normalize(NormalizationForm.FormC)),
        Guid guid => guid.ToString("D"),
        bool flag => flag ? "true" : "false",
        byte or sbyte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset instant => instant.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
        DateTime instant => new DateTimeOffset(instant.ToUniversalTime()).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
        JsonDocument document => CanonicalJson(document.RootElement),
        JsonElement element => CanonicalJson(element),
        byte[] bytes => Convert.ToHexStringLower(bytes),
        _ => throw new RecoveryContractException("CANONICAL_VALUE_UNSUPPORTED")
    };

    public static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string CanonicalJson(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',', element.EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => JsonSerializer.Serialize(property.Name.Normalize(NormalizationForm.FormC)) + ":" + CanonicalJson(property.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(',', element.EnumerateArray().Select(CanonicalJson)) + "]",
        JsonValueKind.String => CanonicalJsonString(element.GetString() ?? string.Empty),
        JsonValueKind.Number => NormalizeJsonNumber(element.GetRawText()),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => throw new RecoveryContractException("CANONICAL_VALUE_UNSUPPORTED")
    };

    private static void ValidateName(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
            !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Snapshot names use the approved lowercase allowlist.", parameter);
    }

    private static string NormalizeJsonNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("G29", CultureInfo.InvariantCulture)
            : throw new RecoveryContractException("CANONICAL_VALUE_UNSUPPORTED");

    private static string CanonicalJsonString(string value)
    {
        if (Guid.TryParseExact(value, "D", out var guid)) return JsonSerializer.Serialize(guid.ToString("D"));
        if (value.Contains('T', StringComparison.Ordinal) && DateTimeOffset.TryParse(value,
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant))
            return JsonSerializer.Serialize(instant.ToUniversalTime().ToString(
                "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
        return JsonSerializer.Serialize(value.Normalize(NormalizationForm.FormC));
    }

    private static bool IsSha256(string value) => value.Length == 64 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public static class FunctionalSnapshotSchema
{
    public static readonly string[] Tables =
    [
        "branch", "person", "employment_version", "availability_day_version", "app_user",
        "identity_credential", "role_assignment_version", "direction_bootstrap", "configuration_release",
        "calendar_day_version", "task_definition", "task_definition_version", "eligibility_policy_version",
        "activation_rule_version", "evidence_requirement_catalog", "evidence_policy_version",
        "evidence_requirement_version", "validation_policy_version", "week_period", "work_plan", "plan_version",
        "plan_version_obligation", "generation_request", "work_obligation", "eligibility_evaluation",
        "eligibility_candidate", "assignment_version", "execution_result", "validation_requirement",
        "validation_decision_version", "file_object", "evidence_item", "evidence_version",
        "evidence_review_snapshot", "audit_event", "idempotency_record", "internal_notice", "outbox_event",
        "scheduled_job_run"
    ];

    public static int Ordinal(string table)
    {
        var index = Array.IndexOf(Tables, table);
        return index < 0 ? int.MaxValue : index;
    }
}

public static class FunctionalSnapshotReconciler
{
    public static FunctionalReconciliationResult Compare(
        FunctionalSnapshot expected,
        FunctionalSnapshot actual,
        int maximumDifferences = FunctionalSnapshotContract.MaximumDifferences)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (maximumDifferences < 1 || maximumDifferences > FunctionalSnapshotContract.MaximumDifferences)
            throw new ArgumentOutOfRangeException(nameof(maximumDifferences));
        FunctionalSnapshotContract.ValidateIntegrity(expected);
        FunctionalSnapshotContract.ValidateIntegrity(actual);
        ValidateComparable(expected, actual);
        if (expected.RootSha256 == actual.RootSha256)
            return new(RecoveryReconciliationStatuses.Matched, expected.RootSha256, actual.RootSha256, 0, false, []);

        var differences = new List<RecoveryDifference>();
        var total = 0;
        foreach (var tableName in FunctionalSnapshotSchema.Tables)
        {
            var left = expected.Tables.SingleOrDefault(table => table.Name == tableName);
            var right = actual.Tables.SingleOrDefault(table => table.Name == tableName);
            if (left?.Count != right?.Count)
                Add("counts", tableName, tableName, null, RecoveryDifferenceKinds.CountChanged,
                    left?.TableSha256, right?.TableSha256);
            var leftStates = left?.StateCounts ?? new Dictionary<string, long>();
            var rightStates = right?.StateCounts ?? new Dictionary<string, long>();
            foreach (var state in leftStates.Keys.Union(rightStates.Keys, StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                leftStates.TryGetValue(state, out var expectedCount);
                rightStates.TryGetValue(state, out var actualCount);
                if (expectedCount != actualCount)
                    Add("counts", tableName, $"{tableName}:state:{state}", null,
                        RecoveryDifferenceKinds.CountChanged, HashCount(expectedCount), HashCount(actualCount));
            }

            var leftRows = left?.Records.ToDictionary(row => row.StableKey, StringComparer.Ordinal) ?? [];
            var rightRows = right?.Records.ToDictionary(row => row.StableKey, StringComparer.Ordinal) ?? [];
            foreach (var key in leftRows.Keys.Union(rightRows.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (!leftRows.TryGetValue(key, out var expectedRow))
                {
                    Add(Group(tableName), tableName, key, null, RecoveryDifferenceKinds.IdentityAdditional, null, rightRows[key].RowSha256);
                    continue;
                }
                if (!rightRows.TryGetValue(key, out var actualRow))
                {
                    Add(Group(tableName), tableName, key, null, MissingKind(tableName), expectedRow.RowSha256, null);
                    continue;
                }
                if (expectedRow.RowSha256 == actualRow.RowSha256) continue;
                var leftFields = expectedRow.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
                var rightFields = actualRow.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
                foreach (var field in leftFields.Keys.Union(rightFields.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    leftFields.TryGetValue(field, out var expectedField);
                    rightFields.TryGetValue(field, out var actualField);
                    if (expectedField?.Sha256 == actualField?.Sha256) continue;
                    Add(Group(tableName), tableName, key, field, ChangedKind(tableName, field),
                        expectedField?.Sha256, actualField?.Sha256);
                }
            }
        }

        var truncated = total > maximumDifferences;
        return new(truncated ? RecoveryReconciliationStatuses.Failed : RecoveryReconciliationStatuses.Different,
            expected.RootSha256, actual.RootSha256, total, truncated, differences);

        void Add(string group, string type, string key, string? field, string kind, string? expectedHash, string? actualHash)
        {
            total++;
            if (differences.Count < maximumDifferences)
                differences.Add(new(differences.Count + 1, group, type, key, field, kind, expectedHash, actualHash));
        }
    }

    private static void ValidateComparable(FunctionalSnapshot expected, FunctionalSnapshot actual)
    {
        if (expected.SchemaVersion != FunctionalSnapshotContract.SchemaVersion || actual.SchemaVersion != expected.SchemaVersion ||
            expected.Kind != FunctionalSnapshotContract.Kind || actual.Kind != expected.Kind ||
            expected.Canonicalization != FunctionalSnapshotContract.Canonicalization ||
            actual.Canonicalization != expected.Canonicalization)
            throw new RecoveryContractException("REFERENCE_VERSION_UNSUPPORTED");
        if (expected.ReconciliationId != actual.ReconciliationId || expected.BranchId != actual.BranchId ||
            expected.CapturedAt != actual.CapturedAt)
            throw new RecoveryContractException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
        if (expected.Revision != actual.Revision || expected.ImageDigest != actual.ImageDigest ||
            expected.Migration != actual.Migration)
            throw new RecoveryContractException("REFERENCE_VERSION_UNSUPPORTED");
        var expectedNames = expected.Tables.Select(table => table.Name).ToArray();
        var actualNames = actual.Tables.Select(table => table.Name).ToArray();
        if (!expectedNames.SequenceEqual(FunctionalSnapshotSchema.Tables) || !actualNames.SequenceEqual(expectedNames))
            throw new RecoveryContractException("SNAPSHOT_TABLE_SET_INVALID");
    }

    private static string Group(string table) => table switch
    {
        "file_object" or "evidence_item" or "evidence_version" or "evidence_review_snapshot" => "evidence",
        "audit_event" => "audit",
        _ when table.Contains("version", StringComparison.Ordinal) => "versions",
        _ => "identities-and-links"
    };

    private static string MissingKind(string table) => table switch
    {
        "audit_event" => RecoveryDifferenceKinds.AuditMissing,
        "file_object" or "evidence_item" or "evidence_version" or "evidence_review_snapshot" => RecoveryDifferenceKinds.EvidenceMissing,
        "employment_version" or "availability_day_version" or "role_assignment_version" or
            "plan_version_obligation" or "eligibility_candidate" or "assignment_version" =>
            RecoveryDifferenceKinds.LinkMissing,
        _ => RecoveryDifferenceKinds.IdentityMissing
    };

    private static string ChangedKind(string table, string field) => table switch
    {
        "audit_event" => RecoveryDifferenceKinds.AuditAltered,
        "file_object" when field is "sha256" or "size_bytes" => RecoveryDifferenceKinds.EvidenceCorrupt,
        _ when field.EndsWith("_id", StringComparison.Ordinal) || field == "supersedes_id" => RecoveryDifferenceKinds.LinkChanged,
        _ when table.Contains("version", StringComparison.Ordinal) || field is "version_no" or "row_version" => RecoveryDifferenceKinds.VersionChanged,
        _ => RecoveryDifferenceKinds.ValueChanged
    };

    private static string HashCount(long value) => FunctionalSnapshotContract.Hash(
        value.ToString(CultureInfo.InvariantCulture));
}

public sealed record RecoveryObjectives(long DatabaseRpoSeconds, long ObjectRpoSeconds, long ObservedRpoSeconds, long ObservedRtoSeconds)
{
    public bool MeetsRpo => ObservedRpoSeconds <= 3_600;
    public bool MeetsRto => ObservedRtoSeconds <= 14_400;
}

public static class RecoveryObjectiveCalculator
{
    public static RecoveryObjectives Calculate(
        DateTimeOffset targetRecoveryAt,
        DateTimeOffset backupSnapshotAt,
        DateTimeOffset replicaScheduledFor,
        DateTimeOffset recoveryStartedAt,
        DateTimeOffset reconciliationCompletedAt)
    {
        var values = new[] { targetRecoveryAt, backupSnapshotAt, replicaScheduledFor, recoveryStartedAt, reconciliationCompletedAt };
        if (values.Any(value => value.Offset != TimeSpan.Zero) || backupSnapshotAt > targetRecoveryAt ||
            replicaScheduledFor > targetRecoveryAt || reconciliationCompletedAt < recoveryStartedAt)
            throw new RecoveryContractException("RECOVERY_CLOCK_INCONSISTENT");
        var database = checked((long)(targetRecoveryAt - backupSnapshotAt).TotalSeconds);
        var objects = checked((long)(targetRecoveryAt - replicaScheduledFor).TotalSeconds);
        var rto = checked((long)(reconciliationCompletedAt - recoveryStartedAt).TotalSeconds);
        return new(database, objects, Math.Max(database, objects), rto);
    }
}

public class RecoveryContractException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed record CreateRecoveryReconciliationCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId, string Reason);
public sealed record ApproveRecoveryReconciliationCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId, Guid ReconciliationId, long ExpectedSequence, string Reason);
public sealed record RecoveryReconciliationQuery(Guid ActorUserId, Guid CorrelationId, Guid ReconciliationId);

public sealed record RecoveryDifferenceDetails(int Ordinal, string Group, string ResourceType, string StableKey,
    string? Field, string Kind, string? ExpectedSha256, string? ActualSha256);

public sealed record RecoveryReconciliationDetails(Guid ReconciliationId, Guid BranchId, string Status,
    DateTimeOffset RequestedAt, DateTimeOffset? TargetRecoveryAt, long Sequence, string? ReferenceRootSha256,
    string? ActualRootSha256, int DifferenceCount, bool DifferencesTruncated, long? ObservedRpoSeconds,
    long? ObservedRtoSeconds, DateTimeOffset? ApprovedAt, Guid? ApprovedBy,
    IReadOnlyList<RecoveryDifferenceDetails> Differences, bool Replayed = false);

public interface IRecoveryReconciliationService
{
    Task<RecoveryReconciliationDetails> CreateAsync(CreateRecoveryReconciliationCommand command, CancellationToken cancellationToken = default);
    Task<RecoveryReconciliationDetails> GetAsync(RecoveryReconciliationQuery query, CancellationToken cancellationToken = default);
    Task<RecoveryReconciliationDetails> ApproveAsync(ApproveRecoveryReconciliationCommand command, CancellationToken cancellationToken = default);
}

public sealed record RecoveryReferenceReadyCommand(Guid ReconciliationId, Guid CorrelationId,
    DateTimeOffset TargetRecoveryAt, string ReferenceManifestSha256, string ReferenceRootSha256);
public sealed record FunctionalReferenceReceipt(DateTimeOffset TargetRecoveryAt, string ManifestUri,
    string ManifestSha256, string RootSha256);
public sealed record FunctionalReconciliationReceipt(DateTimeOffset CompletedAt, string ReportManifestSha256,
    FunctionalReconciliationResult Result, RecoveryObjectives Objectives);

public interface IFunctionalRecoveryOperations
{
    Task<FunctionalReferenceReceipt> CaptureReferenceAsync(Guid reconciliationId,
        CancellationToken cancellationToken = default);
    Task<FunctionalReferenceReceipt> CompleteReferenceAsync(Guid reconciliationId, string referenceManifestUri,
        string backupManifestUri, string replicaManifestUri, CancellationToken cancellationToken = default);
    Task<FunctionalReconciliationReceipt> ReconcileAsync(Guid reconciliationId, string referenceManifestUri,
        string restoreEvidencePath, CancellationToken cancellationToken = default);
}

public sealed record RecoveryRestoreStartedCommand(Guid ReconciliationId, Guid CorrelationId,
    DateTimeOffset StartedAt, string RestoreEvidenceSha256);
public sealed record RecoveryCompletedCommand(Guid ReconciliationId, Guid CorrelationId,
    DateTimeOffset CompletedAt, FunctionalReconciliationResult Result, RecoveryObjectives Objectives,
    string ReportManifestSha256);
public sealed record RecoveryFailedCommand(Guid ReconciliationId, Guid CorrelationId,
    DateTimeOffset FailedAt, string ErrorCode);

public interface IRecoveryTechnicalWriter
{
    Task MarkReferenceReadyAsync(RecoveryReferenceReadyCommand command, CancellationToken cancellationToken = default);
    Task MarkRestoreStartedAsync(RecoveryRestoreStartedCommand command, CancellationToken cancellationToken = default);
    Task CompleteAsync(RecoveryCompletedCommand command, CancellationToken cancellationToken = default);
    Task FailAsync(RecoveryFailedCommand command, CancellationToken cancellationToken = default);
}

public sealed class RecoveryAccessDeniedException : RecoveryContractException { public RecoveryAccessDeniedException() : base("ACCESO_DENEGADO") { } }
public sealed class RecoveryReconciliationNotFoundException : RecoveryContractException { public RecoveryReconciliationNotFoundException() : base("RECONCILIACION_NO_ENCONTRADA") { } }
public sealed class RecoveryReconciliationNotApprovableException : RecoveryContractException { public RecoveryReconciliationNotApprovableException() : base("RECONCILIACION_NO_APROBABLE") { } }
public sealed class RecoveryReconciliationVersionConflictException : RecoveryContractException { public RecoveryReconciliationVersionConflictException() : base("VERSION_CONFLICT") { } }
public sealed class RecoveryAuditFailedException : RecoveryContractException { public RecoveryAuditFailedException() : base("RECONCILIATION_AUDIT_FAILED") { } }
