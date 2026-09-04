using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public const string VersionNumberIndex = "UX_plan_version_plan_number";
    public const string PlanRowVersionIndex = "UX_plan_version_plan_row_version";
    public const string CurrentScopeIndex = "UX_plan_version_current_scope";
    public const string SupersedesIndex = "UX_plan_version_supersedes";

    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.ToTable("plan_version", table =>
        {
            table.HasCheckConstraint("CK_plan_version_number", "version_no > 0");
            table.HasCheckConstraint("CK_plan_version_status", "status IN ('VIGENTE','SUSTITUIDA')");
            table.HasCheckConstraint(
                "CK_plan_version_scope_role",
                "scope_role IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION')");
            table.HasCheckConstraint("CK_plan_version_plan_row_version", "plan_row_version >= 2");
            table.HasCheckConstraint("CK_plan_version_not_self_superseding", "supersedes_id IS NULL OR supersedes_id <> id");
        });
        builder.HasKey(version => version.Id);
        builder.HasAlternateKey(version => new { version.Id, version.PlanId, version.ScopeRole })
            .HasName("AK_plan_version_id_plan_scope");
        builder.Property(version => version.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(version => version.PlanId).HasColumnName("plan_id");
        builder.Property(version => version.VersionNo).HasColumnName("version_no");
        builder.Property(version => version.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(version => version.ScopeRole).HasColumnName("scope_role").HasMaxLength(32);
        builder.Property(version => version.PublishedBy).HasColumnName("published_by");
        builder.Property(version => version.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
        builder.Property(version => version.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(version => version.PlanRowVersion).HasColumnName("plan_row_version");
        builder.HasIndex(version => new { version.PlanId, version.VersionNo }).IsUnique()
            .HasDatabaseName(VersionNumberIndex);
        builder.HasIndex(version => new { version.PlanId, version.PlanRowVersion }).IsUnique()
            .HasDatabaseName(PlanRowVersionIndex);
        builder.HasIndex(version => new { version.PlanId, version.ScopeRole }).IsUnique()
            .HasFilter("status = 'VIGENTE'").HasDatabaseName(CurrentScopeIndex);
        builder.HasIndex(version => version.SupersedesId).IsUnique()
            .HasFilter("supersedes_id IS NOT NULL").HasDatabaseName(SupersedesIndex);
        builder.HasOne<WorkPlan>().WithMany().HasForeignKey(version => version.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(version => version.PublishedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlanVersion>().WithMany()
            .HasForeignKey(
                nameof(PlanVersion.SupersedesId),
                nameof(PlanVersion.PlanId),
                nameof(PlanVersion.ScopeRole))
            .HasPrincipalKey(
                nameof(PlanVersion.Id),
                nameof(PlanVersion.PlanId),
                nameof(PlanVersion.ScopeRole))
            .OnDelete(DeleteBehavior.Restrict);
    }
}
