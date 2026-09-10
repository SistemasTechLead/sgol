using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class ValidationPolicyVersionConfiguration : IEntityTypeConfiguration<ValidationPolicyVersion>
{
    public void Configure(EntityTypeBuilder<ValidationPolicyVersion> builder)
    {
        builder.HasKey(policy => policy.Id);
        builder.HasAlternateKey(policy => new { policy.Id, policy.TaskDefinitionId })
            .HasName("AK_validation_policy_version_id_task_definition_id");
        builder.Property(policy => policy.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(policy => policy.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(policy => policy.TaskDefinitionVersionId).HasColumnName("task_definition_version_id");
        builder.Property(policy => policy.ReleaseId).HasColumnName("release_id");
        builder.Property(policy => policy.BasedOnId).HasColumnName("based_on_id");
        builder.Property(policy => policy.VersionNo).HasColumnName("version_no");
        builder.Property(policy => policy.IsRequired).HasColumnName("is_required");
        builder.Property(policy => policy.ExecutorRole).HasColumnName("executor_role").HasMaxLength(32);
        builder.Property(policy => policy.ValidatorRelation).HasColumnName("validator_relation").HasMaxLength(32);
        builder.Property(policy => policy.ValidatorRole).HasColumnName("validator_role").HasMaxLength(32);
        builder.Property(policy => policy.AllowedResults).HasColumnName("allowed_results").HasColumnType("jsonb");

        builder.ConfigureVersioning("validation_policy_version", nameof(ValidationPolicyVersion.TaskDefinitionId));
        builder.Property(policy => policy.Status).HasMaxLength(16);
        builder.Property(policy => policy.Reason).HasMaxLength(500);

        builder.HasIndex(policy => new { policy.TaskDefinitionId, policy.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_validation_policy_version_number");
        builder.HasIndex(policy => new { policy.ReleaseId, policy.TaskDefinitionId })
            .IsUnique()
            .HasDatabaseName("IX_validation_policy_version_release_task");
        builder.HasIndex(policy => new { policy.TaskDefinitionVersionId, policy.TaskDefinitionId })
            .HasDatabaseName("IX_validation_policy_version_exact_task_version");

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
        builder.HasOne<ValidationPolicyVersion>()
            .WithMany()
            .HasForeignKey(policy => new { policy.BasedOnId, policy.TaskDefinitionId })
            .HasPrincipalKey(policy => new { policy.Id, policy.TaskDefinitionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "validation_policy_version",
            table =>
            {
                table.HasCheckConstraint("CK_validation_policy_version_number", "version_no > 0");
                table.HasCheckConstraint("CK_validation_policy_version_required", "is_required");
                table.HasCheckConstraint(
                    "CK_validation_policy_version_relation",
                    "validator_relation = 'SUPERIOR_INMEDIATO'");
                table.HasCheckConstraint(
                    "CK_validation_policy_version_results",
                    "allowed_results = '[\"CUMPLIDA\",\"INCOMPLETA\",\"NO_CUMPLIDA\"]'::jsonb");
                table.HasCheckConstraint(
                    "CK_validation_policy_version_authority",
                    "(task_definition_id IN ('019d3a10-0007-7000-8000-000000000007', '019d3a10-0018-7000-8000-000000000018') AND executor_role = 'PISO_VENTAS' AND validator_role = 'SUBCOORDINACION') OR " +
                    "(task_definition_id IN ('019d3a10-0005-7000-8000-000000000005', '019d3a10-0008-7000-8000-000000000008', '019d3a10-0011-7000-8000-000000000011', '019d3a10-0092-7000-8000-000000000092', '019d3a10-0093-7000-8000-000000000093') AND executor_role = 'SUBCOORDINACION' AND validator_role = 'ADMINISTRACION') OR " +
                    "(task_definition_id = '019d3a10-0026-7000-8000-000000000026' AND executor_role = 'ADMINISTRACION' AND validator_role = 'DIRECCION')");
            });
    }
}
