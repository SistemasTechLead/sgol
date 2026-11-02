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

internal static class TaskCatalogFilterChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static async Task Run(SgolDbContext db, bool ui)
    {
        var fixture = await QaFixtures.CreateAsync(db);
        var clock = new CorrectionSchedulingChecks.Clock();
        var prefix = "FILTER-" + Guid.NewGuid().ToString("N")[..6];
        var records = Enumerable.Range(0, 61).Select(i => new OperationalTask {
            Id=Guid.CreateVersion7(), TaskCode=prefix+"-"+i, Name=prefix+" prueba "+i.ToString("00"),
            Requirement="COMENTARIO", PeriodType="SEMANAL", Repetitions=3, OperationalCriticality="IMPORTANTE",
            AssignedRole=CanonicalRole.SalesFloor, Active=true, ScheduleMode="DISTRIBUIDA"
        }).ToArray();
        db.AddRange(records);
        var processId=Guid.CreateVersion7();
        db.Add(new OperationalSequence{Id=processId,Name=prefix+" proceso"});
        db.Add(new OperationalTask{Id=Guid.CreateVersion7(),TaskCode=prefix+"-p",Name=prefix+" etapa",ProcessId=processId,Repetitions=3,PeriodType="SEMANAL"});
        db.Add(new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=prefix+"-i", Name=prefix+" inactiva", Requirement="NINGUNO", Active=false, OperationalCriticality=null });
        db.Add(new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=prefix+"-o", Name=prefix+" ocasional", PeriodType="OCASIONAL", Repetitions=1, StartHour=0, DifferentPeople=false });
        db.Add(new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=prefix+"-d", Name=prefix+" eliminada", Deleted=true });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before=System.Text.Json.JsonSerializer.Serialize(await db.OperationalTasks.AsNoTracking().OrderBy(t=>t.Id).ToArrayAsync());
        var webRoot=Path.GetFullPath("src/Sgol.Web/wwwroot");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7293");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7293";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>();builder.Services.AddScoped<OperationalSupervisionService>();builder.Services.AddScoped<OperationalCapacityService>();builder.Services.AddScoped<OperationalCarryoverService>();builder.Services.AddScoped<OperationalCarryoverReader>();builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddSingleton<IFileTechnicalValidator,FileTechnicalValidator>();builder.Services.AddSingleton<IFileMalwareScanner,CheckScanner>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();builder.Services.AddScoped<OperationalProcessCatalogService>();builder.Services.AddScoped<OperationalSchedulingPolicyService>();builder.Services.AddScoped<AssignmentRefreshService>();
        var directorPerson=await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        // This isolated test host explicitly substitutes authentication; product forms, CSRF,
        // authorization services, transaction boundaries and SQL persistence remain real.
        builder.Services.AddScoped<IRazorSessionState>(sp=>sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Path.StartsWithSegments("/mi-trabajo")
            ? new CheckSession(fixture.Worker,fixture.Person,CanonicalRole.SalesFloor) : new CheckSession(fixture.Director,directorPerson));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{var actor=ctx.Request.Path.StartsWithSegments("/mi-trabajo")?fixture.Worker:fixture.Director;ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"TEST_ONLY"));await next();});
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7293")};
        using(var scope=app.Services.CreateScope()) {
            var service=scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
            var filter=new TaskCatalogFilter{Requirement="COMENTARIO",Period="SEMANAL",Repetitions=3,Criticality="IMPORTANTE",Role=CanonicalRole.SalesFloor,State="ACTIVA",Calendar="DISTRIBUIDA"};
            var first=await service.PageAsync(fixture.Director,prefix,null,1,default,filter);
            var second=await service.PageAsync(fixture.Director,prefix,null,2,default,filter);
            Check(first.Total==61 && first.Items.Length==50 && second.Items.Length==11 && !first.Items.Select(t=>t.Id).Intersect(second.Items.Select(t=>t.Id)).Any(),"combined SQL predicates precede count/pagination");
            var inactive=await service.PageAsync(fixture.Director,prefix,null,1,default,new(){State="INACTIVA",Criticality="SIN_DEFINIR"});
            Check(inactive.Total==1,"inactive historical undefined criticality is searchable");
            var repetitions=await service.PageAsync(fixture.Director,prefix,null,1,default,new(){Repetitions=3});
            Check(repetitions.Total==61,"occasional tasks excluded from repetition filter");
            var process=await service.PageAsync(fixture.Director,prefix,processId,1,default,new(){Period="PROCESO"});
            Check(process.Total==1,"process start filter matches actual process membership");
            var contradictory=await service.PageAsync(fixture.Director,prefix,processId,1,default,new(){Period="SEMANAL"});
            Check(contradictory.Total==0,"process historical calendar is not exposed as effective periodicity");
            var all=await service.PageAsync(fixture.Director,prefix,null,1,default);
            Check(all.Total==64,"unfiltered result includes inactive and process tasks, excludes deleted");
            var empty=await service.PageAsync(fixture.Director,prefix,null,20,default,new(){Requirement="FOTOGRAFIA"});
            Check(empty.Total==0 && empty.Page==1,"empty result clamps page");
            try {await service.PageAsync(fixture.Worker,prefix,null,1,default);throw new Exception("Unexpected access");}catch(DirectionPlanException e){Check(e.Status==403,"worker cannot read catalogue");}
        }
        var html=await client.GetStringAsync("/tareas?Search="+prefix+"&Filter.Requirement=COMENTARIO&Filter.Repetitions=3&PageNumber=2");
        Check(html.Contains("61 tareas encontradas") && html.Contains("Filter.Requirement=COMENTARIO") && html.Contains("Filter.Repetitions=3"),"real Razor binding, total and pagination preserve filters");
        foreach(var bad in new[]{"Filter.Repetitions=abc","Filter.Repetitions=101","Filter.State=unknown","ProcessFilter=broken"}) {
            html=WebUtility.HtmlDecode(await client.GetStringAsync("/tareas?"+bad));
            Check(html.Contains("Revisa los filtros:"),"invalid filter explained: "+bad);
        }
        Check(before==System.Text.Json.JsonSerializer.Serialize(await db.OperationalTasks.AsNoTracking().OrderBy(t=>t.Id).ToArrayAsync()),"queries preserve catalogue records");
        if(ui){Console.WriteLine("FILTER_UI_READY https://localhost:7293/tareas?Search="+prefix);await app.WaitForShutdownAsync();}
        else await app.StopAsync();
    }
}