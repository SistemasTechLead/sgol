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

public static class ProcessMyWorkHttpChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid runId, IFormFile pdf)
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
        builder.Services.AddScoped<OperationalCarryoverReader>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<ProcessPlanningService>(); builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddSingleton<IFileTechnicalValidator, FileTechnicalValidator>(); builder.Services.AddSingleton<IFileMalwareScanner, Scanner>();
        var testSession = new Session(actor, await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync());
        builder.Services.AddSingleton<IRazorSessionState>(testSession);
        await using var app = builder.Build();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "TEST_ONLY")); await next(); });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages();
        await app.StartAsync();
        var attempt = await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleAsync(a => a.RunId == runId && a.Status == "ACTIVO");
        var id = attempt.AssignmentId!.Value;
        var path = $"/mi-trabajo?assignedTask={id:D}";
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7259") };
        var waitingStage = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.RunId == runId && s.Position == 2);
        var waitingActor = await db.AppUsers.Where(u => u.PersonId == waitingStage.ReservedPersonId).Select(u => u.Id).SingleAsync();
        testSession.User = waitingActor; testSession.Person = waitingStage.ReservedPersonId!.Value;
        var historicalPlanId = await db.Set<OperationalSequenceRun>().Where(r => r.Id == runId).Select(r => r.PlanId).SingleAsync();
        var historical = new[] { "ENTREGA", "SUPERVISION" }.Select(kind => new OperationalAssignment {
            Id = Guid.NewGuid(), PlanId = historicalPlanId,
            TaskId = waitingStage.TaskId, TaskCode = waitingStage.TaskCode, Name = "Histórica " + kind,
            PersonId = testSession.Person, PersonName = "Fixture", Requirement = "COMENTARIO", LoadMinutes = 5,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-3), DueAt = DateTimeOffset.UtcNow.AddDays(-3).AddMinutes(10),
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-2), CompletedBy = waitingActor, CompletionKind = kind,
            ReviewStatus = "REASIGNADA", ReviewedAt = DateTimeOffset.UtcNow.AddDays(-1), ReviewedBy = waitingActor
        }).ToArray();
        db.AddRange(historical); await db.SaveChangesAsync();
        var pendingBody = await client.GetStringAsync("/mi-trabajo");
        foreach (var original in historical) {
            var row = Regex.Match(pendingBody, $"<tr[^>]*data-work-task=\"{original.Id}\"[\\s\\S]*?</tr>").Value;
            var expected = original.CompletionKind == "ENTREGA" ? "Entregada" : "Reasignada";
            Check(row.Contains($"data-work-timing>{expected}</small>") && row.Contains("Reasignada</span>") && !row.Contains("Por activar"),
                "Historical reassignment preserves delivery or supervision closure in the real My Work table");
            var color = original.CompletionKind == "ENTREGA" ? "delivered" : "scheduled";
            Check(row.Contains("my-work-color--" + color), "Reassigned delivery uses blue; supervision closure keeps its own color");
            if (original.CompletionKind == "ENTREGA")
                Check(pendingBody.IndexOf($"data-work-task=\"{waitingStage.Id}\"") < pendingBody.IndexOf($"data-work-task=\"{original.Id}\""),
                    "Delivered reassignment sorts after gray reservations in the real table");
        }
        using (var scope = app.Services.CreateScope()) {
            var page = await scope.ServiceProvider.GetRequiredService<OperationalTaskService>().MinePageAsync(waitingActor, null, 1, DateTimeOffset.UtcNow, default);
            var deliveredIndex = Array.FindIndex(page.Items, a => a.Id == historical[0].Id);
            Check(deliveredIndex > Array.FindIndex(page.Items, a => a.Id == waitingStage.Id), "SQL pagination puts reserved work ahead of reassigned deliveries");
            Check(OperationalWorkPlan.Priority(page.Items[deliveredIndex], DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow) == 4,
                "Presentation priority groups reassigned deliveries with blue work");
        }
        db.RemoveRange(historical); await db.SaveChangesAsync();
        Check(pendingBody.Contains($"data-work-task=\"{waitingStage.Id}\"") && pendingBody.Contains("En espera de la etapa anterior") &&
            pendingBody.Contains($"/mi-trabajo/procesos?id={runId}") && !pendingBody.Contains($"data-assigned-task=\"{waitingStage.Id}\""),
            "Real My work page renders the waiting task in the shared table with process navigation and no delivery dialog");
        if (Environment.GetEnvironmentVariable("SGOL_PROCESS_MYWORK_PREVIEW") == "1") {
            Console.WriteLine("MYWORK_PREVIEW_READY http://127.0.0.1:7258/mi-trabajo");
            await app.WaitForShutdownAsync(); return;
        }
        testSession.User = actor; testSession.Person = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync();
        var body = await client.GetStringAsync(path);
        Check(body.Contains("handler=CompleteProcess") && body.Contains("Entregar PDF") && !body.Contains("handler=CompleteOperational"), "Mi trabajo shows the process delivery form instead of the generic completion form");
        Check((await client.GetStringAsync($"/mi-trabajo/procesos?id={runId:D}")).Contains("handler=Decide"), "Process page keeps its delivery form");
        var csrf = Hidden(body, "__RequestVerificationToken");
        var key = Hidden(body, "Key");
        var revision = Hidden(body, "Revision");
        async Task<HttpResponseMessage> Submit(bool includePdf = true, bool token = true, bool declared = true, Guid? assignment = null, string? postedRevision = null)
        {
            using var form = new MultipartFormDataContent();
            var fields = new Dictionary<string, string> { ["id"] = (assignment ?? id).ToString(), ["AttemptId"] = attempt.Id.ToString(),
                ["Revision"] = postedRevision ?? revision, ["Key"] = key, ["Decision"] = "ENTREGA", ["Conclusion"] = "Ejemplo: registro completo declarado", ["Declared"] = declared.ToString() };
            if (token) fields["__RequestVerificationToken"] = csrf;
            foreach (var field in fields) form.Add(new StringContent(field.Value), field.Key);
            if (includePdf) { using var bytes = new MemoryStream(); await pdf.CopyToAsync(bytes); var content = new ByteArrayContent(bytes.ToArray());
                content.Headers.ContentType = new("application/pdf"); form.Add(content, "Document", pdf.FileName); }
            return await client.PostAsync("/mi-trabajo?handler=CompleteProcess", form);
        }
        Check((await Submit(token: false)).StatusCode == HttpStatusCode.BadRequest, "Mi trabajo rejects missing CSRF token");
        var missing = await Submit(includePdf: false);
        var errorBody = await missing.Content.ReadAsStringAsync();
        Check(missing.StatusCode == HttpStatusCode.UnprocessableEntity && errorBody.Contains("Ejemplo: registro completo declarado") && Hidden(errorBody, "Key") == key,
            "Missing PDF preserves conclusion and retry key in the task dialog");
        Check((await Submit(declared: false)).StatusCode == HttpStatusCode.UnprocessableEntity, "Process declaration remains mandatory");
        Check((await Submit(assignment: Guid.CreateVersion7())).StatusCode == HttpStatusCode.NotFound, "Delivery rejects an assignment outside the worker's tasks");
        Check((await Submit(postedRevision: "0")).StatusCode == HttpStatusCode.Conflict, "Delivery rejects stale process revisions");
        Check(!await db.Set<OperationalSequenceDocument>().AnyAsync(d => d.RunId == runId), "Rejected deliveries create no PDF versions");
        var delivered = await Submit();
        Check(delivered.StatusCode == HttpStatusCode.Redirect && delivered.Headers.Location?.OriginalString == $"/mi-trabajo?assignedTask={id:D}#plan-tasks",
            "Successful PDF delivery returns to the task in Mi trabajo");
        Check((await db.Set<OperationalSequenceRun>().AsNoTracking().SingleAsync(r => r.Id == runId)).ActivePosition == 2
            && await db.Set<OperationalSequenceDocument>().CountAsync(d => d.RunId == runId) == 1, "Mi trabajo saves V1 and advances the process to its second stage");
        Check((await Submit()).StatusCode == HttpStatusCode.Redirect && await db.Set<OperationalSequenceDocument>().CountAsync(d => d.RunId == runId) == 1,
            "Retrying the Mi trabajo submission does not duplicate the document");
        body = await client.GetStringAsync(path);
        Check(body.Contains("Evidencia enviada") && !body.Contains("handler=CompleteProcess"), "Completed task shows its submitted evidence");
        await app.StopAsync();
    }
    private static FormUrlEncodedContent Form(string csrf, params (string Key, string Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => f.Value).Append(new("__RequestVerificationToken", csrf)));
    private static string Hidden(string body, string name) => WebUtility.HtmlDecode(Regex.Match(body, $"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static void Check(bool value, string text) { if (!value) throw new Exception("FAILED: " + text); Console.WriteLine("PASS: " + text); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026,10,13,18,0,0,TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner { public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask; public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio)); }
    private sealed class Session(Guid user, Guid person) : IRazorSessionState
    {
        public Guid User { get; set; } = user;
        public Guid Person { get; set; } = person;
        public bool IsInvalid => false; public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken token = default) => Task.FromResult<SessionSnapshot?>(new(User,Person,"test","Dirección · Prueba aislada","LOR-001",CanonicalRole.Direction,
            [WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}

