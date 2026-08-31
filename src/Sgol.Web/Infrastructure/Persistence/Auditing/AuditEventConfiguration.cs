using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event");
        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(auditEvent => auditEvent.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone");
        builder.Property(auditEvent => auditEvent.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(auditEvent => auditEvent.ActorType).HasColumnName("actor_type");
        builder.Property(auditEvent => auditEvent.Action).HasColumnName("action");
        builder.Property(auditEvent => auditEvent.ResourceType).HasColumnName("resource_type");
        builder.Property(auditEvent => auditEvent.ResourceId).HasColumnName("resource_id");
        builder.Property(auditEvent => auditEvent.BranchId).HasColumnName("branch_id");
        builder.Property(auditEvent => auditEvent.CorrelationId).HasColumnName("correlation_id");
        builder.Property(auditEvent => auditEvent.RequestId).HasColumnName("request_id");
        builder.Property(auditEvent => auditEvent.BeforeData)
            .HasColumnName("before_data")
            .HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.AfterData)
            .HasColumnName("after_data")
            .HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.Reason).HasColumnName("reason");
        builder.Property(auditEvent => auditEvent.Outcome).HasColumnName("outcome");
        builder.Property(auditEvent => auditEvent.SourceIpHash)
            .HasColumnName("source_ip_hash")
            .HasColumnType("character(64)");

        builder.HasIndex(auditEvent => new
        {
            auditEvent.ResourceType,
            auditEvent.ResourceId,
            auditEvent.OccurredAt,
        }).IsDescending(false, false, true);
        builder.HasIndex(auditEvent => new
        {
            auditEvent.ActorUserId,
            auditEvent.OccurredAt,
        }).IsDescending(false, true);
        builder.HasIndex(auditEvent => new
        {
            auditEvent.BranchId,
            auditEvent.OccurredAt,
        }).IsDescending(false, true);
    }
}
