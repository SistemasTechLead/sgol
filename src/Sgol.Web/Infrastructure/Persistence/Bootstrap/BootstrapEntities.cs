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

    public bool RequiresFirstAccessSetup => MustChangePassword || MfaEnrolledAt is null;
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

    public required string Status { get; init; }

    public DateTimeOffset ValidFrom { get; init; }

    public DateTimeOffset? ValidTo { get; init; }

    public Guid? SupersedesId { get; init; }
}

public sealed class DirectionBootstrapMarker
{
    public bool Singleton { get; init; }

    public DateTimeOffset CompletedAt { get; init; }
}
