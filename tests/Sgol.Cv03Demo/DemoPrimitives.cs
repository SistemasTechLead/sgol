using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;

namespace Sgol.Cv03Demo;

internal sealed class DemoClock(DateTimeOffset initial) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = initial;
    public void Set(DateTimeOffset value) => UtcNow = value;
    public void AdvancePast(DateTimeOffset instant)
    {
        if (UtcNow <= instant)
        {
            UtcNow = instant.AddSeconds(1);
        }
    }
}

internal sealed class DeterministicUuid7Generator(IClock clock) : IUuidGenerator
{
    private long sequence;

    public Guid NewUuid()
    {
        var bytes = Guid.CreateVersion7(clock.UtcNow).ToByteArray();
        var value = Interlocked.Increment(ref sequence);
        for (var index = 0; index < sizeof(long); index++)
        {
            bytes[15 - index] = (byte)(value >> (index * 8));
        }
        return new Guid(bytes);
    }
}

internal static class Cv03Timeline
{
    public static readonly DateTimeOffset Now = Anchor(DateTimeOffset.UtcNow);
    public static readonly DateTimeOffset Before = Now.AddHours(-2);
    public static readonly DateTimeOffset After = Now.AddHours(2);

    internal static DateTimeOffset Anchor(DateTimeOffset observedUtc) =>
        new(observedUtc.Year, observedUtc.Month, observedUtc.Day,
            observedUtc.Hour, observedUtc.Minute, 0, TimeSpan.Zero);
}
