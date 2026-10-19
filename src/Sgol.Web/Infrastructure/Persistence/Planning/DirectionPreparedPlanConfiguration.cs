using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed class DirectionPreparedPlanConfiguration : IEntityTypeConfiguration<DirectionPreparedPlan>
{
    public void Configure(EntityTypeBuilder<DirectionPreparedPlan> b)
    {
        b.ToTable("direction_prepared_plan", t =>
        {
            t.HasCheckConstraint("CK_direction_plan_status", "status IN ('BORRADOR','ASIGNADO')");
            t.HasCheckConstraint("CK_direction_plan_range", "ends_on >= starts_on AND year BETWEEN 1 AND 9999");
            t.HasCheckConstraint("CK_direction_plan_period", "(period_number IS NOT NULL AND ((period_type = 'SEMANAL' AND period_number BETWEEN 1 AND 53) OR (period_type = 'MENSUAL' AND period_number BETWEEN 1 AND 12) OR (period_type = 'TRIMESTRAL' AND period_number BETWEEN 1 AND 4) OR (period_type = 'SEMESTRAL' AND period_number BETWEEN 1 AND 2))) OR (period_type IN ('ANUAL','OCASIONAL') AND period_number IS NULL)");
        });
        b.HasKey(p => p.Id); b.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(p => p.BranchId).HasColumnName("branch_id"); b.Property(p => p.CreatedBy).HasColumnName("created_by");
        b.Property(p => p.CreatedAt).HasColumnName("created_at");
        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(240);
        b.Property(p => p.Area).HasColumnName("area").HasMaxLength(240);
        b.Property(p => p.PeriodType).HasColumnName("period_type").HasMaxLength(16);
        b.Property(p => p.Year).HasColumnName("year"); b.Property(p => p.PeriodNumber).HasColumnName("period_number");
        b.Property(p => p.StartsOn).HasColumnName("starts_on"); b.Property(p => p.EndsOn).HasColumnName("ends_on");
        b.Property(p => p.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(p => p.IsWeeklyOperationsPlan).HasColumnName("is_weekly_operations_plan").HasDefaultValue(false);
        b.Property(p => p.MonthlyPendingJson).HasColumnType("jsonb").HasDefaultValue("[]");
        b.HasIndex(p => new { p.BranchId, p.Year, p.PeriodNumber }).IsUnique().HasFilter("is_weekly_operations_plan");
        b.HasOne<AppUser>().WithMany().HasForeignKey(p => p.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Branch>().WithMany().HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.CreatedAt, p.Id });
    }
}

internal sealed class DirectionPreparedPlanItemConfiguration : IEntityTypeConfiguration<DirectionPreparedPlanItem>
{
    public void Configure(EntityTypeBuilder<DirectionPreparedPlanItem> b)
    {
        b.ToTable("direction_prepared_plan_item", t => t.HasCheckConstraint("CK_direction_plan_item_position", "position BETWEEN 0 AND 999"));
        b.HasKey(p => new { p.PlanId, p.TaskCode });
        b.Property(p => p.PlanId).HasColumnName("plan_id");
        b.Property(p => p.TaskCode).HasColumnName("task_code").HasMaxLength(16);
        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(500);
        b.Property(p => p.Position).HasColumnName("position");
        b.HasOne<OperationalTask>().WithMany().HasForeignKey(p => p.TaskCode).HasPrincipalKey(t => t.TaskCode).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.PlanId, p.Position }).IsUnique();
        b.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(p => p.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
