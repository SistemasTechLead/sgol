using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

var expectations = new Dictionary<string, string[]>
{
    [CanonicalRole.Direction] = ["/mi-trabajo", "/tablero", "/validaciones", "/personas-y-accesos", "/configuracion", "/tareas", "/planificacion", "/reportes"],
    [CanonicalRole.Administration] = ["/mi-trabajo", "/tablero", "/validaciones", "/configuracion", "/tareas", "/planificacion", "/reportes"],
    [CanonicalRole.Subcoordination] = ["/mi-trabajo", "/tablero", "/validaciones", "/asignar-tarea", "/configuracion", "/planificacion", "/reportes"],
    [CanonicalRole.SalesFloor] = ["/mi-trabajo", "/tablero", "/configuracion"]
};
foreach (var (role, allowed) in expectations)
{
    var session = new CheckSession(role);
    var snapshot = await session.GetAsync();
    if (!RoleAwareNavigation.VisibleTo(snapshot, RoleScreenAccess.Items).Select(i => i.Href).SequenceEqual(allowed))
        throw new Exception($"Incorrect menu for {role}");
    foreach (var screen in RoleScreenAccess.Items)
        foreach (var suffix in new[] { "", "/detalle" })
            foreach (var method in new[] { "GET", "POST" })
            {
                var (result, ran) = await Execute(session, screen.Href + suffix, method);
                if (screen.AllowedRoles.Contains(role) ? !ran || result is not null : ran || result is not StatusCodeResult { StatusCode: 403 })
                    throw new Exception($"Incorrect page access: {role} {method} {screen.Href}{suffix}");
            }
    Console.WriteLine($"PASS {role}: menu, direct URLs, nested screens and POST handlers");
    var branch = await Execute(session, "/branches/LOR-001", "GET");
    if (role == CanonicalRole.Direction ? !branch.Ran : branch.Ran || branch.Result is not StatusCodeResult { StatusCode: 403 })
        throw new Exception($"Incorrect legacy branch access for {role}");
}
var anonymous = await Execute(new CheckSession(null), "/tareas", "GET");
if (anonymous.Ran || anonymous.Result is not RedirectResult { Url: "/acceso" }) throw new Exception("Anonymous access must redirect");
Console.WriteLine("PASS anonymous access redirects to login");

if (args.Contains("--verify-local-credentials"))
{
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Sgol") ?? throw new Exception("Missing local connection");
    var settings = new NpgsqlConnectionStringBuilder(connection);
    if (settings.Host != "localhost" || settings.Database != "sgol_local") throw new Exception("Only sgol_local is allowed");
    await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options);
    var lines = await File.ReadAllLinesAsync(".local/equipo-pruebas.txt");
    var credentials = lines.Where(l => l.Split('\t').Length == 3 && l.Split('\t')[0].EndsWith(".demo")).Select(l => l.Split('\t')).ToList();
    var access = await File.ReadAllLinesAsync(".local/acceso.txt");
    credentials.Add(["direccion.local", CanonicalRole.Direction, access.Single(l => l.StartsWith("Contraseña temporal: ")).Split(": ", 2)[1]]);
    foreach (var entry in credentials)
    {
        var credential = await db.IdentityCredentials.AsNoTracking().SingleAsync(c => c.NormalizedUserName == entry[0].ToUpperInvariant());
        var user = await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == credential.UserId);
        var role = await db.RoleAssignmentVersions.AsNoTracking().SingleAsync(r => r.UserId == user.Id && r.ValidTo == null && r.Status == RoleAssignmentStatus.Active);
        var valid = user.Status == AccountStatus.Active && role.RoleCode == entry[1] &&
            new PasswordHasher<AppUser>().VerifyHashedPassword(user, credential.PasswordHash, entry[2]) != PasswordVerificationResult.Failed;
        Console.WriteLine($"Credential {entry[0]}: {(valid ? "VALID" : "OUTDATED")} · {role.RoleCode} · password change required: {user.MustChangePassword}");
    }
}

static async Task<(IActionResult? Result, bool Ran)> Execute(IRazorSessionState session, string path, string method)
{
    var http = new DefaultHttpContext(); http.Request.Path = path; http.Request.Method = method;
    var page = new PageContext { HttpContext = http, RouteData = new Microsoft.AspNetCore.Routing.RouteData(), ActionDescriptor = new CompiledPageActionDescriptor() };
    var filters = new List<IFilterMetadata>(); var instance = new object();
    var context = new PageHandlerExecutingContext(page, filters, null, new Dictionary<string, object?>(), instance);
    var ran = false;
    await new RoleScreenAccessFilter(session).OnPageHandlerExecutionAsync(context, () =>
    {
        ran = true;
        return Task.FromResult(new PageHandlerExecutedContext(page, filters, null, instance));
    });
    return (context.Result, ran);
}

sealed class CheckSession(string? role) : IRazorSessionState
{
    public bool IsInvalid => role is null;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken token = default) => Task.FromResult<SessionSnapshot?>(role is null ? null :
        new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "Fixture", "LOR-001", role, [], DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2)));
}
