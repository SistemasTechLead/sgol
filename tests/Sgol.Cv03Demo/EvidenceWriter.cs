using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sgol.Cv03Demo;

internal static class EvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string SeedHash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',
        DemoContract.SeedId, DemoContract.PostgreSqlImage, DemoContract.SeaweedImage, DemoContract.ClamAvImage,
        string.Join(',', ScenarioCatalog.All.Select(item => item.Id))))));

    public static void Prepare(string repositoryRoot)
    {
        var root = Path.Combine(repositoryRoot, ".artifacts", "cv03");
        var latest = Path.Combine(root, "latest");
        var previous = Path.Combine(root, "previous-failure");
        Directory.CreateDirectory(latest);
        var report = Path.Combine(latest, $"{DemoContract.TaskId}-report.json");
        var failed = File.Exists(report) && File.ReadAllText(report).Contains("\"overallStatus\": \"FAILED\"", StringComparison.Ordinal);
        if (failed)
        {
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
            Directory.CreateDirectory(previous);
            foreach (var file in Directory.EnumerateFiles(latest))
                File.Copy(file, Path.Combine(previous, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var file in Directory.EnumerateFiles(latest)) File.Delete(file);
        var failures = Path.Combine(latest, "failures");
        if (Directory.Exists(failures)) Directory.Delete(failures, recursive: true);
    }

    public static async Task WriteAsync(string repositoryRoot, DemoState state, DateTimeOffset startedAt,
        DateTimeOffset endedAt, bool cleanupSucceeded, CancellationToken cancellationToken = default)
    {
        var latest = Path.Combine(repositoryRoot, ".artifacts", "cv03", "latest");
        Directory.CreateDirectory(latest);
        var commit = Environment.GetEnvironmentVariable("CV03_COMMIT") ?? "UNAVAILABLE";
        var report = new
        {
            schema = DemoContract.ReportSchema,
            schemaVersion = DemoContract.ReportSchemaVersion,
            taskId = DemoContract.TaskId,
            cut = "CV-03",
            commit,
            environment = new
            {
                framework = Environment.Version.ToString(),
                operatingSystem = Environment.OSVersion.VersionString,
                postgres = DemoContract.PostgreSqlImage,
                seaweed = DemoContract.SeaweedImage,
                clamav = DemoContract.ClamAvImage,
                playwright = "NO_APLICA",
            },
            startedAtUtc = startedAt,
            endedAtUtc = endedAt,
            durationMilliseconds = (long)(endedAt - startedAt).TotalMilliseconds,
            seed = new { id = DemoContract.SeedId, sha256 = SeedHash },
            overallStatus = state.OverallStatus,
            overallCode = state.OverallCode,
            cleanup = cleanupSucceeded ? "PASSED" : "FAILED",
            scenarios = state.Results.Select(result => new
            {
                result.Id,
                name = ScenarioCatalog.Require(result.Id).Name,
                coverage = ScenarioCatalog.Require(result.Id).Coverage,
                rule = ScenarioCatalog.Require(result.Id).Rule,
                result.Status,
                result.TechnicalCode,
                durationMilliseconds = (long)(result.EndedAtUtc - result.StartedAtUtc).TotalMilliseconds,
                result.Facts,
                artifacts = result.Status == "FAILED" ? new[] { $"failures/{result.Id}.json" } : [],
                relatedDefect = (string?)null,
            }),
        };
        await File.WriteAllTextAsync(Path.Combine(latest, $"{DemoContract.TaskId}-report.json"),
            JsonSerializer.Serialize(report, JsonOptions), new UTF8Encoding(false), cancellationToken);

        var markdown = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"# {DemoContract.TaskId} — evidencia de ejecución")
            .AppendLine().AppendLine(CultureInfo.InvariantCulture, $"- Commit: `{commit}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Esquema: `{DemoContract.ReportSchema}/v{DemoContract.ReportSchemaVersion}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Semilla: `{DemoContract.SeedId}` / `{SeedHash}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Resultado: `{state.OverallStatus}` / `{state.OverallCode}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Limpieza: `{report.cleanup}`")
            .AppendLine("- Playwright: `NO_APLICA`")
            .AppendLine().AppendLine("| Escenario | Cobertura | Regla | Resultado | Código |")
            .AppendLine("|---|---|---|---|---|");
        foreach (var result in state.Results)
        {
            var scenario = ScenarioCatalog.Require(result.Id);
            markdown.AppendLine(CultureInfo.InvariantCulture,
                $"| `{result.Id}_{scenario.Name}` | {scenario.Coverage} | {scenario.Rule} | `{result.Status}` | `{result.TechnicalCode}` |");
        }
        await File.WriteAllTextAsync(Path.Combine(latest, $"{DemoContract.TaskId}-report.md"), markdown.ToString(),
            new UTF8Encoding(false), cancellationToken);
    }

    public static async Task WriteFailureAsync(string repositoryRoot, string scenarioId, string code,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(repositoryRoot, ".artifacts", "cv03", "latest", "failures");
        Directory.CreateDirectory(directory);
        var artifact = new { schemaVersion = 1, scenarioId, phase = "SCENARIO", code = DemoSafety.Sanitize(code), occurredAtUtc = DateTimeOffset.UtcNow };
        await File.WriteAllTextAsync(Path.Combine(directory, scenarioId + ".json"),
            JsonSerializer.Serialize(artifact, JsonOptions), new UTF8Encoding(false), cancellationToken);
    }
}
