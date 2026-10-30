using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence.Identity;

public sealed class UserProfileInput
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public long Revision { get; set; }
    public Guid IntentKey { get; set; } = Guid.CreateVersion7();
    public bool ManageSpecializations { get; set; }
    public bool IsOnTrial { get; set; }
    public Guid[] SpecializationIds { get; set; } = [];
}
public sealed record UserProfile(Guid Id, string DisplayName, string UserName, string RoleCode,
    string Status, bool MustChangePassword, long Revision, bool IsOnTrial = false, Guid[]? SpecializationIds = null,
    Guid PersonId = default);
public sealed record UserProfileResult(Guid Id, string? TemporaryPassword, bool Replayed = false)
{
    public override string ToString() => $"UserProfileResult {{ Id = {Id}, TemporaryPassword = [REDACTED] }}";
}
public sealed class UserProfileException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class UserProfileService(SgolDbContext db, AuditTransaction audit, IClock clock,
    IPasswordHasher<AppUser> hasher)
{
    public const string Deleted = AccountStatus.Deleted;
    private async Task AuthorizeAsync(Guid actor, CancellationToken ct)
    {
        var allowed = await (from u in db.AppUsers.AsNoTracking()
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            join e in db.EmploymentVersions.AsNoTracking() on u.PersonId equals e.PersonId
            where u.Id == actor && u.Status == AccountStatus.Active &&
                r.BranchId == BranchScope.LorettaId && (r.RoleCode == CanonicalRole.Administration || r.RoleCode == CanonicalRole.Direction) &&
                r.Status == RoleAssignmentStatus.Active && r.ValidTo == null &&
                e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active && e.ValidTo == null
            select u.Id).AnyAsync(ct);
        if (!allowed) throw new UserProfileException("Solo Dirección y Asesor Externo pueden gestionar usuarios.", 403);
    }
    private IQueryable<UserProfile> Query(Guid? id = null) =>
        from u in db.AppUsers.AsNoTracking()
        join p in db.People.AsNoTracking() on u.PersonId equals p.Id
        join c in db.IdentityCredentials.AsNoTracking() on u.Id equals c.UserId
        where (!id.HasValue || u.Id == id.Value) && u.Status != Deleted && db.EmploymentVersions.Any(e => e.PersonId == p.Id &&
            e.BranchId == BranchScope.LorettaId && e.ValidTo == null)
        orderby p.DisplayName
        select new UserProfile(u.Id, p.DisplayName, c.UserName,
            db.RoleAssignmentVersions.Where(r => r.UserId == u.Id && r.BranchId == BranchScope.LorettaId &&
                r.ValidTo == null && r.Status == RoleAssignmentStatus.Active).Select(r => r.RoleCode).FirstOrDefault() ?? "",
            u.Status, u.MustChangePassword, u.AuthenticationRowVersion, p.IsOnTrial,
            db.Set<PersonSpecialization>().Where(s => s.PersonId == p.Id).OrderBy(s => s.SpecializationId).Select(s => s.SpecializationId).ToArray(), p.Id);
    public async Task<IReadOnlyList<UserProfile>> ListAsync(Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        return await Query().ToListAsync(ct);
    }
    public async Task<UserProfileResult> MutateAsync(Guid actor, UserProfileInput input, string action,
        Guid correlation, CancellationToken ct)
    {
        if (action is not ("save" or "deactivate" or "reactivate" or "delete"))
            throw new UserProfileException("La acción no es válida.");
        input.DisplayName = (input.DisplayName ?? "").Trim(); input.UserName = (input.UserName ?? "").Trim();
        if (input.IntentKey == Guid.Empty) throw new UserProfileException("Recarga la página antes de continuar.");
        if (action == "save" && (input.DisplayName.Length is < 2 or > 160 || input.UserName.Length is < 3 or > 80 ||
            input.UserName.Any(char.IsWhiteSpace) || !CanonicalRole.IsDefined(input.RoleCode)))
            throw new UserProfileException("Ingresa un nombre (2–160 caracteres), un usuario sin espacios (3–80) y un rol.");
        var scope = IdempotencyProtocol.Scope(actor.ToString("D"), "PROFILE_" + action, input.Id.ToString("D"));
        var hash = IdempotencyProtocol.HashCanonical("PROFILE_" + action, actor.ToString("D"), input.Id.ToString("D"), input);
        UserProfileResult? result = null;
        try
        {
            await audit.ExecuteAsync(async transactionCt =>
            {
                // Serialize management writes, including changes to the acting administrator.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(730051006)", transactionCt);
                await OperationalTaskService.LockAsync(db, transactionCt);
                await AuthorizeAsync(actor, transactionCt);
                if (action == "save" && input.ManageSpecializations)
                {
                    var at = clock.UtcNow;
                    if (!await db.RoleAssignmentVersions.AnyAsync(r => r.UserId == actor && r.RoleCode == CanonicalRole.Direction &&
                        r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= at &&
                        (r.ValidTo == null || r.ValidTo > at), transactionCt))
                        throw new UserProfileException("Solo Dirección puede asignar especializaciones y modificar el periodo de prueba.", 403);
                    if (input.SpecializationIds.Length > 100 || input.SpecializationIds.Distinct().Count() != input.SpecializationIds.Length ||
                        await db.Set<Specialization>().CountAsync(s => input.SpecializationIds.Contains(s.Id) && (s.Active ||
                            db.Set<PersonSpecialization>().Any(l => l.SpecializationId == s.Id && db.AppUsers.Any(u => u.Id == input.Id && u.PersonId == l.PersonId))), transactionCt) != input.SpecializationIds.Length)
                        throw new UserProfileException("Selecciona especializaciones activas y sin duplicados.");
                }
                var replay = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r =>
                    r.Scope == scope && r.Key == input.IntentKey, transactionCt);
                if (replay is not null)
                {
                    if (replay.RequestHash != hash) throw new UserProfileException("La solicitud ya se usó con otros datos.", 409);
                    result = new(replay.ResourceId, null, true);
                    return Event(actor, correlation, replay.ResourceId, "USER_PROFILE_REPLAYED");
                }
                var now = clock.UtcNow;
                string? password = null;
                AppUser user;
                if (input.Id == Guid.Empty)
                {
                    if (action != "save") throw new UserProfileException("No se encontró el usuario.", 404);
                    var person = new Person { Id = Guid.CreateVersion7(), StableCode = "USR-" + Guid.NewGuid().ToString("N"),
                        DisplayName = input.DisplayName, CreatedAt = now };
                    user = new AppUser { Id = Guid.CreateVersion7(), PersonId = person.Id,
                        Status = AccountStatus.Active, MustChangePassword = true, SecurityStamp = Stamp() };
                    password = Password();
                    db.People.Add(person);
                    db.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), person.Id,
                        BranchScope.LorettaId, EmploymentStatus.Active, now));
                    db.AppUsers.Add(user);
                    db.IdentityCredentials.Add(new IdentityCredential { UserId = user.Id, UserName = input.UserName,
                        NormalizedUserName = input.UserName.ToUpperInvariant(), PasswordHash = hasher.HashPassword(user, password) });
                    db.RoleAssignmentVersions.Add(Role(user.Id, input.RoleCode, now));
                }
                else
                {
                    user = await db.AppUsers.FromSqlInterpolated($"SELECT * FROM app_user WHERE id = {input.Id} FOR UPDATE")
                        .SingleOrDefaultAsync(transactionCt) ?? throw new UserProfileException("No se encontró el usuario.", 404);
                    if (user.Status == Deleted || !await db.EmploymentVersions.AnyAsync(e => e.PersonId == user.PersonId &&
                        e.BranchId == BranchScope.LorettaId && e.ValidTo == null, transactionCt))
                        throw new UserProfileException("No se encontró el usuario.", 404);
                    if (user.AuthenticationRowVersion != input.Revision)
                        throw new UserProfileException("Este perfil cambió. Recarga la página antes de editarlo.", 409);
                    var role = await db.RoleAssignmentVersions.SingleOrDefaultAsync(r => r.UserId == user.Id &&
                        r.BranchId == BranchScope.LorettaId && r.ValidTo == null, transactionCt);
                    if (actor == user.Id && (action != "save" || input.RoleCode != role?.RoleCode))
                        throw new UserProfileException("Puedes editar tu perfil, pero no cambiar tu propio rol ni quitarte el acceso.", 409);
                    if (action == "save")
                    {
                        var person = await db.People.SingleAsync(p => p.Id == user.PersonId, transactionCt);
                        person.DisplayName = input.DisplayName;
                        var credential = await db.IdentityCredentials.SingleAsync(c => c.UserId == user.Id, transactionCt);
                        credential.UserName = input.UserName; credential.NormalizedUserName = input.UserName.ToUpperInvariant();
                        if (role?.RoleCode != input.RoleCode)
                        {
                            if (role is not null) { role.ValidTo = now; role.Status = RoleAssignmentStatus.Superseded; role.RowVersion++; }
                            // Close the current version before inserting its successor.
                            await db.SaveChangesAsync(transactionCt);
                            db.RoleAssignmentVersions.Add(Role(user.Id, input.RoleCode, now, role?.Id));
                            user.SecurityStamp = Stamp();
                        }
                    }
                    else
                    {
                        var status = action == "reactivate" ? AccountStatus.Active : action == "delete" ? Deleted : AccountStatus.Inactive;
                        if (user.Status == status) throw new UserProfileException("El estado del usuario ya cambió.", 409);
                        if (action == "reactivate")
                        {
                            if (!await db.EmploymentVersions.AnyAsync(e => e.PersonId == user.PersonId &&
                                e.BranchId == BranchScope.LorettaId && e.ValidTo == null && e.Status == EmploymentStatus.Active, transactionCt))
                                throw new UserProfileException("La persona debe tener un empleo activo para reactivar su cuenta.", 409);
                            password = Password(); user.MustChangePassword = true;
                            user.AccessFailedCount = 0; user.LockoutLevel = 0; user.LockoutEndUtc = null;
                            var credential = await db.IdentityCredentials.SingleAsync(c => c.UserId == user.Id, transactionCt);
                            credential.PasswordHash = hasher.HashPassword(user, password);
                        }
                        if (action == "delete")
                        {
                            if (role is not null) { role.ValidTo = now; role.Status = RoleAssignmentStatus.Superseded; role.RowVersion++; }
                            var employment = await db.EmploymentVersions.SingleAsync(e => e.PersonId == user.PersonId &&
                                e.BranchId == BranchScope.LorettaId && e.ValidTo == null, transactionCt);
                            if (employment.Status == EmploymentStatus.Active)
                            {
                                var successor = employment.CreateSuccessor(Guid.CreateVersion7(), EmploymentStatus.Inactive, now);
                                await db.SaveChangesAsync(transactionCt);
                                db.EmploymentVersions.Add(successor);
                            }
                        }
                        user.Status = status; user.SecurityStamp = Stamp();
                    }
                    user.AuthenticationRowVersion++;
                }
                if (action == "save" && input.ManageSpecializations)
                {
                    var person = db.People.Local.SingleOrDefault(p => p.Id == user.PersonId) ?? await db.People.SingleAsync(p => p.Id == user.PersonId, transactionCt);
                    if (await db.OperationalAssignments.AnyAsync(a => a.PersonId == user.PersonId && a.CompletedAt == null && a.SpecializationId != null &&
                        (input.IsOnTrial || !input.SpecializationIds.Contains(a.SpecializationId.Value)), transactionCt))
                        throw new UserProfileException("Tiene tareas especializadas pendientes. Resuélvelas o reasígnalas antes de quitar una especialización o marcar A prueba.", 409);
                    person.IsOnTrial = input.IsOnTrial;
                    var links = await db.Set<PersonSpecialization>().Where(s => s.PersonId == person.Id).ToArrayAsync(transactionCt);
                    db.RemoveRange(links.Where(l => !input.SpecializationIds.Contains(l.SpecializationId)));
                    foreach (var specialty in input.SpecializationIds.Where(id => !links.Any(l => l.SpecializationId == id)))
                        db.Add(new PersonSpecialization { PersonId = person.Id, SpecializationId = specialty });
                }
                await db.SaveChangesAsync(transactionCt);
                result = new(user.Id, password);
                db.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, input.IntentKey, hash,
                    "USER", user.Id, 200, new { user.Id }, now, now.AddDays(7)));
                var profile = await Query(user.Id).SingleOrDefaultAsync(transactionCt);
                return Event(actor, correlation, user.Id, input.Id == Guid.Empty ? "USER_CREATED" : "USER_PROFILE_" + action.ToUpperInvariant(), profile);
            }, ct);
            return result!;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new UserProfileException("Ese nombre de usuario ya está registrado. Usa otro.", 409); }
        catch (DbUpdateConcurrencyException)
        { throw new UserProfileException("Este perfil cambió. Recarga la página antes de continuar.", 409); }
        finally { db.ChangeTracker.Clear(); }
    }
    private AuditEvent Event(Guid actor, Guid correlation, Guid id, string action, UserProfile? profile = null) => new()
    {
        Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", Action = action,
        ResourceType = "USER", ResourceId = id, BranchId = BranchScope.LorettaId,
        CorrelationId = correlation, OccurredAt = clock.UtcNow, Outcome = "SUCCESS",
        AfterData = profile is null ? null : JsonSerializer.SerializeToDocument(profile)
    };
    private static RoleAssignmentVersion Role(Guid user, string code, DateTimeOffset now, Guid? previous = null) => new()
    { Id = Guid.CreateVersion7(), UserId = user, BranchId = BranchScope.LorettaId, RoleCode = code,
        Status = RoleAssignmentStatus.Active, ValidFrom = now, SupersedesId = previous };
    private static string Stamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Password() => "Sg!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)) + "a9";
}



