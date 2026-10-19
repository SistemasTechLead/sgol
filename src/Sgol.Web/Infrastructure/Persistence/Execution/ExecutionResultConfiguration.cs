using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

public sealed class ExecutionResultConfiguration : IEntityTypeConfiguration<ExecutionResult>
{
    public const string ObligationIndex = "UX_execution_result_obligation";
    public const string EvidenceReviewIndex = "UX_execution_result_evidence_review";

    public void Configure(EntityTypeBuilder<ExecutionResult> builder)
    {
        builder.ToTable("execution_result", table =>
        {
            table.HasCheckConstraint("CK_execution_result_code", "result_code = 'CONCLUIDA'");
            table.HasCheckConstraint("CK_execution_result_payload", "result_payload = '{\"schemaVersion\": 1}'::jsonb");
        });
        builder.HasKey(result => result.Id);
        builder.Property(result => result.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(result => result.ObligationId).HasColumnName("obligation_id");
        builder.Property(result => result.ResultCode).HasColumnName("result_code").HasMaxLength(16);
        builder.Property(result => result.ResultPayload).HasColumnName("result_payload").HasColumnType("jsonb");
        builder.Property(result => result.EvidenceReviewId).HasColumnName("evidence_review_id");
        builder.Property(result => result.RecordedBy).HasColumnName("recorded_by");
        builder.Property(result => result.RecordedAt).HasColumnName("recorded_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(result => result.ObligationId).IsUnique().HasDatabaseName(ObligationIndex);
        builder.HasIndex(result => result.EvidenceReviewId).IsUnique().HasDatabaseName(EvidenceReviewIndex);
        builder.HasIndex(result => new { result.RecordedBy, result.RecordedAt, result.Id });
        builder.HasOne<WorkObligation>().WithOne().HasForeignKey<ExecutionResult>(result => result.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceReviewSnapshot>().WithOne().HasForeignKey<ExecutionResult>(result => result.EvidenceReviewId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(result => result.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
