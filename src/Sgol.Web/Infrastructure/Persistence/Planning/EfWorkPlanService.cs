using System.Data;
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
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class EfWorkPlanService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IWorkPlanService
{
    private const string ScopePrefix = "planning:work-plan:ensure";
    private const string PlanResource = "WORK_PLAN";
    private const string BranchResource = "BRANCH";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";
    private const int MaximumAttempts = 3;

    public async Task<WorkPlanEnsureResult> EnsureAsync(
        EnsureWorkPlanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorUserId == Guid.Empty || command.IdempotencyKey == Guid.Empty ||
            command.CorrelationId == Guid.Empty || command.BranchId != BranchScope.LorettaId)
        {
            throw new ArgumentException("The work-plan command is invalid.", nameof(command));
        }

        var resource = Resource(command);
        var scope = IdempotencyProtocol.Scope(
            command.ActorUserId.ToString("D"), "WORK_PLAN_ENSURE", resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            "WORK_PLAN_ENSURE",
            command.ActorUserId.ToString("D"),
            resource,
            new { command.BranchId, command.IsoYear, command.IsoWeek });
        var legacyScope = LegacyScope(command.ActorUserId, command.BranchId);
        var legacyRequestHash = LegacyHash(command);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            WorkPlanEnsureResult? result = null;
            WorkPlanException? rejection = null;
            try
            {
                await auditTransaction.ExecuteAsync(
                    IsolationLevel.ReadCommitted,
                    async token =>
                    {
                        var branch = await dbContext.Branches
                            .FromSqlInterpolated($"SELECT * FROM branch WHERE id = {command.BranchId} FOR UPDATE")
                            .AsNoTracking()
                            .SingleOrDefaultAsync(token);
                        if (branch is null || branch.Code != BranchScope.LorettaCode ||
                            branch.Status != BranchScope.ActiveStatus)
                        {
                            throw new WorkPlanConflictException();
                        }

                        var actor = await LockActorAsync(command.ActorUserId, token);
                        var ensuredAt = clock.UtcNow;
                        if (!actor.Exists)
                        {
                            throw new WorkPlanAccessDeniedException();
                        }

                        if (!IsAuthorized(actor, ensuredAt))
                        {
                            rejection = new WorkPlanAccessDeniedException();
                            return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                        }

                        var replay = await LockIdempotencyAsync(
                            scope, legacyScope, command.IdempotencyKey, token);

                        if (replay is not null)
                        {
                            var expectedHash = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                ? requestHash
                                : legacyRequestHash;
                            if (!string.Equals(replay.RequestHash, expectedHash, StringComparison.Ordinal))
                            {
                                rejection = new WorkPlanIdempotencyConflictException();
                                return IdempotencyConflictAudit(command, ensuredAt);
                            }

                            if (replay.ResourceType == BranchResource)
                            {
                                rejection = replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                                    ? ExceptionFor(
                                        IdempotencyProtocol.ReadPayload<RejectionSnapshot>(replay).ErrorCode,
                                        replay.ResponseCode)
                                    : await ReplayRejectionAsync(command, replay.ResponseCode, token);
                                return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                            }

                            if (replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
                            {
                                result = IdempotencyProtocol.ReadPayload<WorkPlanEnsureResult>(replay)
                                    with
                                { Result = WorkPlanResults.Recovered };
                                return RecoveredAudit(command, result, ensuredAt);
                            }

                            var replayedPlan = await LockPlanByIdAsync(replay.ResourceId, token);
                            if (replayedPlan is null)
                            {
                                rejection = new WorkPlanConflictException();
                                return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                            }

                            var replayedPeriod = await dbContext.WeekPeriods.AsNoTracking()
                                .SingleAsync(item => item.Id == replayedPlan.PeriodId, token);
                            result = Result(replayedPlan, replayedPeriod, WorkPlanResults.Recovered);
                            return RecoveredAudit(command, result, ensuredAt);
                        }

                        WeekRange expectedRange;
                        try
                        {
                            expectedRange = WeekContract.Calculate(command.IsoYear, command.IsoWeek);
                        }
                        catch (WeekValidationException)
                        {
                            rejection = new WorkPlanIsoWeekInvalidException();
                            return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                        }

                        var period = await dbContext.WeekPeriods
                            .FromSqlInterpolated($"SELECT * FROM week_period WHERE branch_id = {command.BranchId} AND iso_year = {command.IsoYear} AND iso_week = {command.IsoWeek} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);
                        if (period is null)
                        {
                            rejection = new WorkPlanPeriodNotFoundException();
                            return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                        }

                        if (period.BranchId != command.BranchId || period.IsoYear != command.IsoYear ||
                            period.IsoWeek != command.IsoWeek || period.StartsOn != expectedRange.StartsOn ||
                            period.EndsOn != expectedRange.EndsOn)
                        {
                            rejection = new WorkPlanPeriodIncompatibleException();
                            return RejectedAudit(command, rejection.ErrorCode, ensuredAt);
                        }

                        var plan = await dbContext.WorkPlans
                            .FromSqlInterpolated($"SELECT * FROM work_plan WHERE branch_id = {command.BranchId} AND period_id = {period.Id} FOR UPDATE")
                            .AsTracking()
                            .SingleOrDefaultAsync(token);
                        var created = plan is null;
                        if (created)
                        {
                            plan = new WorkPlan(uuidGenerator.NewUuid(), command.BranchId, period.Id);
                            dbContext.WorkPlans.Add(plan);
                        }

                        result = Result(
                            plan!,
                            period,
                            created ? WorkPlanResults.Created : WorkPlanResults.Recovered);
                        AddSuccessIdempotency(scope, command, requestHash, result, ensuredAt);
                        return created
                            ? CreatedAudit(command, result, ensuredAt)
                            : RecoveredAudit(command, result, ensuredAt);
                    },
                    cancellationToken);

                dbContext.ChangeTracker.Clear();
                if (rejection is not null)
                {
                    throw rejection;
                }

                return result ?? throw new InvalidOperationException("The work-plan transaction produced no result.");
            }
            catch (Exception exception) when (rejection is WorkPlanIdempotencyConflictException &&
                exception is not WorkPlanIdempotencyConflictException)
            {
                dbContext.ChangeTracker.Clear();
                throw new IdempotencyConflictAuditException(exception);
            }
            catch (WorkPlanException)
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
                await IdempotencyProtocol.DelayBeforeRetryAsync(
                    "WORK_PLAN_ENSURE", exception, attempt, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                throw new WorkPlanConcurrencyConflictException();
            }
            catch (DbUpdateException exception) when (GetConstraintName(exception) is not null)
            {
                dbContext.ChangeTracker.Clear();
                throw new WorkPlanConflictException();
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        throw new WorkPlanConcurrencyConflictException();
    }

    private async Task<ActorState> LockActorAsync(Guid actorUserId, CancellationToken token)
    {
        var user = await dbContext.AppUsers
            .FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {actorUserId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(token);
        if (user is null)
        {
            return ActorState.Missing;
        }

        var employments = await dbContext.EmploymentVersions
            .FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {user.PersonId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(token);
        var roles = await dbContext.RoleAssignmentVersions
            .FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {user.Id} AND branch_id = {BranchScope.LorettaId} AND valid_to IS NULL FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(token);
        return new ActorState(user, employments, roles);
    }

    private static bool IsAuthorized(ActorState actor, DateTimeOffset at) =>
        actor.User is { Status: AccountStatus.Active } &&
        actor.Employments.Count == 1 &&
        actor.Employments[0].BranchId == BranchScope.LorettaId &&
        actor.Employments[0].Status == EmploymentStatus.Active &&
        actor.Employments[0].ValidFrom <= at &&
        actor.Employments[0].ValidTo is null &&
        actor.Roles.Count == 1 &&
        actor.Roles[0].Status == RoleAssignmentStatus.Active &&
        actor.Roles[0].ValidFrom <= at &&
        actor.Roles[0].ValidTo is null &&
        CanonicalRole.IsDefined(actor.Roles[0].RoleCode);

    private Task<WorkPlan?> LockPlanByIdAsync(Guid planId, CancellationToken token) =>
        dbContext.WorkPlans
            .FromSqlInterpolated($"SELECT * FROM work_plan WHERE id = {planId} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(token);

    private async Task<IdempotencyRecord?> LockIdempotencyAsync(
        string scope,
        string legacyScope,
        Guid key,
        CancellationToken token)
    {
        var record = await dbContext.IdempotencyRecords
            .FromSqlInterpolated($"SELECT * FROM idempotency_record WHERE scope = {scope} AND key = {key} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(token);
        return record ?? await dbContext.IdempotencyRecords
            .FromSqlInterpolated($"SELECT * FROM idempotency_record WHERE scope = {legacyScope} AND key = {key} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(token);
    }

    private async Task<WorkPlanException> ReplayRejectionAsync(
        EnsureWorkPlanCommand command,
        int responseCode,
        CancellationToken token)
    {
        var requestId = command.IdempotencyKey.ToString("D");
        var audit = await dbContext.AuditEvents.AsNoTracking()
            .Where(item => item.Action == "WORK_PLAN_ENSURE_REJECTED" && item.RequestId == requestId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(token);
        var errorCode = audit?.AfterData?.RootElement.TryGetProperty("errorCode", out var value) == true
            ? value.GetString()
            : null;
        return ExceptionFor(errorCode, responseCode);
    }

    private void AddSuccessIdempotency(
        string scope,
        EnsureWorkPlanCommand command,
        string requestHash,
        WorkPlanEnsureResult result,
        DateTimeOffset at) =>
        dbContext.IdempotencyRecords.Add(NewIdempotency(
            scope,
            command,
            requestHash,
            PlanResource,
            result.PlanId,
            result.Result == WorkPlanResults.Created ? 201 : 200,
            result,
            at));

    private static IdempotencyRecord NewIdempotency(
        string scope,
        EnsureWorkPlanCommand command,
        string requestHash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        object responsePayload,
        DateTimeOffset at) => IdempotencyProtocol.Completed(
            scope,
            command.IdempotencyKey,
            requestHash,
            resourceType,
            resourceId,
            responseCode,
            responsePayload,
            at,
            DateTimeOffset.MaxValue);

    private AuditEvent CreatedAudit(
        EnsureWorkPlanCommand command,
        WorkPlanEnsureResult result,
        DateTimeOffset at) => NewAudit(
            command,
            result.PlanId,
            "WORK_PLAN_ENSURED",
            WorkPlanResults.Created,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                planId = result.PlanId,
                branchId = result.BranchId,
                periodId = result.PeriodId,
                isoYear = result.IsoYear,
                isoWeek = result.IsoWeek,
                status = result.Status,
                rowVersion = result.RowVersion,
            }, JsonSerializerOptions.Web),
            at);

    private AuditEvent RecoveredAudit(
        EnsureWorkPlanCommand command,
        WorkPlanEnsureResult result,
        DateTimeOffset at) => NewAudit(
            command,
            result.PlanId,
            "WORK_PLAN_RECOVERED",
            WorkPlanResults.Recovered,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                planId = result.PlanId,
                branchId = result.BranchId,
                periodId = result.PeriodId,
                isoYear = result.IsoYear,
                isoWeek = result.IsoWeek,
                status = result.Status,
                rowVersion = result.RowVersion,
                result = WorkPlanResults.Recovered,
            }, JsonSerializerOptions.Web),
            at);

    private AuditEvent RejectedAudit(
        EnsureWorkPlanCommand command,
        string errorCode,
        DateTimeOffset at) => NewAudit(
            command,
            resourceId: null,
            "WORK_PLAN_ENSURE_REJECTED",
            errorCode,
            JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                branchId = command.BranchId,
                isoYear = command.IsoYear,
                isoWeek = command.IsoWeek,
                errorCode,
            }, JsonSerializerOptions.Web),
            at);

    private AuditEvent IdempotencyConflictAudit(
        EnsureWorkPlanCommand command,
        DateTimeOffset at) => IdempotencyProtocol.ConflictAudit(
            uuidGenerator.NewUuid(), at, command.ActorUserId, PlanResource, BranchScope.LorettaId,
            command.BranchId, command.CorrelationId, command.IdempotencyKey, "WORK_PLAN_ENSURE");

    private AuditEvent NewAudit(
        EnsureWorkPlanCommand command,
        Guid? resourceId,
        string action,
        string outcome,
        JsonDocument afterData,
        DateTimeOffset at) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = command.ActorUserId,
            ActorType = "USER",
            Action = action,
            ResourceType = PlanResource,
            ResourceId = resourceId,
            BranchId = command.BranchId,
            CorrelationId = command.CorrelationId,
            RequestId = command.IdempotencyKey.ToString("D"),
            AfterData = afterData,
            Outcome = outcome,
        };

    private static WorkPlanEnsureResult Result(WorkPlan plan, WeekPeriod period, string result) => new(
        result,
        plan.Id,
        plan.BranchId,
        BranchScope.LorettaCode,
        plan.PeriodId,
        period.IsoYear,
        period.IsoWeek,
        plan.Status,
        plan.RowVersion);

    private static string Resource(EnsureWorkPlanCommand command) =>
        $"{BranchScope.LorettaCode}:{command.IsoYear.ToString(CultureInfo.InvariantCulture)}:{command.IsoWeek.ToString(CultureInfo.InvariantCulture)}";

    private static string LegacyScope(Guid actorUserId, Guid branchId) =>
        $"{ScopePrefix}:{actorUserId:D}:{branchId:D}";

    private static string LegacyHash(EnsureWorkPlanCommand command)
    {
        var canonical = string.Join('\n',
            "WORK_PLAN_ENSURE",
            command.BranchId.ToString("D"),
            command.IsoYear.ToString(CultureInfo.InvariantCulture),
            command.IsoWeek.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record RejectionSnapshot(string ErrorCode);

    private static WorkPlanException ExceptionFor(string? errorCode, int responseCode) => errorCode switch
    {
        "ACCESO_DENEGADO" => new WorkPlanAccessDeniedException(),
        "SEMANA_ISO_INVALIDA" => new WorkPlanIsoWeekInvalidException(),
        "PERIODO_NO_ENCONTRADO" => new WorkPlanPeriodNotFoundException(),
        "PERIODO_INCOMPATIBLE" => new WorkPlanPeriodIncompatibleException(),
        "WORK_PLAN_CONCURRENCY_CONFLICT" => new WorkPlanConcurrencyConflictException(),
        _ when responseCode == 403 => new WorkPlanAccessDeniedException(),
        _ when responseCode == 404 => new WorkPlanPeriodNotFoundException(),
        _ when responseCode == 422 => new WorkPlanIsoWeekInvalidException(),
        _ => new WorkPlanConflictException(),
    };

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure ||
            postgres?.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName is WorkPlanConfiguration.UniqueBranchPeriodIndex or IdempotencyPrimaryKey;
    }

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;

    private sealed record ActorState(
        AppUser? User,
        IReadOnlyList<EmploymentVersion> Employments,
        IReadOnlyList<RoleAssignmentVersion> Roles)
    {
        public static ActorState Missing { get; } = new(null, [], []);

        public bool Exists => User is not null;
    }
}
