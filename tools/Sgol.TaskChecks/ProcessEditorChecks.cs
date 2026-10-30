using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
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

public static class ProcessEditorChecks
{
    public static async Task CurrentAsync(SgolDbContext db)
    {
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.ValidTo == null).Select(r => r.UserId).FirstAsync();
        var root = await db.Set<OperationalSequence>().SingleAsync(p => p.Id == Guid.Parse("01a15594-eb6b-7217-b82c-8e195ca2d0a5"));
        var before = System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync());
        var requirements = await db.OperationalTasks.AsNoTracking().Where(t => t.ProcessId == root.Id).OrderBy(t => t.Id).Select(t => new {t.Id,t.Requirement}).ToArrayAsync();
        var clock = new Clock();
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var service = new OperationalProcessCatalogService(db,direction,clock,new ConfigurationBuilder().Build());
        var editor = await service.ReadAsync(actor,root.Id,default);
        foreach (var step in editor.Steps) { step.Action = step.Position == editor.Steps.Length ? "AUTORIZAR" : "ENTREGAR"; step.ReleaseCondition = step.Position == 1 ? "INICIO" : "ENTREGA"; }
        await service.SaveAsync(actor,new() { Id=root.Id, Revision=root.Revision, Name=root.Name,Description=root.Description,Steps=editor.Steps.ToList() },default);
        db.ChangeTracker.Clear();editor=await service.ReadAsync(actor,root.Id,default);
        await service.PublishAsync(actor,root.Id,editor.Process.Revision,default);db.ChangeTracker.Clear();
        Check((await service.ReadAsync(actor,root.Id,default)).Process.PublishedVersionId!=null,"Restored copy of Reporte de incidencias publishes all eight original tasks");
        Check(System.Text.Json.JsonSerializer.Serialize(requirements)==System.Text.Json.JsonSerializer.Serialize(await db.OperationalTasks.AsNoTracking().Where(t=>t.ProcessId==root.Id).OrderBy(t=>t.Id).Select(t=>new{t.Id,t.Requirement}).ToArrayAsync()),"All original evidence requirements remain identical");
        Check(before==System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Every original assignment remains identical after saving and publishing the restored copy");
    }
    public static async Task RunAsync(SgolDbContext db, bool checklistOnly = false, bool processRefreshOnly = false, bool itemsOnly = false, bool itemsMore = false, bool itemsIntegrity = false, bool itemsUiCurrent = false, bool processSearchOnly = false)
    {
        if (!itemsMore && await db.People.AnyAsync()) throw new Exception("Requires an empty dedicated database");
        var actor = Guid.CreateVersion7(); var person = Guid.CreateVersion7(); var now = new Clock().UtcNow;
        if (itemsMore) {
            person=await db.People.Where(p=>p.StableCode=="EDITOR-TEST").Select(p=>p.Id).SingleAsync();
            actor=await db.AppUsers.Where(u=>u.PersonId==person).Select(u=>u.Id).SingleAsync();
        } else {
        db.People.Add(new() { Id=person, DisplayName="Process editor test", StableCode="EDITOR-TEST", CreatedAt=now.AddYears(-1) });
        db.AppUsers.Add(new() { Id=actor, PersonId=person, Status=AccountStatus.Active, SecurityStamp="isolated" });
        db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,Sgol.Organization.Contracts.BranchScope.LorettaId,Sgol.Organization.Contracts.EmploymentStatus.Active,now.AddYears(-1)));
        db.RoleAssignmentVersions.Add(new() { Id=Guid.CreateVersion7(), UserId=actor, BranchId=Sgol.Organization.Contracts.BranchScope.LorettaId, RoleCode=CanonicalRole.Direction, Status=RoleAssignmentStatus.Active, ValidFrom=now.AddYears(-1) });
        await db.SaveChangesAsync();
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found.");
        var webRoot = Path.Combine(root.FullName, "src", "Sgol.Web", "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName,
            WebRootPath = webRoot, EnvironmentName = "Development" });
        builder.WebHost.UseUrls("https://localhost:7259", "http://127.0.0.1:7258");
        builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7259";
        builder.Configuration["Processes:OperationsEnabled"] = "true";
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages(o => o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter))))
            .AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>();
        builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
        builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        var clock = new Clock();
        if(itemsUiCurrent) clock.UtcNow=await db.Set<OperationalSequenceEvent>().MaxAsync(e=>e.OccurredAt);
        builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>();
        builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>(); builder.Services.AddScoped<OperationalProcessCatalogService>();
        builder.Services.AddScoped<OperationalCarryoverReader>();
        builder.Services.AddScoped<AssignmentRefreshService>();
        builder.Services.AddScoped<OperationalSchedulingPolicyService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();
        builder.Services.AddScoped<ProcessPlanningService>(); builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddSingleton<IFileTechnicalValidator, FileTechnicalValidator>(); builder.Services.AddSingleton<IFileMalwareScanner, Scanner>();
        builder.Services.AddSingleton<IRazorSessionState>(new Session(actor, await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync()));
        await using var app = builder.Build();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "TEST_ONLY")); await next(); });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages();
        await app.StartAsync();
        using var handler = new HttpClientHandler { AllowAutoRedirect=false, CookieContainer=new() };
        using var client = new HttpClient(handler) { BaseAddress=new("https://localhost:7259") };
        if (itemsOnly || itemsMore)
        {
            if(itemsUiCurrent) { Console.WriteLine("ITEMS_EXISTING_UI_READY https://localhost:7259/mi-trabajo/procesos");await app.WaitForShutdownAsync();return; }
            if (processSearchOnly) await ProcessSearchChecks.RunAsync(db,client);
            else if (itemsIntegrity) await ProcessItemIntegrityChecks.RunAsync(db, actor, person, clock, client, value=>clock.UtcNow=value);
            else if (itemsMore) await ProcessItemChecks.CapacityAsync(db, actor, person, clock, client, value=>clock.UtcNow=value);
            else await ProcessItemChecks.RunAsync(db, actor, person, clock, client);
            if (Environment.GetEnvironmentVariable("SGOL_ITEMS_UI_PREVIEW") == "1") {
                Console.WriteLine("Items real Razor preview: https://localhost:7259/tareas/procesos/items");
                await app.WaitForShutdownAsync();
            }
            await app.StopAsync(); return;
        }
        if (processRefreshOnly)
        {
            await ProcessAssignmentRefreshChecks.RunAsync(db, actor, person, clock, client);
            if (Environment.GetEnvironmentVariable("SGOL_REFRESH_UI_PREVIEW") == "1")
            {
                Console.WriteLine("Process refresh real Razor preview: https://localhost:7259/tareas/actualizar");
                await app.WaitForShutdownAsync();
            }
            await app.StopAsync();
            return;
        }
        if (checklistOnly)
        {
            await IndependentChecklist(db,actor,person,clock,client);
            await app.StopAsync();
            return;
        }
        var page = await client.GetStringAsync("/tareas/procesos?create=true");
        var created = await client.PostAsync("/tareas/procesos?handler=Create", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=Hidden(page,"__RequestVerificationToken"), ["CreateKey"]=Hidden(page,"CreateKey"), ["Input.Name"]="Auditoría de inventario prueba", ["Input.Description"]="" }));
        Check(created.StatusCode==HttpStatusCode.Redirect,"Create draft accepts an empty optional description over HTTP");
        var location=created.Headers.Location!.OriginalString;
        var id=Guid.Parse(location.Split("id=")[1]);
        var version=await db.Set<OperationalSequenceVersion>().SingleAsync(v=>v.SequenceId==id);
        var requirements = new[] { "EXCEL", "COMENTARIO", "FOTOGRAFIA", "CAPTURA_PANTALLA", "DOCUMENTO", "NINGUNO", "CHECKLIST" };
        db.OperationalCapacities.Add(new() { Id=Guid.CreateVersion7(),PersonId=person,From=new(2026,10,26),Through=new(2026,11,26),DailyMinutes=480,Reason="Isolated process test" });
        db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=new(2026,10,26),ChangedBy=actor,ChangedAt=now,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)})) });
        for(var i=1;i<=7;i++) {
            var task=new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=$"TAR-{9000+i}", Name=$"Inventory step {i}", ProcessId=id, Requirement=requirements[i-1], AssignedRole=CanonicalRole.Direction,LoadMinutes=1, OperationalCriticality="FLEXIBLE" };
            if(task.Requirement=="CHECKLIST") task.ChecklistJson=System.Text.Json.JsonSerializer.Serialize(new[]{new ChecklistPoint(Guid.NewGuid(),"Comprobar resultado"),new ChecklistPoint(Guid.NewGuid(),"Confirmar orden")});
            db.Add(task); db.Add(new OperationalSequenceStep { VersionId=version.Id,TaskId=task.Id,Position=i,DeadlineHours=1,ReleaseCondition=i==1?"INICIO":"ENTREGA" });
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        page=await client.GetStringAsync(location);
        var fields=new Dictionary<string,string> { ["__RequestVerificationToken"]=Hidden(page,"__RequestVerificationToken"), ["Input.Id"]=id.ToString(), ["Input.Revision"]=Hidden(page,"Input.Revision"),["Input.Name"]="Auditoría de inventario prueba",["Input.Description"]="",["Input.StartKind"]="OCASIONAL",["Input.StartHour"]="12" };
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==version.Id).OrderBy(s=>s.Position).ToArrayAsync();
        for(var i=0;i<7;i++) {
            var prefix=$"Input.Steps[{i}].";
            fields[prefix+"TaskId"]=steps[i].TaskId.ToString();fields[prefix+"Position"]=(i+1).ToString();
            fields[prefix+"Action"]=i==6?"AUTORIZAR":i==1?"REVISAR":"ENTREGAR";
            fields[prefix+"ReleaseCondition"]=i==0?"INICIO":i==2?"APROBACION":"ENTREGA";
            fields[prefix+"DeadlineKind"]="RELATIVO";fields[prefix+"DeadlineHours"]="1";
            fields[prefix+"Instructions"]="";fields[prefix+"MinimumStartOffsetHours"]="";fields[prefix+"ReturnToPosition"]=i==6?"6":"";
        }
        var saved=await client.PostAsync("/tareas/procesos?handler=Save",new FormUrlEncodedContent(fields));
        var result=await saved.Content.ReadAsStringAsync();
        Check(saved.StatusCode==HttpStatusCode.Redirect,"Seven-stage draft saves over HTTP with empty instructions and description: "+(saved.IsSuccessStatusCode?"":result.Substring(0,Math.Min(400,result.Length))));
        db.ChangeTracker.Clear();
        Check(await db.Set<OperationalSequenceStep>().CountAsync(s=>s.VersionId==version.Id&&s.Instructions=="")==7,"All seven blank instructions persist as empty text");
        page=await client.GetStringAsync(location);
        var published=await client.PostAsync("/tareas/procesos?handler=Publish",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=Hidden(page,"__RequestVerificationToken"),["Input.Id"]=id.ToString(),["Input.Revision"]=Hidden(page,"Input.Revision")}));
        Check(published.StatusCode==HttpStatusCode.Redirect,"Seven-stage draft with delivery, review and acceptance publishes over HTTP");
        Check(!await db.OperationalAssignments.AnyAsync(),"Draft save and publication create no assignments");
        await MixedEvidence(db, actor, person, id, clock, builder.Configuration, client, Hidden(page,"__RequestVerificationToken"));
        await IndependentChecklist(db,actor,person,clock,client);
        await ProcessCatalogDeletionChecks.RunAsync(db, actor, person, clock, value => clock.UtcNow = value, client, builder.Configuration, new Scanner());
        if (Environment.GetEnvironmentVariable("SGOL_DELETION_UI_PREVIEW") == "1") await app.WaitForShutdownAsync();
        await app.StopAsync();
    }
    private static async Task IndependentChecklist(SgolDbContext db,Guid actor,Guid person,Clock clock,HttpClient client)
    {
        var title=new ChecklistPoint(Guid.NewGuid(),"Caja y operación",true);
        var points=new[]{new ChecklistPoint(Guid.NewGuid(),"Abrir área"),new ChecklistPoint(Guid.NewGuid(),"Confirmar limpieza")};
        var json=System.Text.Json.JsonSerializer.Serialize(new[]{title,points[0],points[1]});
        var page=await client.GetStringAsync("/tareas?create=true");
        Check(page.Contains("data-checklist-editor") && page.Contains("CHECKLIST"),"Task editor exposes checklist requirement and parameters");
        var response=await client.PostAsync("/tareas?handler=Save",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=Hidden(page,"__RequestVerificationToken"),["Input.Name"]="Checklist HTTP independiente",
            ["Input.Revision"]="0",["Input.ProcessId"]=OperationalSequence.GeneralId.ToString(),["Input.Requirement"]="CHECKLIST",
            ["Input.ChecklistJson"]=json,["Input.AssignedRole"]=CanonicalRole.Direction,["Input.OperationalCriticality"]="FLEXIBLE",["Input.Active"]="true" }));
        Check(response.StatusCode==HttpStatusCode.Redirect,"Task editor saves checklist through the actual HTTP handler: "+(response.StatusCode==HttpStatusCode.Redirect?"":await response.Content.ReadAsStringAsync()));
        db.ChangeTracker.Clear();var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Checklist HTTP independiente");
        Check(task.ChecklistJson==json,"Actual task form persists optional titles and checkboxes in order");
        var day=WeekContract.LocalToday(clock.UtcNow);
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),Sgol.Organization.Contracts.BranchScope.LorettaId,actor,clock.UtcNow,"Checklist HTTP",day,day);
        var assignment=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=task.Id,TaskCode=task.TaskCode,Name=task.Name,Requirement="CHECKLIST",
            ChecklistJson=task.ChecklistJson,PersonId=person,PersonName="Dirección prueba",AssignedRole=CanonicalRole.Direction,LoadMinutes=1,StartsAt=clock.UtcNow.AddMinutes(-1),DueAt=clock.UtcNow.AddHours(1)};
        db.AddRange(plan,assignment);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        page=await client.GetStringAsync("/mi-trabajo?assignedTask="+assignment.Id);
        var token=Hidden(page,"__RequestVerificationToken");
        var answer=System.Text.Json.JsonSerializer.Serialize(new ChecklistAnswer(OperationalChecklist.Fingerprint(task.ChecklistJson),[points[0].Id]));
        response=await client.PostAsync("/mi-trabajo?handler=ChecklistDraft",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=token,["id"]=assignment.Id.ToString(),["answer"]=answer }));
        Check(response.StatusCode==HttpStatusCode.OK,"Actual draft HTTP handler saves selected marks");
        page=await client.GetStringAsync("/mi-trabajo?assignedTask="+assignment.Id);
        Check(page.Contains("1 de 2 completados") && page.Contains("<h3 class=\"checklist-title\">Caja y operaci"),"Actual task reload restores marks and displays title outside checkbox count");
        response=await client.PostAsync("/mi-trabajo?handler=CompleteOperational",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=token,["id"]=assignment.Id.ToString(),["checklistAnswer"]=answer }));
        Check((int)response.StatusCode==422,"Actual delivery handler rejects an incomplete checklist");
        answer=System.Text.Json.JsonSerializer.Serialize(new ChecklistAnswer(OperationalChecklist.Fingerprint(task.ChecklistJson),points.Select(p=>p.Id).ToArray()));
        response=await client.PostAsync("/mi-trabajo?handler=CompleteOperational",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=token,["id"]=assignment.Id.ToString(),["checklistAnswer"]=answer }));
        Check(response.StatusCode==HttpStatusCode.Redirect,"Actual delivery HTTP handler accepts all checklist points");
        page=await client.GetStringAsync("/mi-trabajo?assignedTask="+assignment.Id);
        Check(page.Contains("2 de 2 completados") && page.Contains("data-readonly=\"true\""),"Submitted checklist renders as read-only evidence");
        if (Environment.GetEnvironmentVariable("SGOL_CHECKLIST_BROWSER_CHECK") is { Length: > 0 } script)
        {
            var start = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute=false };
            start.ArgumentList.Add(script); start.ArgumentList.Add(task.Id.ToString()); start.ArgumentList.Add(assignment.Id.ToString());
            using var browser = System.Diagnostics.Process.Start(start)!;
            await browser.WaitForExitAsync();
            Check(browser.ExitCode==0,"Checklist browser checks pass against the actual isolated Razor application");
        }
    }
    private static async Task MixedEvidence(SgolDbContext db, Guid actor, Guid person, Guid processId, Clock clock, IConfiguration config, HttpClient client, string csrf)
    {
        db.ChangeTracker.Clear();
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var catalog=new OperationalProcessCatalogService(db,direction,clock,config);
        var tasks=new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new Scanner());
        var service=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new Scanner());
        var editor=await catalog.ReadAsync(actor,processId,default);
        await catalog.SetEnabledAsync(actor,processId,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
        var planning=new ProcessPlanningService(db,direction,clock,new FileTechnicalValidator(),new Scanner());
        var day=WeekContract.LocalToday(clock.UtcNow);
        var input=new ProcessPlanInput { ProcessId=processId,Name="Mixed evidence isolated",Mode="OCASION",OccasionTime=new(12,0),From=day,Through=day,People=Enumerable.Range(1,7).ToDictionary(i=>i,i=>person) };
        input.Fingerprint=(await planning.PreviewAsync(actor,input,default)).Fingerprint;
        var plan=await planning.PrepareAsync(actor,input,default);db.ChangeTracker.Clear();
        var run=(await planning.ActivateAsync(actor,plan,default)).Single();db.ChangeTracker.Clear();
        Check((await service.ReadAsync(actor,run,default)).Run.UsesTaskEvidence,"Activation captures evidence mode without changing task requirements");
        IFormFile FormFile(byte[] content,string name,string media)=>new FormFile(new MemoryStream(content),0,content.Length,"Document",name){Headers=new HeaderDictionary(),ContentType=media};
        byte[] Workbook() {
            using var stream=new MemoryStream();
            using(var zip=new System.IO.Compression.ZipArchive(stream,System.IO.Compression.ZipArchiveMode.Create,true)) {
                var parts=new Dictionary<string,string>{
                    ["[Content_Types].xml"]="<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>",
                    ["xl/workbook.xml"]="<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Entrega' sheetId='1' r:id='rId1'/></sheets></workbook>",
                    ["xl/_rels/workbook.xml.rels"]="<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>",
                    ["xl/worksheets/sheet1.xml"]="<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData/></worksheet>"};
                foreach(var part in parts){using var writer=new StreamWriter(zip.CreateEntry(part.Key).Open());writer.Write(part.Value);}
            }
            return stream.ToArray();
        }
        using var bitmap=new SkiaSharp.SKBitmap(16,16);using var image=SkiaSharp.SKImage.FromBitmap(bitmap);using var jpeg=image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg,90);
        var photoBytes=jpeg.ToArray();var excelBytes=Workbook();
        using var pdfStream=new MemoryStream();
        using (var pdf=SkiaSharp.SKDocument.CreatePdf(pdfStream)) { pdf.BeginPage(100,100);pdf.EndPage();pdf.Close(); }
        var pdfBytes=pdfStream.ToArray();
        var delivered=new List<Guid>();
        var returned=false;
        for(var position=1;position<=7;position++) {
            var view=await service.ReadAsync(actor,run,default);var stage=view.Stages.Single(s=>s.Position==position);
            if(stage.Status!="DISPONIBLE") { clock.UtcNow=new[]{clock.UtcNow,stage.PlannedStartsAt}.Max();await service.TickAsync(default);db.ChangeTracker.Clear();view=await service.ReadAsync(actor,run,default);stage=view.Stages.Single(s=>s.Position==position); }
            Check(stage.Status=="DISPONIBLE",$"Stage {position} is actionable only after its predecessor");
            var attempt=view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId);var assignment=attempt.AssignmentId!.Value;
            if(position==7 && !returned) {
                await service.DecideAsync(actor,run,attempt.Id,attempt.Revision,Guid.CreateVersion7(),"DEVOLUCION","Revisar la etapa anterior",null,null,true,default);db.ChangeTracker.Clear();
                var reopened=await service.ReadAsync(actor,run,default);
                Check(reopened.Run.Round==2 && reopened.Run.ActivePosition==6 && reopened.Attempts.Any(a=>a.Id==attempt.Id&&a.Status=="SUSTITUIDO"),"Return creates a new round and retains superseded attempts");
                try { await service.DecideAsync(actor,run,attempt.Id,attempt.Revision,Guid.CreateVersion7(),"SIN_DIFERENCIAS","Stale",null,null,true,default);throw new Exception("Superseded attempt accepted"); }
                catch(DirectionPlanException e) when(e.Code=="PROCESS_CONFLICT") { Console.WriteLine("PASS: Superseded attempt cannot close the process"); }
                db.ChangeTracker.Clear();returned=true;position=5;continue;
            }
            var decision=position==7?"SIN_DIFERENCIAS":position==2?"APROBACION":"ENTREGA";
            IFormFile? file=position==1?FormFile(excelBytes,"evidence.xlsx",OperationalExcel.MediaType):position==5?FormFile(pdfBytes,"evidence.pdf","application/pdf"):null;
            Guid? capture=null;Guid[]? photos=null;IFormFile[]? screenshots=position==4?[FormFile(photoBytes,"screen.jpg","image/jpeg")]:null;
            if(position<=5 && position!=2 || position==7) {
                try { await service.DecideAsync(actor,run,attempt.Id,attempt.Revision,Guid.CreateVersion7(),decision,"Evidence check",null,null,true,default);throw new Exception("Missing evidence accepted"); }
                catch(DirectionPlanException e) when(e.Code is "TASK_FILE_REQUIRED" or "TASK_CAMERA_REQUIRED" or "TASK_PHOTO_LIMIT" or "TASK_CHECKLIST_CHANGED") { Console.WriteLine("PASS: Missing evidence rejected for "+stage.Requirement); }
                db.ChangeTracker.Clear();Check((await service.ReadAsync(actor,run,default)).Run.ActivePosition==position,"Rejected evidence does not advance or complete a stage");
            }
            if(position==3) {
                var session=await tasks.BeginCaptureAsync(actor,assignment,default);capture=session.Id;var photoId=Guid.CreateVersion7();
                await tasks.UploadPhotographAsync(actor,assignment,session.Id,photoId,FormFile(photoBytes,"camera.jpg","image/jpeg"),default);db.ChangeTracker.Clear();photos=[photoId];
            }
            var checklistAnswer=stage.Requirement=="CHECKLIST" ? System.Text.Json.JsonSerializer.Serialize(new ChecklistAnswer(OperationalChecklist.Fingerprint(stage.ChecklistJson),OperationalChecklist.Points(stage.ChecklistJson).Select(p=>p.Id).ToArray())) : null;
            if(checklistAnswer!=null) { var rendered=await client.GetStringAsync("/mi-trabajo?assignedTask="+assignment); Check(rendered.Contains("data-checklist-answer") && rendered.Contains("Comprobar resultado"),"Actual task panel renders checklist points"); }
            var key=Guid.CreateVersion7();
            using var form=new MultipartFormDataContent();
            foreach(var pair in new Dictionary<string,string> { ["__RequestVerificationToken"]=csrf,["id"]=assignment.ToString(),["attemptId"]=attempt.Id.ToString(),["revision"]=attempt.Revision.ToString(),["key"]=key.ToString(),["decision"]=decision,["conclusion"]="Evidence check",["declared"]="true" }) form.Add(new StringContent(pair.Value),pair.Key);
            if(checklistAnswer!=null) form.Add(new StringContent(checklistAnswer),"checklistAnswer");
            if(capture!=null) form.Add(new StringContent(capture.Value.ToString()),"captureSessionId");
            if(photos!=null) form.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(photos)),"photographIds");
            async Task AddFile(IFormFile incoming,string name) { using var stream=new MemoryStream();await incoming.CopyToAsync(stream);var content=new ByteArrayContent(stream.ToArray());content.Headers.ContentType=new(incoming.ContentType);form.Add(content,name,incoming.FileName); }
            if(file!=null) await AddFile(file,"Document");if(screenshots!=null) foreach(var screenshot in screenshots) await AddFile(screenshot,"screenshots");
            var response=await client.PostAsync("/mi-trabajo?handler=CompleteProcess",form);
            Check(response.StatusCode==HttpStatusCode.Redirect,$"Actual Mi trabajo HTTP form accepts {stage.Requirement}: "+(response.StatusCode==HttpStatusCode.Redirect?"":await response.Content.ReadAsStringAsync()));
            db.ChangeTracker.Clear();
            await service.DecideAsync(actor,run,attempt.Id,attempt.Revision,key,decision,"Evidence check",null,file,true,default,capture,photos,screenshots,checklistAnswer);db.ChangeTracker.Clear();
            delivered.Add(assignment);
            Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignment)).CompletedAt!=null,$"{stage.Requirement} evidence is persisted and delivery retry is idempotent");
        }
        var closed=await service.ReadAsync(actor,run,default);
        Check(closed.Run.Status=="CONCLUIDA"&&closed.Run.AuthorizedDocumentId==null,"Mixed process closes without inventing a final PDF");
        Check(await db.OperationalAssignments.CountAsync(a=>a.PlanId==plan&&a.ReviewStatus=="CONCLUIDA")==7,"All seven stages keep their completed review history");
        Check((await tasks.FileAsync(actor,delivered[0],default)).FileContent!.SequenceEqual(excelBytes),"Excel bytes remain available after closure");
        Check((await tasks.PhotographsAsync(actor,delivered[2],default)).Count==1,"Camera evidence remains attached after closure");
        Check((await tasks.PhotographsAsync(actor,delivered[3],default)).Count==1,"Screenshot evidence remains attached after closure");
        var legacyId=Guid.CreateVersion7();await catalog.CreateAsync(actor,legacyId,"Legacy PDF regression","",default);db.ChangeTracker.Clear();
        for(var i=0;i<2;i++) {
            await tasks.SaveAsync(actor,new OperationalTask { Revision=0,Name="PDF regression "+i,ProcessId=legacyId,AssignedRole=CanonicalRole.Direction,Requirement="DOCUMENTO",OperationalCriticality="FLEXIBLE",LoadMinutes=1 },default);db.ChangeTracker.Clear();
        }
        var legacyEditor=await catalog.ReadAsync(actor,legacyId,default);
        var legacyTasks=await db.OperationalTasks.AsNoTracking().Where(t=>t.ProcessId==legacyId).OrderBy(t=>t.Name).ToArrayAsync();
        var legacySteps=legacyTasks.Select((t,i)=>new OperationalSequenceStep { TaskId=t.Id,Position=i+1,Action=i==0?"ENTREGAR":"AUTORIZAR",ReleaseCondition=i==0?"INICIO":"ENTREGA",DeadlineHours=1 }).ToList();
        await catalog.SaveAsync(actor,new() { Id=legacyId,Revision=legacyEditor.Process.Revision,Name=legacyEditor.Process.Name,StartKind="OCASIONAL",StartHour=12,Steps=legacySteps },default);db.ChangeTracker.Clear();legacyEditor=await catalog.ReadAsync(actor,legacyId,default);
        await catalog.PublishAsync(actor,legacyId,legacyEditor.Process.Revision,default);db.ChangeTracker.Clear();legacyEditor=await catalog.ReadAsync(actor,legacyId,default);
        await catalog.SetEnabledAsync(actor,legacyId,legacyEditor.Process.Revision,true,default);db.ChangeTracker.Clear();
        var legacyInput=new ProcessPlanInput { ProcessId=legacyId,Name="Legacy PDF check",Mode="OCASION",OccasionTime=new(12,0),From=day,Through=day,People=new() { [1]=person,[2]=person } };
        legacyInput.Fingerprint=(await planning.PreviewAsync(actor,legacyInput,default)).Fingerprint;
        var legacyPlan=await planning.PrepareAsync(actor,legacyInput,default);db.ChangeTracker.Clear();
        var legacyRun=(await planning.ActivateAsync(actor,legacyPlan,default)).Single();db.ChangeTracker.Clear();
        for(var position=1;position<=2;position++) {
            var view=await service.ReadAsync(actor,legacyRun,default);var stage=view.Stages.Single(s=>s.Position==position);
            if(stage.Status!="DISPONIBLE") { clock.UtcNow=new[]{clock.UtcNow,stage.PlannedStartsAt}.Max();await service.TickAsync(default);db.ChangeTracker.Clear();view=await service.ReadAsync(actor,legacyRun,default);stage=view.Stages.Single(s=>s.Position==position); }
            var attempt=view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId);
            await service.DecideAsync(actor,legacyRun,attempt.Id,attempt.Revision,Guid.CreateVersion7(),position==1?"ENTREGA":"SIN_DIFERENCIAS","PDF regression",null,position==1?FormFile(pdfBytes,"legacy.pdf","application/pdf"):null,true,default);db.ChangeTracker.Clear();
        }
        var legacyClosed=await service.ReadAsync(actor,legacyRun,default);
        Check(!legacyClosed.Run.UsesTaskEvidence&&legacyClosed.Run.Status=="CONCLUIDA"&&legacyClosed.Run.AuthorizedDocumentId!=null&&legacyClosed.Documents.Length==1,"Existing PDF workflow still inherits and authorizes the exact document");
    }
    private static string Hidden(string body,string name)=>WebUtility.HtmlDecode(Regex.Match(body,$"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    private sealed class Clock:IClock{public DateTimeOffset UtcNow { get; set; }=new(2026,10,27,18,0,0,TimeSpan.Zero);}
    private sealed class Scanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
    private sealed class Session(Guid user,Guid person):IRazorSessionState{
        public bool IsInvalid=>false; public void Invalidate(){}
        public Task<SessionSnapshot?> GetAsync(CancellationToken token=default)=>Task.FromResult<SessionSnapshot?>(new(user,person,"test","Dirección prueba","LOR-001",CanonicalRole.Direction,[WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}
