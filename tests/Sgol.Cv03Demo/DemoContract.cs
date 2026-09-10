namespace Sgol.Cv03Demo;

internal static class DemoContract
{
    public const string TaskId = "TECH-E2E-CV-03";
    public const string ReportSchema = "sgol.tech-e2e-cv03.report";
    public const int ReportSchemaVersion = 1;
    public const string SeedId = "CV03-SEED-V1";
    public const string PostgreSqlImage = "postgres:18.6-alpine3.23";
    public const string SeaweedImage = "chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5";
    public const string ClamAvImage = "clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd";
    public const string DatabasePrefix = "sgol_cv03_";
    public const string QuarantineBucket = "sgol-evidence-quarantine";
    public const string CleanBucket = "sgol-evidence-clean";
    public const string LoopbackAddress = "127.0.0.1";

    public static readonly IReadOnlySet<string> ProductRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/api/v1/obligations",
        "/api/v1/obligations/{id}",
        "/api/v1/obligations/{id}/evidence",
        "/api/v1/obligations/{id}/evidence-review",
        "/api/v1/obligations/{id}/conclusion",
        "/api/v1/files/upload-intents",
        "/api/v1/files/{id}/complete",
        "/api/v1/files/{id}/status",
        "/api/v1/me/inbox",
        "/api/v1/me/notices/{id}/read",
    };
}

internal enum DemoMode
{
    Automated,
}

internal sealed record DemoOptions(DemoMode Mode)
{
    public static bool TryParse(string[] args, out DemoOptions? options)
    {
        options = null;
        if (args.Length != 2 || args[0] != "--mode" || args[1] != nameof(DemoMode.Automated))
        {
            return false;
        }

        options = new(DemoMode.Automated);
        return true;
    }
}

internal static class DemoSafety
{
    private static readonly string[] RejectedEnvironmentVariables =
    [
        "ConnectionStrings__Sgol", "DATABASE_URL", "PGHOST", "PGPORT", "PGDATABASE", "PGUSER", "PGPASSWORD",
        "Evidence__Storage__Endpoint", "Evidence__Storage__AccessKey", "Evidence__Storage__SecretKey",
        "Evidence__Scanner__Host", "Evidence__Scanner__Port",
    ];

    private static readonly HashSet<string> AllowedCodes = new(StringComparer.Ordinal)
    {
        "CV03_EXTERNAL_CONFIGURATION_REJECTED", "CV03_DATABASE_NOT_DISPOSABLE", "CV03_INFRASTRUCTURE_START_FAILED",
        "CV03_HOST_START_FAILED", "CV03_SEED_FAILED", "CV03_SCENARIO_FAILED", "CV03_CLEANUP_FAILED",
        "CV03_ARTIFACT_WRITE_FAILED", "CV03_EXECUTION_PASSED", "CV03_UNEXPECTED_FAILURE",
        "CV03_S01_LIST_STATUS", "CV03_S01_OWN_MISSING", "CV03_S01_FOREIGN_VISIBLE", "CV03_S01_DETAIL_STATUS",
        "CV03_S05_INTENT_FAILED", "CV03_S05_UPLOAD_FAILED", "CV03_S05_COMPLETE_FAILED",
        "CV03_S05_OBJECT_MISSING", "CV03_S05_METADATA_MISMATCH", "CV03_S05_UPLOAD_EXPIRED",
        "CV03_S05_STORAGE_UNAVAILABLE", "CV03_S05_ACCESS_FAILED", "CV03_S05_FILE_NOT_FOUND",
        "CV03_S05_REQUIREMENT_FAILED", "CV03_S05_PERSIST_FAILED", "CV03_S05_STATE_FAILED",
        "CV03_S05_OBLIGATION_NOT_FOUND", "CV03_S05_CONDITION_FAILED", "CV03_S05_IDEMPOTENCY_FAILED",
        "CV03_S05_REQUEST_FAILED", "CV03_S05_IO_FAILED",
        "CV03_S05_WORKER_FAILED", "CV03_S05_CLEAN_STATUS_FAILED", "CV03_S05_CONTRIBUTE_FAILED",
        "CV03_S05_LINK_FAILED", "CV03_S05_OUTBOX_EVENT_MISSING", "CV03_S05_OUTBOX_EVENT_FUTURE",
        "CV03_S05_WORKER_EXITED", "CV03_S05_WORKER_RETRYING", "CV03_S05_WORKER_UNCLAIMED",
        "CV03_S20_CSRF_REJECTION_FAILED", "CV03_S20_FIRST_STATUS_FAILED", "CV03_S20_REPLAY_STATUS_FAILED",
        "CV03_S20_FIRST_RESULT_FAILED", "CV03_S20_REPLAY_RESULT_FAILED", "CV03_S20_AUDIT_COUNT_FAILED",
        "CV03_SEED_ACTORS_FAILED", "CV03_SEED_CONFIGURATION_FAILED", "CV03_SEED_OWN_FAILED",
        "CV03_SEED_FOREIGN_FAILED", "CV03_SEED_STRUCTURED_FAILED", "CV03_SEED_BINARY_FAILED",
        "CV03_SEED_COMPLETE_FAILED", "CV03_SEED_RACE_FAILED", "CV03_SEED_INCOMPLETE_FAILED",
        "CV03_SEED_TAR0092_TRUE_FAILED", "CV03_SEED_TAR0092_FALSE_FAILED", "CV03_SEED_FUTURE_FAILED",
        "CV03_SEED_AVAILABLE_FAILED", "CV03_SEED_OVERDUE_FAILED", "CV03_SEED_EVIDENCE_FAILED",
        "CV03_SEED_OBLIGATION_LOOKUP_FAILED", "CV03_SEED_OBLIGATION_REQUEST_FAILED",
        "CV03_SEED_OBLIGATION_MATERIALIZE_FAILED", "CV03_SEED_OBLIGATION_ASSIGNMENT_FAILED",
        "CV03_SEED_OBLIGATION_TASK_FAILED", "CV03_SEED_OBLIGATION_POLICY_FAILED",
        "CV03_SEED_OBLIGATION_RULE_FAILED", "CV03_SEED_OBLIGATION_PERIOD_FAILED",
        "CV03_SEED_OBLIGATION_RULE_PERSIST_FAILED",
        "CV03_SEED_RULE_CHECK_FAILED", "CV03_SEED_RULE_UNIQUE_FAILED", "CV03_SEED_RULE_FK_FAILED",
        "CV03_SEED_PERIOD_CONSTRAINT_FAILED", "CV03_SEED_DATABASE_CONSTRAINT_FAILED",
    };

    public static void RejectExternalConfiguration(Func<string, string?> read) =>
        _ = RejectedEnvironmentVariables.Any(name => !string.IsNullOrWhiteSpace(read(name)))
            ? throw new DemoSafetyException("CV03_EXTERNAL_CONFIGURATION_REJECTED")
            : false;

    public static void ValidateDisposableDatabase(string host, string database, string expected, int port)
    {
        if (host is not ("localhost" or DemoContract.LoopbackAddress) || port <= 0 ||
            !database.StartsWith(DemoContract.DatabasePrefix, StringComparison.Ordinal) || database != expected)
        {
            throw new DemoSafetyException("CV03_DATABASE_NOT_DISPOSABLE");
        }
    }

    public static string Sanitize(string? code) =>
        code is not null && AllowedCodes.Contains(code) ? code : "CV03_UNEXPECTED_FAILURE";
}

internal sealed class DemoSafetyException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

internal sealed class DemoScenarioAssertionException(string errorCode = "CV03_SCENARIO_FAILED") : Exception(errorCode)
{
    public string ErrorCode { get; } = DemoSafety.Sanitize(errorCode);
}
