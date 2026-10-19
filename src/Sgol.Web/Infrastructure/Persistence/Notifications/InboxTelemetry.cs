using System.Diagnostics.Metrics;

namespace Sgol.Web.Infrastructure.Persistence.Notifications;

internal static class InboxTelemetry
{
    private static readonly Meter Meter = new("Sgol.Notifications");
    internal static readonly Counter<long> Queries = Meter.CreateCounter<long>("sgol_inbox_queries_total");
    internal static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>("sgol_inbox_query_duration_seconds");
    internal static readonly Counter<long> Notices = Meter.CreateCounter<long>("sgol_internal_notices_total");
}
