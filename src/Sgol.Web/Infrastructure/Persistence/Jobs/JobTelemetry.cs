using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sgol.JobInfrastructure;

public static class JobTelemetry
{
    internal static readonly ActivitySource Activities = new("Sgol.Jobs");
    internal static readonly Meter Meter = new("Sgol.Jobs");
    internal static readonly Counter<long> OutboxAttempts = Meter.CreateCounter<long>("sgol.worker.outbox.attempts");
    internal static readonly Counter<long> OutboxProcessed = Meter.CreateCounter<long>("sgol.worker.outbox.processed");
    internal static readonly Counter<long> OutboxFailures = Meter.CreateCounter<long>("sgol.worker.outbox.failures");
    internal static readonly Counter<long> OutboxExhausted = Meter.CreateCounter<long>("sgol.worker.outbox.exhausted");
    internal static readonly Counter<long> JobRuns = Meter.CreateCounter<long>("sgol.worker.jobs.runs");
    internal static readonly Histogram<double> OutboxDuration = Meter.CreateHistogram<double>("sgol.worker.outbox.duration", "ms");
    internal static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("sgol.worker.jobs.duration", "ms");
    internal static readonly Histogram<double> ShutdownDuration = Meter.CreateHistogram<double>("sgol.worker.shutdown.duration", "ms");
    internal static readonly Counter<long> RecurringOccurrences = Meter.CreateCounter<long>("sgol.worker.recurrence.occurrences");
    internal static readonly Histogram<double> RecurringDuration = Meter.CreateHistogram<double>("sgol.worker.recurrence.duration", "ms");
    internal static readonly Counter<long> RecurringDelayed = Meter.CreateCounter<long>("sgol.worker.recurrence.delayed");
    internal static readonly Counter<long> OriginSourceUnavailable = Meter.CreateCounter<long>("sgol.worker.recurrence.origin_source_unavailable");

    public static void RecordShutdown(double durationMilliseconds, string result) =>
        ShutdownDuration.Record(durationMilliseconds, new KeyValuePair<string, object?>("result", result));

    public static void RecordRecurringOccurrence(
        string jobName,
        string taskCode,
        string window,
        string result,
        double durationMilliseconds)
    {
        var tags = new TagList
        {
            { "jobName", jobName },
            { "taskCode", taskCode },
            { "window", window },
            { "attempt", 1 },
            { "result", result },
        };
        RecurringOccurrences.Add(1, tags);
        RecurringDuration.Record(durationMilliseconds, tags);
    }

    public static void RecordRecurringDelayed(string jobName, string taskCode, string window) =>
        RecurringDelayed.Add(1, new TagList
        {
            { "jobName", jobName },
            { "taskCode", taskCode },
            { "window", window },
            { "result", "DELAYED" },
        });

    public static void RecordOriginSourceUnavailable(string jobName, string taskCode) =>
        OriginSourceUnavailable.Add(1, new TagList
        {
            { "jobName", jobName },
            { "taskCode", taskCode },
            { "result", "ORIGIN_SOURCE_NOT_IMPLEMENTED" },
        });
}
