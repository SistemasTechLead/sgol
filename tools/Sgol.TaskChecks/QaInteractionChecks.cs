using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Evidence.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using System.Security.Claims;
using System.Net;
using System.Text.RegularExpressions;

using Sgol.Organization.Contracts;

internal static class QaInteractionChecks
{
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, CheckClock clock, Guid director, Guid directorPerson)
    {
        var points = new[] { new ChecklistPoint(Guid.NewGuid(), "Revisar caja"), new ChecklistPoint(Guid.NewGuid(), "Revisar inventario") };
        var definition = JsonSerializer.Serialize(points);
        await tasks.SaveAsync(director, new OperationalTask { Revision=0, Name="QA HTTP", Requirement="CHECKLIST", ChecklistJson=definition, OperationalCriticality="FLEXIBLE", LoadMinutes=5 }, default);
        var source = await db.OperationalTasks.SingleAsync(t => t.Name == "QA HTTP");
        var day = WeekContract.LocalToday(clock.UtcNow);
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, director, clock.UtcNow, "QA HTTP", day, day);
        db.Add(plan);
        var assignment = new OperationalAssignment { Id=Guid.CreateVersion7(), PlanId=plan.Id, TaskId=source.Id, TaskCode=source.TaskCode, Name="QA dos pestañas", PersonId=directorPerson, PersonName="DIR", AssignedRole=CanonicalRole.Direction, Requirement="CHECKLIST", ChecklistJson=definition, LoadMinutes=5, StartsAt=clock.UtcNow.AddMinutes(-10), DueAt=clock.UtcNow.AddHours(1) };
        db.Add(assignment); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await RunHttpAsync(db, clock, director, directorPerson, assignment, points, definition);
    }

    private static async Task RunHttpAsync(SgolDbContext db, CheckClock clock, Guid director, Guid directorPerson, OperationalAssignment assignment, ChecklistPoint[] points, string definition)
    {
        // Real Razor forms, antiforgery and persistence; authentication is explicitly TEST_ONLY.
        var webRoot=Path.GetFullPath("src/Sgol.Web/wwwroot");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7279");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7279";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>();builder.Services.AddScoped<OperationalSupervisionService>();builder.Services.AddScoped<OperationalCapacityService>();builder.Services.AddScoped<OperationalCarryoverService>();builder.Services.AddScoped<OperationalCarryoverReader>();builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddSingleton<IFileTechnicalValidator,FileTechnicalValidator>();builder.Services.AddSingleton<IFileMalwareScanner,CheckScanner>();builder.Services.AddSingleton<IRazorSessionState>(new CheckSession(director,directorPerson));
        await using var app=builder.Build();
        var dropNextDraft=false;
        app.Use(async(ctx,next)=>{
            if(dropNextDraft && ctx.Request.Query["handler"]=="ChecklistDraft") {
                dropNextDraft=false;var originalBody=ctx.Response.Body;await using var discardedResponse=new MemoryStream();ctx.Response.Body=discardedResponse;
                try {await next();} finally {ctx.Response.Body=originalBody;}
                ctx.Abort();return;
            }
            await next();
        });
        app.Use(async(ctx,next)=>{ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,director.ToString())],"TEST_ONLY"));await next();});
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7279")};

        var failures = new List<string>();
        void Check(bool ok, string label) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")} QA: {label}"); if (!ok) failures.Add(label); }
        foreach (var date in new[] { "2026-10-30", "9999-12-31", "0001-01-01" }) {
            var response = await client.GetAsync("/validaciones?PISO_VENTAS.range=CUSTOM&PISO_VENTAS.to=" + date);
            Check(response.IsSuccessStatusCode, "date " + date + " returns 200");
        }
        var page = await client.GetStringAsync($"/mi-trabajo?assignedTask={assignment.Id}");
        var csrf = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var basis = WebUtility.HtmlDecode(Regex.Match(page, "data-draft-base=\"([^\"]+)\"").Groups[1].Value);
        async Task<HttpResponseMessage> Save(string payload, string draftBase, Guid? saveId = null) => await client.PostAsync("/mi-trabajo?handler=ChecklistDraft", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"]=assignment.Id.ToString(), ["answer"]=payload, ["draftBase"]=draftBase, ["saveId"]=(saveId ?? Guid.NewGuid()).ToString(), ["__RequestVerificationToken"]=csrf }));
        string Answer(params Guid[] ids) => JsonSerializer.Serialize(new ChecklistAnswer(OperationalChecklist.Fingerprint(definition), ids));
        await using var staleDb=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
        _=await staleDb.OperationalAssignments.SingleAsync(a=>a.Id==assignment.Id);
        var staleDirection=new EfDirectionPreparedPlanService(staleDb,new AuditTransaction(staleDb),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(staleDb));
        var staleService=new OperationalTaskService(staleDb,staleDirection,clock,new FileTechnicalValidator(),new CheckScanner());
        var saveId=Guid.NewGuid();
        var first = await Save(Answer(points[0].Id), basis,saveId);
        Check(first.IsSuccessStatusCode, "first tab saves");
        var trackedConflict=false;
        try {await staleService.SaveChecklistDraftAsync(director,assignment.Id,Answer(points[1].Id),default,basis,Guid.NewGuid());}
        catch(DirectionPlanException e) when(e.Code=="TASK_CHECKLIST_DRAFT_STALE") {trackedConflict=true;}
        catch(Exception e) {Console.WriteLine("Tracked context failure type: "+e.GetType().Name);}
        Check(trackedConflict,"service rejects a stale previously tracked entity with a recoverable conflict");
        var second = await Save(Answer(points[1].Id), basis);
        Check(second.StatusCode == HttpStatusCode.Conflict, "stale tab conflicts");
        db.ChangeTracker.Clear();
        var saved = await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == assignment.Id);
        Check(OperationalChecklist.Checked(saved.ChecklistDraftJson).SequenceEqual([points[0].Id]), "stale save preserves first tab");
        var confirmation=JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("draftBase").GetString()!;
        var retry=await Save(Answer(points[0].Id),basis,saveId);
        Check(retry.IsSuccessStatusCode,"lost response retry acknowledges exact save");
        db.ChangeTracker.Clear();
        var replay=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignment.Id);
        Check(replay.Revision==saved.Revision && replay.ChecklistDraftJson==saved.ChecklistDraftJson,"retry preserves revision and evidence");
        var unmark=await Save(Answer(),confirmation);
        Check(unmark.IsSuccessStatusCode,"intentional unmark saves");
        db.ChangeTracker.Clear();
        var empty=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignment.Id);
        Check(OperationalChecklist.Checked(empty.ChecklistDraftJson).Length==0,"unmark persisted");
        var lateRetry=await Save(Answer(points[0].Id),basis,saveId);
        Check(lateRetry.StatusCode==HttpStatusCode.Conflict,"old retry after newer edit cannot resurrect marks");
        var emptyBase=JsonDocument.Parse(await unmark.Content.ReadAsStringAsync()).RootElement.GetProperty("draftBase").GetString()!;
        var race=await Task.WhenAll(Save(Answer(points[0].Id),emptyBase),Save(Answer(points[1].Id),emptyBase));
        Check(race.Count(r=>r.IsSuccessStatusCode)==1 && race.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"simultaneous writers have one winner");
        var noCsrf=await client.PostAsync("/mi-trabajo?handler=ChecklistDraft",new FormUrlEncodedContent(new Dictionary<string,string>{{"id",assignment.Id.ToString()},{"answer",Answer(points[0].Id)},{"draftBase",emptyBase},{"saveId",Guid.NewGuid().ToString()}}));
        Check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"draft requires antiforgery");
        var stalePage=await client.GetStringAsync($"/mi-trabajo?assignedTask={assignment.Id}");
        var lastBase=WebUtility.HtmlDecode(Regex.Match(stalePage,"data-draft-base=\"([^\"]+)\"").Groups[1].Value);
        var changed=await db.OperationalAssignments.SingleAsync(x=>x.Id==assignment.Id);
        changed.ChecklistJson=JsonSerializer.Serialize(points.Select(p=>p with {Text=p.Text+" actualizado"}));
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var beforeConflict=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id));
        var audits=await db.AuditEvents.CountAsync();
        var definitionConflict=await Save(Answer(points[0].Id),lastBase);
        Check(definitionConflict.StatusCode==HttpStatusCode.Conflict && (await definitionConflict.Content.ReadAsStringAsync()).Contains("TASK_CHECKLIST_CHANGED"),"definition edit invalidates draft causally");
        db.ChangeTracker.Clear();
        Check(beforeConflict==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id)) && audits==await db.AuditEvents.CountAsync(),"conflict leaves data and audit unchanged");
        // Restore only this owned fixture for browser inspection.
        changed=await db.OperationalAssignments.SingleAsync(x=>x.Id==assignment.Id);changed.ChecklistJson=definition;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        foreach(var query in new[]{"from=2026-11-01&PISO_VENTAS.to=2026-10-01","from=no-date","from=2026-02-30","from=&PISO_VENTAS.to="}) {
            var response=await client.GetAsync("/validaciones?PISO_VENTAS.range=CUSTOM&PISO_VENTAS.search=conservar&PISO_VENTAS."+query);
            var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Check(response.IsSuccessStatusCode && html.Contains("conservar"),"invalid/empty dates preserve form and filters: "+query);
            if(!query.StartsWith("from=&"))Check(html.Contains("tus filtros se conservaron",StringComparison.OrdinalIgnoreCase),"date error explains recovery");
        }
        if (failures.Count > 0) throw new Exception(string.Join("; ", failures));
        if(Environment.GetEnvironmentVariable("SGOL_QA_UI_PREVIEW")=="1") {dropNextDraft=Environment.GetEnvironmentVariable("SGOL_QA_DROP_DRAFT_RESPONSE")=="1";Console.WriteLine($"QA_UI_READY https://localhost:7279/mi-trabajo?assignedTask={assignment.Id}");await app.WaitForShutdownAsync();}
        await app.StopAsync();
    }
}
