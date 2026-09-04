using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class AssignmentVersionConfiguration : IEntityTypeConfiguration<AssignmentVersion>
{
    public const string CurrentObligationIndex = "UX_assignment_version_current_obligation";
    public const string SupersedesIndex = "UX_assignment_version_supersedes";

    public void Configure(EntityTypeBuilder<AssignmentVersion> builder)
    {
        builder.ToTable(
            "assignment_version",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_assignment_version_status",
                    "status IN ('VIGENTE','SUSTITUIDA')");
                table.HasCheckConstraint(
                    "CK_assignment_version_type",
                    "assignment_type IN ('AUTOMATICA','CORRECCION')");
                table.HasCheckConstraint(
                    "CK_assignment_version_explanation_object",
                    "jsonb_typeof(explanation) = 'object'");
                table.HasCheckConstraint(
                    "CK_assignment_version_correction_metadata",
                    "(assignment_type = 'AUTOMATICA' AND reason IS NULL AND assigned_by IS NULL AND supersedes_id IS NULL) OR " +
                    "(assignment_type = 'CORRECCION' AND btrim(reason) <> '' AND assigned_by IS NOT NULL AND supersedes_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_assignment_version_not_self_superseding",
                    "supersedes_id IS NULL OR supersedes_id <> id");
            });

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(assignment => assignment.ObligationId).HasColumnName("obligation_id");
        builder.Property(assignment => assignment.PersonId).HasColumnName("person_id");
        builder.Property(assignment => assignment.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(assignment => assignment.AssignmentType).HasColumnName("assignment_type").HasMaxLength(16);
        builder.Property(assignment => assignment.Explanation).HasColumnName("explanation").HasColumnType("jsonb");
        builder.Property(assignment => assignment.Reason).HasColumnName("reason");
        builder.Property(assignment => assignment.AssignedBy).HasColumnName("assigned_by");
        builder.Property(assignment => assignment.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamp with time zone");
        builder.Property(assignment => assignment.SupersedesId).HasColumnName("supersedes_id");

        builder.HasOne<WorkObligation>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentVersion>()
            .WithMany()
            .HasForeignKey(assignment => assignment.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => assignment.ObligationId)
            .IsUnique()
            .HasFilter("status = 'VIGENTE'")
            .HasDatabaseName(CurrentObligationIndex);
        builder.HasIndex(assignment => assignment.SupersedesId)
            .IsUnique()
            .HasFilter("supersedes_id IS NOT NULL")
            .HasDatabaseName(SupersedesIndex);
        builder.HasIndex(assignment => new
        {
            assignment.PersonId,
            assignment.Status,
            assignment.AssignedAt,
        });
        builder.HasIndex(assignment => new { assignment.ObligationId, assignment.Status });
    }
}
