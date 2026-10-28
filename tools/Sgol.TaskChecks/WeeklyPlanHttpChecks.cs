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

internal static class WeeklyPlanHttpChecks
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    public static async Task RunAsync(SgolDbContext db)
    {
        var at=DateTimeOffset.UtcNow;var clock=new FixedClock(at);var day=WeekContract.LocalToday(at);var week=DirectionPlanContract.OperationsWeek(day);
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode==CanonicalRole.Direction&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var person=await db.AppUsers.Where(u=>u.Id==actor).Select(u=>u.PersonId).SingleAsync();
        var codes=await db.OperationalTasks.Where(t=>t.Active&&!t.Deleted&&t.IsWeeklyOperationsTask).OrderBy(t=>t.TaskCode).Select(t=>t.TaskCode).ToArrayAsync();
        var original=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();var originalJson=JsonSerializer.Serialize(original);
        var input=new DirectionPlanInput("Semana 44","Plan de Operaciones Semanal","SEMANAL",week.Year,week.Week,codes,SchedulingNotBefore:OperationalSchedule.Instant(day),SchedulingSearchMultiplier:2,IsWeeklyOperationsPlan:true);
        var payload=JsonSerializer.SerializeToElement(input,JsonSerializerOptions.Web);
        var parsed=DirectionPreparedPlanApiEndpoints.Parse(payload);
        Check(parsed.SchedulingNotBefore==input.SchedulingNotBefore&&parsed.SchedulingSearchMultiplier==2,"API parser preserves both planning parameters emitted by the real client");
        var legacy=JsonNode.Parse(payload.GetRawText())!.AsObject();legacy.Remove("schedulingNotBefore");legacy.Remove("schedulingSearchMultiplier");
        var legacyParsed=DirectionPreparedPlanApiEndpoints.Parse(JsonSerializer.SerializeToElement(legacy));
        Check(legacyParsed.SchedulingNotBefore==null&&legacyParsed.SchedulingSearchMultiplier==1,"Older payloads retain optional-field defaults");
        foreach(var (field,value) in new (string,JsonNode?)[]{("schedulingNotBefore",JsonValue.Create("invalid")),("schedulingNotBefore",JsonValue.Create(5)),("schedulingSearchMultiplier",JsonValue.Create(0)),("schedulingSearchMultiplier",JsonValue.Create(5)),("schedulingSearchMultiplier",JsonValue.Create("2")),("unknownField",JsonValue.Create(true))})
        {
            var invalid=JsonNode.Parse(payload.GetRawText())!.AsObject();invalid[field]=value;
            try{DirectionPreparedPlanApiEndpoints.Parse(JsonSerializer.SerializeToElement(invalid));throw new Exception("Malformed field accepted: "+field);}catch(FormatException){Console.WriteLine("PASS strict rejection: "+field);}
        }
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent??throw new Exception("Root not found");
        var webRoot=Path.Combine(root.FullName,"src","Sgol.Web","wwwroot");
        var output=Path.Combine(root.FullName,"outputs","plan-http-20261026");Directory.CreateDirectory(output);
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7257");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7257";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages(o=>o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)))).AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>();builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>{o.Cookie.Name="__Host-SGOL-CSRF";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.HeaderName="X-CSRF-TOKEN";});
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<PersonalScheduleService>();builder.Services.AddScoped<IPersonAvatarReader,PersonAvatarService>();
        builder.Services.AddSingleton<IRazorSessionState>(new Session(actor,person));
        await using var app=builder.Build();
        app.UseMiddleware<Sgol.Web.Infrastructure.Http.CorrelationIdMiddleware>();
        app.Use(async(ctx,next)=>{ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString())],"ISOLATED_TEST_ONLY"));await next();});
        app.MapRazorPages();app.MapDirectionPreparedPlanApi();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7257")};
        var page=await client.GetStringAsync("/planificacion?mode=operations");var csrf=Hidden(page,"__RequestVerificationToken");Check(csrf.Length>0,"Actual Razor operations form opens with CSRF protection");
        var fields=new List<KeyValuePair<string,string>>{new("__RequestVerificationToken",csrf),new("name",input.Name),new("area",input.Area),new("periodType","SEMANAL"),new("year",week.Year.ToString()),new("periodNumber",week.Week.ToString()),new("isWeeklyOperationsPlan","true"),new("capacityAdjustments","[]"),new("capacityFingerprint",""),new("searchMultiplier","2")};fields.AddRange(codes.Select(c=>new KeyValuePair<string,string>("taskCodes",c)));
        var prepared=await client.PostAsync("/planificacion?handler=PrepareDirectionPlan",new FormUrlEncodedContent(fields));page=await prepared.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(Path.Combine(output,"prepare.html"),page);
        var intent=Hidden(page,"directionIntent");csrf=Hidden(page,"__RequestVerificationToken");Check(prepared.IsSuccessStatusCode&&intent.Length>0,"Actual form prepares the complete catalogue and issues its signed save intention");
        var saveFields=new[]{new KeyValuePair<string,string>("__RequestVerificationToken",csrf),new("directionIntent",intent)};
        var saved=await client.PostAsync("/planificacion?handler=SaveDirectionPlan",new FormUrlEncodedContent(saveFields));var response=await saved.Content.ReadAsStringAsync();await File.WriteAllTextAsync(Path.Combine(output,"save-response.html"),response);
        Check(saved.StatusCode==HttpStatusCode.Redirect&&saved.Headers.Location?.OriginalString.StartsWith("/planificacion?planId=")==true,"Razor save crosses the real API over HTTP and redirects to the created plan");
        var id=Guid.Parse(saved.Headers.Location!.OriginalString.Split("planId=")[1]);var rows=await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==id).ToArrayAsync();
        Check(rows.Length>0&&rows.Select(a=>a.ScheduleKey).Distinct().Count()==rows.Length,$"HTTP flow persisted {rows.Length} distinct assignments only in the isolated DB");
        var retried=await client.PostAsync("/planificacion?handler=SaveDirectionPlan",new FormUrlEncodedContent(saveFields));Check(retried.StatusCode==HttpStatusCode.Redirect&&retried.Headers.Location==saved.Headers.Location,"Resending the same form recovers the same plan without duplicates");
        var detail=await client.GetStringAsync(saved.Headers.Location);
        Check(detail.Contains(input.Name),"Persisted plan detail opens after HTTP save");
        var storedPlan=await db.DirectionPreparedPlans.AsNoTracking().SingleAsync(p=>p.Id==id);
        if(JsonSerializer.Deserialize<MonthlyPlanPending[]>(storedPlan.MonthlyPendingJson)!.Length>0)
            Check(detail.Contains("Ocurrencias sin asignaci"),"Persisted plan detail renders its pending notices");
        var ids=original.Select(a=>a.Id).ToArray();Check(originalJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>ids.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()),"All original assignments remain identical after the complete form/API lifecycle");
        await app.StopAsync();
    }
    static string Hidden(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    sealed class FixedClock(DateTimeOffset at):IClock{public DateTimeOffset UtcNow=>at;}
    sealed class Session(Guid user,Guid person):IRazorSessionState
    {
        public bool IsInvalid=>false;public void Invalidate(){}
        public Task<SessionSnapshot?> GetAsync(CancellationToken token=default)=>Task.FromResult<SessionSnapshot?>(new(user,person,"test","Dirección · Prueba aislada","LOR-001",CanonicalRole.Direction,[WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}
