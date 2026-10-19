using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Board;

public sealed class WeeklyModel(IRazorSessionState sessions, LiveBoardReader board) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly? Week { get; set; }
    public WeeklyBoardSnapshot Snapshot { get; private set; } = null!;
    public Task<IActionResult> OnGetAsync(CancellationToken token) => LoadAsync(false, token);
    public Task<IActionResult> OnGetRefreshAsync(CancellationToken token) => LoadAsync(true, token);

    private async Task<IActionResult> LoadAsync(bool refresh, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessions.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!ModelState.IsValid || Week is { Year: < 2 or > 9998 }) return BadRequest();
        try { Snapshot = await board.ReadWeekAsync(session.UserId, Week, token); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
        return refresh ? Partial("_WeeklyContent", Snapshot) : Page();
    }
}
