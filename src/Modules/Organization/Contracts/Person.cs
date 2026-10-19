namespace Sgol.Organization.Contracts;

public sealed class Person
{
    public Guid Id { get; init; }

    public required string StableCode { get; init; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public bool IsOnTrial { get; set; }
}

