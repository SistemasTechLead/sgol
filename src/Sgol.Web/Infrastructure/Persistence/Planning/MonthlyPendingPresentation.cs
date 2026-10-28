namespace Sgol.Web.Infrastructure.Persistence.Planning;

public static class MonthlyPendingPresentation
{
    public static string Reason(string reason) => reason switch {
        "ROLLING_MONTH_NO_SLOT" => "No hay un bloque con horario y capacidad compatibles dentro del periodo mensual. Revisa la duración, los turnos o la especialización.",
        "CRITICAL_ACTIVATION_PASSED" => "La hora fija de esta tarea crítica ya pasó. Se conserva este aviso; no se crea una ejecución retroactiva ni se traslada a otro día.",
        "MONTH_DISTRIBUTED_NO_SLOT" => "No hay un día con horario y capacidad compatibles dentro de este plan. La repetición sigue pendiente para otro día del mismo mes.",
        "MONTH_DAY_NOT_IN_MONTH" => "El día no existe en ese mes. Revisa los días preferidos en el catálogo.",
        "MONTH_DATE_PASSED" => "La fecha preferida ya pasó. No se genera trabajo retroactivo.",
        "PLAN_NO_WORKER_FOR_ROLE" => "No hay personal activo del rol requerido.",
        "PLAN_NO_SPECIALIST" => "No hay personal con la especialización requerida.",
        _ => "Sin capacidad u horario compatible dentro de dos días, en el mismo mes y alcance del plan. Revisa horarios, ausencias y cargas."
    };
}
