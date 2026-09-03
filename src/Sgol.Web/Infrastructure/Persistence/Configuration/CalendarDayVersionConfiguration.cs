using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class CalendarDayVersionConfiguration : IEntityTypeConfiguration<CalendarDayVersion>
{
    public void Configure(EntityTypeBuilder<CalendarDayVersion> builder)
    {
        builder.HasKey(day => day.Id);
        builder.Property(day => day.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(day => day.BranchId).HasColumnName("branch_id");
        builder.Property(day => day.LocalDate).HasColumnName("local_date").HasColumnType("date");
        builder.Property(day => day.DayType).HasColumnName("day_type");
        builder.Property(day => day.IsWorkingDay).HasColumnName("is_working_day");
        builder.Property(day => day.ReleaseId).HasColumnName("release_id");
        builder.Property(day => day.PendingReason).HasColumnName("pending_reason");

        builder.ConfigureVersioning(
            "calendar_day_version",
            nameof(CalendarDayVersion.BranchId),
            nameof(CalendarDayVersion.LocalDate));

        builder.HasIndex(day => new { day.ReleaseId, day.LocalDate })
            .IsUnique()
            .HasDatabaseName("IX_calendar_day_version_release_id_local_date")
            .HasFilter("status = 'BORRADOR'");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(day => day.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationRelease>()
            .WithMany()
            .HasForeignKey(day => day.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(
            "calendar_day_version",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_calendar_day_version_day_type",
                    "day_type IN ('LABORABLE', 'FESTIVO', 'CIERRE_EXTRAORDINARIO')");
                table.HasCheckConstraint(
                    "CK_calendar_day_version_working_consistency",
                    "(day_type = 'LABORABLE' AND is_working_day) OR " +
                    "(day_type IN ('FESTIVO', 'CIERRE_EXTRAORDINARIO') AND NOT is_working_day)");
                table.HasCheckConstraint(
                    "CK_calendar_day_version_pending_reason",
                    "(status = 'BORRADOR' AND btrim(pending_reason) <> '') OR " +
                    "(status IN ('VIGENTE', 'SUSTITUIDA') AND pending_reason IS NULL)");
            });
    }
}
