using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EvidenceRequirementCatalogConfiguration : IEntityTypeConfiguration<EvidenceRequirementCatalogEntry>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirementCatalogEntry> builder)
    {
        builder.ToTable(
            "evidence_requirement_catalog",
            table =>
            {
                table.HasCheckConstraint("CK_evidence_requirement_catalog_ordinal", "ordinal > 0");
                table.HasCheckConstraint(
                    "CK_evidence_requirement_catalog_kind",
                    "kind IN ('REGISTRO_DIGITAL','DOCUMENTO_REFERENCIADO','FOTOGRAFIA','FORMULARIO_REFERENCIADO','CHECKLIST_ESTRUCTURADO','DATO_ESTRUCTURADO')");
                table.HasCheckConstraint(
                    "CK_evidence_requirement_catalog_condition",
                    "condition_code IN ('SIEMPRE','DIFERENCIA_O_DANO')");
                table.HasCheckConstraint(
                    "CK_evidence_requirement_catalog_conditional_photo",
                    "condition_code <> 'DIFERENCIA_O_DANO' OR (requirement_code = 'FOTO_DIFERENCIA_DANO' AND kind = 'FOTOGRAFIA')");
            });

        builder.HasKey(item => new { item.TaskDefinitionId, item.RequirementCode });
        builder.Property(item => item.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(item => item.RequirementCode).HasColumnName("requirement_code").HasMaxLength(64);
        builder.Property(item => item.Kind).HasColumnName("kind").HasMaxLength(32);
        builder.Property(item => item.ConditionCode).HasColumnName("condition_code").HasMaxLength(32);
        builder.Property(item => item.Ordinal).HasColumnName("ordinal");

        builder.HasIndex(item => new { item.TaskDefinitionId, item.Ordinal })
            .IsUnique()
            .HasDatabaseName("IX_evidence_requirement_catalog_task_ordinal");
        builder.HasAlternateKey(item => new
        {
            item.TaskDefinitionId,
            item.RequirementCode,
            item.Kind,
            item.ConditionCode,
        })
            .HasName("AK_evidence_requirement_catalog_contract_key");
        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(item => item.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        var seeds = EvidencePolicyCatalog.All.SelectMany(pair =>
        {
            var taskId = TaskDefinitionCatalog.Require(pair.Key).Id;
            return pair.Value.Select(item => new EvidenceRequirementCatalogEntry(
                taskId,
                item.Code,
                item.Kind,
                item.ConditionCode,
                item.Ordinal));
        });
        builder.HasData(seeds);
    }
}

public sealed class EvidencePolicyVersionConfiguration : IEntityTypeConfiguration<EvidencePolicyVersion>
{
    public void Configure(EntityTypeBuilder<EvidencePolicyVersion> builder)
    {
        builder.HasKey(policy => policy.Id);
        builder.HasAlternateKey(policy => new { policy.Id, policy.TaskDefinitionId })
            .HasName("AK_evidence_policy_version_id_task_definition_id");
        builder.Property(policy => policy.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(policy => policy.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(policy => policy.TaskDefinitionVersionId).HasColumnName("task_definition_version_id");
        builder.Property(policy => policy.ReleaseId).HasColumnName("release_id");
        builder.Property(policy => policy.BasedOnId).HasColumnName("based_on_id");
        builder.Property(policy => policy.VersionNo).HasColumnName("version_no");

        builder.ConfigureVersioning("evidence_policy_version", nameof(EvidencePolicyVersion.TaskDefinitionId));
        builder.Property(policy => policy.Status).HasMaxLength(16);
        builder.Property(policy => policy.Reason).HasMaxLength(500);

        builder.HasIndex(policy => new { policy.TaskDefinitionId, policy.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_evidence_policy_version_number");
        builder.HasIndex(policy => new { policy.ReleaseId, policy.TaskDefinitionId })
            .IsUnique()
            .HasDatabaseName("IX_evidence_policy_version_release_task");
        builder.HasIndex(policy => new { policy.TaskDefinitionVersionId, policy.TaskDefinitionId })
            .HasDatabaseName("IX_evidence_policy_version_exact_task_version");

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
        builder.HasOne<EvidencePolicyVersion>()
            .WithMany()
            .HasForeignKey(policy => policy.BasedOnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "evidence_policy_version",
            table => table.HasCheckConstraint("CK_evidence_policy_version_number", "version_no > 0"));
    }
}

public sealed class EvidenceRequirementVersionConfiguration : IEntityTypeConfiguration<EvidenceRequirementVersion>
{
    public void Configure(EntityTypeBuilder<EvidenceRequirementVersion> builder)
    {
        builder.ToTable(
            "evidence_requirement_version",
            table =>
            {
                table.HasCheckConstraint("CK_evidence_requirement_version_ordinal", "ordinal > 0");
                table.HasCheckConstraint("CK_evidence_requirement_version_required", "is_required");
            });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.PolicyVersionId).HasColumnName("policy_version_id");
        builder.Property(item => item.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(item => item.RequirementCode).HasColumnName("requirement_code").HasMaxLength(64);
        builder.Property(item => item.Kind).HasColumnName("kind").HasMaxLength(32);
        builder.Property(item => item.ConditionCode).HasColumnName("condition_code").HasMaxLength(32);
        builder.Property(item => item.Ordinal).HasColumnName("ordinal");
        builder.Property(item => item.IsRequired).HasColumnName("is_required");

        builder.HasIndex(item => new { item.PolicyVersionId, item.RequirementCode })
            .IsUnique()
            .HasDatabaseName("IX_evidence_requirement_version_policy_code");
        builder.HasIndex(item => new { item.PolicyVersionId, item.Ordinal })
            .IsUnique()
            .HasDatabaseName("IX_evidence_requirement_version_policy_ordinal");
        builder.HasOne<EvidencePolicyVersion>()
            .WithMany()
            .HasForeignKey(item => new { item.PolicyVersionId, item.TaskDefinitionId })
            .HasPrincipalKey(policy => new { policy.Id, policy.TaskDefinitionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceRequirementCatalogEntry>()
            .WithMany()
            .HasForeignKey(item => new
            {
                item.TaskDefinitionId,
                item.RequirementCode,
                item.Kind,
                item.ConditionCode,
            })
            .HasPrincipalKey(catalog => new
            {
                catalog.TaskDefinitionId,
                catalog.RequirementCode,
                catalog.Kind,
                catalog.ConditionCode,
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
