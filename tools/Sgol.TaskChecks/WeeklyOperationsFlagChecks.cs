using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class WeeklyOperationsFlagChecks
{
    public static void Run()
    {
        foreach(var period in OperationalSchedule.Periods)
        {
            var task=new OperationalTask{Name="Weekly membership check",PeriodType=period,IsWeeklyOperationsTask=true};
            try {task.Validate();if(period!="SEMANAL")throw new Exception("Nonweekly flag accepted: "+period);}
            catch(DirectionPlanException e){if(period=="SEMANAL"||e.Code!="WEEKLY_OPERATIONS_TASK_INVALID")throw;}
            Console.WriteLine("PASS weekly flag rule: "+period);
        }
        var input=new DirectionPlanInput("Rolling","Test","MENSUAL",2026,10,["TAR-0025"],RollingMonthStartsOn:new(2026,10,26));
        if(DirectionPlanContract.Calculate(DirectionPlanContract.Normalize(input))!=new WeekRange(new(2026,10,26),new(2026,11,26)))throw new Exception("Wrong rolling range");
        if(DirectionPlanContract.Calculate(input with{Year=2026,PeriodNumber=1,RollingMonthStartsOn=new(2026,1,31)})!=new WeekRange(new(2026,1,31),new(2026,2,28)))throw new Exception("Wrong month-end range");
        try {DirectionPlanContract.Normalize(input with{IsWeeklyOperationsPlan=true});throw new Exception("Weekly rolling plan accepted");}catch(DirectionPlanException){}
        Console.WriteLine("PASS rolling month range, month-end boundary and weekly incompatibility");
    }
}
