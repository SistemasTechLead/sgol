using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Validations;

public sealed class BonusModel(IRazorSessionState sessions, TaskBonusService bonus) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Week { get; set; }
    [BindProperty] public Guid Person { get; set; }
    [BindProperty] public Guid Obligation { get; set; }
    [BindProperty] public string Reason { get; set; } = "";
    public TaskBonusResult[] Results { get; private set; } = [];
    public DateOnly SelectedWeek => Week ?? bonus.CurrentWeek;
    public DateOnly CurrentWeek => bonus.CurrentWeek;
    public bool Director { get; private set; }
    public string? Error { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        if (Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsScreen(User)) return StatusCode(403);
        var session = await sessions.GetAsync(token);
        if (session == null) return Redirect("/acceso");
        if (!ModelState.IsValid) return BadRequest();
        Director = session.RoleCode == CanonicalRole.Direction;
        try { Results = await bonus.ReadAsync(session.UserId, Week, false, token); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnGetSnapshotAsync(CancellationToken token)
    {
        var loaded = await OnGetAsync(token);
        if (loaded is not PageResult) return loaded is RedirectResult ? Unauthorized() : loaded;
        var version = '"' + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('-', Results.OrderBy(r => r.PersonId).Select(r => r.ResultVersion))))) + '"';
        Response.Headers.ETag = version;
        if (Request.Headers.IfNoneMatch == version) return StatusCode(304);
        return new JsonResult(Results);
    }
    public async Task<IActionResult> OnPostExcludeAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        if (Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsScreen(User)) return StatusCode(403);
        var session = await sessions.GetAsync(token);
        if (session == null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest();
        try { await bonus.ExcludeAsync(session.UserId, Person, Obligation, SelectedWeek, Reason, token); return RedirectToPage(new { Week = SelectedWeek.ToString("yyyy-MM-dd") }); }
        catch (DirectionPlanException e)
        {
            if (e.Status == 403) return StatusCode(403);
            Error = e.Code switch { "BONUS_CLOSED" => "La semana ya cerró. No se puede excluir la obligación ni cambiar su resultado.",
                "BONUS_REASON_REQUIRED" => "Escribe el motivo de Dirección (5–1000 caracteres).", _ => "La obligación cambió o ya fue excluida. Recarga su detalle antes de volver a decidir." };
            var page = await OnGetAsync(token); Response.StatusCode = e.Status; return page;
        }
    }
}
