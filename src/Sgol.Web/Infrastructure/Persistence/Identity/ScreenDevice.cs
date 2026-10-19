using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class ScreenLinkRequest
{
    public Guid Id { get; init; }
    public Guid ScreenId { get; init; }
    public required string ApprovalCode { get; init; }
    public required string CredentialHash { get; init; }
    public required string DeviceName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class ScreenBrowserTicket
{
    public Guid Id { get; init; }
    public Guid ScreenId { get; init; }
    public Guid SessionStamp { get; init; }
    public required string TicketHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
}

internal sealed class ScreenDeviceConfiguration : IEntityTypeConfiguration<ScreenLinkRequest>, IEntityTypeConfiguration<ScreenBrowserTicket>
{
    public void Configure(EntityTypeBuilder<ScreenLinkRequest> b)
    {
        b.ToTable("screen_link_request");
        b.HasKey(x => x.Id);
        b.Property(x => x.CredentialHash).HasMaxLength(64);
        b.Property(x => x.ApprovalCode).HasMaxLength(10);
        b.Property(x => x.DeviceName).HasMaxLength(120);
        b.HasIndex(x => x.ApprovalCode).IsUnique();
        b.HasIndex(x => new { x.ScreenId, x.ExpiresAt });
        b.HasOne<ScreenAccount>().WithMany().HasForeignKey(x => x.ScreenId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ScreenBrowserTicket> b)
    {
        b.ToTable("screen_browser_ticket");
        b.HasKey(x => x.Id);
        b.Property(x => x.TicketHash).HasMaxLength(64);
        b.HasIndex(x => x.TicketHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
        b.HasOne<ScreenAccount>().WithMany().HasForeignKey(x => x.ScreenId).OnDelete(DeleteBehavior.Restrict);
    }
}
