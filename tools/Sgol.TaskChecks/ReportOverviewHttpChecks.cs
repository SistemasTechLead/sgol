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

internal static class ReportOverviewHttpChecks
{
    public static async Task Run(SgolDbContext db, bool ui)
    {
        void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS report overview: " + name); }
        var fixture=await QaFixtures.CreateAsync(db);
        var clock=new CorrectionSchedulingChecks.Clock { UtcNow=new DateTimeOffset(2026,11,4,18,0,0,TimeSpan.Zero) };
        var sales=await CorrectionSchedulingChecks.Source(db,fixture,clock);
        var sub=await CorrectionSchedulingChecks.Source(db,fixture,clock);
        var historical=await CorrectionSchedulingChecks.Source(db,fixture,clock);
        await db.OperationalAssignments.Where(a=>a.Id==sub.Id).ExecuteUpdateAsync(x=>x.SetProperty(a=>a.AssignedRole,CanonicalRole.Subcoordination));
        await db.OperationalAssignments.Where(a=>a.Id==historical.Id).ExecuteUpdateAsync(x=>x.SetProperty(a=>a.StartsAt,clock.UtcNow.AddMonths(-1)));
        var service=CorrectionSchedulingChecks.Service(db,clock);
        foreach(var reviewer in new[]{CanonicalRole.Direction,CanonicalRole.Subcoordination}) {
            var count=await service.ReviewQueueCountAsync(fixture.Director,reviewer,default);
            var page=await service.PageAsync(fixture.Director,"pending",new Dictionary<string,string>{{"reviewer",reviewer}},1,default);
            Check(count==page.Total && count==page.Filtered,"SQL count equals destination for " + reviewer);
            Check(page.Items.Any(t=>t.Assignment.Id==(reviewer==CanonicalRole.Direction?sub.Id:sales.Id)),"queue includes intended reviewer tasks");
            Check(!page.Items.Any(t=>t.Assignment.Id==(reviewer==CanonicalRole.Direction?sales.Id:sub.Id)),"queue excludes other reviewer tasks");
        }
        var webRoot=Path.GetFullPath("src/Sgol.Web/wwwroot");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7297");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7297";builder.Logging.SetMinimumLevel(LogLevel.Error);
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
        builder.Services.AddScoped<IRazorSessionState>(sp => sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Headers["X-QA-Worker"] == "1" ? new CheckSession(fixture.Worker,fixture.Person,CanonicalRole.SalesFloor) : new CheckSession(fixture.Director,directorPerson));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{var actor=ctx.Request.Path.StartsWithSegments("/mi-trabajo")?fixture.Worker:fixture.Director;ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"TEST_ONLY"));await next();});
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7297")};

        var before=await db.OperationalAssignments.CountAsync();
        var html=WebUtility.HtmlDecode(await client.GetStringAsync("/reportes"));
        Check(html.Contains("Resumen operativo") && html.Contains("report-metric--queue"),"default real Razor report contains queues");
        Check(html.IndexOf("report-result") < html.IndexOf("data-report-save"),"export and save below report");
        foreach(var reviewer in new[]{CanonicalRole.Direction,CanonicalRole.Subcoordination}) {
            var dest=await client.GetStringAsync("/validaciones?Validation=true&Section=pending&pending.reviewer="+reviewer);
            Check(dest.Contains("name=\"pending.reviewer\""),"destination preserves visible queue filter");
        }
        using var worker=new HttpRequestMessage(HttpMethod.Get,"/reportes"); worker.Headers.Add("X-QA-Worker","1");
        var workerHtml=await (await client.SendAsync(worker)).Content.ReadAsStringAsync();
        Check(!workerHtml.Contains("report-metric--queue"),"worker cannot see supervisor queue counts or links");
        Check((await client.GetStringAsync("/reportes?handler=Export")).Contains("metrics"),"export still responds");
        Check(await db.OperationalAssignments.CountAsync()==before,"read-only report and navigation preserve assignment count");
        if(ui) { Console.WriteLine("REPORT_UI_READY https://localhost:7297/reportes"); await app.WaitForShutdownAsync(); }
    }
}
