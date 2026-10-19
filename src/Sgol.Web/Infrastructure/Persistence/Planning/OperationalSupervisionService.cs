using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class SupervisedTask
{
    public required OperationalAssignment Assignment { get; init; }
    public required string Role { get; init; }
    public required string PlanName { get; init; }
    public required string Period { get; init; }
}

public sealed class OperationalSupervisionService(SgolDbContext db, IClock clock, IDirectionPreparedPlanService direction)
{
    public static string[] RolesFor(string role) => role switch
    {
        CanonicalRole.Direction => OperationalSchedule.Roles,
        CanonicalRole.Administration => [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Subcoordination => [CanonicalRole.SalesFloor],
        _ => []
    };

    public async Task<string> AuthorizeAsync(Guid actor, CancellationToken token)
    {
        var now = clock.UtcNow;
        var roles = await (from u in db.AppUsers.AsNoTracking()
            join e in db.EmploymentVersions.AsNoTracking() on u.PersonId equals e.PersonId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where u.Id == actor && u.Status == AccountStatus.Active &&
                e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active &&
                e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now) &&
                r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active &&
                r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)
            select r.RoleCode).ToArrayAsync(token);
        if (roles.Length != 1 || RolesFor(roles[0]).Length == 0) throw Error("ACCESO_DENEGADO", 403);
        return roles[0];
    }

    private IQueryable<SupervisedTask> Query(string role, Guid actor)
    {
        var allowed = RolesFor(role);
        return from a in db.OperationalAssignments.AsNoTracking()
            join p in db.DirectionPreparedPlans.AsNoTracking() on a.PlanId equals p.Id
            join t in db.OperationalTasks.AsNoTracking() on a.TaskId equals t.Id
            where p.BranchId == BranchScope.LorettaId && allowed.Contains(a.AssignedRole ?? t.AssignedRole) &&
                (role == CanonicalRole.Direction || !db.AppUsers.Any(u => u.Id == actor && u.PersonId == a.PersonId))
            // Project metadata explicitly to avoid loading private attachment bytes in the dashboard.
            select new SupervisedTask { Assignment = new OperationalAssignment {
                Id = a.Id, PlanId = a.PlanId, OccasionId = a.OccasionId, TaskId = a.TaskId, TaskCode = a.TaskCode, Name = a.Name, Description = a.Description,
                Requirement = a.Requirement, AssignedRole = a.AssignedRole ?? t.AssignedRole,
                SpecializationId = a.SpecializationId, SpecializationName = a.SpecializationName,
                LoadMinutes = a.LoadMinutes, PersonId = a.PersonId, PersonName = a.PersonName,
                StartsAt = a.StartsAt, DueAt = a.DueAt, CompletedAt = a.CompletedAt,
                GoalUnits = a.GoalUnits, ProgressUnits = a.ProgressUnits, ProgressUnit = a.ProgressUnit, ScheduleMode = a.ScheduleMode, DifferentPeople = a.DifferentPeople,
                Comment = a.Comment, Conclusion = a.Conclusion, FileName = (a.Attachment != null ? a.Attachment.FileName : db.OperationalPhotographs.Where(f => f.AssignmentId == a.Id && f.AttachedAt != null).OrderBy(f => f.Position).Select(f => f.FileName).FirstOrDefault()),
                ReviewStatus = a.ReviewStatus, ReviewComment = a.ReviewComment ?? a.ReassignmentComment,
                ReviewedAt = a.ReviewedAt, ReviewedBy = a.ReviewedBy,
                Revision = a.Revision, ReassignedFromId = a.ReassignedFromId,
                PeriodType = a.PeriodType, Repetitions = a.Repetitions
            }, Role = a.AssignedRole ?? t.AssignedRole, PlanName = p.Name, Period = a.PeriodType };
    }

    public async Task<IReadOnlyList<SupervisedTask>> ListAsync(Guid actor, CancellationToken token) =>
        await Query(await AuthorizeAsync(actor, token), actor).OrderBy(x => x.Assignment.StartsAt).ThenBy(x => x.Assignment.Id).ToArrayAsync(token);

    public async Task AuthorizeAssignmentAsync(Guid actor, Guid id, CancellationToken token)
    {
        if (!await Query(await AuthorizeAsync(actor, token), actor).AnyAsync(x => x.Assignment.Id == id, token))
            throw Error("ACCESO_DENEGADO", 403);
    }

    public async Task ActivateAsync(Guid actor, Guid planId, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        await direction.AuthorizeAsync(actor, token);
        var plan = await db.DirectionPreparedPlans.SingleOrDefaultAsync(p => p.Id == planId && p.BranchId == BranchScope.LorettaId, token)
            ?? throw Error("DIRECTION_PLAN_NOT_FOUND", 404);
        if(await db.Set<PreparedProcessStart>().AnyAsync(p=>p.PlanId==planId,token)) throw Error("PROCESS_USE_ACTION",409);
        if (plan.Status != "ASIGNADO")
        {
            var codes = await db.DirectionPreparedPlanItems.Where(i => i.PlanId == planId).OrderBy(i => i.Position).Select(i => i.TaskCode).ToArrayAsync(token);
            var assignments = await OperationalPlanGenerator.GenerateAsync(db, plan, codes, clock.UtcNow, token);
            db.OperationalAssignments.AddRange(assignments); plan.MarkAssigned();
            Audit(actor, planId, "OPERATIONAL_PLAN_ACTIVATED", new { planId, Count = assignments.Length });
            await db.SaveChangesAsync(token);
        }
        await tx.CommitAsync(token);
    }

    public async Task ReviewAsync(Guid actor, Guid id, long revision, string decision, string? comment, CancellationToken token)
    {
        if (decision is not ("CONCLUIDA" or "MISMA_PERSONA" or "ROL" or "NO_SATISFECHA")) throw Error("REVIEW_INVALID", 422);
        comment = comment?.Trim();
        if (comment?.Length > 4000 || decision != "CONCLUIDA" && string.IsNullOrWhiteSpace(comment))
            throw Error("REVIEW_COMMENT_REQUIRED", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        await AuthorizeAssignmentAsync(actor, id, token);
        await OperationalTaskService.RequireIndependentActionAsync(db, id, token);
        var task = await db.OperationalAssignments.SingleAsync(a => a.Id == id, token);
        if (task.StartsAt > clock.UtcNow) throw Error("TASK_NOT_STARTED", 422);
        if (decision == "CONCLUIDA" && task.GoalUnits > task.ProgressUnits) throw Error("TASK_GOAL_PENDING", 422);
        if (decision == "CONCLUIDA" && task.Requirement == "FOTOGRAFIA")
        {
            var count = await db.OperationalPhotographs.CountAsync(p => p.AssignmentId == task.Id && p.AttachedAt != null, token);
            if (task.CompletedAt is null || count < task.MinimumPhotos || count > task.MaximumPhotos)
                throw Error("TASK_CAMERA_REQUIRED", 422);
        }
        if (decision == "CONCLUIDA" && task.Requirement == "CAPTURA_PANTALLA")
        {
            var count = await db.OperationalPhotographs.CountAsync(p => p.AssignmentId == id && p.AttachedAt != null, token);
            // Legacy single-file deliveries remain reviewable under their original contract.
            var legacy = count == 0 && await db.OperationalAttachments.AnyAsync(f => f.AssignmentId == id &&
                (f.ContentType == "image/jpeg" || f.ContentType == "image/png"), token);
            if (task.CompletedAt == null || !legacy && (count < task.MinimumPhotos || count > task.MaximumPhotos))
                throw Error("TASK_PHOTO_LIMIT", 422);
        }
        if (task.Revision != revision || task.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA")
            throw Error("TASK_CONFLICT", 409);
        var actorRole = await AuthorizeAsync(actor, token);
        var role = task.AssignedRole ?? await db.OperationalTasks.Where(t => t.Id == task.TaskId).Select(t => t.AssignedRole).SingleAsync(token);
        if (decision is "MISMA_PERSONA" or "ROL")
        {
            var now = clock.UtcNow;
            var loaded = await OperationalCapacityService.LedgerAsync(db, now, token);
            var ledger = new CapacityLedger(loaded.Workers, loaded.Tasks.Where(t => t.Id != id).ToArray(), loaded.Capacities, loaded.Unavailable, loaded.Legacy, loaded.Today) { Temporal = loaded.Temporal };
            var replacement = new OperationalAssignment {
                Id = Guid.CreateVersion7(), PlanId = task.PlanId, OccasionId = task.OccasionId, TaskId = task.TaskId, TaskRevision = task.TaskRevision, TaskCode = task.TaskCode,
                Name = task.Name, Description = task.Description, Requirement = task.Requirement, AssignedRole = role, LoadMinutes = task.LoadMinutes,
                MinimumPhotos = task.MinimumPhotos, MaximumPhotos = task.MaximumPhotos, MaximumPhotoBytes = task.MaximumPhotoBytes,
                SpecializationId = task.SpecializationId, SpecializationName = task.SpecializationName,
                PeriodType = task.PeriodType, Repetitions = task.Repetitions,
                StartsAt = now, DueAt = now.Add(task.DueAt - task.StartsAt), ReassignedFromId = task.Id,
                ScheduleMode = task.ScheduleMode, DifferentPeople = task.DifferentPeople, GoalUnits = task.GoalUnits, ProgressUnit = task.ProgressUnit,
                PreferredMonthDays = task.PreferredMonthDays, ReassignmentComment = comment };
            var day = WeekContract.LocalToday(task.StartsAt);
            var weekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); var weekEnd = weekStart.AddDays(6);
            var peers = await db.OperationalAssignments.AsNoTracking().Where(a => a.TaskId == task.TaskId && a.Id != id && a.ReviewStatus != "REASIGNADA")
                .Select(a => new { a.PersonId, a.StartsAt, a.OccasionId }).ToArrayAsync(token);
            var worker = ledger.Workers.Where(w => RolesFor(actorRole).Contains(w.Role) && w.EligibleFor(role, task.SpecializationId) && (decision == "ROL" || w.Id == task.PersonId) && ledger.CanAssign(w.Id, replacement, now))
                .Where(w => task.OccasionId == null || !peers.Any(a => a.PersonId == w.Id && a.OccasionId == task.OccasionId))
                .OrderBy(w => !task.DifferentPeople ? 0 : peers.Count(a => a.PersonId == w.Id && WeekContract.LocalToday(a.StartsAt) >= weekStart && WeekContract.LocalToday(a.StartsAt) <= weekEnd))
                .ThenBy(w => replacement.SpecializationId == null ? ledger.Occupancy(w.Id, ledger.Today, ledger.Today.AddDays(6)) : ledger.SpecializedPeriodLoad(w.Id, replacement))
                .ThenBy(w => ledger.Tasks.Where(t => t.PersonId == w.Id).Sum(t => (long)t.RemainingMinutes))
                .ThenBy(w => w.Code, StringComparer.Ordinal).ThenBy(w => w.Id).FirstOrDefault()
                ?? throw Error("PLAN_NO_AVAILABLE_WORKER", 422);
            replacement.PersonId = worker.Id; replacement.PersonName = worker.Name; replacement.AssignedRole = worker.Role;
            replacement.PreviousLoadMinutes = ledger.Tasks.Where(t => t.PersonId == worker.Id).Sum(t => (long)t.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(worker.Id);
            ledger.CaptureTime(replacement, now); db.OperationalAssignments.Add(replacement);
        }
        task.ReviewStatus = decision is "MISMA_PERSONA" or "ROL" ? "REASIGNADA" : decision;
        if (task.CompletedAt is null)
        {
            task.CompletedAt = clock.UtcNow; task.CompletedBy = actor; task.CompletionKind = "SUPERVISION";
        }
        task.ReviewDecision = decision;
        task.ReviewedAt = clock.UtcNow; task.ReviewedBy = actor; task.ReviewComment = comment; task.Revision++;
        Audit(actor, id, "OPERATIONAL_TASK_REVIEWED", new { task.PlanId, decision, comment, task.Revision });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task AddTasksAsync(Guid actor, Guid planId, string[] codes, CancellationToken token)
    {
        if (codes.Length is < 1 or > 1000 || codes.Distinct().Count() != codes.Length) throw Error("DIRECTION_PLAN_TASK_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var plan = await db.DirectionPreparedPlans.SingleOrDefaultAsync(p => p.Id == planId && p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO", token)
            ?? throw Error("DIRECTION_PLAN_NOT_FOUND", 404);
        if (plan.PeriodType == "OCASIONAL") throw Error("DIRECTION_PLAN_TASK_INVALID", 422);
        if (plan.EndsOn < WeekContract.LocalToday(clock.UtcNow)) throw Error("PLAN_ENDED", 422);
        if (plan.IsWeeklyOperationsPlan && await db.OperationalTasks.AnyAsync(t => codes.Contains(t.TaskCode) && !t.IsWeeklyOperationsTask, token))
            throw Error("WEEKLY_OPERATIONS_TASK_INVALID", 422);
        var additions = await OperationalPlanGenerator.GenerateAsync(db, plan, codes, clock.UtcNow, token, allowReserved: true);
        var items = await db.DirectionPreparedPlanItems.Where(p => p.PlanId == planId).ToArrayAsync(token);
        var definitions = await db.OperationalTasks.Where(t => codes.Contains(t.TaskCode)).ToArrayAsync(token);
        var position = items.Length;
        foreach (var task in definitions.Where(t => !items.Any(i => i.TaskCode == t.TaskCode)))
            db.DirectionPreparedPlanItems.Add(new(planId, task.TaskCode, task.Name, position++));
        db.OperationalAssignments.AddRange(additions);
        Audit(actor, planId, "OPERATIONAL_PLAN_TASKS_ADDED", new { codes, Count = additions.Length });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    private void Audit(Guid actor, Guid id, string action, object data) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
        ResourceId = id, ResourceType = "OPERATIONAL_TASK", BranchId = BranchScope.LorettaId,
        Action = action, Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(), AfterData = JsonSerializer.SerializeToDocument(data)
    });
    private static DirectionPlanException Error(string code, int status) => new(code, status);
}
