using System.ComponentModel.DataAnnotations;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence.Reporting;

public sealed class ReportFilter
{
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Kind { get; set; } = "overview";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Range { get; set; } = "week";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string DateBasis { get; set; } = "start";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Person { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Role { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Plan { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Area { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Task { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Specialization { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Status { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Requirement { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Schedule { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Frequency { get; set; } = "";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Carryover { get; set; } = "";
    public int? Repetitions { get; set; }
    public int? Hours { get; set; }
    public int? Minutes { get; set; }
    public int? ProgressMin { get; set; }
    public int? ProgressMax { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Group { get; set; } = "person";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Order { get; set; } = "due";
    [DisplayFormat(ConvertEmptyStringToNull = false)] public string Columns { get; set; } = "task,person,plan,status,due,progress";
}

public sealed record ReportDefinition(string Id, string Name, string Description);
public sealed record ReportMetric(string Label, string Value, string Hint, string Tone, string? Status = null);
public sealed record ReportSegment(string Label, decimal Value, string Tone);
public sealed record ReportPoint(string Label, decimal Value, string Caption, string? Key = null, decimal? Capacity = null,
    IReadOnlyList<ReportSegment>? Segments = null);
public sealed record ReportChart(string Title, string Unit, string Tone, IReadOnlyList<ReportPoint> Points);
public sealed record ReportRow(Guid Id, Guid PersonId, Guid PlanId, string Task, string Code, string Person,
    string Role, string Plan, string Area, string Status, string Requirement, string Schedule, string Frequency,
    int Repetitions, decimal Hours, int Minutes, int RemainingMinutes, DateTimeOffset Start,
    DateTimeOffset Due, DateTimeOffset? Delivered, DateTimeOffset? Reviewed, int Goal, int Units,
    string Unit, string Specialization, string ReviewComment, Guid? ReassignedFrom)
{
    public CarryoverTiming Carryover { get; init; } = new(null, null, 0, false, "");
    public int? TransferRemainingMinutes { get; init; }
    public int TransferProgressUnits { get; init; }
    public string ProgressAuthors { get; init; } = "";
    public string ChecklistJson { get; init; } = "[]";
    public string ChecklistDraftJson { get; init; } = "null";
    public decimal Progress => Goal > 0 ? Math.Min(100, Math.Round(100m * Units / Goal, 1)) : 0;
    public DateTimeOffset TimelinessDue => Carryover.RefreshCount > 0 ? Carryover.ReferenceDue ?? Due : Due;
    public bool Terminal => Status is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA";
}

public static class ReportCatalog
{
    public static ReportFilter Preset(string kind) => new() {
        Kind = kind,
        DateBasis = kind == "results" ? "reviewed" : kind == "overdue" ? "due" : kind == "review" ? "delivered" : "start",
        Group = kind == "plans" ? "plan" : "person"
    };
    public static bool SameConfiguration(ReportFilter left, ReportFilter right) =>
        typeof(ReportFilter).GetProperties().All(p =>
            (p.Name is nameof(ReportFilter.From) or nameof(ReportFilter.To) && left.Range != "custom" && right.Range != "custom") ||
            Equals(p.GetValue(left), p.GetValue(right)));
    public static readonly ReportDefinition[] Definitions = [
        new("overview", "Resumen operativo", "Una vista general de tareas, vencimientos y revisiones."),
        new("compliance", "Cumplimiento por responsable", "Resultados satisfactorios y pendientes por persona."),
        new("overdue", "Vencimientos y atrasos", "Tareas pendientes fuera de plazo y su antigüedad."),
        new("load", "Carga de trabajo", "Minutos estimados pendientes frente a la capacidad disponible."),
        new("goals", "Avance de metas", "Unidades realizadas, pendientes y porcentaje de avance."),
        new("review", "Pendientes de revisión", "Entregas que esperan una decisión del supervisor."),
        new("results", "Resultados de supervisión", "Conclusiones satisfactorias, no satisfechas y reasignaciones."),
        new("plans", "Seguimiento de planes", "Estado y avance de las tareas de cada plan.")];
    public static readonly Dictionary<string, string> Statuses = new() {
        ["PROGRAMADA"] = "Programada", ["ASIGNADA"] = "Asignada", ["VENCIDA"] = "Vencida",
        ["PENDIENTE_REVISION"] = "Pendiente de revisión", ["CONCLUIDA"] = "Concluida",
        ["REASIGNADA"] = "Reasignada", ["NO_SATISFECHA"] = "No satisfecha" };
    public static readonly Dictionary<string, string> Columns = new() {
        ["carryover"] = "Arrastre / puntualidad", ["referenceDue"] = "Plazo de referencia original", ["recognizedDue"] = "Plazo reconocido", ["carryoverRefs"] = "Arrastres aprobados",
        ["task"] = "Tarea", ["person"] = "Responsable", ["plan"] = "Plan", ["status"] = "Estado",
        ["start"] = "Inicio", ["due"] = "Vencimiento", ["delivered"] = "Entrega", ["reviewed"] = "Revisión",
        ["progress"] = "Avance", ["minutes"] = "Carga estimada", ["requirement"] = "Requisito",
        ["specialization"] = "Especialización", ["comment"] = "Comentario de revisión", ["checklist"] = "Checklist entregado" };
    public static string Status(OperationalAssignment a, DateTimeOffset now) =>
        a.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" ? a.ReviewStatus :
        a.CompletedAt != null ? "PENDIENTE_REVISION" : a.StartsAt > now ? "PROGRAMADA" : a.DueAt < now ? "VENCIDA" : "ASIGNADA";
    public static string Tone(string status) => status switch {
        "CONCLUIDA" => "success", "VENCIDA" or "NO_SATISFECHA" => "danger",
        "PENDIENTE_REVISION" => "warning", _ => "info" };
    public static string RoleName(string role) => Presentation.Navigation.SessionPresentation.RoleName(role) ?? role;
    public static DateOnly Day(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, OperationalSchedule.Zone).DateTime);
    public static string Time(DateTimeOffset? at) => at is { } value ? TimeZoneInfo.ConvertTime(value, OperationalSchedule.Zone).ToString("dd/MM/yyyy HH:mm") : "Sin registro";
    public static string Cell(ReportRow row, string column) => column switch {
        "checklist" => row.Requirement != "CHECKLIST" ? "—" : (row.Delivered == null ? "Borrador: " : "") + string.Join("\n", OperationalChecklist.Points(row.ChecklistJson)
            .Select(p => p.IsTitle ? p.Text : $"{(OperationalChecklist.Checked(row.ChecklistDraftJson).Contains(p.Id) ? "☑" : "☐")} {p.Text}")),
        "carryover" => row.Carryover.Label(row.Due, row.Delivered), "referenceDue" => Time(row.Carryover.ReferenceDue), "recognizedDue" => Time(row.Carryover.RecognizedDue), "carryoverRefs" => row.Carryover.References,
        "task" => row.Task, "person" => row.Person, "plan" => row.Plan, "status" => Statuses[row.Status],
        "start" => Time(row.Start), "due" => Time(row.Due), "delivered" => Time(row.Delivered), "reviewed" => Time(row.Reviewed),
        "progress" => row.Goal > 0 ? $"{row.Units} / {row.Goal} {row.Unit} ({row.Progress:0.#} %)" + (row.ProgressAuthors.Length > 0 ? $" · Autores: {row.ProgressAuthors}" : "") : "Sin meta",
        "minutes" => row.TransferRemainingMinutes != null ? $"{row.RemainingMinutes} min pendientes ({row.Minutes} min originales)" : $"{row.Minutes} min", "requirement" => row.Requirement.ToLowerInvariant(),
        "specialization" => string.IsNullOrEmpty(row.Specialization) ? "Sin especialización" : row.Specialization,
        "comment" => string.IsNullOrEmpty(row.ReviewComment) ? "Sin comentario" : row.ReviewComment, _ => "" };
}


