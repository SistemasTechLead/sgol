using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Sgol.Cv04Demo;

internal sealed class Cv04DemoApplication
{
    public static async Task<int> RunAsync(DemoOptions options)
    {
        _ = options;
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var repositoryRoot = FindRepositoryRoot();
        var commit = Environment.GetEnvironmentVariable("CV04_COMMIT") ?? "UNKNOWN";
        var cycles = new List<CycleResult>();
        var cleanupPassed = true;
        DemoFailureException? primaryFailure = null;

        for (var cycleNumber = 1; cycleNumber <= 2 && primaryFailure is null; cycleNumber++)
        {
            var infrastructure = new Cv04Infrastructure();
            Cv04ScenarioEngine? engine = null;
            var cycleWatch = Stopwatch.StartNew();
            try
            {
                await infrastructure.StartAsync(CancellationToken.None);
                engine = new Cv04ScenarioEngine(infrastructure);
                var executed = await engine.ExecuteAsync(cycleNumber, CancellationToken.None);
                var cycleEvidence = new List<PhaseEvidence>
                {
                    PhaseEvidence.Passed("PREFLIGHT", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("POSTGRESQL", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("HTTPS", "NONE", cycleWatch.ElapsedMilliseconds),
                };
                cycleEvidence.AddRange(executed.Evidence);
                cycles.Add(executed with { Evidence = cycleEvidence });
            }
            catch (DemoFailureException exception)
            {
                primaryFailure = exception;
                var prior = engine?.CurrentEvidence ?? [];
                cycles.Add(new CycleResult(
                    cycleNumber,
                    prior.Append(PhaseEvidence.Failed(exception.Phase, exception.Scenario, exception.Code, exception.Exit,
                        cycleWatch.ElapsedMilliseconds)).ToArray(),
                    string.Empty));
            }
            catch
            {
                primaryFailure = new DemoFailureException(
                    "REPORT", "NONE", "CV04_UNEXPECTED_FAILURE");
                cycles.Add(new CycleResult(
                    cycleNumber,
                    [PhaseEvidence.Failed("REPORT", "NONE", "CV04_UNEXPECTED_FAILURE", 1,
                        cycleWatch.ElapsedMilliseconds)],
                    string.Empty));
            }
            finally
            {
                var cleaned = await infrastructure.CleanupAsync();
                cleanupPassed &= cleaned;
                var index = cycles.FindIndex(item => item.Cycle == cycleNumber);
                if (index >= 0)
                {
                    var items = cycles[index].Evidence.ToList();
                    items.Add(cleaned
                        ? PhaseEvidence.Passed("CLEANUP", "S24", cycleWatch.ElapsedMilliseconds,
                            new Dictionary<string, int> { ["ownedResources"] = 0 })
                        : PhaseEvidence.Failed("CLEANUP", "S24", "CV04_CLEANUP_FAILED", 1,
                            cycleWatch.ElapsedMilliseconds));
                    cycles[index] = cycles[index] with { Evidence = items };
                }
                if (!cleaned && primaryFailure is null)
                {
                    primaryFailure = new DemoFailureException("CLEANUP", "S24", "CV04_CLEANUP_FAILED");
                }
            }
        }

        if (primaryFailure is null)
        {
            if (cycles.Count != 2 || cycles[0].FunctionalFingerprint != cycles[1].FunctionalFingerprint)
            {
                primaryFailure = new DemoFailureException("REPORT", "S23", "CV04_SCENARIO_FAILED");
            }
            else
            {
                var second = cycles[1];
                cycles[1] = second with
                {
                    Evidence = second.Evidence.Append(PhaseEvidence.Passed(
                        "REPORT", "S23", 0,
                        new Dictionary<string, int> { ["matchingFingerprints"] = 2 })).ToArray(),
                };
            }
        }

        watch.Stop();
        var finalState = primaryFailure is null && cleanupPassed ? "PASSED" : "FAILED";
        var report = new DemoReport(
            DemoContract.ReportSchema,
            DemoContract.ReportSchemaVersion,
            DemoContract.TaskId,
            DemoContract.CutId,
            commit,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.Version.ToString(),
            DemoContract.PostgreSqlImage,
            DemoContract.SeedId,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(DemoContract.SeedId))),
            startedAt,
            DateTimeOffset.UtcNow,
            watch.ElapsedMilliseconds,
            cycles,
            "NO_APLICA",
            "NO_APLICA",
            "PASSED",
            cleanupPassed ? "PASSED" : "FAILED",
            finalState);

        try
        {
            await EvidenceWriter.WriteAsync(repositoryRoot, report, CancellationToken.None);
        }
        catch
        {
            return primaryFailure?.Exit ?? 1;
        }

        return primaryFailure?.Exit ?? (cleanupPassed ? 0 : 1);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
