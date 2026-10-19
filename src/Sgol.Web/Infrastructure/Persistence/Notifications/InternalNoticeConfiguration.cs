using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Notifications;

public sealed class InternalNoticeConfiguration : IEntityTypeConfiguration<InternalNotice>
{
    public const string ProducerIndex = "UX_internal_notice_producer";

    public void Configure(EntityTypeBuilder<InternalNotice> builder)
    {
        builder.ToTable("internal_notice", table =>
        {
            table.HasCheckConstraint("CK_internal_notice_type", "notice_type = 'OBLIGATION_ASSIGNED'");
            table.HasCheckConstraint("CK_internal_notice_resource_type", "resource_type = 'ASSIGNMENT_VERSION'");
            table.HasCheckConstraint("CK_internal_notice_read_at", "read_at IS NULL OR read_at >= created_at");
            table.HasCheckConstraint("CK_internal_notice_non_empty_ids",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "recipient_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "resource_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(item => item.NoticeType).HasColumnName("notice_type").HasMaxLength(32);
        builder.Property(item => item.ResourceType).HasColumnName("resource_type").HasMaxLength(32);
        builder.Property(item => item.ResourceId).HasColumnName("resource_id");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(item => item.ReadAt).HasColumnName("read_at").HasColumnType("timestamp with time zone");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(item => item.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentVersion>().WithMany().HasForeignKey(item => item.ResourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.NoticeType, item.ResourceType, item.ResourceId }).IsUnique().HasDatabaseName(ProducerIndex);
        builder.HasIndex(item => new { item.RecipientUserId, item.ReadAt, item.CreatedAt, item.Id })
            .IsDescending(false, false, true, true);
        builder.HasIndex(item => new { item.RecipientUserId, item.CreatedAt, item.Id })
            .IsDescending(false, true, true);
    }
}
