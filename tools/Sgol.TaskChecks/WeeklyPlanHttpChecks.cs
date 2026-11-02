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
    public static async Task RunAsync(SgolDbContext db, bool expectConflict = false, bool resolveConflicts = false, bool ui = false, bool isolatedFixture = false, bool moveConflicts = false, bool navigationOnly = false)
    {
        var at=DateTimeOffset.UtcNow;var clock=new FixedClock(at);var day=WeekContract.LocalToday(at);var week=DirectionPlanContract.OperationsWeek(day);
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode==CanonicalRole.Direction&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var person=await db.AppUsers.Where(u=>u.Id==actor).Select(u=>u.PersonId).SingleAsync();
        if (isolatedFixture)
        {
            // Only the guarded test DB: retain historical assignments, replace weekly selection
            // with two new fixed tasks whose total needs a dated capacity increase.
            var fixture=await QaFixtures.CreateAsync(db);
            var specialty=new Sgol.Web.Infrastructure.Persistence.Organization.Specialization { Id=Guid.CreateVersion7(),Name="Plan resolution fixture "+Guid.NewGuid().ToString("N")[..6],NormalizedName=Guid.NewGuid().ToString("N"),Active=true,Revision=1 };
            db.Add(specialty);
            db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization{PersonId=fixture.Person,SpecializationId=specialty.Id});
            await db.OperationalTasks.ExecuteUpdateAsync(s=>s.SetProperty(t=>t.IsWeeklyOperationsTask,false));
            foreach(var code in Enumerable.Range(0,2).Select(_=>"TAR-RES-"+Guid.NewGuid().ToString("N")[..6]))
            {
                var task=new OperationalTask{Id=Guid.CreateVersion7(),TaskCode=code,Name="Trabajo especializado de prueba "+code,
                    AssignedRole=CanonicalRole.SalesFloor,SpecializationId=specialty.Id,ScheduleMode="DIAS_FIJOS",
                    WeekDays=1<<(int)day.AddDays(1).DayOfWeek,StartHour=9,LoadMinutes=130,OperationalCriticality=moveConflicts ? "FLEXIBLE" : "IMPORTANTE",IsWeeklyOperationsTask=true};
                task.Validate();db.Add(task);
            }
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
        }
        var codes=await db.OperationalTasks.Where(t=>t.Active&&!t.Deleted&&t.IsWeeklyOperationsTask).OrderBy(t=>t.TaskCode).Select(t=>t.TaskCode).ToArrayAsync();
        var original=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();var originalJson=JsonSerializer.Serialize(original);
        var capacityBefore = JsonSerializer.Serialize(await db.OperationalCapacities.AsNoTracking().OrderBy(c=>c.Id).ToArrayAsync());
        var input=new DirectionPlanInput($"Semana {week.Week:00}","Plan de Operaciones Semanal","SEMANAL",week.Year,week.Week,codes,SchedulingNotBefore:OperationalSchedule.Instant(day),SchedulingSearchMultiplier:2,IsWeeklyOperationsPlan:true);
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
        var output=Path.Combine(root.FullName,"outputs",moveConflicts ? "plan-moves-20261102" : resolveConflicts ? "plan-resolutions-20261102" : "plan-http-20261026");Directory.CreateDirectory(output);
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
        app.UseStaticFiles();app.MapRazorPages();app.MapDirectionPreparedPlanApi();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7257")};
        if (navigationOnly)
        {
            var existing = await db.DirectionPreparedPlans.AsNoTracking().SingleAsync(p => p.IsWeeklyOperationsPlan && p.Year == week.Year && p.PeriodNumber == week.Week);
            static string ActiveHref(string html) => Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*aria-current=\"page\"").Groups[1].Value;
            foreach (var route in new[] { "/planificacion?mode=operations", $"/planificacion?planId={existing.Id:D}", "/planificacion" })
            {
                var navigationResponse = await client.GetAsync(route);
                var html = await navigationResponse.Content.ReadAsStringAsync();
                Check(navigationResponse.StatusCode == HttpStatusCode.OK, "Navigation renders without losing origin: " + route);
                var nav = Regex.Match(html, "<nav[^>]*data-section=\"planning\"[\\s\\S]*?</nav>").Value;
                Check(ActiveHref(nav) == (route.Contains("mode=operations") ? route : "/planificacion"), "Exactly the originating section stays active: " + route);
                Check(Regex.Matches(nav, "aria-current=\"page\"").Count == 1, "One accessible current section");
                if (route.Contains("mode=operations")) Check(html.Contains(existing.Name) && !html.Contains("id=\"direction-create-form\""), "Existing weekly plan is shown, not a new creation form");
            }
            Check(originalJson == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()), "Navigation preserves all assignments");
            if (ui) { Console.WriteLine("NAVIGATION_UI_READY https://localhost:7257"); await app.WaitForShutdownAsync(); }
            else await app.StopAsync();
            return;
        }
        var page=await client.GetStringAsync("/planificacion?mode=operations");var csrf=Hidden(page,"__RequestVerificationToken");Check(csrf.Length>0,"Actual Razor operations form opens with CSRF protection");
        var fields=new List<KeyValuePair<string,string>>{new("__RequestVerificationToken",csrf),new("name",input.Name),new("area",input.Area),new("periodType","SEMANAL"),new("year",week.Year.ToString()),new("periodNumber",week.Week.ToString()),new("isWeeklyOperationsPlan","true"),new("capacityAdjustments","[]"),new("capacityFingerprint",""),new("searchMultiplier","2")};fields.AddRange(codes.Select(c=>new KeyValuePair<string,string>("taskCodes",c)));
        var prepared=await client.PostAsync("/planificacion?handler=PrepareDirectionPlan",new FormUrlEncodedContent(fields));page=await prepared.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(Path.Combine(output,"prepare.html"),page);
        var proposed = new List<PlanCapacityAdjustment>();
        var proposedMoves = new List<PlanOccurrenceMove>();
        if (resolveConflicts)
        {
            Check((int)prepared.StatusCode == 422 && page.Contains("direction-conflict-review") && page.Contains("resolutionAdjustment"),
                "Blocked task row opens a review dialog with a dated capacity option");
            if (ui)
            {
                Console.WriteLine("UI_READY https://localhost:7257; press Enter after browser checks");
                await Task.Run(() => Console.ReadLine());
            }
            var firstChoice = WebUtility.HtmlDecode(Regex.Match(page, "name=\"resolutionAdjustment\" value=\"([^\"]+)\"").Groups[1].Value);
            var badChoice = JsonNode.Parse(firstChoice)!.AsObject();badChoice["dailyMinutes"]=481;
            var invalidFields = fields.Where(f=>f.Key!="capacityFingerprint" && f.Key!="__RequestVerificationToken").ToList();
            invalidFields.Add(new("capacityFingerprint",Hidden(page,"capacityFingerprint")));
            invalidFields.Add(new("__RequestVerificationToken",Hidden(page,"__RequestVerificationToken")));
            invalidFields.Add(new("resolutionAdjustment",badChoice.ToJsonString()));
            Check((int)(await client.PostAsync("/planificacion?handler=PrepareDirectionPlan",new FormUrlEncodedContent(invalidFields))).StatusCode==422,"Tampered capacity above 480 is rejected by the server");
            for (var attempt=0; (int)prepared.StatusCode==422 && attempt<30; attempt++)
            {
                var choiceField = moveConflicts ? "moveChoice" : "resolutionAdjustment";
                var choice = WebUtility.HtmlDecode(Regex.Matches(page, $"name=\"{choiceField}\" value=\"([^\"]+)\"").FirstOrDefault()?.Groups[1].Value ?? "");
                Check(choice.Length>0,"Remaining conflict supplies a capacity resolution within existing schedule");
                if(moveConflicts) {var move=JsonSerializer.Deserialize<PlanOccurrenceMove>(choice,JsonSerializerOptions.Web)!;proposedMoves.RemoveAll(m=>m.TaskCode==move.TaskCode&&m.FromDay==move.FromDay);proposedMoves.Add(move);}
                else { var adjustment=JsonSerializer.Deserialize<PlanCapacityAdjustment>(choice,JsonSerializerOptions.Web)!;proposed.RemoveAll(a=>a.PersonId==adjustment.PersonId&&a.Day==adjustment.Day);proposed.Add(adjustment); }
                fields=fields.Where(f=>f.Key is not ("capacityAdjustments" or "capacityFingerprint" or "__RequestVerificationToken" or "resolutionAdjustment" or "occurrenceMoves" or "moveChoice")).ToList();
                fields.Add(new("__RequestVerificationToken",Hidden(page,"__RequestVerificationToken")));
                fields.Add(new("capacityAdjustments",Hidden(page,"capacityAdjustments")));
                fields.Add(new("capacityFingerprint",Hidden(page,"capacityFingerprint")));
                fields.Add(new("occurrenceMoves",Hidden(page,"occurrenceMoves")));
                fields.Add(new(choiceField,choice));
                prepared=await client.PostAsync("/planificacion?handler=PrepareDirectionPlan",new FormUrlEncodedContent(fields));page=await prepared.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(Path.Combine(output,$"resolution-{attempt+1}.html"),page);
                Check(capacityBefore==JsonSerializer.Serialize(await db.OperationalCapacities.AsNoTracking().OrderBy(c=>c.Id).ToArrayAsync()),"Selecting a resolution does not write shared capacity before saving");
            }
            Check(prepared.IsSuccessStatusCode && Hidden(page,"directionIntent").Length>0,"Successive choices produce a complete confirmable plan");
            Check(originalJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Recalculation preserves all existing assignments");
            foreach(var adjustment in proposed)
                Check(page.Contains(adjustment.Day!.Value.ToString("dd/MM/yyyy")),"Confirmation identifies each adjusted date");
            foreach(var move in proposedMoves) Check(page.Contains(move.ToDay.ToString("dd/MM/yyyy")) && page.Contains("Vencimiento"),"Confirmation shows moved date and resulting deadline");
            var definition=await db.OperationalTasks.AsNoTracking().FirstAsync(t=>t.TaskCode==codes[0]);
            await db.OperationalTasks.Where(t=>t.Id==definition.Id).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.Revision,t=>t.Revision+1));
            var stale=await client.PostAsync("/planificacion?handler=SaveDirectionPlan",new FormUrlEncodedContent(new[]{
                new KeyValuePair<string,string>("__RequestVerificationToken",Hidden(page,"__RequestVerificationToken")),new("directionIntent",Hidden(page,"directionIntent"))}));
            Check((int)stale.StatusCode==409,"Concurrent catalogue edit rejects the old confirmation");
            Check(capacityBefore==JsonSerializer.Serialize(await db.OperationalCapacities.AsNoTracking().OrderBy(c=>c.Id).ToArrayAsync()),"Stale confirmation does not persist any capacity adjustment");
            Check(originalJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Stale confirmation does not persist partial assignments");
            await db.OperationalTasks.Where(t=>t.Id==definition.Id).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.Revision,definition.Revision));
            prepared=await client.PostAsync("/planificacion?handler=PrepareDirectionPlan",new FormUrlEncodedContent(fields));page=await prepared.Content.ReadAsStringAsync();
        }
        if (expectConflict)
        {
            var decoded = WebUtility.HtmlDecode(page);
            Check((int)prepared.StatusCode == 422 && decoded.Contains("No se pudo asignar ") && decoded.Contains("Carga / capacidad"),
                "Actual operations form displays blocked task and candidate diagnostics on rejection");
            Check(Hidden(page,"directionIntent").Length == 0, "Blocked preview does not issue a save intention");
            Check(originalJson == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),
                "Rejected HTTP preparation leaves all assignments unchanged");
            Console.WriteLine(Regex.Match(decoded, "No se pudo asignar [^<]+").Value);
            if(ui) {Console.WriteLine("UI_READY https://localhost:7257; press Enter after browser checks");await Task.Run(()=>Console.ReadLine());}
            await app.StopAsync();
            return;
        }
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
        if (resolveConflicts)
        {
            foreach(var move in proposedMoves)
            {
                var moved=rows.Single(a=>a.TaskCode==move.TaskCode);
                Check(WeekContract.LocalToday(moved.StartsAt)==move.ToDay && moved.ScheduleKey==$"{moved.TaskId:N}:{move.FromDay:yyyyMMdd}:0","Moved occurrence uses target date and preserves source reservation identity");
                var definition=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==moved.TaskId);
                Check((definition.WeekDays & (1<<(int)move.FromDay.DayOfWeek))!=0 && (definition.WeekDays & (1<<(int)move.ToDay.DayOfWeek))==0,"Moving an occurrence does not edit its catalogue calendar");
                Check(await db.AuditEvents.AnyAsync(a=>a.ResourceId==moved.Id&&a.Action=="PLAN_OCCURRENCE_MOVED"),"Date exception is audited with its assignment");
            }
            foreach(var adjustment in proposed)
                Check(await db.OperationalCapacities.AnyAsync(c=>c.PersonId==adjustment.PersonId && c.From==adjustment.Day && c.Through==adjustment.Day &&
                    c.Percent==adjustment.Percent && c.DailyMinutes==adjustment.DailyMinutes),"Saving persists the selected capacity only for its chosen date");
            var previous=JsonSerializer.Deserialize<OperationalCapacity[]>(capacityBefore)!;
            foreach(var setting in previous.Where(c=>!proposed.Any(p=>p.PersonId==c.PersonId&&p.Day>=c.From&&p.Day<=c.Through)))
                Check(JsonSerializer.Serialize(setting)==JsonSerializer.Serialize(await db.OperationalCapacities.AsNoTracking().SingleAsync(c=>c.Id==setting.Id)),"Unrelated and historical capacity stays unchanged");
            Check(await db.PlanCapacityAdjustments.CountAsync(c=>c.PlanId==id)==proposed.Count,"Each dated adjustment has a restoration snapshot attached to the plan");
        }
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
