using Microsoft.AspNetCore.Mvc;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    public WeeklyRepairPreview? WeeklyRepair { get; private set; }
    public async Task<IActionResult> OnPostPreviewWeeklyRepairAsync(Guid planId, DateOnly from, DateOnly through, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token);
        if (session == null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        if (await DirectionFormAsync(token) == null) return BadRequest();
        try { WeeklyRepair = await HttpContext.RequestServices.GetRequiredService<OperationalWeeklyRepairService>().PreviewAsync(session.UserId, planId, from, through, token); }
        catch (DirectionPlanException e)
        {
            if (e.Status == 403) return StatusCode(403);
            Response.StatusCode = e.Status; DirectionError = new(Pages.Validations.IndexModel.Message(e.Code), "", null);
        }
        DirectionDetail = await HttpContext.RequestServices.GetRequiredService<IDirectionPreparedPlanService>().ReadAsync(session.UserId, planId, token);
        return await LoadDirectionPlansAsync(token);
    }
    public async Task<IActionResult> OnPostApplyWeeklyRepairAsync(Guid planId, DateOnly from, DateOnly through, string fingerprint, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token);
        if (session == null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        if (await DirectionFormAsync(token) == null) return BadRequest();
        try
        {
            await HttpContext.RequestServices.GetRequiredService<OperationalWeeklyRepairService>().ApplyAsync(session.UserId, planId, from, through, fingerprint, token);
            return Redirect($"/planificacion?planId={planId:D}");
        }
        catch (DirectionPlanException e)
        {
            if (e.Status == 403) return StatusCode(403);
            Response.StatusCode = e.Status; DirectionError = new(Pages.Validations.IndexModel.Message(e.Code), "", null);
            WeeklyRepair = await HttpContext.RequestServices.GetRequiredService<OperationalWeeklyRepairService>().PreviewAsync(session.UserId, planId, from, through, token);
            DirectionDetail = await HttpContext.RequestServices.GetRequiredService<IDirectionPreparedPlanService>().ReadAsync(session.UserId, planId, token);
            return await LoadDirectionPlansAsync(token);
        }
    }
}
