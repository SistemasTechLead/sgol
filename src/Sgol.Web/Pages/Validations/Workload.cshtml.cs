using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Validations;

public sealed class WorkloadModel(IRazorSessionState sessions, OperationalSupervisionService supervision,
    OperationalTaskService tasks, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Day { get; set; }
    [BindProperty(SupportsGet = true)] public string Range { get; set; } = "day";
    [BindProperty] public TransferForm Change { get; set; } = new();
    [BindProperty] public string? Fingerprint { get; set; }
    [TempData] public string? Success { get; set; }
    public string? Error { get; private set; }
    public SupervisionWorkload Workload { get; private set; } = new([], []);
    public SupervisedTask? Selected { get; private set; }
    public CapacityWorker[] Recipients { get; private set; } = [];
    public IReadOnlyList<OperationalLifecycleEntry> History { get; private set; } = [];
    public IReadOnlyList<OperationalContribution> Contributions { get; private set; } = [];
    public TransferPreview? Preview { get; private set; }
    public DateTimeOffset Now => clock.UtcNow;
    public string Href(Guid? id = null) => $"/supervision/carga?Day={Day:yyyy-MM-dd}&Range={Range}" + (id is {} value ? $"&task={value:D}#transfer" : "");
    public string Status(OperationalAssignment a) => a.StartsAt > Now ? "Programada" : a.DueAt < Now ? "Vencida" : a.ProgressUnits > 0 ? "Con avances" : "Activa";

    private async Task<Guid?> ActorAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessions.GetAsync(token);
        if (session == null) return null;
        await supervision.AuthorizeAsync(session.UserId, token);
        return session.UserId;
    }
    private async Task LoadAsync(Guid actor, Guid? id, bool initialize, CancellationToken token)
    {
        Day ??= WeekContract.LocalToday(Now);
        if (Range is not ("day" or "week") || Day.Value.Year is < 2 or > 9998) throw new DirectionPlanException("TRANSFER_RANGE", 422);
        Workload = await supervision.WorkloadAsync(actor, Day.Value, Range == "week", token);
        if (id == null) return;
        Selected = (await supervision.ListAsync(actor, token)).SingleOrDefault(t => t.Assignment.Id == id)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        Recipients = await supervision.TransferRecipientsAsync(actor, id.Value, token);
        History = await tasks.LifecycleHistoryAsync(actor, id.Value, token);
        Contributions = await tasks.ContributionsAsync(actor, id.Value, token);
        if (initialize)
        {
            var a = Selected.Assignment;
            Change = new() { AssignmentId = a.Id, Revision = a.Revision, RemainingMinutes = Math.Max(1, a.RemainingMinutes),
                DueLocal = TimeZoneInfo.ConvertTime(a.DueAt > Now ? a.DueAt : Now.AddMinutes(30), OperationalSchedule.Zone).ToString("yyyy-MM-ddTHH:mm") };
        }
    }
    public async Task<IActionResult> OnGetAsync(Guid? task, CancellationToken token)
    {
        try {
            var actor = await ActorAsync(token); if (actor == null) return Redirect("/acceso");
            await LoadAsync(actor.Value, task, true, token); return Page();
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public Task<IActionResult> OnPostPreviewAsync(CancellationToken token) => ActAsync(false, token);
    public Task<IActionResult> OnPostApplyAsync(CancellationToken token) => ActAsync(true, token);
    private async Task<IActionResult> ActAsync(bool apply, CancellationToken token)
    {
        Guid? actor = null;
        try {
            actor = await ActorAsync(token); if (actor == null) return Redirect("/acceso");
            if (!ModelState.IsValid) throw new DirectionPlanException("TRANSFER_INVALID", 422);
            var request = Change.Request();
            if (apply) {
                await supervision.TransferAsync(actor.Value, request, Fingerprint ?? "", token);
                Success = "Responsabilidad transferida. Se conservaron los avances, su autoría y la evidencia. El cambio ya aparece en Mi trabajo.";
                return Redirect(Href(Change.AssignmentId));
            }
            Preview = await supervision.PreviewTransferAsync(actor.Value, request, token);
            Fingerprint = Preview.Fingerprint; ModelState.Remove(nameof(Fingerprint));
            await LoadAsync(actor.Value, Change.AssignmentId, false, token); return Page();
        } catch (DirectionPlanException e) {
            if (actor == null || e.Status == 403) return StatusCode(e.Status);
            Response.StatusCode = e.Status; Error = Message(e.Code); Fingerprint = null; Preview = null;
            ModelState.Remove(nameof(Fingerprint));
            if (Range is not ("day" or "week")) { Range = "day"; ModelState.Remove(nameof(Range)); }
            await LoadAsync(actor.Value, Change.AssignmentId == Guid.Empty ? null : Change.AssignmentId, false, token);
            if (Selected != null) { Change.Revision = Selected.Assignment.Revision; ModelState.Remove("Change.Revision"); }
            return Page();
        }
    }
    public static string Message(string code) => code switch {
        "TRANSFER_STALE" => "La tarea, evidencia, carga o disponibilidad cambió. Revisa los datos actuales y genera otra vista previa antes de confirmar.",
        "TRANSFER_INVALID" => "Indica destinatario, entre 1 y 4,800 minutos pendientes, un vencimiento futuro y un motivo de hasta 1,000 caracteres.",
        "TRANSFER_STATE" => "La tarea ya fue entregada o tiene una decisión final. No se puede transferir desde aquí.",
        "TRANSFER_RECIPIENT" => "Elige otro colaborador activo dentro de tu alcance y con el rol o especialidad requeridos.",
        "TRANSFER_DEADLINE" => "El nuevo vencimiento debe estar dentro del horario base o excepción del destinatario, sin ausencia ni ampliación de jornada. El cierre conserva su límite de turno.",
        "TRANSFER_SCHEDULE_REQUIRED" => "Activa y configura los horarios personales antes de transferir tareas.",
        "TRANSFER_PROCESS_STATE" => "La etapa no está activa o cambió su dependencia. Actualiza el proceso antes de transferirla.",
        "TRANSFER_RANGE" => "Selecciona una fecha válida y consulta por día o semana.",
        _ => IndexModel.Message(code)
    };
}

public sealed class TransferForm
{
    public Guid AssignmentId { get; set; }
    public long Revision { get; set; }
    public Guid RecipientId { get; set; }
    public int RemainingMinutes { get; set; }
    public string DueLocal { get; set; } = "";
    public string Reason { get; set; } = "";
    public TransferRequest Request()
    {
        if (!DateTime.TryParseExact(DueLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new DirectionPlanException("TRANSFER_INVALID", 422);
        var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), OperationalSchedule.Zone), TimeSpan.Zero);
        return new(AssignmentId, Revision, RecipientId, RemainingMinutes, utc, Reason);
    }
}
