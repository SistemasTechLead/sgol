namespace Sgol.Organization.Contracts;

public sealed record PersonSummary(Guid Id, string StableCode, string DisplayName);

public sealed record EmploymentVersionSnapshot(
    Guid Id,
    string Status,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    Guid? SupersedesId,
    long RowVersion,
    string? PositionText = null,
    string? ShiftText = null);

public sealed record PersonDetails(
    Guid Id,
    string StableCode,
    string DisplayName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EmploymentVersionSnapshot> EmploymentHistory);

public sealed record CreatePersonCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string StableCode,
    string DisplayName);

public sealed record ChangeEmploymentCommand(
    Guid ActorUserId,
    Guid CorrelationId,
    Guid PersonId,
    string Status,
    long ExpectedRowVersion,
    string Reason,
    Guid? IdempotencyKey = null,
    string? PositionText = null,
    string? ShiftText = null);

public sealed record PersonMutationResult(PersonDetails Person, bool Replayed);

public interface IPersonAdministrationService
{
    Task<IReadOnlyList<PersonSummary>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonDetails?> FindAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid personId,
        CancellationToken cancellationToken = default);

    Task<PersonMutationResult> CreateAsync(
        CreatePersonCommand command,
        CancellationToken cancellationToken = default);

    Task<PersonMutationResult> ChangeEmploymentAsync(
        ChangeEmploymentCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class PersonAccessDeniedException() : Exception("PER-PERSONA-ADMIN is required for LOR-001.");

public sealed class PersonCodeConflictException() : Exception("The stable person code is already registered.");

public sealed class PersonVersionConflictException() : Exception("The employment version is no longer current.");

public sealed class PersonIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");

public sealed class PersonNotFoundException() : Exception("The person does not exist.");

public sealed class PersonStateConflictException() : Exception("The requested employment status is already current.");

public sealed class PersonEmploymentNoChangeException() : Exception("The requested employment data is already current.");

public sealed class PersonValidationException(string message) : Exception(message);
