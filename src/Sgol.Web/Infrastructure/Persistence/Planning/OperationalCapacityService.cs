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
public sealed record CapacityRow(Guid PersonId, string Name, string Role, int Percent, int DailyMinutes, long Load, decimal Available, long ProtectedLoad);
public sealed record CapacityChange(Guid PersonId, DateOnly From, DateOnly Through, int Percent, int DailyMinutes, string Reason, bool TransferStarted = false);
public sealed record CapacityMove(Guid TaskId, string TaskName, string FromName, Guid ToId, string ToName, int Minutes, DateTimeOffset StartsAt, DateTimeOffset DueAt);
public sealed record CapacityPreview(CapacityChange Change, IReadOnlyList<CapacityMove> Moves, long RemainingExcess, string Fingerprint);

// One balancing policy is shared by plan generation, reviews and supervisor adjustments.
public sealed class OperationalCapacityService(SgolDbContext db, IClock clock, OperationalSupervisionService supervision)
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
        var tasks = await db.OperationalAssignments.AsNoTracking().Where(a => ids.Contains(a.PersonId) && a.CompletedAt == null)
            .Select(a => new OperationalAssignment { Id = a.Id, PersonId = a.PersonId, PersonName = a.PersonName, Name = a.Name,
                AssignedRole = a.AssignedRole, TaskId = a.TaskId, PlanId = a.PlanId, OccasionId = a.OccasionId, StartsAt = a.StartsAt, DueAt = a.DueAt,
                SpecializationId = a.SpecializationId, SpecializationName = a.SpecializationName,
                PeriodType = a.PeriodType,
                LoadMinutes = a.LoadMinutes, GoalUnits = a.GoalUnits, ProgressUnits = a.ProgressUnits, DifferentPeople = a.DifferentPeople,
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
                    ledger.Load(w.Id, from, through), ledger.Available(w.Id, from, through), relevant.Where(t => t.ProgressUnits > 0 || t.OccasionId != null).Sum(t => (long)t.RemainingMinutes)); }).ToArray();
    }

    public async Task<CapacityPreview> PreviewAsync(Guid actor, CapacityChange change, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
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
        // Capture the input state before simulating any transfers, for stale-preview detection.
        var state = JsonSerializer.Serialize(new { change, DefaultDailyMinutes = OperationalCapacity.DefaultDailyMinutes, ledger.Today, ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
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
        var candidates = ledger.Relevant(worker.Id, change.From, change.Through)
            .Where(t => worker.EligibleFor(t.AssignedRole, t.SpecializationId) && t.OccasionId == null && (change.TransferStarted || t.ProgressUnits == 0))
            .OrderBy(t => t.ProgressUnits > 0).ThenBy(t => t.StartsAt <= clock.UtcNow).ThenByDescending(t => t.StartsAt)
            .ThenByDescending(t => t.RemainingMinutes).ThenBy(t => t.Id).ToArray();
        foreach (var task in candidates)
        {
            if (ledger.Excess(worker.Id, change.From, change.Through) <= 0) break;
            var target = ledger.Workers.Where(w => w.Id != worker.Id && roles.Contains(w.Role) && w.EligibleFor(task.AssignedRole, task.SpecializationId) && ledger.CanAssign(w.Id, task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null))
                .OrderBy(w => RotationCount(w.Id, task))
                .ThenBy(w => task.SpecializationId == null ? ledger.Occupancy(w.Id, change.From, change.Through) : ledger.SpecializedPeriodLoad(w.Id, task)).ThenBy(w => w.Code, StringComparer.Ordinal).ThenBy(w => w.Id).FirstOrDefault();
            if (target == null) continue;
            moves.Add(new(task.Id, task.Name, worker.Name, target.Id, target.Name, task.RemainingMinutes, task.StartsAt, task.DueAt));
            task.PersonId = target.Id; task.AssignedRole = target.Role;
            ledger.InvalidateLoads();
        }
        var excess = (long)Math.Ceiling(ledger.Excess(worker.Id, change.From, change.Through));
        return new(change, moves, excess,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state + JsonSerializer.Serialize(new { Moves=moves,Excess=excess })))));
    }

    public async Task<CapacityPreview> ApplyAsync(Guid actor, CapacityChange change, string fingerprint, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var preview = await BuildAsync(actor, change, token);
        if (preview.Fingerprint != fingerprint) throw new DirectionPlanException("CAPACITY_STALE", 409);
        var moveIds=preview.Moves.Select(m=>m.TaskId).ToArray();
        if(await db.Set<OperationalSequenceAttempt>().AnyAsync(a=>a.AssignmentId!=null && moveIds.Contains(a.AssignmentId.Value),token) ||
            await db.Set<OperationalSequenceStage>().AnyAsync(s=>moveIds.Contains(s.Id),token) ||
            (await ProcessPlanningService.PreparedReservationsAsync(db,token)).Any(r=>moveIds.Contains(r.Id)))
            throw new DirectionPlanException("PROCESS_CAPACITY_ADJUSTMENT_REQUIRED",409);
        var setting = await StoreSettingAsync(db, change, token);
        var currentLedger = await LedgerAsync(db, clock.UtcNow, token);
        var decisions = new List<object>();
        foreach (var move in preview.Moves)
        {
            var task = await db.OperationalAssignments.SingleAsync(a => a.Id == move.TaskId, token);
            var before = new { task.PersonId, task.TemporalPolicyJson, task.TemporalDecisionJson };
            task.PersonId = move.ToId; task.PersonName = move.ToName;
            task.AssignedRole = currentLedger.Workers.Single(w => w.Id == move.ToId).Role; task.Revision++;
            currentLedger.CaptureTime(task, task.StartsAt <= clock.UtcNow ? clock.UtcNow : null);
            decisions.Add(new { AssignmentId=task.Id,Before=before,After=new { task.PersonId,task.TemporalPolicyJson,task.TemporalDecisionJson } });
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
        return true;
    }
    public void CaptureTime(OperationalAssignment task, DateTimeOffset? reception = null)
    {
        if (!Temporal.Enabled) return;
        var decision = CheckTime(task.PersonId, task, reception);
        if (!decision.Eligible) throw new DirectionPlanException(decision.Reason, 422);
        var worker=Workers.Single(w=>w.Id==task.PersonId);
        task.TemporalDecisionJson = JsonSerializer.Serialize(new { decision.Eligible,decision.Reason,decision.ScheduleId,decision.ExceptionId,decision.RotationId,decision.CoverageId,decision.Limit,worker.EmploymentEnd,worker.RoleEnd,PolicyStateRevision=Temporal.StateRevision });
        if (task.TaskId == Temporal.ClosingTaskId && decision.Limit is { } limit) task.DueAt = limit;
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
    public void InvalidateLoads() => dailyLoads = null;
    public IReadOnlyList<CapacityChange> PlannedChanges { get; init; } = [];
    public OperationalCapacity? Setting(Guid person, DateOnly day) => Capacities.SingleOrDefault(c => c.PersonId == person && c.From <= day && c.Through >= day);
    public decimal DayCapacity(Guid person, DateOnly day)
    {
        var w = Workers.SingleOrDefault(w => w.Id == person);
        if (w == null) return 0;
        if (Unavailable.Contains((person, day)) || w.EmploymentEnd <= OperationalSchedule.Instant(day) || w.RoleEnd <= OperationalSchedule.Instant(day)) return 0;
        decimal Limit(decimal minutes)
        {
            if(!Temporal.Enabled) return minutes;
            var limits=new[] { w.EmploymentEnd,w.RoleEnd }.Where(e=>e!=null).Select(e=>e!.Value).ToArray();
            var end=limits.Length==0?DateTimeOffset.MaxValue:limits.Min();
            var usable=(Temporal.Day(person,day).Intervals??[]).Sum(i=>Math.Max(0,(Math.Min(TemporalEligibilityContext.At(day,i.Exit).UtcTicks,end.UtcTicks)-TemporalEligibilityContext.At(day,i.Entry).UtcTicks)/(decimal)TimeSpan.TicksPerMinute));
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
        if (t.GoalUnits <= 0) return [(start, (decimal)t.RemainingMinutes)];
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
