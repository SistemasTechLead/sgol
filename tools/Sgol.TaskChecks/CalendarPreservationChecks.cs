using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

internal static class CalendarPreservationChecks
{
    sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026,11,3,17,0,0,TimeSpan.Zero); }
    sealed class Session(Guid actor, Guid person, IClock clock, string role) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken=default) =>
            Task.FromResult<SessionSnapshot?>(new(actor,person,"calendar-check","Prueba","LOR-001",role,
                [WeekAuthorization.View],clock.UtcNow,clock.UtcNow.AddHours(1),clock.UtcNow.AddHours(2)));
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); Console.WriteLine("PASS calendar preservation: "+message); }
    public static async Task RunAsync(SgolDbContext db)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found");
        var webRoot = Path.Combine(root.FullName, "src", "Sgol.Web", "wwwroot");
        var fixture=await QaFixtures.CreateAsync(db);
        var clock=new Clock();
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var service=new AssignmentRefreshService(db,direction,clock);
        var task=await db.OperationalTasks.SingleAsync(t=>t.Id==fixture.Definition);
        task.PeriodType="MENSUAL";task.Repetitions=1;task.ScheduleMode="DISTRIBUIDA";
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,fixture.Director,clock.UtcNow,"Conservar calendario",new(2026,11,2),new(2026,11,8));
        db.Add(plan);
        var row=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=task.Id,TaskCode=task.TaskCode,
            PersonId=fixture.Person,PersonName="Responsable de prueba con nombre extenso",AssignedRole=task.AssignedRole};
        AssignmentDefinition.Adopt(row,task,null,false);
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        OperationalAgenda.Capture(row,policy,"FLEXIBLE",new(2026,11,3,18,0,0,TimeSpan.Zero));
        db.Add(row);
        var otherTask=new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="CAL-OTHER",Name="Otra tarea",Requirement="NINGUNO",LoadMinutes=10,OperationalCriticality="FLEXIBLE"};
        db.Add(otherTask);
        var other=AssignmentDefinition.Copy(row);other.Id=Guid.CreateVersion7();other.TaskId=otherTask.Id;
        AssignmentDefinition.Adopt(other,otherTask,null,false);other.StartsAt=other.StartsAt.AddHours(1);other.DueAt=other.DueAt.AddHours(1);other.NominalStartsAt=other.StartsAt;db.Add(other);
        await db.SaveChangesAsync();
        task.PreferredMonthDays="29";task.Revision++;
        otherTask.Name="Otra tarea actualizada";otherTask.Revision++;
        db.Add(new OperationalCaptureSession {Id=Guid.CreateVersion7(),AssignmentId=row.Id,ActorId=fixture.Worker,CreatedAt=clock.UtcNow,ExpiresAt=clock.UtcNow.AddMinutes(30)});
        var carryover = new OperationalCarryoverRequest {Id=Guid.CreateVersion7(),BranchId=BranchScope.LorettaId,PersonId=fixture.Person,
            RequestedBy=fixture.Worker,IntentKey=Guid.NewGuid(),Kind="POSPONER",From=new(2026,11,4),Through=new(2026,11,10),
            Cause="DEPENDENCIA_EXTERNA",Explanation="Prueba de conservación",IncidentStart=clock.UtcNow,IncidentEnd=clock.UtcNow.AddMinutes(30),Minutes=30,SubmittedAt=clock.UtcNow,
            SnapshotJson=JsonSerializer.Serialize(new[] {new CarryoverItem(row.Id,row.PersonId,row.PlanId,row.Revision,row.Name,row.AssignedRole,row.StartsAt,row.DueAt,row.DueAt.AddDays(1),true,row.Id,null,"","",row.PersonName)})};
        db.Add(carryover);
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var carryoverBefore=JsonSerializer.Serialize(await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==carryover.Id));
        var before=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==row.Id));
        var request=new AssignmentRefreshRequest(Guid.Empty,Guid.NewGuid(),clock.UtcNow.AddMinutes(3));
        var preview=await service.PreviewAsync(fixture.Director,request,default);
        Check(!preview.CanApply && preview.Decisions.Any(d=>d.AssignmentId==row.Id && d.Problem!.Contains("sesión de captura")),"exact cause and per-assignment decision; no false evidence claim");
        Check(preview.Decisions.Single(d=>d.AssignmentId==row.Id).Problem!.Contains("posposición pendiente"),"pending request identified alongside capture");
        var accepted=request with {PreserveCalendarAssignmentIds=[row.Id]};
        var ready=await service.PreviewAsync(fixture.Director,accepted,default);
        Check(ready.CanApply && ready.Differences.All(d=>d.Before?.Id!=row.Id),"preserved row excluded and other changes remain applicable: "+string.Join(";",ready.Conflicts));
        try {await service.ApplyAsync(fixture.Director,request,ready.Fingerprint,"Tampered",default);throw new Exception("Missing consent accepted");}
        catch(DirectionPlanException e) when(e.Code=="REFRESH_STALE") {Check(true,"cannot remove consent when confirming");}
        db.ChangeTracker.Clear();
        db.Add(new OperationalAttachment {AssignmentId=row.Id,FileName="nuevo.pdf",ContentType="application/pdf",Content=[1,2],CreatedAt=clock.UtcNow});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        try {await service.ApplyAsync(fixture.Director,accepted,ready.Fingerprint,"Stale evidence",default);throw new Exception("Stale evidence accepted");}
        catch(DirectionPlanException e) when(e.Code=="REFRESH_STALE") {Check(true,"new evidence invalidates preview atomically");}
        db.ChangeTracker.Clear();
        var directorPerson=await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7297");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7297";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();builder.Services.AddScoped<AssignmentRefreshService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<IRazorSessionState>(sp=>new Session(fixture.Director,directorPerson,clock,
            sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Cookies["test-role"]=="worker"?CanonicalRole.SalesFloor:CanonicalRole.Direction));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,fixture.Director.ToString())],"TEST_ONLY"));await next();});
        app.UseStaticFiles(new StaticFileOptions {FileProvider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot)});app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler {AllowAutoRedirect=false,CookieContainer=new()};
        using var client=new HttpClient(handler) {BaseAddress=new("https://localhost:7297")};
        string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var html=await client.GetStringAsync("/tareas/actualizar");
        Check(html.Contains("name=\"PreserveCalendarAssignmentIds\""),"real Razor renders checkbox");
        foreach (var code in new[] {"inactive","excluded","calendar","remaining","deadline","process","calendar-preserve"})
        {
            var copy=Sgol.Web.Pages.Tasks.RefreshCaseText.Decision(new(code,"fallback","fallback",["TAR-TEST"],false));
            Check(copy.Action!="fallback" && copy.Effect!="fallback","specific compact decision: "+code);
        }
        foreach (var code in new[] {"specialty","process-mode","process-evidence","progress-unit","worker","invalid-date","schedule","priority","size","capacity","search","carryover","deadline","dependency","critical-stage"})
        {
            var copy=Sgol.Web.Pages.Tasks.RefreshCaseText.Issue(new(code,"detail","TAR-TEST","Task",null));
            Check(copy.Cause!="La actualización requiere revisión.","specific cause and solution: "+code);
        }
        // Produce a real blocked case, then return to the normal confirmation scenario.
        var blockedRow=await db.OperationalAssignments.SingleAsync(a=>a.Id==other.Id);
        blockedRow.ProgressUnits=1;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var blockedPreview=await service.PreviewAsync(fixture.Director,accepted,default);
        Check(blockedPreview.Issues.Any(i=>i.Code=="progress-unit" && i.TaskCode==other.TaskCode),"blocked issue identifies its task structurally");
        var blockedHtml=WebUtility.HtmlDecode(await client.GetStringAsync("/tareas/actualizar"));
        Check(blockedHtml.Contains("Hay avance registrado con la unidad anterior.") && blockedHtml.Contains("Conservar la unidad y modalidad"),"blocked cause and solution render in real page");
        blockedRow=await db.OperationalAssignments.SingleAsync(a=>a.Id==other.Id);blockedRow.ProgressUnits=0;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        foreach(var index in new[] {1,2})
        {
            var inactive=new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="INACTIVE-"+index,Name="Tarea inactiva "+index,Requirement="NINGUNO",LoadMinutes=10,OperationalCriticality="FLEXIBLE",Active=false};
            db.Add(inactive);
            var inactiveRow=AssignmentDefinition.Copy(other);inactiveRow.Id=Guid.CreateVersion7();inactiveRow.TaskId=inactive.Id;inactiveRow.TaskCode=inactive.TaskCode;
            AssignmentDefinition.Adopt(inactiveRow,inactive,null,false);db.Add(inactiveRow);
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        html=await client.GetStringAsync("/tareas/actualizar");
        if(Environment.GetEnvironmentVariable("SGOL_CALENDAR_UI")=="1") {
            Console.WriteLine("CALENDAR_UI_READY https://localhost:7297/tareas/actualizar");
            var done=Path.Combine(root.FullName,".local/calendar-ui-done");
            File.Delete(done);
            while(!File.Exists(done)) await Task.Delay(500);
        }
        var response=await client.PostAsync("/tareas/actualizar?handler=Preview",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["PreserveCalendarAssignmentIds"]=row.Id.ToString(),["RetireInactiveAssignments"]="true"}));
        html=await response.Content.ReadAsStringAsync();
        Check(response.IsSuccessStatusCode && html.Contains("data-refresh-apply"),"real preview binding enables confirmation");
        var form=new Dictionary<string,string> {["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken"),["Key"]=Field(html,"Key"),
            ["NotBefore"]=Field(html,"NotBefore"),["Fingerprint"]=Field(html,"Fingerprint"),["PreserveCalendarAssignmentIds"]=row.Id.ToString(),["RetireInactiveAssignments"]="true",["Reason"]="Conservar pendiente con captura"};
        response=await client.PostAsync("/tareas/actualizar?handler=Apply",new FormUrlEncodedContent(form));
        Check(response.StatusCode==HttpStatusCode.Redirect,"real confirmation succeeds");
        response=await client.PostAsync("/tareas/actualizar?handler=Apply",new FormUrlEncodedContent(form));
        Check(response.StatusCode==HttpStatusCode.Redirect,"confirmation retry is idempotent");
        db.ChangeTracker.Clear();
        Check(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==row.Id)),"complete preserved assignment unchanged, including revision");
        Check(await db.OperationalCaptureSessionsCount(row.Id)==1,"capture retained");
        Check(carryoverBefore==JsonSerializer.Serialize(await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==carryover.Id)),"pending request unchanged");
        Check(await db.OperationalAttachments.AnyAsync(a=>a.AssignmentId==row.Id),"new attachment preserved");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==other.Id)).Name==otherTask.Name,"other assignment updated");
        handler.CookieContainer.Add(client.BaseAddress,new Cookie("test-role","worker"));
        Check((await client.GetAsync("/tareas/actualizar")).StatusCode==HttpStatusCode.Forbidden,"worker denied");
    }
    static Task<int> OperationalCaptureSessionsCount(this SgolDbContext db,Guid id)=>db.Set<OperationalCaptureSession>().CountAsync(s=>s.AssignmentId==id);
}
