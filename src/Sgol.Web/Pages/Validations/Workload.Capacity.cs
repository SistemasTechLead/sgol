using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Validations;

public sealed partial class WorkloadModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? CapacityFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? CapacityThrough { get; set; }
    [BindProperty] public CapacityForm Adjustment { get; set; } = new();
    [BindProperty] public string? CapacityFingerprint { get; set; }
    public IReadOnlyList<CapacityRow> CapacityRows { get; private set; } = [];
    public CapacityPreview? CapacityPreview { get; private set; }
    [TempData] public string? CapacitySuccess { get; set; }
    private void NormalizeCapacityRange()
    {
        var today = WeekContract.LocalToday(Now);
        CapacityFrom ??= today;
        if (CapacityFrom.Value.Year > 9998) CapacityFrom = today;
        CapacityThrough ??= CapacityFrom.Value.AddDays(6 - (((int)CapacityFrom.Value.DayOfWeek + 6) % 7));
        if (CapacityFrom < today || CapacityThrough < CapacityFrom || CapacityThrough.Value.DayNumber - CapacityFrom.Value.DayNumber > 90)
        {
            Error ??= Message("CAPACITY_INVALID");
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            CapacityFrom = today; CapacityThrough = today;
            ModelState.Remove(nameof(CapacityFrom)); ModelState.Remove(nameof(CapacityThrough));
        }
    }
    private async Task LoadCapacityAsync(Guid actor, CancellationToken token)
    {
        NormalizeCapacityRange();
        CapacityRows = await capacity.RowsAsync(actor, CapacityFrom!.Value, CapacityThrough!.Value, token);
    }
    public async Task<IActionResult> OnPostCapacityPreviewAsync(CancellationToken token) => await CapacityActionAsync(false, token);
    public async Task<IActionResult> OnPostCapacityApplyAsync(CancellationToken token) => await CapacityActionAsync(true, token);
    private async Task<IActionResult> CapacityActionAsync(bool apply, CancellationToken token)
    {
        Guid? actor = null;
        try {
            actor = await ActorAsync(token); if (actor is null) return Redirect("/acceso");
            foreach (var key in ModelState.Keys.Where(k => k == nameof(Change) || k.StartsWith(nameof(Change) + ".")).ToArray()) ModelState.Remove(key);
            if (!ModelState.IsValid) throw new DirectionPlanException("CAPACITY_INVALID", 422);
            var change = Adjustment.Value(); CapacityFrom = change.From; CapacityThrough = change.Through;
            if (apply) {
                var result = await capacity.ApplyAsync(actor.Value, change, CapacityFingerprint ?? "", token);
                CapacitySuccess = $"Capacidad actualizada. Se movieron {result.Moves.Count} tareas ({result.Moves.Sum(m => m.Minutes)} minutos)." +
                    (result.RemainingExcess > 0 ? $" Quedan {result.RemainingExcess} minutos de exceso que requieren atención." : "");
                return Redirect(QueryHelpers.AddQueryString("/supervision/carga", new Dictionary<string, string?> { ["CapacityFrom"] = change.From.ToString("yyyy-MM-dd"), ["CapacityThrough"] = change.Through.ToString("yyyy-MM-dd") }) + "#capacity");
            }
            CapacityPreview = await capacity.PreviewAsync(actor.Value, change, token);
            Adjustment.SchedulingNotBefore = CapacityPreview.Change.SchedulingNotBefore;
            ModelState.Remove("Adjustment.SchedulingNotBefore");
            CapacityFingerprint = CapacityPreview.Fingerprint;
            ModelState.Remove(nameof(CapacityFingerprint));
            await LoadAsync(actor.Value, null, false, token); return Page();
        } catch (DirectionPlanException e) {
            if (actor is null || e.Status == 403) return StatusCode(e.Status);
            Response.StatusCode = e.Status; Error = Message(e.Code);
            CapacityPreview = null; CapacityFingerprint = null;
            ModelState.Remove(nameof(CapacityFingerprint));
            NormalizeCapacityRange();
            await LoadAsync(actor.Value, null, false, token); return Page();
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
