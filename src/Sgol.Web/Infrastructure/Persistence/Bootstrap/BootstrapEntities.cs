namespace Sgol.Web.Infrastructure.Persistence.Bootstrap;

public static class BootstrapContract
{
    public static readonly Guid LorettaBranchId = Guid.Parse("019d2d67-2c00-7000-8000-000000000001");

    public const string LorettaBranchCode = "LOR-001";
    public const string DirectionRoleCode = "DIRECCION";
    public const string ActivePersonStatus = "ACTIVA";
    public const string ActiveAccountStatus = "ACTIVA";
    public const string ActiveRoleStatus = "ACTIVO";
}

public sealed class Branch
{
    public Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public required string TimeZone { get; init; }
}

public sealed class Person
{
    public Guid Id { get; init; }

    public required string StableCode { get; init; }

    public required string DisplayName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class EmploymentVersion
{
    public Guid Id { get; init; }

    public Guid PersonId { get; init; }

    public Guid BranchId { get; init; }

    public required string Status { get; init; }

    public string? PositionText { get; init; }

    public string? ShiftText { get; init; }

    public DateTimeOffset ValidFrom { get; init; }

    public DateTimeOffset? ValidTo { get; init; }

    public Guid? SupersedesId { get; init; }

    public long RowVersion { get; init; }
}

public sealed class AppUser
{
    public Guid Id { get; init; }

    public Guid PersonId { get; init; }

    public required string Status { get; init; }

    public bool MustChangePassword { get; init; }

    public DateTimeOffset? MfaEnrolledAt { get; init; }

    public required string SecurityStamp { get; init; }

    public bool RequiresFirstAccessSetup => MustChangePassword || MfaEnrolledAt is null;
}

public sealed class IdentityCredential
{
    public Guid UserId { get; init; }

    public required string UserName { get; init; }

    public required string NormalizedUserName { get; init; }

    public required string PasswordHash { get; init; }
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
