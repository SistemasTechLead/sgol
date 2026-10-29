using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.BuildingBlocks.Time;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalCapacity
{
    public const int DefaultDailyMinutes = 240;
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public int Percent { get; set; } = 100;
    public int DailyMinutes { get; set; } = DefaultDailyMinutes;
    public string Reason { get; set; } = "";
    public long Revision { get; set; } = 1;
}

public sealed class OperationalProgressEntry
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int Units { get; set; }
    public string Note { get; set; } = "";
}

internal sealed class OperationalCapacityConfiguration : IEntityTypeConfiguration<OperationalCapacity>
{
    public void Configure(EntityTypeBuilder<OperationalCapacity> b)
    {
        b.ToTable("operational_capacity", t => t.HasCheckConstraint("CK_capacity_values", "\"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480 AND \"Through\" >= \"From\""));
        b.HasKey(c => c.Id); b.HasIndex(c => new { c.PersonId, c.From });
        b.HasOne<Person>().WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(c => c.Reason).HasMaxLength(1000); b.Property(c => c.Revision).IsConcurrencyToken();
    }
}

internal sealed class OperationalProgressConfiguration : IEntityTypeConfiguration<OperationalProgressEntry>
{
    public void Configure(EntityTypeBuilder<OperationalProgressEntry> b)
    {
        b.ToTable("operational_progress_entry", t => t.HasCheckConstraint("CK_progress_units", "\"Units\" > 0"));
        b.HasKey(p => p.Id); b.HasIndex(p => new { p.AssignmentId, p.OccurredAt });
        b.HasOne<AppUser>().WithMany().HasForeignKey(p => p.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperationalAssignment>().WithMany().HasForeignKey(p => p.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(p => p.Note).HasMaxLength(1000);
    }
}

public sealed record CapacityWorker(Guid Id, string Name, string Code, string Role, DateTimeOffset? EmploymentEnd, DateTimeOffset? RoleEnd)
{
    public bool IsOnTrial { get; init; }
    public Guid[] SpecializationIds { get; init; } = [];
    public bool EligibleFor(string? role, Guid? specialization) =>
        (specialization != null || Role == role) && Qualified(specialization);
    public bool Qualified(Guid? specialization) => specialization == null || !IsOnTrial && SpecializationIds.Contains(specialization.Value);
}
public sealed record CapacityRow(Guid PersonId, string Name, string Role, int Percent, int DailyMinutes, long Load, decimal Available, long ProtectedLoad)
{
    public CapacityActivatedTask[] ActivatedTasks { get; init; } = [];
}
public sealed record CapacityActivatedTask(Guid Id, string Name, DateTimeOffset StartsAt, DateTimeOffset DueAt);
public sealed record CapacityChange(Guid PersonId, DateOnly From, DateOnly Through, int Percent, int DailyMinutes, string Reason, bool TransferStarted = false, Guid[]? ActivatedTaskIds = null, DateTimeOffset? SchedulingNotBefore = null);
public sealed record CapacityMove(Guid TaskId, string TaskName, string FromName, Guid ToId, string ToName, int Minutes, DateTimeOffset StartsAt, DateTimeOffset DueAt)
{
    public DateTimeOffset? PreviousStartsAt { get; init; }
    public DateTimeOffset? PreviousDueAt { get; init; }
    public string? OperationalCriticality { get; init; }
}
public sealed record CapacityPreview(CapacityChange Change, IReadOnlyList<CapacityMove> Moves, long RemainingExcess, string Fingerprint)
{
    public PlanningAssignmentConflict[] Conflicts { get; init; } = [];
}

// One balancing policy is shared by plan generation, reviews and supervisor adjustments.
public sealed partial class OperationalCapacityService(SgolDbContext db, IClock clock, OperationalSupervisionService supervision)
{
    public static async Task<CapacityWorker[]> WorkersAsync(SgolDbContext db, DateTimeOffset now, CancellationToken token)
    {
        var workers = await (from p in db.People.AsNoTracking()
            join u in db.AppUsers.AsNoTracking() on p.Id equals u.PersonId
            join e in db.EmploymentVersions.AsNoTracking() on p.Id equals e.PersonId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where u.Status == AccountStatus.Active && e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active &&
                e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now) && r.BranchId == BranchScope.LorettaId &&
                r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)
            select new CapacityWorker(p.Id, p.DisplayName, p.StableCode, r.RoleCode, e.ValidTo, r.ValidTo)).Distinct().ToArrayAsync(token);
        var ids = workers.Select(w => w.Id).ToArray();
        var trial = await db.People.Where(p => ids.Contains(p.Id) && p.IsOnTrial).Select(p => p.Id).ToArrayAsync(token);
        var links = await (from l in db.Set<Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization>().AsNoTracking()
            join s in db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking() on l.SpecializationId equals s.Id
            where ids.Contains(l.PersonId) && s.Active select l).ToArrayAsync(token);
        return workers.Select(w => w with { IsOnTrial = trial.Contains(w.Id),
            SpecializationIds = links.Where(l => l.PersonId == w.Id).Select(l => l.SpecializationId).Order().ToArray() }).ToArray();
    }

    public static async Task<CapacityLedger> LedgerAsync(SgolDbContext db, DateTimeOffset now, CancellationToken token)
    {
        var workers = await WorkersAsync(db, now, token);
        workers = workers.GroupBy(w => w.Id).Where(g => g.Count() == 1).Select(g => g.Single()).OrderBy(w => w.Id).ToArray();
        var ids = workers.Select(w => w.Id).ToArray();
        var tasks = await db.OperationalAssignments.AsNoTracking().Where(a => ids.Contains(a.PersonId) && (a.CompletedAt == null || a.SchedulingPolicyId != null && a.DueAt > now))
            .Select(a => new OperationalAssignment { Id = a.Id, SchedulingPolicyId = a.SchedulingPolicyId, ClosureMarginPercent = a.ClosureMarginPercent, OperationalCriticality = a.OperationalCriticality, NominalStartsAt = a.NominalStartsAt, ScheduleMode = a.ScheduleMode, TaskCode = a.TaskCode, CompletedAt = a.CompletedAt, ReviewStatus = a.ReviewStatus, PersonId = a.PersonId, PersonName = a.PersonName, Name = a.Name,
                AssignedRole = a.AssignedRole, TaskId = a.TaskId, PlanId = a.PlanId, OccasionId = a.OccasionId, StartsAt = a.StartsAt, DueAt = a.DueAt,
                SpecializationId = a.SpecializationId, SpecializationName = a.SpecializationName,
                PeriodType = a.PeriodType,
                LoadMinutes = a.LoadMinutes, GoalUnits = a.GoalUnits, ProgressUnits = a.ProgressUnits, DifferentPeople = a.DifferentPeople,
                TransferRemainingMinutes = a.TransferRemainingMinutes, TransferProgressUnits = a.TransferProgressUnits,
                TemporalPolicyJson = a.TemporalPolicyJson, TemporalDecisionJson = a.TemporalDecisionJson, Revision = a.Revision, ReassignedFromId = a.ReassignedFromId }).OrderBy(a => a.Id).ToArrayAsync(token);
        var capacities = await db.Set<OperationalCapacity>().AsNoTracking().Where(c => ids.Contains(c.PersonId)).OrderBy(c => c.Id).ToArrayAsync(token);
        var reserved = await (from s in db.Set<OperationalSequenceStage>().AsNoTracking()
            join r in db.Set<OperationalSequenceRun>().AsNoTracking() on s.RunId equals r.Id
            where r.Status == "EN_CURSO" && s.ReservedPersonId != null && ids.Contains(s.ReservedPersonId.Value) &&
                (s.Status == "EN_ESPERA" || s.Status == "PROGRAMADA" || s.Status == "PENDIENTE_ASIGNACION")
            select new { Stage = s, r.PlanId }).ToArrayAsync(token);
        tasks = tasks.Concat(reserved.Select(s => OperationalProcessService.Reservation(s.Stage,s.PlanId))).OrderBy(s=>s.Id).ToArray();
        tasks=tasks.Concat((await ProcessPlanningService.PreparedReservationsAsync(db,token)).Where(a=>ids.Contains(a.PersonId))).OrderBy(a=>a.Id).ToArray();
        var unavailable = await db.AvailabilityDayVersions.AsNoTracking().Where(a => ids.Contains(a.PersonId) && a.Status == AvailabilityVersionStatus.Current && !a.IsAvailable)
            .Select(a => new { a.PersonId, a.LocalDate }).ToArrayAsync(token);
        var legacy = db.LegacyWorkflowEnabled ? await (from a in db.AssignmentVersions.AsNoTracking()
            join o in db.WorkObligations.AsNoTracking() on a.ObligationId equals o.Id
            where ids.Contains(a.PersonId) && a.Status == AssignmentVersionStatuses.Current && o.ExecutionStatus == WorkObligationStatuses.Pending
            group a by a.PersonId into g select new { Person = g.Key, Minutes = g.Count() * 30L }).ToArrayAsync(token) : [];
        return new(workers, tasks, capacities, unavailable.Select(a => (a.PersonId, a.LocalDate)).ToHashSet(),
            legacy.ToDictionary(a => a.Person, a => a.Minutes), WeekContract.LocalToday(now)) { Temporal = await TemporalEligibilityContext.LoadAsync(db, now, token) };
    }

    public async Task<IReadOnlyList<CapacityRow>> RowsAsync(Guid actor, DateOnly from, DateOnly through, CancellationToken token)
    {
        var roles = OperationalSupervisionService.RolesFor(await supervision.AuthorizeAsync(actor, token));
        var ledger = await LedgerAsync(db, clock.UtcNow, token);
        var own = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync(token);
        return ledger.Workers.Where(w => roles.Contains(w.Role) && w.Id != own).OrderBy(w => w.Role).ThenBy(w => w.Name)
            .Select(w => { var c = ledger.Setting(w.Id, from); var relevant = ledger.Relevant(w.Id, from, through);
                return new CapacityRow(w.Id, w.Name, w.Role, c?.Percent ?? 100, c?.DailyMinutes ?? OperationalCapacity.DefaultDailyMinutes,
                    ledger.Load(w.Id, from, through), ledger.Available(w.Id, from, through), relevant.Where(t => t.ProgressUnits > 0 || t.OccasionId != null).Sum(t => (long)t.RemainingMinutes)) {
                    ActivatedTasks = relevant.Where(t => t.SchedulingPolicyId != null && t.OperationalCriticality != "CRITICA" && t.CompletedAt == null && t.ProgressUnits == 0 && t.OccasionId == null && t.StartsAt <= clock.UtcNow && t.DueAt > clock.UtcNow).Select(t => new CapacityActivatedTask(t.Id, t.Name, t.StartsAt, t.DueAt)).ToArray() }; }).ToArray();
    }

    public async Task<CapacityPreview> PreviewAsync(Guid actor, CapacityChange change, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        change = change with { SchedulingNotBefore = clock.UtcNow.AddMinutes(1) };
        var preview = await BuildAsync(actor, change, token);
        await tx.CommitAsync(token); return preview;
    }

    private async Task<CapacityPreview> BuildAsync(Guid actor, CapacityChange change, CancellationToken token)
    {
        if (change.From < WeekContract.LocalToday(clock.UtcNow) || change.Through < change.From || change.Through.DayNumber - change.From.DayNumber > 90 ||
            change.Percent is < 0 or > 100 || change.DailyMinutes is < 1 or > 480 || string.IsNullOrWhiteSpace(change.Reason) || change.Reason.Trim().Length > 1000)
            throw new DirectionPlanException("CAPACITY_INVALID", 422);
        var roles = OperationalSupervisionService.RolesFor(await supervision.AuthorizeAsync(actor, token));
        var ledger = await LedgerAsync(db, clock.UtcNow, token);
        var worker = ledger.Workers.SingleOrDefault(w => w.Id == change.PersonId && roles.Contains(w.Role))
            ?? throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        if (await db.AppUsers.AnyAsync(u => u.Id == actor && u.PersonId == worker.Id, token)) throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        var selectedActive = (change.ActivatedTaskIds ?? []).ToHashSet();
        if (selectedActive.Count > 1000 || selectedActive.Any(id => !ledger.Tasks.Any(t => t.Id == id && t.PersonId == worker.Id && t.SchedulingPolicyId != null && t.CompletedAt == null && t.DueAt > clock.UtcNow && t.ProgressUnits == 0 && t.OccasionId == null && t.OperationalCriticality != "CRITICA"))) throw new DirectionPlanException("CAPACITY_STALE", 409);
        var protectedIds = (await db.OperationalPhotographs.AsNoTracking().Select(p => p.AssignmentId).Distinct().ToArrayAsync(token)).Concat(await db.OperationalAttachments.AsNoTracking().Select(p => p.AssignmentId).ToArrayAsync(token)).ToHashSet();
        protectedIds.UnionWith(await db.Set<OperationalSequenceStage>().AsNoTracking().Select(s => s.Id).ToArrayAsync(token));
        protectedIds.UnionWith((await ProcessPlanningService.PreparedReservationsAsync(db, token)).Select(r => r.Id));
        protectedIds.UnionWith(ledger.Tasks.Where(t => t.ReassignedFromId != null).Select(t => t.Id));
        // Capture the input state before simulating any transfers, for stale-preview detection.
        var state = JsonSerializer.Serialize(new { change, ProtectedIds = protectedIds.Order().ToArray(), DefaultDailyMinutes = OperationalCapacity.DefaultDailyMinutes, ledger.Today, ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2).Select(x => new { Person = x.Item1, Day = x.Item2 }), ledger.Legacy });
        ledger.Override = change;
        var completedPeers = await db.OperationalAssignments.AsNoTracking().Where(a => a.CompletedAt != null && a.ReviewStatus != "REASIGNADA")
            .Select(a => new { a.Id, a.TaskId, a.PersonId, a.StartsAt }).ToArrayAsync(token);
        int RotationCount(Guid person, OperationalAssignment task)
        {
            if (!task.DifferentPeople) return 0;
            var day = WeekContract.LocalToday(task.StartsAt);
            var first = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); var last = first.AddDays(6);
            return ledger.Tasks.Count(a => a.Id != task.Id && a.TaskId == task.TaskId && a.PersonId == person && WeekContract.LocalToday(a.StartsAt) >= first && WeekContract.LocalToday(a.StartsAt) <= last) +
                completedPeers.Count(a => a.Id != task.Id && a.TaskId == task.TaskId && a.PersonId == person && WeekContract.LocalToday(a.StartsAt) >= first && WeekContract.LocalToday(a.StartsAt) <= last);
        }
        var moves = new List<CapacityMove>();
        var conflicts = new List<PlanningAssignmentConflict>();
        var candidates = ledger.Relevant(worker.Id, change.From, change.Through)
            .Where(t => t.CompletedAt == null && t.TransferRemainingMinutes == null && worker.EligibleFor(t.AssignedRole, t.SpecializationId) && t.OccasionId == null && (change.TransferStarted || t.ProgressUnits == 0) &&
                (t.SchedulingPolicyId == null || t.DueAt > clock.UtcNow && t.ProgressUnits == 0 && !protectedIds.Contains(t.Id) && (t.StartsAt > clock.UtcNow || selectedActive.Contains(t.Id) || t.OperationalCriticality == "CRITICA")))
            .OrderBy(t => t.SchedulingPolicyId == null ? 3 : OperationalAgenda.Priority(t.OperationalCriticality)).ThenBy(t => t.ProgressUnits > 0).ThenBy(t => t.StartsAt <= clock.UtcNow).ThenByDescending(t => t.StartsAt)
            .ThenByDescending(t => t.RemainingMinutes).ThenBy(t => t.Id).ToArray();
        foreach (var task in candidates)
        {
            var timeConflict = task.SchedulingPolicyId != null && (!ledger.CheckTime(worker.Id, task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null).Eligible || !OperationalAgenda.Find(ledger, worker.Id, task, task.StartsAt).Eligible);
            if (ledger.Excess(worker.Id, change.From, change.Through) <= 0 && !timeConflict) continue;
            var previousStart = task.StartsAt; var previousDue = task.DueAt;
            var valid = new List<CapacityPlacementChoice>();
            if (timeConflict && task.SchedulingPolicyId != null && task.GoalUnits == 0 && ledger.Excess(worker.Id, change.From, change.Through) <= 0) {
                var ownState = new CapacityLedger(ledger.Workers, ledger.Tasks.Where(t => t.Id != task.Id).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal, PlannedChanges = ledger.PlannedChanges, Override = ledger.Override };
                var ownPlacement = OperationalAgenda.Find(ownState, worker.Id, task, task.OperationalCriticality == "CRITICA" ? task.StartsAt : new[] { task.StartsAt, change.SchedulingNotBefore ?? clock.UtcNow }.Max());
                if (ownPlacement.Eligible) { OperationalAgenda.Apply(task, ownPlacement); if (ownState.CanAssign(worker.Id, task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null)) valid.Add(new(worker, ownPlacement, [])); }
                task.StartsAt = previousStart; task.DueAt = previousDue;
            }
            foreach (var candidate in ledger.Workers.Where(w => w.Id != worker.Id && roles.Contains(w.Role) && w.EligibleFor(task.AssignedRole, task.SpecializationId))) {
                task.StartsAt = previousStart; task.DueAt = previousDue;
                var notBefore = task.OperationalCriticality == "CRITICA" ? task.StartsAt : ledger.Temporal.UsesBaseSchedule(task) && task.OperationalCriticality == "FLEXIBLE" && task.StartsAt > clock.UtcNow ? change.SchedulingNotBefore ?? clock.UtcNow : new[] { task.StartsAt, change.SchedulingNotBefore ?? clock.UtcNow }.Max();
                var placed = task.SchedulingPolicyId == null ? new SchedulingPlacement(true, task.StartsAt, task.DueAt, "LEGACY_CONTRACT") : OperationalAgenda.Find(ledger, candidate.Id, task, notBefore);
                if (placed.Eligible) {
                    OperationalAgenda.Apply(task, placed);
                    if (ledger.CanAssign(candidate.Id, task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null)) { valid.Add(new(candidate, placed, [])); continue; }
                }
                task.StartsAt = previousStart; task.DueAt = previousDue;
                var reflow = WithTargetReflow(ledger, candidate, task, clock.UtcNow, notBefore, protectedIds);
                if (reflow != null) valid.Add(reflow);
            }
            task.StartsAt = previousStart; task.DueAt = previousDue;
            var target = valid.OrderBy(p => p.Placement.StartsAt).ThenBy(p => p.Reflow.Length).ThenBy(p => RotationCount(p.Worker.Id, task))
                .ThenBy(p => ledger.Occupancy(p.Worker.Id, change.From, change.Through)).ThenBy(p => p.Worker.Code, StringComparer.Ordinal).FirstOrDefault();
            if (target == null) {
                var day = WeekContract.LocalToday(task.StartsAt);
                var rejected = ledger.Workers.Where(w => w.Id != worker.Id && roles.Contains(w.Role) && w.EligibleFor(task.AssignedRole, task.SpecializationId)).Select(w => {
                    var attempt = task.SchedulingPolicyId == null ? new SchedulingPlacement(true, task.StartsAt, task.DueAt, "ELIGIBLE") : OperationalAgenda.Find(ledger,w.Id,task,task.OperationalCriticality=="CRITICA" ? task.StartsAt : new[]{task.StartsAt,change.SchedulingNotBefore??clock.UtcNow}.Max());
                    var spans=ledger.Temporal.Day(w.Id,day).Intervals;
                    var hours=spans==null?"Sin configurar":spans.Length==0?"Descanso":string.Join(" · ",spans.Select(i=>$"{i.Entry/60:00}:{i.Entry%60:00}–{i.Exit/60:00}:{i.Exit%60:00}"));
                    return new PlanningCandidateConflict(day,task.StartsAt,w.Name,attempt.Eligible?"PLAN_CAPACITY_EXCEEDED":attempt.Reason,hours,ledger.DayLoad(w.Id,day),ledger.DayCapacity(w.Id,day));
                }).ToArray();
                conflicts.Add(new(task.TaskCode,task.Name,task.LoadMinutes,rejected)); continue;
            }
            var transferred = task.PersonId != target.Worker.Id;
            var recipientName = transferred ? target.Worker.Name : task.PersonName;
            moves.Add(new(task.Id, task.Name, worker.Name, target.Worker.Id, recipientName, task.RemainingMinutes, target.Placement.StartsAt, target.Placement.DueAt) {
                PreviousStartsAt = previousStart, PreviousDueAt = previousDue, OperationalCriticality = task.OperationalCriticality });
            OperationalAgenda.Apply(task, target.Placement);
            task.PersonId = target.Worker.Id; task.PersonName = recipientName; if (transferred) task.AssignedRole = target.Worker.Role;
            foreach (var rescheduled in target.Reflow) {
                var peer = ledger.Tasks.Single(t => t.Id == rescheduled.TaskId);
                peer.StartsAt = rescheduled.StartsAt; peer.DueAt = rescheduled.DueAt; moves.Add(rescheduled);
            }
            ledger.InvalidateLoads();
        }
        var finalMoves = moves.GroupBy(m => m.TaskId).Select(g => g.Last() with {
            FromName = g.First().FromName, PreviousStartsAt = g.First().PreviousStartsAt, PreviousDueAt = g.First().PreviousDueAt }).ToArray();
        moves.Clear(); moves.AddRange(finalMoves);
        var excess = (long)Math.Ceiling(ledger.Excess(worker.Id, change.From, change.Through));
        return new(change, moves, excess,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state + JsonSerializer.Serialize(new { Moves=moves,Excess=excess }))))) { Conflicts = conflicts.ToArray() };
    }

    public async Task<CapacityPreview> ApplyAsync(Guid actor, CapacityChange change, string fingerprint, CancellationToken token)
    {
        bool ledgerTimePassed(CapacityMove m) => m.OperationalCriticality != null && m.StartsAt > (m.PreviousStartsAt ?? m.StartsAt) && m.StartsAt < clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var preview = await BuildAsync(actor, change, token);
        if (preview.Moves.Any(m => ledgerTimePassed(m)) || preview.Fingerprint != fingerprint) throw new DirectionPlanException("CAPACITY_STALE", 409);
        var moveIds=preview.Moves.Select(m=>m.TaskId).ToArray();
        if(await db.Set<OperationalSequenceAttempt>().AnyAsync(a=>a.AssignmentId!=null && moveIds.Contains(a.AssignmentId.Value),token) ||
            await db.Set<OperationalSequenceStage>().AnyAsync(s=>moveIds.Contains(s.Id),token) ||
            (await ProcessPlanningService.PreparedReservationsAsync(db,token)).Any(r=>moveIds.Contains(r.Id)))
            throw new DirectionPlanException("PROCESS_CAPACITY_ADJUSTMENT_REQUIRED",409);
        var applyingAt = clock.UtcNow;
        var selectedActive = change.ActivatedTaskIds ?? [];
        if (change.From < WeekContract.LocalToday(applyingAt) || await db.OperationalAssignments.AnyAsync(a => moveIds.Contains(a.Id) && a.SchedulingPolicyId != null &&
            (a.CompletedAt != null || a.DueAt <= applyingAt || a.ProgressUnits > 0 || a.StartsAt <= applyingAt && a.OperationalCriticality != "CRITICA" && !selectedActive.Contains(a.Id)), token)) throw new DirectionPlanException("CAPACITY_STALE", 409);
        var newMoveIds = await db.OperationalAssignments.Where(a => moveIds.Contains(a.Id) && a.SchedulingPolicyId != null).Select(a => a.Id).ToArrayAsync(token);
        if (await db.OperationalPhotographs.AnyAsync(p => newMoveIds.Contains(p.AssignmentId), token) || await db.OperationalAttachments.AnyAsync(p => newMoveIds.Contains(p.AssignmentId), token)) throw new DirectionPlanException("CAPACITY_STALE", 409);
        var setting = await StoreSettingAsync(db, change, token);
        var currentLedger = await LedgerAsync(db, clock.UtcNow, token);
        var decisions = new List<object>();
        foreach (var move in preview.Moves)
        {
            var task = await db.OperationalAssignments.SingleAsync(a => a.Id == move.TaskId, token);
            var before = new { task.PersonId, task.StartsAt, task.DueAt, task.OperationalCriticality, task.SchedulingPolicyId, task.TemporalPolicyJson, task.TemporalDecisionJson };
            if (task.SchedulingPolicyId != null) { task.StartsAt = move.StartsAt; task.DueAt = move.DueAt; }
            var transferred = task.PersonId != move.ToId;
            task.PersonId = move.ToId; task.PersonName = move.ToName;
            if (transferred) task.AssignedRole = currentLedger.Workers.Single(w => w.Id == move.ToId).Role; task.Revision++;
            currentLedger.CaptureTime(task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null);
            decisions.Add(new { AssignmentId=task.Id,Before=before,After=new { task.PersonId, task.StartsAt, task.DueAt, task.OperationalCriticality, task.SchedulingPolicyId, task.TemporalPolicyJson,task.TemporalDecisionJson } });
        }
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
            ResourceId = setting.Id, ResourceType = "OPERATIONAL_CAPACITY", BranchId = BranchScope.LorettaId, Action = "CAPACITY_REDISTRIBUTED",
            Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(), AfterData = JsonSerializer.SerializeToDocument(new { preview.Change,preview.Moves,preview.RemainingExcess,preview.Fingerprint,TemporalDecisions=decisions }) });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token); return preview;
    }

    internal static async Task<OperationalCapacity> StoreSettingAsync(SgolDbContext db, CapacityChange change, CancellationToken token)
    {
        var setting = await db.OperationalCapacities.SingleOrDefaultAsync(c => c.PersonId == change.PersonId && c.From == change.From && c.Through == change.Through, token);
        if (setting == null) {
            var overlapping = await db.OperationalCapacities.Where(c => c.PersonId == change.PersonId && c.From <= change.Through && c.Through >= change.From).ToArrayAsync(token);
            foreach (var previous in overlapping) {
                if (previous.From < change.From) db.OperationalCapacities.Add(new() { Id = Guid.CreateVersion7(), PersonId = previous.PersonId,
                    From = previous.From, Through = change.From.AddDays(-1), Percent = previous.Percent, DailyMinutes = previous.DailyMinutes, Reason = previous.Reason });
                if (previous.Through > change.Through) db.OperationalCapacities.Add(new() { Id = Guid.CreateVersion7(), PersonId = previous.PersonId,
                    From = change.Through.AddDays(1), Through = previous.Through, Percent = previous.Percent, DailyMinutes = previous.DailyMinutes, Reason = previous.Reason });
                db.OperationalCapacities.Remove(previous);
            }
            setting = new() { Id = Guid.CreateVersion7(), PersonId = change.PersonId, From = change.From, Through = change.Through }; db.OperationalCapacities.Add(setting);
        }
        setting.Percent = change.Percent; setting.DailyMinutes = change.DailyMinutes; setting.Reason = change.Reason.Trim(); setting.Revision++;
        return setting;
    }
}

public sealed class CapacityLedger(CapacityWorker[] workers, OperationalAssignment[] tasks, OperationalCapacity[] capacities,
    HashSet<(Guid, DateOnly)> unavailable, Dictionary<Guid, long> legacy, DateOnly today)
{
    public TemporalEligibilityContext Temporal { get; init; } = new();
    public TemporalDecision CheckTime(Guid person, OperationalAssignment task, DateTimeOffset? reception = null)
    {
        var decision=Temporal.Evaluate(person,task,Unavailable,reception);
        if(!Temporal.Enabled || !decision.Eligible) return decision;
        var worker=Workers.SingleOrDefault(w=>w.Id==person);
        if(worker==null) return decision with { Eligible=false,Reason="SCHEDULE_ACCOUNT_INACTIVE" };
        var limits=new[] { worker.EmploymentEnd,worker.RoleEnd }.Where(e=>e!=null).Select(e=>e!.Value).ToArray();
        if(limits.Length==0) return decision;
        var end=limits.Min();
        var windows=(decision.Windows??[]).Select(w=>w.Exit>end?w with { Exit=end }:w).Where(w=>w.Exit>w.Entry).ToArray();
        var limit=decision.Limit is {} original && original>end?end:decision.Limit;
        var fits=windows.Length > 0;
        return decision with { Eligible=fits,Reason=fits?decision.Reason:"SCHEDULE_EMPLOYMENT_END",Limit=limit,Windows=windows };
    }
    public bool CanAssign(Guid person, OperationalAssignment task, DateTimeOffset? reception = null)
    {
        var decision = CheckTime(person, task, reception);
        if (!decision.Eligible || !CanFit(person, task)) return false;
        if (task.SchedulingPolicyId != null && task.ScheduleMode != "PROGRESO")
        {
            var placed = OperationalAgenda.FindExact(this, person, task);
            if (!placed.Eligible || placed.StartsAt != task.StartsAt || placed.DueAt != task.DueAt) return false;
        }
        return true;
    }
    public void CaptureTime(OperationalAssignment task, DateTimeOffset? reception = null)
    {
        if (!Temporal.Enabled) return;
        var decision = CheckTime(task.PersonId, task, reception);
        if (!decision.Eligible) throw new DirectionPlanException(decision.Reason, 422);
        var worker=Workers.Single(w=>w.Id==task.PersonId);
        task.TemporalDecisionJson = JsonSerializer.Serialize(new { decision.Eligible,decision.Reason,decision.ScheduleId,decision.ExceptionId,decision.RotationId,decision.CoverageId,decision.Limit,worker.EmploymentEnd,worker.RoleEnd,PolicyStateRevision=Temporal.StateRevision });
        if (task.SchedulingPolicyId == null && task.TaskId == Temporal.ClosingTaskId && decision.Limit is { } limit) task.DueAt = limit;
    }
    public CapacityLedger WithAdded(OperationalAssignment task)
    {
        var next = new CapacityLedger(Workers, Tasks.Append(task).ToArray(), Capacities, Unavailable, Legacy, Today)
            { Temporal = Temporal, PlannedChanges = PlannedChanges, Override = Override };
        next.capacityByDay = capacityByDay;
        if (dailyLoads != null) {
            next.dailyLoads = new(dailyLoads);
            foreach (var footprint in next.Footprint(task)) {
                var key = (task.PersonId, footprint.Day); next.dailyLoads[key] = next.dailyLoads.GetValueOrDefault(key) + footprint.Minutes;
            }
        }
        if (agendaByDay != null && legacyAgendaByPerson != null) {
            next.agendaByDay = new(agendaByDay); next.legacyAgendaByPerson = legacyAgendaByPerson;
            if (task.ScheduleMode != "PROGRESO" && task.GoalUnits == 0) {
                if (task.SchedulingPolicyId != null) {
                    var key = (task.PersonId, WeekContract.LocalToday(task.StartsAt)); next.agendaByDay[key] = (next.agendaByDay.GetValueOrDefault(key) ?? []).Append(task).ToArray();
                } else {
                    next.legacyAgendaByPerson = new(legacyAgendaByPerson); next.legacyAgendaByPerson[task.PersonId] = (next.legacyAgendaByPerson.GetValueOrDefault(task.PersonId) ?? []).Append(task).ToArray();
                }
            }
        }
        return next;
    }
    public CapacityWorker[] Workers { get; } = workers;
    public OperationalAssignment[] Tasks { get; } = tasks;
    public OperationalCapacity[] Capacities { get; } = capacities;
    public HashSet<(Guid, DateOnly)> Unavailable { get; } = unavailable;
    public Dictionary<Guid, long> Legacy { get; } = legacy;
    public DateOnly Today { get; } = today;
    private Dictionary<(Guid Person, DateOnly Day), decimal>? dailyLoads;
    private CapacityChange? capacityOverride;
    public CapacityChange? Override
    {
        get => capacityOverride;
        set { capacityOverride = value; InvalidateLoads(); }
    }
    private Dictionary<(Guid Person, DateOnly Day), OperationalAssignment[]>? agendaByDay;
    private Dictionary<Guid, OperationalAssignment[]>? legacyAgendaByPerson;
    internal IEnumerable<OperationalAssignment> SchedulingPeers(Guid person, DateOnly day)
    {
        if (agendaByDay == null) {
            var blocks = Tasks.Where(t => t.ScheduleMode != "PROGRESO" && t.GoalUnits == 0).ToArray();
            agendaByDay = blocks.Where(t => t.SchedulingPolicyId != null).GroupBy(t => (t.PersonId, WeekContract.LocalToday(t.StartsAt))).ToDictionary(g => g.Key, g => g.ToArray());
            legacyAgendaByPerson = blocks.Where(t => t.SchedulingPolicyId == null).GroupBy(t => t.PersonId).ToDictionary(g => g.Key, g => g.ToArray());
        }
        return (agendaByDay.GetValueOrDefault((person, day)) ?? []).Concat(legacyAgendaByPerson!.GetValueOrDefault(person) ?? []);
    }
    public void InvalidateLoads() { dailyLoads = null; agendaByDay = null; legacyAgendaByPerson = null; capacityByDay = []; }
    public IReadOnlyList<CapacityChange> PlannedChanges { get; init; } = [];
    public OperationalCapacity? Setting(Guid person, DateOnly day) => Capacities.SingleOrDefault(c => c.PersonId == person && c.From <= day && c.Through >= day);
    private Dictionary<(Guid Person, DateOnly Day), decimal> capacityByDay = [];
    public decimal DayCapacity(Guid person, DateOnly day)
    {
        var key = (person, day);
        if (!capacityByDay.TryGetValue(key, out var value)) { value = CalculateDayCapacity(person, day); capacityByDay[key] = value; }
        return value;
    }
    private decimal CalculateDayCapacity(Guid person, DateOnly day)
    {
        var w = Workers.SingleOrDefault(w => w.Id == person);
        if (w == null) return 0;
        if (Unavailable.Contains((person, day)) || w.EmploymentEnd <= OperationalSchedule.Instant(day) || w.RoleEnd <= OperationalSchedule.Instant(day)) return 0;
        decimal Limit(decimal minutes)
        {
            if(!Temporal.Enabled) return minutes;
            var limits=new[] { w.EmploymentEnd,w.RoleEnd }.Where(e=>e!=null).Select(e=>e!.Value).ToArray();
            var end=limits.Length==0?DateTimeOffset.MaxValue:limits.Min();
            var usable=(Temporal.Day(person,day,!Temporal.BaseSchedulesForCapacity).Intervals??[]).Sum(i=>Math.Max(0,(Math.Min(TemporalEligibilityContext.At(day,i.Exit).UtcTicks,end.UtcTicks)-TemporalEligibilityContext.At(day,i.Entry).UtcTicks)/(decimal)TimeSpan.TicksPerMinute));
            return Math.Min(minutes,usable);
        }
        if (Override is { } o && o.PersonId == person && o.From <= day && o.Through >= day) return Limit(o.DailyMinutes * o.Percent / 100m);
        var planned = PlannedChanges.SingleOrDefault(c => c.PersonId == person && c.From <= day && c.Through >= day);
        if (planned is not null) return Limit(planned.DailyMinutes * planned.Percent / 100m);
        var c = Setting(person, day); return Limit((c?.DailyMinutes ?? OperationalCapacity.DefaultDailyMinutes) * (c?.Percent ?? 100) / 100m);
    }
    public static IEnumerable<DateOnly> Days(DateOnly from, DateOnly through) => Enumerable.Range(from.DayNumber, through.DayNumber - from.DayNumber + 1).Select(DateOnly.FromDayNumber);
    public DateOnly Day(OperationalAssignment t) => DateOnly.FromDayNumber(Math.Max(WeekContract.LocalToday(t.StartsAt).DayNumber, Today.DayNumber));
    public IEnumerable<(DateOnly Day, decimal Minutes)> Footprint(OperationalAssignment t, Guid? person = null)
    {
        var start = Day(t);
        if (t.GoalUnits <= 0) return [(start, t.PendingItemDemand is {} demand ? (decimal)demand : (decimal)t.RemainingMinutes)];
        var end = WeekContract.LocalToday(t.DueAt.AddTicks(-1));
        if (end < start) end = start;
        var days = Days(start, end).Where(d => DayCapacity(person ?? t.PersonId, d) > 0).ToArray();
        if (days.Length == 0) return [(start, (decimal)t.RemainingMinutes)];
        return days.Select(d => (d, (decimal)t.RemainingMinutes / days.Length));
    }
    public IEnumerable<OperationalAssignment> Relevant(Guid person, DateOnly from, DateOnly through) => Tasks.Where(t => t.PersonId == person && Footprint(t).Any(f => f.Day >= from && f.Day <= through));
    public decimal DayLoad(Guid person, DateOnly day)
    {
        // Build the daily distribution once per ledger, rather than redistributing every task for each candidate/day.
        if (dailyLoads is null)
        {
            dailyLoads = [];
            foreach (var task in Tasks)
                foreach (var slot in Footprint(task))
                {
                    var key = (task.PersonId, slot.Day);
                    dailyLoads[key] = dailyLoads.GetValueOrDefault(key) + slot.Minutes;
                }
        }
        return dailyLoads.GetValueOrDefault((person, day)) + (day == Today ? Legacy.GetValueOrDefault(person) : 0);
    }
    public long Load(Guid person, DateOnly from, DateOnly through) => (long)Math.Ceiling(Days(from, through).Sum(d => DayLoad(person, d)));
    public decimal Available(Guid person, DateOnly from, DateOnly through) => Days(from, through).Sum(d => DayCapacity(person, d));
    public decimal Occupancy(Guid person, DateOnly from, DateOnly through) => Available(person, from, through) is var cap && cap > 0 ? Load(person, from, through) / cap : decimal.MaxValue;
    public long SpecializedPeriodLoad(Guid person, OperationalAssignment task)
    {
        var range = OperationalSchedule.LoadRange(Day(task), task.PeriodType);
        return Load(person, DateOnly.FromDayNumber(Math.Max(range.From.DayNumber, Today.DayNumber)), range.Through);
    }
    public decimal Excess(Guid person, DateOnly from, DateOnly through) => Days(from, through).Sum(d => Math.Max(0, DayLoad(person, d) - DayCapacity(person, d)));
    public bool CanFit(Guid person, OperationalAssignment task)
    {
        var worker = Workers.SingleOrDefault(w => w.Id == person);
        if (worker == null) return false;
        return worker.Qualified(task.SpecializationId) && (worker.EmploymentEnd == null || worker.EmploymentEnd > task.StartsAt) && (worker.RoleEnd == null || worker.RoleEnd > task.StartsAt) &&
            Footprint(task, person).All(f => DayLoad(person, f.Day) + f.Minutes <= DayCapacity(person, f.Day));
    }
}
