using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Sgol.Cv02Demo;

internal static class Cv02DemoApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!DemoOptions.TryParse(args, out var options) || options is null)
        {
            Console.Error.WriteLine("Uso: Sgol.Cv02Demo --mode Automated|Interactive");
            return 64;
        }

        var repositoryRoot = FindRepositoryRoot();
        var state = new DemoState();
        var startedAt = DateTimeOffset.UtcNow;
        Cv02Database? database = null;
        WebApplication? application = null;
        var cleanupSucceeded = false;
        var exitCode = 1;
        var stage = "EVIDENCE";
        try
        {
            EvidenceWriter.Prepare(repositoryRoot);
            stage = "DATABASE";
            database = await Cv02Database.StartAsync(CancellationToken.None);
            Console.WriteLine("CV02_STAGE_DATABASE_READY");
            stage = "HOST";
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ApplicationName = typeof(Cv02DemoApplication).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.WebHost.UseStaticWebAssets();
            builder.WebHost.UseUrls($"http://{DemoContract.LoopbackAddress}:0");
            builder.Services.AddRazorPages();
            builder.Services.AddSingleton(database);
            builder.Services.AddSingleton(state);
            builder.Services.AddSingleton<Cv02ScenarioEngine>();
            builder.Services.AddSingleton<ScenarioCoordinator>();
            application = builder.Build();
            application.UseStaticFiles();
            application.MapStaticAssets();
            application.MapRazorPages();
            await application.StartAsync();
            var address = application.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault()
                ?? throw new DemoSafetyException("CV02_SCENARIO_FAILED");
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Host, DemoContract.LoopbackAddress, StringComparison.Ordinal))
            {
                throw new DemoSafetyException("CV02_DATABASE_NOT_LOOPBACK");
            }
            Console.WriteLine("CV02_STAGE_HOST_READY");

            if (options.Mode == DemoMode.Automated)
            {
                stage = "BROWSER";
                await PlaywrightDemoRunner.RunAsync(uri, repositoryRoot, state, CancellationToken.None);
                exitCode = state.Results.Count == ScenarioCatalog.All.Count &&
                    state.Results.All(item => item.Status == "PASSED") ? 0 : 1;
                if (exitCode == 0) state.MarkPassed();
                else state.MarkFailure("CV02_SCENARIO_FAILED");
            }
            else
            {
                Console.WriteLine($"TECH-E2E-CV-02 disponible en {uri}cv02/calendario");
                await application.WaitForShutdownAsync();
                exitCode = 0;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var code = exception is DemoSafetyException safety
                ? safety.ErrorCode
                : DemoSafety.StageFailure(stage);
            state.MarkFailure(code);
            Console.Error.WriteLine(code);
        }
        finally
        {
            try
            {
                if (application is not null)
                {
                    await application.StopAsync();
                    await application.DisposeAsync();
                }
                if (database is not null)
                {
                    await database.DisposeAsync();
                }
                cleanupSucceeded = true;
            }
            catch
            {
                Console.Error.WriteLine("CV02_CLEANUP_FAILED");
                state.MarkFailure("CV02_CLEANUP_FAILED");
                exitCode = 1;
            }

            try
            {
                await EvidenceWriter.WriteAsync(
                    repositoryRoot, state, startedAt, DateTimeOffset.UtcNow, cleanupSucceeded);
            }
            catch
            {
                state.MarkFailure("CV02_ARTIFACT_WRITE_FAILED");
                Console.Error.WriteLine("CV02_ARTIFACT_WRITE_FAILED");
                exitCode = 1;
            }
        }

        return exitCode;
    }

    internal static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SGOL.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new DemoSafetyException("CV02_SCENARIO_FAILED");
    }
}
