using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Tasks;

public sealed record RefreshCaseText(string Cause, string Action, string Effect, string? Url = null)
{
    public static RefreshCaseText Decision(AssignmentRefreshDecision item) => item.Code.Split(':')[0] switch
    {
        "inactive" => new("Estas tareas están inactivas.", "Retirar sus pendientes", "Se retiran incluso si tienen avance; el historial se conserva."),
        "excluded" => new("Estas tareas salieron del plan semanal.", "Retirar sus pendientes del plan", "Incluye avances y correcciones; el historial se conserva."),
        "calendar" => new("No se puede verificar el calendario anterior.", "Usar el calendario actual", "Puede cambiar los días y horarios. Revisa la propuesta."),
        "remaining" => new("La nueva carga difiere de la estimación manual.", "Recalcular el tiempo pendiente", "Sustituye la estimación manual y conserva el avance."),
        "deadline" => new("El nuevo horario cambia un plazo con arrastre aprobado.", "Autorizar el nuevo plazo", "Revisa las fechas propuestas; la autorización anterior queda en el historial."),
        "process" => new(item.Cause ?? "La tarea cambió de proceso.", "Conservar el proceso anterior", "Los demás cambios sí se aplican. El proceso nuevo se usará en nuevas ejecuciones."),
        "calendar-preserve" => new(item.Cause ?? "El nuevo calendario retiraría un pendiente con actividad.", "Conservar este pendiente", "Queda sin cambios; se actualizan las demás asignaciones."),
        _ => new(item.Cause ?? "Esta actualización requiere una decisión.", item.Title, item.Help)
    };

    public static RefreshCaseText Issue(AssignmentRefreshIssue item) => item.Code switch
    {
        "specialty" => new("La especialización requerida está inactiva.", "Elegir una especialización activa en la tarea.", "", "/tareas"),
        "process-mode" => new("La modalidad Progreso no es compatible con esta etapa.", "Restablecer una modalidad compatible o revisar el proceso.", "", "/tareas/procesos"),
        "process-evidence" => new("El proceso requiere documentos PDF.", "Conservar ese requisito o preparar otra versión del proceso.", "", "/tareas/procesos"),
        "progress-unit" => new("Hay avance registrado con la unidad anterior.", "Conservar la unidad y modalidad; una conversión requiere revisión.", "", "/tareas"),
        "worker" => new("El responsable no cumple el rol o la especialización.", "Elegir un responsable compatible.", "", "/tareas"),
        "invalid-date" => new("El calendario contiene una fecha inexistente.", "Corregir los días del mes de la tarea.", "", "/tareas"),
        "schedule" => new("Faltan horarios para calcular la agenda.", "Configurar los horarios antes de volver a revisar.", ""),
        "priority" => new("La tarea no tiene una criticidad válida.", "Seleccionar la criticidad de la tarea.", "", "/tareas"),
        "size" => new("La propuesta supera 500 bloques de agenda.", "Reducir el alcance de la actualización y volver a revisar.", ""),
        "capacity" => new("No se encontró espacio compatible en la agenda.", "Revisar turnos, carga y responsables disponibles.", "", "/planificacion"),
        "search" => new("La búsqueda llegó a su límite; no confirma que falte espacio.", "Reducir los conflictos y volver a revisar.", ""),
        "carryover" => new("Hay una solicitud de arrastre o posposición pendiente.", "Resolver esa solicitud antes de actualizar este pendiente.", ""),
        "deadline" => new("El nuevo cierre supera el plazo fijo del proceso.", "Revisar el horario y el plazo del proceso.", "", "/tareas/procesos"),
        "dependency" => new("La etapa empezaría antes de su predecesora.", "Revisar el orden y los horarios del proceso.", "", "/tareas/procesos"),
        "critical-stage" => new("El nuevo cierre invade una etapa crítica posterior.", "Ajustar los horarios del proceso.", "", "/tareas/procesos"),
        _ => new("La actualización requiere revisión.", "Revisar el detalle del caso antes de continuar.", "")
    };
}
