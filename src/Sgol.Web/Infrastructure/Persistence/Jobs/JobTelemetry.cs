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

    public static void RecordShutdown(double durationMilliseconds, string result) =>
        ShutdownDuration.Record(durationMilliseconds, new KeyValuePair<string, object?>("result", result));
}
