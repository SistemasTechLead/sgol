using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Generation;

namespace Sgol.JobInfrastructure;

public sealed class RecurringGenerationJob(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    TimeZoneInfo operationalTimeZone,
    ILogger<RecurringGenerationJob> logger) : IScheduledJob
{
    public string Name => RecurringGenerationContract.JobName;

    public string ConcurrencyExhaustedErrorCode => "RECURRENCE_POSTGRES_CONCURRENCY_EXHAUSTED";

    public async Task ExecuteAsync(
        ScheduledJobContext context,
        CancellationToken cancellationToken)
    {
        var observedNow = clock.UtcNow;
        if (context.ScheduledFor > observedNow)
        {
            throw new JobExecutionException("RECURRENCE_SCHEDULED_FOR_FUTURE");
        }

        var previous = await context.DbContext.ScheduledJobRuns.AsNoTracking()
            .Where(run =>
                run.JobName == Name &&
                run.Status == ScheduledJobStatuses.Succeeded &&
                run.ScheduledFor < context.ScheduledFor)
            .OrderByDescending(run => run.ScheduledFor)
            .Select(run => (DateTimeOffset?)run.ScheduledFor)
            .FirstOrDefaultAsync(cancellationToken);
        var fromExclusive = previous ??
            RecurringGenerationContract.StartOfLocalDay(context.ScheduledFor, operationalTimeZone);
        if (previous is not null &&
            context.ScheduledFor - previous.Value > RecurringGenerationContract.RecoveryHorizon)
        {
            throw new JobExecutionException("RECURRENCE_RECOVERY_HORIZON_EXCEEDED");
        }

        await RecordUnavailableOriginSourceAsync(context, cancellationToken);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RecurringGenerationContract.Generated] = 0,
            [RecurringGenerationContract.Recovered] = 0,
            [RecurringGenerationContract.Omitted] = 0,
            [RecurringGenerationContract.Rejected] = 0,
        };
        foreach (var window in RecurringGenerationContract.DueWindows(
                     fromExclusive,
                     context.ScheduledFor,
                     operationalTimeZone))
        {
            var stopwatch = Stopwatch.StartNew();
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>();
            var result = await processor.ProcessAsync(window, context.CorrelationId, cancellationToken);
            if (result is null)
            {
                continue;
            }

            counts[result.Result]++;
            stopwatch.Stop();
            RecordResult(
                window,
                result,
                context.Attempt,
                stopwatch.Elapsed.TotalMilliseconds,
                context.CorrelationId);
            if (result.Result == RecurringGenerationContract.Generated &&
                observedNow - window.OccurrenceInstant > RecurringGenerationContract.DelayedThreshold)
            {
                JobTelemetry.RecordRecurringDelayed(Name, RecurringGenerationContract.TaskCode, Window(window));
                JobLogs.RecurringOccurrenceDelayed(
                    logger,
                    Name,
                    RecurringGenerationContract.TaskCode,
                    Window(window),
                    "DELAYED",
                    context.CorrelationId);
            }
        }

        var run = context.DbContext.ScheduledJobRuns.Local.Single(item => item.Id == context.RunId);
        run.Checkpoint = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            kind = RecurringGenerationContract.CheckpointKind,
            fromExclusive = fromExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            throughInclusive = context.ScheduledFor.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            generated = counts[RecurringGenerationContract.Generated],
            recovered = counts[RecurringGenerationContract.Recovered],
            omitted = counts[RecurringGenerationContract.Omitted],
            rejected = counts[RecurringGenerationContract.Rejected],
        });
    }

    private async Task RecordUnavailableOriginSourceAsync(
        ScheduledJobContext context,
        CancellationToken cancellationToken)
    {
        var taskId = TaskDefinitionCatalog.Require("TAR-0026").Id;
        var exists = await context.DbContext.ActivationRuleVersions.AsNoTracking().AnyAsync(
            rule =>
                rule.TaskDefinitionId == taskId &&
                rule.Mode == ActivationModes.Recurring &&
                rule.EffectiveFrom != null &&
                rule.EffectiveFrom <= context.ScheduledFor &&
                (rule.EffectiveTo == null || context.ScheduledFor < rule.EffectiveTo),
            cancellationToken);
        if (exists)
        {
            JobTelemetry.RecordOriginSourceUnavailable(Name, "TAR-0026");
        }
    }

    private void RecordResult(
        RecurringWindow window,
        RecurringOccurrenceResult result,
        int attempt,
        double durationMilliseconds,
        Guid correlationId)
    {
        var localWindow = Window(window);
        JobTelemetry.RecordRecurringOccurrence(
            Name,
            RecurringGenerationContract.TaskCode,
            localWindow,
            result.Result,
            durationMilliseconds);
        JobLogs.RecurringOccurrenceCompleted(
            logger,
            Name,
            RecurringGenerationContract.TaskCode,
            localWindow,
                attempt,
            durationMilliseconds,
            result.Result,
            result.ErrorCode,
            correlationId);
    }

    private static string Window(RecurringWindow window) =>
        window.Window.ToString("HH:mm", CultureInfo.InvariantCulture);
}
