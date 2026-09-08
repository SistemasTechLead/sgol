using System.Diagnostics.Metrics;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

internal static class ConclusionTelemetry
{
    private static readonly Meter Meter = new("Sgol.Execution");

    internal static readonly Counter<long> Conclusions =
        Meter.CreateCounter<long>("sgol_obligation_conclusions_total");
}
