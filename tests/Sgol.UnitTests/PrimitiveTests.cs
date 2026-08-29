using System.Globalization;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Xunit;

namespace Sgol.UnitTests;

public sealed class PrimitiveTests
{
    [Fact]
    public void UuidGenerator_UsesInjectedClockAndCreatesVersion7Uuid()
    {
        var timestamp = new DateTimeOffset(2026, 8, 29, 12, 34, 56, TimeSpan.Zero);
        var generator = new Uuid7Generator(new FixedClock(timestamp));

        var uuid = generator.NewUuid();

        Assert.Equal(7, uuid.Version);
        Assert.Equal(
            timestamp.ToUnixTimeMilliseconds().ToString("x12", CultureInfo.InvariantCulture),
            uuid.ToString("N")[..12]);
    }

    [Fact]
    public void ClockPort_AllowsDeterministicTime()
    {
        var expected = new DateTimeOffset(2026, 8, 29, 17, 0, 0, TimeSpan.Zero);
        IClock clock = new FixedClock(expected);

        Assert.Equal(expected, clock.UtcNow);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
