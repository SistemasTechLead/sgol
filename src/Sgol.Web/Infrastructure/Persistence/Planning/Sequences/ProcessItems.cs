using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ProcessItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public long Revision { get; set; } = 1;
}

public sealed class ProcessItemRate
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public long Revision { get; set; }
    public string Name { get; set; } = "";
    public int Minutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed record ProcessItemSnapshot(Guid ItemId, Guid RateId, long Revision, string Name, int Minutes);
public sealed record ProcessItemEntry(ProcessItem Item, ProcessItemRate Rate);

public static class ProcessItemRules
{
    public static long Calculate(int? quantity, int minutes)
    {
        if (quantity is null or < 0 || minutes < 1) throw new DirectionPlanException("PROCESS_ITEM_QUANTITY_INVALID", 422);
        return checked((long)quantity.Value * minutes);
    }
    public static bool CanResolve(string originRole) => originRole is "SUBCOORDINACION" or "DIRECCION";
    public static void Configure(ModelBuilder model)
    {
        var item = model.Entity<ProcessItem>();
        item.ToTable("process_item", t => t.HasCheckConstraint("CK_process_item", "length(trim(\"Name\")) > 0 AND \"Revision\" > 0"));
        item.HasKey(x => x.Id); item.Property(x => x.Name).HasMaxLength(120); item.Property(x => x.Revision).IsConcurrencyToken();
        var rate = model.Entity<ProcessItemRate>();
        rate.ToTable("process_item_rate", t => t.HasCheckConstraint("CK_process_item_rate", "\"Minutes\" > 0 AND \"Revision\" > 0"));
        rate.HasKey(x => x.Id); rate.HasIndex(x => new { x.ItemId, x.Revision }).IsUnique();
        rate.Property(x => x.Name).HasMaxLength(120);
        rate.HasOne<ProcessItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        rate.HasOne<Sgol.Web.Infrastructure.Persistence.Bootstrap.AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OperationalSequenceStep>().HasOne<ProcessItem>().WithMany().HasForeignKey(x => x.OutputItemId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OperationalSequenceStage>().HasOne<ProcessItemRate>().WithMany().HasForeignKey(x => x.OutputItemRateId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OperationalSequenceStage>().Property(x => x.OutputItemName).HasMaxLength(120);
        model.Entity<OperationalSequenceStage>().HasOne<OperationalSequenceAttempt>().WithMany().HasForeignKey(x => new { x.RunId, x.LoadSourceAttemptId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        model.Entity<OperationalSequenceAttempt>().HasOne<OperationalSequenceAttempt>().WithMany().HasForeignKey(x => new { x.RunId, x.StageId, x.ContinuationAttemptId }).HasPrincipalKey(x => new { x.RunId, x.StageId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        model.Entity<PreparedProcessStart>().Property(x => x.ItemRatesJson).HasColumnType("jsonb");
        var step = model.Entity<OperationalSequenceStep>();
        step.HasAlternateKey(x => new { x.VersionId, x.TaskId });
        step.HasOne<OperationalSequenceStep>().WithMany().HasForeignKey(x => new { x.VersionId, x.OutputTargetTaskId })
            .HasPrincipalKey(x => new { x.VersionId, x.TaskId }).OnDelete(DeleteBehavior.NoAction);
        step.HasIndex(x => x.VersionId).IsUnique().HasFilter("\"IsFinalAudit\"");
        step.ToTable("operational_sequence_step", t => t.HasCheckConstraint("CK_process_item_link", "(\"OutputItemId\" IS NULL AND \"OutputTargetTaskId\" IS NULL) OR (\"OutputItemId\" IS NOT NULL AND \"OutputTargetTaskId\" IS NOT NULL AND \"OutputTargetTaskId\" <> \"TaskId\" AND NOT \"IsFinalAudit\")"));
        model.Entity<OperationalSequenceAttempt>().ToTable("operational_sequence_attempt", t => {
            t.HasCheckConstraint("CK_process_item_quantity", "\"ItemQuantity\" IS NULL OR \"ItemQuantity\" >= 0");
            t.HasCheckConstraint("CK_process_item_resolution", "\"CapacityResolution\" IS NULL OR \"CapacityResolution\" IN ('','PENDING','LOWEST','FORCE')");
            t.HasCheckConstraint("CK_process_item_continuation", "\"ContinuationAttemptId\" IS NULL OR \"ContinuationAttemptId\" <> \"Id\"");
        });
        model.Entity<OperationalSequenceStage>().ToTable("operational_sequence_stage", t => {
            t.HasCheckConstraint("CK_process_item_result", "(\"CalculatedLoadMinutes\" IS NULL AND \"LoadSourceAttemptId\" IS NULL) OR (\"CalculatedLoadMinutes\" IS NOT NULL AND \"CalculatedLoadMinutes\" > 0 AND \"LoadSourceAttemptId\" IS NOT NULL AND (\"CalculatedLoadMinutes\" > 4800 OR \"LoadMinutes\" = \"CalculatedLoadMinutes\"))");
            t.HasCheckConstraint("CK_process_item_snapshot", "(\"OutputItemRateId\" IS NULL AND \"OutputItemName\" IS NULL AND \"OutputItemMinutes\" IS NULL) OR (\"OutputItemRateId\" IS NOT NULL AND \"OutputItemName\" IS NOT NULL AND \"OutputItemMinutes\" IS NOT NULL AND \"OutputItemMinutes\" > 0 AND NOT \"IsFinalAudit\")");
        });
    }

    public static async Task<Dictionary<int, ProcessItemSnapshot>> CaptureAsync(SgolDbContext db, OperationalSequenceStep[] steps, CancellationToken ct)
    {
        var ids = steps.Where(s => s.OutputItemId != null).Select(s => s.OutputItemId!.Value).Distinct().ToArray();
        var items = await db.Set<ProcessItem>().AsNoTracking().Where(i => ids.Contains(i.Id)).ToArrayAsync(ct);
        var rates = await db.Set<ProcessItemRate>().AsNoTracking().Where(i => ids.Contains(i.ItemId)).ToArrayAsync(ct);
        return steps.Where(s => s.OutputItemId != null).ToDictionary(s => s.Position, s => {
            var item = items.Single(i => i.Id == s.OutputItemId);
            var rate = rates.Single(r => r.ItemId == item.Id && r.Revision == item.Revision);
            return new ProcessItemSnapshot(item.Id, rate.Id, rate.Revision, rate.Name, rate.Minutes);
        });
    }
}

public sealed class ProcessItemCatalogService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public async Task<ProcessItemEntry[]> ListAsync(Guid actor, CancellationToken ct)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, ct);
        return await (from item in db.Set<ProcessItem>().AsNoTracking()
                      join rate in db.Set<ProcessItemRate>().AsNoTracking() on new { ItemId = item.Id, item.Revision } equals new { rate.ItemId, rate.Revision }
                      orderby item.Name select new ProcessItemEntry(item, rate)).ToArrayAsync(ct);
    }
    public async Task SaveAsync(Guid actor, Guid id, long revision, string name, int minutes, bool active, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl) || minutes < 1)
            throw new DirectionPlanException("PROCESS_ITEM_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationalTaskService.LockAsync(db, ct); await direction.AuthorizeAsync(actor, ct);
        var item = await db.Set<ProcessItem>().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item == null) {
            if (revision != 0) throw new DirectionPlanException("PROCESS_CONFLICT", 409);
            item = new() { Id = id }; db.Add(item);
        } else {
            if (item.Revision != revision) throw new DirectionPlanException("PROCESS_CONFLICT", 409);
            item.Revision++;
        }
        item.Name = name.Trim(); item.Active = active;
        var rate = new ProcessItemRate { Id = Guid.CreateVersion7(), ItemId = id, Revision = item.Revision, Name = item.Name, Minutes = minutes, CreatedBy = actor, CreatedAt = clock.UtcNow };
        db.Add(rate);
        db.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
            ResourceType = "PROCESS_ITEM", ResourceId = id, BranchId = BranchScope.LorettaId, Action = "PROCESS_ITEM_CHANGED", Outcome = "SUCCESS", CorrelationId = rate.Id,
            AfterData = JsonSerializer.SerializeToDocument(new { item.Name, item.Active, item.Revision, minutes }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
