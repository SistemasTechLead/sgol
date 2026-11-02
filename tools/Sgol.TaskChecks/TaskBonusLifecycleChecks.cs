using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class TaskBonusChecks
{
    static async Task LifecycleChecks(SgolDbContext db, Clock clock)
    {
        clock.UtcNow=At(2,10);
        var fixture=await QaFixtures.CreateAsync(db);
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var operational=new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var supervision=new OperationalSupervisionService(db,clock,direction);
        var bonus=new TaskBonusService(db,clock,supervision);
        async Task<OperationalAssignment> Add(string name,int day=2,string criticality="IMPORTANTE") {
            var row=new OperationalAssignment{Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=fixture.Definition,PersonId=fixture.Person,PersonName="Origen ciclo",AssignedRole=CanonicalRole.SalesFloor,
                TaskCode="BC-"+Guid.NewGuid().ToString("N")[..8],Name=name,StartsAt=At(day,9),DueAt=At(day,12),Requirement="NINGUNO",OperationalCriticality=criticality,LoadMinutes=10};
            db.Add(row);await db.SaveChangesAsync();db.ChangeTracker.Clear();return row;
        }
        var moved=await Add("Transferencia puntual");
        var request=new TransferRequest(moved.Id,1,fixture.OtherPerson,10,At(3,12),"Cambio autorizado antes de incumplir");
        var preview=await supervision.PreviewTransferAsync(fixture.Director,request,default);
        await supervision.TransferAsync(fixture.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
        var own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        var receiver=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single();
        Check(!own.Details.Any(d=>d.ObligationId==moved.Id)&&receiver.Details.Single(d=>d.ObligationId==moved.Id).Day==new DateOnly(2026,11,3),"B12/I: servicio de transferencia pasa base y plazo al receptor");
        var late=await Add("Transferencia vencida");clock.UtcNow=At(3,10);
        request=new(late.Id,1,fixture.OtherPerson,10,At(3,12),"Recuperación posterior al incumplimiento");
        preview=await supervision.PreviewTransferAsync(fixture.Director,request,default);
        await supervision.TransferAsync(fixture.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
        await operational.CompleteAsync(fixture.Other,late.Id,null,null,default,"Recuperada por receptor");db.ChangeTracker.Clear();
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();receiver=(await bonus.ReadAsync(fixture.Other,Week,true,default)).Single();
        Check(own.Details.Single(d=>d.ObligationId==late.Id).Deducted==5&&!receiver.Details.Any(d=>d.ObligationId==late.Id),"B13/I: entrega real del receptor no borra ni duplica incumplimiento original");
        clock.UtcNow=At(2,10);var correction=await Add("Devolución sin corrección semanal",2,"FLEXIBLE");
        await operational.CompleteAsync(fixture.Worker,correction.Id,null,null,default,"Primera entrega");db.ChangeTracker.Clear();
        var sent=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        var correctionId=await supervision.ReviewAsync(fixture.Director,correction.Id,sent.Revision,"MISMA_PERSONA","Corregir antes del cierre",default);db.ChangeTracker.Clear();
        Check(correctionId!=null,"B10/I: revisión real crea corrección enlazada");
        clock.UtcNow=At(9,10);
        await operational.CompleteAsync(fixture.Worker,correctionId!.Value,null,null,default,"Corrección del lunes");db.ChangeTracker.Clear();
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(own.Details.Single(d=>d.ObligationId==correction.Id).Deducted==1&&own.Details.Count(d=>d.ObligationId==correction.Id)==1,"B10/B11/I: corrección real del lunes pierde total sin base nueva");
        clock.UtcNow=At(2,10);var admin=await Add("Conclusión administrativa");
        await supervision.ReviewAsync(fixture.Director,admin.Id,1,"CONCLUIDA",null,default);db.ChangeTracker.Clear();
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(own.Details.Single(d=>d.ObligationId==admin.Id).Deducted==0&&own.Details.Single(d=>d.ObligationId==admin.Id).SubmittedAt==clock.UtcNow,"Conclusión administrativa real acredita fecha sin cambiar CompletionKind");
        var future=await Add("Clasificación congelada",3,"IMPORTANTE");
        var editable=await db.OperationalAssignments.SingleAsync(a=>a.Id==future.Id);editable.OperationalCriticality="CRITICA";editable.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(own.Details.Single(d=>d.ObligationId==future.Id).Points==5,"B21/I: cambio posterior no recalifica obligación capturada");
        var cancelled=await Add("Cancelación anterior",3);var retired=await db.OperationalAssignments.SingleAsync(a=>a.Id==cancelled.Id);retired.RetiredAt=clock.UtcNow;retired.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();
        Check(own.Details.Single(d=>d.ObligationId==cancelled.Id).Points==0,"B14/I: retirada persistida anterior excluye y conserva trazabilidad");
        var rollbackId=Guid.Empty;
        await using(var tx=await db.Database.BeginTransactionAsync()) {var row=await Add("Rollback económico");rollbackId=row.Id;await tx.RollbackAsync();}
        db.ChangeTracker.Clear();Check(!await db.Set<TaskBonusFact>().AnyAsync(f=>f.AssignmentId==rollbackId),"B24/I: rollback operativo también revierte hechos económicos");
        var crossing = await Add("Revisión confirmada después del corte");
        await operational.CompleteAsync(fixture.Worker, crossing.Id, null, null, default, "Envío anterior al cierre"); db.ChangeTracker.Clear();
        clock.UtcNow = At(9,11).AddMinutes(59);
        await using (var tx = await db.Database.BeginTransactionAsync()) {
            await OperationalTaskService.LockAsync(db, default);
            var changed = await db.OperationalAssignments.SingleAsync(a => a.Id == crossing.Id);
            changed.ReviewStatus = "NO_SATISFECHA"; changed.ReviewedAt = clock.UtcNow; changed.ReviewedBy = fixture.Director; changed.Revision++;
            await db.SaveChangesAsync();
            clock.UtcNow = At(9,12);
            await tx.CommitAsync();
        }
        db.ChangeTracker.Clear();
        own = (await bonus.ReadAsync(fixture.Worker, Week, true, default)).Single();
        Check(own.Details.Single(d => d.ObligationId == crossing.Id).Deducted == 0 &&
            (await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == crossing.Id)).ReviewStatus == "NO_SATISFECHA",
            "B24/I: revisión guardada 11:59 y confirmada 12:00 conserva operación pero no modifica cierre económico");
        clock.UtcNow=At(9,12);
        async Task<TaskBonusResult> ConcurrentRead() {
            await using var context=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options,bonusClock:clock);
            return (await new TaskBonusService(context,clock,CorrectionSchedulingChecks.Service(context,clock)).ReadAsync(fixture.Worker,Week,true,default)).Single();
        }
        var concurrent=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>ConcurrentRead()));
        Check(concurrent.Select(r=>r.ResultVersion).Distinct().Count()==1 && await db.Set<TaskBonusClosure>().CountAsync(c=>c.PersonId==fixture.Person&&c.Week==Week)==1,"B23/B24/I: cuatro cierres concurrentes producen una cabecera y detalle idéntico");
        // Delete using the real authorized preview/confirm flow; immutable facts survive cascades.
        var deletion=await direction.PreviewDeletionAsync(fixture.Director,fixture.Plan,default);
        await direction.DeleteAsync(fixture.Director,fixture.Plan,Guid.NewGuid(),Guid.NewGuid(),deletion.Fingerprint,default);db.ChangeTracker.Clear();
        Check(!await db.OperationalAssignments.IgnoreQueryFilters().AnyAsync(a=>a.PlanId==fixture.Plan) && await db.Set<TaskBonusFact>().AnyAsync(f=>f.AssignmentId==moved.Id&&f.Kind=="HANDOFF"),"B14/B23/I: eliminación real conserva obligación, transferencia y cierre económico");
        own=(await bonus.ReadAsync(fixture.Worker,Week,true,default)).Single();Check(own.ResultVersion==concurrent[0].ResultVersion,"Eliminar plan no reescribe cierre histórico");
    }
}
