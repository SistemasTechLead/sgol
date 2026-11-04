using System.Text.Json;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Pages.Tasks;

internal static class RefreshIssuePresentationChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS refresh issue: " + message);
        }
        var task = new OperationalTask { Name = "Reunión", LoadMinutes = 20, StartHour = 10, StartMinute = 8,
            Requirement = "NINGUNO", ScheduleMode = "DIAS_FIJOS", PeriodType = "SEMANAL", Repetitions = 1 };
        var row = new OperationalAssignment();
        AssignmentDefinition.Adopt(row, task, null, false);
        var before = JsonSerializer.Serialize(row);
        task.LoadMinutes = 45; task.StartHour = 11; task.Requirement = "COMENTARIO";
        var issue = new AssignmentRefreshIssue("capacity", "Conflicto", "TAR-0215", task.Name, null)
            { PreviousAssignment = row, CurrentDefinition = task };
        var changes = RefreshCaseText.Changes(issue);
        Check(changes.Any(c => c.Field == "Carga" && c.Before == "20 minutos" && c.Current == "45 minutos"), "stored versus catalog load");
        Check(changes.Any(c => c.Field == "Hora de activación" && c.Before == "10:08" && c.Current == "11:08"), "old and current activation");
        Check(changes.Any(c => c.Field == "Requisito"), "simultaneous evidence edit");
        Check(JsonSerializer.Serialize(row) == before, "no assignment mutation");
        Check(RefreshCaseText.Issue(issue with { Code = "carryover" }).Url == "/validaciones", "carryover links to main Supervision");
        Check(!RefreshCaseText.Changes(issue with { ProcessStage = true }).Any(c => c.Field == "Hora de activación"), "process calendar excluded");
        Check(RefreshCaseText.Changes(issue with { CurrentDefinition = AssignmentDefinition.Baseline(row, task) }).Length == 0, "unchanged neighbour");
        row.DefinitionSnapshotJson = null; row.TaskRevision = -1;
        Check(RefreshCaseText.Changes(issue).Any(c => c.Field == "Calendario anterior sin verificar"), "legacy calendar remains unknown");
    }
}
