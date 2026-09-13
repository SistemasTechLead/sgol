using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sgol.Operations;

internal static class OperationsTelemetry
{
    private static readonly Meter Meter = new("Sgol.Operations");
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("sgol.operations.runs");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("sgol.operations.duration", "ms");
    private static readonly Counter<long> Objects = Meter.CreateCounter<long>("sgol.operations.objects");
    private static readonly Counter<long> Bytes = Meter.CreateCounter<long>("sgol.operations.bytes");

    internal static void Record(string operation, string result, TimeSpan duration, long objects, long bytes)
    {
        TagList tags = new() { { "operation", operation }, { "result", result } };
        Runs.Add(1, tags);
        Duration.Record(duration.TotalMilliseconds, tags);
        Objects.Add(objects, tags);
        Bytes.Add(bytes, tags);
    }
}
