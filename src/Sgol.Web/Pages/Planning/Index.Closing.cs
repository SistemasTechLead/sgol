using Microsoft.AspNetCore.Mvc;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    [BindProperty] public ClosingSelectionInput Closing { get; set; } = new();
    public ClosingPlanningRow[] ClosingRows { get; private set; } = [];
    public DateOnly ClosingFrom { get; private set; }
    public string? ClosingError { get; private set; }
    private async Task LoadClosingAsync(Guid actor,CancellationToken ct)
    {
        var clock=HttpContext.RequestServices.GetRequiredService<Sgol.BuildingBlocks.Time.IClock>();
        ClosingFrom=WeekContract.LocalToday(clock.UtcNow);
        if(DateOnly.TryParse(Request.Query["closingFrom"],out var requested)) ClosingFrom=requested;
        ClosingRows=await HttpContext.RequestServices.GetRequiredService<PersonalScheduleService>().ReadClosingAsync(actor,ClosingFrom,ct);
    }
    public async Task<IActionResult> OnPostClosingAsync(CancellationToken ct)
    {
        NoStore();
        var session=await sessionState.GetAsync(ct);
        if(session?.RoleCode!=CanonicalRole.Direction) return StatusCode(403);
        try { await formAntiforgery.ValidateRequestAsync(HttpContext); }
        catch(Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return BadRequest(); }
        try
        {
            if(!ModelState.IsValid) throw new DirectionPlanException("SCHEDULE_DATE_INVALID",422);
            await HttpContext.RequestServices.GetRequiredService<PersonalScheduleService>().SaveClosingAsync(session.UserId,Closing,ct);
            TempData["ClosingSaved"]="Cobertura guardada. El último tramo de esa fecha se extiende una hora; las asignaciones existentes se conservan.";
            return Redirect($"/planificacion?closingFrom={Closing.Day:yyyy-MM-dd}");
        }
        catch(DirectionPlanException e)
        {
            ClosingError=TemporalEligibilityContext.Message(e.Code); Response.StatusCode=e.Status;
            return await LoadDirectionPlansAsync(ct);
        }
    }
}
