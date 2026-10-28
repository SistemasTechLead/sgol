using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.MyWork;

public sealed class CarryoverModel(IRazorSessionState sessions, OperationalCarryoverService service, IClock clock) : PageModel
{
    [BindProperty] public string Cause { get; set; } = "FALTA_TIEMPO_OPERATIVO";
    [BindProperty] public int Minutes { get; set; } = 15;
    [BindProperty] public DateTime End { get; set; }
    [BindProperty] public string? Explanation { get; set; }
    [BindProperty] public string? Fingerprint { get; set; }
    [BindProperty] public Guid Intent { get; set; } = Guid.NewGuid();
    [BindProperty] public Guid[] Selected { get; set; } = [];
    [BindProperty] public int? BeforeWeekday { get; set; }
    [BindProperty] public string PostponeCause { get; set; } = "FALTA_TIEMPO_OPERATIVO";
    [BindProperty] public string? PostponeExplanation { get; set; }
    [BindProperty] public string? PostponeFingerprint { get; set; }
    [BindProperty] public Guid PostponeIntent { get; set; } = Guid.NewGuid();
    public OverdueCarryoverTask[] Overdue { get; private set; } = [];
    public PostponementPreview? Postponement { get; private set; }
    public CarryoverPreview? Preview { get; private set; }
    public OperationalCarryoverRequest[] History { get; private set; } = [];
    public string? Error { get; private set; }
    [TempData] public string? Success { get; set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) {
        Response.Headers.CacheControl = "private, no-store";
        var s = await sessions.GetAsync(ct); if (s == null) return Redirect("/acceso");
        End = TimeZoneInfo.ConvertTime(clock.UtcNow, OperationalSchedule.Zone).DateTime;
        try { History = await service.MineAsync(s.UserId, ct); Overdue = await service.OverdueAsync(s.UserId, ct); } catch (DirectionPlanException e) { return StatusCode(e.Status); }
        return Page();
    }
    public Task<IActionResult> OnPostPreviewAsync(CancellationToken ct) => Post(false, ct);
    public Task<IActionResult> OnPostSubmitAsync(CancellationToken ct) => Post(true, ct);
    public Task<IActionResult> OnPostPreviewPostponementAsync(CancellationToken ct) => Postpone(false, ct);
    public Task<IActionResult> OnPostSubmitPostponementAsync(CancellationToken ct) => Postpone(true, ct);
    private async Task<IActionResult> Postpone(bool submit, CancellationToken ct) {
        Response.Headers.CacheControl = "private, no-store";
        var s = await sessions.GetAsync(ct); if (s == null) return Redirect("/acceso");
        foreach (var field in new[] { nameof(Cause), nameof(Minutes), nameof(End), nameof(Explanation), nameof(Fingerprint), nameof(Intent) }) ModelState.Remove(field);
        try {
            if (!ModelState.IsValid) throw new DirectionPlanException("ARRASTRE_DATOS_INVALIDOS", 422);
            if (submit) {
                await service.SubmitPostponementAsync(s.UserId, Selected, PostponeCause, PostponeExplanation, PostponeFingerprint ?? "", PostponeIntent, ct, BeforeWeekday);
                Success = "Petición enviada al supervisor inmediato. Las tareas conservan sus fechas hasta la autorización.";
                return RedirectToPage();
            }
            Postponement = await service.PreviewPostponementAsync(s.UserId, Selected, PostponeCause, PostponeExplanation, ct, BeforeWeekday);
            PostponeFingerprint = Postponement.Fingerprint; ModelState.Remove(nameof(PostponeFingerprint));
        } catch (DirectionPlanException e) {
            Error = e.Code switch { "ARRASTRE_DATOS_INVALIDOS" => "Selecciona al menos una tarea y completa la causa y explicación.",
                "ARRASTRE_LIMITE_SIN_DIAS" => "No hay días disponibles entre mañana y el día indicado. El límite excluye ese día; elige uno posterior.",
                "ARRASTRE_CONFLICTO" => "La selección, los horarios o las cargas cambiaron, o una tarea ya tiene una petición pendiente. Revisa y vuelve a obtener la propuesta.",
                _ => e.Code };
        }
        End = TimeZoneInfo.ConvertTime(clock.UtcNow, OperationalSchedule.Zone).DateTime;
        History = await service.MineAsync(s.UserId, ct); Overdue = await service.OverdueAsync(s.UserId, ct);
        return Page();
    }
    private async Task<IActionResult> Post(bool submit, CancellationToken ct) {
        Response.Headers.CacheControl = "private, no-store";
        var s = await sessions.GetAsync(ct); if (s == null) return Redirect("/acceso");
        foreach (var field in new[] { nameof(Selected), nameof(BeforeWeekday), nameof(PostponeCause), nameof(PostponeExplanation), nameof(PostponeFingerprint), nameof(PostponeIntent) }) ModelState.Remove(field);
        try {
            if (!ModelState.IsValid) throw new DirectionPlanException("ARRASTRE_DATOS_INVALIDOS", 422);
            var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(End, DateTimeKind.Unspecified), OperationalSchedule.Zone));
            if (submit) {
                await service.SubmitAsync(s.UserId, end, Minutes, Cause, Explanation, Fingerprint ?? "", Intent, ct);
                Success = "Petición enviada. Los vencimientos se conservan hasta que un supervisor apruebe.";
                return RedirectToPage();
            }
            Preview = await service.PreviewAsync(s.UserId, end, Minutes, Cause, Explanation, ct);
            Fingerprint = Preview.Fingerprint;
            ModelState.Remove(nameof(Fingerprint));
        } catch (DirectionPlanException e) {
            Error = e.Code switch { "ARRASTRE_DATOS_INVALIDOS" => "Revisa causa, minutos y término. Solo se admiten incidentes de hoy, sin horas futuras. Explica la causa no identificada o la autorización comercial.",
                "ARRASTRE_CONFLICTO" => "El alcance cambió o se solapa con otra petición. Obtén una nueva vista previa.", _ => e.Code };
        }
        History = await service.MineAsync(s.UserId, ct); Overdue = await service.OverdueAsync(s.UserId, ct); return Page();
    }
}
