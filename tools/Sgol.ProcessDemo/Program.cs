using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Evidence;
using Microsoft.Extensions.Options;

if(!args.Contains("--provision-authorized-example") && !args.Contains("--reassign-authorized-example")) throw new Exception("Use an explicit authorized demonstration operation.");
var connection=Environment.GetEnvironmentVariable("SGOL_PROCESS_DEMO_CONNECTION")??throw new Exception("Missing target connection");
var settings=new NpgsqlConnectionStringBuilder(connection);
if(settings.Host!="localhost" || settings.Database!="sgol_local" && !(settings.Database?.StartsWith("sgol_task_checks_processes_deployment_")??false)) throw new Exception("Target outside authorized local demonstration scope.");
await using var db=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options);
if((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Apply reviewed migrations separately after verified recovery. Provisioning never migrates.");
var clock=new DemoClock(); var ids=new Uuid7Generator(clock); var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build();
var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,ids,new DirectionPlanningCatalogReader(db));
var validator=new FileTechnicalValidator(); var scanner=new ClamAvScanner(Options.Create(new EvidenceScannerOptions { Host="localhost",Port=3310 }));
// Configuration does not upload files; the live app uses its existing configured ClamAV adapter.
var tasks=new OperationalTaskService(db,direction,clock,validator,scanner);
var catalog=new OperationalProcessCatalogService(db,direction,clock,config); var processes=new OperationalProcessService(db,direction,clock,validator,scanner);
Directory.CreateDirectory(".local");
if(args.Contains("--reassign-authorized-example")) { await AuthorizedProcessReassignment.RunAsync(db,processes,clock,settings.Database!,args.Contains("--preview")); return; }
var statePath=$".local/inventario-ejemplo-{settings.Database}.json";
if(File.Exists(statePath))
{
    var state=JsonDocument.Parse(await File.ReadAllTextAsync(statePath)); var processId=state.RootElement.GetProperty("processId").GetGuid();
    if(await db.Set<OperationalSequence>().AnyAsync(p=>p.Id==processId)) { Console.WriteLine("Existing example preserved. State: "+statePath); return; }
    throw new Exception("Saved example identity is missing in target; investigate before provisioning another.");
}
var existingVisible=await db.OperationalTasks.AsNoTracking().Where(t=>!t.Deleted).CountAsync();
if(existingVisible!=0) throw new Exception("Retire the authorized test definitions through the reviewed, audited procedure before creating the example.");
var roles=new[] { CanonicalRole.SalesFloor,CanonicalRole.Subcoordination,CanonicalRole.Administration,CanonicalRole.Direction };
var loads=new[] {360,60,60,60};
var people=new Dictionary<int,Guid>(); var actors=new Guid[4];
var password="Ejemplo!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
await using(var tx=await db.Database.BeginTransactionAsync())
{
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
    if(await db.IdentityCredentials.AnyAsync(c=>c.NormalizedUserName.StartsWith("INVENTARIO.EJEMPLO."))) throw new Exception("Example credentials already exist; do not overwrite them.");
    for(var i=0;i<4;i++)
    {
        var at=clock.UtcNow; var person=ids.NewUuid(); var actor=ids.NewUuid(); people[i+1]=person; actors[i]=actor;
        db.People.Add(new Person { Id=person,StableCode=$"INV-DEMO-{i+1}",DisplayName=$"Ejemplo · {roles[i]}",CreatedAt=at });
        var user=new AppUser { Id=actor,PersonId=person,Status=AccountStatus.Active,SecurityStamp=Guid.NewGuid().ToString("N") }; db.Add(user);
        db.EmploymentVersions.Add(new(ids.NewUuid(),person,BranchScope.LorettaId,EmploymentStatus.Active,at));
        db.RoleAssignmentVersions.Add(new() { Id=ids.NewUuid(),UserId=actor,BranchId=BranchScope.LorettaId,RoleCode=roles[i],Status=RoleAssignmentStatus.Active,ValidFrom=at });
        db.IdentityCredentials.Add(new() { UserId=actor,UserName=$"inventario.ejemplo.{i+1}",NormalizedUserName=$"INVENTARIO.EJEMPLO.{i+1}",PasswordHash=new PasswordHasher<AppUser>().HashPassword(user,password) });
        db.OperationalCapacities.Add(new() { Id=ids.NewUuid(),PersonId=person,From=WeekContract.LocalToday(at),Through=WeekContract.LocalToday(at).AddDays(366),DailyMinutes=Math.Max(120,loads[i]),
            Reason="Capacidad exclusiva de cuentas de demostración. No modifica capacidad de participantes reales." });
        db.AuditEvents.Add(new AuditEvent { Id=ids.NewUuid(),OccurredAt=at,ActorType="SYSTEM",Action="DEMO_ACCOUNT_PROVISIONED",ResourceType="APP_USER",ResourceId=actor,
            BranchId=BranchScope.LorettaId,CorrelationId=ids.NewUuid(),Outcome="SUCCESS",Reason="Ejemplo solicitado por el usuario; cuenta sintética identificada como ejemplo." });
    }
    await db.SaveChangesAsync(); await tx.CommitAsync(); db.ChangeTracker.Clear();
}
var key=ids.NewUuid();
await catalog.CreateAsync(actors[3],key,"Ejemplo: Chequeo de inventario","Demostración solicitada: PDF, devolución por faltantes/sobrantes y aceptación con diferencias pendientes. Sin inventario ni participantes reales.",default); db.ChangeTracker.Clear();
var taskIds=new Guid[4];
for(var i=0;i<4;i++)
{
    var rule=OperationalSequenceRules.InventoryPilot[i];
    await tasks.SaveAsync(actors[3],new OperationalTask { Revision=0,Name=rule.Name+" · Ejemplo",ProcessId=key,Requirement="DOCUMENTO",AssignedRole=rule.Role,
        PeriodType="OCASIONAL",LoadMinutes=loads[i],DeadlineHours=rule.DeadlineHours },default); db.ChangeTracker.Clear();
    taskIds[i]=await db.OperationalTasks.Where(t=>t.ProcessId==key && t.AssignedRole==rule.Role).Select(t=>t.Id).SingleAsync();
}
var editor=await catalog.ReadAsync(actors[3],key,default);
await catalog.SaveAsync(actors[3],new ProcessDraftInput { Id=key,Revision=editor.Process.Revision,Name=editor.Process.Name,Description=editor.Process.Description,
    StartKind="SEMESTRAL",StartHour=11,Steps=OperationalSequenceRules.InventoryPilot.Select((s,i)=>new OperationalSequenceStep {
        TaskId=taskIds[i],Position=s.Position,Action=s.Action,ReleaseCondition=s.ReleaseCondition,DeadlineHours=s.DeadlineHours,ReturnToPosition=s.ReturnToPosition,
        Instructions=new[] { "Registrar todo el inventario en PDF. Si se devuelve, buscar faltantes y presentar sobrantes.",
            "Revisar manualmente contra Sizes & Colors y los zapatos. Registrar diferencias pendientes; aprobar o devolver a Piso.",
            "Revisar hoja e irregularidades. Devolver con información o pasar a Dirección.",
            "Aceptar el PDF exacto con conclusión y diferencias pendientes, o devolver a Administración. El plazo es 168 horas; la carga provisional, 60 minutos." }[i] }).ToList() },default);
db.ChangeTracker.Clear(); editor=await catalog.ReadAsync(actors[3],key,default); await catalog.PublishAsync(actors[3],key,editor.Process.Revision,default);
db.ChangeTracker.Clear(); editor=await catalog.ReadAsync(actors[3],key,default); await catalog.SetEnabledAsync(actors[3],key,editor.Process.Revision,true,default); db.ChangeTracker.Clear();
var planning=new ProcessPlanningService(db,direction,clock,validator,scanner);
var planInput=new ProcessPlanInput { Key=ids.NewUuid(),Name="Demostración · Plan de chequeo de inventario",ProcessId=key,Mode="OCASION",
    From=WeekContract.LocalToday(clock.UtcNow),Through=WeekContract.LocalToday(clock.UtcNow),People=people };
var preview=await planning.PreviewAsync(actors[3],planInput,default); planInput.Fingerprint=preview.Fingerprint;
var plan=await planning.PrepareAsync(actors[3],planInput,default);
await File.WriteAllTextAsync(statePath,JsonSerializer.Serialize(new { database=settings.Database,processId=key,planId=plan,people,actors,taskIds },new JsonSerializerOptions { WriteIndented=true }));
await File.WriteAllTextAsync($".local/inventario-ejemplo-accesos-{settings.Database}.txt",
    "Cuentas sintéticas de demostración. Configura MFA en el primer acceso.\n"+string.Join("\n",Enumerable.Range(0,4).Select(i=>$"{roles[i]}\tinventario.ejemplo.{i+1}\t{password}"))+"\n");
Console.WriteLine($"Example configured: 4 tasks, start 11:00 CDMX, loads 360/60/60/60, deadlines 6/1/1/168 hours. State: {statePath}");
sealed class DemoClock : IClock { public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow; }
