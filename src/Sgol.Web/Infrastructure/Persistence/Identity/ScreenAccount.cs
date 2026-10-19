using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

// A screen is deliberately NOT an AppUser or a Person. Worker, employment,
// scheduling and assignment queries cannot include it through their joins.
public sealed class ScreenAccount
{
    public const string BoardReadOnly = "TABLERO_LECTURA";
    public const string PendingLink = "PENDIENTE_VINCULACION";
    public const string Active = "ACTIVA";
    public const string Revoked = "REVOCADA";

    public Guid Id { get; init; }
    public Guid BranchId { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string Purpose { get; init; } = BoardReadOnly;
    public string Status { get; set; } = PendingLink;
    public string? CredentialHash { get; set; }
    public Guid? SessionStamp { get; set; }
    public DateTimeOffset? LinkedAt { get; set; }
    public string? DeviceName { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedBy { get; init; }
}

internal sealed class ScreenAccountConfiguration : IEntityTypeConfiguration<ScreenAccount>
{
    public void Configure(EntityTypeBuilder<ScreenAccount> b)
    {
        b.ToTable("screen_account", t =>
        {
            t.HasCheckConstraint("CK_screen_account_purpose", "purpose = 'TABLERO_LECTURA'");
            t.HasCheckConstraint("CK_screen_account_status", "status IN ('PENDIENTE_VINCULACION', 'ACTIVA', 'REVOCADA')");
            t.HasCheckConstraint("CK_screen_account_link", "(status = 'ACTIVA' AND credential_hash IS NOT NULL AND credential_hash ~ '^[A-F0-9]{64}$' AND session_stamp IS NOT NULL AND linked_at IS NOT NULL AND device_name IS NOT NULL) OR (status <> 'ACTIVA' AND credential_hash IS NULL AND session_stamp IS NULL)");
            t.HasCheckConstraint("CK_screen_account_code", "code ~ '^[A-Z][A-Z0-9-]{2,63}$'");
            t.HasCheckConstraint("CK_screen_account_name", "length(btrim(display_name)) BETWEEN 1 AND 120");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.BranchId).HasColumnName("branch_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);
        b.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(120);
        b.Property(x => x.Purpose).HasColumnName("purpose");
        b.Property(x => x.Status).HasColumnName("status");
        b.Property(x => x.CredentialHash).HasColumnName("credential_hash").HasMaxLength(64);
        b.Property(x => x.SessionStamp).HasColumnName("session_stamp");
        b.Property(x => x.LinkedAt).HasColumnName("linked_at");
        b.Property(x => x.DeviceName).HasColumnName("device_name").HasMaxLength(120);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
