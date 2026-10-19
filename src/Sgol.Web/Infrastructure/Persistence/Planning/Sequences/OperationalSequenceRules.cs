using Sgol.Planning.Contracts;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record SequenceEnablement(string Status, DateTimeOffset? EligibleAt,
    DateTimeOffset? EnabledAt, DateTimeOffset? DueAt, string? Reason);

public sealed record InventoryPilotStage(int Position, string Name, string Role, string Action,
    string ReleaseCondition, int DeadlineHours, int? ReturnToPosition);

public static class OperationalSequenceRules
{
    public static readonly IReadOnlyList<InventoryPilotStage> InventoryPilot = Array.AsReadOnly<InventoryPilotStage>([
        new(1, "Registrar inventario completo", CanonicalRole.SalesFloor, "ENTREGAR", "INICIO", 6, null),
        new(2, "Revisar inventario en Sizes & Colors", CanonicalRole.Subcoordination, "REVISAR", "ENTREGA", 1, 1),
        new(3, "Chequear hoja e irregularidades", CanonicalRole.Administration, "REVISAR", "APROBACION", 1, 2),
        new(4, "Aceptar resultado del inventario", CanonicalRole.Direction, "AUTORIZAR", "APROBACION", 168, 3),
    ]);
    public static IEnumerable<DateOnly> Dates(DateOnly from, DateOnly through, OperationalSequenceVersion version)
    {
        if (version.StartKind == "OCASIONAL") yield break;
        if (version.StartKind != "SEMESTRAL" || version.FirstMonth is < 1 or > 6 ||
            version.WindowFromDay is < 1 or > 28 || version.WindowThroughDay < version.WindowFromDay ||
            version.WindowThroughDay > 28 || version.PreferredWeekDay is < 0 or > 6 || from > through)
            throw Error("SEQUENCE_CALENDAR_INVALID");
        for (var year = from.Year; year <= through.Year; year++)
            foreach (var month in new[] { version.FirstMonth, version.FirstMonth + 6 })
            {
                var first = new DateOnly(year, month, version.WindowFromDay);
                var delta = (version.PreferredWeekDay - (int)first.DayOfWeek + 7) % 7;
                var selected = delta <= version.WindowThroughDay - version.WindowFromDay ? first.AddDays(delta) : first;
                if (selected >= from && selected <= through) yield return selected;
            }
    }

    // EligibleAt is independent of when a screen or timer processes the stage.
    // A missing assignee postpones effective enablement, not the dependency event.
    public static SequenceEnablement Enable(DateTimeOffset? dependencyAt, DateTimeOffset? minimumAt,
        string deadlineKind, int deadlineHours, DateTimeOffset? fixedDueAt, bool hasEligibleAssignee, DateTimeOffset now,
        DateTimeOffset? assignmentReadyAt = null)
    {
        if (deadlineKind is not ("RELATIVO" or "FIJO") || deadlineHours is < 1 or > 8760 ||
            deadlineKind == "FIJO" && fixedDueAt is null)
            throw Error("SEQUENCE_DEADLINE_INVALID");
        if (dependencyAt is null) return new("EN_ESPERA", null, null, null, "Espera la etapa anterior");
        var eligible = minimumAt is { } min && min > dependencyAt ? min : dependencyAt.Value;
        if (deadlineKind == "FIJO" && fixedDueAt <= eligible)
            return fixedDueAt <= now
                ? new("FUERA_DE_PLAZO", eligible, null, null, "El vencimiento fijo pasó antes de poder habilitar la etapa")
                : new("PROGRAMADA", eligible, null, null, "El inicio mínimo es posterior al límite fijo; no existe un intervalo válido");
        if (eligible > now) return new("PROGRAMADA", eligible, null, null, "Espera su inicio mínimo");
        if (!hasEligibleAssignee) return new("PENDIENTE_ASIGNACION", eligible, null, null, "No hay responsable elegible con capacidad");
        var enabled = assignmentReadyAt is { } ready && ready > eligible ? ready : eligible;
        if (enabled > now) return new("PROGRAMADA", eligible, null, null, "Espera la disponibilidad del responsable");
        if (deadlineKind == "FIJO" && fixedDueAt <= enabled)
            return new("FUERA_DE_PLAZO", eligible, null, null, "El vencimiento fijo pasó antes de asignar la etapa");
        try
        {
            return new("DISPONIBLE", eligible, enabled,
                deadlineKind == "FIJO" ? fixedDueAt : enabled.AddHours(deadlineHours), null);
        }
        catch (ArgumentOutOfRangeException) { throw Error("SEQUENCE_DEADLINE_INVALID"); }
    }

    public static bool Satisfies(string condition, string eventKind, string? completionKind,
        bool currentAttempt, bool currentRound) => currentAttempt && currentRound &&
        (condition == "ENTREGA" && eventKind == "ENTREGA" && completionKind == "ENTREGA" ||
         condition == "APROBACION" && eventKind == "APROBACION");

    public static void RequireReturn(string runStatus, int activePosition, int sourcePosition,
        int targetPosition, int? allowedTarget, string? reason, bool currentAttempt)
    {
        if (runStatus != "EN_CURSO" || activePosition != sourcePosition || !currentAttempt)
            throw new DirectionPlanException("SEQUENCE_CONFLICT", 409);
        if (targetPosition < 1 || targetPosition >= sourcePosition || targetPosition != allowedTarget)
            throw Error("SEQUENCE_RETURN_TARGET_INVALID");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 4000)
            throw Error("SEQUENCE_RETURN_REASON_REQUIRED");
    }

    private static DirectionPlanException Error(string code) => new(code, 422);
}
