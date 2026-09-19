using System.Diagnostics;
using System.Diagnostics.Metrics;
using Sgol.Continuity.Contracts;

namespace Sgol.Operations;

internal static class FunctionalRecoveryTelemetry
{
    private static readonly Meter Meter = new("Sgol.Continuity", "1.0.0");
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("recovery_reconciliation_runs_total");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("recovery_reconciliation_duration_seconds");
    private static readonly Histogram<long> Differences =
        Meter.CreateHistogram<long>("recovery_reconciliation_differences");
    private static readonly Histogram<long> Rpo = Meter.CreateHistogram<long>("recovery_reconciliation_rpo_seconds");
    private static readonly Histogram<long> Rto = Meter.CreateHistogram<long>("recovery_reconciliation_rto_seconds");
    private static readonly Histogram<long> ReferenceAge =
        Meter.CreateHistogram<long>("recovery_reference_age_seconds");

    public static void Record(FunctionalReconciliationResult result, RecoveryObjectives objectives,
        DateTimeOffset targetRecoveryAt, DateTimeOffset completedAt, double durationSeconds)
    {
        var tags = new TagList
        {
            { "operation", "functional-recovery" },
            { "stage", "reconciliation" },
            { "result", result.Status },
            { "errorClass", result.Status == RecoveryReconciliationStatuses.Matched ? "none" : "functional" }
        };
        Runs.Add(1, tags);
        Duration.Record(durationSeconds, tags);
        Differences.Record(result.TotalDifferences, tags);
        Rpo.Record(objectives.ObservedRpoSeconds, tags);
        Rto.Record(objectives.ObservedRtoSeconds, tags);
        ReferenceAge.Record(Math.Max(0, (long)(completedAt - targetRecoveryAt).TotalSeconds), tags);
    }
}
