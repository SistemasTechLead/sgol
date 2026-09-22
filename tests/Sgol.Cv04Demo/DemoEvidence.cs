using System.Text.Json;

namespace Sgol.Cv04Demo;

internal sealed record PhaseEvidence(
    string Phase,
    string Scenario,
    int Exit,
    string Code,
    IReadOnlyDictionary<string, int> Counts,
    long DurationMs,
    string State)
{
    public static PhaseEvidence Passed(string phase, string scenario, long durationMs, IReadOnlyDictionary<string, int>? counts = null) =>
        new(phase, scenario, 0, "CV04_NONE", counts ?? EmptyCounts, durationMs, "PASSED");

    public static PhaseEvidence Failed(string phase, string scenario, string code, int exit, long durationMs) =>
        new(phase, scenario, exit, DemoSafety.SanitizeCode(code), EmptyCounts, durationMs, "FAILED");

    private static IReadOnlyDictionary<string, int> EmptyCounts { get; } = new Dictionary<string, int>();
}

internal sealed record CycleResult(int Cycle, IReadOnlyList<PhaseEvidence> Evidence, string FunctionalFingerprint);

internal sealed record DemoReport(
    string SchemaId,
    int SchemaVersion,
    string Task,
    string Cut,
    string Commit,
    string OperatingSystem,
    string Architecture,
    string DotnetVersion,
    string PostgreSqlImage,
    string Seed,
    string SeedFingerprint,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs,
    IReadOnlyList<CycleResult> Cycles,
    string Browser,
    string Accessibility,
    string ReportState,
    string CleanupState,
    string FinalState);

internal static class EvidenceWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task WriteAsync(string repositoryRoot, DemoReport report, CancellationToken cancellationToken)
    {
        var root = Path.Combine(repositoryRoot, ".artifacts", "cv04");
        var latest = Path.Combine(root, "latest");
        var previous = Path.Combine(root, "previous-failure");

        if (Directory.Exists(latest))
        {
            var priorReport = Path.Combine(latest, "TECH-E2E-CV-04-report.json");
            var failed = File.Exists(priorReport) && (await File.ReadAllTextAsync(priorReport, cancellationToken))
                .Contains("\"finalState\": \"FAILED\"", StringComparison.Ordinal);
            if (failed)
            {
                if (Directory.Exists(previous))
                {
                    Directory.Delete(previous, recursive: true);
                }
                Directory.Move(latest, previous);
            }
            else
            {
                Directory.Delete(latest, recursive: true);
            }
        }

        Directory.CreateDirectory(latest);
        var jsonPath = Path.Combine(latest, "TECH-E2E-CV-04-report.json");
        var markdownPath = Path.Combine(latest, "TECH-E2E-CV-04-report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, Markdown(report), cancellationToken);
    }

    private static string Markdown(DemoReport report)
    {
        var lines = new List<string>
        {
            "# TECH-E2E-CV-04 report",
            string.Empty,
            $"- State: `{report.FinalState}`",
            $"- Commit: `{report.Commit}`",
            $"- Seed: `{report.Seed}`",
            $"- Browser: `{report.Browser}`",
            $"- Accessibility: `{report.Accessibility}`",
            $"- Cleanup: `{report.CleanupState}`",
            string.Empty,
            "| Cycle | Phase | Scenario | Exit | Code | Duration ms | State |",
            "|---:|---|---|---:|---|---:|---|",
        };
        foreach (var cycle in report.Cycles)
            foreach (var item in cycle.Evidence)
            {
                lines.Add($"| {cycle.Cycle} | `{item.Phase}` | `{item.Scenario}` | {item.Exit} | `{item.Code}` | {item.DurationMs} | `{item.State}` |");
            }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
