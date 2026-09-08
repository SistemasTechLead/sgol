using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed class WorkObligationConfiguration : IEntityTypeConfiguration<WorkObligation>
{
    public const string GenerationRequestIndex = "UX_work_obligation_generation_request";

    public void Configure(EntityTypeBuilder<WorkObligation> builder)
    {
        builder.ToTable(
            "work_obligation",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_work_obligation_execution_status",
                    "execution_status IN ('PENDIENTE','CONCLUIDA')");
                table.HasCheckConstraint(
                    "CK_work_obligation_origin",
                    "btrim(origin_reference) <> ''");
                table.HasCheckConstraint(
                    "CK_work_obligation_row_version",
                    "row_version > 0");
                table.HasCheckConstraint(
                    "CK_work_obligation_conclusion",
                    "(execution_status = 'PENDIENTE' AND concluded_at IS NULL AND concluded_by IS NULL) OR " +
                    "(execution_status = 'CONCLUIDA' AND concluded_at IS NOT NULL AND concluded_by IS NOT NULL)");
            });

        builder.HasKey(obligation => obligation.Id);
        builder.HasAlternateKey(obligation => new { obligation.Id, obligation.GenerationRequestId })
            .HasName("AK_work_obligation_id_generation_request_id");
        builder.Property(obligation => obligation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(obligation => obligation.TaskDefinitionVersionId).HasColumnName("task_definition_version_id");
        builder.Property(obligation => obligation.BranchId).HasColumnName("branch_id");
        builder.Property(obligation => obligation.PeriodId).HasColumnName("period_id");
        builder.Property(obligation => obligation.GenerationRequestId).HasColumnName("generation_request_id");
        builder.Property(obligation => obligation.OriginReference).HasColumnName("origin_reference");
        builder.Property(obligation => obligation.EvidencePolicyVersionId).HasColumnName("evidence_policy_version_id");
        builder.Property(obligation => obligation.InputPayload).HasColumnName("input_payload").HasColumnType("jsonb");
        builder.Property(obligation => obligation.DueAt).HasColumnName("due_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.ExecutionStatus).HasColumnName("execution_status").HasMaxLength(16);
        builder.Property(obligation => obligation.ConcludedAt).HasColumnName("concluded_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.ConcludedBy).HasColumnName("concluded_by");
        builder.Property(obligation => obligation.RowVersion).HasColumnName("row_version").IsConcurrencyToken();

        builder.HasIndex(obligation => obligation.GenerationRequestId)
            .IsUnique()
            .HasDatabaseName(GenerationRequestIndex);
        builder.HasIndex(obligation => new
        {
            obligation.BranchId,
            obligation.ExecutionStatus,
            obligation.DueAt,
        });
        builder.HasIndex(obligation => obligation.EvidencePolicyVersionId)
            .HasDatabaseName("IX_work_obligation_evidence_policy_version_id");

        builder.HasOne<TaskDefinitionVersion>()
            .WithMany()
            .HasForeignKey(obligation => obligation.TaskDefinitionVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(obligation => obligation.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WeekPeriod>()
            .WithMany()
            .HasForeignKey(obligation => obligation.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GenerationRequest>()
            .WithOne()
            .HasForeignKey<WorkObligation>(obligation => obligation.GenerationRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidencePolicyVersion>()
            .WithMany()
            .HasForeignKey(obligation => obligation.EvidencePolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(obligation => obligation.ConcludedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
