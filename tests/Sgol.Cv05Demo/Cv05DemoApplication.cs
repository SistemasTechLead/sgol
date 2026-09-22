using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Sgol.Cv05Demo;

internal sealed class Cv05DemoApplication
{
    public static async Task<int> RunAsync(DemoOptions options)
    {
        _ = options;
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var repositoryRoot = FindRepositoryRoot();
        var commit = Environment.GetEnvironmentVariable("CV05_COMMIT") ?? "UNKNOWN";
        var cycles = new List<CycleResult>();
        var cycleCleanups = new Dictionary<int, bool>();
        var cleanupPassed = true;
        DemoFailureException? primaryFailure = null;
        var image = new Cv05Image(repositoryRoot, commit);

        try
        {
            DemoSafety.RejectExternalConfiguration(Environment.GetEnvironmentVariable);
            DemoSafety.ValidatePreconditions(repositoryRoot);
            await image.BuildAsync(CancellationToken.None);
        }
        catch (DemoFailureException exception)
        {
            primaryFailure = exception;
            cycles.Add(new CycleResult(1,
                [PhaseEvidence.Failed(exception.Phase, "NONE", exception.Code, exception.Exit, watch.ElapsedMilliseconds)],
                string.Empty));
        }
        catch
        {
            primaryFailure = new DemoFailureException("OCI", "NONE", "CV05_UNEXPECTED_FAILURE");
            cycles.Add(new CycleResult(1,
                [PhaseEvidence.Failed("OCI", "NONE", "CV05_UNEXPECTED_FAILURE", 1, watch.ElapsedMilliseconds)],
                string.Empty));
        }

        for (var cycleNumber = 1; cycleNumber <= 2 && primaryFailure is null; cycleNumber++)
        {
            var infrastructure = new Cv05Infrastructure(image.ImageId);
            Cv05ScenarioEngine? engine = null;
            var cycleWatch = Stopwatch.StartNew();
            try
            {
                await infrastructure.StartAsync(CancellationToken.None);
                engine = new Cv05ScenarioEngine(infrastructure);
                var executed = await engine.ExecuteAsync(cycleNumber, CancellationToken.None);
                var cycleEvidence = new List<PhaseEvidence>
                {
                    PhaseEvidence.Passed("PREFLIGHT", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("POSTGRESQL", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("STORES", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("ANTIMALWARE", "NONE", cycleWatch.ElapsedMilliseconds),
                    PhaseEvidence.Passed("OCI", "NONE", cycleWatch.ElapsedMilliseconds),
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
                    "REPORT", "NONE", "CV05_UNEXPECTED_FAILURE");
                cycles.Add(new CycleResult(
                    cycleNumber,
                    [PhaseEvidence.Failed("REPORT", "NONE", "CV05_UNEXPECTED_FAILURE", 1,
                        cycleWatch.ElapsedMilliseconds)],
                    string.Empty));
            }
            finally
            {
                var cleaned = await infrastructure.CleanupAsync();
                cycleCleanups[cycleNumber] = cleaned;
                cleanupPassed &= cleaned;
                if (!cleaned && primaryFailure is null)
                {
                    primaryFailure = new DemoFailureException("CLEANUP", "S22", "CV05_CLEANUP_FAILED");
                }
            }
        }

        var imageCleaned = false;
        try { imageCleaned = await image.CleanupAsync(); }
        catch { imageCleaned = false; }
        cleanupPassed &= imageCleaned;
        foreach (var cycle in cycles.ToArray())
        {
            var cleaned = cycleCleanups.TryGetValue(cycle.Cycle, out var value) && value && imageCleaned;
            var item = cleaned
                ? PhaseEvidence.Passed("CLEANUP", "S22", watch.ElapsedMilliseconds,
                    new Dictionary<string, int> { ["ownedResources"] = 0 })
                : PhaseEvidence.Failed("CLEANUP", "S22", "CV05_CLEANUP_FAILED", 1, watch.ElapsedMilliseconds);
            cycles[cycles.FindIndex(existing => existing.Cycle == cycle.Cycle)] = cycle with
            { Evidence = cycle.Evidence.Append(item).ToArray() };
        }
        if (!imageCleaned && primaryFailure is null)
            primaryFailure = new DemoFailureException("CLEANUP", "S22", "CV05_CLEANUP_FAILED");

        if (primaryFailure is null)
        {
            if (cycles.Count != 2 || cycles[0].FunctionalFingerprint != cycles[1].FunctionalFingerprint)
            {
                primaryFailure = new DemoFailureException("REPORT", "S21", "CV05_SCENARIO_FAILED");
            }
            else
            {
                var second = cycles[1];
                cycles[1] = second with
                {
                    Evidence = second.Evidence.Append(PhaseEvidence.Passed(
                        "REPORT", "S21", 0,
                        new Dictionary<string, int> { ["matchingFingerprints"] = 2 })).ToArray(),
                };
            }
        }

        watch.Stop();
        var functionalPassed = cycles.Count == 2 &&
            cycles.All(cycle => ScenarioCatalog.All.Where(item => item.Id is not ("S21" or "S22"))
                .All(scenario => cycle.Evidence.Count(item => item.Scenario == scenario.Id &&
                    item.State == "PASSED") == 1)) &&
            cycles[0].FunctionalFingerprint == cycles[1].FunctionalFingerprint;
        var finalState = primaryFailure is null && cleanupPassed ? "PASSED" : "FAILED";
        var report = new DemoReport(
            DemoContract.ReportSchema,
            DemoContract.ReportSchemaVersion,
            DemoContract.TaskId,
            DemoContract.CutId,
            commit,
            DemoContract.BaseCommit,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "WINDOWS" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "LINUX" : "OTHER",
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.Version.ToString(),
            DemoContract.PostgreSqlImage,
            DemoContract.SeaweedImage,
            DemoContract.ClamAvImage,
            image.ImageId ?? "UNAVAILABLE",
            DemoContract.SeedId,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(DemoContract.SeedId))),
            startedAt,
            DateTimeOffset.UtcNow,
            watch.ElapsedMilliseconds,
            cycles,
            ScenarioCatalog.All,
            "NO_APLICA",
            "NO_APLICA",
            functionalPassed ? "PASSED" : "FAILED",
            "PASSED",
            cleanupPassed ? "PASSED" : "FAILED",
            finalState);

        try
        {
            await EvidenceWriter.WriteAsync(repositoryRoot, report, CancellationToken.None);
        }
        catch
        {
            Console.Error.WriteLine("CV05_REPORT_FAILED");
            return primaryFailure?.Exit ?? 1;
        }

        if (primaryFailure is not null)
        {
            Console.Error.WriteLine($"{primaryFailure.Phase}:{primaryFailure.Scenario}:{primaryFailure.Code}:{primaryFailure.Exit}");
            return primaryFailure.Exit;
        }
        if (!cleanupPassed)
        {
            Console.Error.WriteLine("CLEANUP:S22:CV05_CLEANUP_FAILED:1");
            return 1;
        }
        Console.WriteLine("CV05_EXECUTION_PASSED");
        return 0;
    }

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
