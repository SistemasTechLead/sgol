using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.JobInfrastructure;

internal sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_event", table =>
        {
            table.HasCheckConstraint(
                "CK_outbox_event_type",
                "event_type ~ '^[A-Z][A-Z0-9_.]{0,127}$'");
            table.HasCheckConstraint(
                "CK_outbox_event_payload",
                "jsonb_typeof(payload) = 'object' AND (payload - 'schemaVersion' - 'correlationId' - 'data') = '{}'::jsonb AND " +
                "payload ? 'schemaVersion' AND payload ? 'correlationId' AND payload ? 'data' AND " +
                "jsonb_typeof(payload -> 'schemaVersion') = 'number' AND payload ->> 'schemaVersion' = '1' AND " +
                "jsonb_typeof(payload -> 'correlationId') = 'string' AND " +
                "payload ->> 'correlationId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND " +
                "jsonb_typeof(payload -> 'data') = 'object' AND octet_length(payload::text) <= 65536");
            table.HasCheckConstraint(
                "CK_outbox_event_timeline",
                "available_at >= created_at AND (processed_at IS NULL OR processed_at >= created_at)");
            table.HasCheckConstraint(
                "CK_outbox_event_attempt_count",
                "attempt_count >= 0 AND attempt_count <= 5");
            table.HasCheckConstraint(
                "CK_outbox_event_state",
                "(processed_at IS NULL AND attempt_count = 0 AND last_error IS NULL) OR " +
                "(processed_at IS NULL AND attempt_count BETWEEN 1 AND 5 AND last_error IS NOT NULL) OR " +
                "(processed_at IS NOT NULL AND attempt_count BETWEEN 1 AND 5 AND last_error IS NULL)");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(entity => entity.AggregateId).HasColumnName("aggregate_id");
        builder.Property(entity => entity.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.AvailableAt).HasColumnName("available_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasMaxLength(512);
        builder.HasIndex(entity => new { entity.ProcessedAt, entity.AvailableAt, entity.CreatedAt, entity.Id })
            .HasDatabaseName("IX_outbox_event_claim");
        builder.HasIndex(entity => new { entity.EventType, entity.CreatedAt })
            .HasDatabaseName("IX_outbox_event_type_created_at");
    }
}

internal sealed class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        builder.ToTable("scheduled_job_run", table =>
        {
            table.HasCheckConstraint("CK_scheduled_job_run_name", "job_name ~ '^[A-Z][A-Z0-9_]{0,63}$'");
            table.HasCheckConstraint(
                "CK_scheduled_job_run_checkpoint",
                "checkpoint IS NULL OR (jsonb_typeof(checkpoint) = 'object' AND octet_length(checkpoint::text) <= 65536)");
            table.HasCheckConstraint(
                "CK_scheduled_job_run_timeline",
                "ended_at IS NULL OR ended_at >= started_at");
            table.HasCheckConstraint(
                "CK_scheduled_job_run_state",
                "(status = 'RUNNING' AND ended_at IS NULL AND error IS NULL) OR " +
                "(status = 'SUCCEEDED' AND ended_at IS NOT NULL AND error IS NULL) OR " +
                "(status = 'FAILED' AND ended_at IS NOT NULL AND error IS NOT NULL)");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entity => entity.JobName).HasColumnName("job_name").HasMaxLength(64).IsRequired();
        builder.Property(entity => entity.ScheduledFor).HasColumnName("scheduled_for").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.EndedAt).HasColumnName("ended_at").HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(entity => entity.Checkpoint).HasColumnName("checkpoint").HasColumnType("jsonb");
        builder.Property(entity => entity.Error).HasColumnName("error").HasMaxLength(512);
        builder.HasIndex(entity => new { entity.JobName, entity.ScheduledFor })
            .IsUnique()
            .HasDatabaseName("UX_scheduled_job_run_name_scheduled_for");
        builder.HasIndex(entity => new { entity.Status, entity.ScheduledFor })
            .HasDatabaseName("IX_scheduled_job_run_status_scheduled_for");
    }
}
