using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sgol.Cv05Demo;

internal static class DemoContract
{
    public const string TaskId = "TECH-E2E-CV-05";
    public const string CutId = "CV-05";
    public const string ReportSchema = "sgol.tech-e2e-cv05.report";
    public const int ReportSchemaVersion = 1;
    public const string BaseCommit = "00da83ca26f67e523d0d6067af737f38315e62b0";
    public const string SeedId = "CV05-SEED-V1";
    public const string PostgreSqlImage = "postgres:18.6-alpine3.23";
    public const string SeaweedImage = "chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5";
    public const string ClamAvImage = "clamav/clamav:1.5.4-debian@sha256:be3cb41d9833ce9ffb98f3d3e1483c35c0d87060c2bda3624d75fd28bbf0b3bd";
    public const string SourceQuarantineBucket = "cv05-source-quarantine";
    public const string SourceCleanBucket = "cv05-source-clean";
    public const string DestinationQuarantineBucket = "cv05-destination-quarantine";
    public const string DestinationCleanBucket = "cv05-destination-clean";
    public const string ManifestBucket = "cv05-portable-backups";
    public const string DatabasePrefix = "sgol_cv05_";
    public const string LoopbackAddress = "127.0.0.1";
    public const string LatestMigration = "20260918001719_AddHostedAuthentication";

    public static readonly IReadOnlyList<string> Phases =
    [
        "PREFLIGHT", "POSTGRESQL", "STORES", "ANTIMALWARE", "OCI", "HTTPS", "CSRF",
        "LOGIN", "PASSWORD_CHANGE", "MFA", "SEED", "INDICATORS", "DIRECTION",
        "AUDIT", "IDEMPOTENCY", "BACKUP", "REPLICA", "RESTORE", "RECONCILIATION",
        "REPORT", "CLEANUP",
    ];

    public static readonly IReadOnlySet<string> TaskCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "TAR-0005", "TAR-0007", "TAR-0008", "TAR-0011",
        "TAR-0018", "TAR-0026", "TAR-0092", "TAR-0093",
    };

    public static readonly IReadOnlySet<string> ProductRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/api/v1/auth/csrf", "/api/v1/auth/login", "/api/v1/auth/password/change",
        "/api/v1/auth/mfa/enroll", "/api/v1/auth/mfa/confirm", "/api/v1/auth/mfa/verify",
        "/api/v1/auth/session", "/api/v1/people", "/api/v1/users",
        "/api/v1/users/{id}/role-assignments", "/api/v1/users/{id}/mfa-reset",
        "/api/v1/indicators", "/api/v1/direction/overview", "/api/v1/audit-events",
        "/api/v1/audit-events/{id}", "/api/v1/generation-requests",
        "/api/v1/obligations/{id}/evidence", "/api/v1/obligations/{id}/conclusion",
        "/api/v1/obligations/{id}/validation-decisions",
        "/api/v1/validation-decisions/{id}/replacements",
        "/api/v1/continuity/reconciliations", "/api/v1/continuity/reconciliations/{id}",
        "/api/v1/continuity/reconciliations/{id}/approval",
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
        "ASPNETCORE_URLS", "Kestrel__Certificates__Default__Path", "Kestrel__Certificates__Default__Password",
        "SGOL_BOOTSTRAP_INITIAL_PASSWORD", "SGOL_BOOTSTRAP_USER_NAME",
    ];

    private static readonly HashSet<string> AllowedCodes = new(StringComparer.Ordinal)
    {
        "CV05_NONE", "CV05_PRECONDITION_FAILED", "CV05_POSTGRESQL_FAILED", "CV05_STORE_FAILED",
        "CV05_HTTPS_FAILED", "CV05_AUTH_FAILED", "CV05_HTTP_CONTRACT_FAILED",
        "CV05_DATABASE_CONTRACT_FAILED", "CV05_SCENARIO_FAILED", "CV05_REPORT_FAILED",
        "CV05_CLEANUP_FAILED", "CV05_UNEXPECTED_FAILURE", "CV05_EXECUTION_PASSED",
    };

    public static void RejectExternalConfiguration(Func<string, string?> read)
    {
        if (RejectedEnvironmentVariables.Any(name => !string.IsNullOrWhiteSpace(read(name))))
        {
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV05_PRECONDITION_FAILED");
        }
    }

    public static void ValidatePreconditions(string repositoryRoot)
    {
        if (Environment.Version.Major != 10 || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV05_PRECONDITION_FAILED");
        }
        foreach (var project in new[] { "Sgol.Web", "Sgol.Admin", "Sgol.Worker", "Sgol.Operations" })
        {
            var assembly = Path.Combine(repositoryRoot, "src", project, "bin", "Release", "net10.0", $"{project}.dll");
            if (!File.Exists(assembly))
            {
                throw new DemoFailureException("PREFLIGHT", "NONE", "CV05_PRECONDITION_FAILED");
            }
        }
    }

    public static string SanitizeCode(string? code) =>
        code is not null && AllowedCodes.Contains(code) ? code : "CV05_UNEXPECTED_FAILURE";

    public static void ValidateDisposableDatabase(string host, string database, string expected, int port)
    {
        if (host is not ("localhost" or DemoContract.LoopbackAddress) || port <= 0 ||
            !database.StartsWith(DemoContract.DatabasePrefix, StringComparison.Ordinal) || database != expected)
        {
            throw new DemoFailureException("POSTGRESQL", "NONE", "CV05_PRECONDITION_FAILED");
        }
    }

    public static int CaptureExit(Process process)
    {
        if (!process.HasExited)
        {
            throw new InvalidOperationException("Process exit was requested before termination.");
        }
        return process.ExitCode;
    }
}

internal sealed class DemoFailureException(
    string phase,
    string scenario,
    string code,
    int exit = 1) : Exception(DemoSafety.SanitizeCode(code))
{
    public string Phase { get; } = phase;
    public string Scenario { get; } = scenario;
    public string Code { get; } = DemoSafety.SanitizeCode(code);
    public int Exit { get; } = exit;
}
