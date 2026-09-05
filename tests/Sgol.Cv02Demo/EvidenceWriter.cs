using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Sgol.Cv02Demo;

internal static class EvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string SeedHash => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join('|',
            DemoContract.SeedId,
            DemoContract.PostgreSqlImage,
            string.Join(',', ScenarioCatalog.All.Select(item => item.Id))))));

    public static void Prepare(string repositoryRoot)
    {
        var root = Path.Combine(repositoryRoot, ".artifacts", "cv02");
        var latest = Path.Combine(root, "latest");
        var previousFailure = Path.Combine(root, "previous-failure");
        Directory.CreateDirectory(latest);
        if (Directory.Exists(Path.Combine(latest, "failures")))
        {
            Directory.CreateDirectory(previousFailure);
            DeleteFiles(previousFailure);
            DeleteFiles(Path.Combine(previousFailure, "failures"));
            CopyKnownEvidence(latest, previousFailure);
            DeleteFiles(Path.Combine(latest, "failures"));
        }
    }

    public static async Task WriteAsync(
        string repositoryRoot,
        DemoState state,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        bool cleanupSucceeded,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(repositoryRoot, ".artifacts", "cv02");
        var latest = Path.Combine(root, "latest");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(latest);
        var failureArtifacts = state.FailureArtifacts;
        var results = state.Results;
        var report = new
        {
            schemaVersion = 1,
            taskId = DemoContract.TaskId,
            commit = Environment.GetEnvironmentVariable("CV02_COMMIT") ?? "UNAVAILABLE",
            environment = new
            {
                framework = Environment.Version.ToString(),
                operatingSystem = Environment.OSVersion.VersionString,
                postgres = DemoContract.PostgreSqlImage,
                playwright = DemoContract.PlaywrightVersion,
                chromium = DemoContract.ChromiumVersion,
                webkit = DemoContract.WebKitVersion,
            },
            startedAtUtc = startedAt,
            endedAtUtc = endedAt,
            seed = new { id = DemoContract.SeedId, sha256 = SeedHash },
            overallStatus = state.OverallStatus,
            overallCode = state.OverallCode,
            browsers = state.BrowserVersions,
            cleanup = cleanupSucceeded ? "PASSED" : "FAILED",
            scenarios = results.Select(item => new
            {
                item.Id,
                item.Status,
                item.TechnicalCode,
                item.StartedAtUtc,
                item.EndedAtUtc,
                item.Facts,
                coverage = ScenarioCatalog.Require(item.Id).Coverage,
                artifacts = item.Status == "FAILED" ? failureArtifacts : [],
                relatedDefect = (string?)null,
            }),
        };

        await File.WriteAllTextAsync(
            Path.Combine(latest, $"{DemoContract.TaskId}-report.json"),
            JsonSerializer.Serialize(report, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        var markdown = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"# {DemoContract.TaskId} — evidencia de ejecución")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"- Commit: `{report.commit}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Inicio UTC: `{startedAt:O}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Fin UTC: `{endedAt:O}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Semilla: `{DemoContract.SeedId}` / `{SeedHash}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Resultado global: `{report.overallStatus}` / `{report.overallCode}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Navegadores efectivos: `{string.Join(", ", state.BrowserVersions.Select(item => $"{item.Key}={item.Value}"))}`")
            .AppendLine(CultureInfo.InvariantCulture, $"- Limpieza: `{report.cleanup}`")
            .AppendLine()
            .AppendLine("| Escenario | Cobertura | Resultado | Código |")
            .AppendLine("|---|---|---|---|");
        foreach (var item in results)
        {
            markdown.AppendLine(CultureInfo.InvariantCulture, $"| `{item.Id}` | {ScenarioCatalog.Require(item.Id).Coverage} | `{item.Status}` | `{item.TechnicalCode}` |");
        }

        await File.WriteAllTextAsync(
            Path.Combine(latest, $"{DemoContract.TaskId}-report.md"),
            markdown.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        if (state.OverallStatus == "PASSED")
        {
            var failures = Path.Combine(latest, "failures");
            if (Directory.Exists(failures))
            {
                foreach (var file in Directory.EnumerateFiles(failures)) File.Delete(file);
            }
        }
    }

    private static void CopyKnownEvidence(string source, string destination)
    {
        foreach (var report in Directory.EnumerateFiles(source, $"{DemoContract.TaskId}-report.*"))
            File.Copy(report, Path.Combine(destination, Path.GetFileName(report)), overwrite: true);
        var sourceFailures = Path.Combine(source, "failures");
        var destinationFailures = Path.Combine(destination, "failures");
        Directory.CreateDirectory(destinationFailures);
        foreach (var artifact in Directory.EnumerateFiles(sourceFailures))
            File.Copy(artifact, Path.Combine(destinationFailures, Path.GetFileName(artifact)), overwrite: true);
    }

    private static void DeleteFiles(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
    }
}
