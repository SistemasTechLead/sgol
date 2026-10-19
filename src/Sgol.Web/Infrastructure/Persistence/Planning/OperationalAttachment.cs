using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// One document/screenshot per delivery. Camera photographs have their own collection.
public sealed class OperationalAttachment
{
    public Guid AssignmentId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class OperationalAttachmentConfiguration : IEntityTypeConfiguration<OperationalAttachment>
{
    public void Configure(EntityTypeBuilder<OperationalAttachment> b)
    {
        b.ToTable("operational_attachment", t => {
            t.HasCheckConstraint("CK_operational_attachment_size", "octet_length(\"Content\") BETWEEN 1 AND 15728640");
            t.HasCheckConstraint("CK_operational_attachment_type", "\"ContentType\" IN ('application/pdf','image/jpeg','image/png','application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')");
        });
        b.HasKey(x => x.AssignmentId);
        b.Property(x => x.AssignmentId).ValueGeneratedNever();
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.HasOne<OperationalAssignment>().WithOne(x => x.Attachment)
            .HasForeignKey<OperationalAttachment>(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.CreatedAt);
    }
}
