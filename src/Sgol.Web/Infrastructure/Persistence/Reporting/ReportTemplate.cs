using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Reporting;

public sealed class ReportTemplate
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string Configuration { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> b)
    {
        b.ToTable("report_template");
        b.HasKey(t => t.Id);
        b.HasIndex(t => new { t.UserId, t.Name });
        b.Property(t => t.Name).HasMaxLength(100);
        b.Property(t => t.Configuration).HasMaxLength(12000);
        b.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
