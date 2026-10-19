using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class PlanVersionObligationConfiguration : IEntityTypeConfiguration<PlanVersionObligation>
{
    public void Configure(EntityTypeBuilder<PlanVersionObligation> builder)
    {
        builder.ToTable("plan_version_obligation");
        builder.HasKey(item => new { item.PlanVersionId, item.ObligationId });
        builder.Property(item => item.PlanVersionId).HasColumnName("plan_version_id");
        builder.Property(item => item.ObligationId).HasColumnName("obligation_id");
        builder.Property(item => item.AssignmentVersionId).HasColumnName("assignment_version_id");
        builder.HasOne<PlanVersion>().WithMany().HasForeignKey(item => item.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkObligation>().WithMany().HasForeignKey(item => item.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentVersion>().WithMany()
            .HasForeignKey(item => new { item.AssignmentVersionId, item.ObligationId })
            .HasPrincipalKey(assignment => new { assignment.Id, assignment.ObligationId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
