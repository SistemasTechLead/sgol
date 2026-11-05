using System.Text.Json;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

/// <summary>Version-owned count links, frozen with each prepared plan and execution.</summary>
public static class ProcessCountSources
{
    public const int CurrentVersion = 2;

    public static Dictionary<Guid, Guid> FromSteps(OperationalSequenceStep[] steps) => Validate(steps,
        steps.Where(s => s.InputCountSourceTaskId != null).ToDictionary(s => s.TaskId, s => s.InputCountSourceTaskId!.Value));

    public static void ConvertLegacy(OperationalSequenceStep[] steps)
    {
        foreach (var producer in steps.Where(s => s.OutputTargetTaskId != null))
        {
            var recipient = steps.SingleOrDefault(s => s.TaskId == producer.OutputTargetTaskId);
            if (recipient != null && !recipient.IsFinalAudit) recipient.InputCountSourceTaskId ??= producer.TaskId;
            producer.OutputTargetTaskId = null;
        }
    }

    public static Dictionary<Guid, Guid> Read(string? json) => json == null ? [] :
        JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json) ?? throw Invalid("El plan no conserva una configuración de conteos válida.");

    public static Dictionary<Guid, Guid> Validate(OperationalSequenceStep[] steps, IReadOnlyDictionary<Guid, Guid> selected)
    {
        var result = new Dictionary<Guid, Guid>();
        foreach (var (recipientId, sourceId) in selected.OrderBy(p => p.Key))
        {
            var recipient = steps.SingleOrDefault(s => s.TaskId == recipientId);
            if (recipient == null) throw Invalid("La tarea receptora no pertenece a esta versión del proceso. Vuelve a cargar sus etapas.");
            if (sourceId == Guid.Empty) throw Invalid($"Etapa {recipient.Position}: el origen no es válido. Selecciona una etapa anterior o deja el campo sin conteo.");
            var source = steps.SingleOrDefault(s => s.TaskId == sourceId);
            if (recipient.IsFinalAudit) throw Invalid($"Etapa {recipient.Position}: la auditoría final conserva su carga propia; quita su origen de conteo.");
            if (source == null || source.Position >= recipient.Position || source.OutputItemId == null)
                throw Invalid($"Etapa {recipient.Position}: elige como origen una etapa anterior de esta versión que reporte conteo, o selecciona carga propia.");
            result.Add(recipientId, sourceId);
        }
        return result;
    }

    internal static DirectionPlanException Invalid(string detail) => new("PROCESS_COUNT_SOURCE_INVALID", 422)
        { Data = { ["CountConflict"] = detail } };
}
