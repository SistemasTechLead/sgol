using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class MonthlyImportChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (DirectionPlanException) { Console.WriteLine("PASS " + message); return; }
        throw new Exception(message);
    }

    public static async Task RunAsync(SgolDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = OperationalSchedule.Instant(new(2026,11,1),0);
        var realNow = DateTimeOffset.UtcNow;
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.Status == RoleAssignmentStatus.Active && r.ValidTo == null).Select(r=>r.UserId).FirstAsync();
        var person = await db.People.Select(p=>p.Id).FirstAsync();
        CapacityWorker[] workers = [new(person,"A","A",CanonicalRole.SalesFloor,null,null)];
        var temporal = new TemporalEligibilityContext { Enabled = true, Now = now, Schedules = [new PersonalScheduleVersion {
            Id=Guid.NewGuid(),PersonId=person,DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)})) }] };
        CapacityLedger Ledger(OperationalAssignment[]? loads=null, HashSet<(Guid,DateOnly)>? absent=null, CapacityWorker[]? staff=null) =>
            new(staff??workers,loads??[],[],absent??[],[],new(2026,11,1)) { Temporal=temporal };
        OperationalAssignment Load(int day,int minutes) => new() { Id=Guid.NewGuid(),PersonId=person,TaskId=Guid.NewGuid(),StartsAt=OperationalSchedule.Instant(new(2026,11,day),9),DueAt=OperationalSchedule.Instant(new(2026,11,day),19),LoadMinutes=minutes };
        DirectionPreparedPlan Plan(string period="MENSUAL",int number=11) => new(Guid.CreateVersion7(),BranchScope.LorettaId,actor,now,
            new("Comprobación mensual","Prueba",period,2026,number,[]));
        var task = new OperationalTask { Id=Guid.CreateVersion7(),TaskCode="TAR-9000",Name="Prueba mensual aislada",PeriodType="MENSUAL",PreferredMonthDays="10",StartHour=9,DeadlineHours=4,LoadMinutes=30,OperationalCriticality="FLEXIBLE" };
        task.Validate(); db.Add(task); await db.SaveChangesAsync();
        async Task<(OperationalAssignment[] Assignments,DirectionPreparedPlan Plan)> Generate(CapacityLedger ledger,DirectionPreparedPlan? plan=null,string[]? codes=null)
        {
            plan ??= Plan();
            var generated=await OperationalPlanGenerator.GenerateAsync(db,plan,codes??[task.TaskCode],now,default,true,ledger);
            return (generated,plan);
        }
        async Task Day(string preferred,int expected,CapacityLedger? ledger=null,string? message=null)
        {
            task.PreferredMonthDays=preferred;task.Validate();await db.SaveChangesAsync();
            var result=await Generate(ledger??Ledger());
            Check(result.Assignments.Length==1 && WeekContract.LocalToday(result.Assignments[0].StartsAt).Day==expected,message??preferred);
        }
        await Day("10",10,message:"Fecha exacta con capacidad se conserva");
        await Day("10",10,Ledger([Load(10,180)]),"No mover fecha exacta aunque tenga mayor ocupación");
        await Day("8",7,message:"Domingo se mueve al sábado en empate");
        await Day("10",9,Ledger([Load(10,240)]),"Saturación elige vecino anterior a igual carga y distancia");
        await Day("10",11,Ledger([Load(10,240),Load(9,100)]),"A igual distancia elige menor ocupación");
        await Day("10",11,Ledger([Load(10,240),Load(9,240)]),"Menor distancia prevalece sobre vecino más lejano vacío");
        await Day("1",2,message:"Borde inicial permanece dentro del mes");
        await Day("30",30,message:"Borde final permanece dentro del mes");
        task.PreferredMonthDays="31";task.Validate();await db.SaveChangesAsync();
        var invalid=await Generate(Ledger());Check(invalid.Assignments.Length==0 && invalid.Plan.MonthlyPendingJson.Contains("MONTH_DAY_NOT_IN_MONTH"),"Día inexistente se conserva como pendiente explícito");
        task.PreferredMonthDays="10";task.Validate();await db.SaveChangesAsync();
        var unavailable=await Generate(Ledger(absent:Enumerable.Range(8,5).Select(d=>(person,new DateOnly(2026,11,d))).ToHashSet()));
        Check(unavailable.Assignments.Length==0 && unavailable.Plan.MonthlyPendingJson.Contains("PLAN_NO_AVAILABLE_WORKER"),"Falta de capacidad deja pendiente sin sobrecarga");
        var noRole=await Generate(Ledger(staff:[]));Check(noRole.Assignments.Length==0 && noRole.Plan.MonthlyPendingJson.Contains("PLAN_NO_WORKER_FOR_ROLE"),"Ausencia de rol deja pendiente controlado");
        task.PreferredMonthDays="9,10";task.Validate();await db.SaveChangesAsync();
        var collapsed=await Generate(Ledger([Load(9,240)]));
        Check(collapsed.Assignments.Length==2 && collapsed.Assignments.Select(a=>a.ScheduleKey).Distinct().Count()==2,"Dos preferencias conservan conteo e identidad al desplazarse");
        var saved=await Generate(Ledger());db.Add(saved.Plan);db.AddRange(saved.Assignments);await db.SaveChangesAsync();
        var repeat=await Generate(Ledger());Check(repeat.Assignments.Length==0,"Ocurrencias guardadas no se duplican al repetir generación");
        var neighbour=await Generate(Ledger(),Plan("SEMANAL",45));Check(neighbour.Assignments.Length==0,"Plan vecino no reclama ocurrencias de otra semana");
        var owns=await Generate(Ledger(),Plan("SEMANAL",46));Check(owns.Assignments.Length==0,"Plan de la misma semana respeta reserva mensual guardada");
        // Remove only the test assignments, still inside the rollback-only transaction.
        db.RemoveRange(saved.Assignments);db.Remove(saved.Plan);await db.SaveChangesAsync();
        task.PreferredMonthDays="8";task.Validate();await db.SaveChangesAsync();
        var edge=await Generate(Ledger(),Plan("SEMANAL",45));
        Check(edge.Assignments.Length==1 && WeekContract.LocalToday(edge.Assignments[0].StartsAt)==new DateOnly(2026,11,7),"Domingo al borde semanal se reconcilia dentro del alcance");
        var next=await Generate(Ledger(),Plan("SEMANAL",46));Check(next.Assignments.Length==0,"Semana vecina no genera la preferencia anterior");
        var preview=new PlanningCapacityPreview(new(2026,11,1),new(2026,11,30),"",[]) {
            TemporalRows=[new(new(2026,11,8),task.Name,"A","SCHEDULE_CONFLICT",false,"")] };
        var daily=await PlanningCapacity.DailyLoadAsync(db,new("Vista previa","Prueba","MENSUAL",2026,11,[task.TaskCode]),preview,Ledger(),default);
        Check(daily.Single(d=>d.Day==new DateOnly(2026,11,7)).PlanMinutes==30 && preview.TemporalRows.All(r=>r.Eligible && r.Day==new DateOnly(2026,11,7)),
            "Vista previa muestra fecha desplazada sin advertencias falsas en la fecha original");
        var normal=new OperationalTask { Id=Guid.CreateVersion7(),TaskCode="TAR-9001",Name="Prueba fija aislada",ScheduleMode="DIAS_FIJOS",WeekDays=1<<2,StartHour=9,DeadlineHours=4,LoadMinutes=15 };
        normal.Validate();db.Add(normal);task.LoadMinutes=1000;await db.SaveChangesAsync();
        var mixed=await Generate(Ledger(),codes:[task.TaskCode,normal.TaskCode]);
        Check(mixed.Assignments.Length==4 && mixed.Plan.MonthlyPendingJson.Contains("PLAN_NO_AVAILABLE_WORKER"),"Pendiente mensual permite conservar el resto del trabajo válido");
        var ordinary=new OperationalTask { Name="Mensual anterior",PeriodType="MENSUAL",Repetitions=2 };
        Check(OperationalSchedule.Dates(new(2026,11,1),new(2026,11,30),ordinary).SequenceEqual(OperationalSchedule.Dates(new(2026,11,1),new(2026,11,30),"MENSUAL",2)),"Mensuales sin preferencias conservan sus fechas");
        Check(OperationalSchedule.Dates(new(2026,11,1),new(2026,11,30),normal).Count()==4,"Días fijos semanales conservados");
        ordinary.PeriodType="OCASIONAL";ordinary.Validate();Check(!OperationalSchedule.Dates(new(2026,11,1),new(2026,11,30),ordinary).Any(),"Ocasionales no generan calendario");
        var inactive=new OperationalTask { Name="Progreso incompleto",ScheduleMode="PROGRESO",Active=false,GoalUnits=null,ProgressUnit="",DifferentPeople=true };
        inactive.Validate(); Check(inactive.GoalUnits==null && inactive.ProgressUnit=="" && inactive.DifferentPeople,"Inactiva conserva meta y unidad vacías y personas distintas");
        inactive.Active=true;Reject(inactive.Validate,"Activación exige parámetros de progreso completos");
        inactive.GoalUnits=10;inactive.ProgressUnit="pares";Reject(inactive.Validate,"Activación prohíbe Personas distintas en progreso");
        inactive.DifferentPeople=false;inactive.Validate();Check(inactive.GoalUnits==10,"Progreso completo puede validarse");
        Reject(()=>new OperationalTask {Name="Inválida",PeriodType="SEMANAL",PreferredMonthDays="8"}.Validate(),"No confundir días del mes y días semanales");
        Reject(()=>MonthlyTaskCalendar.Parse("0,32"),"Días mensuales fuera de rango se rechazan");
        Check(MonthlyTaskCalendar.Parse("22, 8,8").SequenceEqual(new[]{8,22}),"Fechas únicas ordenadas y sin ocurrencias extra");
        await tx.RollbackAsync(); db.ChangeTracker.Clear();
        Console.WriteLine("Monthly import generation checks passed; fixtures rolled back.");
    }
}
