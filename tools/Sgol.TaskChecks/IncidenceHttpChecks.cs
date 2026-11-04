using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

internal static partial class IncidenceChecks
{
    sealed class Session(Guid user, Guid person, Clock clock, string role) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(user, person, "incidence-check", "Cuenta de prueba", "LOR-001", role,
            [WeekAuthorization.View, "PER-PERSONA-ADMIN", "PER-SUPERVISION-VER"], clock.UtcNow, clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2)));
    }
    static async Task HttpChecks(SgolDbContext db, Clock clock)
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(root.FullName, "src/Sgol.Web/Sgol.Web.csproj"))) root = root.Parent ?? throw new Exception("Repository root not found");
        Directory.SetCurrentDirectory(root.FullName);
        clock.UtcNow = OperationalSchedule.Instant(new(2026, 11, 4), 10);
        var fixture = await QaFixtures.CreateAsync(db);
        var conflictFixture = await QaFixtures.CreateAsync(db);
        var protectedRow = await Row(db,conflictFixture,clock,clock.UtcNow.AddHours(1),30);
        db.Add(new OperationalAttachment {AssignmentId=protectedRow.Id,FileName="protected.pdf",ContentType="application/pdf",Content=[1,2,3],CreatedAt=clock.UtcNow});
        db.Add(new OperationalCapacity {Id=Guid.CreateVersion7(),PersonId=conflictFixture.OtherPerson,From=new(2026,11,4),Through=new(2026,11,4),Percent=0,Reason="Sobrecarga de formulario aislado"});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var supervisor = await QaFixtures.CreateAsync(db);
        var supervisorRole = await db.RoleAssignmentVersions.SingleAsync(r => r.UserId == supervisor.Director && r.Status == RoleAssignmentStatus.Active);
        db.Entry(supervisorRole).Property(r => r.RoleCode).CurrentValue = CanonicalRole.Subcoordination;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var supervisorPerson = await db.AppUsers.Where(u => u.Id == supervisor.Director).Select(u => u.PersonId).SingleAsync();
        await Row(db, fixture, clock, clock.UtcNow.AddHours(1));
        await Row(db, fixture, clock, clock.UtcNow.AddHours(-1));
        await Row(db, conflictFixture, clock, clock.UtcNow.AddHours(-1));
        var person = await db.People.SingleAsync(p => p.Id == fixture.Person);
        person.DisplayName = "Ausente para formulario real con nombre largo y descripción de prueba";
        await db.SaveChangesAsync();
        var directorPerson = await db.AppUsers.Where(u => u.Id == fixture.Director).Select(u => u.PersonId).SingleAsync();
        var scopedService = new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        var privateRequest = new IncidenceRequest(directorPerson,new(2026,11,6),Guid.NewGuid(),"Ausencia fuera del alcance de Subcoordinación",OperationalSchedule.Instant(new(2026,11,6)),[]);
        var privatePreview = await scopedService.PreviewAsync(fixture.Director,privateRequest,default);
        var privateReceipt = await scopedService.ApplyAsync(fixture.Director,privateRequest,privatePreview.Fingerprint,default); db.ChangeTracker.Clear();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName,
            WebRootPath = Path.GetFullPath("src/Sgol.Web/wwwroot"), EnvironmentName = "Development" });
        builder.WebHost.UseUrls("https://localhost:7297"); builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7297";
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection(); builder.Services.AddAntiforgery();
        builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
        builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>();
        builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalSupervisionService>(); builder.Services.AddScoped<OperationalIncidenceService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<IRazorSessionState>(sp => sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Cookies["incidence-role"] switch {
            "worker" => new Session(fixture.Worker, fixture.Person, clock, CanonicalRole.SalesFloor),
            "supervisor" => new Session(supervisor.Director, supervisorPerson, clock, CanonicalRole.Subcoordination),
            _ => new Session(fixture.Director, directorPerson, clock, CanonicalRole.Direction) });
        await using var app = builder.Build();
        app.Use(async (ctx, next) => {
            var user = ctx.Request.Cookies["incidence-role"] switch { "worker" => fixture.Worker, "supervisor" => supervisor.Director, _ => fixture.Director };
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "TEST_ONLY")); await next();
        });
        app.UseStaticFiles(); app.MapRazorPages();
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new("https://localhost:7297") };
        string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var html = await client.GetStringAsync("/supervision/incidencias");
        Check(html.Contains("Registrar ausencia"), "real Razor page renders");
        Check((await client.PostAsync("/supervision/incidencias?handler=Apply", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest, "form requires CSRF");
        var form = new Dictionary<string, string> { ["Day"] = "2026-11-05", ["PersonId"] = fixture.Person.ToString(), ["Key"] = Guid.NewGuid().ToString(),
            ["Reason"] = "Ausencia futura desde formulario HTTP real", ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") };
        var response = await client.PostAsync("/supervision/incidencias?handler=Preview", new FormUrlEncodedContent(form));
        html = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode || !html.Contains("Confirmar ausencia y cambios")) await File.WriteAllTextAsync(Path.GetFullPath(".local/incidence-http-preview-failure.html"), html);
        Check(response.IsSuccessStatusCode && html.Contains("Confirmar ausencia y cambios"), "HTTP preview binds person and date");
        form["Fingerprint"] = Field(html, "Fingerprint"); form["NotBefore"] = Field(html, "NotBefore");
        response = await client.PostAsync("/supervision/incidencias?handler=Apply", new FormUrlEncodedContent(form));
        Check(response.StatusCode == HttpStatusCode.Redirect, "HTTP confirm persists joint operation");
        html = await client.GetStringAsync(response.Headers.Location!);
        Check(html.Contains("Ausencia confirmada"), "receipt renders after redirect");
        response = await client.PostAsync("/supervision/incidencias?handler=Apply", new FormUrlEncodedContent(form));
        Check(response.StatusCode == HttpStatusCode.Redirect, "HTTP duplicate submission replays");
        // A real clock has fractional seconds; the hidden field must round-trip all
        // ticks used to fingerprint the reviewed request, independently of culture.
        var preciseFixture = await QaFixtures.CreateAsync(db);
        await Row(db,preciseFixture,clock,clock.UtcNow.AddHours(1));
        await Row(db,preciseFixture,clock,clock.UtcNow.AddHours(-1));
        clock.UtcNow = clock.UtcNow.AddTicks(1234567);
        var preciseForm = new Dictionary<string,string> {
            ["Day"]="2026-11-04", ["PersonId"]=preciseFixture.Person.ToString(), ["Key"]=Guid.NewGuid().ToString(),
            ["Reason"]="Confirmación con precisión real del reloj", ["Reflow"]="true",
            ["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken") };
        response = await client.PostAsync("/supervision/incidencias?handler=Preview",new FormUrlEncodedContent(preciseForm));
        var preciseHtml = await response.Content.ReadAsStringAsync();
        Check(response.IsSuccessStatusCode && preciseHtml.Contains("data-incidence-confirm"),"HTTP fractional-clock preview is confirmable");
        preciseForm["NotBefore"]=Field(preciseHtml,"NotBefore"); preciseForm["Fingerprint"]=Field(preciseHtml,"Fingerprint");
        Check(DateTimeOffset.TryParse(preciseForm["NotBefore"],System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var roundTripped) && roundTripped==clock.UtcNow.AddMinutes(2),"HTTP hidden time preserves exact fractional seconds");
        clock.UtcNow = clock.UtcNow.AddSeconds(15);
        response = await client.PostAsync("/supervision/incidencias?handler=Apply",new FormUrlEncodedContent(preciseForm));
        Check(response.StatusCode==HttpStatusCode.Redirect,"HTTP fractional-clock preview confirms after normal review delay");
        response = await client.PostAsync("/supervision/incidencias?handler=Apply",new FormUrlEncodedContent(preciseForm));
        Check(response.StatusCode==HttpStatusCode.Redirect,"HTTP precise-time confirmation replays idempotently");
        var invalidRecipientForm = new Dictionary<string, string> {
            ["Day"] = "2026-11-04", ["PersonId"] = conflictFixture.Person.ToString(), ["Key"] = Guid.NewGuid().ToString(),
            ["Reason"] = "Destinataria incompatible enviada desde formulario antiguo", ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"),
            ["Decisions.Index"] = "0", ["Decisions[0].AssignmentId"] = protectedRow.Id.ToString(), ["Decisions[0].Action"] = "TRANSFER",
            ["Decisions[0].RecipientId"] = supervisorPerson.ToString(), ["Decisions[0].AuthorizeOverload"] = "true"
        };
        response = await client.PostAsync("/supervision/incidencias?handler=Preview", new FormUrlEncodedContent(invalidRecipientForm));
        var invalidHtml = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var recipientSelect = Regex.Match(invalidHtml, "<select[^>]*name=\"Decisions\\[0\\]\\.RecipientId\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
        Check(response.IsSuccessStatusCode && invalidHtml.Contains("Su rol es Subcoordinación") && !invalidHtml.Contains("data-incidence-confirm"), "HTTP old incompatible selection gives precise cause and cannot confirm");
        Check(recipientSelect.Contains(conflictFixture.OtherPerson.ToString()) && !recipientSelect.Contains(supervisorPerson.ToString()) && invalidHtml.Contains("La selección anterior dejó de ser válida"), "HTTP excludes incompatible option and explains discarded selection");
        invalidRecipientForm["Decisions[0].Action"] = "RETIRE";
        response = await client.PostAsync("/supervision/incidencias?handler=Preview", new FormUrlEncodedContent(invalidRecipientForm));
        var retirementHtml = await response.Content.ReadAsStringAsync();
        Check(response.IsSuccessStatusCode && retirementHtml.Contains("data-incidence-confirm") &&
            Field(retirementHtml, "Decisions[keep0].RecipientId") == "" && Field(retirementHtml, "Decisions[keep0].AuthorizeOverload") == "false",
            "HTTP retirement discards forged recipient and overload before review");
        var staleForm = new Dictionary<string,string>(invalidRecipientForm) {
            ["Fingerprint"]="deliberately-stale", ["NotBefore"]=Field(retirementHtml,"NotBefore") };
        response=await client.PostAsync("/supervision/incidencias?handler=Apply",new FormUrlEncodedContent(staleForm));
        var staleHtml=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Check(response.StatusCode==HttpStatusCode.Conflict && staleHtml.Contains("no coinciden con la vista previa") && !staleHtml.Contains("límite temporal"),"HTTP state mismatch is not mislabeled as a temporal expiry");
        Check(Field(staleHtml,"Decisions[keep0].AssignmentId")==protectedRow.Id.ToString() && Field(staleHtml,"Decisions[keep0].Action")=="RETIRE", "HTTP rejection preserves reviewed decisions for the next review");
        handler.CookieContainer.Add(client.BaseAddress, new Cookie("incidence-role", "worker"));
        Check((await client.GetAsync("/supervision/incidencias")).StatusCode == HttpStatusCode.Forbidden, "worker cannot browse supervision");
        handler.CookieContainer.Add(client.BaseAddress, new Cookie("incidence-role", "supervisor"));
        html = await client.GetStringAsync("/supervision/incidencias");
        Check(html.Contains("Solo Dirección") && !html.Contains("Registrar ausencia"), "supervisor gets scoped read-only page");
        Check((await client.GetAsync($"/supervision/incidencias?Day=2026-11-06&receipt={privateReceipt.Id}")).StatusCode==HttpStatusCode.Forbidden,"supervisor cannot read receipt outside scope by guessing its id");
        form["__RequestVerificationToken"] = Field(html,"__RequestVerificationToken");
        Check((await client.PostAsync("/supervision/incidencias?handler=Apply",new FormUrlEncodedContent(form))).StatusCode==HttpStatusCode.Forbidden,"supervisor crafted confirmation rejected by server");
        var output = Path.GetFullPath(".local/incidence-ui"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "fixture.json"), JsonSerializer.Serialize(new { origin = "https://localhost:7297", person = fixture.Person, day = "2026-11-04", conflictPerson=conflictFixture.Person, conflictRecipient=conflictFixture.OtherPerson, ineligibleRecipient=supervisorPerson }));
        Console.WriteLine("INCIDENCE_UI_READY " + output);
        await app.WaitForShutdownAsync();
    }
}
