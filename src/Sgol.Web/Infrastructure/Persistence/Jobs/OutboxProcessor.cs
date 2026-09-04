using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.JobInfrastructure;

public enum OutboxProcessResult
{
    NoWork,
    Processed,
    RetryScheduled,
    Exhausted
}

public sealed class OutboxProcessor(
    SgolDbContext dbContext,
    OutboxHandlerRegistry registry,
    IClock clock,
    ILogger<OutboxProcessor> logger)
{
    private static readonly TimeSpan[] InfrastructureRetryDelays =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500)];

    public async Task<OutboxProcessResult> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ProcessAttemptAsync(cancellationToken);
            }
            catch (Exception exception) when (IsRetryableInfrastructureFailure(exception) &&
                                               attempt < InfrastructureRetryDelays.Length)
            {
                dbContext.ChangeTracker.Clear();
                JobLogs.PostgresConcurrencyRetry(logger, attempt + 1, string.Empty, "RETRY", Guid.Empty);
                await Task.Delay(InfrastructureRetryDelays[attempt], cancellationToken);
            }
        }
    }

    private async Task<OutboxProcessResult> ProcessAttemptAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var now = clock.UtcNow;
        var outboxEvent = await dbContext.OutboxEvents
            .FromSqlInterpolated($$"""
                SELECT *
                FROM outbox_event
                WHERE processed_at IS NULL
                  AND attempt_count < {{JobContract.MaximumAttempts}}
                  AND available_at <= {{now}}
                ORDER BY available_at, created_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (outboxEvent is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return OutboxProcessResult.NoWork;
        }

        JobLogs.OutboxClaimed(
            logger,
            outboxEvent.Id,
            outboxEvent.EventType,
            outboxEvent.AttemptCount + 1,
            "CLAIMED");

        await transaction.CreateSavepointAsync("before_handler", cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.Empty;
        JsonDocument? envelope = null;
        Activity? activity = null;

        try
        {
            outboxEvent.AttemptCount++;
            JobTelemetry.OutboxAttempts.Add(1, Tags(outboxEvent.EventType, outboxEvent.AttemptCount, "STARTED"));
            envelope = ParseEnvelope(outboxEvent.Payload);
            correlationId = ReadCorrelationId(envelope.RootElement);
            activity = JobTelemetry.Activities.StartActivity("outbox.deliver", ActivityKind.Internal);
            activity?.SetTag("service", "Sgol.Worker");
            activity?.SetTag("correlationId", correlationId);
            activity?.SetTag("eventType", outboxEvent.EventType);
            activity?.SetTag("attempt", outboxEvent.AttemptCount);
            var errorCode = ResolveHandler(outboxEvent.EventType, envelope.RootElement, out var handler, out var data);
            if (errorCode is not null || handler is null)
            {
                throw new JobExecutionException(errorCode ?? "UNREGISTERED_EVENT_TYPE");
            }

            await handler.HandleAsync(
                new OutboxDeliveryContext(
                    outboxEvent.Id,
                    outboxEvent.EventType,
                    outboxEvent.AggregateId,
                    correlationId,
                    data,
                    dbContext),
                cancellationToken);
            outboxEvent.ProcessedAt = now;
            outboxEvent.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            stopwatch.Stop();
            JobTelemetry.OutboxProcessed.Add(1, Tags(outboxEvent.EventType, outboxEvent.AttemptCount, "SUCCEEDED"));
            JobTelemetry.OutboxDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                Tags(outboxEvent.EventType, outboxEvent.AttemptCount, "SUCCEEDED"));
            JobLogs.OutboxProcessed(
                logger,
                outboxEvent.Id,
                outboxEvent.EventType,
                outboxEvent.AttemptCount,
                stopwatch.Elapsed.TotalMilliseconds,
                "SUCCEEDED",
                correlationId);
            return OutboxProcessResult.Processed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception) when (!IsRetryableInfrastructureFailure(exception))
        {
            await transaction.RollbackToSavepointAsync("before_handler", cancellationToken);
            dbContext.ChangeTracker.Clear();
            var lockedEvent = await dbContext.OutboxEvents.SingleAsync(
                candidate => candidate.Id == outboxEvent.Id,
                cancellationToken);
            lockedEvent.AttemptCount++;
            lockedEvent.LastError = Sanitize(exception);
            var exhausted = lockedEvent.AttemptCount >= JobContract.MaximumAttempts;
            lockedEvent.AvailableAt = exhausted ? now : now + BackoffAfter(lockedEvent.AttemptCount);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            stopwatch.Stop();
            var result = exhausted ? "EXHAUSTED" : "RETRY_SCHEDULED";
            JobTelemetry.OutboxFailures.Add(1, Tags(lockedEvent.EventType, lockedEvent.AttemptCount, result));
            JobTelemetry.OutboxDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                Tags(lockedEvent.EventType, lockedEvent.AttemptCount, result));

            if (exhausted)
            {
                JobTelemetry.OutboxExhausted.Add(1, Tags(lockedEvent.EventType, lockedEvent.AttemptCount, result));
                JobLogs.OutboxAttemptsExhausted(
                    logger,
                    lockedEvent.Id,
                    lockedEvent.EventType,
                    lockedEvent.AttemptCount,
                    stopwatch.Elapsed.TotalMilliseconds,
                    result,
                    lockedEvent.LastError!,
                    correlationId);
                return OutboxProcessResult.Exhausted;
            }

            JobLogs.OutboxRetryScheduled(
                logger,
                lockedEvent.Id,
                lockedEvent.EventType,
                lockedEvent.AttemptCount,
                stopwatch.Elapsed.TotalMilliseconds,
                result,
                lockedEvent.LastError!,
                correlationId);
            return OutboxProcessResult.RetryScheduled;
        }
        finally
        {
            activity?.Dispose();
            envelope?.Dispose();
        }
    }

    private string? ResolveHandler(
        string eventType,
        JsonElement envelope,
        out IOutboxHandler? handler,
        out JsonElement data)
    {
        data = envelope.TryGetProperty("data", out var value) ? value : default;
        if (!registry.TryGet(eventType, out handler) || handler is null)
        {
            return "UNREGISTERED_EVENT_TYPE";
        }

        return data.ValueKind != JsonValueKind.Object || !handler.IsPayloadValid(data)
            ? "INVALID_EVENT_PAYLOAD"
            : null;
    }

    private static JsonDocument ParseEnvelope(string payload)
    {
        try
        {
            return JsonDocument.Parse(
                payload,
                new JsonDocumentOptions { MaxDepth = JobContract.MaximumJsonDepth });
        }
        catch (JsonException)
        {
            throw new JobExecutionException("INVALID_EVENT_PAYLOAD");
        }
    }

    private static Guid ReadCorrelationId(JsonElement envelope) =>
        envelope.TryGetProperty("correlationId", out var value) &&
        value.ValueKind == JsonValueKind.String &&
        Guid.TryParseExact(value.GetString(), "D", out var correlationId) &&
        correlationId != Guid.Empty
            ? correlationId
            : throw new JobExecutionException("INVALID_EVENT_PAYLOAD");

    private static string Sanitize(Exception exception) => exception switch
    {
        JobExecutionException known => known.ErrorCode,
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => "POSTGRES_UNIQUE_VIOLATION",
        _ => "UNEXPECTED_HANDLER_FAILURE"
    };

    private static TimeSpan BackoffAfter(int attemptCount) => attemptCount switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromMinutes(60),
        _ => TimeSpan.Zero
    };

    internal static bool IsRetryableInfrastructureFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState is PostgresErrorCodes.DeadlockDetected or
                    PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.LockNotAvailable)
            {
                return true;
            }

            if (current is NpgsqlException and not PostgresException)
            {
                return true;
            }
        }

        return false;
    }

    private static TagList Tags(string eventType, int attempt, string result) => new()
    {
        { "eventType", eventType },
        { "attempt", attempt },
        { "result", result }
    };
}
