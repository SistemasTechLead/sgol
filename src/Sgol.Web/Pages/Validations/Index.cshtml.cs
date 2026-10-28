using Sgol.Web.Presentation.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Validations;

public sealed record SupervisionSection(string Key, string Title, IReadOnlyList<SupervisedTask> All, IReadOnlyList<SupervisedTask> Items);
public sealed record SupervisionFilterModel(IndexModel Page, SupervisionSection Section);

public sealed partial class IndexModel(IRazorSessionState sessions, OperationalSupervisionService supervision, OperationalCapacityService capacity, OperationalTaskService tasks, IClock clock, OperationalCarryoverService carryovers) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? CapacityFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? CapacityThrough { get; set; }
    [BindProperty] public CapacityForm Change { get; set; } = new();
    [BindProperty] public string? Fingerprint { get; set; }
    public IReadOnlyList<CapacityRow> CapacityRows { get; private set; } = [];
    public CapacityPreview? CapacityPreview { get; private set; }
    public IReadOnlyList<OperationalProgressEntry> ProgressHistory { get; private set; } = [];
    public IReadOnlyList<OperationalPhotographDetails> Photographs { get; private set; } = [];
    [TempData] public string? CapacitySuccess { get; set; }
    public IReadOnlyList<SupervisionSection> Sections { get; private set; } = [];
    public SupervisedTask? Selected { get; private set; }
    public OccasionDetails? Occasion { get; private set; }
    public string? Error { get; private set; }
    public string? AttemptedDecision { get; private set; }
    public string? AttemptedComment { get; private set; }
    public string Role { get; private set; } = "";
    public DateTimeOffset Now => clock.UtcNow;
    public string Value(string key, string field) => Request.Query[$"{key}.{field}"].ToString();
    public string TaskHref(Guid id) => QueryHelpers.AddQueryString("/validaciones", Request.Query
        .Where(p => p.Key != "task" && p.Key != "handler").ToDictionary(p => p.Key, p => (string?)p.Value.ToString())
        .Append(new KeyValuePair<string, string?>("task", id.ToString())).ToDictionary());
    public static bool Pending(OperationalAssignment a) => a.CompletedAt != null && (a.ReviewStatus == null || a.ReviewStatus == "PENDIENTE_REVISION");
    public string Status(OperationalAssignment a) => a.ReviewStatus switch {
        "CONCLUIDA" => "Concluida", "NO_SATISFECHA" => "Terminada no satisfecha", "REASIGNADA" => "No concluida · reasignada",
        _ when Pending(a) => "Pendiente de revisión", _ when a.StartsAt > Now => "Programada",
        _ when a.DueAt < Now => "Vencida", _ => "Asignada"
    };
    public string StatusCode(OperationalAssignment a) => a.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" ? a.ReviewStatus :
        Pending(a) ? "PENDIENTE_REVISION" : a.StartsAt > Now ? "PROGRAMADA" : a.DueAt < Now ? "VENCIDA" : "ASIGNADA";
    public string Tone(OperationalAssignment a) => StatusCode(a) switch {
        "CONCLUIDA" => "badge--exito", "NO_SATISFECHA" or "VENCIDA" => "badge--peligro",
        "PENDIENTE_REVISION" => "badge--advertencia", "REASIGNADA" => "badge--info", _ => "badge--neutro"
    };
    private async Task<Guid?> ActorAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessions.GetAsync(token);
        if (session is null) return null;
        Role = await supervision.AuthorizeAsync(session.UserId, token);
        return session.UserId;
    }
    public async Task<IActionResult> OnGetAsync(Guid? task, CancellationToken token)
    {
        try {
            var actor = await ActorAsync(token); if (actor is null) return Redirect("/acceso");
            await LoadAsync(actor.Value, task, token); return Page();
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    private async Task LoadAsync(Guid actor, Guid? selected, CancellationToken token)
    {
        var today = WeekContract.LocalToday(Now);
        CapacityFrom ??= today;
        CapacityThrough ??= today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7));
        if (CapacityFrom < today || CapacityThrough < CapacityFrom || CapacityThrough.Value.DayNumber - CapacityFrom.Value.DayNumber > 90)
            throw new DirectionPlanException("CAPACITY_INVALID", 422);
        Carryovers = await carryovers.InboxAsync(actor, token);
        CapacityRows = await capacity.RowsAsync(actor, CapacityFrom.Value, CapacityThrough.Value, token);
        var all = await supervision.ListAsync(actor, token);
        CarryoverCurrent = all.ToDictionary(x => x.Assignment.Id, x => x.Assignment);
        if (selected is not null) Selected = all.SingleOrDefault(t => t.Assignment.Id == selected)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (Selected?.Assignment.GoalUnits > 0) ProgressHistory = await tasks.ProgressHistoryAsync(actor, selected!.Value, token);
        if (Selected is not null) Photographs = await tasks.PhotographsAsync(actor, Selected.Assignment.Id, token);
        if (Selected?.Assignment.OccasionId != null) Occasion = await tasks.OccasionAsync(actor, Selected.Assignment.Id, token);
        var sections = new List<SupervisionSection>();
        var pending = all.Where(t => Pending(t.Assignment) &&
            (Role != CanonicalRole.Direction || t.Role == CanonicalRole.Subcoordination || t.Assignment.SpecializationId != null))
            .OrderByDescending(t => t.Assignment.CompletedAt).ToArray();
        sections.Add(new("pending", "Pendientes de revisión", pending, Filter("pending", pending)));
        foreach (var role in OperationalSupervisionService.RolesFor(Role)) {
            var tasks = all.Where(t => t.Role == role).ToArray();
            sections.Add(new(role, SessionPresentation.RoleName(role)!, tasks, Filter(role, tasks)));
        }
        Sections = sections;
        if (Validation)
            CardPhotos = await supervision.PhotoIdsAsync(actor, ValidationSection.Items.Select(t => t.Assignment.Id).ToArray(), token);
    }
    private IReadOnlyList<SupervisedTask> Filter(string key, IEnumerable<SupervisedTask> tasks)
    {
        var today = WeekContract.LocalToday(Now);
        var range = Value(key, "range");
        DateOnly? start = range switch {
            "SEMANAL" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), "MENSUAL" => new(today.Year, today.Month, 1),
            "TRIMESTRAL" => new(today.Year, (today.Month - 1) / 3 * 3 + 1, 1),
            "SEMESTRAL" => new(today.Year, (today.Month - 1) / 6 * 6 + 1, 1), "ANUAL" => new(today.Year, 1, 1), _ => null
        };
        DateOnly? end = start is { } from ? range switch {
            "SEMANAL" => from.AddDays(6), "MENSUAL" => from.AddMonths(1).AddDays(-1),
            "TRIMESTRAL" => from.AddMonths(3).AddDays(-1), "SEMESTRAL" => from.AddMonths(6).AddDays(-1),
            _ => new DateOnly(from.Year, 12, 31)
        } : null;
        if (range == "CUSTOM") {
            start = DateOnly.TryParse(Value(key, "from"), out var f) ? f : null;
            end = DateOnly.TryParse(Value(key, "to"), out var t) ? t : null;
        }
        var search = Value(key, "search").Trim();
        return tasks.Where(t => {
            var a = t.Assignment; var day = WeekContract.LocalToday(a.StartsAt);
            return (start == null || day >= start) && (end == null || day <= end) &&
                (search.Length == 0 || SearchText.Contains(a.Name, search) || SearchText.Contains(a.TaskCode, search)) &&
                (Value(key, "period") == "" || t.Period == Value(key, "period")) &&
                (Value(key, "requirement") == "" || a.Requirement == Value(key, "requirement")) &&
                (Value(key, "status") == "" || StatusCode(a) == Value(key, "status")) &&
                (Value(key, "person") == "" || a.PersonId.ToString() == Value(key, "person")) &&
                (Value(key, "plan") == "" || a.PlanId.ToString() == Value(key, "plan")) &&
                (!int.TryParse(Value(key, "repetitions"), out var repeats) || a.Repetitions == repeats) &&
                (!int.TryParse(Value(key, "hours"), out var hours) || (a.DueAt - a.StartsAt).TotalHours == hours) &&
                (!int.TryParse(Value(key, "minutes"), out var minutes) || a.LoadMinutes == minutes);
        }).ToArray();
    }
    public async Task<IActionResult> OnPostReviewAsync(Guid id, long revision, string decision, string? comment, CancellationToken token)
    {
        Guid? actor = null;
        try {
            actor = await ActorAsync(token); if (actor is null) return Redirect("/acceso");
            var correctionId = await supervision.ReviewAsync(actor.Value, id, revision, decision, comment, token);
            return Redirect(TaskHref(correctionId ?? id));
        } catch (DirectionPlanException e) {
            if (actor is null || e.Status == 403) return StatusCode(e.Status);
            Response.StatusCode = e.Status; Error = Message(e.Code);
            AttemptedDecision = decision; AttemptedComment = comment;
            await LoadAsync(actor.Value, id, token); return Page();
        }
    }
    public static string Message(string code) => code switch {
        "REVIEW_BATCH_INVALID" => "Selecciona entre 1 y 500 tareas distintas para revisar.",
        "REASSIGNMENT_NO_ELIGIBLE_RECIPIENT" => "No hay un destinatario activo y autorizado con el rol y la especialidad requeridos para esta devolución.",
        "REASSIGNMENT_NO_VALID_SLOT" => "Ningún destinatario autorizado está en turno y disponible para recibir la corrección ahora. La entrega anterior se conservó sin cambios.",
        "REASSIGNMENT_CAPACITY_EXCEEDED" => "La corrección supera la capacidad disponible del destinatario. La entrega anterior se conservó sin cambios.",
        "SCHEDULING_NO_CONTIGUOUS_SLOT" or "SCHEDULING_CRITICAL_ANCHOR_PASSED" or "SCHEDULING_CRITICAL_OVERLAP" or "SCHEDULING_LEGACY_RESERVATION" or
        "SCHEDULE_EMPLOYMENT_END" or "SCHEDULE_ACCOUNT_INACTIVE" or "SCHEDULE_ABSENT" or "SCHEDULE_MISSING" or "SCHEDULE_DEADLINE_PASSED" or
        "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" or "SCHEDULE_NOT_PRESENT_AT_RECEPTION" or "CLOSING_COVERAGE_REQUIRED" => Sgol.Web.Infrastructure.Persistence.Planning.TemporalEligibilityContext.Message(code),
        "SCHEDULING_CRITICALITY_REQUIRED" => "Configura la criticidad de la tarea antes de generar la corrección.",
        "CAPACITY_INVALID" => "Revisa la vigencia (desde hoy, máximo 91 días), el porcentaje, los minutos diarios y el motivo.",
        "CAPACITY_OVERLAP" => "Ya hay un ajuste que se cruza con estas fechas. Usa la misma vigencia del ajuste existente o un intervalo que no se cruce.",
        "CAPACITY_STALE" => "La carga o disponibilidad cambió. Calcula de nuevo la vista previa antes de aplicar.",
        "REVIEW_COMMENT_REQUIRED" => "Para marcar una tarea como no concluida, escribe un comentario de hasta 4,000 caracteres.",
        "REVIEW_INVALID" => "Selecciona una decisión para esta tarea.",
        "SCHEDULING_SEARCH_INCONCLUSIVE" => Sgol.Web.Infrastructure.Persistence.Planning.TemporalEligibilityContext.Message(code),
        "SCHEDULING_CIRCUIT_ORDER" => Sgol.Web.Infrastructure.Persistence.Planning.TemporalEligibilityContext.Message(code),
        "PLAN_NO_AVAILABLE_WORKER" => "No hay una persona disponible con capacidad suficiente para estas fechas. Revisa disponibilidad y minutos diarios en Supervisión.",
        "PLAN_DISTINCT_WORKERS_REQUIRED" => "No hay suficientes personas distintas del rol para las repeticiones de esta semana.",
        "PLAN_NO_WORKER_FOR_ROLE" => "El plan contiene un rol sin personas disponibles.",
        "DIRECTION_PLAN_TASK_INVALID" => "Revisa las tareas del plan: alguna está inactiva o ya no está disponible.",
        _ => Pages.Tasks.IndexModel.Message(code)
    };
    public async Task<IActionResult> OnPostCapacityPreviewAsync(CancellationToken token) => await CapacityActionAsync(false, token);
    public async Task<IActionResult> OnPostCapacityApplyAsync(CancellationToken token) => await CapacityActionAsync(true, token);
    private async Task<IActionResult> CapacityActionAsync(bool apply, CancellationToken token)
    {
        Guid? actor = null;
        try {
            actor = await ActorAsync(token); if (actor is null) return Redirect("/acceso");
            if (!ModelState.IsValid) throw new DirectionPlanException("CAPACITY_INVALID", 422);
            var change = Change.Value(); CapacityFrom = change.From; CapacityThrough = change.Through;
            if (apply) {
                var result = await capacity.ApplyAsync(actor.Value, change, Fingerprint ?? "", token);
                CapacitySuccess = $"Capacidad actualizada. Se movieron {result.Moves.Count} tareas ({result.Moves.Sum(m => m.Minutes)} minutos)." +
                    (result.RemainingExcess > 0 ? $" Quedan {result.RemainingExcess} minutos de exceso que requieren atención." : "");
                return Redirect(QueryHelpers.AddQueryString("/validaciones", new Dictionary<string, string?> { ["CapacityFrom"] = change.From.ToString("yyyy-MM-dd"), ["CapacityThrough"] = change.Through.ToString("yyyy-MM-dd") }) + "#capacity");
            }
            CapacityPreview = await capacity.PreviewAsync(actor.Value, change, token);
            Change.SchedulingNotBefore = CapacityPreview.Change.SchedulingNotBefore;
            ModelState.Remove("Change.SchedulingNotBefore");
            Fingerprint = CapacityPreview.Fingerprint;
            ModelState.Remove(nameof(Fingerprint));
            await LoadAsync(actor.Value, null, token); return Page();
        } catch (DirectionPlanException e) {
            if (actor is null || e.Status == 403) return StatusCode(e.Status);
            Response.StatusCode = e.Status; Error = Message(e.Code);
            CapacityFrom = CapacityThrough = null;
            await LoadAsync(actor.Value, null, token); return Page();
        }
    }
}

public sealed class CapacityForm
{
    public Guid PersonId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public int Percent { get; set; } = 100;
    public int DailyMinutes { get; set; } = OperationalCapacity.DefaultDailyMinutes;
    public string Reason { get; set; } = "";
    public bool TransferStarted { get; set; }
    public Guid[] ActivatedTaskIds { get; set; } = [];
    public DateTimeOffset? SchedulingNotBefore { get; set; }
    public CapacityChange Value() => new(PersonId, From, Through, Percent, DailyMinutes, Reason, TransferStarted, ActivatedTaskIds, SchedulingNotBefore);
}


