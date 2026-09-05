namespace Sgol.Cv02Demo;

internal static class DemoContract
{
    public const string TaskId = "TECH-E2E-CV-02";
    public const string SeedId = "CV02-SEED-V1";
    public const string PostgreSqlImage = "postgres:18.6-alpine3.23";
    public const string PlaywrightVersion = "1.62.0";
    public const string ChromiumVersion = "151.0.7922.34";
    public const string WebKitVersion = "26.5";
    public const string DatabasePrefix = "sgol_cv02_";
    public const string LoopbackAddress = "127.0.0.1";

    public static readonly IReadOnlySet<string> FunctionalRoutes = new HashSet<string>(StringComparer.Ordinal)
    {
        "/cv02/calendario",
        "/cv02/recurrencias",
        "/cv02/plan-semanal",
    };
}

internal enum DemoMode
{
    Automated,
    Interactive,
}

internal sealed record DemoOptions(DemoMode Mode)
{
    public static bool TryParse(string[] args, out DemoOptions? options)
    {
        options = null;
        if (args.Length != 2 || !string.Equals(args[0], "--mode", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Enum.TryParse<DemoMode>(args[1], ignoreCase: false, out var mode))
        {
            return false;
        }

        options = new DemoOptions(mode);
        return true;
    }
}

internal static class DemoSafety
{
    private static readonly string[] RejectedEnvironmentVariables =
    [
        "ConnectionStrings__Sgol",
        "DATABASE_URL",
        "PGHOST",
        "PGPORT",
        "PGDATABASE",
        "PGUSER",
        "PGPASSWORD",
    ];

    public static void RejectExternalDatabaseConfiguration(
        Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        if (RejectedEnvironmentVariables.Any(name =>
                !string.IsNullOrWhiteSpace(readEnvironmentVariable(name))))
        {
            throw new DemoSafetyException("CV02_EXTERNAL_DATABASE_CONFIGURATION_REJECTED");
        }
    }

    public static void ValidateDisposableDatabase(
        string host,
        string database,
        string expectedDatabase,
        int port)
    {
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, DemoContract.LoopbackAddress, StringComparison.Ordinal))
        {
            throw new DemoSafetyException("CV02_DATABASE_NOT_LOOPBACK");
        }

        if (port <= 0 ||
            !database.StartsWith(DemoContract.DatabasePrefix, StringComparison.Ordinal) ||
            !string.Equals(database, expectedDatabase, StringComparison.Ordinal))
        {
            throw new DemoSafetyException("CV02_DATABASE_NOT_DISPOSABLE");
        }
    }

    public static string Sanitize(string? errorCode) => errorCode switch
    {
        "CV02_EXTERNAL_DATABASE_CONFIGURATION_REJECTED" => errorCode,
        "CV02_DATABASE_NOT_LOOPBACK" => errorCode,
        "CV02_DATABASE_NOT_DISPOSABLE" => errorCode,
        "CV02_DATABASE_START_FAILED" => errorCode,
        "CV02_HOST_START_FAILED" => errorCode,
        "CV02_BROWSER_EXECUTION_FAILED" => errorCode,
        "CV02_EVIDENCE_PREPARE_FAILED" => errorCode,
        "CV02_BROWSER_NOT_INSTALLED" => errorCode,
        "CV02_BROWSER_INSTALL_LOCK_PRESENT" => errorCode,
        "CV02_SCENARIO_FAILED" => errorCode,
        "CV02_CLEANUP_FAILED" => errorCode,
        "CV02_ARTIFACT_WRITE_FAILED" => errorCode,
        "CV02_EXECUTION_PASSED" => errorCode,
        _ => "CV02_UNEXPECTED_FAILURE",
    };

    public static string StageFailure(string stage) => stage switch
    {
        "EVIDENCE" => "CV02_EVIDENCE_PREPARE_FAILED",
        "DATABASE" => "CV02_DATABASE_START_FAILED",
        "HOST" => "CV02_HOST_START_FAILED",
        "BROWSER" => "CV02_BROWSER_EXECUTION_FAILED",
        _ => "CV02_UNEXPECTED_FAILURE",
    };
}

internal sealed class DemoSafetyException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}

internal sealed class DemoScenarioAssertionException() : Exception("CV02_SCENARIO_FAILED");
