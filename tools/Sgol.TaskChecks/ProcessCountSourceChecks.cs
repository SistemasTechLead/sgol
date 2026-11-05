using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessCountSourceChecks
{
    public static async Task LegacyAsync(SgolDbContext db, Guid actor, IClock clock, HttpClient client, Action<DateTimeOffset> setTime)
    {
        var start=await db.Set<PreparedProcessStart>().AsNoTracking().Where(s=>s.Status=="PREPARADO").FirstAsync();
        setTime(start.StartsAt);
        var page=await client.GetStringAsync($"/planificacion/procesos?planId={start.PlanId}");
        var response=await client.PostAsync("/planificacion/procesos?handler=Activate",new FormUrlEncodedContent(new Dictionary<string,string>{["planId"]=start.PlanId.ToString(),["__RequestVerificationToken"]=Value(page,"__RequestVerificationToken")}));
        Check(response.StatusCode==HttpStatusCode.Redirect,"Historical prepared plan activates through actual form");db.ChangeTracker.Clear();
        var run=await db.Set<OperationalSequenceRun>().AsNoTracking().SingleAsync(r=>r.PlanId==start.PlanId);
        Check(run.CountSemanticsVersion==0,"Historical prepared plan starts with historical semantics");
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var engine=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new Scanner());
        for(var n=0;n<5;n++){var active=await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s=>s.RunId==run.Id&&s.Position==1);if(active.Status!="PROGRAMADA")break;setTime(active.PlannedStartsAt);await engine.TickAsync(default);db.ChangeTracker.Clear();}
        var view=await engine.ReadAsync(actor,run.Id,default);var stage=view.Stages[0];var attempt=view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId);
        page=await client.GetStringAsync($"/mi-trabajo/procesos?id={run.Id}");
        var fields=new Dictionary<string,string>{["RunId"]=run.Id.ToString(),["AttemptId"]=attempt.Id.ToString(),["Revision"]=attempt.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["Decision"]="ENTREGA",["Conclusion"]="Historical zero semantics",["Declared"]="true",["ItemQuantity"]="0",["__RequestVerificationToken"]=Value(page,"__RequestVerificationToken")};
        response=await client.PostAsync("/mi-trabajo/procesos?handler=Decide",new FormUrlEncodedContent(fields));Check(response.StatusCode==HttpStatusCode.Redirect,"Historical count delivery succeeds after migration");db.ChangeTracker.Clear();
        view=await engine.ReadAsync(actor,run.Id,default);Check(view.Run.ActivePosition==4&&view.Stages[1].Status=="OMITIDA"&&view.Stages[2].Status=="OMITIDA"&&view.Stages[3].LoadMinutes==1,"Historical zero still skips the whole intermediate segment and retains audit load");
        response=await client.PostAsync("/mi-trabajo/procesos?handler=Decide",new FormUrlEncodedContent(fields));Check(response.StatusCode==HttpStatusCode.Redirect,"Historical delivery retry remains idempotent");
    }
    public static async Task MigrationAsync(SgolDbContext db)
    {
        var types=new[]{typeof(OperationalSequenceVersion),typeof(OperationalSequenceStep),typeof(PreparedProcessStart),typeof(OperationalSequenceRun),typeof(OperationalSequenceStage),typeof(OperationalSequenceAttempt),typeof(OperationalAssignment),typeof(OperationalAttachment)};
        async Task<string[]> Snapshot() {
            await db.Database.OpenConnectionAsync();var values=new List<string>();
            foreach(var type in types) {
                var table=db.Model.FindEntityType(type)!.GetTableName()!;
                using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText=$"SELECT md5(COALESCE(string_agg(j::text, '' ORDER BY j::text), '')) FROM (SELECT to_jsonb(t) - 'CountSourcesJson' - 'CountSemanticsVersion' - 'InputCountSourceTaskId' AS j FROM \"{table}\" t) rows";
                values.Add((string)(await command.ExecuteScalarAsync())!);
            }
            await db.Database.CloseConnectionAsync();return values.ToArray();
        }
        var before=await Snapshot();await db.Database.MigrateAsync();Check(Enumerable.SequenceEqual(before, await Snapshot()),"Migration preserves all historical definition, plan, execution, assignment and evidence values");
        Check(!await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.CountSemanticsVersion!=0),"Existing runs retain legacy semantics");
        Check(!await db.Set<OperationalSequenceStage>().AnyAsync(s=>s.InputCountSourceTaskId!=null),"Migration does not reinterpret historical source links");
        await db.Database.MigrateAsync();Check(Enumerable.SequenceEqual(before, await Snapshot()),"Repeated migration changes no historical rows");
        Check(!db.Database.HasPendingModelChanges(),"Migrated model matches code");
    }
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid person, IClock clock, HttpClient client, Action<DateTimeOffset> setTime)
    {
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build());
        var engine = new OperationalProcessService(db, direction, clock, new FileTechnicalValidator(), new Scanner());
        var planning = new ProcessPlanningService(db, direction, clock, new FileTechnicalValidator(), new Scanner());
        var day = WeekContract.LocalToday(clock.UtcNow);
        var excel = Excel();
        db.Add(new OperationalCapacity { Id=Guid.CreateVersion7(), PersonId=person, From=day, Through=day.AddDays(90), DailyMinutes=360, Reason="Count-source isolated checks" });
        db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(), PersonId=person, Revision=1, From=day, ChangedBy=actor, ChangedAt=clock.UtcNow, IntentKey=Guid.CreateVersion7(), RequestHash=new string('0',64), DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)})) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var item = Guid.CreateVersion7();
        var html = await client.GetStringAsync("/tareas/procesos/items");
        await Post("/tareas/procesos/items?handler=Save", html, new() { ["id"]=item.ToString(), ["revision"]="0", ["name"]="Cajas grandes", ["minutes"]="2", ["active"]="true" }, HttpStatusCode.Redirect);

        async Task<(Guid Root, OperationalTask[] Tasks, Guid Run)> Prepare(int offset, bool chain = false)
        {
            setTime(OperationalSchedule.Instant(day.AddDays(offset), 10));
            var root = Guid.CreateVersion7();
            await catalog.CreateAsync(actor, root, "Orígenes " + offset, "", default); db.ChangeTracker.Clear();
            var tasks = Enumerable.Range(0,6).Select(i=>new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=$"CNT-{offset}-{i}", Name=$"{(char)('A'+i)} · Orígenes {offset}", ProcessId=root, Requirement=offset==0&&i==2?"EXCEL":"COMENTARIO", AssignedRole=CanonicalRole.Direction, LoadMinutes=i==5?9:7, OperationalCriticality="FLEXIBLE" }).ToArray();
            db.AddRange(tasks); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var editor = await catalog.ReadAsync(actor,root,default);
            var fields = new Dictionary<string,string> { ["Input.Id"]=root.ToString(), ["Input.Revision"]=editor.Process.Revision.ToString(), ["Input.Name"]=editor.Process.Name, ["Input.StartKind"]=offset==8?"SEMANAL":"OCASIONAL" };
            if(offset==8){fields["Input.Schedule.Time"]="10:00";fields["Input.Schedule.Mode"]="DIAS_FIJOS";fields["Input.Schedule.Repetitions"]="1";fields["ProcessDays"]=((int)day.AddDays(offset).DayOfWeek).ToString();}
            for (var i=0;i<tasks.Length;i++) {
                var prefix=$"Input.Steps[{i}].";
                fields[prefix+"TaskId"]=tasks[i].Id.ToString(); fields[prefix+"Position"]=(i+1).ToString(); fields[prefix+"Action"]=i==5?"AUTORIZAR":"ENTREGAR";
                fields[prefix+"ReleaseCondition"]=i==0?"INICIO":"ENTREGA"; fields[prefix+"DeadlineHours"]="1"; fields[prefix+"IsFinalAudit"]=(i==5).ToString();
                if(offset==2&&i==2)fields[prefix+"Action"]="REVISAR";
                if(offset==2&&i==3)fields[prefix+"ReleaseCondition"]="APROBACION";
                if (i==0 || chain && i==2) fields[prefix+"OutputItemId"]=item.ToString();
                if (i is 2 or 4) fields[prefix+"InputCountSourceTaskId"]=(chain && i==4?tasks[2]:tasks[0]).Id.ToString();
            }
            html=await client.GetStringAsync($"/tareas/procesos?id={root}");
            fields["Input.Steps[2].InputCountSourceTaskId"]=tasks[4].Id.ToString();
            await Post("/tareas/procesos?handler=Save",html,fields,HttpStatusCode.UnprocessableEntity);
            Check(!await db.Set<OperationalSequenceStep>().AnyAsync(s=>s.VersionId==editor.Draft!.Id),"Invalid future source leaves no partial draft");
            fields["Input.Steps[2].InputCountSourceTaskId"]=tasks[0].Id.ToString();
            await Post("/tareas/procesos?handler=Save",html,fields,HttpStatusCode.Redirect); db.ChangeTracker.Clear();
            editor=await catalog.ReadAsync(actor,root,default);
            Check(editor.Steps[2].InputCountSourceTaskId==tasks[0].Id && editor.Steps[1].InputCountSourceTaskId==null,"Real editor save/reopen preserves optional source by identity");
            html=await client.GetStringAsync($"/tareas/procesos?id={root}");
            Check(html.Contains("Tomar conteo de") && html.Contains("InputCountSourceTaskId"),"Actual Edit details includes source selector");
            await Post("/tareas/procesos?handler=Publish",html,new(){["Input.Id"]=root.ToString(),["Input.Revision"]=editor.Process.Revision.ToString()},HttpStatusCode.Redirect); db.ChangeTracker.Clear();
            editor=await catalog.ReadAsync(actor,root,default); await catalog.SetEnabledAsync(actor,root,editor.Process.Revision,true,default); db.ChangeTracker.Clear();
            var key=Guid.CreateVersion7();
            fields=new() { ["Input.Key"]=key.ToString(), ["Input.Name"]="Plan conteos "+offset, ["Input.ProcessId"]=root.ToString(), ["Input.Mode"]=offset==8?"CALENDARIO":"OCASION", ["Input.From"]=day.AddDays(offset).ToString("yyyy-MM-dd"), ["Input.Through"]=day.AddDays(offset).ToString("yyyy-MM-dd"), ["Input.OccasionTime"]="10:00" };
            for(var i=1;i<=6;i++)fields[$"Input.People[{i}]"]=person.ToString();
            html=await client.GetStringAsync($"/planificacion/procesos?processId={root}");
            Check(!html.Contains("name=\"Input.CountSources["),"Planning does not add a second source selector");
            html=await Post("/planificacion/procesos?handler=Preview",html,fields,HttpStatusCode.OK);
            fields["Input.Fingerprint"]=Value(html,"Input.Fingerprint");
            await Post("/planificacion/procesos?handler=Prepare",html,fields,HttpStatusCode.Redirect); db.ChangeTracker.Clear();
            var start=await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(s=>s.PlanId==key);
            Check(ProcessCountSources.Read(start.CountSourcesJson).Count==2,"Prepared plan freezes both source links");
            if(offset==8) {
                editor=await catalog.ReadAsync(actor,root,default);editor.Steps[4].InputCountSourceTaskId=null;
                await catalog.SaveAsync(actor,new(){Id=root,Revision=editor.Process.Revision,Name=editor.Process.Name,StartKind="SEMANAL",Schedule=ProcessStartSchedule.Read(editor.Configuration!.StartScheduleJson),Steps=editor.Steps.ToList()},default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root,default);
                await catalog.PublishAsync(actor,root,editor.Process.Revision,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root,default);await catalog.SetEnabledAsync(actor,root,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
                await catalog.SaveItemAsync(actor,item,1,"Cajas grandes",9,true,default);db.ChangeTracker.Clear();
            }
            html=await client.GetStringAsync($"/planificacion/procesos?planId={key}");
            await Post("/planificacion/procesos?handler=Activate",html,new(){["planId"]=key.ToString()},HttpStatusCode.Redirect); db.ChangeTracker.Clear();
            var run=await db.Set<OperationalSequenceRun>().SingleAsync(r=>r.PlanId==key);
            Check(run.CountSemanticsVersion==2,"New execution uses selected-count semantics");
            if(offset==8) {var stages=await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==run.Id).OrderBy(s=>s.Position).ToArrayAsync();Check(stages[4].InputCountSourceTaskId==tasks[0].Id&&stages[0].OutputItemMinutes==2,"Recurring prepared plan retains original links and rate after new publication and tariff change");}
            return(root,tasks,run.Id);
        }
        async Task<ProcessRunView> Ready(Guid run) {
            db.ChangeTracker.Clear(); var view=await engine.ReadAsync(actor,run,default);
            for(var n=0;n<5;n++) { var active=view.Stages.Single(s=>s.Position==view.Run.ActivePosition); if(active.Status!="PROGRAMADA")break; setTime(new[]{clock.UtcNow,active.PlannedStartsAt}.Max());await engine.TickAsync(default);db.ChangeTracker.Clear();view=await engine.ReadAsync(actor,run,default); }
            return view;
        }
        async Task Deliver(Guid run,int? quantity=null, bool correction=false) {
            var view=await Ready(run);var active=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);var attempt=view.Attempts.Single(a=>a.Id==active.CurrentAttemptId);
            Check(active.Status=="DISPONIBLE",$"Stage {active.Position} ready: {active.Status} {active.BlockReason}");
            html=await client.GetStringAsync($"/mi-trabajo/procesos?id={run}");
            var fields=new Dictionary<string,string>{["RunId"]=run.ToString(),["AttemptId"]=attempt.Id.ToString(),["Revision"]=attempt.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["Decision"]=active.IsFinalAudit?"SIN_DIFERENCIAS":"ENTREGA",["Conclusion"]="Evidencia real de prueba",["Declared"]="true"};
            if(quantity!=null)fields["ItemQuantity"]=quantity.ToString()!;
            if(active.Requirement=="EXCEL")fields["TestExcel"]="true";
            if(correction) {
                var before=JsonSerializer.Serialize(view.Attempts);
                html=await Post("/mi-trabajo/procesos?handler=Decide",html,fields,HttpStatusCode.Conflict);
                db.ChangeTracker.Clear();Check(before==JsonSerializer.Serialize((await engine.ReadAsync(actor,run,default)).Attempts),"Correction preview creates no consumer attempts");
                Check(html.Contains("Revisa el impacto"),"Correction displays reviewed impact");
                fields["CapacityResolution"]="PENDING";fields["CapacityFingerprint"]=Value(html,"CapacityFingerprint");
            }
            await Task.WhenAll(Post("/mi-trabajo/procesos?handler=Decide",html,fields,HttpStatusCode.Redirect),
                Post("/mi-trabajo/procesos?handler=Decide",html,fields,HttpStatusCode.Redirect));
            fields["Conclusion"]="Intención distinta";
            await Post("/mi-trabajo/procesos?handler=Decide",html,fields,HttpStatusCode.Conflict);
            db.ChangeTracker.Clear();
        }
        var positive=await Prepare(0);
        await Deliver(positive.Run,12);
        var result=await Ready(positive.Run);
        Check(result.Run.ActivePosition==2 && result.Stages[2].LoadMinutes==24 && result.Stages[4].LoadMinutes==24 && result.Stages[1].LoadMinutes==7 && result.Stages[3].LoadMinutes==7,"A=12 updates C/E only and activates independent B");
        await Deliver(positive.Run);
        await Deliver(positive.Run);
        await Deliver(positive.Run);
        result=await Ready(positive.Run); var independentAttempt=result.Stages[1].CurrentAttemptId;
        var originalConsumer=result.Attempts.Single(a=>a.Id==result.Stages[2].CurrentAttemptId);
        var originalEvidence=(await db.OperationalAttachments.AsNoTracking().SingleAsync(a=>a.AssignmentId==originalConsumer.AssignmentId)).Content;
        var current=result.Stages.Single(s=>s.Position==result.Run.ActivePosition);
        html=await client.GetStringAsync($"/mi-trabajo/procesos?id={positive.Run}");
        await Post("/mi-trabajo/procesos?handler=ReturnPendingCount",html,new(){["RunId"]=positive.Run.ToString(),["stageId"]=current.Id.ToString(),["stageRevision"]=current.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["reason"]="Recontar cajas"},HttpStatusCode.Redirect);
        await Deliver(positive.Run,15,correction:true);
        result=await Ready(positive.Run);
        Check(result.Stages[1].CurrentAttemptId==independentAttempt && result.Stages[1].Status=="CONCLUIDA" && result.Stages[2].LoadMinutes==30 && result.Attempts.Any(a=>a.ItemQuantity==12),"Correction preserves independent completed attempt and original count");
        Check(Enumerable.SequenceEqual(originalEvidence, (await db.OperationalAttachments.AsNoTracking().SingleAsync(a=>a.AssignmentId==originalConsumer.AssignmentId)).Content),"Corrected consumer retains original Excel evidence bytes");
        while((await Ready(positive.Run)).Run.Status=="EN_CURSO")await Deliver(positive.Run);
        Check((await Ready(positive.Run)).Run.Status=="CONCLUIDA","Positive flow closes through actual audit");
        var positiveView=await Ready(positive.Run);var currentAssignments=positiveView.Attempts.Where(a=>positiveView.Stages.Any(s=>s.CurrentAttemptId==a.Id)).Select(a=>a.AssignmentId).ToArray();
        Check(!await db.OperationalAssignments.AnyAsync(a=>currentAssignments.Contains(a.Id)&&a.ReviewStatus=="PENDIENTE_REVISION"),"Correction review follows actual dependency events, leaving no current receipt unreviewed at closure");
        if(Environment.GetEnvironmentVariable("SGOL_COUNT_CORRECTION_ONLY")=="1") { Console.WriteLine("COUNT_CORRECTION_PASS");return; }
        var zero=await Prepare(2);await Deliver(zero.Run,0);result=await Ready(zero.Run);
        Check(result.Run.ActivePosition==2 && result.Stages[2].Status=="OMITIDA" && result.Stages[4].Status=="OMITIDA" && result.Stages[3].Status=="EN_ESPERA","Zero omits only nonconsecutive direct consumers");
        await Deliver(zero.Run);result=await Ready(zero.Run);Check(result.Run.ActivePosition==4,"Independent D executes after omitted C");
        await Deliver(zero.Run);result=await Ready(zero.Run);Check(result.Run.ActivePosition==6&&result.Stages[5].LoadMinutes==9,"Audit retains own workload after zero");await Deliver(zero.Run);
        var chain=await Prepare(4,true);await Deliver(chain.Run,0);await Deliver(chain.Run);await Deliver(chain.Run);result=await Ready(chain.Run);
        Check(result.Run.ActivePosition==5&&result.Stages[4].Status=="PENDIENTE_ASIGNACION"&&result.Stages[4].BlockReason!.Contains("origen fue omitido"),"Omitted producer leaves downstream source unresolved, without inventing zero");
        current=result.Stages[4];html=await client.GetStringAsync($"/mi-trabajo/procesos?id={chain.Run}");
        await Post("/mi-trabajo/procesos?handler=ReturnPendingCount",html,new(){["RunId"]=chain.Run.ToString(),["stageId"]=current.Id.ToString(),["stageRevision"]=current.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["reason"]="El cero era incorrecto"},HttpStatusCode.Redirect);
        await Deliver(chain.Run,12,correction:true);result=await Ready(chain.Run);
        Check(result.Run.ActivePosition==3&&result.Stages[2].Status=="DISPONIBLE"&&result.Stages[3].Status=="CONCLUIDA","Zero-to-positive correction revives C and preserves independent D");
        await Deliver(chain.Run,3);result=await Ready(chain.Run);
        Check(result.Run.ActivePosition==5&&result.Stages[4].LoadMinutes==6,"Second producer count remains distinct from A");
        await Deliver(chain.Run);await Deliver(chain.Run);
        var recurring=await Prepare(8);await Deliver(recurring.Run,12);result=await Ready(recurring.Run);Check(result.Stages[4].LoadMinutes==24,"Recurring execution calculates using frozen source and tariff");
        await catalog.SaveItemAsync(actor,item,2,"Cajas grandes",2,true,default);db.ChangeTracker.Clear();
        var capacity=await Prepare(10);result=await Ready(capacity.Run);current=result.Stages[0];var pending=result.Attempts.Single(a=>a.Id==current.CurrentAttemptId);
        html=await client.GetStringAsync($"/mi-trabajo/procesos?id={capacity.Run}");
        var demand=new Dictionary<string,string>{["RunId"]=capacity.Run.ToString(),["AttemptId"]=pending.Id.ToString(),["Revision"]=pending.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["Decision"]="ENTREGA",["Conclusion"]="Volumen excedido",["Declared"]="true",["ItemQuantity"]="2147483647"};
        var failure=await Post("/mi-trabajo/procesos?handler=Decide",html,demand,HttpStatusCode.Conflict);
        Check(failure.Contains(capacity.Tasks[2].TaskCode)&&failure.Contains(capacity.Tasks[4].TaskCode),"Capacity conflict identifies both consumers");
        db.ChangeTracker.Clear();Check((await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleAsync(a=>a.Id==pending.Id)).ItemQuantity==null,"Rejected demand rolls back the producer receipt");
        demand["CapacityResolution"]="PENDING";demand["CapacityFingerprint"]="stale";
        await Post("/mi-trabajo/procesos?handler=Decide",html,demand,HttpStatusCode.Conflict);
        demand["CapacityFingerprint"]=Value(failure,"CapacityFingerprint");
        await Post("/mi-trabajo/procesos?handler=Decide",html,demand,HttpStatusCode.Redirect);result=await Ready(capacity.Run);
        Check(result.Stages[2].CalculatedLoadMinutes==4294967294L && result.Stages[4].CalculatedLoadMinutes==4294967294L,"Full oversized demand preserved for all consumers without truncation");
        var denied=await client.PostAsync("/mi-trabajo/procesos?handler=ReturnPendingCount",new FormUrlEncodedContent(new Dictionary<string,string>{["RunId"]=capacity.Run.ToString()}));
        Check(denied.StatusCode==HttpStatusCode.BadRequest,"Actual correction form rejects missing CSRF");
        try { await engine.ReadAsync(Guid.NewGuid(),capacity.Run,default);throw new Exception("Foreign actor read accepted"); } catch(DirectionPlanException e) when(e.Status==403) { Console.WriteLine("PASS: Foreign actor cannot read count process"); }
        Check(!db.Database.HasPendingModelChanges(),"Migration model matches code");
        if(Environment.GetEnvironmentVariable("SGOL_COUNT_UI")=="1") {
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent!;
            var info=new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("SGOL_NODE_PATH") ?? "node") { WorkingDirectory=root.FullName,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
            info.ArgumentList.Add("tools/Sgol.MobileChecks/process-count-source-checks.cjs");info.ArgumentList.Add(positive.Root.ToString());
            using var browser=System.Diagnostics.Process.Start(info)!;var output=browser.StandardOutput.ReadToEndAsync();var errors=browser.StandardError.ReadToEndAsync();await browser.WaitForExitAsync();Console.WriteLine(await output);Console.WriteLine(await errors);Check(browser.ExitCode==0,"Real editor browser checks");
        }
        Console.WriteLine("COUNT_SOURCES_CORE_PASS");

        async Task<string> Post(string url,string page,Dictionary<string,string> fields,HttpStatusCode expected) {
            var copy=new Dictionary<string,string>(fields){["__RequestVerificationToken"]=Value(page,"__RequestVerificationToken")};
            using var multipart=new MultipartFormDataContent();
            var attach=copy.Remove("TestExcel");
            foreach(var pair in copy)multipart.Add(new StringContent(pair.Value),pair.Key);
            if(attach) { var file=new ByteArrayContent(excel);file.Headers.ContentType=new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");multipart.Add(file,"Document","evidence.xlsx"); }
            var response=await client.PostAsync(url,multipart);var content=await response.Content.ReadAsStringAsync();
            if(response.StatusCode!=expected)throw new Exception($"{url}: expected {expected}, got {response.StatusCode}: {WebUtility.HtmlDecode(Regex.Match(content,@"<p class=\""alerta[^>]*>(.*?)</p>",RegexOptions.Singleline).Groups[1].Value)}");
            Console.WriteLine($"PASS HTTP {url}: {expected}");return content;
        }
    }
    private static string Value(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static byte[] Excel() {
        using var stream=new MemoryStream();
        using(var zip=new System.IO.Compression.ZipArchive(stream,System.IO.Compression.ZipArchiveMode.Create,true)) {
            var parts=new Dictionary<string,string>{
                ["[Content_Types].xml"]="<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>",
                ["xl/workbook.xml"]="<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Entrega' sheetId='1' r:id='rId1'/></sheets></workbook>",
                ["xl/_rels/workbook.xml.rels"]="<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>",
                ["xl/worksheets/sheet1.xml"]="<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData/></worksheet>"};
            foreach(var part in parts) { using var writer=new StreamWriter(zip.CreateEntry(part.Key).Open());writer.Write(part.Value); }
        }
        return stream.ToArray();
    }
    private static void Check(bool valid,string message){if(!valid)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    private sealed class Scanner:IFileMalwareScanner { public Task CheckAvailabilityAsync(CancellationToken ct)=>Task.CompletedTask; public Task<EvidenceScanOutcome> ScanAsync(Stream stream,long length,CancellationToken ct)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio)); }
}
