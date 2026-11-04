using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

internal static class RefreshSearchReplayChecks
{
    sealed class Clock : IClock { public DateTimeOffset UtcNow { get; } = DateTimeOffset.Parse(Environment.GetEnvironmentVariable("SGOL_REFRESH_TIME") ?? DateTimeOffset.UtcNow.ToString("O")); }
    sealed class Session(Guid actor, Guid person, IClock clock, string role) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(actor, person, "refresh-check", "Prueba", "LOR-001", role, [WeekAuthorization.View], clock.UtcNow, clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2)));
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS refresh replay: " + message); }
    public static async Task Run(SgolDbContext db)
    {
        var clock = new Clock();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == "DIRECCION" && r.ValidTo == null).Select(r => r.UserId).FirstAsync();
        var service = new AssignmentRefreshService(db, direction, clock);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
        Console.WriteLine("CAPACITY " + JsonSerializer.Serialize(ledger.Workers.Select(w => new { w.Name, Load = ledger.DayLoad(w.Id, ledger.Today), Capacity = ledger.DayCapacity(w.Id, ledger.Today), Overdue = ledger.Tasks.Where(t => t.PersonId == w.Id && t.CompletedAt == null && t.DueAt <= clock.UtcNow).Sum(t => t.RemainingMinutes) })));
        var request = new AssignmentRefreshRequest(Guid.Empty, Guid.NewGuid(), clock.UtcNow.AddMinutes(3), ReplaceRemainingEstimate: true, ChangeAuthorizedDeadline: true, AdoptLegacyCalendar: true, RetireInactiveAssignments: true, RetireExcludedAssignments: true);
        var originalRows = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var before = JsonSerializer.Serialize(originalRows);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var preview = await service.PreviewAsync(actor, request, default);
        if (Environment.GetEnvironmentVariable("SGOL_REFRESH_APPLY") == "1")
        {
            request = request with { PreserveCalendarAssignmentIds = preview.Decisions.Where(d => d.Code.StartsWith("calendar-preserve:")).Select(d => d.AssignmentId!.Value).ToArray(),
                PreserveProcessAssignmentIds = preview.Decisions.Where(d => d.Code.StartsWith("process:")).Select(d => d.AssignmentId!.Value).ToArray() };
            preview = await service.PreviewAsync(actor, request, default);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { clock.UtcNow, timer.Elapsed, preview.CanApply, preview.Conflicts, preview.Decisions, Issues = preview.Issues.Select(i => new { i.Code, i.TaskCode, i.Context, i.Detail }), Changes = preview.Differences.Select(d => new { Code = d.After?.TaskCode ?? d.Before?.TaskCode, d.Fields, d.Retire, Before = d.Before?.StartsAt, After = d.After?.StartsAt }) }));
        if (before != JsonSerializer.Serialize(await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().OrderBy(a => a.Id).ToArrayAsync())) throw new Exception("Preview wrote assignments");
        if (Environment.GetEnvironmentVariable("SGOL_REFRESH_APPLY") != "1") return;
        Check(preview.CanApply, "current snapshot produces an applicable proposal");
        Check(preview.Notices.Any(n => n.Contains("no se reprograman")), "preview explains excluded overdue demand");
        var again = await service.PreviewAsync(actor, request, default);
        Check(again.Fingerprint == preview.Fingerprint, "repeat preview is deterministic");
        await Http(db, actor, clock);
        db.ChangeTracker.Clear();
        var after = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var changedIds = preview.Differences.Select(d => (d.Before ?? d.After)!.Id).ToHashSet();
        Check(JsonSerializer.Serialize(originalRows.Where(a => !changedIds.Contains(a.Id))) == JsonSerializer.Serialize(after.Where(a => !changedIds.Contains(a.Id))), "all untouched rows, deliveries and historical overdue work remain identical");
        foreach (var diff in preview.Differences.Where(d => d.Before != null && !d.Retire))
        {
            var row = after.Single(a => a.Id == diff.Before!.Id);
            Check(row.ProgressUnits == diff.Before!.ProgressUnits && row.CompletedAt == diff.Before.CompletedAt, "progress and completion preserved: " + row.TaskCode);
        }
    }

    static async Task Http(SgolDbContext db, Guid actor, IClock clock)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found");
        var webRoot = Path.Combine(root.FullName, "src", "Sgol.Web", "wwwroot");
        var person = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName, WebRootPath = webRoot, EnvironmentName = "Development" });
        builder.WebHost.UseUrls("https://localhost:7298"); builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7298"; builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly); builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment); builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>(); builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>(); builder.Services.AddScoped<AssignmentRefreshService>();
        builder.Services.AddScoped<OperationalTaskService>(); builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>(); builder.Services.AddScoped<OperationalProcessCatalogService>(); builder.Services.AddScoped<OperationalSchedulingPolicyService>();
        builder.Services.AddSingleton<Sgol.Evidence.Contracts.IFileTechnicalValidator,Sgol.Evidence.Technical.FileTechnicalValidator>(); builder.Services.AddSingleton<Sgol.Evidence.Contracts.IFileMalwareScanner,CheckScanner>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<IRazorSessionState>(sp => new Session(actor, person, clock, sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Cookies["test-role"] == "worker" ? CanonicalRole.SalesFloor : CanonicalRole.Direction));
        await using var app = builder.Build();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "TEST_ONLY")); await next(); });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages(); await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() };
        using var client = new HttpClient(handler) { BaseAddress = new("https://localhost:7298") };
        string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var html = await client.GetStringAsync("/tareas/actualizar");
        Check((await client.PostAsync("/tareas/actualizar?handler=Preview", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.BadRequest, "CSRF required");
        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", Field(html, "__RequestVerificationToken")) };
        foreach (Match checkbox in Regex.Matches(html, "<input[^>]*type=\"checkbox\"[^>]*>"))
            form.Add(new(Regex.Match(checkbox.Value, "name=\"([^\"]*)\"").Groups[1].Value, WebUtility.HtmlDecode(Regex.Match(checkbox.Value, "value=\"([^\"]*)\"").Groups[1].Value)));
        var response = await client.PostAsync("/tareas/actualizar?handler=Preview", new FormUrlEncodedContent(form)); html = await response.Content.ReadAsStringAsync();
        Check(response.IsSuccessStatusCode && html.Contains("data-refresh-apply"), "real review form enables confirmation with explicit decisions");
        if (Environment.GetEnvironmentVariable("SGOL_REFRESH_UI") == "1")
        {
            var done = Path.Combine(root.FullName, ".local/refresh-search-20261104/ui-done"); File.Delete(done);
            Console.WriteLine("REFRESH_UI_READY https://localhost:7298/tareas/actualizar");
            while (!File.Exists(done)) await Task.Delay(500);
        }
        form.RemoveAll(p => p.Key == "__RequestVerificationToken");
        foreach (var name in new[] { "__RequestVerificationToken", "Key", "NotBefore", "Fingerprint" }) form.Add(new(name, Field(html, name)));
        form.Add(new("Reason", "Regresión aislada de búsqueda"));
        response = await client.PostAsync("/tareas/actualizar?handler=Apply", new FormUrlEncodedContent(form));
        Check(response.StatusCode == HttpStatusCode.Redirect, "real confirmation applies atomically");
        response = await client.PostAsync("/tareas/actualizar?handler=Apply", new FormUrlEncodedContent(form));
        Check(response.StatusCode == HttpStatusCode.Redirect, "HTTP confirmation retry is idempotent");
        html = await client.GetStringAsync("/tareas");
        Check(Regex.IsMatch(html, "<button[^>]*disabled[^>]*>Actualizar Asignaciones</button>"), "catalog disables update after all changes and exceptions are confirmed");
        handler.CookieContainer.Add(client.BaseAddress, new Cookie("test-role", "worker"));
        Check((await client.GetAsync("/tareas/actualizar")).StatusCode == HttpStatusCode.Forbidden, "worker denied");
    }
}
