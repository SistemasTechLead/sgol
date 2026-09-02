namespace Sgol.Organization.Contracts;

public sealed class EmploymentVersion
{
    private EmploymentVersion()
    {
    }

    public EmploymentVersion(
        Guid id,
        Guid personId,
        Guid branchId,
        string status,
        DateTimeOffset validFrom,
        Guid? supersedesId = null,
        long rowVersion = 1)
    {
        if (status is not EmploymentStatus.Active and not EmploymentStatus.Inactive)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(rowVersion, 1);

        Id = id;
        PersonId = personId;
        BranchId = branchId;
        Status = status;
        ValidFrom = validFrom;
        SupersedesId = supersedesId;
        RowVersion = rowVersion;
    }

    public Guid Id { get; private init; }

    public Guid PersonId { get; private init; }

    public Guid BranchId { get; private init; }

    public string Status { get; private init; } = null!;

    public string? PositionText { get; private init; }

    public string? ShiftText { get; private init; }

    public DateTimeOffset ValidFrom { get; private init; }

    public DateTimeOffset? ValidTo { get; private set; }

    public Guid? SupersedesId { get; private init; }

    public long RowVersion { get; private set; }

    public EmploymentVersion CreateSuccessor(Guid id, string status, DateTimeOffset changedAt)
    {
        if (ValidTo is not null)
        {
            throw new InvalidOperationException("Only the current employment version can be superseded.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(changedAt, ValidFrom);

        ValidTo = changedAt;
        RowVersion++;

        return new EmploymentVersion(
            id,
            PersonId,
            BranchId,
            status,
            changedAt,
            Id,
            RowVersion);
    }
}

public static class EmploymentStatus
{
    public const string Active = "ACTIVA";
    public const string Inactive = "INACTIVA";
}
