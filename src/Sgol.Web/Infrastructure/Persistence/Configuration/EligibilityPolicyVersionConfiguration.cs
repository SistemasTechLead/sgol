using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EligibilityPolicyVersionConfiguration : IEntityTypeConfiguration<EligibilityPolicyVersion>
{
    public void Configure(EntityTypeBuilder<EligibilityPolicyVersion> builder)
    {
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(policy => policy.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(policy => policy.TaskDefinitionVersionId).HasColumnName("task_definition_version_id");
        builder.Property(policy => policy.ReleaseId).HasColumnName("release_id");
        builder.Property(policy => policy.BasedOnId).HasColumnName("based_on_id");
        builder.Property(policy => policy.VersionNo).HasColumnName("version_no");
        builder.Property(policy => policy.RequiredRole).HasColumnName("required_role").HasMaxLength(32);
        builder.Property(policy => policy.RequiresAvailability).HasColumnName("requires_availability");
        builder.Property(policy => policy.RequiredShift).HasColumnName("required_shift").HasMaxLength(80);

        builder.ConfigureVersioning("eligibility_policy_version", nameof(EligibilityPolicyVersion.TaskDefinitionId));

        builder.HasIndex(policy => new { policy.TaskDefinitionId, policy.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_eligibility_policy_version_number");
        builder.HasIndex(policy => new { policy.ReleaseId, policy.TaskDefinitionId })
            .IsUnique()
            .HasDatabaseName("IX_eligibility_policy_version_release_task");
        builder.HasIndex(policy => new { policy.TaskDefinitionVersionId, policy.TaskDefinitionId })
            .HasDatabaseName("IX_eligibility_policy_version_exact_task_version");

        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(policy => policy.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskDefinitionVersion>()
            .WithMany()
            .HasForeignKey(policy => new { policy.TaskDefinitionVersionId, policy.TaskDefinitionId })
            .HasPrincipalKey(version => new { version.Id, version.TaskDefinitionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationRelease>()
            .WithMany()
            .HasForeignKey(policy => policy.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EligibilityPolicyVersion>()
            .WithMany()
            .HasForeignKey(policy => policy.BasedOnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "eligibility_policy_version",
            table =>
            {
                table.HasCheckConstraint("CK_eligibility_policy_version_number", "version_no > 0");
                table.HasCheckConstraint(
                    "CK_eligibility_policy_version_role",
                    "required_role IN ('ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS')");
                table.HasCheckConstraint(
                    "CK_eligibility_policy_version_availability",
                    "requires_availability");
                table.HasCheckConstraint(
                    "CK_eligibility_policy_version_shift",
                    "required_shift IS NULL");
            });
    }
}
