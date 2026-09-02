namespace Sgol.Organization.Contracts;

public sealed class Person
{
    public Guid Id { get; init; }

    public required string StableCode { get; init; }

    public required string DisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
