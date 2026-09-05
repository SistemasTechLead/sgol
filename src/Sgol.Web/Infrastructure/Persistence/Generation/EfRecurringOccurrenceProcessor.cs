using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public interface IRecurringOccurrenceProcessor
{
    Task<RecurringOccurrenceResult?> ProcessAsync(
        RecurringWindow window,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class EfRecurringOccurrenceProcessor(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IWorkObligationMaterializer obligationMaterializer,
    IEligibilityEvaluationService eligibilityService,
    IAutomaticAssignmentService assignmentService,
    IClock clock,
    IUuidGenerator uuidGenerator,
    TimeZoneInfo operationalTimeZone) : IRecurringOccurrenceProcessor
{
    private const string GenerationScope = "generation-request:create:system:recurrence";
    private const string PlanScope = "planning:work-plan:ensure:system";
    private static readonly Guid TaskDefinitionId =
        TaskDefinitionCatalog.Require(RecurringGenerationContract.TaskCode).Id;

    public async Task<RecurringOccurrenceResult?> ProcessAsync(
        RecurringWindow window,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.ActivationRuleVersions.AsNoTracking()
            .Where(rule =>
                rule.TaskDefinitionId == TaskDefinitionId &&
                rule.EffectiveFrom != null &&
                rule.EffectiveFrom <= window.OccurrenceInstant &&
                (rule.EffectiveTo == null || window.OccurrenceInstant < rule.EffectiveTo))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return null;
        }

        if (rules.Count != 1 || !IsValidRule(rules[0]))
        {
            return await RecordNonGeneratingResultAsync(
                window,
                rules.FirstOrDefault()?.Id,
                RecurringGenerationContract.Rejected,
                "RECURRENCE_RULE_INVALID",
                correlationId,
                cancellationToken);
        }

        var rule = rules[0];
        var taskVersionCount = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .CountAsync(version =>
                version.Id == rule.TaskDefinitionVersionId &&
                version.TaskDefinitionId == TaskDefinitionId &&
                version.Status != TaskDefinitionStatuses.InactiveForNew &&
                version.EffectiveFrom != null &&
                version.EffectiveFrom <= window.OccurrenceInstant &&
                (version.EffectiveTo == null || window.OccurrenceInstant < version.EffectiveTo),
                cancellationToken);
        if (taskVersionCount != 1)
        {
            return await RecordNonGeneratingResultAsync(
                window,
                rule.Id,
                RecurringGenerationContract.Rejected,
                "RECURRENCE_RULE_INVALID",
                correlationId,
                cancellationToken);
        }

        var calendar = await dbContext.CalendarDayVersions.AsNoTracking()
            .Where(day =>
                day.BranchId == BranchScope.LorettaId &&
                day.LocalDate == window.LocalDate &&
                day.EffectiveFrom != null &&
                day.EffectiveFrom <= window.OccurrenceInstant &&
                (day.EffectiveTo == null || window.OccurrenceInstant < day.EffectiveTo))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (calendar.Count != 1)
        {
            return await RecordNonGeneratingResultAsync(
                window,
                rule.Id,
                RecurringGenerationContract.Rejected,
                "RECURRENCE_CALENDAR_NOT_CONFIGURED",
                correlationId,
                cancellationToken);
        }

        if (!calendar[0].IsWorkingDay)
        {
            return await RecordNonGeneratingResultAsync(
                window,
                rule.Id,
                RecurringGenerationContract.Omitted,
                errorCode: null,
                correlationId,
                cancellationToken);
        }

        var period = await EnsurePeriodAsync(window, correlationId, cancellationToken);
        var identity = RecurringGenerationContract.Identity(
            rule.Id,
            BranchScope.LorettaId,
            period.Id,
            window.LocalDate,
            window.Window);
        var requestResult = await EnsureGenerationRequestAsync(
            rule.Id,
            period.Id,
            window,
            identity,
            correlationId,
            cancellationToken);
        var hadObligation = requestResult.Request.ObligationId is not null;
        var obligation = await obligationMaterializer.MaterializeAsync(
            new MaterializeWorkObligationCommand(requestResult.Request.Id, correlationId),
            cancellationToken);

        var evaluationRequestId = RecurringGenerationContract.PurposeId(
            "SGOL_RECURRENCE_ELIGIBILITY_V1",
            identity.IdempotencyKey.ToString("N", CultureInfo.InvariantCulture));
        var eligibility = await eligibilityService.EvaluateAsync(
            new EvaluateEligibilityCommand(
                evaluationRequestId,
                correlationId,
                obligation.ObligationId,
                window.LocalDate,
                EligibilityDateSources.ScheduledOccurrence),
            cancellationToken);

        var assignmentCreated = false;
        var assignmentRecovered = true;
        if (eligibility.Result == EligibilityResults.EligibleCandidates)
        {
            var assignmentRequestId = RecurringGenerationContract.PurposeId(
                "SGOL_RECURRENCE_ASSIGNMENT_V1",
                identity.IdempotencyKey.ToString("N", CultureInfo.InvariantCulture));
            var assignment = await assignmentService.AssignAsync(
                new AssignObligationCommand(
                    assignmentRequestId,
                    obligation.ObligationId,
                    eligibility.EvaluationId,
                    correlationId),
                cancellationToken);
            assignmentCreated = assignment.Result == AutomaticAssignmentResults.Created;
            assignmentRecovered = assignment.Result == AutomaticAssignmentResults.Recovered;
        }

        var planCreated = await EnsurePlanAsync(period, identity, correlationId, cancellationToken);
        var created = requestResult.Created || !hadObligation || !eligibility.Replayed ||
            assignmentCreated || planCreated;
        if (!assignmentRecovered && !assignmentCreated &&
            eligibility.Result == EligibilityResults.EligibleCandidates)
        {
            throw new InvalidOperationException("Automatic assignment returned an unsupported result.");
        }

        var result = created
            ? RecurringGenerationContract.Generated
            : RecurringGenerationContract.Recovered;
        await RecordOccurrenceAuditAsync(
            window,
            rule.Id,
            period.Id,
            obligation.ObligationId,
            result,
            errorCode: null,
            correlationId,
            cancellationToken);
        return new RecurringOccurrenceResult(
            result,
            rule.Id,
            period.Id,
            obligation.ObligationId,
            null,
            created);
    }

    private static bool IsValidRule(ActivationRuleVersion rule)
    {
        if (rule.Mode != ActivationModes.Recurring ||
            rule.OriginKeySchema != RecurringGenerationContract.OriginType)
        {
            return false;
        }

        try
        {
            _ = ActivationPolicyCatalog.Validate(
                RecurringGenerationContract.TaskCode,
                rule.Mode,
                rule.Schedule.RootElement,
                rule.OriginKeySchema);
            return true;
        }
        catch (ActivationPolicyValidationException)
        {
            return false;
        }
    }

    private async Task<WeekPeriod> EnsurePeriodAsync(
        RecurringWindow window,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var isoYear = ISOWeek.GetYear(window.LocalDate.ToDateTime(TimeOnly.MinValue));
        var isoWeek = ISOWeek.GetWeekOfYear(window.LocalDate.ToDateTime(TimeOnly.MinValue));
        var existing = await FindPeriodAsync(isoYear, isoWeek, cancellationToken);
        if (existing is not null)
        {
            return ValidatePeriod(existing, isoYear, isoWeek);
        }

        var range = WeekContract.Calculate(isoYear, isoWeek);
        var now = clock.UtcNow;
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, operationalTimeZone).DateTime);
        var period = new WeekPeriod(
            uuidGenerator.NewUuid(),
            BranchScope.LorettaId,
            isoYear,
            isoWeek,
            range.StartsOn,
            range.EndsOn,
            WeekContract.DeriveStatus(range.EndsOn, localToday));
        try
        {
            await auditTransaction.ExecuteAsync(
                token =>
                {
                    dbContext.WeekPeriods.Add(period);
                    return Task.FromResult(NewAudit(
                        now,
                        "WEEK_PERIOD_CREATED_BY_RECURRENCE",
                        "WEEK_PERIOD",
                        period.Id,
                        correlationId,
                        JsonSerializer.SerializeToDocument(new
                        {
                            schemaVersion = 1,
                            period.Id,
                            period.BranchId,
                            period.IsoYear,
                            period.IsoWeek,
                            period.StartsOn,
                            period.EndsOn,
                        }),
                        "SUCCESS"));
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
            return period;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(
            exception,
            WeekPeriodConfiguration.UniqueIndex))
        {
            dbContext.ChangeTracker.Clear();
            var recovered = await FindPeriodAsync(isoYear, isoWeek, cancellationToken)
                ?? throw new InvalidOperationException("The concurrent ISO period could not be recovered.");
            return ValidatePeriod(recovered, isoYear, isoWeek);
        }
    }

    private static WeekPeriod ValidatePeriod(WeekPeriod period, int isoYear, int isoWeek)
    {
        var expected = WeekContract.Calculate(isoYear, isoWeek);
        if (period.StartsOn != expected.StartsOn || period.EndsOn != expected.EndsOn)
        {
            throw new JobExecutionException("RECURRENCE_PERIOD_CONFLICT");
        }

        return period;
    }

    private Task<WeekPeriod?> FindPeriodAsync(int isoYear, int isoWeek, CancellationToken cancellationToken) =>
        dbContext.WeekPeriods.AsNoTracking().SingleOrDefaultAsync(
            period => period.BranchId == BranchScope.LorettaId &&
                period.IsoYear == isoYear &&
                period.IsoWeek == isoWeek,
            cancellationToken);

    private async Task<(GenerationRequest Request, bool Created)> EnsureGenerationRequestAsync(
        Guid ruleVersionId,
        Guid periodId,
        RecurringWindow window,
        RecurringIdentity identity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await FindRequestAsync(ruleVersionId, periodId, identity, cancellationToken);
        if (existing is not null)
        {
            ValidateRequest(existing, identity);
            return (existing, false);
        }

        var request = new GenerationRequest(
            uuidGenerator.NewUuid(),
            identity.IdempotencyKey,
            identity.RequestHash,
            ruleVersionId,
            BranchScope.LorettaId,
            periodId,
            RecurringGenerationContract.OriginType,
            identity.OriginReference,
            requestedBy: null,
            window.OccurrenceInstant);
        var now = clock.UtcNow;
        try
        {
            await auditTransaction.ExecuteAsync(
                token =>
                {
                    dbContext.GenerationRequests.Add(request);
                    dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                    {
                        Scope = GenerationScope,
                        Key = identity.IdempotencyKey,
                        RequestHash = identity.RequestHash,
                        Status = "COMPLETED",
                        ResourceType = "GENERATION_REQUEST",
                        ResourceId = request.Id,
                        ResponseCode = 201,
                        CreatedAt = now,
                        ExpiresAt = DateTimeOffset.MaxValue,
                    });
                    return Task.FromResult(NewAudit(
                        now,
                        "GENERATION_REQUEST_ACCEPTED",
                        "GENERATION_REQUEST",
                        request.Id,
                        correlationId,
                        RequestAuditData(request),
                        "SUCCESS"));
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
            return (request, true);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(
            exception,
            GenerationRequestConfiguration.IdempotencyIndex,
            GenerationRequestConfiguration.FunctionalKeyIndex,
            "PK_idempotency_record"))
        {
            dbContext.ChangeTracker.Clear();
            existing = await FindRequestAsync(ruleVersionId, periodId, identity, cancellationToken)
                ?? throw new InvalidOperationException("The concurrent generation request could not be recovered.");
            ValidateRequest(existing, identity);
            return (existing, false);
        }
    }

    private Task<GenerationRequest?> FindRequestAsync(
        Guid ruleVersionId,
        Guid periodId,
        RecurringIdentity identity,
        CancellationToken cancellationToken) => dbContext.GenerationRequests.AsNoTracking()
        .SingleOrDefaultAsync(request =>
            request.RuleVersionId == ruleVersionId &&
            request.BranchId == BranchScope.LorettaId &&
            request.PeriodId == periodId &&
            request.OriginType == RecurringGenerationContract.OriginType &&
            request.OriginReference == identity.OriginReference,
            cancellationToken);

    private static void ValidateRequest(GenerationRequest request, RecurringIdentity identity)
    {
        if (request.IdempotencyKey != identity.IdempotencyKey ||
            !string.Equals(request.RequestHash, identity.RequestHash, StringComparison.Ordinal) ||
            request.RequestedBy is not null)
        {
            throw new InvalidOperationException("The recurring generation identity conflicts with persisted data.");
        }
    }

    private async Task<bool> EnsurePlanAsync(
        WeekPeriod period,
        RecurringIdentity identity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.WorkPlans.AsNoTracking().SingleOrDefaultAsync(
            plan => plan.BranchId == BranchScope.LorettaId && plan.PeriodId == period.Id,
            cancellationToken);
        var planKey = RecurringGenerationContract.PurposeId(
            "SGOL_RECURRENCE_PLAN_V1",
            identity.IdempotencyKey.ToString("N", CultureInfo.InvariantCulture));
        var hash = Hash(BranchScope.LorettaId, period.Id);
        if (existing is not null)
        {
            await EnsurePlanIdempotencyAsync(existing, planKey, hash, correlationId, cancellationToken);
            return false;
        }

        var plan = new WorkPlan(uuidGenerator.NewUuid(), BranchScope.LorettaId, period.Id);
        var now = clock.UtcNow;
        try
        {
            await auditTransaction.ExecuteAsync(
                token =>
                {
                    dbContext.WorkPlans.Add(plan);
                    AddPlanIdempotency(plan, planKey, hash, now);
                    return Task.FromResult(NewAudit(
                        now,
                        "WORK_PLAN_CREATED_BY_RECURRENCE",
                        "WORK_PLAN",
                        plan.Id,
                        correlationId,
                        PlanAuditData(plan),
                        "SUCCESS"));
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(
            exception,
            WorkPlanConfiguration.UniqueBranchPeriodIndex,
            "PK_idempotency_record"))
        {
            dbContext.ChangeTracker.Clear();
            existing = await dbContext.WorkPlans.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.BranchId == BranchScope.LorettaId && candidate.PeriodId == period.Id,
                cancellationToken)
                ?? throw new InvalidOperationException("The concurrent work plan could not be recovered.");
            await EnsurePlanIdempotencyAsync(existing, planKey, hash, correlationId, cancellationToken);
            return false;
        }
    }

    private async Task EnsurePlanIdempotencyAsync(
        WorkPlan plan,
        Guid key,
        string hash,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            item => item.Scope == PlanScope && item.Key == key,
            cancellationToken);
        if (record is not null)
        {
            if (record.ResourceId != plan.Id || record.RequestHash != hash)
            {
                throw new InvalidOperationException("The recurring work-plan identity conflicts with persisted data.");
            }

            return;
        }

        var now = clock.UtcNow;
        try
        {
            await auditTransaction.ExecuteAsync(
                token =>
                {
                    AddPlanIdempotency(plan, key, hash, now);
                    return Task.FromResult(NewAudit(
                        now,
                        "WORK_PLAN_RECOVERED_BY_RECURRENCE",
                        "WORK_PLAN",
                        plan.Id,
                        correlationId,
                        PlanAuditData(plan),
                        "SUCCESS"));
                },
                cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(
            exception,
            "PK_idempotency_record"))
        {
            dbContext.ChangeTracker.Clear();
            record = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
                item => item.Scope == PlanScope && item.Key == key,
                cancellationToken);
            if (record is null || record.ResourceId != plan.Id || record.RequestHash != hash)
            {
                throw new InvalidOperationException("The concurrent recurring plan identity could not be recovered.");
            }
        }
    }

    private void AddPlanIdempotency(WorkPlan plan, Guid key, string hash, DateTimeOffset now) =>
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Scope = PlanScope,
            Key = key,
            RequestHash = hash,
            Status = "COMPLETED",
            ResourceType = "WORK_PLAN",
            ResourceId = plan.Id,
            ResponseCode = 201,
            CreatedAt = now,
            ExpiresAt = DateTimeOffset.MaxValue,
        });

    private async Task<RecurringOccurrenceResult> RecordNonGeneratingResultAsync(
        RecurringWindow window,
        Guid? ruleVersionId,
        string result,
        string? errorCode,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await RecordOccurrenceAuditAsync(
            window,
            ruleVersionId,
            periodId: null,
            obligationId: null,
            result,
            errorCode,
            correlationId,
            cancellationToken);
        return new RecurringOccurrenceResult(result, ruleVersionId, null, null, errorCode, false);
    }

    private async Task RecordOccurrenceAuditAsync(
        RecurringWindow window,
        Guid? ruleVersionId,
        Guid? periodId,
        Guid? obligationId,
        string result,
        string? errorCode,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await auditTransaction.ExecuteAsync(
            NewAudit(
                now,
                result switch
                {
                    RecurringGenerationContract.Generated => "RECURRENCE_GENERATED",
                    RecurringGenerationContract.Recovered => "RECURRENCE_RECOVERED",
                    RecurringGenerationContract.Omitted => "RECURRENCE_OMITTED",
                    _ => "RECURRENCE_REJECTED",
                },
                "RECURRENCE_OCCURRENCE",
                obligationId,
                correlationId,
                JsonSerializer.SerializeToDocument(new
                {
                    schemaVersion = 1,
                    taskCode = RecurringGenerationContract.TaskCode,
                    ruleVersionId,
                    branchCode = BranchScope.LorettaCode,
                    window.LocalDate,
                    window = window.Window.ToString("HH:mm", CultureInfo.InvariantCulture),
                    periodId,
                    originType = RecurringGenerationContract.OriginType,
                    originReference = RecurringGenerationContract.OriginReference(window.LocalDate, window.Window),
                    result,
                    errorCode,
                }),
                result == RecurringGenerationContract.Rejected ? "REJECTED" : result),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private AuditEvent NewAudit(
        DateTimeOffset occurredAt,
        string action,
        string resourceType,
        Guid? resourceId,
        Guid correlationId,
        JsonDocument afterData,
        string outcome) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = occurredAt,
            ActorType = "SYSTEM",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            AfterData = afterData,
            Outcome = outcome,
        };

    private static JsonDocument RequestAuditData(GenerationRequest request) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            generationRequestId = request.Id,
            request.RuleVersionId,
            request.BranchId,
            request.PeriodId,
            request.OriginType,
            request.OriginReference,
            request.Result,
            request.RequestedBy,
            request.RequestedAt,
        });

    private static JsonDocument PlanAuditData(WorkPlan plan) => JsonSerializer.SerializeToDocument(new
    {
        schemaVersion = 1,
        planId = plan.Id,
        plan.BranchId,
        plan.PeriodId,
        plan.Status,
        plan.RowVersion,
    });

    private static bool IsUniqueViolation(DbUpdateException exception, params string[] constraints) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraintName,
        } && constraints.Contains(constraintName, StringComparer.Ordinal);

    private static string Hash(params Guid[] values) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values.Select(
            value => value.ToString("N", CultureInfo.InvariantCulture))))));
}
