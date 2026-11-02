using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Independent history: no FK to mutable/deletable operational assignments, and no evidence content.
public sealed class TaskBonusFact
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AssignmentId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Kind { get; set; } = "STATE";
    public string SnapshotJson { get; set; } = "{}";
}
public sealed class TaskBonusClosure
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PersonId { get; set; }
    public DateOnly Week { get; set; }
    public string RulesVersion { get; set; } = TaskBonusCalculator.RulesVersion;
    public DateTimeOffset ClosedAt { get; set; }
    public string ResultJson { get; set; } = "{}";
}
public sealed class TaskBonusExclusion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ObligationId { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly Week { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Reason { get; set; } = "";
}
public sealed class TaskBonusAvailabilityFact
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly Day { get; set; }
    public bool Available { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed record TaskBonusAssignmentSnapshot(Guid Id, Guid? ReassignedFromId, Guid PersonId, string PersonName,
    string TaskCode, string Name, string? Role, DateTimeOffset StartsAt, DateTimeOffset DueAt,
    string? Criticality, string ScheduleMode, int GoalUnits, int LoadMinutes,
    DateTimeOffset? CompletedAt, Guid? CompletedBy, string? CompletionKind, DateTimeOffset? ReviewedAt,
    Guid? ReviewedBy, string? ReviewStatus, DateTimeOffset? RetiredAt, string? ScheduleKey, Guid? ProcessStageId = null)
{
    public static TaskBonusAssignmentSnapshot From(OperationalAssignment a) => new(a.Id, a.ReassignedFromId,
        a.PersonId, a.PersonName, a.TaskCode, a.Name, a.AssignedRole, a.StartsAt, a.DueAt,
        a.OperationalCriticality, a.ScheduleMode, a.GoalUnits, a.LoadMinutes, a.CompletedAt, a.CompletedBy,
        a.CompletionKind, a.ReviewedAt, a.ReviewedBy, a.ReviewStatus, a.RetiredAt, a.ScheduleKey);
}

public static class TaskBonusPersistence
{
    public static void Configure(ModelBuilder model)
    {
        var f = model.Entity<TaskBonusFact>();
        f.ToTable("task_bonus_fact"); f.HasKey(x => x.Id);
        f.HasIndex(x => new { x.AssignmentId, x.Revision, x.Kind }).IsUnique();
        f.HasIndex(x => x.RecordedAt);
        f.Property(x => x.Kind).HasMaxLength(16); f.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        var c = model.Entity<TaskBonusClosure>();
        c.ToTable("task_bonus_closure"); c.HasKey(x => x.Id);
        c.HasIndex(x => new { x.PersonId, x.Week, x.RulesVersion }).IsUnique();
        c.Property(x => x.RulesVersion).HasMaxLength(64); c.Property(x => x.ResultJson).HasColumnType("jsonb");
        var e = model.Entity<TaskBonusExclusion>();
        e.ToTable("task_bonus_exclusion"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.ObligationId, x.PersonId, x.Week }).IsUnique();
        e.Property(x => x.Reason).HasMaxLength(1000);
        var a = model.Entity<TaskBonusAvailabilityFact>();
        a.ToTable("task_bonus_availability_fact"); a.HasKey(x => x.Id);
        a.HasIndex(x => new { x.PersonId, x.Day, x.RecordedAt });
    }

    public static void Capture(SgolDbContext db, DateTimeOffset now)
    {
        db.ChangeTracker.DetectChanges();
        foreach (var transfer in db.ChangeTracker.Entries<OperationalTransfer>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray())
            if (!db.ChangeTracker.Entries<TaskBonusFact>().Any(e => e.Entity.Id == transfer.Id))
                db.Add(new TaskBonusFact { Id = transfer.Id, AssignmentId = transfer.AssignmentId,
                    Revision = transfer.At.UtcTicks, RecordedAt = now, Kind = "HANDOFF", SnapshotJson = JsonSerializer.Serialize(transfer) });
        foreach (var item in db.ChangeTracker.Entries<Sgol.Organization.Contracts.AvailabilityDayVersion>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray())
            if (!db.ChangeTracker.Entries<TaskBonusAvailabilityFact>().Any(e => e.Entity.Id == item.Id))
                db.Add(new TaskBonusAvailabilityFact { Id = item.Id, PersonId = item.PersonId, Day = item.LocalDate,
                    Available = item.IsAvailable, RecordedAt = now });
        foreach (var entry in db.ChangeTracker.Entries<OperationalAssignment>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            var state = TaskBonusAssignmentSnapshot.From(entry.Entity);
            if (entry.State == EntityState.Modified && state == TaskBonusAssignmentSnapshot.From((OperationalAssignment)entry.OriginalValues.ToObject()))
                continue; // Evidence drafts and unrelated metadata never produce economic events.
            var stage = db.ChangeTracker.Entries<OperationalSequenceAttempt>().Where(a => a.Entity.AssignmentId == state.Id)
                .Select(a => (Guid?)a.Entity.StageId).FirstOrDefault() ?? db.Set<OperationalSequenceAttempt>().AsNoTracking()
                .Where(a => a.AssignmentId == state.Id).Select(a => (Guid?)a.StageId).FirstOrDefault();
            state = state with { ProcessStageId = stage };
            var kind = entry.State == EntityState.Deleted ? "DELETED" : "STATE";
            var pending = db.ChangeTracker.Entries<TaskBonusFact>().FirstOrDefault(f => f.State == EntityState.Added &&
                f.Entity.AssignmentId == state.Id && f.Entity.Revision == entry.Entity.Revision && f.Entity.Kind == kind);
            var fact = pending?.Entity ?? new TaskBonusFact { AssignmentId = state.Id, Revision = entry.Entity.Revision, Kind = kind };
            fact.RecordedAt = now; fact.SnapshotJson = JsonSerializer.Serialize(state);
            if (pending == null) db.Add(fact);
        }
        db.PendingBonusFacts.UnionWith(db.ChangeTracker.Entries<TaskBonusFact>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id));
        db.PendingBonusAvailability.UnionWith(db.ChangeTracker.Entries<TaskBonusAvailabilityFact>().Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id));
        foreach (var exclusion in db.ChangeTracker.Entries<TaskBonusExclusion>().Where(e => e.State == EntityState.Added))
            db.PendingBonusExclusions[exclusion.Entity.Id] = exclusion.Entity.Week;
    }

    public static async Task CaptureDeletionAsync(SgolDbContext db, Guid planId, DateTimeOffset at, CancellationToken token)
    {
        var assignments = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.PlanId == planId).ToArrayAsync(token);
        db.AddRange(assignments.Select(a => new TaskBonusFact { AssignmentId = a.Id, Revision = a.Revision + 1,
            Kind = "DELETED", RecordedAt = at, SnapshotJson = JsonSerializer.Serialize(TaskBonusAssignmentSnapshot.From(a)) }));
        await db.SaveChangesAsync(token);
    }
}
