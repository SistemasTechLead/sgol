using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Sgol.Cv04Demo;

public sealed class NoDockerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ParserAcceptsOnlyCanonicalAutomatedMode()
    {
        Assert.True(DemoOptions.TryParse(["--mode", "Automated"], out var parsed));
        Assert.Equal(DemoMode.Automated, parsed!.Mode);
        Assert.False(DemoOptions.TryParse([], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "automated"], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "Automated", "--url", "https://example.invalid"], out _));
    }

    [Fact]
    public void CatalogIsClosedAndComplete()
    {
        Assert.Equal(24, ScenarioCatalog.All.Count);
        Assert.Equal(Enumerable.Range(1, 24).Select(index => $"S{index:00}"), ScenarioCatalog.All.Select(item => item.Id));
        Assert.Equal(17, DemoContract.Phases.Count);
        Assert.Equal(8, DemoContract.TaskCodes.Count);
        Assert.Equal(ScenarioCatalog.All.Count, ScenarioCatalog.All.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EvidenceHasOnlyClosedSanitizedFields()
    {
        const string forbidden = "synthetic-secret-marker";
        var evidence = PhaseEvidence.Failed("MFA_VERIFY", "S22", forbidden, 1, 12);
        var json = JsonSerializer.Serialize(evidence, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["code", "counts", "durationMs", "exit", "phase", "scenario", "state"], names);
        Assert.Equal("CV04_UNEXPECTED_FAILURE", document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoAssemblyContainsNoClaimsPrincipalConstructionOrCv03Reference()
    {
        var root = FindRepositoryRoot();
        var source = string.Join('\n', Directory.GetFiles(Path.Combine(root, "tests", "Sgol.Cv04Demo"), "*.cs")
            .Where(path => !path.EndsWith("Tests.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        Assert.DoesNotContain("new ClaimsPrincipal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ClaimsIdentity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv03Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv02Demo", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeExitIsCapturedImmediatelyFromTerminatedProcess()
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("dotnet", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        Assert.True(process.Start());
        process.WaitForExit();
        Assert.Equal(0, DemoSafety.CaptureExit(process));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
