using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class EfDirectionPreparedPlanService(SgolDbContext db, AuditTransaction audit,
    IClock clock, IUuidGenerator ids, IDirectionPlanningCatalogReader catalog) : IDirectionPreparedPlanService
{
    private const string Operation = "DIRECTION_PLAN_CREATE";
    private const string Resource = "DIRECTION_PREPARED_PLAN";

    public async Task AuthorizeAsync(Guid actor, CancellationToken token = default)
    {
        await CheckAuthorityAsync(actor, clock.UtcNow, false, token);
    }

    public async Task AuthorizeWeeklyOperationsAsync(Guid actor, CancellationToken token = default)
    {
        await CheckAuthorityAsync(actor, clock.UtcNow, false, token, allowSubcoordination: true);
    }

    private async Task<string> CheckAuthorityAsync(Guid actor, DateTimeOffset at, bool locked, CancellationToken token, bool allowAdministration = false, bool allowSubcoordination = false)
    {
        var users = locked ? db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {actor} FOR UPDATE") : db.AppUsers.Where(u => u.Id == actor);
        var user = await users.AsNoTracking().SingleOrDefaultAsync(token);
        if (user is null || user.Status != AccountStatus.Active) throw Denied();
        var employmentRows = locked ? db.EmploymentVersions.FromSqlInterpolated($"SELECT * FROM employment_version WHERE person_id = {user.PersonId} FOR UPDATE")
            : db.EmploymentVersions.Where(e => e.PersonId == user.PersonId);
        var employments = await employmentRows.AsNoTracking().ToListAsync(token);
        var roleRows = locked ? db.RoleAssignmentVersions.FromSqlInterpolated($"SELECT * FROM role_assignment_version WHERE user_id = {actor} FOR UPDATE")
            : db.RoleAssignmentVersions.Where(r => r.UserId == actor);
        var roles = await roleRows.AsNoTracking().ToListAsync(token);
        if (locked) at = clock.UtcNow;
        var current = roles.Where(r => r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= at && (r.ValidTo is null || r.ValidTo > at)).ToArray();
        if (current.Length != 1 || current[0].BranchId != BranchScope.LorettaId ||
            current[0].RoleCode != CanonicalRole.Direction && !(allowAdministration && current[0].RoleCode == CanonicalRole.Administration) &&
            !(allowSubcoordination && current[0].RoleCode == CanonicalRole.Subcoordination)) throw Denied();
        var active = employments.Where(e => e.Status == EmploymentStatus.Active && e.ValidFrom <= at && (e.ValidTo is null || e.ValidTo > at)).ToArray();
        if (active.Length != 1 || active[0].BranchId != BranchScope.LorettaId) throw Denied();
        return current[0].RoleCode;
    }

    public async Task<DirectionPlanSaved> CreateAsync(Guid actor, Guid key, Guid correlation, DirectionPlanInput input, CancellationToken token = default)
    {
        if (actor == Guid.Empty || key == Guid.Empty || correlation == Guid.Empty) throw new DirectionPlanException("DIRECTION_PLAN_REQUEST_INVALID", 400);
        DirectionPlanSaved? saved = null;
        DirectionPlanException? rejection = null;
        try
        {
            await audit.ExecuteAsync(IsolationLevel.ReadCommitted, async ct =>
            {
                await OperationalTaskService.LockAsync(db, ct);
                // The account lock serializes creation/replay for this actor. The protocol's DB unique key is the final safeguard.
                var exists = await db.AppUsers.AnyAsync(u => u.Id == actor, ct);
                if (!exists) throw Denied();
                var at = clock.UtcNow;
                try { await CheckAuthorityAsync(actor, at, true, ct, allowSubcoordination: input.IsWeeklyOperationsPlan); }
                catch (DirectionPlanException e) { rejection = e; return Event(actor, key, correlation, null, "DIRECTION_PLAN_CREATE_REJECTED", e.Code, at); }
                at = clock.UtcNow;

                DirectionPlanInput normalized;
                DirectionPlanException? validation = null;
                try { normalized = DirectionPlanContract.Normalize(input); }
                catch (DirectionPlanException e) { normalized = input; validation = e; }
                var scope = IdempotencyProtocol.Scope(actor.ToString("D"), Operation, BranchScope.LorettaCode);
                object hashInput = !normalized.IsWeeklyOperationsPlan && normalized.CapacityAdjustments is not { Count: > 0 }
                    ? new { normalized.Name, normalized.Area, normalized.PeriodType, normalized.Year, normalized.PeriodNumber, normalized.TaskCodes, normalized.CapacityFingerprint, normalized.SchedulingNotBefore, normalized.SchedulingSearchMultiplier }
                    : normalized.IsWeeklyOperationsPlan ? normalized
                    : new { normalized.Name, normalized.Area, normalized.PeriodType, normalized.Year, normalized.PeriodNumber, normalized.TaskCodes,
                        normalized.CapacityAdjustments, normalized.CapacityFingerprint, normalized.SchedulingNotBefore, normalized.SchedulingSearchMultiplier };
                if (normalized.RollingMonthStartsOn != null) hashInput = new { RollingMonth = normalized };
                var hash = IdempotencyProtocol.HashCanonical(Operation, actor.ToString("D"), BranchScope.LorettaCode, hashInput);
                var replay = await db.IdempotencyRecords.FromSqlInterpolated($"SELECT * FROM idempotency_record WHERE scope = {scope} AND key = {key} FOR UPDATE")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (replay is not null)
                {
                    if (replay.RequestHash != hash)
                    {
                        rejection = new("IDEMPOTENCY_CONFLICT", 409);
                        return IdempotencyProtocol.ConflictAudit(ids.NewUuid(), at, actor, Resource, replay.ResourceId,
                            BranchScope.LorettaId, correlation, key, Operation);
                    }
                    if (replay.ResponseCode >= 400)
                    {
                        var error = IdempotencyProtocol.ReadPayload<Rejection>(replay);
                        rejection = new(error.Code, replay.ResponseCode);
                        return Event(actor, key, correlation, null, "DIRECTION_PLAN_CREATE_REJECTED", error.Code, at);
                    }
                    saved = IdempotencyProtocol.ReadPayload<DirectionPlanSaved>(replay) with { Result = "RECOVERED" };
                    return Event(actor, key, correlation, saved.Plan.PlanId, "DIRECTION_PLAN_RECOVERED", "RECOVERED", at);
                }

                try
                {
                    if (validation is not null) throw validation;
                    var options = (await catalog.ReadTaskOptionsAsync(ct)).ToDictionary(t => t.TaskCode, StringComparer.Ordinal);
                    var excluded = await db.OperationalTasks.Where(t => normalized.TaskCodes.Contains(t.TaskCode) && (!t.Active || t.Deleted)).Select(t => t.TaskCode).ToArrayAsync(ct);
                    var activeCodes = normalized.TaskCodes.Except(excluded).ToArray();
                    if (normalized.IsWeeklyOperationsPlan)
                    {
                        if (await db.DirectionPreparedPlans.AnyAsync(p => p.BranchId == BranchScope.LorettaId &&
                            p.IsWeeklyOperationsPlan && p.Year == normalized.Year && p.PeriodNumber == normalized.PeriodNumber, ct))
                            throw new DirectionPlanException("WEEKLY_OPERATIONS_PLAN_EXISTS", 409);
                        var expected = options.Values.Where(t => t.IsWeeklyOperationsTask).Select(t => t.TaskCode).ToHashSet(StringComparer.Ordinal);
                        if (expected.Count == 0) throw new DirectionPlanException("WEEKLY_OPERATIONS_TASKS_REQUIRED", 422);
                        if (!expected.SetEquals(activeCodes)) throw new DirectionPlanException("WEEKLY_OPERATIONS_CATALOG_CHANGED", 409);
                    }
                    if (activeCodes.Any(code => !options.ContainsKey(code))) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
                    var plan = new DirectionPreparedPlan(ids.NewUuid(), BranchScope.LorettaId, actor, at, normalized);
                    var schedulingPolicy = await OperationalSchedulingPolicyService.CurrentAsync(db, ct);
                    plan.CaptureSchedulingPolicy(schedulingPolicy.Id);
                    if (string.IsNullOrWhiteSpace(normalized.CapacityFingerprint)) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
                    CapacityLedger? preparedLedger = null;
                    CapacityChange[] changes = [];
                    if (!string.IsNullOrWhiteSpace(normalized.CapacityFingerprint) || normalized.CapacityAdjustments is { Count: > 0 })
                    {
                        var state = await PlanningCapacity.ReadAsync(db, normalized, at, ct);
                        await PlanningCapacity.DailyLoadAsync(db, normalized, state.Preview, state.Ledger, ct);
                        if (state.Preview.AgendaFingerprint != normalized.CapacityFingerprint) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
                        changes = PlanningCapacity.Changes(normalized, state.Preview, state.Ledger);
                        var ledger = state.Ledger;
                        preparedLedger = new(ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today)
                            { Temporal = ledger.Temporal, PlannedChanges = changes };
                    }
                    var assignments = await OperationalPlanGenerator.GenerateAsync(db, plan, normalized.TaskCodes, at, ct, preparedLedger: preparedLedger);
                    var savingDay = WeekContract.LocalToday(clock.UtcNow);
                    if (assignments.Any(a => a.SchedulingPolicyId != null && a.ScheduleMode != "PROGRESO" && a.StartsAt < clock.UtcNow &&
                        !(normalized.IsWeeklyOperationsPlan && WeekContract.LocalToday(a.StartsAt) == savingDay))) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
                    // Capacity adjustment rows reference this plan during the per-segment flush below.
                    db.DirectionPreparedPlans.Add(plan);
                    // Only stage settings after successful generation: rejected plans must not change shared capacity.
                    foreach (var change in changes)
                    {
                        var previous = await db.OperationalCapacities.AsNoTracking()
                            .Where(c => c.PersonId == change.PersonId && c.From <= change.Through && c.Through >= change.From).ToArrayAsync(ct);
                        // Preserve just the overwritten interval so deleting a plan can restore prior capacity.
                        var before = previous.Select(c => new OperationalCapacity { Id = c.Id, PersonId = c.PersonId,
                            From = c.From < change.From ? change.From : c.From, Through = c.Through > change.Through ? change.Through : c.Through,
                            Percent = c.Percent, DailyMinutes = c.DailyMinutes, Reason = c.Reason, Revision = c.Revision }).ToArray();
                        var setting = await OperationalCapacityService.StoreSettingAsync(db, change, ct);
                        var adjustmentId = ids.NewUuid();
                        db.PlanCapacityAdjustments.Add(StoredPlanCapacityAdjustment.Capture(adjustmentId, plan.Id, at, setting, before));
                        db.AuditEvents.Add(new AuditEvent { Id = adjustmentId, ActorUserId = actor, ActorType = "USER", OccurredAt = at,
                            ResourceId = setting.Id, ResourceType = "OPERATIONAL_CAPACITY", BranchId = BranchScope.LorettaId,
                            Action = "PLAN_CAPACITY_ADJUSTED", Outcome = "SUCCESS", CorrelationId = correlation,
                            BeforeData = JsonSerializer.SerializeToDocument(before),
                            AfterData = JsonSerializer.SerializeToDocument(new { plan.Id, change, setting }) });
                        // Flush each disjoint segment inside this transaction so overlap splitting sees prior segments.
                        await db.SaveChangesAsync(ct);
                    }
                    plan.MarkAssigned();
                    db.OperationalAssignments.AddRange(assignments);
                    var tasks = activeCodes.Select((code, index) => new DirectionPreparedPlanItem(plan.Id, code, options[code].Name, index)).ToArray();
                    db.DirectionPreparedPlanItems.AddRange(tasks);
                    saved = new("CREATED", plan.Details(tasks.Select(t => new DirectionPlanTask(t.TaskCode, t.Name)).ToArray()) with {
                        Assignments = assignments.Select(a => a.Details()).ToArray()
                    });
                    db.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, key, hash, Resource, plan.Id, 201, saved, at, DateTimeOffset.MaxValue));
                    return Event(actor, key, correlation, plan.Id, "DIRECTION_PLAN_CREATED", "CREATED", at, saved.Plan);
                }
                catch (DirectionPlanException e)
                {
                    rejection = e;
                    db.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, key, hash, Resource, BranchScope.LorettaId,
                        e.Status, new Rejection(e.Code), at, DateTimeOffset.MaxValue));
                    return Event(actor, key, correlation, null, "DIRECTION_PLAN_CREATE_REJECTED", e.Code, at);
                }
            }, token);
        }
        finally { db.ChangeTracker.Clear(); }
        if (rejection is not null) throw rejection;
        return saved ?? throw new InvalidOperationException("Preparation was not confirmed.");
    }

    public async Task<DirectionPlanDetails> ReadAsync(Guid actor, Guid id, CancellationToken token = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", token);
        await CheckAuthorityAsync(actor, clock.UtcNow, false, token, allowAdministration: true, allowSubcoordination: true);
        var plan = await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.BranchId == BranchScope.LorettaId, token)
            ?? throw new DirectionPlanException("DIRECTION_PLAN_NOT_FOUND", 404);
        var details = (await DetailsAsync([plan], token))[0];
        await tx.CommitAsync(token); return details;
    }

    public async Task<DirectionPlanPage> ListAsync(Guid actor, int limit, DirectionPlanCursor? cursor, CancellationToken token = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", token);
        var role = await CheckAuthorityAsync(actor, clock.UtcNow, false, token, allowAdministration: true, allowSubcoordination: true);
        if (limit is < 1 or > 100) throw new DirectionPlanException("DIRECTION_PLAN_REQUEST_INVALID", 400);
        if (cursor is not null && (cursor.Actor != actor || cursor.Role != role || cursor.Limit != limit || cursor.PlanId == Guid.Empty || cursor.CreatedAt.Offset != TimeSpan.Zero))
            throw new DirectionPlanException("CURSOR_INVALIDO", 400);
        var today = WeekContract.LocalToday(clock.UtcNow);
        var query = db.DirectionPreparedPlans.AsNoTracking().Where(p => p.BranchId == BranchScope.LorettaId && p.EndsOn >= today);
        if (cursor is not null) query = query.Where(p => p.CreatedAt < cursor.CreatedAt || p.CreatedAt == cursor.CreatedAt && p.Id.CompareTo(cursor.PlanId) > 0);
        var page = await query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(limit + 1).ToListAsync(token);
        DirectionPlanCursor? next = null;
        if (page.Count > limit) { page.RemoveAt(page.Count - 1); next = new(actor, role, limit, page[^1].CreatedAt, page[^1].Id); }
        var details = await DetailsAsync(page, token);
        await tx.CommitAsync(token); return new(details, next);
    }

    private async Task<IReadOnlyList<DirectionPlanDetails>> DetailsAsync(IReadOnlyList<DirectionPreparedPlan> plans, CancellationToken token)
    {
        var planIds = plans.Select(p => p.Id).ToArray();
        var items = await db.DirectionPreparedPlanItems.AsNoTracking().Where(i => planIds.Contains(i.PlanId)).OrderBy(i => i.Position).ToListAsync(token);
        var assignments = await db.OperationalAssignments.AsNoTracking().Where(a => planIds.Contains(a.PlanId)).OrderBy(a => a.StartsAt)
            .Select(a => new { a.PlanId, Detail = new OperationalAssignmentDetails(a.Id, a.TaskCode, a.Name, a.Requirement,
                a.LoadMinutes, a.PersonId, a.PersonName, a.StartsAt, a.DueAt, a.CompletedAt, a.PreviousLoadMinutes, a.Comment, (a.Attachment != null ? a.Attachment.FileName : db.OperationalPhotographs.Where(f => f.AssignmentId == a.Id && f.AttachedAt != null).OrderBy(f => f.Position).Select(f => f.FileName).FirstOrDefault()), a.AssignedRole,
                a.Conclusion, a.ReviewStatus, a.ReviewComment ?? a.ReassignmentComment, null, null, null, null, null,
                a.ScheduleMode, a.GoalUnits, a.ProgressUnits, a.ProgressUnit, a.Revision, a.SpecializationId, a.SpecializationName,
                a.MinimumPhotos, a.MaximumPhotos, a.MaximumPhotoBytes, a.OccasionId, a.Description) }).ToArrayAsync(token);
        return plans.Select(p => p.Details(items.Where(i => i.PlanId == p.Id).Select(i => new DirectionPlanTask(i.TaskCode, i.Name)).ToArray()) with {
            Assignments = assignments.Where(a => a.PlanId == p.Id).Select(a => a.Detail).ToArray()
        }).ToArray();
    }

    private AuditEvent Event(Guid actor, Guid key, Guid correlation, Guid? id, string action, string outcome, DateTimeOffset at, DirectionPlanDetails? plan = null) => new()
    {
        Id = ids.NewUuid(), OccurredAt = at, ActorUserId = actor, ActorType = "USER", Action = action, ResourceType = Resource,
        ResourceId = id, BranchId = BranchScope.LorettaId, CorrelationId = correlation, RequestId = key.ToString("D"), Outcome = outcome,
        AfterData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, plan, outcome }, JsonSerializerOptions.Web)
    };
    private static DirectionPlanException Denied() => new("ACCESO_DENEGADO", 403);
    private sealed record Rejection(string Code);
}
