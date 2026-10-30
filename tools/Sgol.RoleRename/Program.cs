using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var connection = builder.Configuration["ConnectionStrings:Sgol"] ?? throw new Exception("Falta conexión.");
var target = new NpgsqlConnectionStringBuilder(connection);
if (!builder.Environment.IsDevelopment() || target.Host is not ("localhost" or "127.0.0.1") ||
    !(target.Database?.StartsWith("sgol_task_checks_role_", StringComparison.Ordinal) == true ||
      target.Database == "sgol_local" && args.Contains("--apply-local")))
    throw new Exception("Solo Development en localhost: copia sgol_task_checks_role_* o sgol_local con --apply-local.");
var passwords = new[] { "Vale", "Dafne" }.ToDictionary(n => n,
    n => Environment.GetEnvironmentVariable("SGOL_ROLE_" + n.ToUpperInvariant() + "_PASSWORD")
        ?? throw new Exception("Falta contraseña en variable privada para " + n));
builder.Services.AddSgolPersistence(builder.Configuration);
using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
var profiles = scope.ServiceProvider.GetRequiredService<UserProfileService>();
var tasks = scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
var processes = scope.ServiceProvider.GetRequiredService<OperationalProcessCatalogService>();
var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
var actor = await (from r in db.RoleAssignmentVersions
    join u in db.AppUsers on r.UserId equals u.Id
    where r.RoleCode == CanonicalRole.Direction && r.ValidTo == null && r.Status == RoleAssignmentStatus.Active && u.Status == AccountStatus.Active
    select r.UserId).FirstAsync();
var specialty = await db.Set<Specialization>().AsNoTracking().SingleAsync(s => s.Name == "Administración" && s.Active);
var originals = await db.OperationalTasks.AsNoTracking().Where(t => t.AssignedRole == CanonicalRole.Administration).ToArrayAsync();
var publishedSteps = await (from p in db.Set<OperationalSequence>()
    join s in db.Set<OperationalSequenceStep>() on p.PublishedVersionId equals s.VersionId
    where s.TaskSnapshot != null
    select new { p.Id, s.TaskSnapshot }).ToArrayAsync();
var oldPublishedProcesses = publishedSteps.Where(s => JsonNode.Parse(s.TaskSnapshot!)!["AssignedRole"]?.GetValue<string>() == CanonicalRole.Administration)
    .Select(s => s.Id).Distinct().ToArray();
var affectedProcesses = originals.Where(t => t.ProcessId != OperationalSequence.GeneralId).Select(t => t.ProcessId)
    .Concat(oldPublishedProcesses).Distinct().ToArray();
foreach (var id in affectedProcesses)
    if ((await processes.ReadAsync(actor, id, default)).Draft != null)
        throw new Exception("El proceso " + id + " tiene un borrador ajeno; resolver antes de continuar.");

// Compare all operational data except the explicitly requested role corrections.
string OperationalState() => JsonSerializer.Serialize(db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).AsEnumerable()
    .Select(a => { a.DefinitionSnapshotJson = Normalize(a.DefinitionSnapshotJson); a.Revision = 0; return a; }));
string? Normalize(string? json)
{
    if (json == null) return null;
    var node = JsonNode.Parse(json)!;
    if (node["AssignedRole"]?.GetValue<string>() == CanonicalRole.Administration) node["AssignedRole"] = CanonicalRole.Subcoordination;
    return node.ToJsonString();
}
var before = OperationalState();
foreach (var task in originals)
{
    task.AssignedRole = CanonicalRole.Subcoordination;
    task.SpecializationId = specialty.Id;
    await tasks.SaveAsync(actor, task, default);
    db.ChangeTracker.Clear();
}
Console.WriteLine($"Tareas corregidas: {originals.Length}.");
foreach (var id in affectedProcesses)
{
    var editor = await processes.ReadAsync(actor, id, default);
    var config = editor.Configuration ?? throw new Exception("Proceso sin configuración.");
    var enabled = editor.Process.Enabled;
    await processes.SaveAsync(actor, new ProcessDraftInput { Id = id, Revision = editor.Process.Revision,
        Name = config.Name, Description = config.Description, StartKind = config.StartKind, FirstMonth = config.FirstMonth,
        WindowFromDay = config.WindowFromDay, WindowThroughDay = config.WindowThroughDay,
        PreferredWeekDay = config.PreferredWeekDay, StartHour = config.StartHourConfirmed ? config.StartHour : null,
        Steps = editor.Steps.ToList() }, default);
    db.ChangeTracker.Clear();
    editor = await processes.ReadAsync(actor, id, default);
    await processes.PublishAsync(actor, id, editor.Process.Revision, default);
    db.ChangeTracker.Clear();
    if (enabled)
    {
        editor = await processes.ReadAsync(actor, id, default);
        await processes.SetEnabledAsync(actor, id, editor.Process.Revision, true, default);
        db.ChangeTracker.Clear();
    }
}
Console.WriteLine($"Procesos con nueva versión: {affectedProcesses.Length}.");

// Historical snapshots retain their other requirements, specialty and evidence.
// Only the obsolete required role is corrected; the real worker role is untouched.
await new AuditTransaction(db).ExecuteAsync(async ct =>
{
    var assignments = await db.OperationalAssignments.Where(a => a.DefinitionSnapshotJson != null).ToArrayAsync(ct);
    var changed = assignments.Where(a => JsonNode.Parse(a.DefinitionSnapshotJson! )!["AssignedRole"]?.GetValue<string>() == CanonicalRole.Administration).ToArray();
    foreach (var a in changed) { a.DefinitionSnapshotJson = Normalize(a.DefinitionSnapshotJson); a.Revision++; }
    Console.WriteLine($"Copias de definición corregidas: {changed.Length}.");
    return Audit("LEGACY_REQUIRED_ROLE_CORRECTED", "ASSIGNMENT", null,
        new { Ids = changed.Select(a => a.Id).ToArray(), OldRole = CanonicalRole.Administration, NewRole = CanonicalRole.Subcoordination });
});
db.ChangeTracker.Clear();
if (OperationalState() != before) throw new Exception("Cambió un dato operativo fuera del rol requerido; revisar antes de continuar.");

foreach (var (name, password) in passwords)
{
    var all = await profiles.ListAsync(actor, default);
    var profile = all.SingleOrDefault(p => p.UserName.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (profile == null)
    {
        await profiles.MutateAsync(actor, new UserProfileInput { DisplayName = name, UserName = name, RoleCode = CanonicalRole.Administration }, "save", Guid.CreateVersion7(), default);
        profile = (await profiles.ListAsync(actor, default)).Single(p => p.UserName == name);
    }
    if (profile.RoleCode != CanonicalRole.Administration || profile.DisplayName != name || profile.Status != AccountStatus.Active)
        throw new Exception("Perfil existente incompatible: " + name);
    var user = await db.AppUsers.SingleAsync(u => u.Id == profile.Id);
    var credential = await db.IdentityCredentials.SingleAsync(c => c.UserId == user.Id);
    if (hasher.VerifyHashedPassword(user, credential.PasswordHash, password) == PasswordVerificationResult.Failed)
    {
        if (await db.AuditEvents.AnyAsync(e => e.Action == "REQUESTED_INITIAL_PASSWORD_SET" && e.ResourceId == user.Id))
            throw new Exception("La contraseña inicial ya fue sustituida; no se restablece: " + name);
        await new AuditTransaction(db).ExecuteAsync(ct =>
        {
            credential.PasswordHash = hasher.HashPassword(user, password);
            user.SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.AuthenticationRowVersion++;
            return Task.FromResult(Audit("REQUESTED_INITIAL_PASSWORD_SET", "USER", user.Id, new { user.MustChangePassword }));
        });
    }
    db.ChangeTracker.Clear();
    var result = await scope.ServiceProvider.GetRequiredService<IHostedAuthenticationService>().LoginAsync(name, password, Guid.CreateVersion7());
    Console.WriteLine($"Acceso verificado para {name}; configuración inicial: {result.NextStep}.");
}
var admin = (await profiles.ListAsync(actor, default)).SingleOrDefault(p => p.UserName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
if (admin != null)
{
    if (admin.RoleCode != CanonicalRole.Administration) throw new Exception("La cuenta Admin cambió de rol; revisar.");
    await profiles.MutateAsync(actor, new UserProfileInput { Id = admin.Id, Revision = admin.Revision }, "delete", Guid.CreateVersion7(), default);
}
if (SessionPresentation.RoleName(CanonicalRole.Administration) != "Asesor Externo") throw new Exception("Etiqueta incorrecta.");
if (await db.OperationalTasks.AnyAsync(t => t.AssignedRole == CanonicalRole.Administration)) throw new Exception("Quedan tareas antiguas.");
if (OperationalState() != before) throw new Exception("Se alteró el estado de asignaciones.");
Console.WriteLine("Verificado: nombre del rol, permisos conservados, Admin eliminado lógicamente y estado operativo intacto.");

AuditEvent Audit(string action, string resource, Guid? id, object after) => new()
{
    Id = Guid.CreateVersion7(), OccurredAt = DateTimeOffset.UtcNow, ActorUserId = actor, ActorType = "USER",
    Action = action, ResourceType = resource, ResourceId = id, BranchId = BranchScope.LorettaId,
    CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS", AfterData = JsonSerializer.SerializeToDocument(after),
    Reason = "Cambio solicitado de Administración a Asesor Externo; conservar responsables, fechas, evidencia y auditoría."
};
