using System.Text.Json;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;
internal static class PersonalScheduleChecks
{
    public static void Run()
    {
        var a=Guid.NewGuid(); var mar=Guid.NewGuid(); var director=Guid.NewGuid(); var closing=Guid.NewGuid();
        var mon=new DateOnly(2026,10,19); var wed=mon.AddDays(2); var sun=mon.AddDays(-1);
        PersonalScheduleVersion Schedule(Guid p,int entry,int exit) => new() { Id=Guid.NewGuid(),PersonId=p,Revision=1,From=sun,
            DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[] { new WorkInterval(entry,exit) })) };
        var ms=Schedule(mar,660,1200); var days=ms.Days();
        foreach(var d in new[] {1,2,4,5,6}) days[d]=[new(660,900),new(960,1200)];
        days[3]=[]; days[0]=[new(600,840)]; ms.DaysJson=JsonSerializer.Serialize(days);
        var context=new TemporalEligibilityContext { Enabled=true,ClosingTaskId=closing,Schedules=[Schedule(a,900,1140),ms,Schedule(director,600,1200)],
            Rotations=[new() { Id=Guid.NewGuid(),From=sun,Revision=1,ExcludedPersonId=director,RegularPersonId=mar,Anchor=wed,ParticipantsJson=JsonSerializer.Serialize(new[] {a}) }] };
        OperationalAssignment Task(DateOnly day,int minute,int hours=1,Guid? id=null) => new() { Id=Guid.NewGuid(),TaskId=id??Guid.NewGuid(),StartsAt=TemporalEligibilityContext.At(day,minute),DueAt=TemporalEligibilityContext.At(day,minute).AddHours(hours),LoadMinutes=15,GoalUnits=0,AssignedRole="PISO_VENTAS" };
        var count=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine($"PASS {++count}: {name}"); }
        var calendarMonday=new DateOnly(2026,10,12);
        for(var offset=0;offset<7;offset++) {
            var today=calendarMonday.AddDays(offset);
            var identity=DirectionPlanContract.OperationsWeek(today);
            var range=DirectionPlanContract.Calculate("SEMANAL",identity.Year,identity.Week,today);
            Check(range.StartsOn==(offset<5?today:calendarMonday.AddDays(7)) && range.EndsOn==calendarMonday.AddDays(offset<5?6:13),$"Operations calendar for {today.DayOfWeek}");
        }
        var rollover=DirectionPlanContract.OperationsWeek(new(2024,12,28));
        Check(rollover==(2025,1) && DirectionPlanContract.Calculate("SEMANAL",rollover.Year,rollover.Week,new(2024,12,28))==new WeekRange(new(2024,12,30),new(2025,1,5)),"Weekend operations cross ISO year boundary");
        Check(DirectionPlanContract.Calculate("SEMANAL",2026,42)==new WeekRange(calendarMonday,calendarMonday.AddDays(6)),"Ordinary weekly plan retains full calendar week");
        Check(context.Evaluate(a,Task(mon.AddDays(-14),900),[]).Eligible,"Saved weekly schedule applies before its original effective date");
        var replacement=Schedule(a,540,780); replacement.Revision=2; replacement.From=mon.AddDays(30);
        var global=new TemporalEligibilityContext { Enabled=true,Schedules=[context.Schedules[0],replacement] };
        Check(global.Day(a,mon.AddDays(-14),false).Schedule?.Id==replacement.Id && global.Evaluate(a,Task(mon.AddDays(-14),600),[]).Eligible,"Latest revision applies to earlier plan weeks");
        Check(!global.Evaluate(a,Task(mon,900),[]).Eligible,"Old revision does not fill gaps in current global schedule");
        Check(context.Evaluate(a,Task(mon,900),[]).Eligible,"Previously captured context keeps its original schedule");
        var exceptionContext=new TemporalEligibilityContext { Enabled=true,Schedules=global.Schedules,Exceptions=[new() { PersonId=a,Day=mon.AddDays(-14),Revision=1,IntervalsJson="[]" }] };
        Check(!exceptionContext.Evaluate(a,Task(mon.AddDays(-14),600),[]).Eligible,"Date exception overrides global weekly schedule");
        Check(context.ClosingPerson(mon)==mar,"Default closing follows actual 20:00 schedule");
        Check(context.ClosingPerson(wed)==null,"Wednesday has no automatic rotation");
        Check(context.ClosingPerson(wed.AddDays(7))==null,"Next Wednesday remains unselected");
        Check(context.ClosingPerson(sun)==mar,"Sunday default follows 14:00 schedule");
        Check(context.Day(mar,mon).Intervals!.Max(i=>i.Exit)==1260,"Default closing extends final shift to 21:00");
        Check(context.Day(mar,sun).Intervals!.Max(i=>i.Exit)==900,"Sunday closing extends final shift to 15:00");
        Check(context.Day(a,wed).Intervals!.Max(i=>i.Exit)==1140,"Unselected person retains original shift");
        Check(context.Evaluate(mar,Task(mon,1190,id:closing),[]).Eligible,"Mariana can close");
        Check(!context.Evaluate(director,Task(mon,1190,id:closing),[]).Eligible,"Director exclusion is preserved");
        Check(!context.Evaluate(a,Task(wed,1190,id:closing),[]).Eligible,"Unselected Wednesday cannot close");
        Check(context.Evaluate(mar,Task(mon,1190,id:closing),[],TemporalEligibilityContext.At(mon,1255)).Eligible,"Late closing reception uses shared 21:00 operating limit");
        Check(!context.Evaluate(mar,Task(mon,1190,id:closing),[],TemporalEligibilityContext.At(mon,1260)).Eligible,"No closing reception after operating limit");
        var longer=new TemporalEligibilityContext { Enabled=true,Schedules=[Schedule(a,900,1380)] };
        Check(longer.Day(a,mon).Intervals!.Max(i=>i.Exit)==1380,"Coverage never shortens a pre-existing longer schedule");
        var selected=new TemporalEligibilityContext { Enabled=true,ClosingTaskId=closing,Schedules=context.Schedules,Rotations=context.Rotations,Coverages=[new() { Id=Guid.NewGuid(),Day=wed,PersonId=a,Revision=1 }] };
        Check(selected.Day(a,wed).Intervals!.Max(i=>i.Exit)==1200,"Selecting Wednesday extends final shift by exactly one hour");
        Check(selected.Evaluate(a,Task(wed,1190,id:closing),[]).Eligible,"Selected Wednesday person becomes closing candidate");
        Check(selected.Evaluate(a,Task(wed,1190,id:closing),[]).Limit==TemporalEligibilityContext.At(wed,1260),"Shared operating limit is 21:00");
        Check(selected.ClosingPerson(wed.AddDays(7))==null,"Choice affects its date only");
        Check(!selected.Evaluate(a,Task(wed,1190,id:closing),[(a,wed)]).Eligible,"Absence still wins over coverage");
        Check(context.Evaluate(mar,Task(sun,600),[]).Eligible,"Any task can be scheduled Sunday without a whitelist");
        Check(!context.Evaluate(a,Task(mon,840,3),[]).Eligible,"A long deadline never permits waiting for personal entry");
        Check(!context.Evaluate(mar,Task(mon,540,8),[]).Eligible,"09:00 activation rejects Mariana entering at 11:00 despite a 17:00 deadline");
        Check(!context.Evaluate(mar,Task(mon,540,8),[],TemporalEligibilityContext.At(mon,660)).Eligible,"Late reception cannot bypass absence at original activation");
        Check(!context.Evaluate(mar,Task(mon,0,48),[]).Eligible,"Multi-day margin cannot bypass absence at activation");
        Check(context.Evaluate(mar,Task(mon,660,8),[]).Eligible,"Mariana is eligible exactly at 11:00 entry");
        Check(!context.Evaluate(mar,Task(mon,900,8),[]).Eligible,"Lunch activation cannot wait for the afternoon shift");
        Check(!context.Evaluate(mar,Task(wed,660,48),[]).Eligible,"Rest-day activation cannot wait for tomorrow");
        Check(!context.Evaluate(a,Task(mon,900,8),[],TemporalEligibilityContext.At(mon,1140)).Eligible,"Late reception at exit cannot wait for another shift");
        Check(context.Evaluate(a,Task(mon,900,8),[],TemporalEligibilityContext.At(mon,1000)).Eligible,"Late reception during the same shift remains eligible");
        Check(!context.Evaluate(a,Task(mon,840),[]).Eligible,"No presence before deadline remains a conflict");
        Check(!context.Evaluate(a,Task(mon,1140),[]).Eligible,"Exit is exclusive");
        Check(context.Evaluate(a,Task(mon,900),[]).Eligible,"Entry is inclusive");
        var large=Task(mon,1139); large.LoadMinutes=120;
        Check(context.Evaluate(a,large,[]).Eligible,"Task duration no longer imposes a work-window policy");
        Check(!context.Evaluate(Guid.NewGuid(),Task(mon,900),[]).Eligible,"Unknown schedule is never unlimited");
        var ledger=new CapacityLedger([new(mar,"Mariana","M","PISO_VENTAS",null,null)],[],[],[],[],mon) { Temporal=context };
        Check(ledger.DayCapacity(mar,mon)==240,"Extending presence does not increase capacity");
        large.PersonId=mar;large.LoadMinutes=300;
        Check(!ledger.CanAssign(mar,large),"Original daily capacity still applies");
        var calendar=new OperationalTask { StartHour=9,StartMinute=15,SundayStartMinute=600 };
        Check(calendar.StartOn(sun)==TemporalEligibilityContext.At(sun,600),"Nullable Sunday time overrides Sunday only");
        Check(calendar.StartOn(mon)==TemporalEligibilityContext.At(mon,555),"Weekday activation remains unchanged");
        calendar.SundayStartTime=null;
        Check(calendar.StartOn(sun)==TemporalEligibilityContext.At(sun,555),"Null Sunday time uses base activation");
        foreach(var bad in new[] { new[] {new WorkInterval(900,780)},new[] {new WorkInterval(540,800),new(780,900)},new[] {new WorkInterval(-1,10)} }) {
            var rejected=false; try { TemporalEligibilityContext.ValidateIntervals(bad); } catch {rejected=true;} Check(rejected,"Invalid personal interval is rejected");
        }
        Console.WriteLine($"{count} simplified personal schedule checks passed.");
    }
}
