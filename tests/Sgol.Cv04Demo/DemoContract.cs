using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sgol.Cv04Demo;

internal static class DemoContract
{
    public const string TaskId = "TECH-E2E-CV-04";
    public const string CutId = "CV-04";
    public const string ReportSchema = "sgol.tech-e2e-cv04.report";
    public const int ReportSchemaVersion = 1;
    public const string SeedId = "CV04-SEED-V1";
    public const string PostgreSqlImage = "postgres:18.6-alpine3.23";
    public const string DatabasePrefix = "sgol_cv04_";
    public const string LoopbackAddress = "127.0.0.1";
    public const string LatestMigration = "20260918001719_AddHostedAuthentication";

    public static readonly IReadOnlyList<string> Phases =
    [
        "PREFLIGHT", "POSTGRESQL", "HTTPS", "CSRF", "LOGIN", "PASSWORD_CHANGE",
        "MFA_ENROLL", "MFA_VERIFY", "SEED", "POLICY", "VALIDATION", "REPLACEMENT",
        "SUPERVISION", "AUTHORIZATION", "AUDIT", "REPORT", "CLEANUP",
    ];

    public static readonly IReadOnlySet<string> TaskCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "TAR-0005", "TAR-0007", "TAR-0008", "TAR-0011",
        "TAR-0018", "TAR-0026", "TAR-0092", "TAR-0093",
    };

    public static readonly IReadOnlySet<string> ProductRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/api/v1/auth/csrf",
        "/api/v1/auth/login",
        "/api/v1/auth/password/change",
        "/api/v1/auth/mfa/enroll",
        "/api/v1/auth/mfa/confirm",
        "/api/v1/auth/mfa/verify",
        "/api/v1/auth/session",
        "/api/v1/people",
        "/api/v1/users",
        "/api/v1/users/{id}/role-assignments",
        "/api/v1/users/{id}/mfa-reset",
        "/api/v1/configuration/releases",
        "/api/v1/configuration/releases/{id}/publish",
        "/api/v1/task-definitions/{taskCode}/validation-policy",
        "/api/v1/obligations/{id}/validation-decisions",
        "/api/v1/validation-decisions/{id}/replacements",
        "/api/v1/obligations/{id}/validations",
        "/api/v1/obligations/{id}",
        "/api/v1/obligations/{id}/evidence",
        "/api/v1/supervision/obligations",
        "/api/v1/validations/pending",
    };

    public static DateTimeOffset NextEffectiveFrom(DateTimeOffset currentEffectiveFrom, DateTimeOffset now)
    {
        var baseline = currentEffectiveFrom > now ? currentEffectiveFrom : now;
        return baseline.AddSeconds(1);
    }
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
        "CV04_NONE", "CV04_PRECONDITION_FAILED", "CV04_POSTGRESQL_START_FAILED", "CV04_HTTPS_START_FAILED",
        "CV04_HTTP_CONTRACT_FAILED", "CV04_DATABASE_CONTRACT_FAILED", "CV04_SCENARIO_FAILED",
        "CV04_TASK_CONFIGURATION_FAILED", "CV04_EVIDENCE_CONFIGURATION_FAILED", "CV04_WEEK_CONFIGURATION_FAILED",
        "CV04_OBLIGATION_CONFIGURATION_FAILED", "CV04_MATERIALIZATION_FAILED", "CV04_ASSIGNMENT_FAILED",
        "CV04_EVIDENCE_SEED_FAILED", "CV04_CONCLUSION_FAILED",
        "CV04_TASK_CURRENT_MISSING", "CV04_EVIDENCE_CURRENT_MISSING", "CV04_VALIDATION_CURRENT_MISSING",
        "CV04_WEEK_CURRENT_MISSING",
        "CV04_HTTP_STATUS_200", "CV04_HTTP_STATUS_201", "CV04_HTTP_STATUS_400", "CV04_HTTP_STATUS_401",
        "CV04_HTTP_STATUS_403", "CV04_HTTP_STATUS_404", "CV04_HTTP_STATUS_409", "CV04_HTTP_STATUS_412",
        "CV04_HTTP_STATUS_422", "CV04_HTTP_STATUS_500",
        "CV04_POLICY_RELEASE_CREATE_REJECTED", "CV04_POLICY_DRAFT_REJECTED",
        "CV04_POLICY_OVERLAP", "CV04_POLICY_COVERAGE", "CV04_POLICY_PRECONDITION",
        "CV04_POLICY_PUBLICATION_INVALID", "CV04_POLICY_UNKNOWN_REJECTION",
        "CV04_REPORT_FAILED", "CV04_CLEANUP_FAILED", "CV04_UNEXPECTED_FAILURE", "CV04_EXECUTION_PASSED",
    };

    public static void RejectExternalConfiguration(Func<string, string?> read)
    {
        if (RejectedEnvironmentVariables.Any(name => !string.IsNullOrWhiteSpace(read(name))))
        {
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV04_PRECONDITION_FAILED");
        }
    }

    public static void ValidatePreconditions(string repositoryRoot)
    {
        if (Environment.Version.Major != 10 || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV04_PRECONDITION_FAILED");
        }

        var web = Path.Combine(repositoryRoot, "src", "Sgol.Web", "bin", "Release", "net10.0", "Sgol.Web.dll");
        var admin = Path.Combine(repositoryRoot, "src", "Sgol.Admin", "bin", "Release", "net10.0", "Sgol.Admin.dll");
        if (!File.Exists(web) || !File.Exists(admin))
        {
            throw new DemoFailureException("PREFLIGHT", "NONE", "CV04_PRECONDITION_FAILED");
        }
    }

    public static string SanitizeCode(string? code) =>
        code is not null && AllowedCodes.Contains(code) ? code : "CV04_UNEXPECTED_FAILURE";

    public static void ValidateDisposableDatabase(string host, string database, string expected, int port)
    {
        if (host is not ("localhost" or DemoContract.LoopbackAddress) || port <= 0 ||
            !database.StartsWith(DemoContract.DatabasePrefix, StringComparison.Ordinal) || database != expected)
        {
            throw new DemoFailureException("POSTGRESQL", "NONE", "CV04_PRECONDITION_FAILED");
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
