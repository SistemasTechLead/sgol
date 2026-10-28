using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Navigation;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Tasks;

public sealed class RefreshModel(IRazorSessionState sessions, AssignmentRefreshService refresh, SgolDbContext db, IClock clock) : PageModel
{
    [BindProperty] public Guid Key { get; set; }
    [BindProperty] public DateTimeOffset NotBefore { get; set; }
    [BindProperty] public bool ReplaceRemainingEstimate { get; set; }
    [BindProperty] public bool ChangeAuthorizedDeadline { get; set; }
    [BindProperty] public bool AdoptLegacyCalendar { get; set; }
    [BindProperty] public bool RetireInactiveAssignments { get; set; }
    [BindProperty] public bool RetireExcludedAssignments { get; set; }
    [BindProperty] public string Fingerprint { get; set; } = "";
    [BindProperty] public string Reason { get; set; } = "";
    public AssignmentRefreshPreview? Preview { get; private set; }
    public Dictionary<Guid, string> Plans { get; private set; } = [];
    public Dictionary<string, string> DecisionTasks { get; private set; } = [];
    public string? Error { get; private set; }
    private AssignmentRefreshRequest RequestInput => new(Guid.Empty, Key, NotBefore, null, ReplaceRemainingEstimate, ChangeAuthorizedDeadline, AdoptLegacyCalendar, RetireInactiveAssignments, RetireExcludedAssignments);
    private async Task<Guid?> Actor(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(ct);
        return session?.RoleCode == CanonicalRole.Direction ? session.UserId : null;
    }
    private async Task Load(Guid actor, CancellationToken ct)
    {
        Preview = await refresh.PreviewAsync(actor, RequestInput, ct);
        Fingerprint = Preview.Fingerprint;
        var planIds = Preview.Differences.Select(d => (d.After ?? d.Before)!.PlanId).Distinct().ToArray();
        Plans = await db.DirectionPreparedPlans.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var codes = Preview.Decisions.SelectMany(d => d.TaskCodes).Distinct().ToArray();
        DecisionTasks = await db.OperationalTasks.AsNoTracking().Where(t => codes.Contains(t.TaskCode)).ToDictionaryAsync(t => t.TaskCode, t => t.Name, ct);
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        Key = Guid.CreateVersion7(); ResetTime();
        try { await Load(actor.Value, ct); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status, Message(e.Code)); }
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        Key = Guid.CreateVersion7(); ResetTime(); ModelState.Clear();
        try { await Load(actor.Value, ct); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status, Message(e.Code)); }
    }
    public async Task<IActionResult> OnPostApplyAsync(CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return StatusCode(403);
        if (!ModelState.IsValid) { Error = "Revisa los datos y vuelve a calcular la propuesta."; }
        else
        {
            try
            {
                await refresh.ApplyAsync(actor.Value, RequestInput, Fingerprint, Reason, ct);
                TempData["AssignmentsUpdated"] = "Asignaciones actualizadas. Se conservaron avances, evidencias y el historial de cambios.";
                return RedirectToPage("Index");
            }
            catch (DirectionPlanException e) { Error = Message(e.Code); }
            catch (DbUpdateConcurrencyException) { Error = Message("REFRESH_STALE"); }
        }
        Key = Guid.CreateVersion7(); ResetTime(); ModelState.Clear();
        await Load(actor.Value, ct); return Page();
    }
    private void ResetTime() => NotBefore = DateTimeOffset.FromUnixTimeSeconds((clock.UtcNow.ToUnixTimeSeconds() / 60 + 3) * 60);
    private static string Message(string code) => code switch {
        "REFRESH_STALE" => "La propuesta venció o cambió el trabajo mientras la revisabas. Se calculó otra vista previa; revisa los cambios antes de aplicar.",
        "REFRESH_REASON_REQUIRED" => "Escribe el motivo de la actualización (hasta 1,000 caracteres).",
        "REFRESH_INVALID" => "La solicitud no es válida. Abre otra vista previa desde Tareas.",
        _ => IndexModel.Message(code)
    };
    public static string Date(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, OperationalSchedule.Zone).ToString("dd/MM/yyyy HH:mm");
    public static string DecisionField(string code) => code switch {
        "excluded" => nameof(RetireExcludedAssignments), "inactive" => nameof(RetireInactiveAssignments), "calendar" => nameof(AdoptLegacyCalendar),
        "remaining" => nameof(ReplaceRemainingEstimate), "deadline" => nameof(ChangeAuthorizedDeadline), _ => throw new InvalidOperationException("Unknown decision") };
    public static string[] Changes(AssignmentRefreshDifference diff)
    {
        if (diff.Retire || diff.After == null) return ["Se quitará del trabajo activo. Sus avances y archivos se conservarán como historial."];
        if (diff.Before == null) return ["Se agregará una asignación según el calendario actual."];
        var a = diff.Before; var b = diff.After; var changes = new List<string>();
        changes.AddRange(OperationalChecklist.Changes(a.ChecklistJson, b.ChecklistJson));
        if (a.Name != b.Name) changes.Add($"Nombre: {a.Name} → {b.Name}");
        if (a.Description != b.Description) changes.Add("Se actualizarán las instrucciones.");
        if (a.Requirement != b.Requirement) changes.Add($"Evidencia: {IndexModel.Label(a.Requirement)} → {IndexModel.Label(b.Requirement)}");
        if (a.LoadMinutes != b.LoadMinutes) changes.Add($"Carga: {a.LoadMinutes} → {b.LoadMinutes} minutos");
        if (a.GoalUnits != b.GoalUnits || a.ProgressUnit != b.ProgressUnit) changes.Add($"Meta: {a.GoalUnits} {a.ProgressUnit} → {b.GoalUnits} {b.ProgressUnit}");
        if (a.TransferRemainingMinutes != b.TransferRemainingMinutes) changes.Add($"Tiempo pendiente: {a.RemainingMinutes} → {b.RemainingMinutes} minutos");
        if (RequiredRole(a) != RequiredRole(b)) changes.Add($"Rol requerido: {RequiredRole(a)} → {RequiredRole(b)}");
        if (a.SpecializationId != b.SpecializationId) changes.Add($"Especialización: {a.SpecializationName ?? "Ninguna"} → {b.SpecializationName ?? "Ninguna"}");
        if (a.OperationalCriticality != b.OperationalCriticality) changes.Add($"Prioridad: {OperationalAgenda.Label(a.OperationalCriticality)} → {OperationalAgenda.Label(b.OperationalCriticality)}");
        if (a.MinimumPhotos != b.MinimumPhotos || a.MaximumPhotos != b.MaximumPhotos)
            changes.Add($"Cantidad de imágenes: {a.MinimumPhotos}–{a.MaximumPhotos} → {b.MinimumPhotos}–{b.MaximumPhotos}");
        if (diff.Fields.Any(f => f is "Recurrencia" or "Hora de activación" or "Modalidad")) changes.Add("Se aplicará el calendario actual de la tarea.");
        if (a.PersonId != b.PersonId) changes.Add($"Responsable: {a.PersonName} → {b.PersonName}");
        if (a.StartsAt != b.StartsAt || a.DueAt != b.DueAt) changes.Add("Se ajustará el horario. Revisa las fechas en el detalle.");
        if (b.GoalUnits > 0 && b.ProgressUnits > b.GoalUnits) changes.Add($"El avance ({b.ProgressUnits} {b.ProgressUnit}) supera la nueva meta: se conserva completo y la tarea queda lista para entregar.");
        if (changes.Count == 0) changes.Add("Se sincronizará la configuración de la asignación con la tarea.");
        return changes.ToArray();
    }
    public static string RequiredRole(OperationalAssignment a) => a.DefinitionSnapshotJson == null ? "Sin instantánea verificable" :
        IndexModel.RoleLabel(JsonSerializer.Deserialize<OperationalTask>(a.DefinitionSnapshotJson)!.AssignedRole);
    public static string Calendar(OperationalAssignment a)
    {
        if (a.OccasionId != null) return "Ocasión: conserva su ventana propia";
        if (a.DefinitionSnapshotJson == null) return "Calendario anterior sin verificar";
        var t = JsonSerializer.Deserialize<OperationalTask>(a.DefinitionSnapshotJson)!;
        var days = t.ScheduleMode == "DIAS_FIJOS" ? string.Join(", ", Enumerable.Range(0,7).Where(d => (t.WeekDays & (1 << d)) != 0)
            .Select(d => new[] { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" }[d])) : t.PreferredMonthDays;
        return $"{IndexModel.Label(t.PeriodType)} · {t.Repetitions} repeticiones · {(t.ScheduleMode == "PROGRESO" ? "Progreso" : t.ScheduleMode == "DIAS_FIJOS" ? "Días fijos" : "Distribuida")} · {days} · {t.StartHour:00}:{t.StartMinute:00}" +
            (t.SundayStartMinute is {} sunday ? $" · Domingo {sunday / 60:00}:{sunday % 60:00}" : "") +
            (t.DifferentPeople ? " · Priorizar personas distintas" : "") + (t.IsWeeklyOperationsTask ? " · Plan de operaciones semanal" : "");
    }
}
