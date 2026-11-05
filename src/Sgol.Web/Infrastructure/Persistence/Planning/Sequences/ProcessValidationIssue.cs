using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record ProcessValidationIssue(Guid? TaskId, string Field, string Message);

public sealed partial class OperationalProcessCatalogService
{
    private static DirectionPlanException LocatedError(string code, IEnumerable<Guid> ids, string field = "", int status = 422)
    {
        var error = Error(code, status);
        error.Data["ProcessIssues"] = ids.Distinct().Select(id => new ProcessValidationIssue(id, field, "")).ToArray();
        return error;
    }

    public static ProcessValidationIssue[] DiagnoseRules(ProcessDraftInput input, bool publish)
    {
        var issues = new List<ProcessValidationIssue>();
        void Add(bool invalid, string field, string message, Guid? task = null) { if (invalid) issues.Add(new(task, field, message)); }
        Add(input.StartKind is not ("OCASIONAL" or "SEMESTRAL" or "SEMANAL" or "MENSUAL" or "TRIMESTRAL" or "ANUAL"), "StartKind", "Selecciona una periodicidad de inicio válida.");
        Add(input.FirstMonth is < 1 or > 6, "FirstMonth", "El primer mes debe estar entre 1 y 6.");
        Add(input.WindowFromDay is < 1 or > 28, "WindowFromDay", "El día inicial debe estar entre 1 y 28.");
        Add(input.WindowThroughDay < input.WindowFromDay || input.WindowThroughDay > 28, "WindowThroughDay", "El día final debe ser igual o posterior al inicial y no superar 28.");
        Add(input.PreferredWeekDay is < 0 or > 6, "PreferredWeekDay", "Selecciona un día de la semana válido.");
        Add(input.StartHour is < 0 or > 23 || publish && input.StartKind == "SEMESTRAL" && input.StartHour == null, "StartHour", "Indica una hora de inicio entre 0 y 23 para publicar el calendario.");
        Add(input.Steps.Count > 32 || publish && input.Steps.Count == 0, "Steps", "El proceso debe tener entre 1 y 32 tareas; añade o retira etapas.");
        var positionsValid = input.Steps.Select(s => s.Position).Order().SequenceEqual(Enumerable.Range(1, input.Steps.Count));
        foreach (var s in input.Steps)
        {
            void Stage(bool invalid, string field, string message) => Add(invalid, field, message, s.TaskId);
            Stage(!positionsValid, "Position", "Ordena las etapas en posiciones consecutivas, empezando en 1.");
            Stage(s.TaskId == Guid.Empty || input.Steps.Count(p => p.TaskId == s.TaskId) != 1, "TaskId", "Selecciona una tarea válida y conserva una sola etapa por tarea.");
            Stage(s.Action is not ("ENTREGAR" or "REVISAR" or "SELLAR" or "AUTORIZAR"), "Action", "Selecciona una acción válida para esta etapa.");
            Stage(s.Position == 1 ? s.ReleaseCondition != "INICIO" : s.ReleaseCondition is not ("ENTREGA" or "APROBACION"), "ReleaseCondition", "La primera etapa comienza con Inicio; las demás requieren entrega o aprobación anterior.");
            Stage(s.DeadlineKind is not ("RELATIVO" or "FIJO") || s.DeadlineHours is < 1 or > 8760, "DeadlineHours", "Revisa el plazo de la etapa: debe ser relativo o fijo, entre 1 y 8760 horas.");
            Stage(s.MinimumStartOffsetHours is < 0 or > 8760, "MinimumStartOffsetHours", "Las horas de inicio mínimo deben estar entre 0 y 8760.");
            Stage(s.ReturnToPosition is { } target && (target < 1 || target >= s.Position), "ReturnToPosition", "La devolución debe apuntar a una etapa anterior.");
            Stage(s.Instructions == null || s.Instructions.Length > 4000, "Instructions", "Las instrucciones admiten hasta 4000 caracteres.");
            Stage(s.InputCountSourceTaskId is {} origin && (s.IsFinalAudit || !input.Steps.Any(p => p.TaskId == origin && p.Position < s.Position && p.OutputItemId != null)),
                "InputCountSourceTaskId", "Elige una etapa anterior que reporte conteo o carga propia. La auditoría final siempre conserva carga propia.");
            if (publish && positionsValid && s.Position > 1)
                Stage(s.ReleaseCondition != (input.Steps.Single(p => p.Position == s.Position - 1).Action == "ENTREGAR" ? "ENTREGA" : "APROBACION"), "ReleaseCondition", "La entrada debe corresponder a la entrega o aprobación de la etapa anterior.");
            if (input.Steps.Any(p => p.OutputItemId != null))
            {
                Stage(s.Position == input.Steps.Count && !s.IsFinalAudit || s.IsFinalAudit && s.Position != input.Steps.Count, "IsFinalAudit", "Con conteos, marca únicamente la última tarea como auditoría final.");
                Stage(s.OutputItemId != null && (s.IsFinalAudit || s.Position == input.Steps.Count), "OutputItemId", "La auditoría final no produce conteo. Configura el ítem en una etapa anterior; sus receptoras se eligen al preparar cada plan.");
            }
            Stage(s.IsFinalAudit && s.Position != input.Steps.Count || s.OutputItemId == null && s.OutputTargetTaskId != null, "OutputItemId", "Revisa el enlace del conteo y deja la auditoría final solo en la última etapa.");
            if (publish)
            {
                Stage(s.Position == 1 && s.Action != "ENTREGAR", "Action", "La primera tarea debe usar Entregar.");
                Stage(s.Position == input.Steps.Count && s.Action != "AUTORIZAR", "Action", "La última tarea debe usar Aceptar resultado.");
            }
        }
        Add(publish && input.Steps.Count < 2, "Steps", "Añade al menos dos tareas: una entrega inicial y una aceptación final.");
        return issues.ToArray();
    }
}
