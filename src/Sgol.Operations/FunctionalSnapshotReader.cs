using System.Data;
using System.Text.Json;
using Npgsql;
using Sgol.Continuity.Contracts;

namespace Sgol.Operations;

public sealed record ExportedFunctionalSnapshot(FunctionalSnapshot Snapshot, string PostgreSqlSnapshotId);

public sealed class FunctionalSnapshotReader
{
    private const string ContractBaselineMigration = "20260912213000_AddPortableDataProtectionKeyRing";
    private const string ExpectedLatestMigration = "20260914210503_AddRecoveryReconciliation";
    private static readonly Dictionary<string, Projection> Projections = BuildProjections();

    public static async Task<ExportedFunctionalSnapshot> CaptureReferenceAsync(
        string connectionString,
        Guid reconciliationId,
        Guid branchId,
        string revision,
        string imageDigest,
        Func<string, CancellationToken, Task> createBackupWithinSnapshot,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        await using (var utc = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'", connection, transaction))
            await utc.ExecuteNonQueryAsync(cancellationToken);
        var (snapshotId, capturedAt) = await ExportSnapshotAsync(connection, transaction, cancellationToken);
        await AssertMigrationAsync(connection, transaction, cancellationToken);
        var records = new List<FunctionalSnapshotRecord>();
        foreach (var table in FunctionalSnapshotSchema.Tables)
        {
            var projection = Projections[table];
            records.AddRange(await ReadTableAsync(connection, transaction, projection, capturedAt, cancellationToken));
        }
        var snapshot = FunctionalSnapshotContract.CreateSnapshot(reconciliationId, branchId, capturedAt,
            revision, imageDigest, ContractBaselineMigration, records);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        if (serialized.LongLength > FunctionalSnapshotContract.MaximumSnapshotBytes)
            throw new RecoveryContractException("CARDINALITY_LIMIT_EXCEEDED");
        ArgumentNullException.ThrowIfNull(createBackupWithinSnapshot);
        await createBackupWithinSnapshot(snapshotId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(snapshot, snapshotId);
    }

    public static async Task<FunctionalSnapshot> CaptureActualAsync(
        string connectionString,
        Guid reconciliationId,
        Guid branchId,
        DateTimeOffset referenceCapturedAt,
        string revision,
        string imageDigest,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(cancellationToken);
        await using (var utc = new NpgsqlCommand("SET LOCAL TIME ZONE 'UTC'", connection, transaction))
            await utc.ExecuteNonQueryAsync(cancellationToken);
        await AssertMigrationAsync(connection, transaction, cancellationToken);
        var records = new List<FunctionalSnapshotRecord>();
        foreach (var table in FunctionalSnapshotSchema.Tables)
            records.AddRange(await ReadTableAsync(connection, transaction, Projections[table], referenceCapturedAt, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        var snapshot = FunctionalSnapshotContract.CreateSnapshot(reconciliationId, branchId, referenceCapturedAt,
            revision, imageDigest, ContractBaselineMigration, records);
        if (JsonSerializer.SerializeToUtf8Bytes(snapshot).LongLength > FunctionalSnapshotContract.MaximumSnapshotBytes)
            throw new RecoveryContractException("CARDINALITY_LIMIT_EXCEEDED");
        return snapshot;
    }

    private static async Task<(string SnapshotId, DateTimeOffset CapturedAt)> ExportSnapshotAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_export_snapshot(), transaction_timestamp()", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new RecoveryContractException("REFERENCE_MISSING");
        return (reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime());
    }

    private static async Task AssertMigrationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT migration_id FROM \"__EFMigrationsHistory\" ORDER BY migration_id DESC LIMIT 1", connection, transaction);
        var migration = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(migration, ExpectedLatestMigration, StringComparison.Ordinal))
            throw new RecoveryContractException("REFERENCE_VERSION_UNSUPPORTED");
    }

    private static async Task<IReadOnlyList<FunctionalSnapshotRecord>> ReadTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Projection projection,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT {projection.StableKeySql} AS stable_key, to_jsonb(t)::text AS row_json " +
            $"FROM \"{projection.Table}\" AS t{projection.CutoffSql} ORDER BY stable_key";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        if (projection.CutoffSql.Length != 0) command.Parameters.AddWithValue("captured_at", capturedAt);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var records = new List<FunctionalSnapshotRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (records.Count >= FunctionalSnapshotContract.MaximumRowsPerTable)
                throw new RecoveryContractException("CARDINALITY_LIMIT_EXCEEDED");
            var stableKey = reader.GetString(0);
            using var document = JsonDocument.Parse(reader.GetString(1));
            var fields = document.RootElement.EnumerateObject()
                .Where(property => !projection.ExcludedColumns.Contains(property.Name))
                .Select(property => new KeyValuePair<string, object?>(property.Name, property.Value.Clone()));
            records.Add(FunctionalSnapshotContract.CreateRecord(projection.Table,
                FunctionalSnapshotContract.Hash(stableKey), fields));
        }
        return records;
    }

    private static Dictionary<string, Projection> BuildProjections()
    {
        static Projection P(string table, string[] keys, string? cutoff = null, params string[] excluded)
        {
            var key = "concat_ws('|', " + string.Join(", ", keys.Select(column => $"t.\"{column}\"::text")) + ")";
            var where = cutoff is null ? string.Empty : $" WHERE t.\"{cutoff}\" <= @captured_at";
            return new(table, key, where, excluded.ToHashSet(StringComparer.Ordinal));
        }

        Projection[] values =
        [
            P("branch", ["id"]), P("person", ["id"]), P("employment_version", ["id"]),
            P("availability_day_version", ["id"]), P("app_user", ["id"], excluded: ["security_stamp"]),
            P("identity_credential", ["user_id"], excluded: ["password_hash"]),
            P("role_assignment_version", ["id"]), P("direction_bootstrap", ["singleton"]),
            P("configuration_release", ["id"]), P("calendar_day_version", ["id"]),
            P("task_definition", ["id"]), P("task_definition_version", ["id"]),
            P("eligibility_policy_version", ["id"]), P("activation_rule_version", ["id"]),
            P("evidence_requirement_catalog", ["task_definition_id", "requirement_code"]),
            P("evidence_policy_version", ["id"]), P("evidence_requirement_version", ["id"]),
            P("validation_policy_version", ["id"]), P("week_period", ["id"]), P("work_plan", ["id"]),
            P("plan_version", ["id"]), P("plan_version_obligation", ["plan_version_id", "obligation_id"]),
            P("generation_request", ["id"]), P("work_obligation", ["id"]),
            P("eligibility_evaluation", ["id"]), P("eligibility_candidate", ["evaluation_id", "person_id"]),
            P("assignment_version", ["id"]), P("execution_result", ["id"]),
            P("validation_requirement", ["id"]), P("validation_decision_version", ["id"]),
            P("file_object", ["id"]), P("evidence_item", ["id"]), P("evidence_version", ["id"]),
            P("evidence_review_snapshot", ["id"]), P("audit_event", ["id"], "occurred_at"),
            P("idempotency_record", ["scope", "key"], "created_at"), P("internal_notice", ["id"]),
            P("outbox_event", ["id"], "created_at"), P("scheduled_job_run", ["id"], "started_at"),
        ];
        var result = values.ToDictionary(item => item.Table, StringComparer.Ordinal);
        if (!result.Keys.Order(StringComparer.Ordinal).SequenceEqual(
            FunctionalSnapshotSchema.Tables.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("Functional snapshot projection inventory is incomplete.");
        return result;
    }

    private sealed record Projection(string Table, string StableKeySql, string CutoffSql,
        IReadOnlySet<string> ExcludedColumns);
}
