using System.Text.Json;

namespace Sgol.FrontendBrowserTests;

internal sealed record Front005Step(string Route, string State, long DurationMs, string[] Roles);
internal sealed record Front005Run(string Task, string Sha, int Cycle, string Profile, string Browser,
    string Platform, bool Https, Front005Step[] Steps, bool Cleanup, string Failure);

internal static class TechFront005Report
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    internal static readonly string[] Routes = ["R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9"];
    internal static readonly string[] Roles = ["DIRECCION", "ADMINISTRACION", "SUBCOORDINACION", "PISO_VENTAS"];

    internal static void Validate(Front005Run run, bool complete)
    {
        if (run.Task != "TECH-FRONT-005" || run.Sha.Length != 40 || run.Sha.Any(c => !Uri.IsHexDigit(c)) ||
            run.Cycle is < 1 or > 2 || run.Profile is not ("desktop" or "mobile") ||
            run.Platform != "Windows AMD64 / Docker Linux AMD64" || !run.Https ||
            run.Browser.Length > 50 || run.Browser.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or ' ')) ||
            run.Failure is not ("NONE" or "HARNESS_FAILED" or "CLEANUP_FAILED") ||
            run.Steps.Any(s => !Routes.Contains(s.Route, StringComparer.Ordinal) || s.State is not ("PASS" or "FAIL") ||
                s.DurationMs < 0 || s.Roles.Any(r => !Roles.Contains(r, StringComparer.Ordinal))) ||
            run.Steps.Select(s => s.Route).Distinct(StringComparer.Ordinal).Count() != run.Steps.Length)
            throw new InvalidOperationException("Invalid sanitized frontend report.");
        if (complete && (!run.Cleanup || run.Failure != "NONE" ||
            !run.Steps.Select(s => s.Route).SequenceEqual(Routes) || run.Steps.Any(s => s.State != "PASS") ||
            !Roles.All(r => run.Steps.Any(s => s.Roles.Contains(r, StringComparer.Ordinal)))))
            throw new InvalidOperationException("Incomplete frontend demo.");
    }

    internal static async Task WriteAsync(string directory, Front005Run run)
    {
        Validate(run, complete: run.Failure == "NONE");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(run, SerializerOptions));
    }

    internal static void ValidateCycles(IReadOnlyList<Front005Run> runs, string sha)
    {
        if (runs.Count != 4 || runs.Any(r => r.Sha != sha) ||
            runs.Select(r => (r.Cycle, r.Profile)).Distinct().Count() != 4)
            throw new InvalidOperationException("Two complete cycles for the current head are required.");
        foreach (var run in runs) Validate(run, complete: true);
    }
}
