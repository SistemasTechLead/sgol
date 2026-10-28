using System.Text.Json;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// The required role belongs to the definition; Assignment.AssignedRole describes its worker.
public static class AssignmentDefinition
{
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static string Capture(OperationalTask task) => JsonSerializer.Serialize(task);
    public static bool IsMetadataField(string field) => field is "Nombre" or "Descripción" or "Requisito" or "Límites de imágenes" or "Checklist";
    public static bool Pending(OperationalAssignment a) => a.RetiredAt == null && a.CompletedAt == null &&
        a.ReviewStatus is not ("CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" or "PENDIENTE_REVISION");

    public static OperationalTask Baseline(OperationalAssignment a, OperationalTask current)
    {
        if (a.DefinitionSnapshotJson != null) return JsonSerializer.Deserialize<OperationalTask>(a.DefinitionSnapshotJson)!;
        var old = Copy(current);
        old.Name = a.Name; old.Description = a.Description; old.Requirement = a.Requirement; old.ChecklistJson = a.ChecklistJson;
        old.MinimumPhotos = a.MinimumPhotos; old.MaximumPhotos = a.MaximumPhotos; old.MaximumPhotoBytes = a.MaximumPhotoBytes;
        old.AssignedRole = a.SpecializationId == null ? a.AssignedRole ?? current.AssignedRole : current.AssignedRole;
        old.SpecializationId = a.SpecializationId; old.LoadMinutes = a.LoadMinutes;
        old.OperationalCriticality = a.OperationalCriticality;
        old.PeriodType = a.PeriodType; old.ScheduleMode = a.ScheduleMode; old.Repetitions = a.Repetitions;
        old.GoalUnits = a.GoalUnits; old.ProgressUnit = a.ProgressUnit; old.DifferentPeople = a.DifferentPeople;
        old.PreferredMonthDays = a.PreferredMonthDays;
        return old;
    }

    public static string[] Differences(OperationalAssignment a, OperationalTask t, bool process = false)
    {
        var old = Baseline(a, t);
        var changes = new List<string>();
        void Change(bool test, string label) { if (test) changes.Add(label); }
        Change(a.OccasionId == null && a.Name != t.Name, "Nombre");
        Change(a.Description != t.Description, "Descripción");
        Change(a.Requirement != t.Requirement, "Requisito");
        Change(a.ChecklistJson != t.ChecklistJson, "Checklist");
        Change(a.MinimumPhotos != t.MinimumPhotos || a.MaximumPhotos != t.MaximumPhotos || a.MaximumPhotoBytes != t.MaximumPhotoBytes, "Límites de imágenes");
        Change(old.AssignedRole != t.AssignedRole || a.SpecializationId != t.SpecializationId || t.SpecializationId == null && a.AssignedRole != null && a.AssignedRole != t.AssignedRole, "Elegibilidad");
        Change(a.LoadMinutes != t.LoadMinutes, "Carga");
        Change(a.OperationalCriticality != t.OperationalCriticality, "Criticidad");
        Change(!process && old.ProcessId != t.ProcessId, "Pertenencia al proceso");
        if (!process && a.OccasionId == null)
        {
            Change(a.ScheduleMode != t.ScheduleMode, "Modalidad");
            Change(t.ScheduleMode == "PROGRESO" && (a.GoalUnits != t.GoalUnits || a.ProgressUnit != t.ProgressUnit), "Meta o unidad");
            Change(a.PeriodType != t.PeriodType || a.Repetitions != t.Repetitions || old.WeekDays != t.WeekDays ||
                a.PreferredMonthDays != t.PreferredMonthDays || a.DifferentPeople != t.DifferentPeople, "Recurrencia");
            Change(old.StartHour != t.StartHour || old.StartMinute != t.StartMinute || old.SundayStartMinute != t.SundayStartMinute, "Hora de activación");
            Change(old.Active != t.Active || old.IsWeeklyOperationsTask != t.IsWeeklyOperationsTask, "Participación en planes");
        }
        if (!process && a.OccasionId == null && changes.Any(f => !IsMetadataField(f)) && a.TaskRevision != t.Revision &&
            (a.DefinitionSnapshotJson == null || JsonSerializer.Deserialize<JsonElement>(a.DefinitionSnapshotJson).TryGetProperty("_CalendarKnown", out var known) && !known.GetBoolean()))
            changes.Add("Calendario anterior sin verificar");
        return changes.ToArray();
    }

    public static void AdoptMetadata(OperationalAssignment a, OperationalTask t)
    {
        // A partial adoption must not claim a new calendar, role, workload or goal.
        var snapshot = a.DefinitionSnapshotJson != null ? System.Text.Json.Nodes.JsonNode.Parse(a.DefinitionSnapshotJson)!
            : JsonSerializer.SerializeToNode(Baseline(a,t))!;
        if (a.DefinitionSnapshotJson == null) snapshot["_CalendarKnown"] = a.TaskRevision == t.Revision;
        if (a.OccasionId == null) { a.Name = t.Name; snapshot["Name"] = t.Name; }
        a.ChecklistDraftJson = OperationalChecklist.Reconcile(a.ChecklistJson, t.ChecklistJson, a.ChecklistDraftJson);
        a.ChecklistJson = t.ChecklistJson;
        a.Description = t.Description; a.Requirement = t.Requirement;
        a.MinimumPhotos = t.MinimumPhotos; a.MaximumPhotos = t.MaximumPhotos; a.MaximumPhotoBytes = t.MaximumPhotoBytes;
        snapshot["ChecklistJson"] = t.ChecklistJson;
        snapshot["Description"] = t.Description; snapshot["Requirement"] = t.Requirement;
        snapshot["MinimumPhotos"] = t.MinimumPhotos; snapshot["MaximumPhotos"] = t.MaximumPhotos; snapshot["MaximumPhotoBytes"] = t.MaximumPhotoBytes;
        a.DefinitionSnapshotJson = snapshot.ToJsonString();
    }

    public static void Adopt(OperationalAssignment a, OperationalTask t, string? specialty, bool process)
    {
        if (a.OccasionId == null) a.Name = t.Name;
        a.ChecklistDraftJson = OperationalChecklist.Reconcile(a.ChecklistJson, t.ChecklistJson, a.ChecklistDraftJson);
        a.ChecklistJson = t.ChecklistJson;
        a.Description = t.Description; a.Requirement = t.Requirement;
        a.MinimumPhotos = t.MinimumPhotos; a.MaximumPhotos = t.MaximumPhotos; a.MaximumPhotoBytes = t.MaximumPhotoBytes;
        a.SpecializationId = t.SpecializationId; a.SpecializationName = specialty;
        a.LoadMinutes = t.LoadMinutes; a.OperationalCriticality = t.OperationalCriticality;
        if (!process && a.OccasionId == null)
        {
            a.PeriodType = t.PeriodType; a.Repetitions = t.Repetitions; a.ScheduleMode = t.ScheduleMode;
            a.GoalUnits = t.ScheduleMode == "PROGRESO" ? t.GoalUnits ?? 0 : 0;
            a.ProgressUnit = t.ProgressUnit; a.DifferentPeople = t.DifferentPeople; a.PreferredMonthDays = t.PreferredMonthDays;
        }
        a.TaskRevision = t.Revision; a.DefinitionSnapshotJson = Capture(t);
    }
}
