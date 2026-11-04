using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    sealed class FailAfterSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public int Remaining { get; set; } = 2;
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (--Remaining == 0) throw new InvalidOperationException("AUDIT_INJECTED_AFTER_SAVE");
            return ValueTask.FromResult(result);
        }
    }
    public static async Task AuditChecks(SgolDbContext db, Clock clock)
    {
        clock.UtcNow = OperationalSchedule.Instant(new(2026,11,4),10).AddTicks(7654321);
        var f = await QaFixtures.CreateAsync(db);
        var a = await Row(db,f,clock,clock.UtcNow.AddHours(1));
        var b = await Row(db,f,clock,clock.UtcNow.AddHours(2));
        var service = new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        var request = new IncidenceRequest(f.Person,new(2026,11,4),Guid.NewGuid(),"Auditoría de intención equivalente",clock.UtcNow.AddMinutes(2),
            [new(a.Id,"TRANSFER",f.OtherPerson,true),new(b.Id,"RETIRE")]);
        var preview = await service.PreviewAsync(f.Director,request,default);
        Check(preview.CanApply,"audit baseline mixed transfer and retirement");
        var reordered = request with { Decisions = request.Decisions.Reverse().ToArray() };
        Check((await service.PreviewAsync(f.Director,reordered,default)).Fingerprint == preview.Fingerprint,
            "audit decision enumeration order does not change logical intention");
        var irrelevant = request with { Decisions = [request.Decisions[0],new(b.Id,"RETIRE",f.OtherPerson,true)] };
        Check((await service.PreviewAsync(f.Director,irrelevant,default)).Fingerprint == preview.Fingerprint,
            "audit service discards recipient and overload on retirement");
        clock.UtcNow = clock.UtcNow.AddSeconds(45);
        var receipt = await service.ApplyAsync(f.Director,reordered,preview.Fingerprint,default);
        Check((await service.ApplyAsync(f.Director,irrelevant,preview.Fingerprint,default)).Id == receipt.Id,
            "audit equivalent replay returns one receipt after realistic delay");
        await Reject(async()=>await service.ApplyAsync(f.Director,request with { Decisions=[new(a.Id,"RETIRE"),new(b.Id,"RETIRE")] },preview.Fingerprint,default),
            "audit changed action with same key remains rejected");
        // Existing receipts used the noncanonical request hash. Preserve replay across upgrade.
        var legacy = await db.Set<OperationalIncidence>().SingleAsync(i=>i.Id==receipt.Id);
        legacy.IntentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(reordered)));
        legacy.ReceiptJson = JsonSerializer.Serialize(preview with { Request=reordered });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check((await service.ApplyAsync(f.Director,request,preview.Fingerprint,default)).Id==receipt.Id,
            "audit historical receipt with noncanonical hash replays equivalent intention");

        foreach(var offset in new[]{-1,0,1})
        {
            var boundary = await QaFixtures.CreateAsync(db);
            var row = await Row(db,boundary,clock,clock.UtcNow.AddHours(1));
            var req = request with { PersonId=boundary.Person,Key=Guid.NewGuid(),Decisions=[],NotBefore=clock.UtcNow.AddMinutes(2) };
            // PostgreSQL stores microseconds; use the actual persisted deadline,
            // not the pre-save .NET value that may contain a seventh decimal.
            clock.UtcNow = (await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==row.Id)).DueAt.AddTicks(offset);
            var p = await service.PreviewAsync(boundary.Director,req,default);
            Check(p.Changes.Single(c=>c.Before.Id==row.Id).Action==(offset<0?"TRANSFER":"RETIRE"),$"audit deadline tick {offset}");
            clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10).AddTicks(7654321);
        }
        var stale = await QaFixtures.CreateAsync(db);
        var task = await Row(db,stale,clock,clock.UtcNow.AddHours(2));
        var staleRequest = request with {PersonId=stale.Person,Key=Guid.NewGuid(),Decisions=[new(task.Id,"TRANSFER",stale.OtherPerson,true)]};
        var reviewed = await service.PreviewAsync(stale.Director,staleRequest,default);
        var changed = await db.OperationalAssignments.SingleAsync(x=>x.Id==task.Id);
        changed.Comment="Avance concurrente que debe conservarse"; changed.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(async()=>await service.ApplyAsync(stale.Director,staleRequest,reviewed.Fingerprint,default),"audit concurrent comment invalidates explicit transfer");
        Check(!await db.Set<OperationalIncidence>().AnyAsync(i=>i.PersonId==stale.Person) &&
            (await db.OperationalAssignments.SingleAsync(x=>x.Id==task.Id)).Comment==changed.Comment,
            "audit concurrent evidence rejection leaves original owner and no absence");
        await Reject(async()=>await service.PreviewAsync(stale.Director,staleRequest with {Day=new(2026,11,3)},default),"audit manipulated past date rejected");
        await Reject(async()=>await service.PreviewAsync(stale.Director,staleRequest with {Decisions=[staleRequest.Decisions[0],staleRequest.Decisions[0]]},default),"audit duplicate decisions rejected");
        // Eight rows cover every pair of day/reflow/recipient/overload levels.
        for(var n=0;n<8;n++)
        {
            clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10).AddTicks(1234567);
            var pair=await QaFixtures.CreateAsync(db);
            var future=(n&1)!=0; var reflow=(n&2)!=0; var explicitRecipient=(n&4)!=0;
            var overload=future ^ reflow ^ explicitRecipient;
            var day=new DateOnly(2026,11,4).AddDays(future?1:0);
            var row=await Row(db,pair,clock,OperationalSchedule.Instant(day,11));
            var req=new IncidenceRequest(pair.Person,day,Guid.NewGuid(),"Combinación por pares de reparto",future?OperationalSchedule.Instant(day):clock.UtcNow.AddMinutes(2),
                [new(row.Id,"TRANSFER",explicitRecipient?pair.OtherPerson:null,overload)],reflow);
            var p=await service.PreviewAsync(pair.Director,req,default);
            Check(p.CanApply,$"pair {n}: confirmable day={day} reflow={reflow} explicit={explicitRecipient} overload={overload}");
            var saved=await service.ApplyAsync(pair.Director,req,p.Fingerprint,default); db.ChangeTracker.Clear();
            var persisted=await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==row.Id);
            Check(persisted.PersonId==p.Changes.Single().After!.PersonId && persisted.LoadMinutes==row.LoadMinutes &&
                (await service.HistoryAsync(pair.Director,day,default)).Any(x=>x.Id==saved.Id),$"pair {n}: persisted effort, owner and history agree with review");
        }
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
        var atomic=await QaFixtures.CreateAsync(db);
        var expired=await Row(db,atomic,clock,clock.UtcNow.AddHours(-1));
        var active=await Row(db,atomic,clock,clock.UtcNow.AddHours(1));
        db.Add(new Sgol.Organization.Contracts.AvailabilityDayVersion(Guid.CreateVersion7(),atomic.Person,
            Sgol.Organization.Contracts.BranchScope.LorettaId,new(2026,11,4),true,atomic.Director));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        async Task<string> Snapshot() => JsonSerializer.Serialize(new {
            Assignments=await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            Availability=await db.AvailabilityDayVersions.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            Receipts=await db.Set<OperationalIncidence>().CountAsync(), Dispositions=await db.Set<OperationalIncidenceDisposition>().CountAsync(),
            Transfers=await db.Set<OperationalTransfer>().CountAsync(), Exclusions=await db.Set<TaskBonusExclusion>().CountAsync(),
            Facts=await db.Set<TaskBonusFact>().CountAsync(), Audit=await db.AuditEvents.CountAsync()
        });
        var beforeAtomic=await Snapshot();
        var fail=new FailAfterSave();
        await using var failedDb=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(fail).Options,bonusClock:clock);
        var atomicService=new OperationalIncidenceService(failedDb,clock,CorrectionSchedulingChecks.Service(failedDb,clock));
        var atomicRequest=new IncidenceRequest(atomic.Person,new(2026,11,4),Guid.NewGuid(),"Fallo después del segundo guardado",clock.UtcNow.AddMinutes(2),[new(active.Id,"TRANSFER",atomic.OtherPerson,true)]);
        var atomicPreview=await atomicService.PreviewAsync(atomic.Director,atomicRequest,default);
        var rejected=false;
        try { await atomicService.ApplyAsync(atomic.Director,atomicRequest,atomicPreview.Fingerprint,default); }
        catch(InvalidOperationException e) when(e.Message=="AUDIT_INJECTED_AFTER_SAVE") { rejected=true; }
        db.ChangeTracker.Clear();
        Check(rejected && beforeAtomic==await Snapshot(),"audit second save failure rolls back versioned availability, retirement, transfer, bonus, receipt and audit");
        var retried=await atomicService.ApplyAsync(atomic.Director,atomicRequest,atomicPreview.Fingerprint,default);
        Check((await atomicService.ApplyAsync(atomic.Director,atomicRequest,atomicPreview.Fingerprint,default)).Id==retried.Id,
            "audit retry after second-save rollback succeeds once on same context");
        await StateAuditChecks(db,clock);
    }
}
