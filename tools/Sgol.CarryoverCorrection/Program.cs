using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

// Narrow operator for the two assignments explicitly authorized in this chat. No general live test mode.
var apply = args.SequenceEqual(new[] { "--apply-authorized-viri-before-saturday" });
if (!apply && !args.SequenceEqual(new[] { "--check-isolated" })) throw new Exception("Use --check-isolated or --apply-authorized-viri-before-saturday.");
var connection = Environment.GetEnvironmentVariable("SGOL_CARRYOVER_CORRECTION_CONNECTION") ?? throw new Exception("Explicit correction connection required.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host != "localhost" || settings.Database == null || (apply ? settings.Database != "sgol_local" : !settings.Database.StartsWith("sgol_task_checks_"))) throw new Exception("Connection outside authorized scope.");
var requestId = Guid.Parse("01a17efe-16a1-710e-8755-281ad35133be");
Guid[] selected = [Guid.Parse("01a1797f-b6bb-7146-b1b3-12448b6f8875"), Guid.Parse("01a1797f-b6bf-7e28-afa8-68ced8fc3b78")];
var options = new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options;
await using var db = new SgolDbContext(options);
var clock = new RunClock();
if (WeekContract.LocalToday(clock.UtcNow) != new DateOnly(2026,10,27)) throw new Exception("This authorization targets the Tuesday 27 October workflow only.");
await db.Database.MigrateAsync();
Check(!db.Database.HasPendingModelChanges(), "EF model matches migration snapshot");
var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
var supervision = new OperationalSupervisionService(db,clock,direction);
var service = new OperationalCarryoverService(db,clock,supervision);
var originalRequest = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==requestId);
var originalRequestJson = JsonSerializer.Serialize(originalRequest);
Check(originalRequest.Kind=="POSPONER" && originalRequest.State=="APROBADA" && originalRequest.Items().Select(i=>i.AssignmentId).Order().SequenceEqual(selected.Order()),"Exact approved payroll request and two authorized IDs");
var workers = await OperationalCapacityService.WorkersAsync(db,clock.UtcNow,default);
var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
var viri = users.Single(u=>u.Id==originalRequest.RequestedBy);
Check(workers.Single(w=>w.Id==viri.PersonId).Name=="Viri", "Requester is the existing Viri account");
var director = users.Single(u=>u.PersonId==workers.Single(w=>w.Role==CanonicalRole.Direction).Id).Id;
var before = await Rows(); var beforeHash = Hash(before);
var protectedBefore = await ProtectedHashes();
if (!apply) {
    await Rejected(()=>service.RestorePostponementAsync(viri.Id,requestId,selected,"Prueba aislada",default),"ACCESO_DENEGADO");
    Check(PostponementWindow.For(new(2026,10,27),6)==(new DateOnly(2026,10,28),new DateOnly(2026,10,30),new DateOnly(2026,10,31)),"Before Saturday means Wednesday, Thursday, Friday only");
    Check(PostponementWindow.For(new(2026,10,27),null).Through==new DateOnly(2026,11,3),"No extra limit preserves the seven-day window");
    Check(PostponementWindow.For(new(2026,10,27),2).Through==new DateOnly(2026,11,2),"Same weekday means the next occurrence, exclusively");
    await Rejected(()=>Task.Run(()=>PostponementWindow.For(new(2026,10,27),3)),"ARRASTRE_LIMITE_SIN_DIAS");
    await Rejected(()=>Task.Run(()=>PostponementWindow.For(new(2026,10,27),7)),"ARRASTRE_DATOS_INVALIDOS");
}
var restorationReason="Corrección solicitada expresamente por el usuario: restaurar prenómina y nómina a sus fechas vencidas anteriores y solicitar nueva distribución antes del sábado 31/10/2026.";
await service.RestorePostponementAsync(director,requestId,selected,restorationReason,default); db.ChangeTracker.Clear();
var restored = await Rows();
foreach(var id in selected) {
    var row=restored.Single(a=>a.Id==id); var item=originalRequest.Items().Single(i=>i.AssignmentId==id);
    Check(row.StartsAt==item.Start && row.DueAt==item.Due && row.DueAt<clock.UtcNow && row.CompletedAt==null,"Restored overdue dates: "+row.TaskCode);
    if (!apply) Check((await new OperationalCarryoverReader(db).ForAsync(id)).ApprovedCount==0,"Restored approval cannot continue recognizing the November deadline: "+row.TaskCode);
}
Check(JsonSerializer.Serialize(before.Where(a=>!selected.Contains(a.Id)))==JsonSerializer.Serialize(restored.Where(a=>!selected.Contains(a.Id))),"Restoration leaves every unrelated assignment unchanged");
Check(originalRequestJson==JsonSerializer.Serialize(await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==requestId)),"Original approval and its frozen snapshot remain immutable");
if(!apply) {
    var restoredHash=Hash(restored);
    await service.RestorePostponementAsync(director,requestId,selected,restorationReason,default);
    Check(restoredHash==Hash(await Rows()),"Restoration retry does not decrement revisions or duplicate corrections");
}
const string cause="FALTA_TIEMPO_OPERATIVO";
const string explanation="A solicitud expresa del usuario, asistencia técnica envía por cuenta de Viri la nueva petición de prenómina y nómina, con asignación antes del sábado 31/10/2026 (miércoles, jueves o viernes). Requiere autorización de Supervisión.";
var preview=await service.PreviewPostponementAsync(viri.Id,selected,cause,explanation,default,6);
Check(preview.Conflicts.Length==0,"Current payroll tasks fit before Saturday: "+string.Join("; ",preview.Conflicts));
Check(preview.Items.Length==2 && preview.Days.Length==3 && preview.Items.All(i=>i.BeforeDate==new DateOnly(2026,10,31) && WeekContract.LocalToday(i.ProposedStart!.Value)>=new DateOnly(2026,10,28) && WeekContract.LocalToday(i.Proposed)<=new DateOnly(2026,10,30)),"Both proposals strictly respect the Wednesday-Friday window");
if (!apply) await Rejected(()=>service.SubmitPostponementAsync(viri.Id,selected,cause,explanation,preview.Fingerprint,Guid.NewGuid(),default,0),"ARRASTRE_CONFLICTO");
var intent=Guid.NewGuid();
var idNew=await service.SubmitPostponementAsync(viri.Id,selected,cause,explanation,preview.Fingerprint,intent,default,6);
db.AuditEvents.Add(new AuditEvent { Id=Guid.CreateVersion7(),OccurredAt=clock.UtcNow,ActorUserId=director,ActorType="USER",Action="OPERATIONAL_POSTPONEMENT_SUBMITTED_ON_BEHALF",
    ResourceType="OPERATIONAL_CARRYOVER",ResourceId=idNew,BranchId=originalRequest.BranchId,CorrelationId=requestId,Outcome="SUCCESS",
    AfterData=JsonSerializer.SerializeToDocument(new { RequestId=idNew,RequestedBy=viri.Id,BeforeWeekday=6,Reason="Envío asistido autorizado expresamente por el usuario de esta conversación; no representa un clic personal de Viri." }) });
await db.SaveChangesAsync();
Check(Hash(restored)==Hash(await Rows()),"Sending the new request preserves the restored overdue assignments until approval");
Check((await service.InboxAsync(director,default)).Any(r=>r.Id==idNew),"Direction receives Viri's new before-Saturday request");
var pendingRequest=await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==idNew);
Check(pendingRequest.State=="PENDIENTE" && pendingRequest.Through==new DateOnly(2026,10,30),"Exclusive limit is persisted with the pending request");
if(!apply) {
    Check(idNew==await service.SubmitPostponementAsync(viri.Id,selected,cause,explanation,preview.Fingerprint,intent,default,6),"Constrained submission replay is idempotent");
    await service.DecideAsync(director,idNew,pendingRequest.Revision,true,"Aprobación de prueba solo en la copia aislada",default); db.ChangeTracker.Clear();
    var applied=await Rows();
    foreach(var item in preview.Items) {
        var row=applied.Single(a=>a.Id==item.AssignmentId);
        Check(row.StartsAt==item.ProposedStart && row.DueAt==item.Proposed && WeekContract.LocalToday(row.DueAt)<new DateOnly(2026,10,31),"Approval applies the exact allowed slot: "+row.TaskCode);
        Check((await new OperationalCarryoverReader(db).ForAsync(row.Id)).ApprovedCount==1,"Only the new approval counts after restoration: "+row.TaskCode);
    }
    Check(JsonSerializer.Serialize(before.Where(a=>!selected.Contains(a.Id)))==JsonSerializer.Serialize(applied.Where(a=>!selected.Contains(a.Id))),"Approval preserves unrelated assignments");
}
Check(Hash(protectedBefore)==Hash(await ProtectedHashes()),"Evidence, submissions, reviews, plans, schedules, capacities and other protected tables are unchanged");
var output=Path.GetFullPath(".local/carryover-before-saturday-20261027/"+(apply?"live-result.json":"isolated-result.json"));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new { Database=settings.Database, OriginalRequest=requestId, NewRequest=idNew, Actor=director, RequestedBy=viri.Id, BeforeHash=beforeHash,
    AfterHash=Hash(await Rows()), IsLive=apply, Pending=apply, preview.From, preview.Through, preview.BeforeDate, preview.Items, preview.Days },new JsonSerializerOptions {WriteIndented=true}));
foreach(var item in preview.Items) Console.WriteLine($"PROPOSAL {item.Task}: {TimeZoneInfo.ConvertTime(item.ProposedStart!.Value,OperationalSchedule.Zone):dddd dd/MM/yyyy HH:mm} - {TimeZoneInfo.ConvertTime(item.Proposed,OperationalSchedule.Zone):HH:mm}");
Console.WriteLine($"RESULT request={idNew}; live={apply}; pending={apply}; report={output}");

async Task<OperationalAssignment[]> Rows()=>await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();
async Task<Dictionary<string,string>> ProtectedHashes() {
    await using var conn=new NpgsqlConnection(connection); await conn.OpenAsync();
    var result=new Dictionary<string,string>(); var names=new List<string>();
    await using(var cmd=new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename",conn))
    await using(var reader=await cmd.ExecuteReaderAsync()) while(await reader.ReadAsync()) names.Add(reader.GetString(0));
    foreach(var name in names.Except(new[]{"operational_assignment","operational_carryover_request","audit_event","__EFMigrationsHistory"})) {
        var quoted='"'+name.Replace("\"","\"\"")+'"';
        await using var cmd=new NpgsqlCommand("SELECT md5(coalesce(string_agg(md5(to_jsonb(t)::text),'' ORDER BY md5(to_jsonb(t)::text)),'')) FROM "+quoted+" t",conn);
        result[name]=(string)(await cmd.ExecuteScalarAsync())!;
    }
    return result;
}
static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
static void Check(bool value,string text) { if(!value) throw new Exception(text); Console.WriteLine("PASS "+text); }
static async Task Rejected(Func<Task> action,string code) { try {await action(); throw new Exception("Expected "+code);} catch(DirectionPlanException e){Check(e.Code==code,"Rejected "+code);} }
sealed class RunClock:IClock {public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow;}
