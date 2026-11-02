using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class TaskBonusChecks
{
    static async Task CoverageChecks(SgolDbContext db, Clock clock)
    {
        clock.UtcNow = At(2,10);
        var fixture = await QaFixtures.CreateAsync(db);
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var tasks = new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var bonus = new TaskBonusService(db,clock,new OperationalSupervisionService(db,clock,direction));
        var noBase = (await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(noBase.CalculationState == "INCIDENCIA" && noBase.BonusPercent == null,"B17/I: horario vigente con trabajo y sin base produce incidencia");
        var previousSchedule=await db.Set<PersonalScheduleVersion>().SingleAsync(s=>s.PersonId==fixture.Person);
        previousSchedule.Through=Week.AddDays(6);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(),PersonId=fixture.Person,Revision=2,From=Week.AddDays(7),ChangedAt=clock.UtcNow,
            ChangedBy=fixture.Director,IntentKey=Guid.NewGuid(),RequestHash="future rest",DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>Array.Empty<WorkInterval>())) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check((await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single().CalculationState == "INCIDENCIA", "B17/I: horario futuro de descanso no borra el trabajo de la semana actual");
        foreach(var day in CapacityLedger.Days(Week,Week.AddDays(6)))
            db.Add(new PersonalScheduleException { Id=Guid.CreateVersion7(),PersonId=fixture.Person,Day=day,Revision=1,IntervalsJson="[]",Reason="Ausencia aislada",ChangedAt=clock.UtcNow,ChangedBy=fixture.Director,IntentKey=Guid.NewGuid(),RequestHash="absence" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        noBase=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(noBase.ScorePercent==null && noBase.BonusPercent==0 && noBase.CalculationState=="SIN_TAREAS_EVALUABLES","B17/I: ausencias toda la semana y sin tareas no inventan calificación");
        var progress=new OperationalAssignment { Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,TaskCode="BONO-META",Name="Meta multidiaria",
            PersonId=fixture.OtherPerson,PersonName="Meta aislada",AssignedRole=CanonicalRole.SalesFloor,StartsAt=At(2,9),DueAt=At(4,14),
            Requirement="NINGUNO",OperationalCriticality="FLEXIBLE",ScheduleMode="PROGRESO",GoalUnits=10,ProgressUnit="unidades",LoadMinutes=10 };
        db.Add(progress); await db.SaveChangesAsync();db.ChangeTracker.Clear();
        try { await tasks.CompleteAsync(fixture.Other,progress.Id,null,null,default,"Aún incompleta"); throw new Exception("Expected goal restriction"); }
        catch(DirectionPlanException e) when(e.Code=="TASK_GOAL_PENDING") { Check(true,"B18/I: meta incompleta mantiene la validación operativa de entrega"); }
        db.ChangeTracker.Clear();
        await tasks.ProgressAsync(fixture.Other,progress.Id,1,5,"Primer avance",default);db.ChangeTracker.Clear();
        clock.UtcNow=At(4,14).AddMinutes(1);
        var partial=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single();
        Check(partial.DeductedPoints==0 && partial.Details.Single().SubmittedAt==null,"B18/I: avance no es envío y meta vencida por hora espera fin de su día autorizado");
        clock.UtcNow=At(5,10);
        Check((await bonus.ReadAsync(fixture.Other,Week,true,default)).Single().DeductedPoints==.5m,"B18/I: meta incompleta descuenta mitad al terminar el día de su plazo");
        await tasks.ProgressAsync(fixture.Other,progress.Id,2,5,"Meta terminada",default);db.ChangeTracker.Clear();
        await tasks.CompleteAsync(fixture.Other,progress.Id,null,null,default,"Enviada en recuperación");db.ChangeTracker.Clear();
        Check((await bonus.ReadAsync(fixture.Other,Week,true,default)).Single().DeductedPoints==.5m,"B05/B18/I: entrega real de progreso completo conserva medio punto sin duplicarlo");
        clock.UtcNow=At(2,10);
        var simultaneous=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,TaskCode="BONO-COMBINADO",Name="Cambio combinado",
            PersonId=fixture.OtherPerson,PersonName="Combinación aislada",AssignedRole=CanonicalRole.SalesFloor,StartsAt=At(2,9),DueAt=At(2,12),Requirement="NINGUNO",OperationalCriticality="IMPORTANTE",LoadMinutes=10};
        db.Add(simultaneous);await db.SaveChangesAsync();db.ChangeTracker.Clear();clock.UtcNow=At(3,10);
        await using(var combined=await db.Database.BeginTransactionAsync()) {
            await OperationalTaskService.LockAsync(db,default);
            var changed=await db.OperationalAssignments.SingleAsync(a=>a.Id==simultaneous.Id);
            changed.OperationalCriticality="CRITICA";changed.RetiredAt=clock.UtcNow;changed.Revision++;
            db.Add(new PersonalScheduleException {Id=Guid.CreateVersion7(),PersonId=fixture.OtherPerson,Day=Week.AddDays(1),Revision=1,IntervalsJson="[]",Reason="Ausencia del día actual",ChangedAt=clock.UtcNow,ChangedBy=fixture.Director,IntentKey=Guid.NewGuid(),RequestHash="simultaneous"});
            await db.SaveChangesAsync();await combined.CommitAsync();
        }
        db.ChangeTracker.Clear();
        var combinedResult=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single().Details.Single(d=>d.ObligationId==simultaneous.Id);
        Check(combinedResult.Points==5 && combinedResult.Deducted==5,"B21/I: clasificación, ausencia y cancelación simultáneas conservan los 5 puntos y el incumplimiento original");
        clock.UtcNow=At(2,10);
        var postponed=new OperationalAssignment { Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,TaskCode="BONO-ARRASTRE",Name="Arrastre sin perdón",
            PersonId=fixture.OtherPerson,PersonName="Arrastre aislado",AssignedRole=CanonicalRole.SalesFloor,StartsAt=At(2,9),DueAt=At(2,12),Requirement="NINGUNO",OperationalCriticality="FLEXIBLE",LoadMinutes=10 };
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        OperationalAgenda.Capture(postponed,policy,"FLEXIBLE",postponed.StartsAt);
        postponed.DueAt=postponed.StartsAt.AddMinutes(OperationalAgenda.Duration(postponed.LoadMinutes,policy.MarginPercent));
        db.Add(postponed);await db.SaveChangesAsync();db.ChangeTracker.Clear();clock.UtcNow=At(3,10);
        var carryover=new OperationalCarryoverService(db,clock,new OperationalSupervisionService(db,clock,direction));
        var proposal=await carryover.PreviewPostponementAsync(fixture.Other,[postponed.Id],"FALTA_TIEMPO_OPERATIVO","Arrastre aislado para verificar bono",default);
        var request=await carryover.SubmitPostponementAsync(fixture.Other,[postponed.Id],"FALTA_TIEMPO_OPERATIVO","Arrastre aislado para verificar bono",proposal.Fingerprint,Guid.CreateVersion7(),default);db.ChangeTracker.Clear();
        await carryover.DecideAsync(fixture.Director,request,1,true,"Autorización de prueba aislada",default);db.ChangeTracker.Clear();
        var moved=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single().Details.Single(d=>d.ObligationId==postponed.Id);
        Check(moved.Day==Week && moved.Deducted==.5m && moved.Points==1,"B11/I: arrastre autorizado real conserva día y descuento original sin base adicional");
        clock.UtcNow=At(2,10);
        var name="Seis repeticiones bono "+Guid.NewGuid().ToString("N");
        await tasks.SaveAsync(fixture.Director,new OperationalTask { Name=name,Description="Repeticiones verificadas",Requirement="NINGUNO",PeriodType="SEMANAL",Repetitions=6,LoadMinutes=10,DeadlineHours=24,OperationalCriticality="IMPORTANTE",AssignedRole=CanonicalRole.SalesFloor,Revision=0 },default);db.ChangeTracker.Clear();
        var definition=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name==name);
        var input=new DirectionPlanInput("Seis obligaciones de bono","Prueba aislada","SEMANAL",2026,45,[definition.TaskCode],SchedulingNotBefore:clock.UtcNow);
        input=await QaFixtures.ConfirmAsync(db,input,clock.UtcNow);
        var key=Guid.CreateVersion7();var saved=await direction.CreateAsync(fixture.Director,key,Guid.CreateVersion7(),input);db.ChangeTracker.Clear();
        var ids=saved.Plan.Assignments!.Select(a=>a.Id).ToArray();
        var facts=await db.Set<TaskBonusFact>().AsNoTracking().Where(f=>ids.Contains(f.AssignmentId)).ToArrayAsync();
        var obligations=TaskBonusService.Materialize(facts,[],[],At(9,12),At(9,12));
        Check(ids.Length==6 && obligations.Length==6 && obligations.Sum(o=>o.Points)==30,"B01/I: generador real crea seis repeticiones Importantes y exactamente 30 puntos");
        await direction.CreateAsync(fixture.Director,key,Guid.CreateVersion7(),input);db.ChangeTracker.Clear();
        Check(await db.Set<TaskBonusFact>().CountAsync(f=>ids.Contains(f.AssignmentId))==facts.Length,"B23/I: reintento del generador no duplica hechos del bono");
        clock.UtcNow=At(9,11,59);
        var lateExclusion=new TaskBonusExclusion {ObligationId=progress.Id,PersonId=fixture.OtherPerson,Week=Week,ActorId=fixture.Director,At=clock.UtcNow,Reason="Cruce de corte aislado"};
        var rejectedAtCommit=false;
        await using(var crossing=await db.Database.BeginTransactionAsync()) {
            await OperationalTaskService.LockAsync(db,default);db.Add(lateExclusion);await db.SaveChangesAsync();clock.UtcNow=At(9,12);
            try {await crossing.CommitAsync();}
            catch(DirectionPlanException e) when(e.Code=="BONUS_CLOSED") {rejectedAtCommit=true;await crossing.RollbackAsync();}
        }
        db.ChangeTracker.Clear();
        Check(rejectedAtCommit&&!await db.Set<TaskBonusExclusion>().AnyAsync(e=>e.Id==lateExclusion.Id),"B24/I: excepción iniciada 11:59 y confirmada 12:00 se revierte íntegramente");
        clock.UtcNow=At(9,13);
        var registrations=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        registrations.AddSingleton<Sgol.BuildingBlocks.Time.IClock>(clock);
        registrations.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));
        registrations.AddScoped<TaskBonusService>(sp=> {
            var context=sp.GetRequiredService<SgolDbContext>();
            return new(context,clock,CorrectionSchedulingChecks.Service(context,clock));
        });
        await using var provider=registrations.BuildServiceProvider();
        using var worker=new TaskBonusClosingWorker(provider.GetRequiredService<IServiceScopeFactory>(),Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskBonusClosingWorker>.Instance);
        await worker.StartAsync(default);
        for(var attempt=0;attempt<100&&!await db.Set<TaskBonusClosure>().AnyAsync(c=>c.PersonId==fixture.Person&&c.Week==Week);attempt++) await Task.Delay(100);
        await worker.StopAsync(default);
        var closed=await db.Set<TaskBonusClosure>().AsNoTracking().SingleAsync(c=>c.PersonId==fixture.Person&&c.Week==Week);
        Check(JsonSerializer.Deserialize<TaskBonusResult>(closed.ResultJson)!.Closed,"B25/I: worker real arrancado después del corte materializa el cierre pendiente");
    }
}
