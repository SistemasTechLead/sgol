using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.JobInfrastructure;

namespace Sgol.Worker;

public static class WorkerApplication
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(30);

    public static async Task<int> RunAsync(
        string[] args,
        Action<IServiceCollection>? configureServices = null,
        Action<string>? writeOutput = null,
        IConfiguration? configuration = null,
        Action<IServiceCollection, IConfiguration, IHostEnvironment>? configureHostServices = null,
        CancellationToken cancellationToken = default)
    {
        writeOutput ??= Console.WriteLine;
        if (!WorkerCommandParser.TryParse(args, out var command) || command is null)
        {
            writeOutput(WorkerCommandParser.HelpText);
            return 64;
        }

        if (command.Kind == WorkerCommandKind.Help)
        {
            writeOutput(WorkerCommandParser.HelpText);
            return 0;
        }

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [] });
        if (configuration is not null)
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddConfiguration(configuration);
        }
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "O";
            options.UseUtcTimestamp = true;
        });

        if (!JobServiceCollectionExtensions.HasValidConnectionString(builder.Configuration))
        {
            writeOutput("PostgreSQL configuration is required and must be valid.");
            return 69;
        }

        builder.Services.AddSgolJobInfrastructure(builder.Configuration);

        configureServices?.Invoke(builder.Services);
        configureHostServices?.Invoke(builder.Services, builder.Configuration, builder.Environment);
        using var host = builder.Build();
        await host.StartAsync(CancellationToken.None);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sgol.Worker");
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["service"] = "Sgol.Worker"
        });
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetime.ApplicationStopping);
        JobLogs.WorkerStarted(logger, command.Kind.ToString());

        try
        {
            return command.Kind switch
            {
                WorkerCommandKind.Outbox => await RunOutboxAsync(host.Services, stopping.Token),
                WorkerCommandKind.RunJob => await RunScheduledJobAsync(host.Services, command, stopping.Token),
                _ => 64
            };
        }
        finally
        {
            var stopwatch = Stopwatch.StartNew();
            using var stopTimeout = new CancellationTokenSource(ShutdownGrace);
            try
            {
                await host.StopAsync(stopTimeout.Token);
                JobTelemetry.RecordShutdown(stopwatch.Elapsed.TotalMilliseconds, "STOPPED");
                JobLogs.WorkerStopped(logger, stopwatch.Elapsed.TotalMilliseconds, "STOPPED");
            }
            catch (OperationCanceledException)
            {
                JobTelemetry.RecordShutdown(stopwatch.Elapsed.TotalMilliseconds, "CANCELLED_TIMEOUT");
                JobLogs.WorkerShutdownGraceExceeded(
                    logger,
                    stopwatch.Elapsed.TotalMilliseconds,
                    "CANCELLED_TIMEOUT");
            }
        }
    }

    private static async Task<int> RunOutboxAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var loop = services.GetRequiredService<IOutboxLoop>();
        try
        {
            await loop.ProbeDatabaseAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            return 69;
        }

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Sgol.Worker");
        return await AwaitControlledAsync(loop.RunAsync(cancellationToken), logger, cancellationToken) ?? 0;
    }

    private static async Task<int> RunScheduledJobAsync(
        IServiceProvider services,
        WorkerCommand command,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<ScheduledJobRegistry>();
        if (!registry.TryGet(command.JobName!, out _))
        {
            return 64;
        }

        try
        {
            await services.GetRequiredService<IOutboxLoop>().ProbeDatabaseAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            return 69;
        }

        var runner = scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>();
        var generator = scope.ServiceProvider.GetRequiredService<IUuidGenerator>();
        try
        {
            var runTask = runner.RunAsync(
                command.JobName!,
                command.ScheduledFor!.Value,
                generator.NewUuid(),
                cancellationToken);
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Sgol.Worker");
            var controlled = await AwaitControlledAsync(runTask, logger, cancellationToken);
            if (controlled is not null)
            {
                return controlled.Value;
            }

            var result = await runTask;
            return result == ScheduledJobResult.Failed ? 1 : 0;
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return 69;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private static async Task<int?> AwaitControlledAsync(
        Task work,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var shutdownLogged = false;
        try
        {
            var cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (await Task.WhenAny(work, cancellation) == work)
            {
                await work;
                return null;
            }

            JobLogs.WorkerShutdownRequested(logger, "CANCELLATION_REQUESTED");
            shutdownLogged = true;
            try
            {
                await work.WaitAsync(ShutdownGrace, CancellationToken.None);
                return 0;
            }
            catch (TimeoutException)
            {
                return 130;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!shutdownLogged)
            {
                JobLogs.WorkerShutdownRequested(logger, "CANCELLATION_REQUESTED");
            }

            return 0;
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or DbUpdateException)
        {
            return 1;
        }
    }
}
