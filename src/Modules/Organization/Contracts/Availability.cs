namespace Sgol.Organization.Contracts;

public static class AvailabilityAuthorization
{
    public const string Administer = "PER-DISPONIBILIDAD-ADMIN";
}

public static class AvailabilityVersionStatus
{
    public const string Current = "VIGENTE";
    public const string Historical = "HISTORICA";
}

public sealed class AvailabilityDayVersion
{
    private AvailabilityDayVersion()
    {
    }

    public AvailabilityDayVersion(
        Guid id,
        Guid personId,
        Guid branchId,
        DateOnly localDate,
        bool isAvailable,
        Guid changedBy,
        Guid? supersedesId = null,
        long rowVersion = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowVersion, 1);

        Id = id;
        PersonId = personId;
        BranchId = branchId;
        LocalDate = localDate;
        IsAvailable = isAvailable;
        Status = AvailabilityVersionStatus.Current;
        SupersedesId = supersedesId;
        ChangedBy = changedBy;
        RowVersion = rowVersion;
    }

    public Guid Id { get; private init; }

    public Guid PersonId { get; private init; }

    public Guid BranchId { get; private init; }

    public DateOnly LocalDate { get; private init; }

    public bool IsAvailable { get; private init; }

    public string Status { get; private set; } = null!;

    public Guid? SupersedesId { get; private init; }

    public Guid ChangedBy { get; private init; }

    public long RowVersion { get; private set; }

    public AvailabilityDayVersion CreateSuccessor(Guid id, bool isAvailable, Guid changedBy)
    {
        if (Status != AvailabilityVersionStatus.Current)
        {
            throw new InvalidOperationException("Only the current availability version can be superseded.");
        }

        Status = AvailabilityVersionStatus.Historical;
        RowVersion++;

        return new AvailabilityDayVersion(
            id,
            PersonId,
            BranchId,
            LocalDate,
            isAvailable,
            changedBy,
            Id,
            RowVersion);
    }
}

public sealed record AvailabilityDaySnapshot(
    Guid Id,
    Guid PersonId,
    DateOnly LocalDate,
    bool IsAvailable,
    long RowVersion);

public sealed record PutAvailabilityCommand(
    Guid ActorUserId,
    Guid CorrelationId,
    Guid PersonId,
    DateOnly LocalDate,
    bool IsAvailable,
    long? ExpectedRowVersion);

public interface IAvailabilityAdministrationService
{
    Task<IReadOnlyList<AvailabilityDaySnapshot>> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid personId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<AvailabilityDaySnapshot> PutAsync(
        PutAvailabilityCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class AvailabilityAccessDeniedException()
    : Exception($"{AvailabilityAuthorization.Administer} is required for LOR-001.");

public sealed class AvailabilityPersonNotFoundException() : Exception("The person does not exist.");

public sealed class AvailabilityPersonInactiveException() : Exception("The person is not active.");

public sealed class AvailabilityPersonOutOfScopeException() : Exception("The person is outside LOR-001.");

public sealed class AvailabilityIfMatchRequiredException()
    : Exception("If-Match is required to correct an availability value.");

public sealed class AvailabilityVersionConflictException() : Exception("The availability version changed.");

public sealed class AvailabilityValidationException(string message) : Exception(message);
