using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;
using System.Globalization;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Sgol")
    ?? throw new InvalidOperationException("Falta la conexión local.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (!builder.Environment.IsDevelopment() || settings.Host is not ("localhost" or "127.0.0.1") || settings.Database != "sgol_local")
    throw new InvalidOperationException("Esta herramienta solo prepara sgol_local en localhost y Development.");
builder.Services.AddSgolPersistence(builder.Configuration);
using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
if (args.Contains("--migrate-only")) throw new InvalidOperationException("Usa Sgol.DatabaseSecurity con la cuenta migradora; LocalSetup solo recibe runtime.");
if (!await db.DirectionBootstrapMarkers.AnyAsync())
{
    await scope.ServiceProvider.GetRequiredService<DirectionBootstrapService>().ExecuteAsync(new()
    {
        PersonStableCode = "LOCAL-DIRECCION",
        PersonDisplayName = "Dirección · pruebas locales",
        UserName = "direccion.local",
        InitialPassword = Environment.GetEnvironmentVariable("SGOL_BOOTSTRAP_INITIAL_PASSWORD")
            ?? throw new InvalidOperationException("Falta la contraseña inicial local.")
    });
    Console.WriteLine("Cuenta direccion.local creada. Completa cambio de contraseña y MFA desde Acceso.");
}
if (args.Contains("--seed-weekly-operations"))
{
    var now = scope.ServiceProvider.GetRequiredService<Sgol.BuildingBlocks.Time.IClock>().UtcNow;
    var local = TimeZoneInfo.ConvertTime(now, OperationalSchedule.Zone);
    var today = DateOnly.FromDateTime(local.DateTime);
    var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.Status == RoleAssignmentStatus.Active &&
        r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)).Select(r => r.UserId).FirstAsync();
    var tasks = scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
    var examples = new[] { ("Barrer y trapear", 30, 0, Math.Max(0, local.Hour - 1)),
        ("Regar los árboles", 15, 1, 9), ("Cerrar la tienda", 15, 2, 20),
        ("Limpiar zapatos", 40, 3, 11), ("Limpiar los estantes de exhibición", 25, 4, 10) };
    var added = new List<string>();
    foreach (var (name, minutes, offset, hour) in examples)
    {
        // Re-running the seed never overwrites existing catalogue tasks or their calendars.
        if ((await tasks.ListAsync(actor, default)).Any(t => t.Name == name && t.IsWeeklyOperationsTask)) continue;
        var day = today.AddDays(offset);
        await tasks.SaveAsync(actor, new OperationalTask { Name = name, Revision = 0, IsWeeklyOperationsTask = true,
            Requirement = "COMENTARIO", AssignedRole = CanonicalRole.SalesFloor, PeriodType = "SEMANAL",
            ScheduleMode = "DIAS_FIJOS", WeekDays = 1 << (int)day.DayOfWeek, StartHour = hour,
            DeadlineHours = offset == 0 ? 24 : 2, LoadMinutes = minutes }, default);
        added.Add($"{name}: {day.DayOfWeek} {hour:00}:00, {minutes} min");
    }
    Console.WriteLine($"Tareas de prueba del maestro agregadas: {added.Count}.");
    foreach (var line in added) Console.WriteLine(line);
}
if (args.Contains("--seed-team") || args.Contains("--seed-specializations") || args.Contains("--seed-reports"))
{
    var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction &&
        r.ValidTo == null && r.Status == RoleAssignmentStatus.Active).Select(r => r.UserId).FirstAsync();
    var profiles = scope.ServiceProvider.GetRequiredService<UserProfileService>();
    var examples = new[] {
        ("ana.demo", "Ana · prueba", CanonicalRole.SalesFloor),
        ("bruno.demo", "Bruno · prueba", CanonicalRole.SalesFloor),
        ("carla.demo", "Carla · prueba", CanonicalRole.SalesFloor),
        ("diego.demo", "Diego · prueba", CanonicalRole.SalesFloor),
        ("elena.demo", "Elena · prueba", CanonicalRole.Subcoordination),
        ("fabian.demo", "Fabián · prueba", CanonicalRole.Subcoordination),
        ("gabriela.demo", "Gabriela · prueba", CanonicalRole.Administration),
        ("hugo.demo", "Hugo · prueba", CanonicalRole.Administration)
    };
    var credentials = new List<string>();
    foreach (var (login, name, role) in examples)
    {
        if (await db.IdentityCredentials.AnyAsync(c => c.NormalizedUserName == login.ToUpperInvariant())) continue;
        var result = await profiles.MutateAsync(actor, new UserProfileInput {
            DisplayName = name, UserName = login, RoleCode = role
        }, "save", Guid.CreateVersion7(), default);
        credentials.Add($"{login}\t{role}\t{result.TemporaryPassword}");
    }
    if (credentials.Count > 0)
    {
        var path = Path.GetFullPath(".local/equipo-pruebas.txt");
        await File.AppendAllLinesAsync(path, credentials);
        Console.WriteLine($"Equipo ficticio: {credentials.Count} personas creadas. Accesos temporales en {path}.");
    }
    else Console.WriteLine("El equipo ficticio ya existe; no se modificaron sus accesos.");
}
if (args.Contains("--seed-specializations"))
{
    var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.ValidTo == null && r.Status == RoleAssignmentStatus.Active).Select(r => r.UserId).FirstAsync();
    var specialties = scope.ServiceProvider.GetRequiredService<SpecializationService>();
    foreach (var name in new[] { "Contaduría", "Marketing" })
        if (!await db.Set<Specialization>().AnyAsync(s => s.NormalizedName == name.ToUpperInvariant()))
        { await specialties.SaveAsync(actor, new Specialization { Name = name }, default); db.ChangeTracker.Clear(); }
    var skills = await specialties.OptionsAsync(actor, default);
    var accounting = skills.Single(s => s.Name == "Contaduría").Id;
    var marketing = skills.Single(s => s.Name == "Marketing").Id;
    var profiles = scope.ServiceProvider.GetRequiredService<UserProfileService>();
    foreach (var (login, trial, ids) in new (string, bool, Guid[])[] {
        ("ana.demo", false, [marketing]), ("bruno.demo", false, [marketing]),
        ("carla.demo", true, [marketing]), ("gabriela.demo", false, [accounting, marketing]), ("hugo.demo", false, [accounting]) })
    {
        var p = (await profiles.ListAsync(actor, default)).Single(p => p.UserName == login);
        if (p.IsOnTrial == trial && (p.SpecializationIds ?? []).Order().SequenceEqual(ids.Order())) continue;
        await profiles.MutateAsync(actor, new UserProfileInput { Id = p.Id, Revision = p.Revision, UserName = p.UserName,
            DisplayName = p.DisplayName, RoleCode = p.RoleCode, ManageSpecializations = true, IsOnTrial = trial, SpecializationIds = ids }, "save", Guid.CreateVersion7(), default);
    }
    var tasks = scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
    foreach (var task in new[] {
        new OperationalTask { Name = "Prueba · revisar comprobantes contables", AssignedRole = CanonicalRole.Administration, SpecializationId = accounting, LoadMinutes = 15, Revision = 0 },
        new OperationalTask { Name = "Prueba · preparar publicación de marketing", AssignedRole = CanonicalRole.SalesFloor, SpecializationId = marketing, LoadMinutes = 15, Repetitions = 2, Revision = 0 },
        new OperationalTask { Name = "Prueba · revisar campaña de marketing", AssignedRole = CanonicalRole.SalesFloor, SpecializationId = marketing, LoadMinutes = 15, Revision = 0 } })
        if (!await db.OperationalTasks.AnyAsync(t => t.Name == task.Name)) { await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear(); }
    const string planName = "Prueba · especializaciones";
    if (!await db.DirectionPreparedPlans.AnyAsync(p => p.Name == planName))
    {
        var nextWeek = WeekContract.LocalToday(DateTimeOffset.UtcNow).AddDays(7).ToDateTime(TimeOnly.MinValue);
        var codes = await db.OperationalTasks.Where(t => t.Name.StartsWith("Prueba · ") && t.SpecializationId != null && t.Active && !t.Deleted).Select(t => t.TaskCode).ToArrayAsync();
        var plan = await scope.ServiceProvider.GetRequiredService<IDirectionPreparedPlanService>().CreateAsync(actor, Guid.CreateVersion7(), Guid.CreateVersion7(),
            new DirectionPlanInput(planName, "Equipo de pruebas", "SEMANAL", ISOWeek.GetYear(nextWeek), ISOWeek.GetWeekOfYear(nextWeek), codes));
        foreach (var assignment in plan.Plan.Assignments ?? []) Console.WriteLine($"Prueba: {assignment.Name} → {assignment.PersonName} · {assignment.SpecializationName}");
    }
    Console.WriteLine("Contaduría y Marketing creadas, perfiles ficticios configurados y tres tareas de prueba disponibles. Las tareas anteriores no requieren especialización.");
}
if (args.Contains("--create-social-task"))
{
    const string name = "Compartir contenido en redes";
    if (!await db.OperationalTasks.AnyAsync(t => t.Name == name && !t.Deleted))
    {
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction &&
            r.ValidTo == null && r.Status == RoleAssignmentStatus.Active).Select(r => r.UserId).FirstAsync();
        await scope.ServiceProvider.GetRequiredService<OperationalTaskService>().SaveAsync(actor, new OperationalTask {
            Name = name, PeriodType = "OCASIONAL", Requirement = "CAPTURA_PANTALLA", AssignedRole = CanonicalRole.SalesFloor,
            LoadMinutes = 10, DeadlineHours = 24, Revision = 0
        }, default);
        Console.WriteLine("Plantilla Compartir contenido en redes creada. No se generaron asignaciones.");
    }
    else Console.WriteLine("La plantilla ya existe; se conserva su configuración.");
}
if (args.Contains("--seed-reports")) await ReportSimulationSeeder.SeedAsync(db);
if (args.Contains("--seed-board")) await LiveBoardSimulationSeeder.SeedAsync(db, scope.ServiceProvider);
if (args.Contains("--prepare-board-ana")) await LiveBoardSimulationSeeder.PrepareAnaAsync(db);
if (args.Contains("--seed-board-deadlines"))
{
    await BoardDeadlineSimulationSeeder.SeedAsync(db);
    return;
}
if (args.Contains("--seed-board-all-plans"))
{
    await BoardAllPlansSeeder.SeedAsync(db, scope.ServiceProvider);
    return;
}
Console.WriteLine("Datos locales preparados con runtime; el almacenamiento se aprovisiona por separado.");
