using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed class OperationalCarryoverConfiguration : IEntityTypeConfiguration<OperationalCarryoverRequest>
{
    public void Configure(EntityTypeBuilder<OperationalCarryoverRequest> b)
    {
        b.ToTable("operational_carryover_request", t => {
            t.HasCheckConstraint("CK_carryover_interval", "\"Minutes\" > 0 AND ((\"Kind\" = 'INCIDENTE' AND \"IncidentEnd\" = \"IncidentStart\" + \"Minutes\" * interval '1 minute' AND \"From\" IS NULL AND \"Through\" IS NULL) OR (\"Kind\" = 'POSPONER' AND \"From\" IS NOT NULL AND \"Through\" IS NOT NULL AND \"Through\" BETWEEN \"From\" AND \"From\" + 6))");
            t.HasCheckConstraint("CK_carryover_state", "\"Revision\" > 0 AND ((\"State\" = 'PENDIENTE' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR (\"State\" IN ('APROBADA','RECHAZADA') AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND length(\"DecisionComment\") > 0))");
        });
        b.HasKey(r => r.Id); b.HasIndex(r => r.IntentKey).IsUnique();
        b.Property(r => r.Kind).HasMaxLength(12).HasDefaultValue("INCIDENTE");
        b.HasIndex(r => new { r.BranchId, r.State, r.SubmittedAt });
        b.HasIndex(r => new { r.PersonId, r.IncidentStart, r.IncidentEnd });
        b.Property(r => r.Revision).IsConcurrencyToken();
        b.Property(r => r.SnapshotJson).HasColumnType("jsonb");
        b.Property(r => r.ResultsJson).HasColumnType("jsonb");
        b.Property(r => r.Explanation).HasMaxLength(4000); b.Property(r => r.DecisionComment).HasMaxLength(4000);
        b.HasOne<Person>().WithMany().HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Restrict);
        // Embedded immutable task items are stored atomically as a versioned snapshot, without evidence bytes.
    }
}
