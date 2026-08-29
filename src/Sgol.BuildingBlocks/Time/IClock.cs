namespace Sgol.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
