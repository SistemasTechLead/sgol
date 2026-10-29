using System.Text.Json;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessItemBoundaryChecks
{
    public static void Run()
    {
        var person=Guid.CreateVersion7();var day=new DateOnly(2026,10,29);
        var worker=new CapacityWorker(person,"Boundary worker","ITEMS-BOUNDARY",CanonicalRole.SalesFloor,null,null);
        var schedule=new PersonalScheduleVersion{Id=Guid.CreateVersion7(),PersonId=person,Revision=1,DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,780),new WorkInterval(840,1140)}))};
        CapacityLedger Ledger(CapacityWorker[]? workers=null,HashSet<(Guid,DateOnly)>? absent=null,PersonalScheduleVersion[]? schedules=null)=>new(workers??[worker],[],[],absent??[],[],day){Temporal=new(){Enabled=true,Schedules=schedules??[schedule]}};
        var task=new OperationalAssignment{Id=Guid.CreateVersion7(),PersonId=person,AssignedRole=CanonicalRole.SalesFloor,LoadMinutes=60,StartsAt=OperationalSchedule.Instant(day,12).AddMinutes(50),DueAt=OperationalSchedule.Instant(day,14).AddMinutes(20)};
        Check(OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force permits internal break without expanding shift");
        task.StartsAt=OperationalSchedule.Instant(day,13).AddMinutes(10);Check(OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force may begin inside an internal break");
        Check(!OperationalProcessService.ForceWindow(Ledger(absent:[(person,day)]),task).Eligible,"Force rejects absence");
        Check(!OperationalProcessService.ForceWindow(Ledger(workers:[]),task).Eligible,"Force rejects inactive account/employment");
        Check(!OperationalProcessService.ForceWindow(Ledger(schedules:[]),task).Eligible,"Force rejects unknown schedule");
        Check(!OperationalProcessService.ForceWindow(Ledger(workers:[worker with{EmploymentEnd=task.DueAt.AddMinutes(-1)}]),task).Eligible,"Force rejects employment ending inside interval");
        Check(!OperationalProcessService.ForceWindow(Ledger(workers:[worker with{RoleEnd=task.DueAt.AddMinutes(-1)}]),task).Eligible,"Force rejects role ending inside interval");
        task.SpecializationId=Guid.CreateVersion7();Check(!OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force cannot bypass specialization");task.SpecializationId=null;
        task.AssignedRole=CanonicalRole.Administration;Check(!OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force cannot bypass role");task.AssignedRole=CanonicalRole.SalesFloor;
        task.DueAt=OperationalSchedule.Instant(day,19).AddMinutes(1);Check(!OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force does not trim work ending after shift");
        task.StartsAt=OperationalSchedule.Instant(day,8);task.DueAt=OperationalSchedule.Instant(day,10);Check(!OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force rejects time before shift");
        task.StartsAt=OperationalSchedule.Instant(day,18);task.DueAt=OperationalSchedule.Instant(day.AddDays(1),10);Check(!OperationalProcessService.ForceWindow(Ledger(),task).Eligible,"Force never splits work across days");
        var tomorrow=OperationalSchedule.Instant(day.AddDays(1),12);
        var stage=new OperationalSequenceStage{SchedulingPolicyId=Guid.CreateVersion7(),OperationalCriticality="FLEXIBLE",LoadMinutes=36,ClosureMarginPercent=50,NominalStartsAt=OperationalSchedule.Instant(day,12),PlannedStartsAt=OperationalSchedule.Instant(day,12),PlannedDueAt=OperationalSchedule.Instant(day,13)};
        OperationalProcessService.RebaseReturnedAgenda(stage,tomorrow);
        Check(stage.NominalStartsAt==tomorrow&&stage.PlannedStartsAt==tomorrow&&stage.PlannedDueAt==tomorrow.AddMinutes(54),"New flexible round rebases whole corrected load and captured margin");
        stage.OperationalCriticality="CRITICA";var nominal=stage.NominalStartsAt;
        OperationalProcessService.RebaseReturnedAgenda(stage,tomorrow.AddDays(1));Check(stage.NominalStartsAt==nominal,"Returned critical anchor never shifts automatically");
        stage.OperationalCriticality="FLEXIBLE";stage.FixedDueAt=tomorrow.AddHours(1);
        OperationalProcessService.RebaseReturnedAgenda(stage,tomorrow.AddDays(1));Check(stage.NominalStartsAt==nominal&&stage.FixedDueAt==tomorrow.AddHours(1),"Returned fixed deadline remains unchanged");
        Console.WriteLine("ITEMS_BOUNDARIES_PASS");
    }
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
}
