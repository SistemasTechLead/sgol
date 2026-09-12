using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class EfRoleAssignmentService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IRoleAssignmentService, IRoleHierarchyResolver
{
    private const string ActiveRoleIndex = "IX_role_assignment_version_user_id_branch_id";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string ResourceType = "ROLE_ASSIGNMENT";

    public async Task<RoleAssignmentMutationResult> ChangeAsync(
        ChangeRoleAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var reason = RequireValue(command.Reason, "reason");
        var roleCode = NormalizeRoleCode(command.RoleCode);
        const string operation = "ROLE_ASSIGNMENT_CHANGE";
        var resource = command.UserId.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { command.UserId, roleCode, reason }, command.ExpectedRowVersion);
        var legacyOperation = roleCode is null ? "revoke" : "set";
        var legacyScope = $"role-assignments:{legacyOperation}:{command.ActorUserId:D}:{command.UserId:D}";
        var legacyRequestHash = ComputeHash(
            command.UserId.ToString("D", CultureInfo.InvariantCulture),
            roleCode ?? "<REVOKE>",
            reason,
            command.ExpectedRowVersion?.ToString(CultureInfo.InvariantCulture) ?? "<NONE>");
        var replay = await FindReplayAsync(scope, legacyScope, command, requestHash, legacyRequestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        Guid affectedAssignmentId = default;
        var originalDetails = await LoadDetailsAsync(command.UserId, cancellationToken);
        try
        {
            await auditTransaction.ExecuteAsync(
                async criticalWriteCancellationToken =>
                {
                    var target = await dbContext.AppUsers
                        .FromSqlInterpolated($"""
                            SELECT *
                            FROM app_user
                            WHERE id = {command.UserId}
                            FOR UPDATE
                            """)
                        .SingleOrDefaultAsync(criticalWriteCancellationToken)
                        ?? throw new RoleTargetNotFoundException();
                    if (target.Status != AccountStatus.Active)
                    {
                        throw new RoleTargetInactiveException();
                    }

                    var activeEmployment = await dbContext.EmploymentVersions
                        .AsNoTracking()
                        .AnyAsync(
                            item => item.PersonId == target.PersonId &&
                                item.BranchId == BranchScope.LorettaId &&
                                item.Status == EmploymentStatus.Active &&
                                item.ValidTo == null,
                            criticalWriteCancellationToken);
                    if (!activeEmployment)
                    {
                        throw new RoleTargetOutOfScopeException();
                    }

                    var current = await dbContext.RoleAssignmentVersions
                        .SingleOrDefaultAsync(
                            item => item.UserId == target.Id &&
                                item.BranchId == BranchScope.LorettaId &&
                                item.Status == RoleAssignmentStatus.Active &&
                                item.ValidTo == null,
                            criticalWriteCancellationToken);

                    if (current is null)
                    {
                        if (roleCode is null)
                        {
                            throw new RoleAssignmentNotFoundException();
                        }

                        if (command.ExpectedRowVersion is not null)
                        {
                            throw new RoleVersionConflictException();
                        }
                    }
                    else
                    {
                        if (command.ExpectedRowVersion is null)
                        {
                            throw new RoleIfMatchRequiredException();
                        }

                        if (current.RowVersion != command.ExpectedRowVersion)
                        {
                            throw new RoleVersionConflictException();
                        }

                        if (string.Equals(current.RoleCode, roleCode, StringComparison.Ordinal))
                        {
                            throw new RoleAssignmentNoChangeException();
                        }
                    }

                    var now = clock.UtcNow;
                    var beforeData = current is null ? null : SafeRoleData(current);
                    RoleAssignmentVersion? successor = null;
                    if (current is not null)
                    {
                        current.Status = RoleAssignmentStatus.Superseded;
                        current.ValidTo = now;
                        current.RowVersion++;
                        target.SecurityStamp = NewSecurityStamp();
                        affectedAssignmentId = current.Id;
                    }

                    if (roleCode is not null)
                    {
                        successor = new RoleAssignmentVersion
                        {
                            Id = uuidGenerator.NewUuid(),
                            UserId = target.Id,
                            BranchId = BranchScope.LorettaId,
                            RoleCode = roleCode,
                            Status = RoleAssignmentStatus.Active,
                            ValidFrom = now,
                            SupersedesId = current?.Id,
                            RowVersion = current?.RowVersion ?? 1,
                        };
                        dbContext.RoleAssignmentVersions.Add(successor);
                        affectedAssignmentId = successor.Id;
                    }

                    var afterData = successor is null
                        ? JsonSerializer.SerializeToDocument(new
                        {
                            schemaVersion = 1,
                            userId = target.Id,
                            branchCode = BranchScope.LorettaCode,
                            roleCode = (string?)null,
                            status = (string?)null,
                            supersededAssignmentId = current!.Id,
                            rowVersion = current.RowVersion,
                        })
                        : SafeRoleData(successor);
                    var history = originalDetails.History
                        .Select(item => current is not null && item.Id == current.Id
                            ? item with
                            {
                                Status = current.Status,
                                ValidTo = current.ValidTo,
                                RowVersion = current.RowVersion,
                            }
                            : item)
                        .ToList();
                    if (successor is not null)
                    {
                        history.Add(new RoleAssignmentSnapshot(
                            successor.Id,
                            successor.RoleCode,
                            successor.Status,
                            successor.ValidFrom,
                            successor.ValidTo,
                            successor.SupersedesId,
                            successor.RowVersion));
                    }

                    var response = new RoleAssignmentDetails(command.UserId, BranchScope.LorettaId, history);
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        affectedAssignmentId,
                        response,
                        now));

                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        target.Id,
                        current is null ? "ROLE_ASSIGNED" : successor is null ? "ROLE_REVOKED" : "ROLE_CHANGED",
                        beforeData,
                        afterData,
                        reason);
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new RoleVersionConflictException();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, legacyScope, command, requestHash, legacyRequestHash, cancellationToken)
                ?? throw new RoleIdempotencyConflictException();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == ActiveRoleIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new RoleAssignmentConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new RoleAssignmentMutationResult(
            await LoadDetailsAsync(command.UserId, cancellationToken),
            Replayed: false);
    }

    public async Task<bool> CanAccessUserAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var roles = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            where (user.Id == actorUserId || user.Id == targetUserId) &&
                user.Status == AccountStatus.Active &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidTo == null &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select new { UserId = user.Id, role.RoleCode })
            .ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        var actor = roles.SingleOrDefault(item => item.UserId == actorUserId);
        var target = roles.SingleOrDefault(item => item.UserId == targetUserId);
        if (actor is null || target is null)
        {
            return false;
        }

        return RoleHierarchy.CanAccess(actor.RoleCode, target.RoleCode, actorUserId == targetUserId);
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var actor = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == AccountStatus.Active &&
                role.BranchId == BranchScope.LorettaId &&
                role.RoleCode == CanonicalRole.Direction &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidTo == null &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select user.Id)
            .AnyAsync(cancellationToken);
        if (actor)
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "ROLE_ADMIN_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new RoleAccessDeniedException();
    }

    private async Task<RoleAssignmentMutationResult?> FindReplayAsync(
        string scope,
        string legacyScope,
        ChangeRoleAssignmentCommand command,
        string requestHash,
        string legacyRequestHash,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == command.IdempotencyKey, cancellationToken)
            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == legacyScope && item.Key == command.IdempotencyKey, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var expectedHash = record.ProtocolVersion == IdempotencyProtocol.CurrentVersion
            ? requestHash
            : legacyRequestHash;
        if (!string.Equals(record.RequestHash, expectedHash, StringComparison.Ordinal))
        {
            dbContext.ChangeTracker.Clear();
            await AuditConflictAsync(command, record.ResourceId, cancellationToken);
            throw new RoleIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return new RoleAssignmentMutationResult(
                IdempotencyProtocol.ReadPayload<RoleAssignmentDetails>(record),
                Replayed: true);
        }

        return new RoleAssignmentMutationResult(
            await LoadDetailsAsync(command.UserId, cancellationToken),
            Replayed: true);
    }

    private Task AuditConflictAsync(ChangeRoleAssignmentCommand command, Guid resourceId, CancellationToken token) =>
        IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(),
                clock.UtcNow,
                command.ActorUserId,
                ResourceType,
                resourceId,
                BranchScope.LorettaId,
                command.CorrelationId,
                command.IdempotencyKey,
                "ROLE_ASSIGNMENT_CHANGE"),
            token);

    private async Task<RoleAssignmentDetails> LoadDetailsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var history = await dbContext.RoleAssignmentVersions
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.BranchId == BranchScope.LorettaId)
            .OrderBy(item => item.RowVersion)
            .ThenBy(item => item.ValidTo == null)
            .ThenBy(item => item.ValidFrom)
            .ThenBy(item => item.Id)
            .Select(item => new RoleAssignmentSnapshot(
                item.Id,
                item.RoleCode,
                item.Status,
                item.ValidFrom,
                item.ValidTo,
                item.SupersedesId,
                item.RowVersion))
            .ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return new RoleAssignmentDetails(userId, BranchScope.LorettaId, history);
    }

    private AuditEvent NewAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        string action,
        JsonDocument? beforeData = null,
        JsonDocument? afterData = null,
        string? reason = null,
        string outcome = "SUCCESS") => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = action,
            ResourceType = ResourceType,
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    private static JsonDocument SafeRoleData(RoleAssignmentVersion role) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            assignmentId = role.Id,
            userId = role.UserId,
            branchCode = BranchScope.LorettaCode,
            roleCode = role.RoleCode,
            status = role.Status,
            validFrom = role.ValidFrom,
            validTo = role.ValidTo,
            supersedesId = role.SupersedesId,
            rowVersion = role.RowVersion,
        });

    private static IdempotencyRecord NewIdempotencyRecord(
        string scope,
        Guid key,
        string requestHash,
        Guid resourceId,
        RoleAssignmentDetails response,
        DateTimeOffset createdAt) => IdempotencyProtocol.Completed(
            scope,
            key,
            requestHash,
            ResourceType,
            resourceId,
            StatusCodes.Status200OK,
            response,
            createdAt,
            DateTimeOffset.MaxValue);

    private static string? NormalizeRoleCode(string? roleCode)
    {
        if (roleCode is null)
        {
            return null;
        }

        var normalized = RequireValue(roleCode, "roleCode").ToUpperInvariant();
        if (!CanonicalRole.IsDefined(normalized))
        {
            throw new RoleValidationException("roleCode must be a canonical role.");
        }

        return normalized;
    }

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RoleValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string ComputeHash(params string[] parts)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', parts));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string NewSecurityStamp() => Convert.ToHexString(Guid.NewGuid().ToByteArray());

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;

}
