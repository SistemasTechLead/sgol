using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Validations;

public sealed class IncidentsModel(IRazorSessionState sessions, OperationalIncidenceService service, IClock clock) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Day { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty] public Guid PersonId { get; set; }
    [BindProperty] public Guid Key { get; set; } = Guid.NewGuid();
    [BindProperty] public string Reason { get; set; } = "";
    [BindProperty] public DateTimeOffset NotBefore { get; set; }
    [BindProperty] public string? Fingerprint { get; set; }
    [BindProperty] public bool Reflow { get; set; }
    [BindProperty] public List<IncidenceDecisionForm> Decisions { get; set; } = [];
    public CapacityWorker[] People { get; private set; } = [];
    public IncidencePersonSummary[] Summaries { get; private set; } = [];
    public CapacityWorker[] FilteredPeople => People.Where(p => string.IsNullOrWhiteSpace(Search) || p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToArray();
    public int PageCount => Math.Max(1, (FilteredPeople.Length + 19) / 20);
    public OperationalIncidence[] History { get; private set; } = [];
    public IncidencePreview? Preview { get; private set; }
    public OperationalIncidence? Receipt { get; private set; }
    public bool CanConfirm { get; private set; }
    public string? Error { get; private set; }
    public DateOnly Today => WeekContract.LocalToday(clock.UtcNow);
    public static string Effect(IncidenceChange change) => change.Action switch {
        "RETIRE" => "Retirar del trabajo activo y excluir del bono del día; conservar historial.",
        "HOLD" => "Liberar reserva inviable y dejar la etapa pendiente de reprogramación, sin satisfacer su dependencia.",
        "HOLD_PREPARED" => "Dejar el inicio del proceso preparado pendiente de reprogramación; liberar sus reservas y conservar el plan.",
        "REDISTRIBUTE" => "Conservar obligación, plazo y bono; distribuir esfuerzo en días disponibles.",
        "MOVE" => $"Reacomodar dentro del día para {change.After?.PersonName}.",
        _ => $"Transferir a {change.After?.PersonName}; conservar avance y evidencia."
    };
    async Task<Guid?> Actor(CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessions.GetAsync(ct);
        CanConfirm = session?.RoleCode == CanonicalRole.Direction;
        return session?.UserId;
    }
    async Task Load(Guid actor, CancellationToken ct)
    {
        Day ??= Today;
        if (Day < TaskBonusCalculator.FirstWeek || Day > Today.AddDays(366)) throw new DirectionPlanException("Selecciona una fecha válida para consultar incidencias.", 422);
        People = await service.PeopleAsync(actor, Day.Value, ct);
        Summaries = await service.SummariesAsync(actor, Day.Value, ct);
        PageNumber = Math.Clamp(PageNumber, 1, PageCount);
        History = await service.HistoryAsync(actor, Day.Value, ct);
    }
    public async Task<IActionResult> OnGetAsync(Guid? receipt, CancellationToken ct)
    {
        try
        {
            var actor = await Actor(ct); if (actor == null) return Redirect("/acceso");
            await Load(actor.Value, ct);
            if (receipt != null) Receipt = History.SingleOrDefault(i => i.Id == receipt) ?? throw new DirectionPlanException("ACCESO_DENEGADO", 403);
            return Page();
        }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public Task<IActionResult> OnPostPreviewAsync(CancellationToken ct) => Act(false, ct);
    public Task<IActionResult> OnPostApplyAsync(CancellationToken ct) => Act(true, ct);
    async Task<IActionResult> Act(bool apply, CancellationToken ct)
    {
        var actor = await Actor(ct); if (actor == null) return Redirect("/acceso");
        try
        {
            await Load(actor.Value, ct);
            if (!ModelState.IsValid) throw new DirectionPlanException("Revisa persona, fecha y los datos del formulario.", 422);
            if (!CanConfirm) return StatusCode(403);
            if (!apply) NotBefore = Day == Today ? clock.UtcNow.AddMinutes(2) : OperationalSchedule.Instant(Day!.Value);
            var request = new IncidenceRequest(PersonId, Day!.Value, Key, Reason, NotBefore,
                Decisions.Where(d => !string.IsNullOrEmpty(d.Action)).Select(d => new IncidenceDecision(d.AssignmentId, d.Action, d.RecipientId, d.AuthorizeOverload)).ToArray(), Reflow);
            if (apply)
            {
                var receipt = await service.ApplyAsync(actor.Value, request, Fingerprint ?? "", ct);
                return Redirect($"/supervision/incidencias?Day={Day:yyyy-MM-dd}&receipt={receipt.Id}");
            }
            Preview = await service.PreviewAsync(actor.Value, request, ct); Fingerprint = Preview.Fingerprint;
            ModelState.Remove(nameof(Fingerprint)); ModelState.Remove(nameof(NotBefore));
            return Page();
        }
        catch (DirectionPlanException e)
        {
            if (e.Status == 403) return StatusCode(403);
            Response.StatusCode = e.Status; Error = e.Code; Fingerprint = ""; ModelState.Remove(nameof(Fingerprint));
            return Page();
        }
        catch (Exception e) when (e is Microsoft.EntityFrameworkCore.DbUpdateException or Npgsql.PostgresException)
        {
            Response.StatusCode = 409;
            Error = "No se confirmó el resultado. Consulta las incidencias de esta fecha o reintenta con la misma clave; no se duplicará una confirmación ya guardada.";
            return Page();
        }
    }
}

public sealed class IncidenceDecisionForm
{
    public Guid AssignmentId { get; set; }
    public string Action { get; set; } = "";
    public Guid? RecipientId { get; set; }
    public bool AuthorizeOverload { get; set; }
}
