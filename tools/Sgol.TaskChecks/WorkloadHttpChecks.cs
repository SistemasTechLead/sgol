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

internal static class WorkloadHttpChecks
{
    internal sealed class Clock : IClock
    {
        private readonly System.Diagnostics.Stopwatch elapsed = System.Diagnostics.Stopwatch.StartNew();
        public TimeSpan Advance { get; set; }
        public DateTimeOffset UtcNow => TemporalEligibilityContext.At(new(2026, 11, 4), 600).Add(elapsed.Elapsed).Add(Advance);
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS workload: " + message); }
    public static async Task Run(SgolDbContext db, bool ui)
    {
        var clock = new Clock();
        var fixture = await QaFixtures.CreateAsync(db);
        var day = WeekContract.LocalToday(clock.UtcNow);
        var specialty = new Sgol.Web.Infrastructure.Persistence.Organization.Specialization { Id=Guid.CreateVersion7(),Name="Carga QA "+Guid.NewGuid().ToString("N"),NormalizedName=Guid.NewGuid().ToString("N"),Revision=1 };
        db.Add(specialty);
        foreach(var person in new[]{fixture.Person,fixture.OtherPerson}) db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization {PersonId=person,SpecializationId=specialty.Id});
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        async Task<OperationalAssignment> Add(string name, int hour, bool completed = false, bool progress = false) {
            var a = new OperationalAssignment { Id=Guid.CreateVersion7(), PlanId=fixture.Plan, TaskId=fixture.Definition,
                SpecializationId=specialty.Id, TaskCode="QA-CARGA", Name=name, PersonId=fixture.Person, PersonName="Persona QA con nombre largo para comprobar redistribuciones", AssignedRole=CanonicalRole.SalesFloor,
                Requirement="NINGUNO", LoadMinutes=30, GoalUnits=progress ? 100 : 0, ProgressUnits=progress ? 20 : 0,
                CompletedAt=completed ? clock.UtcNow.AddMinutes(-30) : null, CompletedBy=completed ? fixture.Worker : null,
                ReviewStatus=completed ? "CONCLUIDA" : null };
            OperationalAgenda.Capture(a, policy, "FLEXIBLE", TemporalEligibilityContext.At(day, hour * 60));
            db.Add(a); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return a;
        }
        var future = await Add("Futura redistribuible", 11);
        var active = await Add("Activa que conserva responsable sin selección", 10);
        var protectedTask = await Add("Avance protegido", 9, progress:true);
        var completedTask = await Add("Histórica concluida", 9, completed:true);
        var originalProtected = System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==protectedTask.Id || a.Id==completedTask.Id).OrderBy(a=>a.Id).ToArrayAsync());
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
        var directorPerson=await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        // This isolated test host explicitly substitutes authentication; product forms, CSRF,
        // authorization services, transaction boundaries and SQL persistence remain real.
        builder.Services.AddScoped<IRazorSessionState>(sp=>sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Headers["X-QA-Worker"] == "1"
            ? new CheckSession(fixture.Worker,fixture.Person,CanonicalRole.SalesFloor) : new CheckSession(fixture.Director,directorPerson));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{var actor=ctx.Request.Headers["X-QA-Worker"] == "1" ? fixture.Worker : fixture.Director;ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"TEST_ONLY"));await next();});
        app.MapGet("/qa-token", (HttpContext ctx, Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf) => csrf.GetAndStoreTokens(ctx).RequestToken!);
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7293")};
        string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        async Task<string> Get(string path) => await client.GetStringAsync(path);
        var html = await Get("/supervision/carga");
        Check(html.Contains("id=\"capacity\"") && html.Contains("handler=CapacityPreview") && html.Contains("capacity.js"), "new route renders capacity form and script");
        Check(!(await Get("/validaciones")).Contains("id=\"capacity\""), "supervision no longer contains capacity editor");
        var form = new Dictionary<string,string> { ["Range"]="day", ["Adjustment.PersonId"]=fixture.Person.ToString(), ["Adjustment.From"]=day.ToString("yyyy-MM-dd"), ["Adjustment.Through"]=day.ToString("yyyy-MM-dd"), ["Adjustment.Percent"]="0", ["Adjustment.DailyMinutes"]="240", ["Adjustment.Reason"]="Ajuste operativo HTTP", ["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken") };
        async Task<(HttpResponseMessage response,string html)> Post(string handlerName, Dictionary<string,string> data) {
            var r=await client.PostAsync("/supervision/carga?handler="+handlerName,new FormUrlEncodedContent(data)); var body=await r.Content.ReadAsStringAsync();
            if((int)r.StatusCode>=400) { Directory.CreateDirectory(".local/workload-20261104"); await File.WriteAllTextAsync(".local/workload-20261104/last-error.html",body); }
            return (r,body);
        }
        var p=await Post("CapacityPreview",form);
        Check(p.response.IsSuccessStatusCode && p.html.Contains("data-capacity-preview"), "HTTP preview accepts only capacity fields, separate from transfer form");
        Check(!await db.OperationalCapacities.AnyAsync(c=>c.PersonId==fixture.Person),"preview leaves capacity unchanged");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==future.Id)).PersonId==fixture.Person,"preview leaves assignments unchanged");
        Check(p.html.Contains(future.Name) && p.html.Contains("Quedan"),"preview shows movable task and protected excess");
        form["CapacityFingerprint"]=Field(p.html,"CapacityFingerprint"); form["Adjustment.SchedulingNotBefore"]=Field(p.html,"Adjustment.SchedulingNotBefore");
        await Task.Delay(1200); // A real elapsed interval, not a frozen preview/confirmation clock.
        var applied=await Post("CapacityApply",form);
        Check(applied.response.StatusCode==HttpStatusCode.Redirect && applied.response.Headers.Location!.ToString().StartsWith("/supervision/carga?"),"confirmation after real elapsed time saves and returns to workload");
        db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==future.Id)).PersonId==fixture.OtherPerson,"future task redistributed to eligible coworker");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==active.Id)).PersonId==fixture.Person,"active unselected task remains with owner");
        Check(originalProtected==System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==protectedTask.Id || a.Id==completedTask.Id).OrderBy(a=>a.Id).ToArrayAsync()),"progress and completed historical snapshots unchanged");
        Check(await db.AuditEvents.AnyAsync(a=>a.Action=="CAPACITY_REDISTRIBUTED" && a.ActorUserId==fixture.Director),"capacity audit records actor and redistribution");
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        Check(ledger.DayCapacity(fixture.Person,day)==0 && ledger.DayCapacity(fixture.Person,day.AddDays(1))==240,"saved reduction expires and base capacity returns next day");
        html=await Get(applied.response.Headers.Location!.ToString());
        Check(html.Contains("Capacidad actualizada") && html.Contains("0 min disponibles"),"reopen displays persisted setting and result");
        var replay=await Post("CapacityApply",form);
        Check(replay.response.StatusCode==HttpStatusCode.Conflict && await db.OperationalCapacities.CountAsync(c=>c.PersonId==fixture.Person)==1,"repeated confirmation rejects stale proposal without duplicate setting");
        form["Adjustment.Percent"]="50"; form.Remove("CapacityFingerprint"); form.Remove("Adjustment.SchedulingNotBefore");
        p=await Post("CapacityPreview",form); Check(p.response.IsSuccessStatusCode,"second capacity proposal");
        form["CapacityFingerprint"]=Field(p.html,"CapacityFingerprint");form["Adjustment.SchedulingNotBefore"]=Field(p.html,"Adjustment.SchedulingNotBefore");
        // Another real database context represents a collaborator changing activity during review.
        await using(var concurrent = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options)) {
            var a=await concurrent.OperationalAssignments.SingleAsync(a=>a.Id==protectedTask.Id);a.ProgressUnits++;a.Revision++;await concurrent.SaveChangesAsync();
        }
        var stale=await Post("CapacityApply",form);
        Check(stale.response.StatusCode==HttpStatusCode.Conflict && WebUtility.HtmlDecode(stale.html).Contains("Calcula de nuevo"),"concurrent progress invalidates preview with actionable error");
        Check((await db.OperationalCapacities.AsNoTracking().SingleAsync(c=>c.PersonId==fixture.Person)).Percent==0,"concurrent rejection is atomic");
        form["Adjustment.DailyMinutes"]="999";
        var invalid=await Post("CapacityPreview",form);Check(invalid.response.StatusCode==HttpStatusCode.UnprocessableEntity,"server rejects invalid capacity");
        form["Adjustment.DailyMinutes"]="240";
        var noCsrf=new Dictionary<string,string>(form);noCsrf.Remove("__RequestVerificationToken");
        Check((await Post("CapacityApply",noCsrf)).response.StatusCode==HttpStatusCode.BadRequest,"antiforgery required");
        client.DefaultRequestHeaders.Add("X-QA-Worker","1");
        var directorToken=form["__RequestVerificationToken"]; form["__RequestVerificationToken"]=await Get("/qa-token");
        Check((await Post("CapacityPreview",form)).response.StatusCode==HttpStatusCode.Forbidden,"worker cannot change capacity");client.DefaultRequestHeaders.Remove("X-QA-Worker");form["__RequestVerificationToken"]=directorToken;
        // Existing individual transfer still binds independently after adding the capacity form.
        html=await Get($"/supervision/carga?task={active.Id}");
        var transfer=new Dictionary<string,string> { ["Range"]="day", ["Change.AssignmentId"]=active.Id.ToString(),["Change.Revision"]=Field(html,"Change.Revision"),["Change.RecipientId"]=fixture.OtherPerson.ToString(),["Change.RemainingMinutes"]="30",["Change.DueLocal"]=day.ToString("yyyy-MM-dd")+"T12:30",["Change.Reason"]="Transferencia individual tras ajuste",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken") };
        p=await Post("Preview",transfer);Check(p.response.IsSuccessStatusCode && p.html.Contains("Confirmar transferencia"),"individual transfer preview still works with independent fields");
        transfer["Fingerprint"]=Field(p.html,"Fingerprint");var transferred=await Post("Apply",transfer);
        Check(transferred.response.StatusCode==HttpStatusCode.Redirect && (await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==active.Id)).PersonId==fixture.OtherPerson,"individual transfer persists from same page");
        p=await Post("CapacityPreview",form);Check(p.response.IsSuccessStatusCode,"fresh proposal after activity change");
        form["CapacityFingerprint"]=Field(p.html,"CapacityFingerprint");form["Adjustment.SchedulingNotBefore"]=Field(p.html,"Adjustment.SchedulingNotBefore");clock.Advance=TimeSpan.FromDays(1);
        Check((await Post("CapacityApply",form)).response.StatusCode==HttpStatusCode.UnprocessableEntity,"day rollover rejects past adjustment without writing");clock.Advance=TimeSpan.Zero;
        Directory.CreateDirectory(".local/workload-20261104");
        await File.WriteAllTextAsync(".local/workload-20261104/fixture.json",System.Text.Json.JsonSerializer.Serialize(new {person=fixture.Person, other=fixture.OtherPerson, day, task=protectedTask.Id}));
        if(ui){Console.WriteLine("WORKLOAD_UI_READY https://localhost:7293/supervision/carga");await app.WaitForShutdownAsync();}else await app.StopAsync();
    }
}
