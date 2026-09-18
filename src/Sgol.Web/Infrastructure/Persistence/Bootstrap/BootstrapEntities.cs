namespace Sgol.Web.Infrastructure.Persistence.Bootstrap;

public static class BootstrapContract
{
    public const string DirectionRoleCode = "DIRECCION";
    public const string ActivePersonStatus = "ACTIVA";
    public const string ActiveAccountStatus = "ACTIVA";
    public const string ActiveRoleStatus = "ACTIVO";
}

public sealed class AppUser
{
    public Guid Id { get; init; }

    public Guid PersonId { get; init; }

    public required string Status { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTimeOffset? MfaEnrolledAt { get; set; }

    public required string SecurityStamp { get; set; }

    public int AccessFailedCount { get; set; }

    public int LockoutLevel { get; set; }

    public DateTimeOffset? LockoutEndUtc { get; set; }

    public long AuthenticationRowVersion { get; set; } = 1;

    public bool RequiresFirstAccessSetup => MustChangePassword || MfaEnrolledAt is null;
}

public static class AuthenticationChallengePurpose
{
    public const string ChangePassword = "CHANGE_PASSWORD";
    public const string EnrollMfa = "ENROLL_MFA";
    public const string VerifyMfa = "VERIFY_MFA";
    public const string RegenerateRecoveryCodes = "REGENERATE_RECOVERY_CODES";
}

public static class AuthenticationChallengeStatus
{
    public const string Pending = "PENDING";
    public const string Consumed = "CONSUMED";
    public const string Expired = "EXPIRED";
}

public sealed class AuthenticationChallenge
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public required string Purpose { get; set; }

    public required string Status { get; set; }

    public required string SecurityStampSnapshot { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public int FailedAttemptCount { get; set; }

    public string? ProtectedTotpSecret { get; set; }

    public long RowVersion { get; set; } = 1;
}

public sealed class MfaTotpCredential
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public required string ProtectedSecret { get; init; }

    public int ProtectionVersion { get; init; } = 1;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset EnrolledAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public long? LastAcceptedTimeStep { get; set; }

    public long RowVersion { get; set; } = 1;
}

public sealed class MfaRecoveryCode
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public required string CodeHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public long RowVersion { get; set; } = 1;
}

public sealed class IdentityCredential
{
    public Guid UserId { get; init; }

    public required string UserName { get; init; }

    public required string NormalizedUserName { get; init; }

    public required string PasswordHash { get; set; }
}

public sealed class RoleAssignmentVersion
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid BranchId { get; init; }

    public required string RoleCode { get; init; }

    public required string Status { get; set; }

    public DateTimeOffset ValidFrom { get; init; }

    public DateTimeOffset? ValidTo { get; set; }

    public Guid? SupersedesId { get; init; }

    public long RowVersion { get; set; } = 1;
}

public sealed class DirectionBootstrapMarker
{
    public bool Singleton { get; init; }

    public DateTimeOffset CompletedAt { get; init; }
}
