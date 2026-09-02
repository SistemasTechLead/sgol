using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Organization;

public sealed class EfPersonAdministrationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IPersonAdministrationService
{
    private const string CreateScope = "people:create";
    private const string EmploymentScope = "people:employment";
    private const string PersonCodeIndex = "IX_person_stable_code";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const string CurrentEmploymentIndex = "IX_employment_version_person_id_branch_id";
    private const string EmploymentSuccessorIndex = "IX_employment_version_supersedes_id";

    public async Task<IReadOnlyList<PersonSummary>> ListAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);

        return await dbContext.People
            .AsNoTracking()
            .OrderBy(person => person.StableCode)
            .Select(person => new PersonSummary(person.Id, person.StableCode, person.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonDetails?> FindAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);
        return await LoadDetailsAsync(personId, cancellationToken);
    }

    public async Task<PersonMutationResult> CreateAsync(
        CreatePersonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);

        var stableCode = RequireValue(command.StableCode, "stableCode");
        var displayName = RequireValue(command.DisplayName, "displayName");
        var requestHash = ComputeHash(stableCode, displayName);
        var replay = await FindReplayAsync(CreateScope, command.IdempotencyKey, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var now = clock.UtcNow;
        var personId = uuidGenerator.NewUuid();
        var employmentId = uuidGenerator.NewUuid();
        var person = new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = displayName,
            CreatedAt = now,
        };
        var employment = new EmploymentVersion(
            employmentId,
            personId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            now);

        using var afterData = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            personId,
            employmentVersionId = employmentId,
            employmentStatus = EmploymentStatus.Active,
        });

        var auditEvent = NewAuditEvent(
            command.ActorUserId,
            command.CorrelationId,
            personId,
            "PERSON_CREATED",
            afterData: afterData);

        try
        {
            await auditTransaction.ExecuteAsync(
                auditEvent,
                _ =>
                {
                    dbContext.People.Add(person);
                    dbContext.EmploymentVersions.Add(employment);
                    dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                        CreateScope,
                        command.IdempotencyKey,
                        requestHash,
                        personId,
                        StatusCodes.Status201Created,
                        now));
                    return Task.CompletedTask;
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(CreateScope, command.IdempotencyKey, requestHash, cancellationToken)
                ?? throw new PersonIdempotencyConflictException();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == PersonCodeIndex)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await FindReplayAsync(
                CreateScope,
                command.IdempotencyKey,
                requestHash,
                cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw new PersonCodeConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var createdPerson = (await LoadDetailsAsync(personId, cancellationToken))!;
        dbContext.ChangeTracker.Clear();
        return new PersonMutationResult(createdPerson, Replayed: false);
    }

    public async Task<PersonMutationResult> ChangeEmploymentAsync(
        ChangeEmploymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        var reason = RequireValue(command.Reason, "reason");
        if (command.Status is not EmploymentStatus.Active and not EmploymentStatus.Inactive)
        {
            throw new PersonValidationException("status must be ACTIVA or INACTIVA.");
        }

        var updatesLaborData = command.PositionText is not null || command.ShiftText is not null;
        var positionText = command.PositionText is null ? null : RequireValue(command.PositionText, "positionText");
        var shiftText = command.ShiftText is null ? null : RequireValue(command.ShiftText, "shiftText");

        var requestHash = ComputeHash(
            command.PersonId.ToString("D", CultureInfo.InvariantCulture),
            command.Status,
            command.ExpectedRowVersion.ToString(CultureInfo.InvariantCulture),
            reason,
            command.PositionText is null ? "0" : "1",
            positionText ?? string.Empty,
            command.ShiftText is null ? "0" : "1",
            shiftText ?? string.Empty);
        if (command.IdempotencyKey is Guid idempotencyKey)
        {
            var replay = await FindReplayAsync(EmploymentScope, idempotencyKey, requestHash, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        var current = await dbContext.EmploymentVersions
            .SingleOrDefaultAsync(
                employment => employment.PersonId == command.PersonId &&
                    employment.BranchId == BranchScope.LorettaId &&
                    employment.ValidTo == null,
                cancellationToken);
        if (current is null)
        {
            var personExists = await dbContext.People
                .AsNoTracking()
                .AnyAsync(person => person.Id == command.PersonId, cancellationToken);
            dbContext.ChangeTracker.Clear();
            throw personExists ? new PersonVersionConflictException() : new PersonNotFoundException();
        }

        if (current.RowVersion != command.ExpectedRowVersion)
        {
            dbContext.ChangeTracker.Clear();
            throw new PersonVersionConflictException();
        }

        if (!updatesLaborData && current.Status == command.Status)
        {
            dbContext.ChangeTracker.Clear();
            throw new PersonStateConflictException();
        }

        var effectivePositionText = positionText ?? current.PositionText;
        var effectiveShiftText = shiftText ?? current.ShiftText;
        if (updatesLaborData &&
            current.Status == command.Status &&
            string.Equals(current.PositionText, effectivePositionText, StringComparison.Ordinal) &&
            string.Equals(current.ShiftText, effectiveShiftText, StringComparison.Ordinal))
        {
            dbContext.ChangeTracker.Clear();
            throw new PersonEmploymentNoChangeException();
        }

        var now = clock.UtcNow;
        var successor = updatesLaborData
            ? current.CreateSuccessor(
                uuidGenerator.NewUuid(),
                command.Status,
                effectivePositionText,
                effectiveShiftText,
                now)
            : current.CreateSuccessor(uuidGenerator.NewUuid(), command.Status, now);
        using var beforeData = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            employmentVersionId = current.Id,
            employmentStatus = current.Status,
            positionText = current.PositionText,
            shiftText = current.ShiftText,
            rowVersion = command.ExpectedRowVersion,
        });
        using var afterData = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            employmentVersionId = successor.Id,
            employmentStatus = successor.Status,
            positionText = successor.PositionText,
            shiftText = successor.ShiftText,
            rowVersion = successor.RowVersion,
            supersedesId = successor.SupersedesId,
        });
        var auditEvent = NewAuditEvent(
            command.ActorUserId,
            command.CorrelationId,
            command.PersonId,
            "PERSON_EMPLOYMENT_CHANGED",
            beforeData,
            afterData,
            reason);

        try
        {
            await auditTransaction.ExecuteAsync(
                auditEvent,
                _ =>
                {
                    dbContext.EmploymentVersions.Add(successor);
                    if (command.IdempotencyKey is Guid key)
                    {
                        dbContext.IdempotencyRecords.Add(NewIdempotencyRecord(
                            EmploymentScope,
                            key,
                            requestHash,
                            command.PersonId,
                            StatusCodes.Status200OK,
                            now));
                    }

                    return Task.CompletedTask;
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new PersonVersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is CurrentEmploymentIndex or EmploymentSuccessorIndex)
        {
            dbContext.ChangeTracker.Clear();
            if (command.IdempotencyKey is Guid key)
            {
                var concurrentReplay = await FindReplayAsync(
                    EmploymentScope,
                    key,
                    requestHash,
                    cancellationToken);
                if (concurrentReplay is not null)
                {
                    return concurrentReplay;
                }
            }

            throw new PersonVersionConflictException();
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            var key = command.IdempotencyKey!.Value;
            return await FindReplayAsync(EmploymentScope, key, requestHash, cancellationToken)
                ?? throw new PersonIdempotencyConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var changedPerson = (await LoadDetailsAsync(command.PersonId, cancellationToken))!;
        dbContext.ChangeTracker.Clear();
        return new PersonMutationResult(changedPerson, Replayed: false);
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
                user.Status == BootstrapContract.ActiveAccountStatus &&
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

        var deniedEvent = NewAuditEvent(
            actorUserId,
            correlationId,
            resourceId: null,
            action: "PERSON_ACCESS_DENIED",
            outcome: "DENIED");
        await auditTransaction.ExecuteAsync(
            deniedEvent,
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new PersonAccessDeniedException();
    }

    private async Task<PersonMutationResult?> FindReplayAsync(
        string scope,
        Guid key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new PersonIdempotencyConflictException();
        }

        var person = await LoadDetailsAsync(record.ResourceId, cancellationToken)
            ?? throw new PersonNotFoundException();
        dbContext.ChangeTracker.Clear();
        return new PersonMutationResult(person, Replayed: true);
    }

    private async Task<PersonDetails?> LoadDetailsAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await dbContext.People
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == personId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        var history = await dbContext.EmploymentVersions
            .AsNoTracking()
            .Where(employment => employment.PersonId == personId && employment.BranchId == BranchScope.LorettaId)
            .OrderBy(employment => employment.RowVersion)
            .ThenBy(employment => employment.ValidTo == null)
            .ThenBy(employment => employment.ValidFrom)
            .ThenBy(employment => employment.Id)
            .Select(employment => new EmploymentVersionSnapshot(
                employment.Id,
                employment.Status,
                employment.ValidFrom,
                employment.ValidTo,
                employment.SupersedesId,
                employment.RowVersion,
                employment.PositionText,
                employment.ShiftText))
            .ToListAsync(cancellationToken);

        return new PersonDetails(
            person.Id,
            person.StableCode,
            person.DisplayName,
            person.CreatedAt,
            history);
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
            ResourceType = "PERSON",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = reason,
            Outcome = outcome,
        };

    private static IdempotencyRecord NewIdempotencyRecord(
        string scope,
        Guid key,
        string requestHash,
        Guid resourceId,
        int responseCode,
        DateTimeOffset createdAt) => new()
        {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            Status = "COMPLETED",
            ResourceType = "PERSON",
            ResourceId = resourceId,
            ResponseCode = responseCode,
            CreatedAt = createdAt,
            ExpiresAt = DateTimeOffset.MaxValue,
        };

    private static string RequireValue(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PersonValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string ComputeHash(params string[] parts)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', parts));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
