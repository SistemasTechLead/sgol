using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalCaptureSession
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int UploadAttempts { get; set; }
}

public sealed class OperationalPhotograph
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid? CaptureSessionId { get; set; }
    public int Position { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Content { get; set; } = [];
    public long ReceivedBytes { get; set; }
    public string ReceivedSha256 { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AttachedAt { get; set; }
}

public sealed record OperationalPhotographDetails(Guid Id, string FileName, string ContentType, int Position, long SizeBytes);

internal sealed class OperationalCaptureSessionConfiguration : IEntityTypeConfiguration<OperationalCaptureSession>
{
    public void Configure(EntityTypeBuilder<OperationalCaptureSession> b)
    {
        b.ToTable("operational_capture_session"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.AssignmentId });
        b.HasIndex(x => new { x.ActorId, x.CreatedAt });
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<OperationalAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OperationalPhotographConfiguration : IEntityTypeConfiguration<OperationalPhotograph>
{
    public void Configure(EntityTypeBuilder<OperationalPhotograph> b)
    {
        b.ToTable("operational_photograph", t =>
        {
            t.HasCheckConstraint("CK_operational_photo_size", "octet_length(\"Content\") BETWEEN 1 AND 15728640 AND \"ReceivedBytes\" BETWEEN 1 AND 15728640");
            t.HasCheckConstraint("CK_operational_photo_type", "\"ContentType\" IN ('image/jpeg','image/png')");
            t.HasCheckConstraint("CK_operational_photo_position", "\"Position\" BETWEEN 0 AND 19");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(32);
        b.Property(x => x.ReceivedSha256).HasMaxLength(64);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => new { x.AssignmentId, x.Position }).IsUnique().HasFilter("\"AttachedAt\" IS NOT NULL");
        b.HasIndex(x => x.CaptureSessionId);
        b.HasOne<OperationalAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<OperationalCaptureSession>().WithMany().HasForeignKey(x => new { x.CaptureSessionId, x.AssignmentId }).HasPrincipalKey(x => new { x.Id, x.AssignmentId }).OnDelete(DeleteBehavior.Cascade);
    }
}
