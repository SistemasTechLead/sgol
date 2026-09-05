using System.Security.Cryptography;
using System.Text;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;

namespace Sgol.Cv02Demo;

internal sealed class DemoClock(DateTimeOffset initial) : IClock
{
    private long utcTicks = initial.UtcTicks;

    public DateTimeOffset UtcNow => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);

    public void Set(DateTimeOffset value) => Interlocked.Exchange(ref utcTicks, value.UtcTicks);
}

internal sealed class DeterministicUuid7Generator(DemoClock clock) : IUuidGenerator
{
    private long counter;

    public Guid NewUuid()
    {
        var sequence = Interlocked.Increment(ref counter);
        var material = SHA256.HashData(Encoding.UTF8.GetBytes($"{DemoContract.SeedId}|{sequence}"));
        Span<byte> bytes = stackalloc byte[16];
        material.AsSpan(0, 16).CopyTo(bytes);
        var unixMilliseconds = clock.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(unixMilliseconds >> 40);
        bytes[1] = (byte)(unixMilliseconds >> 32);
        bytes[2] = (byte)(unixMilliseconds >> 24);
        bytes[3] = (byte)(unixMilliseconds >> 16);
        bytes[4] = (byte)(unixMilliseconds >> 8);
        bytes[5] = (byte)unixMilliseconds;
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}
