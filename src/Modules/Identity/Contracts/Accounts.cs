namespace Sgol.Identity.Contracts;

public static class AccountStatus
{
    public const string Active = "ACTIVA";
    public const string Inactive = "INACTIVA";
}

public sealed record AccountSummary(
    Guid Id,
    Guid PersonId,
    string UserName,
    string Status,
    bool MustChangePassword,
    DateTimeOffset? MfaEnrolledAt);

public sealed class CreateAccountCommand
{
    public required Guid ActorUserId { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid CorrelationId { get; init; }

    public required Guid PersonId { get; init; }

    public required string UserName { get; init; }

    public required string TemporaryPassword { get; init; }

    public override string ToString() =>
        $"{nameof(CreateAccountCommand)} {{ ActorUserId = {ActorUserId}, PersonId = {PersonId}, " +
        $"UserName = {UserName}, TemporaryPassword = [REDACTED] }}";
}

public sealed class ChangeAccountStatusCommand
{
    public required Guid ActorUserId { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid CorrelationId { get; init; }

    public required Guid UserId { get; init; }

    public required string Reason { get; init; }

    public string? TemporaryPassword { get; init; }

    public override string ToString() =>
        $"{nameof(ChangeAccountStatusCommand)} {{ ActorUserId = {ActorUserId}, UserId = {UserId}, " +
        $"Reason = {Reason}, TemporaryPassword = [REDACTED] }}";
}

public sealed record AccountMutationResult(AccountSummary Account, bool Replayed);

public interface IAccountAdministrationService
{
    Task<IReadOnlyList<AccountSummary>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<AccountMutationResult> CreateAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default);

    Task<AccountMutationResult> DeactivateAsync(
        ChangeAccountStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<AccountMutationResult> ReactivateAsync(
        ChangeAccountStatusCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class AccountAccessDeniedException()
    : Exception("PER-USUARIO-ADMIN is required for LOR-001.");

public sealed class AccountPersonNotFoundException() : Exception("The person does not exist.");

public sealed class AccountPersonOutOfScopeException()
    : Exception("The person is not active in LOR-001.");

public sealed class AccountNotFoundException() : Exception("The account does not exist.");

public sealed class AccountConflictException()
    : Exception("The person or access identifier already has an account.");

public sealed class AccountStateConflictException()
    : Exception("The requested account status is already current.");

public sealed class AccountIdempotencyConflictException()
    : Exception("The idempotency key was already used with different content.");

public sealed class AccountValidationException(string message) : Exception(message);
