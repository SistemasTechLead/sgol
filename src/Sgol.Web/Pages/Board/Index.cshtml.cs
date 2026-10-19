using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Board;

public sealed class IndexModel(IRazorSessionState sessions, LiveBoardReader board,
    Sgol.Web.Infrastructure.Persistence.Identity.ScreenDeviceService? screenDevices = null) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? Plan { get; set; }
    [BindProperty(SupportsGet = true, Name = "task")] public Guid? SelectedTask { get; set; }
    public LiveBoardSnapshot Snapshot { get; private set; } = null!;

    public Task<IActionResult> OnGetAsync(CancellationToken token) => LoadAsync(false, token);
    public Task<IActionResult> OnGetRefreshAsync(CancellationToken token) => LoadAsync(true, token);

    public async Task<IActionResult> OnGetEventsAsync(CancellationToken token)
    {
        var session = await sessions.GetAsync(token);
        if (session is null) return Unauthorized();
        if (!ModelState.IsValid) return BadRequest();
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        string? version = null;
        try
        {
            // Short-lived streams renew normal session validation and work across server restarts.
            for (var tick = 0; tick < 45 && !token.IsCancellationRequested; tick++)
            {
                if (Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsScreen(User) &&
                    (screenDevices is null || !Guid.TryParse(User.FindFirst(Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.Stamp)?.Value, out var stamp) ||
                     await screenDevices.ValidateAsync(session.UserId, stamp, token) is null))
                {
                    await Response.WriteAsync("event: expired\ndata: {}\n\n", token);
                    await Response.Body.FlushAsync(token);
                    break;
                }
                var current = await board.ReadAsync(session.UserId, Plan, token, SelectedTask);
                if (current.Now >= session.AbsoluteExpiresAt)
                {
                    await Response.WriteAsync("event: expired\ndata: {}\n\n", token);
                    await Response.Body.FlushAsync(token);
                    break;
                }
                if (version != current.Version)
                {
                    version = current.Version;
                    await Response.WriteAsync("event: changed\ndata: {}\n\n", token);
                }
                else await Response.WriteAsync(": heartbeat\n\n", token);
                await Response.Body.FlushAsync(token);
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        }
        catch (DirectionPlanException)
        {
            await Response.WriteAsync("event: expired\ndata: {}\n\n", token);
            await Response.Body.FlushAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        return new EmptyResult();
    }

    private async Task<IActionResult> LoadAsync(bool refresh, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessions.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!ModelState.IsValid) return BadRequest();
        try { Snapshot = await board.ReadAsync(session.UserId, Plan, token, SelectedTask); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
        return refresh ? Partial("_BoardContent", Snapshot) : Page();
    }
}
