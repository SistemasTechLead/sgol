using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence.Organization;

public sealed class Specialization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool Active { get; set; } = true;
    public long Revision { get; set; }
}
public sealed class PersonSpecialization
{
    public Guid PersonId { get; set; }
    public Guid SpecializationId { get; set; }
}
internal sealed class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> b)
    {
        b.ToTable("specialization"); b.HasKey(s => s.Id);
        b.Property(s => s.Name).HasMaxLength(120); b.Property(s => s.NormalizedName).HasMaxLength(120);
        b.HasIndex(s => s.NormalizedName).IsUnique(); b.Property(s => s.Revision).IsConcurrencyToken();
    }
}
internal sealed class PersonSpecializationConfiguration : IEntityTypeConfiguration<PersonSpecialization>
{
    public void Configure(EntityTypeBuilder<PersonSpecialization> b)
    {
        b.ToTable("person_specialization"); b.HasKey(s => new { s.PersonId, s.SpecializationId });
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Specialization>().WithMany().HasForeignKey(s => s.SpecializationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed record SpecializationView(Specialization Item, string[] People, OperationalTask[] Tasks, string[] PendingTasks);

public sealed class SpecializationService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public async Task<List<Specialization>> OptionsAsync(Guid actor, CancellationToken ct)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, ct);
        return await db.Set<Specialization>().AsNoTracking().Where(s => s.Active).OrderBy(s => s.Name).ToListAsync(ct);
    }
    public async Task<List<SpecializationView>> ListAsync(Guid actor, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        var items = await db.Set<Specialization>().AsNoTracking().OrderBy(s => s.Name).ToArrayAsync(ct);
        var links = await (from link in db.Set<PersonSpecialization>().AsNoTracking()
                           join p in db.People.AsNoTracking() on link.PersonId equals p.Id
                           select new { link.SpecializationId, p.DisplayName, p.IsOnTrial }).ToArrayAsync(ct);
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => t.SpecializationId != null && !t.Deleted).ToArrayAsync(ct);
        var pending = await db.OperationalAssignments.AsNoTracking().Where(a => a.SpecializationId != null && a.CompletedAt == null)
            .Select(a => new { a.SpecializationId, a.Name, a.PersonName }).ToArrayAsync(ct);
        return items.Select(s => new SpecializationView(s,
            links.Where(l => l.SpecializationId == s.Id).OrderBy(l => l.DisplayName).Select(l => l.DisplayName + (l.IsOnTrial ? " · A prueba (no elegible)" : "")).ToArray(),
            tasks.Where(t => t.SpecializationId == s.Id).OrderBy(t => t.Name).ToArray(),
            pending.Where(a => a.SpecializationId == s.Id).Select(a => a.Name + " · " + a.PersonName).ToArray())).ToList();
    }
    public async Task SaveAsync(Guid actor, Specialization input, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        input.Name = (input.Name ?? "").Trim();
        if (input.Name.Length is < 2 or > 120 || input.Name.Any(char.IsControl)) throw new DirectionPlanException("SPECIALIZATION_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationalTaskService.LockAsync(db, ct); await direction.AuthorizeAsync(actor, ct);
        var normalized = input.Name.ToUpperInvariant();
        if (await db.Set<Specialization>().AnyAsync(s => s.Id != input.Id && s.NormalizedName == normalized, ct))
            throw new DirectionPlanException("SPECIALIZATION_DUPLICATE", 409);
        var item = input.Id == Guid.Empty ? null : await db.Set<Specialization>().SingleOrDefaultAsync(s => s.Id == input.Id, ct);
        if (input.Id != Guid.Empty && item == null) throw new DirectionPlanException("SPECIALIZATION_NOT_FOUND", 404);
        if (item != null && item.Revision != input.Revision) throw new DirectionPlanException("SPECIALIZATION_CONFLICT", 409);
        if (!input.Active && (await db.OperationalTasks.AnyAsync(t => t.SpecializationId == input.Id && t.Active && !t.Deleted, ct) ||
            await db.OperationalAssignments.AnyAsync(a => a.SpecializationId == input.Id && a.CompletedAt == null, ct)))
            throw new DirectionPlanException("SPECIALIZATION_IN_USE", 409);
        if (item == null) { item = new() { Id = Guid.CreateVersion7() }; db.Add(item); }
        item.Name = input.Name; item.NormalizedName = normalized; item.Active = input.Active; item.Revision++;
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
            ResourceId = item.Id, ResourceType = "SPECIALIZATION", BranchId = BranchScope.LorettaId, Action = "SPECIALIZATION_SAVED",
            Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(), AfterData = JsonSerializer.SerializeToDocument(item) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public static string Message(string code) => code switch {
        "SPECIALIZATION_INVALID" => "Escribe un nombre de 2 a 120 caracteres.",
        "SPECIALIZATION_DUPLICATE" => "Ya existe una especialización con ese nombre.",
        "SPECIALIZATION_CONFLICT" => "La especialización cambió. Recarga antes de guardar.",
        "SPECIALIZATION_IN_USE" => "Hay tareas activas o asignaciones pendientes que requieren esta especialización. Resuélvelas antes de desactivarla.",
        "TASK_SPECIALIZATION_INVALID" => "Selecciona una especialización activa.",
        "PLAN_NO_SPECIALIST" => "No hay personas con la especialización requerida que hayan terminado su periodo de prueba.",
        _ => "La especialización no está disponible."
    };
}
