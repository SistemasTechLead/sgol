namespace Sgol.Organization.Contracts;

public sealed class Branch
{
    public Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public required string TimeZone { get; init; }
}
