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
    internal static readonly Counter<long> UploadIntents = Meter.CreateCounter<long>("sgol.evidence.upload_intents");
    internal static readonly Counter<long> UploadCompletions = Meter.CreateCounter<long>("sgol.evidence.upload_completions");
    internal static readonly Counter<long> Promotions = Meter.CreateCounter<long>("sgol.evidence.promotions");
    internal static readonly Counter<long> Links = Meter.CreateCounter<long>("sgol.evidence.links");
    internal static readonly Counter<long> Replacements = Meter.CreateCounter<long>("sgol.evidence.replacements");
    internal static readonly Counter<long> Rejections = Meter.CreateCounter<long>("sgol.evidence.rejections");
    internal static readonly Counter<long> Reviews = Meter.CreateCounter<long>("sgol.evidence.reviews");
    internal static readonly Counter<long> ReviewSnapshots =
        Meter.CreateCounter<long>("sgol.evidence.review_snapshots");
    internal static readonly Histogram<double> StorageDuration =
        Meter.CreateHistogram<double>("sgol.evidence.storage.duration", "ms");
    internal static readonly Histogram<double> ScanDuration =
        Meter.CreateHistogram<double>("sgol.evidence.scan.duration", "ms");
    internal static readonly Histogram<double> ReviewDuration =
        Meter.CreateHistogram<double>("sgol.evidence.review_duration", "ms");
}
