using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Endpoints;
using System.Text.Json;

internal static class PlanResolutionChecks
{
    public static void Rules()
    {
        static void Check(bool ok,string text) { if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text); }
        var person=Guid.NewGuid();var day=new DateOnly(2026,11,2);
        var input=new DirectionPlanInput("Resolution","Test","SEMANAL",2026,45,["TAR-X"],
            [new(person,75),new(person,100,day.AddDays(2),360)],new string('a',64));
        var normalized=DirectionPlanContract.Normalize(input);
        var parsed=DirectionPreparedPlanApiEndpoints.Parse(JsonSerializer.SerializeToElement(normalized,JsonSerializerOptions.Web));
        Check(parsed.CapacityAdjustments!.SequenceEqual(input.CapacityAdjustments!),"Dated and legacy capacity adjustments survive strict API parsing");
        foreach(var adjustment in new[]{new PlanCapacityAdjustment(person,100,day,481),new(person,101,day,240),new(person,100,day,null),new(person,100,null,240)})
        {
            try { DirectionPlanContract.Normalize(input with{CapacityAdjustments=[adjustment]});throw new Exception("Invalid adjustment accepted"); }
            catch(DirectionPlanException e) { Check(e.Code=="PLAN_CAPACITY_INVALID","Malformed capacity choice rejected"); }
        }
        try { DirectionPlanContract.Normalize(input with{CapacityAdjustments=[new(person,100,day,300),new(person,100,day,400)]});throw new Exception("Duplicate adjustment accepted"); }
        catch(DirectionPlanException e) {Check(e.Code=="PLAN_CAPACITY_INVALID","Duplicate person/date rejected");}
        var preview=new PlanningCapacityPreview(day,day.AddDays(6),new string('a',64),[new(person,"Test","PISO_VENTAS",[],0,0,0,100,240)]);
        var ledger=new CapacityLedger([],[],[],[],[],day);
        var changes=PlanningCapacity.Changes(input,preview,ledger);
        Check(changes.Length==3 && changes.Count(c=>c.From<=day.AddDays(2)&&c.Through>=day.AddDays(2))==1,
            "Dated exception splits the general percentage without overlapping segments");
        Check(changes.Single(c=>c.From==day.AddDays(2)).DailyMinutes==360,"Selected day keeps its explicit capacity");
        try { PlanningCapacity.Changes(input with{CapacityAdjustments=[new(person,100,day.AddDays(-1),300)]},preview,ledger);throw new Exception("Outside date accepted"); }
        catch(DirectionPlanException e) {Check(e.Code=="PLAN_CAPACITY_INVALID","Date outside plan rejected");}
        try { PlanningCapacity.Changes(input with{CapacityAdjustments=[new(Guid.NewGuid(),100,day,300)]},preview,ledger);throw new Exception("Unknown person accepted"); }
        catch(DirectionPlanException e) {Check(e.Code=="PLAN_CAPACITY_INVALID","Person outside eligible plan workers rejected");}
        var moved=input with{OccurrenceMoves=[new("TAR-X",day,day.AddDays(1))]};
        Check(DirectionPreparedPlanApiEndpoints.Parse(JsonSerializer.SerializeToElement(moved,JsonSerializerOptions.Web)).OccurrenceMoves!.SequenceEqual(moved.OccurrenceMoves!),"Date exceptions survive strict API parsing");
        foreach(var move in new[]{new PlanOccurrenceMove("TAR-X",day,day),new("TAR-X",day,day.AddDays(7)),new("OTHER",day,day.AddDays(1))})
        {
            try{DirectionPlanContract.Normalize(input with{OccurrenceMoves=[move]});throw new Exception("Illegal move accepted");}
            catch(DirectionPlanException e){Check(e.Code=="PLAN_MOVE_INVALID","Date exception cannot leave the week, repeat source date or target an unselected task");}
        }
    }
}
