using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// The source capacity ID is a historical identifier: that mutable row may later be split or removed.
public sealed class StoredPlanCapacityAdjustment
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid SourceCapacityId { get; set; }
    public Guid PersonId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public int Percent { get; set; }
    public int DailyMinutes { get; set; }
    public string Reason { get; set; } = "";
    public long? SourceRevision { get; set; }
    public List<PlanCapacityPreviousSegment> PreviousSegments { get; set; } = [];

    public static StoredPlanCapacityAdjustment Capture(Guid id, Guid planId, DateTimeOffset at,
        OperationalCapacity setting, IReadOnlyList<OperationalCapacity> previous) => new() {
        Id = id, PlanId = planId, CreatedAt = at, SourceCapacityId = setting.Id, PersonId = setting.PersonId,
        From = setting.From, Through = setting.Through, Percent = setting.Percent,
        DailyMinutes = setting.DailyMinutes, Reason = setting.Reason, SourceRevision = setting.Revision,
        PreviousSegments = previous.Select((p, i) => new PlanCapacityPreviousSegment {
            AdjustmentId = id, Position = i, From = p.From, Through = p.Through,
            Percent = p.Percent, DailyMinutes = p.DailyMinutes, Reason = p.Reason, Revision = p.Revision
        }).ToList()
    };
}

public sealed class PlanCapacityPreviousSegment
{
    public Guid AdjustmentId { get; set; }
    public int Position { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public int Percent { get; set; }
    public int DailyMinutes { get; set; }
    public string Reason { get; set; } = "";
    public long Revision { get; set; }
}

internal sealed class StoredPlanCapacityAdjustmentConfiguration : IEntityTypeConfiguration<StoredPlanCapacityAdjustment>
{
    public void Configure(EntityTypeBuilder<StoredPlanCapacityAdjustment> b)
    {
        b.ToTable("plan_capacity_adjustment", t => t.HasCheckConstraint("CK_plan_capacity_values",
            "\"Through\" >= \"From\" AND \"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.PlanId, x.CreatedAt, x.Id });
        b.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.HasMany(x => x.PreviousSegments).WithOne().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PlanCapacityPreviousSegmentConfiguration : IEntityTypeConfiguration<PlanCapacityPreviousSegment>
{
    public void Configure(EntityTypeBuilder<PlanCapacityPreviousSegment> b)
    {
        b.ToTable("plan_capacity_previous_segment", t => t.HasCheckConstraint("CK_plan_capacity_previous_values",
            "\"Position\" >= 0 AND \"Through\" >= \"From\" AND \"Percent\" BETWEEN 0 AND 100 AND \"DailyMinutes\" BETWEEN 1 AND 480"));
        b.HasKey(x => new { x.AdjustmentId, x.Position });
        b.Property(x => x.Reason).HasMaxLength(1000);
    }
}
