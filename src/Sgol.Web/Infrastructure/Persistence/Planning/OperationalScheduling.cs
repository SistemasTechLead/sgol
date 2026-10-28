using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalSchedulingPolicy
{
    public static readonly Guid ReflowId = Guid.Parse("b06ff0ad-6160-438a-a847-03201b541e72");
    public static readonly Guid InitialId = Guid.Parse("d2a5d1af-5886-4b9c-bfb1-590399c8f734");
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public int MarginPercent { get; set; } = 50;
    public int FormulaVersion { get; set; } = 1;
    public int AgendaVersion { get; set; } = 1;
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}

public sealed class OperationalSchedulingSettings
{
    public int Id { get; set; } = 1;
    public Guid CurrentPolicyId { get; set; }
    public long Revision { get; set; } = 1;
}

internal sealed class OperationalSchedulingConfiguration : IEntityTypeConfiguration<OperationalSchedulingPolicy>, IEntityTypeConfiguration<OperationalSchedulingSettings>
{
    public void Configure(EntityTypeBuilder<OperationalSchedulingPolicy> b)
    {
        b.ToTable("operational_scheduling_policy", t => t.HasCheckConstraint("CK_scheduling_policy", "\"MarginPercent\" BETWEEN 0 AND 500 AND \"Revision\" > 0 AND \"FormulaVersion\" = 1 AND \"AgendaVersion\" IN (1,2)"));
        b.HasKey(p => p.Id); b.HasIndex(p => p.Revision).IsUnique();
        b.HasData(new OperationalSchedulingPolicy { Id = OperationalSchedulingPolicy.InitialId, Revision = 1, MarginPercent = 50, ChangedAt = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero) });
    }
    public void Configure(EntityTypeBuilder<OperationalSchedulingSettings> b)
    {
        b.ToTable("operational_scheduling_settings", t => t.HasCheckConstraint("CK_scheduling_settings", "\"Id\" = 1 AND \"Revision\" > 0"));
        b.HasKey(s => s.Id); b.Property(s => s.Id).ValueGeneratedNever(); b.Property(s => s.Revision).IsConcurrencyToken();
        b.HasOne<OperationalSchedulingPolicy>().WithMany().HasForeignKey(s => s.CurrentPolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(new OperationalSchedulingSettings { CurrentPolicyId = OperationalSchedulingPolicy.InitialId });
    }
}

public sealed class OperationalSchedulingPolicyService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public static async Task<OperationalSchedulingPolicy> CurrentAsync(SgolDbContext db, CancellationToken token) =>
        await (from s in db.Set<OperationalSchedulingSettings>().AsNoTracking() join p in db.Set<OperationalSchedulingPolicy>().AsNoTracking() on s.CurrentPolicyId equals p.Id where s.Id == 1 select p).SingleAsync(token);

    public async Task<OperationalSchedulingPolicy> ReadAsync(Guid actor, CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, token);
        return await CurrentAsync(db, token);
    }

    public async Task ChangeAsync(Guid actor, int margin, long revision, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        if (margin is < 0 or > 500) throw new DirectionPlanException("SCHEDULING_MARGIN_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var state = await db.Set<OperationalSchedulingSettings>().SingleAsync(token);
        if (state.Revision != revision) throw new DirectionPlanException("SCHEDULING_POLICY_STALE", 409);
        var before = await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == state.CurrentPolicyId, token);
        if (before.MarginPercent == margin) { await tx.CommitAsync(token); return; }
        var policy = new OperationalSchedulingPolicy { Id = Guid.CreateVersion7(), Revision = state.Revision + 1, MarginPercent = margin, AgendaVersion = before.AgendaVersion, ChangedBy = actor, ChangedAt = clock.UtcNow };
        db.Add(policy); state.CurrentPolicyId = policy.Id; state.Revision = policy.Revision;
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
            ResourceType = "SCHEDULING_POLICY", ResourceId = policy.Id, BranchId = Sgol.Organization.Contracts.BranchScope.LorettaId,
            Action = "SCHEDULING_POLICY_CHANGED", Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(),
            BeforeData = JsonSerializer.SerializeToDocument(before), AfterData = JsonSerializer.SerializeToDocument(policy) });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
}

public sealed record SchedulingPlacement(bool Eligible, DateTimeOffset StartsAt, DateTimeOffset DueAt, string Reason);

// No database access: the same captured inputs are used in preview, save and exceptional adjustments.
public static class OperationalAgenda
{
    public static bool IsDefined(string? value) => value is "CRITICA" or "IMPORTANTE" or "FLEXIBLE";
    public static string Label(string? value) => value switch { "CRITICA" => "Crítica", "IMPORTANTE" => "Importante", "FLEXIBLE" => "Flexible", _ => "Sin clasificar" };
    public static int Priority(string? value) => value switch { "CRITICA" => 0, "IMPORTANTE" => 1, "FLEXIBLE" => 2, _ => 3 };
    public static int Duration(int load, int margin)
    {
        if (load is < 1 or > 4800 || margin is < 0 or > 500) throw new DirectionPlanException("SCHEDULING_MARGIN_INVALID", 422);
        return checked((int)decimal.Ceiling(load * (100m + margin) / 100m));
    }
    public static void Capture(OperationalAssignment task, OperationalSchedulingPolicy policy, string? criticality, DateTimeOffset nominal)
    {
        if (!IsDefined(criticality)) throw new DirectionPlanException("SCHEDULING_CRITICALITY_REQUIRED", 422);
        task.SchedulingPolicyId = policy.Id; task.ClosureMarginPercent = policy.MarginPercent;
        task.OperationalCriticality = criticality; task.NominalStartsAt = nominal;
        task.StartsAt = nominal;
        task.DueAt = task.ScheduleMode == "PROGRESO" ? OperationalSchedule.Instant(WeekContract.LocalToday(nominal).AddDays(7)) : nominal.AddMinutes(Duration(task.LoadMinutes, policy.MarginPercent));
    }
    public static SchedulingPlacement Find(CapacityLedger ledger, Guid person, OperationalAssignment task, DateTimeOffset notBefore)
    {
        var preferred = FindForward(ledger, person, task, notBefore, false);
        if (preferred.Eligible || !ledger.Temporal.UsesBaseSchedule(task) || task.OperationalCriticality != "FLEXIBLE") return preferred;
        return FindForward(ledger, person, task, notBefore, true);
    }
    public static SchedulingPlacement FindExact(CapacityLedger ledger, Guid person, OperationalAssignment task) =>
        FindForward(ledger, person, task, task.StartsAt, ledger.Temporal.UsesBaseSchedule(task) && task.OperationalCriticality == "FLEXIBLE");

    // Finite minute domain for explicit preparation; saved peers are immutable inputs.
    public static IEnumerable<SchedulingPlacement> Alternatives(CapacityLedger ledger, Guid person, OperationalAssignment task, DateTimeOffset notBefore)
    {
        if (task.ScheduleMode == "PROGRESO" || task.SchedulingPolicyId == null || task.OperationalCriticality == "CRITICA")
        {
            var only = Find(ledger, person, task, notBefore);
            if (only.Eligible) yield return only;
            yield break;
        }
        var cursor = notBefore;
        var end = OperationalSchedule.Instant(WeekContract.LocalToday(task.NominalStartsAt ?? task.StartsAt).AddDays(1));
        while (cursor < end)
        {
            var next = FindForward(ledger, person, task, cursor, ledger.Temporal.UsesBaseSchedule(task) && task.OperationalCriticality == "FLEXIBLE");
            if (!next.Eligible) yield break;
            yield return next;
            cursor = next.StartsAt.AddMinutes(1);
        }
    }

    private static SchedulingPlacement FindForward(CapacityLedger ledger, Guid person, OperationalAssignment task, DateTimeOffset notBefore, bool advance)
    {
        var nominal = task.NominalStartsAt ?? task.StartsAt;
        var day = WeekContract.LocalToday(nominal);
        SchedulingPlacement Fail(string reason) => new(false, nominal, task.DueAt, reason);
        if (task.SchedulingPolicyId == null) return new(true, task.StartsAt, task.DueAt, "LEGACY_CONTRACT");
        if (task.ScheduleMode == "PROGRESO") return new(true, task.StartsAt, task.DueAt, "WEEKLY_PROGRESS");
        var worker = ledger.Workers.SingleOrDefault(w => w.Id == person);
        if (worker == null) return Fail("SCHEDULE_ACCOUNT_INACTIVE");
        if (ledger.Unavailable.Contains((person, day))) return Fail("SCHEDULE_ABSENT");
        if (task.TaskId == ledger.Temporal.ClosingTaskId && ledger.Temporal.ClosingPerson(day) != person) return Fail("CLOSING_COVERAGE_REQUIRED");
        var intervals = ledger.Temporal.Day(person, day, ledger.Temporal.UsesCoverageSchedule(task)).Intervals;
        if (intervals == null) return Fail("SCHEDULE_MISSING");
        if (intervals.Length == 0) return Fail("SCHEDULE_ABSENT");
        var peers = ledger.SchedulingPeers(person, day).Where(t => t.PersonId == person && t.Id != task.Id && t.ScheduleMode != "PROGRESO" && t.GoalUnits == 0 && t.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") && t.StartsAt < OperationalSchedule.Instant(day.AddDays(1)) && t.DueAt > OperationalSchedule.Instant(day)).ToArray();
        if (peers.Any(t => t.SchedulingPolicyId == null && t.CompletedAt == null) || ledger.Legacy.GetValueOrDefault(person) > 0 && day == ledger.Today)
            return Fail("SCHEDULING_LEGACY_RESERVATION");
        var blocks = peers.Where(t => t.SchedulingPolicyId != null).OrderBy(t => t.StartsAt).ThenBy(t => t.DueAt).ToArray();
        var duration = Duration(task.LoadMinutes, task.ClosureMarginPercent ?? throw new DirectionPlanException("SCHEDULING_POLICY_INVALID", 422));
        var critical = task.OperationalCriticality == "CRITICA";
        var minimum = notBefore;
        var maximum = OperationalSchedule.Instant(day.AddDays(1));
        if (ledger.Temporal.UsesBaseSchedule(task))
            foreach (var peer in ledger.Tasks.Where(p => p.Id != task.Id && p.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") && WeekContract.LocalToday(p.StartsAt) == day))
                foreach (var relation in CashCircuit)
                {
                    if (task.TaskCode == relation.After && peer.TaskCode == relation.Before && peer.DueAt > minimum) minimum = peer.DueAt;
                    if (task.TaskCode == relation.Before && peer.TaskCode == relation.After && peer.StartsAt < maximum) maximum = peer.StartsAt;
                }
        if (critical && nominal < notBefore) return Fail("SCHEDULING_CRITICAL_ANCHOR_PASSED");
        foreach (var interval in intervals.OrderBy(i => i.Entry))
        {
            var entry = TemporalEligibilityContext.At(day, interval.Entry); var exit = TemporalEligibilityContext.At(day, interval.Exit);
            if (maximum < exit) exit = maximum;
            if (worker.EmploymentEnd is { } employment && employment < exit) exit = employment;
            if (worker.RoleEnd is { } role && role < exit) exit = role;
            var start = critical ? nominal : new[] { advance ? entry : nominal, minimum, entry }.Max();
            if (start < minimum) continue;
            if (start < entry || start >= exit) continue;
            foreach (var block in blocks)
            {
                if (block.DueAt <= start) continue;
                if (start.AddMinutes(duration) <= block.StartsAt) break;
                if (start < block.DueAt && start.AddMinutes(duration) > block.StartsAt)
                {
                    if (critical) return Fail("SCHEDULING_CRITICAL_OVERLAP");
                    start = block.DueAt;
                }
            }
            var due = start.AddMinutes(duration);
            if (due <= exit && (!critical || start == nominal)) return new(true, start, due, "ELIGIBLE");
        }
        return Fail("SCHEDULING_NO_CONTIGUOUS_SLOT");
    }
    // Explicit circuit captured by agenda policy 2, across recipients in the branch/day.
    public static readonly (string Before, string After)[] CashCircuit = [("TAR-0004", "TAR-0032"), ("TAR-0032", "TAR-0200")];
    public static bool CircuitFits(IEnumerable<OperationalAssignment> peers, OperationalAssignment task, DateTimeOffset start, DateTimeOffset due) =>
        peers.Where(p => p.Id != task.Id && p.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") && WeekContract.LocalToday(p.StartsAt) == WeekContract.LocalToday(start))
            .All(p => CashCircuit.All(r => (task.TaskCode != r.Before || p.TaskCode != r.After || due <= p.StartsAt) &&
                (task.TaskCode != r.After || p.TaskCode != r.Before || p.DueAt <= start)));
    public static void Apply(OperationalAssignment task, SchedulingPlacement placement)
    {
        if (!placement.Eligible) throw new DirectionPlanException(placement.Reason, 422);
        task.StartsAt = placement.StartsAt; task.DueAt = placement.DueAt;
    }
}
