using Sgol.Web.Infrastructure.Persistence.Configuration;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class CarryoverChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static DateTimeOffset At(int h, int m = 0) => TemporalEligibilityContext.At(new(2026,10,22), h * 60 + m);
    private static OperationalAssignment Row(int h, int m, int endH, int endM) => new() { Id = Guid.NewGuid(), StartsAt = At(h,m), DueAt = At(endH,endM), Name = "Ejemplo", Revision = 1 };
    private static CarryoverItem[] Calc(OperationalAssignment[] rows, DateTimeOffset end, int minutes, WorkInterval[]? windows = null) => OperationalCarryoverCalculator.Calculate(rows, end, minutes, windows ?? [new(480,1200)]);
    public static void Rules()
    {
        var a = Row(14,0,14,30); var b = Row(14,30,15,0); var c = Row(18,30,19,0);
        var output = Calc([a,b,c], At(14,40), 30);
        Require(output.Length == 2 && output.Single(i=>i.AssignmentId==a.Id).Proposed == At(15) && output.Single(i=>i.AssignmentId==b.Id).Proposed == At(15,30), "Agreed 30-minute example and full gap");
        a = Row(18,0,18,30); b = Row(18,30,19,0);
        output = Calc([a,b], At(18,20), 10, [new(480,1140)]);
        Require(output[0].Proposed == At(18,40) && output[1].Proposed == At(19,10) && output[1].Warning.Contains("fuera de turno"), "10-minute example and shift warning");
        a = Row(14,30,15,0); b = Row(15,20,15,50); c = Row(16,0,16,30);
        output = Calc([a,b,c], At(14,50), 30);
        Require(output[1].Proposed == At(16) && output.Length == 2, "Partial gap and exact proposed frontier contact");
        b = Row(15,30,16,0); Require(Calc([a,b], At(14,50), 30).Length == 1,"Exact full gap");
        b = Row(14,30,15,0); c = Row(15,0,15,30); var d = Row(15,30,16,0);
        output = Calc([a,b,c,d],At(14,50),30);
        Require(output.Length==4 && output.All(i=>i.Minutes==30),"Parallel tasks and three-link chain do not multiply delay");
        Require(JsonSerializer.Serialize(output) == JsonSerializer.Serialize(Calc([d,c,b,a],At(14,50),30)), "Input-order independence");
        a.CompletedAt=At(14,45); Require(Calc([a,b],At(14,50),30).Length==1,"Delivered before submission excluded");
        a=Row(13,0,14,0); b=Row(14,30,15,0); Require(Calc([a,b],At(14,30),30).Length==0,"Incident endpoint contact excluded and earlier expiry excluded");
        a=Row(14,0,14,30); b=Row(15,30,16,0);
        output=Calc([a,b],At(14,20),10,[new(480,870),new(930,1200)]);
        Require(output.Length==2 && output[1].Minutes==10,"Break never absorbs propagation");
        a=Row(23,20,23,50); output=Calc([a],At(23,40),30,[new(480,1440)]);
        Require(output[0].Warning.Contains("siguiente día"),"Midnight explicit conflict");
        a=Row(14,0,14,30); a.ScheduleMode="PROGRESO"; Require(Calc([a],At(14,20),10).Length==0,"Progress cannot bridge");
        Console.WriteLine("PASS carryover rules: agreed examples, partial/full gaps, endpoints, parallel/group order, chains, delivered/expired exclusions, breaks, midnight, PROGRESO.");
    }
    private static OperationalCarryoverService Service(SgolDbContext db, IClock clock) {
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        return new(db,clock,new OperationalSupervisionService(db,clock,direction));
    }
    private static async Task Rejected(Func<Task> action, string code) {
        try { await action(); throw new Exception("Expected rejection: "+code); } catch(DirectionPlanException e) { Require(e.Code==code,"Expected "+code+", got "+e.Code); }
    }
    public static async Task SeedUi(SgolDbContext db)
    {
        var clock = new TestClock { UtcNow = DateTimeOffset.UtcNow };
        var service = Service(db, clock);
        var rows = await db.OperationalAssignments.AsNoTracking().Where(a => a.CompletedAt == null && a.ReviewStatus != "REASIGNADA").OrderBy(a => a.StartsAt).ToArrayAsync();
        var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
        foreach (var row in rows.Where(a => WeekContract.LocalToday(a.StartsAt) == WeekContract.LocalToday(clock.UtcNow) && a.StartsAt.AddMinutes(1) < clock.UtcNow)) {
            var user = users.FirstOrDefault(u => u.PersonId == row.PersonId); if (user == null) continue;
            try {
                var p = await service.PreviewAsync(user.Id, row.StartsAt.AddMinutes(1), 1, "FALLA_SISTEMA_HERRAMIENTA", "Prueba UI exclusivamente en copia aislada", default);
                if (p.Conflicts.Length != 0) continue;
                await service.SubmitAsync(user.Id,p.End,p.Minutes,p.Cause,p.Explanation,p.Fingerprint,Guid.NewGuid(),default);
                Console.WriteLine($"UI snapshot seeded: {row.PersonName}, {p.Items.Length} frozen assignments"); return;
            } catch (DirectionPlanException) { }
        }
        throw new Exception("No eligible current snapshot for UI");
    }
    public static async Task Run(SgolDbContext db)
    {
        Rules();
        var clock = new TestClock { UtcNow = At(19,0) };
        var service = Service(db,clock);
        var original = await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();
        var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
        var supervisor = await db.RoleAssignmentVersions.Where(r=>r.RoleCode=="DIRECCION" && r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        CarryoverPreview? preview = null; Guid actor=default;
        foreach(var row in original.Where(a=>a.CompletedAt==null && a.ReviewStatus is not ("CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA") && WeekContract.LocalToday(a.StartsAt)==new DateOnly(2026,10,22) && a.StartsAt<At(19))) {
            var user=users.FirstOrDefault(u=>u.PersonId==row.PersonId); if(user==null || user.Id==supervisor) continue;
            clock.UtcNow=row.StartsAt.AddMinutes(2);
            try {
                var p=await service.PreviewAsync(user.Id,row.StartsAt.AddMinutes(1),1,"FALLA_SISTEMA_HERRAMIENTA","Prueba en copia restaurada",default);
                if(p.Conflicts.Length==0) { preview=p; actor=user.Id; if(p.Items.Length>1) break; }
                else Console.WriteLine("Snapshot candidate "+row.TaskCode+": "+string.Join("; ",p.Conflicts));
            } catch(DirectionPlanException) { }
        }
        Require(preview!=null,"At least one unmodified current assignment must support an incident");
        clock.UtcNow=preview!.End.AddMinutes(1);
        preview=await service.PreviewAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,default);
        await db.AppUsers.Where(u => u.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "INACTIVA"));
        await Rejected(()=>service.PreviewAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,default),"ACCESO_DENEGADO");
        await db.AppUsers.Where(u => u.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "ACTIVA"));
        var before=JsonSerializer.Serialize(original);
        var intent=Guid.NewGuid();
        var id=await service.SubmitAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,preview.Fingerprint,intent,default);
        Require(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Submitting preserves every assignment field");
        Require(id==await service.SubmitAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,preview.Fingerprint,intent,default),"Submission replay is idempotent");
        var inbox=await service.InboxAsync(supervisor,default); Require(inbox.Count(r=>r.State=="PENDIENTE")==1,"Authorized inbox count");
        var overlap=await service.PreviewAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,default); Require(overlap.Conflicts.Any(c=>c.Contains("solapa")),"Pending overlap rejected");
        await Rejected(()=>service.DecideAsync(actor,id,1,true,"Auto",default),"ACCESO_DENEGADO");
        await Rejected(()=>service.DecideAsync(supervisor,id,1,true,"",default),"ARRASTRE_COMENTARIO_REQUERIDO");
        await service.DecideAsync(supervisor,id,1,false,"Rechazo de prueba",default);
        Require(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Rejection preserves every assignment field");
        Require((await service.InboxAsync(supervisor,default)).Count(r=>r.State=="PENDIENTE")==0,"Last decision changes pending count");
        preview=await service.PreviewAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,default);
        id=await service.SubmitAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,preview.Fingerprint,Guid.NewGuid(),default);
        // A valid preview cannot silently survive a structural date change.
        var changed=await db.OperationalAssignments.SingleAsync(a=>a.Id==preview.Items[0].AssignmentId);
        changed.DueAt=changed.DueAt.AddMinutes(1); changed.Revision++; await db.SaveChangesAsync();
        var changedDue=changed.DueAt;
        await Rejected(()=>service.DecideAsync(supervisor,id,1,true,"Stale",default),"ARRASTRE_CONFLICTO");
        db.ChangeTracker.Clear(); Require((await db.OperationalAssignments.SingleAsync(a=>a.Id==changed.Id)).DueAt==changedDue,"Conflict has no partial changes");
        changed=await db.OperationalAssignments.SingleAsync(a=>a.Id==changed.Id); changed.DueAt=preview.Items[0].Due; changed.Revision++; await db.SaveChangesAsync();
        // Delivery and final quality decision while authorization is pending.
        changed.CompletedAt=changed.DueAt.AddSeconds(30); changed.CompletedBy=actor; changed.CompletionKind="ENTREGA"; changed.ReviewStatus="CONCLUIDA"; changed.ReviewDecision="CONCLUIDA"; changed.ReviewedBy=supervisor; changed.ReviewedAt=changed.CompletedAt; changed.Revision++; await db.SaveChangesAsync();
        var preserved=JsonSerializer.Serialize(changed);
        db.ChangeTracker.Clear();
        await service.DecideAsync(supervisor,id,1,true,"Autorizado en copia",default);
        db.ChangeTracker.Clear();
        Require(preserved==JsonSerializer.Serialize(await db.OperationalAssignments.SingleAsync(a=>a.Id==changed.Id)),"Delivery and quality state preserved exactly");
        var request=await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==id);
        Require(request.Results().Single(r=>r.AssignmentId==changed.Id).Kind=="RECONOCIMIENTO","Recognition instead of modifying delivered deadline");
        try {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_carryover_request SET \"Explanation\" = 'rewrite' WHERE \"Id\" = {id}");
            throw new Exception("Database accepted terminal history rewrite");
        } catch(Npgsql.PostgresException e) { Require(e.SqlState == "P0001", "Immutable history trigger"); }
        var reader=new OperationalCarryoverReader(db); var timing=await reader.ForAsync(changed.Id);
        Require(timing.Label(changed.DueAt,changed.CompletedAt).Contains("Entrega tardía con arrastre autorizado") && timing.Label(changed.DueAt,changed.CompletedAt).Contains("dentro del plazo"),"Historical timeliness and recognized coverage");
        var after=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();
        var affected=preview.Items.Select(i=>i.AssignmentId).ToHashSet();
        Require(JsonSerializer.Serialize(original.Where(a=>!affected.Contains(a.Id)))==JsonSerializer.Serialize(after.Where(a=>!affected.Contains(a.Id))),"Every field outside frozen scope preserved");
        foreach(var item in preview.Items.Where(i=>i.AssignmentId!=changed.Id)) Require(after.Single(a=>a.Id==item.AssignmentId).DueAt==item.Proposed,"Pending deadline equals submitted preview");
        var audits=await db.AuditEvents.CountAsync(); await service.DecideAsync(supervisor,id,1,true,"Autorizado en copia",default);
        Require(await db.AuditEvents.CountAsync()==audits,"Decision replay cannot duplicate audit or extension");
        await Rejected(()=>service.DecideAsync(supervisor,id,1,false,"Reverse",default),"ARRASTRE_CONFLICTO");
        overlap=await service.PreviewAsync(actor,preview.End,preview.Minutes,preview.Cause,preview.Explanation,default); Require(overlap.Conflicts.Any(c=>c.Contains("solapa")),"Approved overlap rejected");
        // A second, independent incident uses current effective deadlines. Two separate contexts race for one decision.
        var secondEnd = preview.Items.First(i => i.AssignmentId != changed.Id).Start.AddMinutes(1);
        if (secondEnd <= preview.End.AddMinutes(1)) secondEnd = preview.End.AddMinutes(2);
        clock.UtcNow = secondEnd.AddMinutes(1);
        var second = await service.PreviewAsync(actor, secondEnd, 1, preview.Cause, "Segundo incidente", default);
        Require(second.Conflicts.Length == 0, "Independent second incident is eligible: " + string.Join("; ", second.Conflicts));
        var secondId = await service.SubmitAsync(actor, secondEnd, 1, second.Cause, second.Explanation, second.Fingerprint, Guid.NewGuid(), default);
        var connection = db.Database.GetConnectionString()!;
        async Task<bool> Race(bool approve) {
            await using var racingDb = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options);
            try { await Service(racingDb, clock).DecideAsync(supervisor, secondId, 1, approve, approve ? "Concurrent approve" : "Concurrent reject", default); return true; }
            catch(DirectionPlanException e) when(e.Code=="ARRASTRE_CONFLICTO") { return false; }
        }
        var race = await Task.WhenAll(Race(true), Race(false));
        Require(race.Count(x=>x)==1,"Exactly one concurrent decision commits");
        Require(await db.AuditEvents.CountAsync(a=>a.ResourceId==secondId && (a.Action=="OPERATIONAL_CARRYOVER_APPROVED" || a.Action=="OPERATIONAL_CARRYOVER_REJECTED"))==1,"One terminal audit under concurrent decisions");
        await Rejected(()=>service.PreviewAsync(actor,At(23),1,preview.Cause,"Future",default),"ARRASTRE_DATOS_INVALIDOS");
        Console.WriteLine("PASS carryover concurrency: independent second incident, two simultaneous decisions with separate contexts, one commit and one terminal audit; future incident rejected.");
        Console.WriteLine($"PASS restored-current lifecycle: {original.Length} assignments, {preview.Items.Length} frozen items; unchanged submission/rejection, idempotency, permission, overlap, structural conflict atomicity, concurrent delivery/review recognition, exact outside-scope preservation, terminal decision protection.");
    }
}

