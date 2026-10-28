using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sgol.Web.Infrastructure.Persistence.Bootstrap;

public sealed class PersonalSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string AuthenticationMethod { get; set; } = "mfa";
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}

internal sealed class PersonalSessionConfiguration : IEntityTypeConfiguration<PersonalSession>
{
    public void Configure(EntityTypeBuilder<PersonalSession> builder)
    {
        builder.ToTable("personal_session", table => table.HasCheckConstraint("ck_personal_session_lifetime",
            "expires_at > issued_at AND expires_at <= issued_at + interval '24 hours'"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.IssuedAt).HasColumnName("issued_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.AuthenticationMethod).HasColumnName("authentication_method").HasMaxLength(16);
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(64);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
