using Microsoft.AspNetCore.Mvc;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class IndexModel
{
    public TaskBonusResult? Bonus { get; private set; }
    public DateOnly BonusCurrentWeek { get; private set; }
    public string? BonusError { get; private set; }
    private async Task LoadBonusAsync(Guid actor, CancellationToken token)
    {
        var service = HttpContext.RequestServices.GetService<TaskBonusService>();
        if (service == null) return;
        BonusCurrentWeek = service.CurrentWeek;
        DateOnly? week = null;
        if (Request.Query.TryGetValue("BonusWeek", out var query))
        {
            if (!DateOnly.TryParseExact(query.ToString(), "yyyy-MM-dd", out var parsed)) { BonusError = "Selecciona una semana válida para consultar el bono."; return; }
            week = parsed;
        }
        try { Bonus = (await service.ReadAsync(actor, week, true, token)).Single(); }
        catch (DirectionPlanException) { BonusError = "No se pudo consultar esta semana del bono. Revisa el periodo y vuelve a intentarlo."; }
    }
    public async Task<IActionResult> OnGetBonusAsync(CancellationToken token)
    {
        NoStore();
        if (Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsScreen(User)) return StatusCode(403);
        var session = await sessionState.GetAsync(token);
        if (session == null) return Unauthorized();
        await LoadBonusAsync(session.UserId, token);
        if (Bonus == null) return StatusCode(503);
        Response.Headers.ETag = '"' + Bonus.ResultVersion + '"';
        if (Request.Headers.IfNoneMatch == Response.Headers.ETag) return StatusCode(304);
        return new JsonResult(new[] { Bonus });
    }
}
