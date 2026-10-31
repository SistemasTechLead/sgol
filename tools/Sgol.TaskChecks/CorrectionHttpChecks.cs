using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
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

internal static class CorrectionHttpChecks
{
    public static async Task Run(SgolDbContext db, QaFixtures.Actors fixture, CorrectionSchedulingChecks.Clock clock, OperationalAssignment source, bool ui)
    {
        var webRoot=Path.GetFullPath("src/Sgol.Web/wwwroot");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7289");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7289";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>();builder.Services.AddScoped<OperationalSupervisionService>();builder.Services.AddScoped<OperationalCapacityService>();builder.Services.AddScoped<OperationalCarryoverService>();builder.Services.AddScoped<OperationalCarryoverReader>();builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddSingleton<IFileTechnicalValidator,FileTechnicalValidator>();builder.Services.AddSingleton<IFileMalwareScanner,CheckScanner>();
        var directorPerson=await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        // This isolated test host explicitly substitutes authentication; product forms, CSRF,
        // authorization services, transaction boundaries and SQL persistence remain real.
        builder.Services.AddScoped<IRazorSessionState>(sp=>sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Path.StartsWithSegments("/mi-trabajo")
            ? new CheckSession(fixture.Worker,fixture.Person,CanonicalRole.SalesFloor) : new CheckSession(fixture.Director,directorPerson));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{var actor=ctx.Request.Path.StartsWithSegments("/mi-trabajo")?fixture.Worker:fixture.Director;ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"TEST_ONLY"));await next();});
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7289")};
        string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var path=$"/validaciones?task={source.Id:D}";
        var html=await client.GetStringAsync(path);var csrf=Field(html,"__RequestVerificationToken");
        var form=new Dictionary<string,string>{["id"]=source.Id.ToString(),["revision"]="1",["decision"]="MISMA_PERSONA",["result"]="no",["comment"]="Comentario HTTP conservado",["Schedule.Mode"]="CUSTOM",["Schedule.From"]="2026-11-01",["stage"]="preview",["__RequestVerificationToken"]=csrf};
        var response=await client.PostAsync("/validaciones?handler=Review",new FormUrlEncodedContent(form));html=await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) { Console.WriteLine("HTTP preview status " + response.StatusCode); await File.WriteAllTextAsync(".local/correction-20261031/http-failure.html", html); }
        CorrectionSchedulingChecks.Check(response.IsSuccessStatusCode && html.Contains("data-correction-preview") && html.Contains("Comentario HTTP conservado"),"real individual form renders preview and retains comment");
        CorrectionSchedulingChecks.Check(!await db.OperationalAssignments.AnyAsync(a=>a.ReassignedFromId==source.Id),"HTTP preview has no persistence");
        form["PreviewAt"]=Field(html,"PreviewAt");form["ReviewFingerprint"]=Field(html,"ReviewFingerprint");form["stage"]="apply";
        response=await client.PostAsync("/validaciones?handler=Review",new FormUrlEncodedContent(form));
        CorrectionSchedulingChecks.Check(response.StatusCode==HttpStatusCode.Redirect,"real individual confirmation persists");
        var correction=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.ReassignedFromId==source.Id);
        html=WebUtility.HtmlDecode(await client.GetStringAsync($"/mi-trabajo?assignedTask={correction.Id}"));
        CorrectionSchedulingChecks.Check(html.Contains("Comentario HTTP conservado") && html.Contains("Corrección de la entrega"),"real My Work dialog exposes origin and correction comment");
        response=await client.PostAsync("/validaciones?handler=Review",new FormUrlEncodedContent(form));
        CorrectionSchedulingChecks.Check(response.StatusCode==HttpStatusCode.Conflict,"HTTP repeated confirmation conflicts without duplicate");
        var batchSource=await CorrectionSchedulingChecks.Source(db,fixture,clock);
        html=await client.GetStringAsync("/validaciones?Validation=true");csrf=Field(html,"__RequestVerificationToken");
        var prefix=$"Reviews[{batchSource.Id:N}]";
        var batch=new Dictionary<string,string>{["Validation"]="true",["Section"]="pending",["Reviews.Index"]=batchSource.Id.ToString("N"),[prefix+".Id"]=batchSource.Id.ToString(),[prefix+".Revision"]="1",[prefix+".Result"]="no",[prefix+".Action"]="MISMA_PERSONA",[prefix+".Comment"]="Grupo HTTP",[prefix+".Schedule.Mode"]="FREE",["stage"]="preview",["__RequestVerificationToken"]=csrf};
        response=await client.PostAsync("/validaciones?handler=BatchReview",new FormUrlEncodedContent(batch));html=await response.Content.ReadAsStringAsync();
        CorrectionSchedulingChecks.Check(response.IsSuccessStatusCode && html.Contains("data-correction-preview"),"real indexed group form binds scheduling fields");
        batch["PreviewAt"]=Field(html,"PreviewAt");batch["ReviewFingerprint"]=Field(html,"ReviewFingerprint");batch["stage"]="apply";
        response=await client.PostAsync("/validaciones?handler=BatchReview",new FormUrlEncodedContent(batch));
        CorrectionSchedulingChecks.Check(response.StatusCode==HttpStatusCode.Redirect && await db.OperationalAssignments.AnyAsync(a=>a.ReassignedFromId==batchSource.Id),"real group confirmation persists");
        var noCsrf=await client.PostAsync("/validaciones?handler=Review",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=source.Id.ToString()}));
        CorrectionSchedulingChecks.Check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"review requires antiforgery");
        var correctionDay=WeekContract.LocalToday(correction.StartsAt);
        db.AvailabilityDayVersions.Add(new(Guid.CreateVersion7(),fixture.Person,Sgol.Organization.Contracts.BranchScope.LorettaId,correctionDay,false,fixture.Director));
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        html=WebUtility.HtmlDecode(await client.GetStringAsync($"/validaciones?task={correction.Id}"));
        CorrectionSchedulingChecks.Check(html.Contains("requieren reprogramación") && html.Contains("Reprogramar corrección"),"registered absence is visible in real supervision with explicit action");
        var reschedule=new Dictionary<string,string>{["id"]=correction.Id.ToString(),["revision"]=correction.Revision.ToString(),["comment"]="Reprogramación HTTP por ausencia",["Schedule.Mode"]="CUSTOM",["Schedule.From"]="2026-11-03",["stage"]="preview",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")};
        response=await client.PostAsync("/validaciones?handler=Reschedule",new FormUrlEncodedContent(reschedule));html=await response.Content.ReadAsStringAsync();
        CorrectionSchedulingChecks.Check(response.IsSuccessStatusCode && html.Contains("data-correction-preview"),"real rescheduling form previews existing correction");
        reschedule["PreviewAt"]=Field(html,"PreviewAt");reschedule["ReviewFingerprint"]=Field(html,"ReviewFingerprint");reschedule["stage"]="apply";
        response=await client.PostAsync("/validaciones?handler=Reschedule",new FormUrlEncodedContent(reschedule));
        CorrectionSchedulingChecks.Check(response.StatusCode==HttpStatusCode.Redirect,"real rescheduling confirmation saves");
        html=WebUtility.HtmlDecode(await client.GetStringAsync($"/mi-trabajo?assignedTask={correction.Id}"));
        CorrectionSchedulingChecks.Check(html.Contains("Corrección reprogramada") && html.Contains("Reprogramación HTTP por ausencia") && html.Contains("Comentario HTTP conservado"),"history records before/after and preserves original correction comment");
        var export=await client.GetStringAsync($"/reportes?handler=Export&F.Range=custom&F.From=2026-11-03&F.To=2026-11-03&F.Plan={source.PlanId}");
        CorrectionSchedulingChecks.Check(System.Text.Json.JsonDocument.Parse(export).RootElement.GetProperty("rows").EnumerateArray().Any(r => r.EnumerateArray().Any(c => c.GetString()==correction.Name)),"report includes correction by effective date after historical plan end");
        var completion=new Dictionary<string,string>{["id"]=correction.Id.ToString(),["conclusion"]="Corrección entregada anticipadamente",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")};
        response=await client.PostAsync("/mi-trabajo?handler=CompleteOperational",new FormUrlEncodedContent(completion));
        CorrectionSchedulingChecks.Check(response.StatusCode==HttpStatusCode.Redirect,"early delivery still works after explicit rescheduling");
        db.ChangeTracker.Clear();var delivered=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        var denied=await CorrectionSchedulingChecks.Service(db,clock).PreviewReviewsAsync(fixture.Director,[new(delivered.Id,delivered.Revision,"REPROGRAMAR","No mover entregada",new())],default);
        CorrectionSchedulingChecks.Check(delivered.CompletedAt!=null && denied.Items.Single().Error=="TASK_CONFLICT","delivered correction cannot be rescheduled");
        var browserSource=await CorrectionSchedulingChecks.Source(db,fixture,clock);
        if(ui){Console.WriteLine($"CORRECTION_UI_READY https://localhost:7289/validaciones?task={browserSource.Id:D} worker=/mi-trabajo?assignedTask={correction.Id:D}");await app.WaitForShutdownAsync();}
        else await app.StopAsync();
    }
}
