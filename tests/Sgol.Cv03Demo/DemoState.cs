namespace Sgol.Cv03Demo;

internal sealed record DemoScenarioResult(
    string Id, string Status, string TechnicalCode, DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc, IReadOnlyDictionary<string, long> Facts)
{
    public static DemoScenarioResult Passed(string id, DateTimeOffset start, IReadOnlyDictionary<string, long>? facts = null) =>
        new(id, "PASSED", "CV03_EXECUTION_PASSED", start, DateTimeOffset.UtcNow, facts ?? new Dictionary<string, long>());

    public static DemoScenarioResult Failed(string id, DateTimeOffset start, string code) =>
        new(id, "FAILED", DemoSafety.Sanitize(code), start, DateTimeOffset.UtcNow, new Dictionary<string, long>());
}

internal sealed class DemoState
{
    private readonly List<DemoScenarioResult> results = [];
    public IReadOnlyList<DemoScenarioResult> Results => results;
    public string OverallStatus { get; private set; } = "FAILED";
    public string OverallCode { get; private set; } = "CV03_SCENARIO_FAILED";

    public void Add(DemoScenarioResult result) => results.Add(result);
    public void MarkPassed() { OverallStatus = "PASSED"; OverallCode = "CV03_EXECUTION_PASSED"; }
    public void MarkFailure(string code) { OverallStatus = "FAILED"; OverallCode = DemoSafety.Sanitize(code); }
}
