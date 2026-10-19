using Sgol.BuildingBlocks.Time;

namespace Sgol.BuildingBlocks.Identifiers;

public sealed class Uuid7Generator(IClock clock) : IUuidGenerator
{
    public Guid NewUuid() => Guid.CreateVersion7(clock.UtcNow);
}
