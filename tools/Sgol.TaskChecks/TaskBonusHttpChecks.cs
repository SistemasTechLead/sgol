using System.Net;
using System.Security.Claims;
using System.Text.Json;
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

internal static partial class TaskBonusChecks
{
    sealed class BonusSession(Guid user, Guid person, Clock clock, string role = CanonicalRole.Direction) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(user,person,"bonus-check","Cuenta de prueba","LOR-001",role,
            [WeekAuthorization.View,"PER-PERSONA-ADMIN","PER-BANDEJA-PROPIA","PER-SUPERVISION-VER"],clock.UtcNow,clock.UtcNow.AddHours(1),clock.UtcNow.AddHours(2)));
    }
    public static async Task Run(SgolDbContext source, bool ui)
    {
        var clock = new Clock();
        var options = new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options;
        await using var db = new SgolDbContext(options, bonusClock: clock);
        var fixture = await QaFixtures.CreateAsync(db);
        var directorPerson = await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        var tasks = Enumerable.Range(0, 6).Select(i => new OperationalAssignment {
            Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,TaskCode="BONO-"+i,Name="Tarea de bono " + i,
            PersonId=fixture.Person,PersonName="Colaboradora de prueba con nombre extenso",AssignedRole=CanonicalRole.SalesFloor,
            StartsAt=At(2,9),DueAt=At(2,12),Requirement="NINGUNO",OperationalCriticality=i==0?"FLEXIBLE":"IMPORTANTE",LoadMinutes=10,
            ScheduleMode="DISTRIBUIDA",Revision=1
        }).ToArray();
        db.AddRange(tasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Check(await db.Set<TaskBonusFact>().CountAsync(f=>tasks.Select(t=>t.Id).Contains(f.AssignmentId))==6,"B01/I: creación real captura una obligación por asignación");
        var original=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==fixture.Plan).OrderBy(a=>a.Id).ToArrayAsync());
        var service=CorrectionSchedulingChecks.Service(db,clock);
        var bonus=new TaskBonusService(db,clock,service);
        var first=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(first.EvaluablePoints==26 && first.ScorePercent==100 && !first.Closed,"B03/I: seis asignaciones futuras cuentan sin ser entregas");
        Check(original==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==fixture.Plan).OrderBy(a=>a.Id).ToArrayAsync()),"Consulta no altera fechas, cargas, responsables ni estados");
        try{await bonus.ReadAsync(fixture.Worker,Week,false,default);throw new Exception("Access expected");}catch(DirectionPlanException e){Check(e.Status==403,"B29: colaborador no obtiene resumen ajeno");}
        var personalBoard=await bonus.BoardAsync(fixture.Worker,Week,default);
        Check(personalBoard.Count==1 && personalBoard.ContainsKey(fixture.Person),"B29: Tablero del colaborador solo obtiene su bono, sin detalle");
        try { await bonus.ExcludeAsync(fixture.Director,Guid.NewGuid(),tasks[0].Id,Week,"Persona fuera del ámbito",default);throw new Exception("Access expected"); }
        catch(DirectionPlanException e) {Check(e.Status==403,"B29: excepción de Dirección rechaza persona fuera de su ámbito");}
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=Path.GetFullPath("src/Sgol.Web/wwwroot"),EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7296");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7296";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>();builder.Services.AddScoped<OperationalSupervisionService>();builder.Services.AddScoped<OperationalCapacityService>();builder.Services.AddScoped<OperationalCarryoverService>();builder.Services.AddScoped<OperationalCarryoverReader>();builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddScoped<TaskBonusService>();builder.Services.AddScoped<LiveBoardReader>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddSingleton<IFileTechnicalValidator,FileTechnicalValidator>();builder.Services.AddSingleton<IFileMalwareScanner,CheckScanner>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();builder.Services.AddScoped<OperationalProcessCatalogService>();builder.Services.AddScoped<OperationalSchedulingPolicyService>();builder.Services.AddScoped<AssignmentRefreshService>();
        builder.Services.AddScoped<IRazorSessionState>(sp=>{
            var context=sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            var worker=context.Request.Cookies["bonus-role"]=="worker" || context.Request.Path.StartsWithSegments("/mi-trabajo");
            return worker ? new BonusSession(fixture.Worker,fixture.Person,clock,CanonicalRole.SalesFloor) : new BonusSession(fixture.Director,directorPerson,clock);
        });
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{
            var worker=ctx.Request.Cookies["bonus-role"]=="worker" || ctx.Request.Path.StartsWithSegments("/mi-trabajo");
            ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,(worker?fixture.Worker:fixture.Director).ToString())],ctx.Request.Cookies["bonus-role"]=="screen"?Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.Scheme:"TEST_ONLY"));await next();
        });
        app.UseStaticFiles();app.MapRazorPages();
        // Only in this dedicated test executable, never registered by the product.
        app.MapPost("/__test/clock/{day:int}/{hour:int}",(int day,int hour)=>{clock.UtcNow=At(day,hour);return Results.Ok();});
        await app.StartAsync();
        using var handler=new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new()};using var client=new HttpClient(handler){BaseAddress=new("https://localhost:7296")};
        string Field(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var html=await client.GetStringAsync("/mi-trabajo?assignedTask="+tasks[0].Id);
        Check(html.Contains("data-task-bonus") && html.Contains("sábado 7"),"B22/I: Mi trabajo real muestra medidor y primer sábado aplicable");
        var response=await client.PostAsync("/mi-trabajo?handler=CompleteOperational",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=tasks[0].Id.ToString(),["conclusion"]="Entrega puntual real",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")}));
        Check(response.StatusCode==HttpStatusCode.Redirect,"B04/I: formulario real NINGUNO entrega sin requisito nuevo");
        db.ChangeTracker.Clear();var delivered=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==tasks[0].Id);
        Check(delivered.CompletionKind=="ENTREGA" && delivered.CompletedAt==clock.UtcNow,"Entrega conserva fecha, autor y estado operativo");
        var after=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(after.ScorePercent==first.ScorePercent && after.EvaluablePoints==26,"B16/B26: entrega puntual cambia hechos pero no simula subida");
        var json=await client.GetStringAsync("/mi-trabajo?handler=Bonus");Check(JsonDocument.Parse(json).RootElement[0].GetProperty("scorePercent").GetDecimal()==100,"Handler interno devuelve cálculo servidor");
        html=await client.GetStringAsync("/supervision/bono-tareas");
        Check(html.Contains("Porcentaje para el bono") && html.Contains("BONO-0") && html.Contains("Excluir del bono"),"Resumen y detalle real de Supervisión incluyen completadas");
        var snapshot=await client.GetAsync("/supervision/bono-tareas?handler=Snapshot");
        Check(snapshot.Headers.ETag?.Tag.Length==66,"B27: ETag de lote mantiene tamaño constante aunque crezca el equipo");
        using(var unchanged=new HttpRequestMessage(HttpMethod.Get,"/supervision/bono-tareas?handler=Snapshot")) {
            unchanged.Headers.IfNoneMatch.Add(snapshot.Headers.ETag!);
            Check((await client.SendAsync(unchanged)).StatusCode==HttpStatusCode.NotModified,"B27: lote sin cambios responde 304");
        }
        var noCsrf=await client.PostAsync("/supervision/bono-tareas?handler=Exclude",new FormUrlEncodedContent(new Dictionary<string,string>()));
        Check(noCsrf.StatusCode==HttpStatusCode.BadRequest,"B29: excepción exige antiforgery real");
        clock.UtcNow=At(3,10);
        var beforeExclusion=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(beforeExclusion.DeductedPoints==25,"B06/I: medianoche deriva descuentos sin cambiar asignaciones");
        response=await client.PostAsync("/supervision/bono-tareas?handler=Exclude",new FormUrlEncodedContent(new Dictionary<string,string>{["Week"]="2026-11-02",["Person"]=fixture.Person.ToString(),["Obligation"]=tasks[1].Id.ToString(),["Reason"]="Excepción autorizada de Dirección",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")}));
        Check(response.StatusCode==HttpStatusCode.Redirect,"B15/I: formulario Dirección excluye con motivo");
        var excluded=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(excluded.EvaluablePoints==21 && excluded.DeductedPoints==20 && await db.AuditEvents.AnyAsync(e=>e.Action=="TASK_BONUS_EXCLUDED" && e.ResourceId==tasks[1].Id),"Exclusión retira base y descuento con auditoría");
        await bonus.ExcludeAsync(fixture.Director,fixture.Person,tasks[1].Id,Week,"Excepción autorizada de Dirección",default);
        Check(await db.Set<TaskBonusExclusion>().CountAsync(e=>e.ObligationId==tasks[1].Id)==1,"B23/I: reintento no duplica exclusión");
        if(!ui)
        {
            clock.UtcNow=At(9,12);
            var final=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
            Check(final.Closed && final.ClosedAt==At(9,12),"B08/I: cierre persiste en corte exacto");
            clock.UtcNow=At(9,13);await service.ReviewAsync(fixture.Director,tasks[0].Id,delivered.Revision,"NO_SATISFECHA","Revisión tardía de prueba",default);
            db.ChangeTracker.Clear();var repeated=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
            Check(JsonSerializer.Serialize(final)==JsonSerializer.Serialize(repeated),"B09/B25: revisión posterior no cambia cierre persistido");
            try{await bonus.ExcludeAsync(fixture.Director,fixture.Person,tasks[2].Id,Week,"Intento después del cierre",default);throw new Exception("Expected closed");}catch(DirectionPlanException e){Check(e.Code=="BONUS_CLOSED","B15: excepción posterior rechazada");}
            await PersistenceChecks(db,clock,fixture);
            await LifecycleChecks(db,clock);
            await CoverageChecks(db,clock);
        }
        clock.UtcNow=At(2,10); // Board shows daily bars: test on the fixture assignment day.
        html=await client.GetStringAsync("/tablero");Check(html.Contains("data-task-bonus"),"B29: tablero personal integra medidor autorizado");
        handler.CookieContainer.Add(client.BaseAddress!,new Cookie("bonus-role","screen"));html=await client.GetStringAsync("/tablero");
        Check(!html.Contains("data-task-bonus") && !html.Contains("data-bonus-score") && !html.Contains("task-bonus.js"),"B29: HTML quiosco no recibe datos ni controlador de bono");
        handler.CookieContainer.Add(client.BaseAddress!,new Cookie("bonus-role","worker"));
        response=await client.GetAsync("/supervision/bono-tareas?handler=Snapshot");Check(response.StatusCode==HttpStatusCode.Forbidden,"B29: handler de Supervisión deniega colaborador");
        html=await client.GetStringAsync("/mi-trabajo");
        response=await client.PostAsync("/supervision/bono-tareas?handler=Exclude",new FormUrlEncodedContent(new Dictionary<string,string>{["Week"]="2026-11-02",["Person"]=fixture.Person.ToString(),["Obligation"]=tasks[0].Id.ToString(),["Reason"]="Intento sin permiso de Dirección",["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")}));
        Check(response.StatusCode==HttpStatusCode.Forbidden,"B29: POST con CSRF válido deniega excepción a colaborador");
        handler.CookieContainer.Add(client.BaseAddress!,new Cookie("bonus-role","director"));
        var dir=Path.GetFullPath(".local/bonus-20261102");Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"fixture.json"),JsonSerializer.Serialize(new{origin="https://localhost:7296",person=fixture.Person,worker=fixture.Worker,tasks=tasks.Select(t=>t.Id),week="2026-11-02"}));
        if(ui){Console.WriteLine("BONUS_UI_READY "+dir);await app.WaitForShutdownAsync();}else await app.StopAsync();
    }

    static async Task PersistenceChecks(SgolDbContext db,Clock clock,QaFixtures.Actors fixture)
    {
        clock.UtcNow=At(2,10);
        var a=new OperationalAssignment{Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,PersonId=fixture.OtherPerson,PersonName="Otra",AssignedRole=CanonicalRole.SalesFloor,
            TaskCode="BONO-OFF",Name="Cierre tras apagado",StartsAt=At(8,9),DueAt=At(8,15),Requirement="NINGUNO",OperationalCriticality="IMPORTANTE",LoadMinutes=10};
        db.Add(a);await db.SaveChangesAsync();
        clock.UtcNow=At(8,14);a.CompletedAt=clock.UtcNow;a.CompletedBy=fixture.Other;a.CompletionKind="ENTREGA";a.ReviewStatus="PENDIENTE_REVISION";a.Revision++;await db.SaveChangesAsync();
        clock.UtcNow=At(9,13);a.ReviewedAt=clock.UtcNow;a.ReviewedBy=fixture.Director;a.ReviewStatus="NO_SATISFECHA";a.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var service=CorrectionSchedulingChecks.Service(db,clock);var bonus=new TaskBonusService(db,clock,service);
        var closure=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single();
        Check(closure.ScorePercent==100 && closure.Closed,"B25/I: sin cierre previo reconstruye al corte e ignora revisión tardía");
        Check(await db.Set<TaskBonusClosure>().CountAsync(c=>c.PersonId==fixture.OtherPerson&&c.Week==Week)==1,"B23/I: cierre único persistido");
    }
}
