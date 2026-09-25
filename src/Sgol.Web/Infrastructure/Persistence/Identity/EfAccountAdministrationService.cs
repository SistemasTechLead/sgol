using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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

public sealed class EfAccountAdministrationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator,
    IPasswordHasher<AppUser> passwordHasher) : IAccountAdministrationService
{
    private const string AppUserPersonIndex = "IX_app_user_person_id";
    private const string CredentialUserNameIndex = "IX_identity_credential_normalized_user_name";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";

    public async Task<IReadOnlyList<AccountSummary>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);

        var accounts = await LoadAccountsQuery().ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return accounts;
    }

    public async Task<AccountMutationResult> CreateAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var userName = RequireValue(command.UserName, "UserName");
        var normalizedUserName = userName.ToUpperInvariant();
        var generatedPassword = command.TemporaryPassword is null;
        var temporaryPassword = RequireTemporaryPassword(command.TemporaryPassword ?? NewTemporaryPassword());
        const string operation = "ACCOUNT_CREATE";
        const string resource = "new:LOR-001";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { command.PersonId, userName = normalizedUserName, temporaryPassword = command.TemporaryPassword });
        var legacyScope = CreateScope(command.ActorUserId);
        var legacyRequestHash = ComputeHash(
            command.PersonId.ToString("D"),
            normalizedUserName,
            temporaryPassword);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (!await dbContext.People.AsNoTracking().AnyAsync(
                person => person.Id == command.PersonId,
                cancellationToken))
        {
            dbContext.ChangeTracker.Clear();
            throw new AccountPersonNotFoundException();
        }

        var now = clock.UtcNow;
        var user = new AppUser
        {
            Id = uuidGenerator.NewUuid(),
            PersonId = command.PersonId,
            Status = AccountStatus.Active,
            MustChangePassword = true,
            MfaEnrolledAt = null,
            SecurityStamp = NewSecurityStamp(),
        };
        var credential = new IdentityCredential
        {
            UserId = user.Id,
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            PasswordHash = passwordHasher.HashPassword(user, temporaryPassword),
        };
        var response = new AccountSummary(
            user.Id, user.PersonId, userName, user.Status, user.MustChangePassword, user.MfaEnrolledAt);

        try
        {
            await auditTransaction.ExecuteAsync(
                async criticalWriteCancellationToken =>
                {
                    var employment = await dbContext.EmploymentVersions
                        .FromSqlInterpolated($"""
                            SELECT *
                            FROM employment_version
                            WHERE person_id = {command.PersonId}
                              AND branch_id = {BranchScope.LorettaId}
                              AND valid_to IS NULL
                            FOR SHARE
                            """)
                        .SingleOrDefaultAsync(criticalWriteCancellationToken);
                    if (employment is null || employment.Status != EmploymentStatus.Active)
                    {
                        throw new AccountPersonOutOfScopeException();
                    }

                    dbContext.AppUsers.Add(user);
                    dbContext.IdentityCredentials.Add(credential);
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        user.Id,
                        StatusCodes.Status201Created,
                        response,
                        $"/api/v1/users/{user.Id:D}",
                        now));

                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        user.Id,
                        "USER_CREATED",
                        afterData: SafeAccountData(user, userName));
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is
            AppUserPersonIndex or CredentialUserNameIndex or IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindReplayAsync(
                scope,
                legacyScope,
                command.IdempotencyKey,
                requestHash,
                legacyRequestHash,
                command.ActorUserId,
                command.CorrelationId,
                operation,
                cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw new AccountConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new AccountMutationResult((await LoadAccountAsync(user.Id, cancellationToken))!, Replayed: false,
            generatedPassword ? temporaryPassword : null);
    }

    public Task<AccountMutationResult> DeactivateAsync(
        ChangeAccountStatusCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(command, AccountStatus.Inactive, requiresTemporaryPassword: false, cancellationToken);

    public Task<AccountMutationResult> ReactivateAsync(
        ChangeAccountStatusCommand command,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(command, AccountStatus.Active, requiresTemporaryPassword: true, cancellationToken);

    public async Task<AccountMutationResult> ResetMfaAsync(
        ResetMfaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var mfaAge = command.MfaAuthenticatedAt is { } mfaAt ? clock.UtcNow - mfaAt : TimeSpan.MaxValue;
        if (mfaAge < TimeSpan.Zero || mfaAge > TimeSpan.FromMinutes(5))
        {
            throw new AccountRecentMfaRequiredException();
        }

        var reason = RequireValue(command.Reason, "Reason");
        var generatedPassword = command.TemporaryPassword is null;
        const string operation = "ACCOUNT_MFA_RESET";
        var resource = command.UserId.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation,
            command.ActorUserId.ToString("D"),
            resource,
            new { command.UserId, reason, temporaryPassword = command.TemporaryPassword });
        var replay = await FindReplayAsync(
            scope,
            scope,
            command.IdempotencyKey,
            requestHash,
            requestHash,
            command.ActorUserId,
            command.CorrelationId,
            operation,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var temporaryPassword = RequireTemporaryPassword(command.TemporaryPassword ?? NewTemporaryPassword());

        try
        {
            await auditTransaction.ExecuteAsync(
                async criticalWriteCancellationToken =>
                {
                    var user = await dbContext.AppUsers
                        .FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {command.UserId} FOR UPDATE")
                        .SingleOrDefaultAsync(criticalWriteCancellationToken)
                        ?? throw new AccountNotFoundException();
                    if (user.Status != AccountStatus.Active)
                    {
                        throw new AccountTargetInactiveException();
                    }

                    var activeEmployment = await dbContext.EmploymentVersions.AsNoTracking()
                        .AnyAsync(item => item.PersonId == user.PersonId &&
                            item.BranchId == BranchScope.LorettaId &&
                            item.Status == EmploymentStatus.Active && item.ValidTo == null,
                            criticalWriteCancellationToken);
                    if (!activeEmployment)
                    {
                        throw new AccountPersonOutOfScopeException();
                    }
                    var credential = await dbContext.IdentityCredentials
                        .SingleAsync(item => item.UserId == user.Id, criticalWriteCancellationToken);
                    var now = clock.UtcNow;
                    var beforeData = SafeAccountData(user, credential.UserName);

                    var totpCredentials = await dbContext.MfaTotpCredentials
                        .Where(item => item.UserId == user.Id && item.RevokedAt == null)
                        .ToListAsync(criticalWriteCancellationToken);
                    foreach (var item in totpCredentials)
                    {
                        item.RevokedAt = now;
                        item.RowVersion++;
                    }

                    var recoveryCodes = await dbContext.MfaRecoveryCodes
                        .Where(item => item.UserId == user.Id && item.ConsumedAt == null && item.RevokedAt == null)
                        .ToListAsync(criticalWriteCancellationToken);
                    foreach (var item in recoveryCodes)
                    {
                        item.RevokedAt = now;
                        item.RowVersion++;
                    }

                    var challenges = await dbContext.AuthenticationChallenges
                        .Where(item => item.UserId == user.Id && item.Status == AuthenticationChallengeStatus.Pending)
                        .ToListAsync(criticalWriteCancellationToken);
                    foreach (var item in challenges)
                    {
                        item.Status = AuthenticationChallengeStatus.Consumed;
                        item.ConsumedAt = now;
                        item.RowVersion++;
                    }

                    credential.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
                    user.MustChangePassword = true;
                    user.MfaEnrolledAt = null;
                    user.AccessFailedCount = 0;
                    user.LockoutLevel = 0;
                    user.LockoutEndUtc = null;
                    user.AuthenticationRowVersion++;
                    user.SecurityStamp = NewSecurityStamp();
                    var afterData = SafeAccountData(user, credential.UserName);
                    var response = new AccountSummary(
                        user.Id,
                        user.PersonId,
                        credential.UserName,
                        user.Status,
                        user.MustChangePassword,
                        user.MfaEnrolledAt);
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        user.Id,
                        StatusCodes.Status200OK,
                        response,
                        null,
                        now));

                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        user.Id,
                        "AUTH_MFA_RESET",
                        beforeData,
                        afterData,
                        reason);
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(
                scope,
                scope,
                command.IdempotencyKey,
                requestHash,
                requestHash,
                command.ActorUserId,
                command.CorrelationId,
                operation,
                cancellationToken)
                ?? throw new AccountIdempotencyConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new AccountMutationResult((await LoadAccountAsync(command.UserId, cancellationToken))!, Replayed: false,
            generatedPassword ? temporaryPassword : null);
    }

    private async Task<AccountMutationResult> ChangeStatusAsync(
        ChangeAccountStatusCommand command,
        string requestedStatus,
        bool requiresTemporaryPassword,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var reason = RequireValue(command.Reason, "Reason");
        var generatedPassword = requiresTemporaryPassword && command.TemporaryPassword is null;
        var temporaryPassword = requiresTemporaryPassword
            ? RequireTemporaryPassword(command.TemporaryPassword ?? NewTemporaryPassword())
            : null;
        var operation = requestedStatus == AccountStatus.Active ? "ACCOUNT_REACTIVATE" : "ACCOUNT_DEACTIVATE";
        var resource = command.UserId.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { command.UserId, requestedStatus, reason, temporaryPassword = command.TemporaryPassword });
        var legacyOperation = requestedStatus == AccountStatus.Active ? "reactivate" : "deactivate";
        var legacyScope = StatusScope(command.ActorUserId, legacyOperation, command.UserId);
        var legacyRequestHash = ComputeHash(
            command.UserId.ToString("D"),
            requestedStatus,
            reason,
            temporaryPassword ?? string.Empty);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        try
        {
            await auditTransaction.ExecuteAsync(
                async criticalWriteCancellationToken =>
                {
                    var user = await dbContext.AppUsers
                        .FromSqlInterpolated($"""
                            SELECT *
                            FROM app_user
                            WHERE id = {command.UserId}
                            FOR UPDATE
                            """)
                        .SingleOrDefaultAsync(criticalWriteCancellationToken)
                        ?? throw new AccountNotFoundException();
                    var credential = await dbContext.IdentityCredentials
                        .SingleAsync(item => item.UserId == user.Id, criticalWriteCancellationToken);

                    if (user.Status == requestedStatus)
                    {
                        throw new AccountStateConflictException();
                    }

                    if (requestedStatus == AccountStatus.Active)
                    {
                        var activeEmployment = await dbContext.EmploymentVersions
                            .FromSqlInterpolated($"""
                                SELECT *
                                FROM employment_version
                                WHERE person_id = {user.PersonId}
                                  AND branch_id = {BranchScope.LorettaId}
                                  AND valid_to IS NULL
                                FOR SHARE
                                """)
                            .SingleOrDefaultAsync(criticalWriteCancellationToken);
                        if (activeEmployment is null || activeEmployment.Status != EmploymentStatus.Active)
                        {
                            throw new AccountPersonOutOfScopeException();
                        }

                        credential.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword!);
                        user.MustChangePassword = true;
                    }

                    var beforeData = SafeAccountData(user, credential.UserName);
                    user.Status = requestedStatus;
                    user.SecurityStamp = NewSecurityStamp();
                    var afterData = SafeAccountData(user, credential.UserName);
                    var now = clock.UtcNow;
                    var response = new AccountSummary(
                        user.Id, user.PersonId, credential.UserName, user.Status,
                        user.MustChangePassword, user.MfaEnrolledAt);
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        scope,
                        command.IdempotencyKey,
                        requestHash,
                        user.Id,
                        StatusCodes.Status200OK,
                        response,
                        null,
                        now));

                    return NewAuditEvent(
                        command.ActorUserId,
                        command.CorrelationId,
                        user.Id,
                        requestedStatus == AccountStatus.Active ? "USER_REACTIVATED" : "USER_DEACTIVATED",
                        beforeData,
                        afterData,
                        reason);
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.ActorUserId, command.CorrelationId, operation, cancellationToken)
                ?? throw new AccountIdempotencyConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new AccountMutationResult((await LoadAccountAsync(command.UserId, cancellationToken))!, Replayed: false,
            generatedPassword ? temporaryPassword : null);
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var authorized = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == AccountStatus.Active &&
                role.BranchId == BranchScope.LorettaId &&
                role.RoleCode == BootstrapContract.DirectionRoleCode &&
                role.Status == BootstrapContract.ActiveRoleStatus &&
                role.ValidTo == null &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select user.Id)
            .AnyAsync(cancellationToken);
        if (authorized)
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "USER_ADMIN_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new AccountAccessDeniedException();
    }

    private async Task<AccountMutationResult?> FindReplayAsync(
        string scope,
        string legacyScope,
        Guid key,
        string requestHash,
        string legacyRequestHash,
        Guid actorUserId,
        Guid correlationId,
        string operation,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken)
            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Scope == legacyScope && item.Key == key, cancellationToken);
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
            await AuditConflictAsync(actorUserId, correlationId, key, operation, record.ResourceId, cancellationToken);
            throw new AccountIdempotencyConflictException();
        }

        if (record.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
        {
            return new AccountMutationResult(
                IdempotencyProtocol.ReadPayload<AccountSummary>(record),
                Replayed: true);
        }

        var account = await LoadAccountAsync(record.ResourceId, cancellationToken)
            ?? throw new AccountNotFoundException();
        return new AccountMutationResult(account, Replayed: true);
    }

    private Task AuditConflictAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid key,
        string operation,
        Guid resourceId,
        CancellationToken token) => IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(),
                clock.UtcNow,
                actorUserId,
                "APP_USER",
                resourceId,
                BranchScope.LorettaId,
                correlationId,
                key,
                operation),
            token);

    private IQueryable<AccountSummary> LoadAccountsQuery() =>
        from user in dbContext.AppUsers.AsNoTracking()
        join credential in dbContext.IdentityCredentials.AsNoTracking()
            on user.Id equals credential.UserId
        orderby credential.UserName, user.Id
        select new AccountSummary(
            user.Id,
            user.PersonId,
            credential.UserName,
            user.Status,
            user.MustChangePassword,
            user.MfaEnrolledAt);

    private async Task<AccountSummary?> LoadAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join credential in dbContext.IdentityCredentials.AsNoTracking()
                on user.Id equals credential.UserId
            where user.Id == userId
            select new AccountSummary(
                user.Id,
                user.PersonId,
                credential.UserName,
                user.Status,
                user.MustChangePassword,
                user.MfaEnrolledAt))
            .SingleOrDefaultAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return account;
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
            ResourceType = "APP_USER",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    private static JsonDocument SafeAccountData(AppUser user, string userName) =>
        JsonSerializer.SerializeToDocument(new
        {
            id = user.Id,
            personId = user.PersonId,
            userName,
            status = user.Status,
            mustChangePassword = user.MustChangePassword,
            mfaEnrolledAt = user.MfaEnrolledAt,
        });

    private static IdempotencyRecord NewIdempotencyRecord(
        string scope,
        Guid key,
        string requestHash,
        Guid resourceId,
        int responseCode,
        AccountSummary response,
        string? responseLocation,
        DateTimeOffset createdAt) => IdempotencyProtocol.Completed(
            scope,
            key,
            requestHash,
            "APP_USER",
            resourceId,
            responseCode,
            response,
            createdAt,
            DateTimeOffset.MaxValue,
            responseLocation: responseLocation);

    private static string CreateScope(Guid actorUserId) => $"users:create:{actorUserId:D}";

    private static string StatusScope(Guid actorUserId, string operation, Guid userId) =>
        $"users:{operation}:{actorUserId:D}:{userId:D}";

    private static string RequireValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AccountValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string RequireTemporaryPassword(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 14)
        {
            throw new AccountValidationException("TemporaryPassword does not meet the approved minimum length.");
        }

        return value;
    }

    private static string NewTemporaryPassword() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string ComputeHash(params string[] parts)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', parts));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string NewSecurityStamp() => Convert.ToHexString(Guid.NewGuid().ToByteArray());

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
