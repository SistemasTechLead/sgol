using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using System.Text.Json;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class PersonAvatar
{
    public Guid PersonId { get; set; }
    public required string Variant { get; set; }
    public int Palette { get; set; }
    public int Seed { get; set; }
}

internal sealed class PersonAvatarConfiguration : IEntityTypeConfiguration<PersonAvatar>
{
    public void Configure(EntityTypeBuilder<PersonAvatar> builder)
    {
        builder.ToTable("person_avatar", table => table.HasCheckConstraint("CK_person_avatar_options",
            "\"Variant\" IN ('marble','beam','pixel','sunset','ring','bauhaus') AND \"Palette\" BETWEEN 0 AND 3 AND \"Seed\" BETWEEN 0 AND 999999"));
        builder.HasKey(x => x.PersonId);
        builder.Property(x => x.PersonId).ValueGeneratedNever();
        builder.Property(x => x.Variant).HasMaxLength(12);
        builder.HasOne<Person>().WithOne().HasForeignKey<PersonAvatar>(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
    }
}

public interface IPersonAvatarReader
{
    Task<PersonAvatar?> FindAsync(Guid personId, CancellationToken token = default);
    Task<string?> UserNameAsync(Guid personId, CancellationToken token = default);
}

public sealed class PersonAvatarService(SgolDbContext db, AuditTransaction audit) : IPersonAvatarReader
{
    public static readonly string[] Variants = ["marble", "beam", "pixel", "sunset", "ring", "bauhaus"];
    private Dictionary<Guid, PersonAvatar>? cache;
    private Dictionary<Guid, string>? userNames;

    public async Task<string?> UserNameAsync(Guid personId, CancellationToken token = default)
    {
        userNames ??= await (from user in db.AppUsers.AsNoTracking()
                            join credential in db.IdentityCredentials.AsNoTracking() on user.Id equals credential.UserId
                            select new { user.PersonId, credential.UserName }).ToDictionaryAsync(x => x.PersonId, x => x.UserName, token);
        return userNames.GetValueOrDefault(personId);
    }

    // One query per rendered response, including every repeated avatar on the board.
    public async Task<PersonAvatar?> FindAsync(Guid personId, CancellationToken token = default)
    {
        cache ??= await db.Set<PersonAvatar>().AsNoTracking().ToDictionaryAsync(x => x.PersonId, token);
        return cache.GetValueOrDefault(personId);
    }

    public async Task SaveAsync(SessionSnapshot session, string? variant, int palette, int seed, CancellationToken token)
    {
        if (variant is not null && (!Variants.Contains(variant) || palette is < 0 or > 3 || seed is < 0 or > 999999))
            throw new ArgumentException("Elige un estilo y una paleta válidos.");
        await audit.ExecuteAsync(async ct =>
        {
            // Serializes simultaneous saves, including first-time creation in two tabs.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({session.PersonId.ToString()}, 0))", ct);
            var existing = await db.Set<PersonAvatar>().SingleOrDefaultAsync(x => x.PersonId == session.PersonId, ct);
            var before = existing is null ? null : JsonSerializer.SerializeToDocument(existing);
            if (variant is null)
            {
                if (existing is not null) db.Remove(existing);
            }
            else
            {
                if (existing is null) { existing = new PersonAvatar { PersonId = session.PersonId, Variant = variant }; db.Add(existing); }
                existing.Variant = variant; existing.Palette = palette; existing.Seed = seed;
            }
            return new AuditEvent
            {
                Id = Guid.CreateVersion7(), OccurredAt = DateTimeOffset.UtcNow, ActorUserId = session.UserId,
                ActorType = "USER", Action = "PERSON_AVATAR_UPDATED", ResourceType = "PERSON", ResourceId = session.PersonId,
                BranchId = BranchScope.LorettaId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
                BeforeData = before, AfterData = variant is null ? null : JsonSerializer.SerializeToDocument(existing)
            };
        }, token);
        cache = null;
    }
}
