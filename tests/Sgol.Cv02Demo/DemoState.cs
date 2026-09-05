using System.Collections.Concurrent;

namespace Sgol.Cv02Demo;

internal sealed record DemoScenarioResult(
    string Id,
    string Status,
    string Message,
    string TechnicalCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    IReadOnlyDictionary<string, string> Facts)
{
    public static DemoScenarioResult Failed(
        string id,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string errorCode) =>
        new(id, "FAILED", "El escenario no pudo completarse.", DemoSafety.Sanitize(errorCode),
            startedAt, endedAt, new Dictionary<string, string>());
}

internal sealed class DemoState
{
    private readonly ConcurrentDictionary<string, DemoScenarioResult> results = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> browserVersions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> failureArtifacts = new(StringComparer.Ordinal);

    public string OverallStatus { get; private set; } = "PENDING";

    public string OverallCode { get; private set; } = "CV02_EXECUTION_PENDING";

    public IReadOnlyDictionary<string, string> BrowserVersions => browserVersions;

    public IReadOnlyList<string> FailureArtifacts => failureArtifacts.Keys.Order(StringComparer.Ordinal).ToArray();

    public IReadOnlyList<DemoScenarioResult> Results => ScenarioCatalog.All
        .Select(item => results.GetValueOrDefault(item.Id))
        .Where(item => item is not null)
        .Cast<DemoScenarioResult>()
        .ToArray();

    public void Set(DemoScenarioResult result)
    {
        results[result.Id] = result;
        if (result.Status == "FAILED") MarkFailure(result.TechnicalCode);
    }

    public DemoScenarioResult? Get(string id) => results.GetValueOrDefault(id);

    public void MarkPassed()
    {
        OverallStatus = "PASSED";
        OverallCode = "CV02_EXECUTION_PASSED";
    }

    public void MarkFailure(string errorCode)
    {
        OverallStatus = "FAILED";
        OverallCode = DemoSafety.Sanitize(errorCode);
    }

    public void RecordBrowser(string name, string version) => browserVersions[name] = version;

    public void RecordFailureArtifact(string relativePath) => failureArtifacts[relativePath] = 0;
}
