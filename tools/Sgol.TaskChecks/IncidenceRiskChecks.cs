using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    sealed class AdvanceOnCommit(Clock clock) : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public DateTimeOffset? AdvanceTo { get; set; }
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> TransactionCommittingAsync(System.Data.Common.DbTransaction transaction,
            Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (AdvanceTo is {} instant) { clock.UtcNow = instant; AdvanceTo = null; }
            return ValueTask.FromResult(result);
        }
    }
    static async Task RiskChecks(SgolDbContext db, Clock clock)
    {
        clock.UtcNow = OperationalSchedule.Instant(new(2026, 11, 4), 10);
        var f = await QaFixtures.CreateAsync(db);
        var a = await Row(db, f, clock, clock.UtcNow.AddHours(1), 30);
        db.Add(new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = f.OtherPerson, From = new(2026, 11, 4), Through = new(2026, 11, 4), Percent = 0, Reason = "Capacidad cero de prueba" });
        db.Add(new OperationalAttachment { AssignmentId = a.Id, FileName = "evidence.pdf", ContentType = "application/pdf", Content = [1, 2, 3], CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var svc = new OperationalIncidenceService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var request = new IncidenceRequest(f.Person, new(2026, 11, 4), Guid.NewGuid(), "Sobrecarga explícita de prueba", clock.UtcNow.AddMinutes(2), [new(a.Id, "TRANSFER", f.OtherPerson)]);
        var p = await svc.PreviewAsync(f.Director, request, default);
        Check(!p.CanApply, "capacity shortage requires explicit exception");
        request = request with { Decisions = [new(a.Id, "TRANSFER", f.OtherPerson, true)] };
        p = await svc.PreviewAsync(f.Director, request, default);
        Check(p.CanApply && p.Changes.Single().OverloadAuthorized && p.Changes.Single().ExcessMinutes >= 30, "local overload exposes excess without reducing effort");
        await svc.ApplyAsync(f.Director, request, p.Fingerprint, default); db.ChangeTracker.Clear();
        Check((await db.OperationalAttachments.SingleAsync(e => e.AssignmentId == a.Id)).Content.SequenceEqual(new byte[] { 1, 2, 3 }), "transfer preserves evidence bytes");
        Check(await db.Set<OperationalCapacity>().AnyAsync(c => c.PersonId == f.OtherPerson && c.Percent == 0), "overload leaves global capacity unchanged");
        var absentRecipient = await QaFixtures.CreateAsync(db);
        var blocked = await Row(db, absentRecipient, clock, clock.UtcNow.AddHours(2));
        db.Add(new AvailabilityDayVersion(Guid.CreateVersion7(), absentRecipient.OtherPerson, BranchScope.LorettaId, request.Day, false, absentRecipient.Director)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        svc = new(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var invalid = request with { PersonId = absentRecipient.Person, Key = Guid.NewGuid(), Decisions = [new(blocked.Id, "TRANSFER", absentRecipient.OtherPerson, true)] };
        Check(!(await svc.PreviewAsync(absentRecipient.Director, invalid, default)).CanApply, "overload never waives recipient absence");
        var outside = await QaFixtures.CreateAsync(db);
        var longTask = await Row(db,outside,clock,clock.UtcNow.AddHours(1),30);
        var outsideRequest = request with { PersonId=outside.Person, Key=Guid.NewGuid(), NotBefore=OperationalSchedule.Instant(request.Day,22).AddMinutes(55), Decisions=[new(longTask.Id,"TRANSFER",outside.OtherPerson,true)] };
        Check(!(await svc.PreviewAsync(outside.Director,outsideRequest,default)).CanApply,"overload never waives shift end or required duration");

        var baseLedger = await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        var consumed = new OperationalAssignment {Id=Guid.NewGuid(),PersonId=outside.OtherPerson,LoadMinutes=120,StartsAt=clock.UtcNow.AddHours(-1),DueAt=clock.UtcNow,CompletedAt=clock.UtcNow};
        var spentLedger = new CapacityLedger(baseLedger.Workers,[consumed],baseLedger.Capacities,baseLedger.Unavailable,baseLedger.Legacy,baseLedger.Today) {Temporal=baseLedger.Temporal};
        Check(OperationalIncidenceService.RemainingCapacity(spentLedger,outside.OtherPerson,request.Day,clock.UtcNow)==120 && OperationalIncidenceService.Pending(spentLedger,outside.OtherPerson,request.Day)==0,
            "completed effort consumes daily budget but is not counted twice as pending");
        Check(OperationalIncidenceService.RemainingCapacity(spentLedger,outside.OtherPerson,request.Day,OperationalSchedule.Instant(request.Day,22).AddMinutes(30))==30,
            "remaining shift time caps the operating budget");
        consumed.CompletionKind="SUPERVISION";consumed.ReviewStatus="REASIGNADA";
        Check(OperationalIncidenceService.RemainingCapacity(spentLedger,outside.OtherPerson,request.Day,clock.UtcNow)==240,
            "administrative replacement is not counted as completed effort alongside its correction");

        var multi = await QaFixtures.CreateAsync(db);
        var weekly = await Row(db, multi, clock, clock.UtcNow.AddDays(-1), 80, 10, OperationalSchedule.Instant(new(2026, 11, 8), 18));
        svc = new(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var multiRequest = request with { PersonId = multi.Person, Key = Guid.NewGuid(), Decisions = [] };
        p = await svc.PreviewAsync(multi.Director, multiRequest, default);
        Check(p.CanApply && p.Changes.Single().Action == "REDISTRIBUTE", "multi-day obligation conserves deadline and bonus");
        var original = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == weekly.Id));
        await svc.ApplyAsync(multi.Director, multiRequest, p.Fingerprint, default); db.ChangeTracker.Clear();
        Check(original == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == weekly.Id)), "multi-day distribution changes availability, not obligation or deadline");
        Check(!await db.Set<TaskBonusExclusion>().AnyAsync(e => e.PersonId == multi.Person), "multi-day obligation not economically excluded");
        var multiOverload=await QaFixtures.CreateAsync(db);
        var impossible=await Row(db,multiOverload,clock,clock.UtcNow,600,10,OperationalSchedule.Instant(new(2026,11,5),9).AddMinutes(5));
        var overloadRequest=request with { PersonId=multiOverload.Person,Key=Guid.NewGuid(),Decisions=[new(impossible.Id,"TRANSFER",multiOverload.OtherPerson,true)] };
        Check(!(await svc.PreviewAsync(multiOverload.Director,overloadRequest,default)).CanApply,"multi-day overload cannot place daily effort outside remaining shifts or after deadline");

        var rollback = await QaFixtures.CreateAsync(db); var retired = await Row(db, rollback, clock, clock.UtcNow.AddHours(-1));
        svc = new(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var rollbackRequest = request with { PersonId = rollback.Person, Key = Guid.NewGuid(), Decisions = [] };
        p = await svc.PreviewAsync(rollback.Director, rollbackRequest, default);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION incidence_test_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'isolated commit failure'; END $$;
            CREATE CONSTRAINT TRIGGER incidence_test_rollback AFTER INSERT ON operational_incidence DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION incidence_test_fail();
            """);
        var failed = false;
        try { await svc.ApplyAsync(rollback.Director, rollbackRequest, p.Fingerprint, default); }
        catch (Npgsql.PostgresException) { failed = true; }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER incidence_test_rollback ON operational_incidence; DROP FUNCTION incidence_test_fail();"); }
        db.ChangeTracker.Clear();
        Check(failed && !await db.Set<OperationalIncidence>().AnyAsync(i => i.PersonId == rollback.Person) &&
            !await db.AvailabilityDayVersions.AnyAsync(i => i.PersonId == rollback.Person) &&
            !await db.Set<TaskBonusExclusion>().AnyAsync(i => i.PersonId == rollback.Person) && await db.OperationalAssignments.AnyAsync(i => i.Id == retired.Id), "deferred commit failure rolls back receipt, availability, retirement and bonus");

        var commitFixture = await QaFixtures.CreateAsync(db);
        var deadline = await Row(db, commitFixture, clock, clock.UtcNow.AddHours(1));
        var advance = new AdvanceOnCommit(clock);
        await using var commitDb = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(advance).Options, bonusClock: clock);
        var commitService = new OperationalIncidenceService(commitDb,clock,CorrectionSchedulingChecks.Service(commitDb,clock));
        var commitRequest = request with { PersonId=commitFixture.Person, Key=Guid.NewGuid(), Decisions=[] };
        var commitPreview = await commitService.PreviewAsync(commitFixture.Director,commitRequest,default);
        advance.AdvanceTo=deadline.DueAt;
        await Reject(async()=>await commitService.ApplyAsync(commitFixture.Director,commitRequest,commitPreview.Fingerprint,default),"deadline crossed inside commit callback rejects transaction");
        Check(!await db.Set<OperationalIncidence>().AnyAsync(i=>i.PersonId==commitFixture.Person) && await db.OperationalAssignments.AnyAsync(a=>a.Id==deadline.Id && a.PersonId==commitFixture.Person),"commit-time clock rejection leaves original assignment and no absence");
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
    }
}
