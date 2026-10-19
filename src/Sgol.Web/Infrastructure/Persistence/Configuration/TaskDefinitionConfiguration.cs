using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class TaskDefinitionConfiguration : IEntityTypeConfiguration<TaskDefinition>
{
    public void Configure(EntityTypeBuilder<TaskDefinition> builder)
    {
        builder.HasKey(definition => definition.Id);
        builder.Property(definition => definition.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(definition => definition.TaskCode).HasColumnName("task_code").HasMaxLength(8);
        builder.Property(definition => definition.Name).HasColumnName("name").HasMaxLength(200);
        builder.HasIndex(definition => definition.TaskCode)
            .IsUnique()
            .HasDatabaseName("IX_task_definition_task_code");
        builder.ToTable(
            "task_definition",
            table => table.HasCheckConstraint(
                "CK_task_definition_mvp_code",
                "task_code IN ('TAR-0005','TAR-0007','TAR-0008','TAR-0011','TAR-0018','TAR-0026','TAR-0092','TAR-0093')"));
        builder.HasData(TaskDefinitionCatalog.All.Select(seed => new
        {
            seed.Id,
            seed.TaskCode,
            seed.Name,
        }));
    }
}

public sealed class TaskDefinitionVersionConfiguration : IEntityTypeConfiguration<TaskDefinitionVersion>
{
    public void Configure(EntityTypeBuilder<TaskDefinitionVersion> builder)
    {
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(version => version.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(version => version.VersionNo).HasColumnName("version_no");
        builder.Property(version => version.Status).HasColumnName("status").HasMaxLength(20);
        builder.Property(version => version.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone");
        builder.Property(version => version.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone");
        builder.Property(version => version.SchemaVersion).HasColumnName("schema_version");
        builder.Property(version => version.TaskPayload).HasColumnName("task_payload").HasColumnType("jsonb");
        builder.Property(version => version.ReleaseId).HasColumnName("release_id");
        builder.Property(version => version.Reason).HasColumnName("reason");
        builder.Property(version => version.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(version => version.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        builder.HasIndex(version => new { version.TaskDefinitionId, version.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_task_definition_version_number");
        builder.HasAlternateKey(version => new { version.Id, version.TaskDefinitionId })
            .HasName("AK_task_definition_version_id_task_definition_id");
        builder.HasIndex(version => version.TaskDefinitionId)
            .IsUnique()
            .HasDatabaseName("IX_task_definition_version_one_live")
            .HasFilter("status IN ('VIGENTE', 'INACTIVA_NUEVAS')");
        builder.HasIndex(version => version.SupersedesId)
            .IsUnique()
            .HasDatabaseName("IX_task_definition_version_supersedes_id")
            .HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(version => new { version.ReleaseId, version.TaskDefinitionId })
            .IsUnique()
            .HasDatabaseName("IX_task_definition_version_release_definition");

        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(version => version.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationRelease>()
            .WithMany()
            .HasForeignKey(version => version.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskDefinitionVersion>()
            .WithOne()
            .HasForeignKey<TaskDefinitionVersion>(version => version.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "task_definition_version",
            table =>
            {
                table.HasCheckConstraint("CK_task_definition_version_number", "version_no > 0");
                table.HasCheckConstraint("CK_task_definition_version_schema", "schema_version = 1 AND task_payload = '{}'::jsonb");
                table.HasCheckConstraint("CK_task_definition_version_row_version", "row_version > 0");
                table.HasCheckConstraint(
                    "CK_task_definition_version_status",
                    "status IN ('BORRADOR','VIGENTE','SUSTITUIDA','INACTIVA_NUEVAS')");
                table.HasCheckConstraint(
                    "CK_task_definition_version_lifecycle",
                    "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL AND reason IS NULL AND supersedes_id IS NULL) OR " +
                    "(status IN ('VIGENTE','INACTIVA_NUEVAS') AND effective_from IS NOT NULL AND effective_to IS NULL AND btrim(reason) <> '') OR " +
                    "(status = 'SUSTITUIDA' AND effective_from IS NOT NULL AND effective_to > effective_from AND btrim(reason) <> '')");
                table.HasCheckConstraint(
                    "CK_task_definition_version_not_self_superseding",
                    "supersedes_id IS NULL OR supersedes_id <> id");
            });
    }
}
