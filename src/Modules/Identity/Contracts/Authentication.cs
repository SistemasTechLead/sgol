namespace Sgol.Identity.Contracts;

public static class AuthenticationNextStep
{
    public const string ChangePassword = "CHANGE_PASSWORD";
    public const string EnrollMfa = "ENROLL_MFA";
    public const string VerifyMfa = "VERIFY_MFA";
    public const string RecoveryCodes = "RECOVERY_CODES";
    public const string RegenerateRecoveryCodes = "REGENERATE_RECOVERY_CODES";
    public const string Authenticated = "AUTHENTICATED";
}

public sealed record AuthenticationFlowResult(
    Guid ChallengeId,
    string NextStep,
    DateTimeOffset ChallengeExpiresAt);

public sealed record MfaEnrollmentResult(
    string ManualKey,
    string OtpAuthUri,
    DateTimeOffset ExpiresAt);

public sealed record AuthenticatedSession(
    Guid UserId,
    Guid PersonId,
    string UserName,
    string DisplayName,
    string RoleCode,
    string SecurityStamp,
    DateTimeOffset MfaAuthenticatedAt,
    DateTimeOffset AbsoluteExpiresAt,
    IReadOnlyList<string> Permissions);

public sealed record MfaCompletionResult(
    AuthenticatedSession Session,
    IReadOnlyList<string> RecoveryCodes,
    string NextStep);

public sealed record SessionSnapshot(
    Guid UserId,
    Guid PersonId,
    string UserName,
    string DisplayName,
    string BranchCode,
    string RoleCode,
    IReadOnlyList<string> Permissions,
    DateTimeOffset MfaAuthenticatedAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt);

public interface IHostedAuthenticationService
{
    Task<AuthenticationFlowResult> LoginAsync(
        string userName,
        string password,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<AuthenticationFlowResult> ChangePasswordAsync(
        Guid? challengeId,
        Guid? authenticatedUserId,
        string currentPassword,
        string newPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<MfaEnrollmentResult> BeginMfaEnrollmentAsync(
        Guid challengeId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<MfaCompletionResult> ConfirmMfaEnrollmentAsync(
        Guid challengeId,
        string totpCode,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<MfaCompletionResult> VerifyMfaAsync(
        Guid challengeId,
        string? totpCode,
        string? recoveryCode,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<MfaCompletionResult> RegenerateRecoveryCodesAsync(
        Guid? challengeId,
        Guid? authenticatedUserId,
        string currentPassword,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<SessionSnapshot> GetSessionAsync(
        Guid userId,
        string securityStamp,
        DateTimeOffset mfaAuthenticatedAt,
        DateTimeOffset absoluteExpiresAt,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedSession?> ValidateSessionAsync(
        Guid userId,
        string securityStamp,
        DateTimeOffset mfaAuthenticatedAt,
        DateTimeOffset absoluteExpiresAt,
        CancellationToken cancellationToken = default);

    Task RecordSessionRejectedAsync(
        Guid? userId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task RecordLogoutAsync(
        Guid? userId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class AuthenticationFailedException : Exception
{
}

public sealed class AuthenticationAccountLockedException(DateTimeOffset retryAt)
    : Exception("The account is temporarily locked.")
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public sealed class AuthenticationChallengeInvalidException()
    : Exception("The authentication challenge is invalid or expired.")
{
}

public sealed class AuthenticationPasswordPolicyException(string message) : Exception(message);

public sealed class AuthenticationMfaCodeInvalidException()
    : Exception("The MFA code is invalid.")
{
}

public sealed class AuthenticationMfaStateInconsistentException()
    : Exception("The persisted MFA state is inconsistent.")
{
}

public sealed class AuthenticationRecoveryRegenerationRequiredException()
    : Exception("Recovery codes must be regenerated before a full session can be issued.")
{
}

public sealed class AuthenticationSessionInvalidException() : Exception("The session is no longer valid.")
{
}

public sealed class AuthenticationValidationException(string message) : Exception(message);
