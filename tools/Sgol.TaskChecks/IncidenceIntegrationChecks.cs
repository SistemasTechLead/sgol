using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    public static async Task IntegrationChecks(SgolDbContext source, bool ui)
    {
        var clock=new Clock();
        await using var db=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options,bonusClock:clock);
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var tasks=new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var supervision=CorrectionSchedulingChecks.Service(db,clock);
        var incidents=new OperationalIncidenceService(db,clock,supervision);
        var bonus=new TaskBonusService(db,clock,supervision);
        IncidenceRequest Request(Guid person,DateOnly? day=null)=>new(person,day??new(2026,11,4),Guid.NewGuid(),"Integración final aislada",day is {} d?OperationalSchedule.Instant(d):clock.UtcNow.AddMinutes(2),[]);

        foreach(var status in new[]{"PENDIENTE_REVISION","CONCLUIDA","NO_SATISFECHA","REASIGNADA"})
        {
            var f=await QaFixtures.CreateAsync(db);
            var a=await Row(db,f,clock,clock.UtcNow.AddMinutes(-20));
            await tasks.CompleteAsync(f.Worker,a.Id,null,null,default,"Entrega histórica comprobada");db.ChangeTracker.Clear();
            if(status!="PENDIENTE_REVISION")
            {
                var row=await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==a.Id);
                await supervision.ReviewAsync(f.Director,a.Id,row.Revision,status=="REASIGNADA"?"MISMA_PERSONA":status,"Resolución histórica aislada",default);db.ChangeTracker.Clear();
            }
            var before=JsonSerializer.Serialize(await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(x=>x.PlanId==f.Plan).OrderBy(x=>x.Id).ToArrayAsync());
            var p=await incidents.PreviewAsync(f.Director,Request(f.Person),default);
            Check(!p.CanApply && p.Cases.Any(c=>c.EntityId==a.Id && c.Cause.Contains("entregado")),"integration historical "+status+": whole-day contradiction identified");
            await Reject(async()=>await incidents.ApplyAsync(f.Director,p.Request,p.Fingerprint,default),"integration historical "+status+": no exclusion of completed work");
            Check(before==JsonSerializer.Serialize(await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(x=>x.PlanId==f.Plan).OrderBy(x=>x.Id).ToArrayAsync()),"integration historical "+status+": assignments and correction preserved");
        }

        var economic=await QaFixtures.CreateAsync(db);
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
        await Row(db,economic,clock,clock.UtcNow.AddHours(-1));
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,9),12);
        var closed=(await bonus.ReadAsync(economic.Worker,new(2026,11,2),true,default)).Single();
        var closureBefore=JsonSerializer.Serialize(await db.Set<TaskBonusClosure>().AsNoTracking().Where(c=>c.PersonId==economic.Person).ToArrayAsync());
        Check(await db.Set<TaskBonusClosure>().AnyAsync(c=>c.PersonId==economic.Person),"integration real bonus read persists weekly closure");
        var next=await Row(db,economic,clock,clock.UtcNow.AddHours(1));
        var request=Request(economic.Person,new(2026,11,9)) with {NotBefore=clock.UtcNow.AddMinutes(2)};
        var preview=await incidents.PreviewAsync(economic.Director,request,default);
        Check(preview.CanApply,"integration next-week absence can follow a real persisted closure");
        await incidents.ApplyAsync(economic.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
        Check(closureBefore==JsonSerializer.Serialize(await db.Set<TaskBonusClosure>().AsNoTracking().Where(c=>c.PersonId==economic.Person).ToArrayAsync()) &&
            (await bonus.ReadAsync(economic.Worker,new(2026,11,2),true,default)).Single().ResultVersion==closed.ResultVersion,
            "integration incidence and bonus reread leave existing weekly closure byte-identical");

        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,30),10);
        var monthly=await QaFixtures.CreateAsync(db);
        var m=await Row(db,monthly,clock,clock.UtcNow,80,10,OperationalSchedule.Instant(new(2026,12,2),18));
        var mr=await db.OperationalAssignments.SingleAsync(a=>a.Id==m.Id);mr.PeriodType="MENSUAL";mr.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var original=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==m.Id));
        request=Request(monthly.Person,new(2026,11,30)) with {NotBefore=clock.UtcNow.AddMinutes(2)};
        preview=await incidents.PreviewAsync(monthly.Director,request,default);
        Check(preview.CanApply && preview.Changes.Single().Action=="REDISTRIBUTE","integration monthly obligation redistributes remaining effort across month boundary");
        await incidents.ApplyAsync(monthly.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
        clock.UtcNow=OperationalSchedule.Instant(new(2026,12,1),10);
        Check(original==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==m.Id)) &&
            !await db.Set<TaskBonusExclusion>().AnyAsync(e=>e.ObligationId==m.Id) &&
            (await bonus.ReadAsync(monthly.Worker,new(2026,11,30),true,default)).Single().Details.Any(d=>d.ObligationId==m.Id && d.Points>0),
            "integration monthly obligation keeps owner, deadline, effort and bonus after rollover");

        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
        foreach(var mode in new[]{"missing","trial","qualified"})
        {
            var f=await QaFixtures.CreateAsync(db);var a=await Row(db,f,clock,clock.UtcNow.AddHours(1));
            var spec=new Specialization{Id=Guid.NewGuid(),Name="Incidence specialty "+Guid.NewGuid(),NormalizedName=Guid.NewGuid().ToString(),Active=true};db.Add(spec);
            var row=await db.OperationalAssignments.SingleAsync(x=>x.Id==a.Id);row.SpecializationId=spec.Id;row.SpecializationName=spec.Name;row.Revision++;
            if(mode!="missing")db.Add(new PersonSpecialization{PersonId=f.OtherPerson,SpecializationId=spec.Id});
            if(mode=="trial")(await db.People.SingleAsync(p=>p.Id==f.OtherPerson)).IsOnTrial=true;
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
            request=Request(f.Person) with {Decisions=[new(a.Id,"TRANSFER",f.OtherPerson,true)]};
            preview=await incidents.PreviewAsync(f.Director,request,default);
            Check(preview.CanApply==(mode=="qualified"),"integration specialty "+mode+": overload does not bypass qualification or trial");
            if(preview.CanApply)await incidents.ApplyAsync(f.Director,request,preview.Fingerprint,default);
            else await Reject(async()=>await incidents.ApplyAsync(f.Director,request,preview.Fingerprint,default),"integration specialty "+mode+": no partial commit");
        }

        var race=await QaFixtures.CreateAsync(db);var second=await QaFixtures.CreateAsync(db);
        await Row(db,race,clock,clock.UtcNow.AddHours(-1));
        var r1=Request(race.Person);var r2=r1 with {Key=Guid.NewGuid(),Reason="Otra Dirección confirma simultáneamente"};
        var p1=await incidents.PreviewAsync(race.Director,r1,default);var p2=await incidents.PreviewAsync(second.Director,r2,default);
        async Task<bool> Confirm(Guid actor,IncidenceRequest req,string hash)
        {
            await using var ctx=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options,bonusClock:clock);
            try{await new OperationalIncidenceService(ctx,clock,CorrectionSchedulingChecks.Service(ctx,clock)).ApplyAsync(actor,req,hash,default);return true;}
            catch(DirectionPlanException){return false;}
            catch(Npgsql.PostgresException e) when(e.SqlState=="40001"){return false;}
            catch(DbUpdateException e) when(e.InnerException is Npgsql.PostgresException {SqlState:"40001" or "23505"}){return false;}
        }
        var results=await Task.WhenAll(Confirm(race.Director,r1,p1.Fingerprint),Confirm(second.Director,r2,p2.Fingerprint));
        Check(results.Count(x=>x)==1 && await db.Set<OperationalIncidence>().CountAsync(x=>x.PersonId==race.Person)==1,
            "integration two distinct directors and keys race: exactly one absence and receipt");

        var bounded=await QaFixtures.CreateAsync(db);
        db.Add(new PersonalScheduleException{Id=Guid.NewGuid(),PersonId=bounded.OtherPerson,Day=new(2026,11,4),Revision=1,
            IntervalsJson=JsonSerializer.Serialize(new[]{new WorkInterval(600,720)}),ChangedAt=clock.UtcNow,ChangedBy=bounded.Director,Reason="Bounded search fixture",IntentKey=Guid.NewGuid(),RequestHash="fixture"});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var jobs=new List<OperationalAssignment>();for(var i=0;i<9;i++)jobs.Add(await Row(db,bounded,clock,clock.UtcNow.AddMinutes(10+i),10));
        request=Request(bounded.Person) with {Decisions=jobs.Select(j=>new IncidenceDecision(j.Id,"TRANSFER",bounded.OtherPerson)).ToArray()};
        preview=await incidents.PreviewAsync(bounded.Director,request,default);
        Check(!preview.CanApply && preview.Cases.Any(c=>c.Cause.Contains("20,000")),"integration real 20000-step search exhaustion reports inconclusive, not impossible");
        await Reject(async()=>await incidents.ApplyAsync(bounded.Director,request,preview.Fingerprint,default),"integration exhausted search commits nothing");
        Check(!await db.Set<OperationalIncidence>().AnyAsync(i=>i.PersonId==bounded.Person) && await db.OperationalAssignments.CountAsync(a=>a.PersonId==bounded.Person)==9,
            "integration exhausted search preserves all assignments and availability");
        await IntegrationProducerChecks(db,clock,direction,tasks,incidents);
        if(ui) await IntegrationHttpChecks(db,clock);
    }

    static async Task IntegrationProducerChecks(SgolDbContext db, Clock clock, EfDirectionPreparedPlanService direction,
        OperationalTaskService tasks, OperationalIncidenceService incidents)
    {
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
        var f=await QaFixtures.CreateAsync(db);var day=new DateOnly(2026,11,4);
        var schedule=new PersonalScheduleService(db,direction,clock);
        var row=(await schedule.ReadClosingAsync(f.Director,day,default))[0];
        await schedule.SaveClosingAsync(f.Director,new(){Day=day,PersonId=f.Person,Revision=row.Revision},default);db.ChangeTracker.Clear();
        var absent=new IncidenceRequest(f.Person,day,Guid.NewGuid(),"Ausencia de cobertura de cierre",clock.UtcNow.AddMinutes(2),[]);
        var preview=await incidents.PreviewAsync(f.Director,absent,default);
        Check(preview.CanApply,"integration absence with closing coverage and no active task is reviewable");
        await incidents.ApplyAsync(f.Director,absent,preview.Fingerprint,default);db.ChangeTracker.Clear();
        row=(await schedule.ReadClosingAsync(f.Director,day,default))[0];
        Check(row.Candidates.All(p=>p.PersonId!=f.Person),"integration closing selection excludes confirmed absent person");
        await Reject(()=>schedule.SaveClosingAsync(f.Director,new(){Day=day,PersonId=f.Person,Revision=row.Revision},default),"integration closing producer refuses absent person");
        await schedule.SaveClosingAsync(f.Director,new(){Day=day,PersonId=f.OtherPerson,Revision=row.Revision},default);db.ChangeTracker.Clear();
        Check((await schedule.ReadClosingAsync(f.Director,day,default))[0].PersonId==f.OtherPerson,"integration explicit replacement restores closing coverage");
        var definition=await db.OperationalTasks.SingleAsync(t=>t.Id==f.Definition);definition.PeriodType="OCASIONAL";definition.Revision++;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var occasion=new OccasionInput(definition.Id,definition.Revision,"Asignación incompatible","Instrucciones de prueba","","",clock.UtcNow.AddHours(1),clock.UtcNow.AddHours(2),[f.Person]);
        var before=await db.OperationalAssignments.CountAsync();var intent=Guid.NewGuid();
        await Reject(async()=>await tasks.AssignOccasionAsync(f.Director,intent,occasion,[],default),"integration occasional producer refuses confirmed absence");
        db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync()==before && !await db.Set<OperationalOccasion>().AnyAsync(o=>o.Id==intent),"integration failed occasional producer writes no partial assignment");
        var success=Guid.NewGuid();var eligible=occasion with {PersonIds=[f.OtherPerson]};
        var scheduled=await tasks.PreviewOccasionAsync(f.Director,eligible,default);
        await tasks.AssignOccasionAsync(f.Director,success,eligible with {SchedulingFingerprint=scheduled.Fingerprint,SchedulingNotBefore=scheduled.NotBefore},[],default);db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(a=>a.OccasionId==success && a.PersonId==f.OtherPerson),"integration same occasional producer succeeds for eligible recipient");
    }
}
