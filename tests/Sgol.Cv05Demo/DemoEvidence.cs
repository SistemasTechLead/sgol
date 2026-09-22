using System.Text.Json;

namespace Sgol.Cv05Demo;

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
        new(phase, scenario, 0, "CV05_NONE", counts ?? EmptyCounts, durationMs, "PASSED");

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
    string BaseCommit,
    string OperatingSystem,
    string Architecture,
    string DotnetVersion,
    string PostgreSqlImage,
    string SeaweedImage,
    string ClamAvImage,
    string OciImageDigest,
    string Seed,
    string SeedFingerprint,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs,
    IReadOnlyList<CycleResult> Cycles,
    IReadOnlyList<DemoScenario> Scenarios,
    string Browser,
    string Accessibility,
    string ExecutionState,
    string ReportState,
    string CleanupState,
    string FinalState);

internal static class EvidenceWriter
{
    private static readonly HashSet<string> AllowedCountKeys = new(StringComparer.Ordinal)
    {
        "accounts", "obligations", "matchingFingerprints", "ownedResources",
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task WriteAsync(string repositoryRoot, DemoReport report, CancellationToken cancellationToken)
    {
        Validate(report);
        var root = Path.Combine(repositoryRoot, ".artifacts", "cv05");
        var latest = Path.Combine(root, "latest");
        var previous = Path.Combine(root, "previous-failure");

        if (Directory.Exists(latest))
        {
            var priorReport = Path.Combine(latest, "TECH-E2E-CV-05-report.json");
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
        var jsonPath = Path.Combine(latest, "TECH-E2E-CV-05-report.json");
        var markdownPath = Path.Combine(latest, "TECH-E2E-CV-05-report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, Markdown(report), cancellationToken);
        if (new FileInfo(jsonPath).Length == 0 || new FileInfo(markdownPath).Length == 0)
            throw new DemoFailureException("REPORT", "S21", "CV05_REPORT_FAILED");
        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath, cancellationToken));
        if (written.RootElement.GetProperty("schemaId").GetString() != DemoContract.ReportSchema ||
            written.RootElement.GetProperty("commit").GetString() != report.Commit)
            throw new DemoFailureException("REPORT", "S21", "CV05_REPORT_FAILED");
    }

    internal static void Validate(DemoReport report)
    {
        if (report.SchemaId != DemoContract.ReportSchema ||
            report.SchemaVersion != DemoContract.ReportSchemaVersion ||
            report.Task != DemoContract.TaskId || report.Cut != DemoContract.CutId ||
            report.BaseCommit != DemoContract.BaseCommit ||
            report.Commit.Length != 40 || report.Commit.Any(item => !Uri.IsHexDigit(item)) ||
            report.Browser != "NO_APLICA" || report.Accessibility != "NO_APLICA" ||
            !report.Scenarios.Select(item => item.Id).SequenceEqual(ScenarioCatalog.All.Select(item => item.Id)) ||
            report.Cycles.Select(item => item.Cycle).Distinct().Count() != report.Cycles.Count ||
            report.Cycles.Any(item => item.Evidence.Any(evidence =>
                !DemoContract.Phases.Contains(evidence.Phase, StringComparer.Ordinal) ||
                evidence.Scenario != "NONE" && !ScenarioCatalog.All.Any(scenario => scenario.Id == evidence.Scenario) ||
                DemoSafety.SanitizeCode(evidence.Code) != evidence.Code ||
                evidence.State is not ("PASSED" or "FAILED" or "NOT_APPLICABLE") ||
                evidence.Exit < 0 || evidence.DurationMs < 0 ||
                evidence.Counts.Any(count => !AllowedCountKeys.Contains(count.Key) || count.Value < 0))))
            throw new DemoFailureException("REPORT", "S21", "CV05_REPORT_FAILED");

        if (report.FinalState != "PASSED") return;
        if (report.ExecutionState != "PASSED" || report.ReportState != "PASSED" ||
            report.CleanupState != "PASSED" || report.Cycles.Count != 2 ||
            report.OciImageDigest.Length != 71 ||
            !report.OciImageDigest.StartsWith("sha256:", StringComparison.Ordinal) ||
            report.OciImageDigest[7..].Any(item => !Uri.IsHexDigit(item)) ||
            report.Cycles[0].FunctionalFingerprint != report.Cycles[1].FunctionalFingerprint)
            throw new DemoFailureException("REPORT", "S21", "CV05_REPORT_FAILED");
        foreach (var cycle in report.Cycles)
        {
            foreach (var scenario in ScenarioCatalog.All.Where(item => item.Id is not ("S21" or "S22")))
                if (cycle.Evidence.Count(item => item.Scenario == scenario.Id && item.State == "PASSED" &&
                    item.Exit == 0) != 1)
                    throw new DemoFailureException("REPORT", scenario.Id, "CV05_REPORT_FAILED");
            if (cycle.Evidence.Count(item => item.Scenario == "S22" && item.State == "PASSED") != 1 ||
                cycle.Evidence.Any(item => item.State == "FAILED"))
                throw new DemoFailureException("REPORT", "S22", "CV05_REPORT_FAILED");
        }
        if (report.Cycles[1].Evidence.Count(item => item.Scenario == "S21" && item.State == "PASSED") != 1)
            throw new DemoFailureException("REPORT", "S21", "CV05_REPORT_FAILED");
    }

    private static string Markdown(DemoReport report)
    {
        var lines = new List<string>
        {
            "# TECH-E2E-CV-05 report",
            string.Empty,
            $"- State: `{report.FinalState}`",
            $"- Commit: `{report.Commit}`",
            $"- Base: `{report.BaseCommit}`",
            $"- OCI image digest: `{report.OciImageDigest}`",
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
