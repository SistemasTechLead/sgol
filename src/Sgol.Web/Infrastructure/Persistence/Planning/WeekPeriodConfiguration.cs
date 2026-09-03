using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class WeekPeriodConfiguration : IEntityTypeConfiguration<WeekPeriod>
{
    public const string UniqueIndex = "UX_week_period_branch_iso_week";

    public void Configure(EntityTypeBuilder<WeekPeriod> builder)
    {
        builder.HasKey(period => period.Id);
        builder.Property(period => period.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(period => period.BranchId).HasColumnName("branch_id");
        builder.Property(period => period.IsoYear).HasColumnName("iso_year");
        builder.Property(period => period.IsoWeek).HasColumnName("iso_week");
        builder.Property(period => period.StartsOn).HasColumnName("starts_on").HasColumnType("date");
        builder.Property(period => period.EndsOn).HasColumnName("ends_on").HasColumnType("date");
        builder.Property(period => period.DerivedStatus).HasColumnName("derived_status");

        builder.HasIndex(period => new { period.BranchId, period.IsoYear, period.IsoWeek })
            .IsUnique()
            .HasDatabaseName(UniqueIndex);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(period => period.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "week_period",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_week_period_iso_year",
                    "iso_year BETWEEN 1 AND 9999");
                table.HasCheckConstraint(
                    "CK_week_period_iso_week",
                    "iso_week BETWEEN 1 AND 53");
                table.HasCheckConstraint(
                    "CK_week_period_dates",
                    "ends_on = starts_on + 6 AND EXTRACT(ISODOW FROM starts_on) = 1");
                table.HasCheckConstraint(
                    "CK_week_period_derived_status",
                    "derived_status IN ('VIGENTE', 'TRANSCURRIDA')");
            });
    }
}
