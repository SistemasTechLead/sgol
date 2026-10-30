using Sgol.Web.Infrastructure.Authentication;
using Microsoft.AspNetCore.DataProtection;
using System.Text.RegularExpressions;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;

var connection = Environment.GetEnvironmentVariable("SGOL_PROFILE_CHECKS_CONNECTION") ?? throw new Exception("Set a dedicated test connection.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host != "localhost" || settings.Database is null || !settings.Database.StartsWith("sgol_profile_checks_"))
    throw new Exception("Checks require a dedicated localhost sgol_profile_checks_* database.");
await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options);
await db.Database.MigrateAsync();
if (await db.People.AnyAsync()) throw new Exception("Test database must be empty.");
var clock = new CheckClock();
var hasher = new PasswordHasher<AppUser>();
var audit = new AuditTransaction(db);
var profiles = new UserProfileService(db, audit, clock, hasher);
var admin = Seed("ADMIN", CanonicalRole.Administration);
var director = Seed("DIR", CanonicalRole.Direction);
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
Check((await profiles.ListAsync(admin, default)).Count == 2, "Administration sees every profile including itself");
Check((await profiles.ListAsync(director, default)).Count == 2, "Direction sees every profile including itself");
foreach (var role in new[] { CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
{
    var actor = Seed(role, role); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await Rejected(() => profiles.ListAsync(actor, default), 403);
}
var input = new UserProfileInput { DisplayName = "Ana López", UserName = "ana.lopez", RoleCode = CanonicalRole.SalesFloor };
var created = await profiles.MutateAsync(admin, input, "save", Guid.CreateVersion7(), default);
Check(created.TemporaryPassword is { Length: >= 16 }, "Generated temporary password");
var user = await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == created.Id);
var credential = await db.IdentityCredentials.AsNoTracking().SingleAsync(c => c.UserId == user.Id);
Check(user.MustChangePassword && user.MfaEnrolledAt is null, "First access setup required");
Check(hasher.VerifyHashedPassword(user, credential.PasswordHash, created.TemporaryPassword!) != PasswordVerificationResult.Failed,
    "Temporary password is hashed and usable");
var replay = await profiles.MutateAsync(admin, input, "save", Guid.CreateVersion7(), default);
Check(replay.Replayed && replay.TemporaryPassword is null && replay.Id == created.Id, "Replay creates no duplicate and exposes no secret");
var duplicate = new UserProfileInput { DisplayName = "Otra persona", UserName = "ANA.LOPEZ", RoleCode = CanonicalRole.Direction };
var before = await db.People.CountAsync();
await Rejected(() => profiles.MutateAsync(admin, duplicate, "save", Guid.CreateVersion7(), default), 409);
Check(await db.People.CountAsync() == before, "Duplicate username rolls back person, employment, user and role");
var profile = (await profiles.ListAsync(admin, default)).Single(p => p.Id == created.Id);
Check(profile.RoleCode == CanonicalRole.SalesFloor, "Role is assigned when user is created");
var change = Input(profile); change.DisplayName = "Ana López García"; change.UserName = "ana.garcia"; change.RoleCode = CanonicalRole.Subcoordination;
await profiles.MutateAsync(admin, change, "save", Guid.CreateVersion7(), default);
await Rejected(() => profiles.MutateAsync(admin, Input(profile), "save", Guid.CreateVersion7(), default), 409);
profile = (await profiles.ListAsync(admin, default)).Single(p => p.Id == created.Id);
Check(profile.DisplayName == "Ana López García" && profile.UserName == "ana.garcia" && profile.RoleCode == CanonicalRole.Subcoordination,
    "Name, username and role update together");
Check((await db.AppUsers.AsNoTracking().SingleAsync(u => u.Id == created.Id)).SecurityStamp != user.SecurityStamp, "Role edit invalidates old sessions");
await profiles.MutateAsync(admin, Input(profile), "deactivate", Guid.CreateVersion7(), default);
profile = (await profiles.ListAsync(admin, default)).Single(p => p.Id == created.Id);
Check(profile.Status == AccountStatus.Inactive, "Deactivate prevents access");
change = Input(profile); change.RoleCode = CanonicalRole.Direction;
await profiles.MutateAsync(admin, change, "save", Guid.CreateVersion7(), default);
profile = (await profiles.ListAsync(admin, default)).Single(p => p.Id == created.Id);
Check(profile.RoleCode == CanonicalRole.Direction && profile.Status == AccountStatus.Inactive, "Inactive profiles can be edited without activation");
var activation = await profiles.MutateAsync(admin, Input(profile), "reactivate", Guid.CreateVersion7(), default);
Check(activation.TemporaryPassword is not null && activation.TemporaryPassword != created.TemporaryPassword, "Reactivate issues fresh temporary password");
profile = (await profiles.ListAsync(admin, default)).Single(p => p.Id == created.Id);
await profiles.MutateAsync(admin, Input(profile), "delete", Guid.CreateVersion7(), default);
Check(!(await profiles.ListAsync(admin, default)).Any(p => p.Id == created.Id), "Deleted profile disappears");
Check(await db.People.AnyAsync(p => p.Id == user.PersonId) && await db.RoleAssignmentVersions.AnyAsync(r => r.UserId == user.Id), "Deletion preserves referenced history");
Check(!await db.EmploymentVersions.AnyAsync(e => e.PersonId == user.PersonId && e.ValidTo == null && e.Status == EmploymentStatus.Active), "Deleted person excluded from future work");
await Rejected(() => profiles.MutateAsync(admin, Input(profile), "reactivate", Guid.CreateVersion7(), default), 404);
var own = (await profiles.ListAsync(admin, default)).Single(p => p.Id == admin);
await Rejected(() => profiles.MutateAsync(admin, Input(own), "delete", Guid.CreateVersion7(), default), 409);
await Rejected(() => profiles.MutateAsync(admin, Input(own), "deactivate", Guid.CreateVersion7(), default), 409);
change = Input(own); change.RoleCode = CanonicalRole.Direction;
await Rejected(() => profiles.MutateAsync(admin, change, "save", Guid.CreateVersion7(), default), 409);
change = Input(own); change.DisplayName = "Mi perfil actualizado";
await profiles.MutateAsync(admin, change, "save", Guid.CreateVersion7(), default);
Check((await profiles.ListAsync(admin, default)).Single(p => p.Id == admin).DisplayName == change.DisplayName, "Administrator edits own profile");
var legacy = new EfAccountAdministrationService(db, audit, clock, new Uuid7Generator(clock), hasher);
Check(!(await legacy.ListAsync(admin, Guid.CreateVersion7())).Any(u => u.Id == created.Id), "Legacy list excludes deleted profiles");
Check((await legacy.ListAsync(director, Guid.CreateVersion7())).Count > 0, "Legacy API also allows Direction");
var records = await db.IdempotencyRecords.AsNoTracking().ToListAsync();
Check(records.All(r => !r.ResponsePayload!.RootElement.GetRawText().Contains(created.TemporaryPassword!)), "No temporary password persisted in idempotency snapshots");
var events = await db.AuditEvents.AsNoTracking().ToListAsync();
Check(events.All(e => e.AfterData is null || !e.AfterData.RootElement.GetRawText().Contains(created.TemporaryPassword!)), "No temporary password persisted in audit");
// Exercise the compiled Razor page, model binding and antiforgery against the isolated database.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(Sgol.Web.Pages.People.IndexModel).Assembly.GetName().Name,
    ContentRootPath = Path.GetFullPath("src/Sgol.Web"), EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();
builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddScoped<AuditTransaction>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Planning.OperationalSchedulingPolicyService>();
builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Planning.AssignmentRefreshService>();
builder.Services.AddScoped<IPersonAvatarReader, PersonAvatarService>();
builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Planning.PersonalScheduleService>();
builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Planning.OperationalProcessCatalogService>();
builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();
builder.Services.AddScoped<Sgol.Planning.Contracts.IDirectionPreparedPlanService, Sgol.Web.Infrastructure.Persistence.Planning.EfDirectionPreparedPlanService>();
builder.Services.AddScoped<Sgol.Configuration.Contracts.IDirectionPlanningCatalogReader, Sgol.Web.Infrastructure.Persistence.Configuration.DirectionPlanningCatalogReader>();
// These form checks never upload files; only the catalog and save paths use this fixture.
builder.Services.AddScoped(s => new Sgol.Web.Infrastructure.Persistence.Planning.OperationalTaskService(
    s.GetRequiredService<SgolDbContext>(), s.GetRequiredService<Sgol.Planning.Contracts.IDirectionPreparedPlanService>(), clock, null!, null!));
builder.Services.AddSingleton<IClock>(clock);
builder.Services.AddSingleton<IUuidGenerator>(new Uuid7Generator(clock));
builder.Services.AddSingleton(typeof(UserProfileService).Assembly.GetType("Sgol.Web.Infrastructure.Authentication.AuthenticationTelemetry")!);
builder.Services.AddSingleton(typeof(UserProfileService).Assembly.GetType("Sgol.Web.Infrastructure.Persistence.Identity.TotpSecretProtector")!);
builder.Services.AddSingleton<IPasswordHasher<AppUser>>(hasher);
var fixtureSession = new FixtureSession(director, clock.UtcNow) { Role = CanonicalRole.Direction };
builder.Services.AddSingleton<IRazorSessionState>(fixtureSession);
builder.Services.AddScoped(s => new RazorAntiforgeryBridge(s.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>(), null!));
await using var app = builder.Build();
app.UseStaticFiles(); app.MapRazorPages(); await app.StartAsync();
using var http = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(app.Urls.Single()) };
var html = await http.GetStringAsync("/personas-y-accesos");
Check(html.Contains("Tu perfil") && html.Contains("Perfiles de usuarios") && !html.Contains("Registrar persona"), "Rebuilt Razor page renders profiles and own profile");
html = await http.GetStringAsync("/personas-y-accesos?create=true");
Check(html.Contains("Input.RoleCode") && html.Contains("Input.DisplayName") && html.Contains("__RequestVerificationToken"), "Create form includes role and CSRF token");
var csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
Check(csrf.Length > 0, "CSRF token can be submitted");
var data = new Dictionary<string, string> { ["Input.DisplayName"] = "Usuario desde formulario", ["Input.UserName"] = "form.user",
    ["Input.RoleCode"] = CanonicalRole.SalesFloor, ["Input.IntentKey"] = Guid.CreateVersion7().ToString(), ["Input.Id"] = Guid.Empty.ToString(), ["Input.Revision"] = "0" };
var deniedPost = await http.PostAsync("/personas-y-accesos?handler=Save", new FormUrlEncodedContent(data));
Check((int)deniedPost.StatusCode == 400, "POST without CSRF is blocked");
data["__RequestVerificationToken"] = csrf;
var posted = await http.PostAsync("/personas-y-accesos?handler=Save", new FormUrlEncodedContent(data));
html = await posted.Content.ReadAsStringAsync();
Check(posted.IsSuccessStatusCode && html.Contains("Contraseña temporal") && html.Contains("form.user"), "Real create POST succeeds and displays temporary password");
var formUser = (await profiles.ListAsync(admin, default)).Single(p => p.UserName == "form.user");
var statusData = new Dictionary<string, string> { ["Input.Id"] = formUser.Id.ToString(), ["Input.Revision"] = formUser.Revision.ToString(),
    ["Input.IntentKey"] = Guid.CreateVersion7().ToString(), ["__RequestVerificationToken"] = csrf };
posted = await http.PostAsync("/personas-y-accesos?handler=Deactivate", new FormUrlEncodedContent(statusData));
Check(posted.IsSuccessStatusCode && (await profiles.ListAsync(admin, default)).Single(p => p.Id == formUser.Id).Status == AccountStatus.Inactive,
    "Status POST binds correctly without profile fields");
fixtureSession.Role = CanonicalRole.Direction; fixtureSession.Actor = director;
html = await http.GetStringAsync("/personas-y-accesos");
Check(html.Contains("/personas-y-accesos/especializaciones"), "Director sees specialization entry only in People");
html = await http.GetStringAsync("/personas-y-accesos/especializaciones?create=true");
csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
var specializationForm = new Dictionary<string, string> { ["Input.Name"] = "Marketing UI", ["Input.Active"] = "true",
    ["Input.Id"] = Guid.Empty.ToString(), ["Input.Revision"] = "0", ["__RequestVerificationToken"] = csrf };
posted = await http.PostAsync("/personas-y-accesos/especializaciones?handler=Save", new FormUrlEncodedContent(specializationForm));
Check(posted.IsSuccessStatusCode && (await posted.Content.ReadAsStringAsync()).Contains("Marketing UI"), "Specialization form creates and redirects successfully");
var uiSkill = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking().SingleAsync(s => s.Name == "Marketing UI");
html = await http.GetStringAsync("/personas-y-accesos?create=true");
Check(html.Contains("Input.IsOnTrial") && html.Contains("Input.SpecializationIds"), "Director profile form includes probation and multiple specialties");
html = await http.GetStringAsync("/tareas?create=true");
Check(html.Contains("Requiere especializaci") && html.Contains("data-requires-specialization") && html.Contains("Marketing UI"), "Task editor renders specialty checkbox and active selector");
csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
var taskForm = new Dictionary<string, string> { ["Input.Name"] = "UI specialty task", ["Input.Revision"] = "0", ["Input.AssignedRole"] = CanonicalRole.SalesFloor,
    ["RequiresSpecialization"] = "true", ["Input.SpecializationId"] = "", ["__RequestVerificationToken"] = csrf };
posted = await http.PostAsync("/tareas?handler=Save", new FormUrlEncodedContent(taskForm));
Check((int)posted.StatusCode == 422, "Checked specialization requires a selection on the server");
taskForm["Input.SpecializationId"] = uiSkill.Id.ToString();
posted = await http.PostAsync("/tareas?handler=Save", new FormUrlEncodedContent(taskForm));
Check(posted.IsSuccessStatusCode && await db.OperationalTasks.AnyAsync(t => t.Name == "UI specialty task" && t.SpecializationId == uiSkill.Id), "Real task form persists specialty requirement");
taskForm["Input.Name"] = "UI ordinary task"; taskForm["RequiresSpecialization"] = "false";
posted = await http.PostAsync("/tareas?handler=Save", new FormUrlEncodedContent(taskForm));
Check(posted.IsSuccessStatusCode && await db.OperationalTasks.AnyAsync(t => t.Name == "UI ordinary task" && t.SpecializationId == null), "Unchecked requirement clears even a submitted specialty");
fixtureSession.Role = CanonicalRole.Administration; fixtureSession.Actor = admin;
Check((int)(await http.GetAsync("/personas-y-accesos/especializaciones")).StatusCode == 403, "Administration cannot open specializations directly");
Check((int)(await http.GetAsync("/personas-y-accesos")).StatusCode==403, "Administration cannot open the Direction-only People screen");
posted=await http.PostAsync("/personas-y-accesos?handler=ScheduleSave",new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"]=csrf }));
Check((int)posted.StatusCode==403,"Administration cannot write personal schedules through forged POST");
posted = await http.PostAsync("/personas-y-accesos/especializaciones?handler=Save", new FormUrlEncodedContent(specializationForm));
Check((int)posted.StatusCode == 403, "Administration cannot create specialties through a forged POST");
fixtureSession.Role = CanonicalRole.Direction; fixtureSession.Actor = director;
html = await http.GetStringAsync("/personas-y-accesos");
Check(html.Contains("navegacion-lateral__item\" href=\"/personas-y-accesos") && html.Contains("Perfiles de usuarios"), "Direction sees sidebar link and page");
html = await http.GetStringAsync($"/personas-y-accesos?edit={director}");
Check(html.Contains("Editar mi perfil") && html.Contains("value=\"DIRECCION\""), "Direction own-profile form preserves Direction role");
Check(html.Contains("Horario laboral") && html.Contains("Excepciones por fecha") && !html.Contains("Cobertura de cierre"), "People keeps schedules and exceptions; shared closing is no longer inside a person");
csrf = Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
var schedulePost=new Dictionary<string,string> { ["Schedule.UserId"]=director.ToString(),["Schedule.Key"]=Guid.CreateVersion7().ToString(),["Schedule.Revision"]="0",
    ["Schedule.From"]=Sgol.Planning.Contracts.WeekContract.LocalToday(clock.UtcNow).ToString("yyyy-MM-dd"),["__RequestVerificationToken"]=csrf };
foreach(var day in Enumerable.Range(0,7)) schedulePost[$"Schedule.Days[{day}].Mode"]="REST";
var securityBefore=await db.AppUsers.AsNoTracking().Where(u=>u.Id==director).Select(u=>u.SecurityStamp).SingleAsync();
posted=await http.PostAsync("/personas-y-accesos?handler=SchedulePreview",new FormUrlEncodedContent(schedulePost));
html=await posted.Content.ReadAsStringAsync();
Check(posted.IsSuccessStatusCode && html.Contains("Guardar horario revisado"),"Real schedule POST renders after disabled empty interval fields are omitted");
var fingerprint=Regex.Match(html,"name=\"Schedule.Fingerprint\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
Check(fingerprint.Length>0 && !await db.Set<Sgol.Web.Infrastructure.Persistence.Planning.PersonalScheduleVersion>().AnyAsync(),"Schedule preview writes no versions and returns a review fingerprint");
schedulePost["Schedule.Fingerprint"]=fingerprint;
posted=await http.PostAsync("/personas-y-accesos?handler=ScheduleSave",new FormUrlEncodedContent(schedulePost));
Check(posted.IsSuccessStatusCode,"Reviewed schedule saves through the real protected form");
posted=await http.PostAsync("/personas-y-accesos?handler=ScheduleSave",new FormUrlEncodedContent(schedulePost));
Check(posted.IsSuccessStatusCode && await db.Set<Sgol.Web.Infrastructure.Persistence.Planning.PersonalScheduleVersion>().CountAsync()==1,"Double schedule POST stores one version");
Check(await db.AppUsers.AsNoTracking().Where(u=>u.Id==director).Select(u=>u.SecurityStamp).SingleAsync()==securityBefore,"Schedule edits preserve identity sessions");
schedulePost.Remove("Schedule.Days[0].Mode");
posted=await http.PostAsync("/personas-y-accesos?handler=SchedulePreview",new FormUrlEncodedContent(schedulePost));
Check((int)posted.StatusCode==422,"Partial schedule POST cannot erase an omitted day");
html = await http.GetStringAsync("/personas-y-accesos?create=true");
csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
data["Input.UserName"] = "direction.created"; data["Input.IntentKey"] = Guid.CreateVersion7().ToString(); data["__RequestVerificationToken"] = csrf;
posted = await http.PostAsync("/personas-y-accesos?handler=Save", new FormUrlEncodedContent(data));
Check(posted.IsSuccessStatusCode && (await profiles.ListAsync(director, default)).Any(p => p.UserName == "direction.created"), "Direction can create users through the real form");
var directionOwn = (await profiles.ListAsync(director, default)).Single(p => p.Id == director);
var ownEdit = Input(directionOwn); ownEdit.DisplayName = "Dirección actualizada";
await profiles.MutateAsync(director, ownEdit, "save", Guid.CreateVersion7(), default);
directionOwn = (await profiles.ListAsync(director, default)).Single(p => p.Id == director);
await Rejected(() => profiles.MutateAsync(director, Input(directionOwn), "delete", Guid.CreateVersion7(), default), 409);
ownEdit = Input(directionOwn); ownEdit.RoleCode = CanonicalRole.Administration;
await Rejected(() => profiles.MutateAsync(director, ownEdit, "save", Guid.CreateVersion7(), default), 409);
var directionTarget = (await profiles.ListAsync(director, default)).Single(p => p.UserName == "direction.created");
var directionEdit = Input(directionTarget); directionEdit.RoleCode = CanonicalRole.Subcoordination;
await profiles.MutateAsync(director, directionEdit, "save", Guid.CreateVersion7(), default);
directionTarget = (await profiles.ListAsync(director, default)).Single(p => p.Id == directionTarget.Id);
await profiles.MutateAsync(director, Input(directionTarget), "deactivate", Guid.CreateVersion7(), default);
directionTarget = (await profiles.ListAsync(director, default)).Single(p => p.Id == directionTarget.Id);
await profiles.MutateAsync(director, Input(directionTarget), "reactivate", Guid.CreateVersion7(), default);
directionTarget = (await profiles.ListAsync(director, default)).Single(p => p.Id == directionTarget.Id);
await profiles.MutateAsync(director, Input(directionTarget), "delete", Guid.CreateVersion7(), default);
Check(!(await profiles.ListAsync(director, default)).Any(p => p.Id == directionTarget.Id), "Direction can edit roles, deactivate, reactivate and delete");
fixtureSession.Role = CanonicalRole.SalesFloor;
Check((int)(await http.GetAsync("/personas-y-accesos")).StatusCode == 403, "Other roles still cannot access the page");
// Password-only access preserves temporary-password setup and the original MFA flow.
builder.Configuration["Authentication:RequireMfa"] = "false";
var authInput = new UserProfileInput { DisplayName = "Prueba sin MFA", UserName = "auth.password.only", RoleCode = CanonicalRole.SalesFloor };
var authCreated = await profiles.MutateAsync(admin, authInput, "save", Guid.CreateVersion7(), default);
using var authScope = app.Services.CreateScope();
var authType = typeof(UserProfileService).Assembly.GetType("Sgol.Web.Infrastructure.Persistence.Identity.EfHostedAuthenticationService")!;
var authService = (IHostedAuthenticationService)ActivatorUtilities.CreateInstance(authScope.ServiceProvider, authType);
var flowResult = await authService.LoginAsync(authInput.UserName, authCreated.TemporaryPassword!, Guid.CreateVersion7());
Check(flowResult.NextStep == AuthenticationNextStep.ChangePassword && flowResult.Session is null, "Temporary password must still be changed");
flowResult = await authService.ChangePasswordAsync(flowResult.ChallengeId, null, authCreated.TemporaryPassword!, "Changed!1234567890", Guid.CreateVersion7());
Check(flowResult.NextStep == AuthenticationNextStep.Authenticated && flowResult.Session?.AuthenticationMethod == "pwd", "Changing temporary password grants password-only session");
flowResult = await authService.LoginAsync(authInput.UserName, "Changed!1234567890", Guid.CreateVersion7());
var passwordSession = flowResult.Session!;
Check(flowResult.NextStep == AuthenticationNextStep.Authenticated && passwordSession.AuthenticationMethod == "pwd", "Username and password grant session without MFA");
Check(AuthenticationPrincipalFactory.Create(passwordSession).FindFirst(SgolClaimTypes.AuthenticationMethod)?.Value == "pwd", "Password session does not claim MFA authentication");
Check(await authService.ValidateSessionAsync(passwordSession.UserId, passwordSession.SecurityStamp, passwordSession.MfaAuthenticatedAt, passwordSession.AbsoluteExpiresAt, sessionId: passwordSession.SessionId) is not null, "Session validates without an enrolled authenticator");
try { await authService.LoginAsync(authInput.UserName, "Wrong!1234567890", Guid.CreateVersion7()); throw new Exception("Wrong password was accepted"); }
catch (AuthenticationFailedException) { Check(true, "Wrong password is still rejected"); }
builder.Configuration["Authentication:RequireMfa"] = "true";
flowResult = await authService.LoginAsync(authInput.UserName, "Changed!1234567890", Guid.CreateVersion7());
Check(flowResult.NextStep == AuthenticationNextStep.EnrollMfa && flowResult.Session is null, "Re-enabling MFA restores enrollment flow");
Check(await authService.ValidateSessionAsync(passwordSession.UserId, passwordSession.SecurityStamp, passwordSession.MfaAuthenticatedAt, passwordSession.AbsoluteExpiresAt, sessionId: passwordSession.SessionId) is null, "Re-enabling MFA rejects unenrolled sessions");
builder.Configuration["Authentication:RequireMfa"] = "false";
await app.StopAsync();
Console.WriteLine("All user profile checks passed.");

Guid Seed(string name, string role)
{
    var p = new Person { Id = Guid.CreateVersion7(), StableCode = name, DisplayName = name, CreatedAt = clock.UtcNow };
    var u = new AppUser { Id = Guid.CreateVersion7(), PersonId = p.Id, Status = AccountStatus.Active, SecurityStamp = "stamp-" + name, MfaEnrolledAt = clock.UtcNow };
    db.People.Add(p); db.AppUsers.Add(u);
    db.IdentityCredentials.Add(new() { UserId = u.Id, UserName = name, NormalizedUserName = name, PasswordHash = "test" });
    db.EmploymentVersions.Add(new(Guid.CreateVersion7(), p.Id, BranchScope.LorettaId, EmploymentStatus.Active, clock.UtcNow));
    db.RoleAssignmentVersions.Add(new() { Id = Guid.CreateVersion7(), UserId = u.Id, BranchId = BranchScope.LorettaId, RoleCode = role, Status = RoleAssignmentStatus.Active, ValidFrom = clock.UtcNow });
    return u.Id;
}
static UserProfileInput Input(UserProfile p) => new() { Id = p.Id, DisplayName = p.DisplayName, UserName = p.UserName, RoleCode = p.RoleCode, Revision = p.Revision };
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
static async Task Rejected(Func<Task> work, int status)
{
    try { await work(); throw new Exception("Expected rejection " + status); }
    catch (UserProfileException e) when (e.Status == status) { Console.WriteLine("PASS rejected " + status); }
}
sealed class CheckClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 5, 23, 0, 0, TimeSpan.Zero); }


sealed class FixtureSession(Guid actor, DateTimeOffset now) : IRazorSessionState
{
    public Guid Actor { get; set; } = actor;
    public string Role { get; set; } = CanonicalRole.Administration;
    public bool IsInvalid => false;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(
        new(Actor, Actor, "admin", "Administración de prueba", "LOR-001", Role, [], now, now.AddHours(1), now.AddHours(8)));
}





