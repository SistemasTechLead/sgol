using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class AuthorizedProcessReassignment
{
    private sealed record Request(Guid Actor,Guid Run,long Revision,Guid Key,Dictionary<int,Guid> People,string Reason,CapacityChange Capacity);
    public static async Task RunAsync(SgolDbContext db,OperationalProcessService service,IClock clock,string database,bool preview)
    {
        var file=$".local/process-assignment-{database}.json";
        if(preview)
        {
            var run=await db.Set<OperationalSequenceRun>().AsNoTracking().SingleAsync(r=>r.Name=="Ejemplo: Chequeo de inventario" && r.Status=="EN_CURSO");
            var stages=await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==run.Id).OrderBy(s=>s.Position).ToArrayAsync();
            if(stages.Length!=4 || run.ActivePosition!=1) throw new Exception("This authorized adjustment requires the four-stage example with Piso still active.");
            var ana=await db.IdentityCredentials.Where(c=>c.NormalizedUserName=="ANA.DEMO").Select(c=>c.UserId).SingleAsync();
            var director=await db.IdentityCredentials.Where(c=>c.NormalizedUserName=="DIRECCION.LOCAL").Select(c=>c.UserId).SingleAsync();
            var anaPerson=await db.AppUsers.Where(u=>u.Id==ana).Select(u=>u.PersonId).SingleAsync();
            var directorPerson=await db.AppUsers.Where(u=>u.Id==director).Select(u=>u.PersonId).SingleAsync();
            var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
            var from=WeekContract.LocalToday(clock.UtcNow); var through=WeekContract.LocalToday(stages.Max(s=>s.PlannedDueAt));
            var existing=CapacityLedger.Days(from,through).Max(d=>ledger.DayLoad(anaPerson,d));
            var minutes=(int)Math.Ceiling(Math.Max(CapacityLedger.Days(from,through).Max(d=>ledger.DayCapacity(anaPerson,d)),existing+stages[0].LoadMinutes));
            if(minutes>480) throw new Exception($"Ana requires {minutes} minutes/day including pending load; current limit 480. Nothing changed.");
            const string reason="Solicitud del usuario: probar el inventario con ana.demo en Piso y direccion.local en Dirección, elevando temporalmente la capacidad de Ana para la etapa y su carga pendiente.";
            var request=new Request(director,run.Id,run.Revision,Guid.CreateVersion7(),new() { [1]=anaPerson,[4]=directorPerson },reason,
                new(anaPerson,from,through,100,minutes,"Capacidad temporal de prueba autorizada para el inventario; al terminar la vigencia vuelve la capacidad habitual."));
            await File.WriteAllTextAsync(file,JsonSerializer.Serialize(request,new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine($"Preview only: ana.demo -> Piso; direccion.local -> Dirección; Ana capacity {minutes} min/day, {from:yyyy-MM-dd} through {through:yyyy-MM-dd}; prior maximum daily pending load {existing} min. Other stages unchanged. Request: {file}");
            return;
        }
        var input=JsonSerializer.Deserialize<Request>(await File.ReadAllTextAsync(file))!;
        await service.ReassignAsync(input.Actor,input.Run,input.Revision,input.Key,input.People,input.Reason,input.Capacity,default,systemAction:true);
        db.ChangeTracker.Clear();
        var current=await service.ReadAsync(input.Actor,input.Run,default);
        if(current.Stages[0].ReservedPersonId!=input.People[1] || current.Stages[3].ReservedPersonId!=input.People[4] || current.Run.ActivePosition!=1)
            throw new Exception("Unexpected assignment result.");
        var attempt=current.Attempts.Single(a=>a.Id==current.Stages[0].CurrentAttemptId);
        if(!await db.OperationalAssignments.AnyAsync(a=>a.Id==attempt.AssignmentId && a.PersonId==input.People[1] && a.CompletedAt==null))
            throw new Exception("Ana did not receive the current assignment.");
        Console.WriteLine($"Verified: Piso assigned to ana.demo with a new attempt; Dirección reserved for direccion.local; deadline and history preserved. Run: {input.Run}");
    }
}
