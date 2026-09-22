using Xunit;

namespace Sgol.Cv05Demo;

public sealed class Cv05PureContractTests
{
    [Fact]
    public void ClosedCatalogHasTwentyTwoUniqueApprovedScenarios()
    {
        Assert.Equal(22, ScenarioCatalog.All.Count);
        Assert.Equal(Enumerable.Range(1, 22).Select(number => $"S{number:D2}"),
            ScenarioCatalog.All.Select(item => item.Id));
        Assert.Equal(22, ScenarioCatalog.All.Select(item => item.Name).Distinct().Count());
    }

    [Fact]
    public void CommandRejectsUnapprovedInputsAndExternalConfiguration()
    {
        Assert.True(DemoOptions.TryParse(["--mode", "Automated"], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "Interactive"], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "Automated", "--url", "https://example.invalid"], out _));
        Assert.Throws<DemoFailureException>(() => DemoSafety.RejectExternalConfiguration(name =>
            name == "ConnectionStrings__Sgol" ? "synthetic" : null));
        DemoSafety.RejectExternalConfiguration(_ => null);
    }

    [Fact]
    public void ReportPassRequiresCompleteMatrixTwoCyclesAndCleanup()
    {
        var report = ValidReport();
        EvidenceWriter.Validate(report);
        var missing = report with
        {
            Cycles = [report.Cycles[0] with
            {
                Evidence = report.Cycles[0].Evidence.Where(item => item.Scenario != "S15").ToArray(),
            }, report.Cycles[1]],
        };
        Assert.Throws<DemoFailureException>(() => EvidenceWriter.Validate(missing));
        Assert.Throws<DemoFailureException>(() => EvidenceWriter.Validate(report with { CleanupState = "FAILED" }));
        Assert.Throws<DemoFailureException>(() => EvidenceWriter.Validate(report with
        {
            Cycles = [report.Cycles[0], report.Cycles[1] with { FunctionalFingerprint = new string('b', 64) }],
        }));
    }

    [Fact]
    public async Task ReportWriterCreatesOnlyCv05Originals()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sgol-cv05-pure-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            await EvidenceWriter.WriteAsync(root, ValidReport(), CancellationToken.None);
            var latest = Path.Combine(root, ".artifacts", "cv05", "latest");
            Assert.True(File.Exists(Path.Combine(latest, "TECH-E2E-CV-05-report.json")));
            Assert.True(File.Exists(Path.Combine(latest, "TECH-E2E-CV-05-report.md")));
            Assert.False(File.Exists(Path.Combine(latest, "TECH-E2E-CV-04-report.json")));
        }
        finally
        {
            var resolved = Path.GetFullPath(root);
            var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            Assert.Equal(parent, Path.GetDirectoryName(resolved)?.TrimEnd(Path.DirectorySeparatorChar));
            Assert.StartsWith("sgol-cv05-pure-", Path.GetFileName(resolved), StringComparison.Ordinal);
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }

    [Fact]
    public void Hu035BootstrapWaitsForFinalPostgreSqlTcpServer()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);

        var script = File.ReadAllText(Path.Combine(directory.FullName, "scripts", "operations",
            "new-tech-ops-synthetic-environment.ps1"));
        Assert.Contains("--health-cmd 'pg_isready -h 127.0.0.1 -U postgres -d postgres'", script,
            StringComparison.Ordinal);
    }

    private static DemoReport ValidReport()
    {
        var first = ScenarioCatalog.All.Where(item => item.Id is not ("S21" or "S22"))
            .Select(item => PhaseEvidence.Passed("REPORT", item.Id, 0)).Append(
                PhaseEvidence.Passed("CLEANUP", "S22", 0)).ToArray();
        var second = ScenarioCatalog.All.Select(item => PhaseEvidence.Passed(
            item.Id == "S22" ? "CLEANUP" : "REPORT", item.Id, 0)).ToArray();
        return new DemoReport(DemoContract.ReportSchema, 1, DemoContract.TaskId, DemoContract.CutId,
            DemoContract.BaseCommit, DemoContract.BaseCommit, "WINDOWS", "X64", "10.0.0",
            DemoContract.PostgreSqlImage, DemoContract.SeaweedImage, DemoContract.ClamAvImage,
            "sha256:" + new string('a', 64), DemoContract.SeedId, new string('a', 64),
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow, 60_000,
            [new CycleResult(1, first, new string('a', 64)),
                new CycleResult(2, second, new string('a', 64))],
            ScenarioCatalog.All, "NO_APLICA", "NO_APLICA", "PASSED", "PASSED", "PASSED", "PASSED");
    }
}
