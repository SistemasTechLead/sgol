using System.Globalization;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Presentation.Components;

public sealed record TaskBonusGaugeViewModel(TaskBonusResult Result, bool Compact = false)
{
    public string Score => Result.ScoreDisplay;
    public static string EventLabel(string kind) => kind switch {
        "SUBMITTED" => "Enviada a revisión", "RETURNED" => "Devuelta para corrección", "REJECTED" => "No satisfecha",
        "ADMIN_COMPLETION" => "Concluida por Supervisión", "APPROVED" => "Revisión confirmada", "CANCELLED" => "Cancelación autorizada",
        "EXCLUDED" => "Excluida del bono por Dirección", "TRANSFERRED" => "Obligación transferida", "TRANSFERRED_OVERDUE" => "Transferida para recuperación", _ => "Cambio registrado" };
    public static string Format(decimal? value) => TaskBonusResult.FormatScore(value);
    public string State => Result.CalculationState switch {
        "NO_VIGENTE" => "Evaluación aún no vigente", "SIN_TAREAS_EVALUABLES" => "Sin tareas evaluables",
        "INCIDENCIA" => "Sin porcentaje calculable", _ => Result.Closed ? "Cerrado" : "Provisional"
    };
}
