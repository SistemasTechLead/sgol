using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.Web.Infrastructure.Persistence.Continuity;

internal sealed class RecoveryReconciliationConfiguration : IEntityTypeConfiguration<RecoveryReconciliation>
{
    public void Configure(EntityTypeBuilder<RecoveryReconciliation> builder)
    {
        builder.ToTable("recovery_reconciliation", table =>
        {
            table.HasCheckConstraint("CK_recovery_reconciliation_reason", "length(reason) BETWEEN 1 AND 500");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.BranchId).HasColumnName("branch_id");
        builder.Property(item => item.RequestedBy).HasColumnName("requested_by");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(item => item.RequestedAt).HasColumnName("requested_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(item => new { item.BranchId, item.RequestedAt, item.Id });
    }
}

internal sealed class RecoveryReconciliationEventConfiguration : IEntityTypeConfiguration<RecoveryReconciliationEvent>
{
    public void Configure(EntityTypeBuilder<RecoveryReconciliationEvent> builder)
    {
        builder.ToTable("recovery_reconciliation_event", table =>
        {
            table.HasCheckConstraint("CK_recovery_reconciliation_event_sequence", "sequence > 0");
            table.HasCheckConstraint("CK_recovery_reconciliation_event_difference_count", "difference_count IS NULL OR difference_count >= 0");
            table.HasCheckConstraint("CK_recovery_reconciliation_event_rpo", "observed_rpo_seconds IS NULL OR observed_rpo_seconds >= 0");
            table.HasCheckConstraint("CK_recovery_reconciliation_event_rto", "observed_rto_seconds IS NULL OR observed_rto_seconds >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.ReconciliationId).HasColumnName("reconciliation_id");
        builder.Property(item => item.Sequence).HasColumnName("sequence");
        builder.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(item => item.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(item => item.TechnicalActor).HasColumnName("technical_actor").HasMaxLength(32);
        builder.Property(item => item.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CorrelationId).HasColumnName("correlation_id");
        builder.Property(item => item.ErrorCode).HasColumnName("error_code").HasMaxLength(128);
        builder.Property(item => item.ReferenceManifestSha256).HasColumnName("reference_manifest_sha256").HasColumnType("character(64)");
        builder.Property(item => item.ReportManifestSha256).HasColumnName("report_manifest_sha256").HasColumnType("character(64)");
        builder.Property(item => item.RestoreEvidenceSha256).HasColumnName("restore_evidence_sha256").HasColumnType("character(64)");
        builder.Property(item => item.ReferenceRootSha256).HasColumnName("reference_root_sha256").HasColumnType("character(64)");
        builder.Property(item => item.ActualRootSha256).HasColumnName("actual_root_sha256").HasColumnType("character(64)");
        builder.Property(item => item.DifferenceCount).HasColumnName("difference_count");
        builder.Property(item => item.DifferencesTruncated).HasColumnName("differences_truncated");
        builder.Property(item => item.ObservedRpoSeconds).HasColumnName("observed_rpo_seconds");
        builder.Property(item => item.ObservedRtoSeconds).HasColumnName("observed_rto_seconds");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.HasIndex(item => new { item.ReconciliationId, item.Sequence }).IsUnique();
        builder.HasIndex(item => item.ReconciliationId)
            .IsUnique()
            .HasFilter("status IN ('DIFFERENT','FAILED','APPROVED')")
            .HasDatabaseName("UX_recovery_reconciliation_terminal");
        builder.HasOne<RecoveryReconciliation>().WithMany().HasForeignKey(item => item.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RecoveryReconciliationDifferenceConfiguration : IEntityTypeConfiguration<RecoveryReconciliationDifference>
{
    public void Configure(EntityTypeBuilder<RecoveryReconciliationDifference> builder)
    {
        builder.ToTable("recovery_reconciliation_difference", table =>
        {
            table.HasCheckConstraint("CK_recovery_reconciliation_difference_ordinal", "ordinal > 0 AND ordinal <= 100000");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.ReconciliationId).HasColumnName("reconciliation_id");
        builder.Property(item => item.Ordinal).HasColumnName("ordinal");
        builder.Property(item => item.Group).HasColumnName("group_name").HasMaxLength(32).IsRequired();
        builder.Property(item => item.ResourceType).HasColumnName("resource_type").HasMaxLength(64).IsRequired();
        builder.Property(item => item.StableKey).HasColumnName("stable_key").HasMaxLength(256).IsRequired();
        builder.Property(item => item.Field).HasColumnName("field_name").HasMaxLength(128);
        builder.Property(item => item.Kind).HasColumnName("kind").HasMaxLength(64).IsRequired();
        builder.Property(item => item.ExpectedSha256).HasColumnName("expected_sha256").HasColumnType("character(64)");
        builder.Property(item => item.ActualSha256).HasColumnName("actual_sha256").HasColumnType("character(64)");
        builder.HasIndex(item => new { item.ReconciliationId, item.Ordinal }).IsUnique();
        builder.HasOne<RecoveryReconciliation>().WithMany().HasForeignKey(item => item.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
    }
}
