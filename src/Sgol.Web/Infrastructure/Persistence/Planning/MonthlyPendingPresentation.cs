namespace Sgol.Web.Infrastructure.Persistence.Planning;

public static class MonthlyPendingPresentation
{
    public static string Reason(string reason) => reason switch {
        "MONTH_DAY_NOT_IN_MONTH" => "El día no existe en ese mes. Revisa los días preferidos en el catálogo.",
        "MONTH_DATE_PASSED" => "La fecha preferida ya pasó. No se genera trabajo retroactivo.",
        "PLAN_NO_WORKER_FOR_ROLE" => "No hay personal activo del rol requerido.",
        "PLAN_NO_SPECIALIST" => "No hay personal con la especialización requerida.",
        _ => "Sin capacidad u horario compatible dentro de dos días, en el mismo mes y alcance del plan. Revisa horarios, ausencias y cargas."
    };
}
