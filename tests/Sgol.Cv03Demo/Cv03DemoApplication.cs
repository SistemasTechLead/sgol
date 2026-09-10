namespace Sgol.Cv03Demo;

internal static class Cv03DemoApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!DemoOptions.TryParse(args, out _))
        {
            Console.Error.WriteLine("Uso: Sgol.Cv03Demo --mode Automated");
            return 64;
        }

        var root = FindRepositoryRoot();
        var state = new DemoState();
        var started = DateTimeOffset.UtcNow;
        Cv03Infrastructure? infrastructure = null;
        var cleanup = false;
        var exit = 1;
        try
        {
            EvidenceWriter.Prepare(root);
            infrastructure = await Cv03Infrastructure.StartAsync(CancellationToken.None);
            var engine = new Cv03ScenarioEngine(infrastructure);
            foreach (var scenario in ScenarioCatalog.All)
            {
                var began = DateTimeOffset.UtcNow;
                try
                {
                    var facts = await engine.RunAsync(scenario.Id, CancellationToken.None);
                    state.Add(DemoScenarioResult.Passed(scenario.Id, began, facts));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var code = exception switch
                    {
                        DemoSafetyException safety => safety.ErrorCode,
                        DemoScenarioAssertionException assertion => assertion.ErrorCode,
                        _ => "CV03_SCENARIO_FAILED",
                    };
                    state.Add(DemoScenarioResult.Failed(scenario.Id, began, code));
                    state.MarkFailure(code);
                    await EvidenceWriter.WriteFailureAsync(root, scenario.Id, code);
                    break;
                }
            }
            if (state.Results.Count == ScenarioCatalog.All.Count && state.Results.All(item => item.Status == "PASSED"))
            {
                state.MarkPassed();
                exit = 0;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            state.MarkFailure(exception is DemoSafetyException safety ? safety.ErrorCode : "CV03_UNEXPECTED_FAILURE");
        }
        finally
        {
            try
            {
                if (infrastructure is not null) await infrastructure.DisposeAsync();
                cleanup = true;
            }
            catch
            {
                state.MarkFailure("CV03_CLEANUP_FAILED");
                exit = 1;
            }
            try
            {
                await EvidenceWriter.WriteAsync(root, state, started, DateTimeOffset.UtcNow, cleanup);
            }
            catch
            {
                Console.Error.WriteLine("CV03_ARTIFACT_WRITE_FAILED");
                exit = 1;
            }
        }
        return exit;
    }

    internal static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "SGOL.slnx"))) return current.FullName;
        throw new DemoSafetyException("CV03_UNEXPECTED_FAILURE");
    }
}
