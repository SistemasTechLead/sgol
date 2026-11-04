using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    static void EconomicChecks(Clock clock)
    {
        var at = clock.UtcNow; var person = Guid.NewGuid(); var receiver = Guid.NewGuid(); var stage = Guid.NewGuid();
        var row = new OperationalAssignment { Id = Guid.NewGuid(), PersonId = person, PersonName = "Origen", Name = "Compromiso",
            TaskCode = "INC-ECONOMIC", StartsAt = at, DueAt = at.AddHours(3), LoadMinutes = 10, OperationalCriticality = "FLEXIBLE" };
        TaskBonusFact Fact(DateTimeOffset recorded, Guid? stageId = null) => new() { AssignmentId = row.Id, Revision = row.Revision,
            RecordedAt = recorded, SnapshotJson = JsonSerializer.Serialize(TaskBonusAssignmentSnapshot.From(row) with { ProcessStageId = stageId }) };
        var first = Fact(at.AddHours(-1));
        row.ReviewStatus = "REASIGNADA"; row.ReviewedAt = at.AddMinutes(-30); row.Revision++;
        var returned = Fact(at.AddMinutes(-30));
        var move = new OperationalTransfer { Id = Guid.NewGuid(), AssignmentId = row.Id, FromPersonId = person, ToPersonId = receiver, At = at, NewDueAt = row.DueAt };
        row.PersonId = receiver; row.ReviewStatus = "PENDIENTE_REVISION"; row.ReviewedAt = null; row.CompletedAt = at.AddMinutes(10); row.CompletionKind = "ENTREGA"; row.Revision++;
        var submitted = Fact(at.AddMinutes(10));
        var decision = new IncidenceBonusDecision(row.Id,row.Id,null,person,receiver,new(2026,11,4),"TRANSFER",at);
        var obligations = TaskBonusService.Materialize([first,returned,submitted],[move],[],at.AddHours(1),DateTimeOffset.MaxValue,[decision]);
        Check(obligations.Length == 1 && obligations[0].PersonId == receiver && obligations[0].Events.Any(e=>e.Kind=="SUBMITTED") && !obligations[0].Events.Any(e=>e.Kind=="RETURNED"), "transfer keeps recipient delivery without prior owner's return penalty or duplicate obligation");
        row = new() { Id=Guid.NewGuid(),PersonId=person,PersonName="Origen",Name="Etapa",TaskCode="INC-STAGE",StartsAt=at,DueAt=at.AddHours(1),LoadMinutes=10,OperationalCriticality="FLEXIBLE" };
        var original = Fact(at.AddMinutes(-5),stage); var originalId = row.Id;
        var retirement = new IncidenceBonusDecision(row.Id,row.Id,stage,person,null,new(2026,11,4),"RETIRE",at);
        row.Id=Guid.NewGuid();row.ReassignedFromId=originalId;row.StartsAt=at.AddDays(1);row.DueAt=row.StartsAt.AddHours(1);
        var rescheduled=Fact(at.AddMinutes(1),stage);
        obligations=TaskBonusService.Materialize([original,rescheduled],[],[],at.AddDays(1),DateTimeOffset.MaxValue,[retirement]);
        Check(obligations.Length==2 && obligations.Single(o=>o.Id==originalId).Events.Any(e=>e.Kind=="EXCLUDED") && !obligations.Single(o=>o.Id==row.Id).Events.Any(e=>e.Kind=="EXCLUDED"), "new process commitment retains ancestry without inheriting retired exclusion");
        Check(TaskBonusService.IncidenceRoot([original,rescheduled],row.Id,[retirement])==row.Id,"second incidence resolves same new economic root as bonus materialization");
    }

    static async Task ConcurrentChecks(SgolDbContext db, Clock clock)
    {
        var f=await QaFixtures.CreateAsync(db);
        var svc=new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        var request=new IncidenceRequest(f.Person,new(2026,11,6),Guid.NewGuid(),"Doble envío concurrente aislado",OperationalSchedule.Instant(new(2026,11,6)),[]);
        var p=await svc.PreviewAsync(f.Director,request,default);
        async Task<Guid?> Apply()
        {
            await using var separate=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options,bonusClock:clock);
            try { return (await new OperationalIncidenceService(separate,clock,CorrectionSchedulingChecks.Service(separate,clock)).ApplyAsync(f.Director,request,p.Fingerprint,default)).Id; }
            catch(Npgsql.PostgresException e) when(e.SqlState=="40001") { return null; }
            catch(DbUpdateException e) when(e.InnerException is Npgsql.PostgresException { SqlState: "40001" }) { return null; }
        }
        var results=await Task.WhenAll(Apply(),Apply());
        var receipt=await svc.ApplyAsync(f.Director,request,p.Fingerprint,default);
        Check(results.Any(r=>r==receipt.Id) && results.Where(r=>r!=null).All(r=>r==receipt.Id) && await db.Set<OperationalIncidence>().CountAsync(i=>i.PersonId==f.Person)==1,
            "concurrent duplicate and serialization retry resolve to one receipt");
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var tasks=new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var definition=await db.OperationalTasks.SingleAsync(t=>t.Id==f.Definition);
        definition.PeriodType="OCASIONAL"; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var occasion=new OccasionInput(definition.Id,definition.Revision,"Trabajo futuro","Instrucciones de prueba","","",request.NotBefore.AddHours(11),request.NotBefore.AddHours(12),[f.Person]);
        try { await tasks.PreviewOccasionAsync(f.Director,occasion,default); throw new Exception("Absent future person accepted"); }
        catch(DirectionPlanException e) { Check(e.Code=="PLAN_NO_AVAILABLE_WORKER","real occasional producer refuses previously confirmed future absence"); }
    }
}
