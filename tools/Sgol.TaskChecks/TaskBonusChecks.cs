using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class TaskBonusChecks
{
    internal sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = At(2, 10); }
    internal static DateTimeOffset At(int day, int hour = 0, int minute = 0) => OperationalSchedule.Instant(new(2026, 11, day)).AddHours(hour).AddMinutes(minute);
    static readonly DateOnly Week = new(2026, 11, 2);
    static readonly Guid Person = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    static int count;
    internal static void Check(bool passed, string scenario) { if (!passed) throw new Exception("FAIL " + scenario); Console.WriteLine("PASS " + ++count + " " + scenario); }
    static TaskBonusObligation Obligation(string criticality = "FLEXIBLE", int day = 2, params TaskBonusEvent[] events) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Person, "BONO-TEST", "Obligación de prueba", new(2026, 11, day), criticality,
            TaskBonusCalculator.Points(criticality), events);
    static TaskBonusResult Calculate(DateTimeOffset at, params TaskBonusObligation[] items) => TaskBonusCalculator.Calculate(Person, Week, at, items, true);
    public static void Rules()
    {
        var six = Enumerable.Range(0, 6).Select(_ => Obligation("IMPORTANTE")).ToArray();
        Check(Calculate(At(2, 10), six).EvaluablePoints == 30, "B01: seis repeticiones = 30 puntos, sin multiplicar frecuencia");
        foreach (var (basis, discount, expected) in new[] { (50m,5m,50), (100m,5m,100), (100m,15m,50), (100m,5.00001m,50), (100m,15.00001m,0) })
        {
            var paid = Obligation("CRITICA") with { Points = basis - discount, Events = [new TaskBonusEvent("SUBMITTED",At(2,10))] };
            var lost = Obligation("IMPORTANTE") with { Points = discount };
            Check(Calculate(At(3), paid, lost).BonusPercent == expected, $"B02: A={basis} D={discount} conserva {expected}%");
        }
        var flexible = Obligation();
        Check(Calculate(At(2,23), flexible).ScorePercent == 100, "B03/B04: futura o vencida por hora no descuenta antes de terminar el día");
        Check(Calculate(At(3), flexible).DeductedPoints == .5m, "B05: primera medianoche descuenta mitad");
        Check(Calculate(At(4), flexible).DeductedPoints == 1, "B06: segunda medianoche sustituye por total");
        Check(Calculate(At(4), flexible with { Events = [new TaskBonusEvent("SUBMITTED",At(3,15))] }).DeductedPoints == .5m, "B05: envío en recuperación conserva mitad");
        foreach (var kind in new[] { "IMPORTANTE", "CRITICA" }) Check(Calculate(At(3), Obligation(kind)).DeductedPoints == TaskBonusCalculator.Points(kind), "B06: " + kind + " pierde total");
        Check(Calculate(At(9), Obligation(day:8)).DeductedPoints == 1, "B07: domingo sin recuperación al lunes");
        Check(Calculate(At(9), Obligation(day:7, events:[new TaskBonusEvent("SUBMITTED",At(8,23))])).DeductedPoints == .5m, "B07: sábado enviado domingo conserva mitad");
        var sunday = Obligation("IMPORTANTE",8,new TaskBonusEvent("SUBMITTED",At(8,23)));
        Check(Calculate(At(9,12), sunday).ScorePercent == 100 && Calculate(At(9,12), sunday).Closed, "B08: envío pendiente de revisión se fija al corte");
        Check(Calculate(At(9,12), sunday with { Events=[..sunday.Events,new TaskBonusEvent("REJECTED",At(9,11,59))] }).ScorePercent == 0, "B09: rechazo antes del corte descuenta");
        foreach(var time in new[]{At(9,12),At(9,12,1)}) Check(Calculate(At(10), sunday with { Events=[..sunday.Events,new TaskBonusEvent("REJECTED",time)] }).ScorePercent == 100,"B09/B24: rechazo en/después del corte no cambia cierre");
        var returned = sunday with { Events=[..sunday.Events,new TaskBonusEvent("RETURNED",At(9,11)),new TaskBonusEvent("SUBMITTED",At(9,11,30))] };
        Check(Calculate(At(9,12), returned).DeductedPoints == 5,"B10: corrección del lunes no recupera la devolución");
        var corrected = Obligation("FLEXIBLE",2,new TaskBonusEvent("SUBMITTED",At(2,10)),new TaskBonusEvent("RETURNED",At(2,11)),new TaskBonusEvent("SUBMITTED",At(3,10)));
        Check(Calculate(At(9,12),corrected).DeductedPoints==.5m,"B11: corrección usa su envío sin duplicar obligación");
        Check(Calculate(At(3),Obligation("IMPORTANTE",2,new TaskBonusEvent("CANCELLED",At(2,23)))).EvaluablePoints==0,"B14: cancelación anterior excluye base");
        Check(Calculate(At(4),Obligation("IMPORTANTE",2,new TaskBonusEvent("CANCELLED",At(3,1)))).DeductedPoints==5,"B14: cancelación tardía conserva descuento");
        Check(Calculate(At(4),Obligation("IMPORTANTE",2,new TaskBonusEvent("EXCLUDED",At(3,1)))).EvaluablePoints==0,"B15: excepción expresa retira obligación completa");
        Check(Calculate(At(9,12),Obligation("IMPORTANTE",2,new TaskBonusEvent("ADMIN_COMPLETION",At(2,15)))).ScorePercent==100,"Conclusión administrativa acredita con su fecha");
        var noWork=TaskBonusCalculator.Calculate(Person,Week,At(9,12),[],false);
        Check(noWork.Closed && noWork.ScorePercent==null && noWork.BonusPercent==0,"B17: sin trabajo ni base, sin calificación y bono0");
        Check(Calculate(At(9,12)).Closed && Calculate(At(9,12)).BonusPercent==null,"B17: trabajó sin base, incidencia cerrada sin número");
        Check(TaskBonusCalculator.Calculate(Person,Week.AddDays(-7),At(2),[],false).CalculationState=="NO_VIGENTE","B22: semana anterior no vigente");
        Check(Calculate(At(2),flexible).Period.PaymentReferenceDate==new DateOnly(2026,11,14),"Primer sábado de referencia: 14 noviembre");
        Check(Calculate(At(2),flexible).NextChangeAt==At(3),"B33: transición temporal sin actividad");
        Check(Calculate(At(3), flexible).DeductedPoints==Calculate(At(3),flexible).DeductedPoints,"B23: consulta no acumula descuentos");
        Check(Calculate(At(9,12),Obligation("CRITICA") with { Points=6 },Obligation("CRITICA") with { Points=4 }).EvaluablePoints==10,"B20 calculador: participaciones históricas explícitas 6/4, total10");
        MaterializationRules();
    }
    static void MaterializationRules()
    {
        var a=new OperationalAssignment {Id=Guid.NewGuid(),PersonId=Person,PersonName="Origen",TaskCode="TEST",Name="Transferencia",StartsAt=At(8,9),DueAt=At(8,15),OperationalCriticality="IMPORTANTE",LoadMinutes=60};
        TaskBonusFact Fact(OperationalAssignment x, DateTimeOffset at)=>new(){AssignmentId=x.Id,Revision=x.Revision,RecordedAt=at,SnapshotJson=JsonSerializer.Serialize(TaskBonusAssignmentSnapshot.From(x))};
        var original=Fact(a,At(2));var recipient=Guid.NewGuid();
        var move=new OperationalTransfer{AssignmentId=a.Id,FromPersonId=Person,ToPersonId=recipient,At=At(8,13),NewDueAt=At(10,15)};
        a.PersonId=recipient;a.StartsAt=move.At;a.DueAt=move.NewDueAt;a.Revision++;
        var next=Fact(a,move.At);
        var result=TaskBonusService.Materialize([original,next],[move],[],At(9,12),At(9,12));
        Check(result.Single().PersonId==recipient && result.Single().Day==new DateOnly(2026,11,10),"B12/B13: transferencia puntual pasa a semana del nuevo plazo");
        move.At=At(9,1);next.RecordedAt=move.At;
        result=TaskBonusService.Materialize([original,next],[move],[],At(9,12),At(9,12));
        Check(result.Single().PersonId==Person && result.Single().Day==new DateOnly(2026,11,8),"B13: después de incumplir conserva origen, no crea puntos al receptor");
        var before=JsonSerializer.Deserialize<TaskBonusAssignmentSnapshot>(original.SnapshotJson)!;
        original.SnapshotJson=JsonSerializer.Serialize(before with{StartsAt=At(1,9),DueAt=At(1,12)});
        Check(TaskBonusService.Materialize([original,next],[move],[],At(9,12),At(9,12)).Length==0,"B22: pendientes anteriores no se convierten en puntos nuevos");
    }
}
