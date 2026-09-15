using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.JobInfrastructure;

public enum ScheduledJobResult
{
    Completed,
    AlreadyCompleted,
    LockBusy,
    Failed
}

public sealed class ScheduledJobRunner(
    SgolDbContext dbContext,
    ScheduledJobRegistry registry,
    IClock clock,
    IUuidGenerator uuidGenerator,
    JobDatabaseOptions databaseOptions,
    ILogger<ScheduledJobRunner> logger)
{
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500)];

    public Task<ScheduledJobResult> RunAsync(
        string jobName,
        DateTimeOffset scheduledFor,
        Guid correlationId,
        CancellationToken cancellationToken = default) =>
        RunAsync(jobName, scheduledFor, correlationId, null, cancellationToken);

    public async Task<ScheduledJobResult> RunAsync(
        string jobName,
        DateTimeOffset scheduledFor,
        Guid correlationId,
        string? checkpoint,
        CancellationToken cancellationToken = default)
    {
        if (!registry.TryGet(jobName, out var job) || job is null)
        {
            throw new ArgumentException("Scheduled job is not registered.", nameof(jobName));
        }

        await using var lockConnection = new NpgsqlConnection(databaseOptions.ConnectionString);
        await lockConnection.OpenAsync(cancellationToken);
        var lockKey = job.PreventOverlappingSlots
            ? ComputeLockKey(jobName)
            : ComputeLockKey(jobName, scheduledFor);
        if (!await TryAcquireLockAsync(lockConnection, lockKey, cancellationToken))
        {
            JobLogs.ScheduledJobLockBusy(logger, jobName, "SKIPPED_LOCKED", correlationId);
            return ScheduledJobResult.LockBusy;
        }

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await RunAttemptAsync(job, scheduledFor, correlationId, attempt + 1, checkpoint,
                        cancellationToken);
                }
                catch (Exception exception) when (IsUniqueViolation(exception) && attempt == 0)
                {
                    dbContext.ChangeTracker.Clear();
                    await Task.Delay(RetryDelays[0], cancellationToken);
                }
                catch (Exception exception) when (IsUniqueViolation(exception))
                {
                    dbContext.ChangeTracker.Clear();
                    JobLogs.ScheduledJobFailed(
                        logger,
                        jobName,
                        Guid.Empty,
                        0,
                        "FAILED",
                        "POSTGRES_UNIQUE_VIOLATION",
                        correlationId);
                    return ScheduledJobResult.Failed;
                }
                catch (Exception exception) when (
                    OutboxProcessor.IsRetryableInfrastructureFailure(exception) && attempt < RetryDelays.Length)
                {
                    dbContext.ChangeTracker.Clear();
                    JobLogs.PostgresConcurrencyRetry(
                        logger,
                        attempt + 1,
                        jobName,
                        "RETRY",
                        correlationId);
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                }
                catch (Exception exception) when (OutboxProcessor.IsRetryableInfrastructureFailure(exception))
                {
                    dbContext.ChangeTracker.Clear();
                    await PersistFailedRunAsync(
                        job.Name,
                        scheduledFor,
                        job.ConcurrencyExhaustedErrorCode,
                        cancellationToken);
                    JobLogs.ScheduledJobFailed(
                        logger,
                        jobName,
                        Guid.Empty,
                        0,
                        "FAILED",
                        job.ConcurrencyExhaustedErrorCode,
                        correlationId);
                    return ScheduledJobResult.Failed;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    dbContext.ChangeTracker.Clear();
                    JobLogs.ScheduledJobFailed(
                        logger,
                        jobName,
                        Guid.Empty,
                        0,
                        "FAILED",
                        "JOB_INFRASTRUCTURE_FAILURE",
                        correlationId);
                    return ScheduledJobResult.Failed;
                }
            }
        }
        finally
        {
            try
            {
                await ReleaseLockAsync(lockConnection, lockKey);
            }
            catch (NpgsqlException)
            {
                JobLogs.ScheduledJobFailed(
                    logger,
                    jobName,
                    Guid.Empty,
                    0,
                    "FAILED",
                    "POSTGRES_LOCK_RELEASE_FAILURE",
                    correlationId);
            }
        }
    }

    private async Task PersistFailedRunAsync(
        string jobName,
        DateTimeOffset scheduledFor,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var run = await dbContext.ScheduledJobRuns.SingleOrDefaultAsync(
            candidate => candidate.JobName == jobName && candidate.ScheduledFor == scheduledFor,
            cancellationToken);
        var failedAt = clock.UtcNow;
        if (run is null)
        {
            run = new ScheduledJobRun
            {
                Id = uuidGenerator.NewUuid(),
                JobName = jobName,
                ScheduledFor = scheduledFor,
                StartedAt = failedAt,
            };
            dbContext.ScheduledJobRuns.Add(run);
        }

        run.EndedAt = failedAt;
        run.Status = ScheduledJobStatuses.Failed;
        run.Error = errorCode;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task<ScheduledJobResult> RunAttemptAsync(
        IScheduledJob job,
        DateTimeOffset scheduledFor,
        Guid correlationId,
        int attempt,
        string? checkpoint,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
        var run = await dbContext.ScheduledJobRuns
            .FromSqlInterpolated($$"""
                SELECT * FROM scheduled_job_run
                WHERE job_name = {{job.Name}} AND scheduled_for = {{scheduledFor}}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (run?.Status == ScheduledJobStatuses.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return ScheduledJobResult.AlreadyCompleted;
        }

        var startedAt = clock.UtcNow;
        if (run is null)
        {
            run = new ScheduledJobRun
            {
                Id = uuidGenerator.NewUuid(),
                JobName = job.Name,
                ScheduledFor = scheduledFor,
                StartedAt = startedAt,
                Status = ScheduledJobStatuses.Running,
                Checkpoint = checkpoint
            };
            dbContext.ScheduledJobRuns.Add(run);
        }
        else
        {
            if (!string.Equals(run.Checkpoint, checkpoint, StringComparison.Ordinal))
                throw new JobExecutionException("JOB_CHECKPOINT_CONFLICT");
            run.StartedAt = startedAt;
            run.EndedAt = null;
            run.Status = ScheduledJobStatuses.Running;
            run.Error = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CreateSavepointAsync("before_job", cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        using var activity = JobTelemetry.Activities.StartActivity("job.run", ActivityKind.Internal);
        activity?.SetTag("service", "Sgol.Worker");
        activity?.SetTag("correlationId", correlationId);
        activity?.SetTag("jobName", job.Name);
        JobLogs.ScheduledJobStarted(
            logger,
            job.Name,
            run.Id,
            scheduledFor,
            "RUNNING",
            correlationId);

        try
        {
            await job.ExecuteAsync(
                new ScheduledJobContext(run.Id, scheduledFor, run.Checkpoint, dbContext, correlationId, attempt),
                cancellationToken);
            run.Status = ScheduledJobStatuses.Succeeded;
            run.EndedAt = clock.UtcNow;
            run.Error = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            stopwatch.Stop();
            RecordJobMetric(job.Name, "SUCCEEDED", stopwatch.Elapsed.TotalMilliseconds);
            JobLogs.ScheduledJobCompleted(
                logger,
                job.Name,
                run.Id,
                stopwatch.Elapsed.TotalMilliseconds,
                "SUCCEEDED",
                correlationId);
            return ScheduledJobResult.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception) when (!OutboxProcessor.IsRetryableInfrastructureFailure(exception))
        {
            await transaction.RollbackToSavepointAsync("before_job", cancellationToken);
            dbContext.ChangeTracker.Clear();
            var lockedRun = await dbContext.ScheduledJobRuns.SingleAsync(
                candidate => candidate.Id == run.Id,
                cancellationToken);
            lockedRun.Status = ScheduledJobStatuses.Failed;
            lockedRun.EndedAt = clock.UtcNow;
            lockedRun.Error = Sanitize(exception);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            stopwatch.Stop();
            RecordJobMetric(job.Name, "FAILED", stopwatch.Elapsed.TotalMilliseconds);
            JobLogs.ScheduledJobFailed(
                logger,
                job.Name,
                lockedRun.Id,
                stopwatch.Elapsed.TotalMilliseconds,
                "FAILED",
                lockedRun.Error!,
                correlationId);
            return ScheduledJobResult.Failed;
        }
    }

    private static async Task<bool> TryAcquireLockAsync(
        NpgsqlConnection connection,
        int lockKey,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(@namespace_key, @run_key)",
            connection)
        {
            CommandTimeout = 15
        };
        command.Parameters.AddWithValue("namespace_key", 0x53474F4C);
        command.Parameters.AddWithValue("run_key", lockKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, int lockKey)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_unlock(@namespace_key, @run_key)",
            connection)
        {
            CommandTimeout = 15
        };
        command.Parameters.AddWithValue("namespace_key", 0x53474F4C);
        command.Parameters.AddWithValue("run_key", lockKey);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    internal static int ComputeLockKey(string jobName, DateTimeOffset scheduledFor)
    {
        var canonical = string.Concat(
            jobName,
            "\n",
            scheduledFor.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(digest);
    }

    internal static int ComputeLockKey(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        return ComputeLockKey(jobName, DateTimeOffset.UnixEpoch);
    }

    private static bool IsUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }

    private static string Sanitize(Exception exception) => exception is JobExecutionException known
        ? known.ErrorCode
        : "UNEXPECTED_JOB_FAILURE";

    private static void RecordJobMetric(string jobName, string result, double duration)
    {
        var tags = new TagList { { "jobName", jobName }, { "result", result } };
        JobTelemetry.JobRuns.Add(1, tags);
        JobTelemetry.JobDuration.Record(duration, tags);
    }
}

public sealed class JobDatabaseOptions(string connectionString)
{
    internal string ConnectionString { get; } = connectionString;

    public override string ToString() => nameof(JobDatabaseOptions);
}
