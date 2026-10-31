using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Web.Presentation.Navigation;

internal static class ClosingPermissionChecks
{
    static void Check(bool ok,string text) { if(!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    public static async Task RunAsync(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock,
        Guid director, Guid sub, Guid subPerson, Guid admin, Guid worker, Guid candidate)
    {
        var day=WeekContract.LocalToday(clock.UtcNow);
        db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(), PersonId=candidate, Revision=1, From=day,
            ChangedAt=clock.UtcNow, ChangedBy=director, IntentKey=Guid.CreateVersion7(), RequestHash="fixture",
            DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(900,1140)})) });
        db.Add(new TemporalAssignmentState { Enabled=true });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service=new PersonalScheduleService(db,direction,clock);
        foreach(var actor in new[]{director,sub})
            Check((await service.ReadClosingAsync(actor,day,default))[0].Candidates.Any(c=>c.PersonId==candidate),"Authorized role reads eligible candidates");
        foreach(var actor in new[]{admin,worker}) {
            await Denied(()=>service.ReadClosingAsync(actor,day,default));
            await Denied(()=>service.SaveClosingAsync(actor,new(){Day=day,PersonId=candidate},default));
        }
        await Denied(()=>direction.AuthorizeAsync(sub));
        await Denied(()=>service.ReadAsync(sub,worker,default));
        var session=new Session(sub,subPerson,CanonicalRole.Subcoordination);
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent??throw new Exception("Root not found");
        var webRoot=Path.Combine(root.FullName,"src","Sgol.Web","wwwroot");

        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7257");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7257";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages(o=>o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)))).AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>();builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>{o.Cookie.Name="__Host-SGOL-CSRF";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.HeaderName="X-CSRF-TOKEN";});
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<PersonalScheduleService>();builder.Services.AddScoped<IPersonAvatarReader,PersonAvatarService>();
        builder.Services.AddSingleton<IRazorSessionState>(session);
        await using var app=builder.Build();
        app.UseMiddleware<Sgol.Web.Infrastructure.Http.CorrelationIdMiddleware>();
        app.Use(async(ctx,next)=>{ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,session.User.ToString())],"ISOLATED_TEST_ONLY"));await next();});
        app.UseStaticFiles();app.MapRazorPages();app.MapDirectionPreparedPlanApi();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7257")};
        var page=await client.GetStringAsync("/planificacion");
        Check(page.Contains("closing-planning-dialog") && page.Contains("Guardar cobertura"),"Subcoordination sees actual closing form");
        Check((await client.GetAsync("/planificacion?mode=create")).StatusCode==HttpStatusCode.Forbidden,"Direction plan creation remains forbidden");
        var csrf=Hidden(page,"__RequestVerificationToken");
        var key=Guid.CreateVersion7();
        List<KeyValuePair<string,string>> Fields(long revision,Guid intent,Guid personId)=>[
            new("__RequestVerificationToken",csrf),new("Closing.Day",day.ToString("yyyy-MM-dd")),
            new("Closing.PersonId",personId.ToString()),new("Closing.Revision",revision.ToString()),new("Closing.Key",intent.ToString())];
        var noCsrf=await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(0,key,candidate).Where(f=>f.Key!="__RequestVerificationToken")));
        Check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"Missing CSRF rejected");
        var saved=await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(0,key,candidate)));
        Check(saved.StatusCode==HttpStatusCode.Redirect,"Subcoordination saves real Razor form");
        page=await client.GetStringAsync(saved.Headers.Location);
        Check(page.Contains("Cobertura guardada"),"Saved form reopens with success");
        var replay=await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(0,key,candidate)));
        Check(replay.StatusCode==HttpStatusCode.Redirect && await db.Set<ClosingCoverageVersion>().CountAsync()==1,"Replay does not duplicate coverage");
        var version=await db.Set<ClosingCoverageVersion>().AsNoTracking().SingleAsync();
        Check(version.ChangedBy==sub && version.PersonId==candidate,"Stored coverage attributes actual Subcoordination author");
        var stale=await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(0,Guid.CreateVersion7(),candidate)));
        Check(stale.StatusCode==HttpStatusCode.Conflict,"Stale revision rejected through form");
        var invalid=await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(1,Guid.CreateVersion7(),subPerson)));
        Check(invalid.StatusCode==HttpStatusCode.UnprocessableEntity,"Ineligible candidate rejected");
        session.Role=CanonicalRole.Administration; session.User=admin;
        page=await client.GetStringAsync("/planificacion");
        Check(!page.Contains("closing-planning-dialog"),"Administration does not see coverage controls");
        Check((await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(1,Guid.CreateVersion7(),candidate)))).StatusCode==HttpStatusCode.Forbidden,"Administration cannot forge closing POST");
        session.Role=CanonicalRole.Direction; session.User=director;
        page=await client.GetStringAsync("/planificacion"); csrf=Hidden(page,"__RequestVerificationToken");
        Check(page.Contains("closing-planning-dialog"),"Direction retains form");
        Check((await client.PostAsync("/planificacion?handler=Closing",new FormUrlEncodedContent(Fields(1,Guid.CreateVersion7(),candidate)))).StatusCode==HttpStatusCode.Redirect,"Direction retains saving");
        Check(await db.Set<ClosingCoverageVersion>().CountAsync()==2,"Rejected writes left no versions");
        if(Environment.GetEnvironmentVariable("SGOL_CLOSING_PREVIEW")=="1") {
            session.User=sub; session.Role=CanonicalRole.Subcoordination;
            Console.WriteLine("Preview ready https://localhost:7257/planificacion");
            await app.WaitForShutdownAsync();
        }
        await app.StopAsync();
    }
    static async Task Denied(Func<Task> action) {
        try { await action(); throw new Exception("Expected denial"); }
        catch(DirectionPlanException e) when(e.Status==403) { Console.WriteLine("PASS unauthorized service call denied"); }
    }
    static string Hidden(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    sealed class Session(Guid user,Guid person,string role):IRazorSessionState {
        public Guid User {get;set;}=user;
        public string Role {get;set;}=role;
        public bool IsInvalid=>false;public void Invalidate(){}
        public Task<SessionSnapshot?> GetAsync(CancellationToken token=default)=>Task.FromResult<SessionSnapshot?>(new(User,person,"test","Prueba aislada","LOR-001",Role,[WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}