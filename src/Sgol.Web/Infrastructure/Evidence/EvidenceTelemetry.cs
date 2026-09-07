using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sgol.Web.Infrastructure.Evidence;

internal static class EvidenceTelemetry
{
    internal static readonly ActivitySource Activities = new("Sgol.Evidence");
    internal static readonly Meter Meter = new("Sgol.Evidence");
    internal static readonly Counter<long> QuarantineEntered =
        Meter.CreateCounter<long>("sgol.evidence.quarantine.entered");
    internal static readonly Counter<long> QuarantineExited =
        Meter.CreateCounter<long>("sgol.evidence.quarantine.exited");
    internal static readonly Counter<long> Scans = Meter.CreateCounter<long>("sgol.evidence.scans");
    internal static readonly Counter<long> ScanFailures =
        Meter.CreateCounter<long>("sgol.evidence.scan_failures");
    internal static readonly Histogram<double> StorageDuration =
        Meter.CreateHistogram<double>("sgol.evidence.storage.duration", "ms");
    internal static readonly Histogram<double> ScanDuration =
        Meter.CreateHistogram<double>("sgol.evidence.scan.duration", "ms");
}
