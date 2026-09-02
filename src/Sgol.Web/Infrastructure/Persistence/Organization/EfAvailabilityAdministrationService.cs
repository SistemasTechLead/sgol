using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Organization;

public sealed class EfAvailabilityAdministrationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IAvailabilityAdministrationService
{
    public const int MaximumQueryDays = 100;

    private const string CurrentAvailabilityIndex =
        "IX_availability_day_version_person_id_branch_id_local_date";
    private const string AvailabilitySuccessorIndex =
        "IX_availability_day_version_supersedes_id";

    public async Task<IReadOnlyList<AvailabilityDaySnapshot>> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid personId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(fromDate, toDate);
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);
        await EnsureActiveLorettaPersonAsync(personId, cancellationToken);

        var values = await dbContext.AvailabilityDayVersions
            .AsNoTracking()
            .Where(availability =>
                availability.PersonId == personId &&
                availability.BranchId == BranchScope.LorettaId &&
                availability.Status == AvailabilityVersionStatus.Current &&
                availability.LocalDate >= fromDate &&
                availability.LocalDate <= toDate)
            .OrderBy(availability => availability.LocalDate)
            .Select(availability => new AvailabilityDaySnapshot(
                availability.Id,
                availability.PersonId,
                availability.LocalDate,
                availability.IsAvailable,
                availability.RowVersion))
            .ToListAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return values;
    }

    public async Task<AvailabilityDaySnapshot> PutAsync(
        PutAvailabilityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, command.CorrelationId, cancellationToken);
        await EnsureActiveLorettaPersonAsync(command.PersonId, cancellationToken);

        AvailabilityDayVersion? saved = null;
        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    await EnsureActiveLorettaPersonLockedAsync(command.PersonId, token);
                    var currentRows = await dbContext.AvailabilityDayVersions
                        .FromSqlInterpolated(
                            $"""
                            SELECT *
                            FROM availability_day_version
                            WHERE person_id = {command.PersonId}
                              AND branch_id = {BranchScope.LorettaId}
                              AND local_date = {command.LocalDate}
                              AND status = {AvailabilityVersionStatus.Current}
                            FOR UPDATE
                            """)
                        .AsTracking()
                        .ToListAsync(token);
                    var current = currentRows.SingleOrDefault();

                    if (current is null && command.ExpectedRowVersion is not null)
                    {
                        throw new AvailabilityVersionConflictException();
                    }

                    if (current is not null && command.ExpectedRowVersion is null)
                    {
                        throw new AvailabilityIfMatchRequiredException();
                    }

                    if (current is not null && current.RowVersion != command.ExpectedRowVersion)
                    {
                        throw new AvailabilityVersionConflictException();
                    }

                    JsonDocument? beforeData = null;
                    if (current is null)
                    {
                        saved = new AvailabilityDayVersion(
                            uuidGenerator.NewUuid(),
                            command.PersonId,
                            BranchScope.LorettaId,
                            command.LocalDate,
                            command.IsAvailable,
                            command.ActorUserId);
                    }
                    else
                    {
                        beforeData = SerializeAuditValue(current);
                        saved = current.CreateSuccessor(
                            uuidGenerator.NewUuid(),
                            command.IsAvailable,
                            command.ActorUserId);
                    }

                    dbContext.AvailabilityDayVersions.Add(saved);
                    var afterData = SerializeAuditValue(saved);
                    return new AuditEvent
                    {
                        Id = uuidGenerator.NewUuid(),
                        OccurredAt = clock.UtcNow,
                        ActorUserId = command.ActorUserId,
                        ActorType = "APP_USER",
                        Action = current is null ? "AVAILABILITY_CREATED" : "AVAILABILITY_CORRECTED",
                        ResourceType = "AVAILABILITY_DAY",
                        ResourceId = saved.Id,
                        BranchId = BranchScope.LorettaId,
                        CorrelationId = command.CorrelationId,
                        BeforeData = beforeData,
                        AfterData = afterData,
                        Outcome = "SUCCESS",
                    };
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new AvailabilityVersionConflictException();
        }
        catch (DbUpdateException exception) when (
            GetConstraintName(exception) is CurrentAvailabilityIndex or AvailabilitySuccessorIndex)
        {
            dbContext.ChangeTracker.Clear();
            throw new AvailabilityVersionConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToSnapshot(saved!);
        dbContext.ChangeTracker.Clear();
        return result;
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

        await auditTransaction.ExecuteAsync(
            new AuditEvent
            {
                Id = uuidGenerator.NewUuid(),
                OccurredAt = clock.UtcNow,
                ActorUserId = actorUserId,
                ActorType = "APP_USER",
                Action = "AVAILABILITY_ACCESS_DENIED",
                ResourceType = "AVAILABILITY_DAY",
                BranchId = BranchScope.LorettaId,
                CorrelationId = correlationId,
                Outcome = "DENIED",
            },
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new AvailabilityAccessDeniedException();
    }

    private async Task EnsureActiveLorettaPersonAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
        {
            throw new AvailabilityPersonNotFoundException();
        }

        var currentEmployments = await dbContext.EmploymentVersions
            .AsNoTracking()
            .Where(employment => employment.PersonId == personId && employment.ValidTo == null)
            .ToListAsync(cancellationToken);
        EnsureActiveLorettaEmployment(currentEmployments);
    }

    private async Task EnsureActiveLorettaPersonLockedAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        var currentEmployments = await dbContext.EmploymentVersions
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM employment_version
                WHERE person_id = {personId}
                  AND valid_to IS NULL
                FOR SHARE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        EnsureActiveLorettaEmployment(currentEmployments);
    }

    private static void EnsureActiveLorettaEmployment(List<EmploymentVersion> currentEmployments)
    {
        var lorettaEmployment = currentEmployments.SingleOrDefault(
            employment => employment.BranchId == BranchScope.LorettaId);
        if (lorettaEmployment is null)
        {
            if (currentEmployments.Count > 0)
            {
                throw new AvailabilityPersonOutOfScopeException();
            }

            throw new AvailabilityPersonInactiveException();
        }

        if (lorettaEmployment.Status != EmploymentStatus.Active)
        {
            throw new AvailabilityPersonInactiveException();
        }
    }

    private static void ValidateRange(DateOnly fromDate, DateOnly toDate)
    {
        if (toDate < fromDate)
        {
            throw new AvailabilityValidationException("toDate must be on or after fromDate.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > MaximumQueryDays)
        {
            throw new AvailabilityValidationException(
                $"The availability range cannot exceed {MaximumQueryDays} inclusive days.");
        }
    }

    private static JsonDocument SerializeAuditValue(AvailabilityDayVersion value) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            availabilityVersionId = value.Id,
            personId = value.PersonId,
            branchId = value.BranchId,
            localDate = value.LocalDate,
            isAvailable = value.IsAvailable,
            status = value.Status,
            supersedesId = value.SupersedesId,
            changedBy = value.ChangedBy,
            rowVersion = value.RowVersion,
        });

    private static AvailabilityDaySnapshot ToSnapshot(AvailabilityDayVersion value) => new(
        value.Id,
        value.PersonId,
        value.LocalDate,
        value.IsAvailable,
        value.RowVersion);

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
