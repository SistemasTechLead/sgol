using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessItemLegacyReplayChecks
{
    public static async Task RunAsync(SgolDbContext db)
    {
        var candidate=await (from a in db.Set<OperationalSequenceAttempt>().AsNoTracking()
                            join s in db.Set<OperationalSequenceStage>().AsNoTracking() on a.StageId equals s.Id
                            join r in db.Set<OperationalSequenceRun>().AsNoTracking() on a.RunId equals r.Id
                            join f in db.OperationalAttachments.AsNoTracking() on a.AssignmentId equals f.AssignmentId
                            where s.Position==1&&s.Requirement=="EXCEL"&&r.UsesTaskEvidence&&s.OutputItemRateId==null
                            select new {Attempt=a,Attachment=f}).FirstAsync();
        var audits=await db.AuditEvents.AsNoTracking().Where(e=>e.ResourceId==candidate.Attempt.RunId&&e.Action=="PROCESS_DECISION"&&e.AfterData!=null).ToArrayAsync();
        var recorded=audits.Single(e=>e.AfterData!.RootElement.GetProperty("attemptId").GetGuid()==candidate.Attempt.Id);
        var attemptId=recorded.AfterData!.RootElement.GetProperty("attemptId").GetGuid();
        if(attemptId!=candidate.Attempt.Id)throw new Exception("Fixture must include the first original Excel delivery audit");
        var before=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync());
        var clock=new Clock();var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var engine=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new Scanner());
        var file=new FormFile(new MemoryStream(candidate.Attachment.Content),0,candidate.Attachment.Content.Length,"Document",candidate.Attachment.FileName){Headers=new HeaderDictionary(),ContentType=candidate.Attachment.ContentType};
        await engine.DecideAsync(recorded.ActorUserId!.Value,candidate.Attempt.RunId,attemptId,candidate.Attempt.Revision,Guid.ParseExact(recorded.RequestId!,"N"),recorded.AfterData.RootElement.GetProperty("decision").GetString()!,candidate.Attempt.Comment!,candidate.Attempt.UnresolvedDifferences,file,true,default);
        db.ChangeTracker.Clear();
        try { await engine.DecideAsync(recorded.ActorUserId!.Value,candidate.Attempt.RunId,attemptId,candidate.Attempt.Revision,Guid.ParseExact(recorded.RequestId!,"N"),recorded.AfterData.RootElement.GetProperty("decision").GetString()!,candidate.Attempt.Comment!+" distinto",candidate.Attempt.UnresolvedDifferences,file,true,default);throw new Exception("Changed historical payload accepted"); }
        catch(Sgol.Planning.Contracts.DirectionPlanException e)when(e.Code=="PROCESS_CONFLICT"){Console.WriteLine("PASS: Historical replay still rejects changed payload");}
        db.ChangeTracker.Clear();
        if(before!=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()))throw new Exception("Original delivery retry changed history");
        Console.WriteLine("PASS: Pre-item historical delivery retry keeps its original hash and changes no rows");
    }
    private sealed class Clock:IClock{public DateTimeOffset UtcNow=>new(2026,10,27,18,0,0,TimeSpan.Zero);}
    private sealed class Scanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
}
