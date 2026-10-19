using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class ActivationRuleVersionConfiguration : IEntityTypeConfiguration<ActivationRuleVersion>
{
    public void Configure(EntityTypeBuilder<ActivationRuleVersion> builder)
    {
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(rule => rule.TaskDefinitionId).HasColumnName("task_definition_id");
        builder.Property(rule => rule.TaskDefinitionVersionId).HasColumnName("task_definition_version_id");
        builder.Property(rule => rule.ReleaseId).HasColumnName("release_id");
        builder.Property(rule => rule.BasedOnId).HasColumnName("based_on_id");
        builder.Property(rule => rule.VersionNo).HasColumnName("version_no");
        builder.Property(rule => rule.Mode).HasColumnName("mode").HasMaxLength(16);
        builder.Property(rule => rule.Schedule).HasColumnName("schedule").HasColumnType("jsonb");
        builder.Property(rule => rule.OriginKeySchema).HasColumnName("origin_key_schema").HasMaxLength(64);

        builder.ConfigureVersioning("activation_rule_version", nameof(ActivationRuleVersion.TaskDefinitionId));

        builder.HasIndex(rule => new { rule.TaskDefinitionId, rule.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_activation_rule_version_number");
        builder.HasIndex(rule => new { rule.ReleaseId, rule.TaskDefinitionId })
            .IsUnique()
            .HasDatabaseName("IX_activation_rule_version_release_task");
        builder.HasIndex(rule => new { rule.TaskDefinitionVersionId, rule.TaskDefinitionId })
            .HasDatabaseName("IX_activation_rule_version_exact_task_version");

        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(rule => rule.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskDefinitionVersion>()
            .WithMany()
            .HasForeignKey(rule => new { rule.TaskDefinitionVersionId, rule.TaskDefinitionId })
            .HasPrincipalKey(version => new { version.Id, version.TaskDefinitionId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationRelease>()
            .WithMany()
            .HasForeignKey(rule => rule.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ActivationRuleVersion>()
            .WithMany()
            .HasForeignKey(rule => rule.BasedOnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "activation_rule_version",
            table =>
            {
                table.HasCheckConstraint("CK_activation_rule_version_number", "version_no > 0");
                table.HasCheckConstraint(
                    "CK_activation_rule_version_mode",
                    "mode IN ('MANUAL','RECURRENTE')");
                table.HasCheckConstraint(
                    "CK_activation_rule_version_origin_schema",
                    "origin_key_schema IN ('MANUAL_REFERENCE_V1','WORKING_DAY_WINDOW_V1','SERVICE_DUE_DATE_REFERENCE_V1')");
                table.HasCheckConstraint(
                    "CK_activation_rule_version_manual_schedule",
                    "(mode = 'MANUAL' AND schedule = 'null'::jsonb) OR (mode = 'RECURRENTE' AND jsonb_typeof(schedule) = 'object')");
                table.HasCheckConstraint(
                    "CK_activation_rule_version_task_policy",
                    "(task_definition_id = '019d3a10-0005-7000-8000-000000000005'::uuid AND mode = 'RECURRENTE' AND origin_key_schema = 'WORKING_DAY_WINDOW_V1') OR " +
                    "(task_definition_id IN ('019d3a10-0007-7000-8000-000000000007'::uuid,'019d3a10-0008-7000-8000-000000000008'::uuid,'019d3a10-0011-7000-8000-000000000011'::uuid,'019d3a10-0018-7000-8000-000000000018'::uuid,'019d3a10-0092-7000-8000-000000000092'::uuid,'019d3a10-0093-7000-8000-000000000093'::uuid) AND mode = 'MANUAL' AND origin_key_schema = 'MANUAL_REFERENCE_V1') OR " +
                    "(task_definition_id = '019d3a10-0026-7000-8000-000000000026'::uuid AND mode = 'RECURRENTE' AND origin_key_schema = 'SERVICE_DUE_DATE_REFERENCE_V1')");
                table.HasCheckConstraint(
                    "CK_activation_rule_version_schedule_shape",
                    "(task_definition_id = '019d3a10-0005-7000-8000-000000000005'::uuid AND schedule = '{\"kind\":\"WORKING_DAY_WINDOWS\",\"localTimes\":[\"12:00\",\"17:00\"],\"timeZone\":\"America/Mexico_City\",\"workingDaysOnly\":true}'::jsonb) OR " +
                    "(task_definition_id IN ('019d3a10-0007-7000-8000-000000000007'::uuid,'019d3a10-0008-7000-8000-000000000008'::uuid,'019d3a10-0011-7000-8000-000000000011'::uuid,'019d3a10-0018-7000-8000-000000000018'::uuid,'019d3a10-0092-7000-8000-000000000092'::uuid,'019d3a10-0093-7000-8000-000000000093'::uuid) AND schedule = 'null'::jsonb) OR " +
                    "(task_definition_id = '019d3a10-0026-7000-8000-000000000026'::uuid AND schedule ? 'localTime' AND schedule->>'localTime' ~ '^(?:[01][0-9]|2[0-3]):[0-5][0-9]$' AND schedule - 'localTime' = '{\"kind\":\"BUSINESS_DAYS_BEFORE_DUE_DATE\",\"businessDaysBefore\":3,\"timeZone\":\"America/Mexico_City\",\"adjustDueDateToPreviousBusinessDay\":true}'::jsonb)");
            });
    }
}
