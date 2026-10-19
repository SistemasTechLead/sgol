using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

public static class ProcessPlanDeletionHttpChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid planId, bool skipDownload = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found.");
        var webRoot = Path.Combine(root.FullName, "src", "Sgol.Web", "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName,
            WebRootPath = webRoot, EnvironmentName = "Development" });
        builder.WebHost.UseUrls("https://localhost:7259", "http://127.0.0.1:7258");
        builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7259";
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages(o => o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter))))
            .AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>();
        builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
        builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddSingleton<IClock>(new Clock()); builder.Services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>();
        builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>(); builder.Services.AddScoped<OperationalProcessCatalogService>();
        builder.Services.AddScoped<ProcessPlanningService>();
        builder.Services.AddSingleton<IFileTechnicalValidator, FileTechnicalValidator>(); builder.Services.AddSingleton<IFileMalwareScanner, Scanner>();
        builder.Services.AddSingleton<IRazorSessionState>(new Session(actor, await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync()));
        await using var app = builder.Build();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "TEST_ONLY")); await next(); });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages();
        await app.StartAsync();
        var path = $"/planificacion/procesos?planId={planId:D}";
        if (Environment.GetEnvironmentVariable("SGOL_PROCESS_DELETION_PREVIEW") == "1")
        {
            Console.WriteLine($"Isolated visual preview: http://127.0.0.1:7258{path}");
            await app.WaitForShutdownAsync(); return;
        }
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7259") };
        var body = await client.GetStringAsync(path);
        Check(body.Contains("Eliminar plan de proceso"), "Process consultation exposes deletion");
        var csrf = Hidden(body, "__RequestVerificationToken");
        var invalid = await client.PostAsync("/planificacion/procesos?handler=PrepareDeletion", new FormUrlEncodedContent(new Dictionary<string,string> { ["planId"] = planId.ToString() }));
        Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Deletion preview rejects missing CSRF token");
        var preview = await client.PostAsync("/planificacion/procesos?handler=PrepareDeletion", Form(csrf, ("planId", planId.ToString())));
        body = await preview.Content.ReadAsStringAsync(); Check(preview.IsSuccessStatusCode && body.Contains("No, eliminar sin descargar archivos"), "Preview offers explicit download and skip choices");
        var intent = Hidden(body, "deleteIntent"); csrf = Hidden(body, "__RequestVerificationToken");
        async Task<HttpResponseMessage> Delete(string choice, string confirmation = "ELIMINAR") => await client.PostAsync("/planificacion/procesos?handler=Delete",
            Form(csrf, ("deleteIntent", intent), ("confirmation", confirmation), ("archiveChoice", choice)));
        Check((await Delete("download", "incorrecta")).StatusCode == HttpStatusCode.BadRequest, "Deletion requires exact confirmation text");
        Check((await Delete("missing")).StatusCode == HttpStatusCode.BadRequest, "Deletion requires an explicit valid file choice");
        var blocked = await Delete("download");
        Check(blocked.StatusCode == HttpStatusCode.Conflict && (await blocked.Content.ReadAsStringAsync()).Contains("Primero pulsa Descargar"), "Choosing download cannot delete before an archive is generated");
        if (!skipDownload)
        {
            var download = await client.PostAsync("/planificacion/procesos?handler=DownloadArchive", Form(csrf, ("deleteIntent", intent)));
            Check(download.IsSuccessStatusCode && download.Content.Headers.ContentType?.MediaType == "application/zip" && (await download.Content.ReadAsByteArrayAsync()).Length > 0,
                "Archive POST downloads a ZIP and issues a signed receipt");
            Check(await db.DirectionPreparedPlans.AnyAsync(p => p.Id == planId), "Downloading preserves the plan");
        }
        var deleted = await Delete(skipDownload ? "skip" : "download");
        Check(deleted.StatusCode == HttpStatusCode.Redirect && deleted.Headers.Location?.OriginalString == "/planificacion", skipDownload ? "Explicit skip choice deletes without requiring download" : "Downloaded plan deletes and returns to planning");
        Check(!await db.DirectionPreparedPlans.AnyAsync(p => p.Id == planId), "HTTP deletion removes the reviewed plan");
        Check((await Delete(skipDownload ? "skip" : "download")).StatusCode == HttpStatusCode.Redirect, "HTTP deletion retry recovers the original result");
        await app.StopAsync();
    }
    private static FormUrlEncodedContent Form(string csrf, params (string Key, string Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => f.Value).Append(new("__RequestVerificationToken", csrf)));
    private static string Hidden(string body, string name) => WebUtility.HtmlDecode(Regex.Match(body, $"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static void Check(bool value, string text) { if (!value) throw new Exception("FAILED: " + text); Console.WriteLine("PASS: " + text); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026,10,13,18,0,0,TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner { public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask; public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio)); }
    private sealed class Session(Guid user, Guid person) : IRazorSessionState
    {
        public bool IsInvalid => false; public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken token = default) => Task.FromResult<SessionSnapshot?>(new(user,person,"test","Dirección · Prueba aislada","LOR-001",CanonicalRole.Direction,
            [WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}
