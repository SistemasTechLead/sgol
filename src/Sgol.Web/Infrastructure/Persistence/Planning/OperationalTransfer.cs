using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Assignment.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Immutable handoff receipt. Execution/evidence keep their existing assignment and authors.
public sealed class OperationalTransfer
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string TaskName { get; set; } = "";
    public Guid FromPersonId { get; set; }
    public string FromName { get; set; } = "";
    public Guid ToPersonId { get; set; }
    public string ToName { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset At { get; set; }
    public DateTimeOffset PreviousStartsAt { get; set; }
    public DateTimeOffset PreviousDueAt { get; set; }
    public DateTimeOffset NewDueAt { get; set; }
    public int ProgressUnits { get; set; }
    public int RemainingMinutes { get; set; }
    public string Reason { get; set; } = "";
    public string SnapshotJson { get; set; } = "{}";
}

internal sealed class OperationalTransferConfiguration : IEntityTypeConfiguration<OperationalTransfer>
{
    public void Configure(EntityTypeBuilder<OperationalTransfer> b)
    {
        b.ToTable("operational_transfer", t => t.HasCheckConstraint("CK_transfer_values",
            "\"FromPersonId\" <> \"ToPersonId\" AND \"RemainingMinutes\" BETWEEN 1 AND 4800 AND \"ProgressUnits\" >= 0 AND \"NewDueAt\" > \"At\""));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AssignmentId, x.At });
        b.HasIndex(x => new { x.FromPersonId, x.At });
        b.HasIndex(x => new { x.ToPersonId, x.At });
        b.HasOne<OperationalAssignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.FromPersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(x => x.ToPersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TaskName).HasMaxLength(500);
        b.Property(x => x.FromName).HasMaxLength(300);
        b.Property(x => x.ToName).HasMaxLength(300);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
    }
}
