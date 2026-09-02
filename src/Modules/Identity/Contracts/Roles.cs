namespace Sgol.Identity.Contracts;

public static class CanonicalRole
{
    public const string Direction = "DIRECCION";
    public const string Administration = "ADMINISTRACION";
    public const string Subcoordination = "SUBCOORDINACION";
    public const string SalesFloor = "PISO_VENTAS";

    public static bool IsDefined(string? roleCode) => roleCode is
        Direction or Administration or Subcoordination or SalesFloor;
}

public static class RoleAssignmentStatus
{
    public const string Active = "ACTIVO";
    public const string Superseded = "SUSTITUIDO";
}

public static class RoleHierarchy
{
    public static bool CanAccess(string actorRoleCode, string targetRoleCode, bool sameUser)
    {
        if (!CanonicalRole.IsDefined(actorRoleCode) || !CanonicalRole.IsDefined(targetRoleCode))
        {
            return false;
        }

        if (sameUser || actorRoleCode == CanonicalRole.Direction)
        {
            return true;
        }

        return Rank(actorRoleCode) > Rank(targetRoleCode);
    }

    private static int Rank(string roleCode) => roleCode switch
    {
        CanonicalRole.Direction => 4,
        CanonicalRole.Administration => 3,
        CanonicalRole.Subcoordination => 2,
        CanonicalRole.SalesFloor => 1,
        _ => 0,
    };
}

public sealed record RoleAssignmentSnapshot(
    Guid Id,
    string RoleCode,
    string Status,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    Guid? SupersedesId,
    long RowVersion);

public sealed record RoleAssignmentDetails(
    Guid UserId,
    Guid BranchId,
    IReadOnlyList<RoleAssignmentSnapshot> History);

public sealed record ChangeRoleAssignmentCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid UserId,
    string? RoleCode,
    string Reason,
    long? ExpectedRowVersion);

public sealed record RoleAssignmentMutationResult(RoleAssignmentDetails Assignment, bool Replayed);

public interface IRoleAssignmentService
{
    Task<RoleAssignmentMutationResult> ChangeAsync(
        ChangeRoleAssignmentCommand command,
        CancellationToken cancellationToken = default);
}

public interface IRoleHierarchyResolver
{
    Task<bool> CanAccessUserAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}

public sealed class RoleAccessDeniedException() : Exception("PER-ROL-ADMIN is required for LOR-001.");

public sealed class RoleTargetNotFoundException() : Exception("The target account does not exist.");

public sealed class RoleTargetInactiveException() : Exception("The target account is not active.");

public sealed class RoleTargetOutOfScopeException() : Exception("The target account is not active in LOR-001.");

public sealed class RoleAssignmentNotFoundException() : Exception("There is no active role assignment to revoke.");

public sealed class RoleAssignmentNoChangeException() : Exception("The requested role is already active.");

public sealed class RoleIfMatchRequiredException() : Exception("If-Match is required to change or revoke an active role.");

public sealed class RoleVersionConflictException() : Exception("The active role assignment version changed.");

public sealed class RoleIdempotencyConflictException()
    : Exception("The idempotency key was already used with different content.");

public sealed class RoleAssignmentConflictException() : Exception("Only one active role is allowed per user and branch.");

public sealed class RoleValidationException(string message) : Exception(message);
