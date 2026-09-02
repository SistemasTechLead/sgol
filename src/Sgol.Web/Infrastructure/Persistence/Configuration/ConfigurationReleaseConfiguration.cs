using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class ConfigurationReleaseConfiguration : IEntityTypeConfiguration<ConfigurationRelease>
{
    public void Configure(EntityTypeBuilder<ConfigurationRelease> builder)
    {
        builder.HasKey(release => release.Id);
        builder.Property(release => release.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(release => release.BranchId).HasColumnName("branch_id");
        builder.Property(release => release.VersionNo).HasColumnName("version_no");
        builder.Property(release => release.PublishedBy).HasColumnName("published_by");
        builder.Property(release => release.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamp with time zone");

        builder.ConfigureVersioning("configuration_release", nameof(ConfigurationRelease.BranchId));

        builder.HasIndex(release => new { release.BranchId, release.VersionNo })
            .IsUnique()
            .HasDatabaseName("IX_configuration_release_branch_id_version_no")
            .HasFilter("version_no IS NOT NULL");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(release => release.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(release => release.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "configuration_release",
            table => table.HasCheckConstraint(
                "CK_configuration_release_publication",
                "(status = 'BORRADOR' AND version_no IS NULL AND published_by IS NULL AND published_at IS NULL) OR " +
                "(status IN ('VIGENTE', 'SUSTITUIDA') AND version_no > 0 AND published_by IS NOT NULL AND published_at IS NOT NULL)"));
    }
}
