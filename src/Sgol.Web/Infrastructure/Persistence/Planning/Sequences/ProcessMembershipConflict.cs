namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record ProcessMembershipConflict(Guid TaskId, string TaskName, string TaskCode, string Reason);

public sealed partial class OperationalProcessCatalogService
{
    private async Task RequireValidMembership(Guid processId, OperationalSequenceStep[] steps,
        OperationalTask[] members, CancellationToken token)
    {
        var ids = steps.Select(s => s.TaskId).ToArray();
        var referenced = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync(
            db.OperationalTasks.Where(t => ids.Contains(t.Id)), token);
        var conflicts = DiagnoseMembership(processId, steps, members.Concat(referenced).DistinctBy(t => t.Id).ToArray());
        if (conflicts.Length == 0) return;
        var error = Error("PROCESS_MEMBERSHIP_INVALID");
        error.Data["TaskConflicts"] = conflicts;
        throw error;
    }

    public static ProcessMembershipConflict[] DiagnoseMembership(Guid processId, OperationalSequenceStep[] steps, OperationalTask[] tasks)
    {
        var conflicts = new List<ProcessMembershipConflict>();
        foreach (var id in steps.Select(s => s.TaskId).Concat(tasks.Where(t => t.ProcessId == processId && !t.Deleted).Select(t => t.Id)).Distinct())
        {
            var task = tasks.FirstOrDefault(t => t.Id == id);
            var count = steps.Count(s => s.TaskId == id);
            void Add(string reason) => conflicts.Add(new(id, task?.Name ?? "Tarea no disponible", task?.TaskCode ?? id.ToString(), reason));
            if (task == null || task.Deleted) Add("La tarea fue eliminada o ya no está disponible. Quítala del borrador y selecciona una tarea disponible.");
            else
            {
                if (task.ProcessId != processId) Add("La tarea ya no pertenece a este proceso. Quítala del borrador y revisa su pertenencia en el catálogo antes de añadirla de nuevo.");
                if (!task.Active) Add("La tarea está inactiva. Actívala en el catálogo o quítala del proceso y guarda el borrador.");
                if (count == 0) Add("La tarea pertenece al proceso, pero falta en el orden de etapas. Agrégala desde Añadir tarea.");
            }
            if (count > 1) Add("La tarea aparece más de una vez. Conserva una sola etapa y guarda el borrador.");
        }
        return conflicts.ToArray();
    }
}
