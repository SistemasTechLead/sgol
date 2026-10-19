using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed class WorkPlanConfiguration : IEntityTypeConfiguration<WorkPlan>
{
    public const string UniqueBranchPeriodIndex = "UX_work_plan_branch_period";

    public void Configure(EntityTypeBuilder<WorkPlan> builder)
    {
        builder.ToTable(
            "work_plan",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_work_plan_status",
                    "status IN ('BORRADOR','PUBLICADO')");
                table.HasCheckConstraint("CK_work_plan_row_version", "row_version >= 1");
            });
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(plan => plan.BranchId).HasColumnName("branch_id");
        builder.Property(plan => plan.PeriodId).HasColumnName("period_id");
        builder.Property(plan => plan.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(plan => plan.RowVersion).HasColumnName("row_version");
        builder.HasIndex(plan => new { plan.BranchId, plan.PeriodId })
            .IsUnique()
            .HasDatabaseName(UniqueBranchPeriodIndex);
        builder.HasOne<Branch>().WithMany().HasForeignKey(plan => plan.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WeekPeriod>().WithMany().HasForeignKey(plan => plan.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
