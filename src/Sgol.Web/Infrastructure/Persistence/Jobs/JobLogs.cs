using Microsoft.Extensions.Logging;

namespace Sgol.JobInfrastructure;

public static partial class JobLogs
{
    [LoggerMessage(2100, LogLevel.Information, "Worker mode {mode} started", EventName = "WorkerStarted")]
    public static partial void WorkerStarted(ILogger logger, string mode);

    [LoggerMessage(2101, LogLevel.Information, "Worker stopped duration {durationMs} result {result}", EventName = "WorkerStopped")]
    public static partial void WorkerStopped(ILogger logger, double durationMs, string result);

    [LoggerMessage(2110, LogLevel.Debug, "Outbox event {eventId} type {eventType} attempt {attempt} result {result}", EventName = "OutboxClaimed")]
    public static partial void OutboxClaimed(ILogger logger, Guid eventId, string eventType, int attempt, string result);

    [LoggerMessage(2111, LogLevel.Information, "Outbox event {eventId} type {eventType} attempt {attempt} duration {durationMs} result {result} correlation {correlationId}", EventName = "OutboxProcessed")]
    public static partial void OutboxProcessed(ILogger logger, Guid eventId, string eventType, int attempt, double durationMs, string result, Guid correlationId);

    [LoggerMessage(2112, LogLevel.Warning, "Outbox event {eventId} type {eventType} attempt {attempt} duration {durationMs} result {result} error {errorCode} correlation {correlationId}", EventName = "OutboxRetryScheduled")]
    public static partial void OutboxRetryScheduled(ILogger logger, Guid eventId, string eventType, int attempt, double durationMs, string result, string errorCode, Guid correlationId);

    [LoggerMessage(2113, LogLevel.Error, "Outbox event {eventId} type {eventType} attempt {attempt} duration {durationMs} result {result} error {errorCode} correlation {correlationId}", EventName = "OutboxAttemptsExhausted")]
    public static partial void OutboxAttemptsExhausted(ILogger logger, Guid eventId, string eventType, int attempt, double durationMs, string result, string errorCode, Guid correlationId);

    [LoggerMessage(2120, LogLevel.Information, "Scheduled job {jobName} run {jobRunId} scheduled {scheduledFor} result {result} correlation {correlationId}", EventName = "ScheduledJobStarted")]
    public static partial void ScheduledJobStarted(ILogger logger, string jobName, Guid jobRunId, DateTimeOffset scheduledFor, string result, Guid correlationId);

    [LoggerMessage(2121, LogLevel.Information, "Scheduled job {jobName} result {result} correlation {correlationId}", EventName = "ScheduledJobLockBusy")]
    public static partial void ScheduledJobLockBusy(ILogger logger, string jobName, string result, Guid correlationId);

    [LoggerMessage(2122, LogLevel.Information, "Scheduled job {jobName} run {jobRunId} duration {durationMs} result {result} correlation {correlationId}", EventName = "ScheduledJobCompleted")]
    public static partial void ScheduledJobCompleted(ILogger logger, string jobName, Guid jobRunId, double durationMs, string result, Guid correlationId);

    [LoggerMessage(2123, LogLevel.Error, "Scheduled job {jobName} run {jobRunId} duration {durationMs} result {result} error {errorCode} correlation {correlationId}", EventName = "ScheduledJobFailed")]
    public static partial void ScheduledJobFailed(ILogger logger, string jobName, Guid jobRunId, double durationMs, string result, string errorCode, Guid correlationId);

    [LoggerMessage(2124, LogLevel.Warning, "PostgreSQL concurrency retry {attempt} job {jobName} result {result} correlation {correlationId}", EventName = "PostgresConcurrencyRetry")]
    public static partial void PostgresConcurrencyRetry(ILogger logger, int attempt, string jobName, string result, Guid correlationId);

    [LoggerMessage(2130, LogLevel.Information, "Worker shutdown result {result}", EventName = "WorkerShutdownRequested")]
    public static partial void WorkerShutdownRequested(ILogger logger, string result);

    [LoggerMessage(2131, LogLevel.Error, "Worker shutdown duration {durationMs} result {result}", EventName = "WorkerShutdownGraceExceeded")]
    public static partial void WorkerShutdownGraceExceeded(ILogger logger, double durationMs, string result);
}
