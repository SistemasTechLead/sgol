using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Tasks;

public sealed class ProcessItemsModel(IRazorSessionState sessions, OperationalProcessCatalogService catalog) : PageModel
{
    public ProcessItemEntry[] Items { get; private set; } = [];
    public string? Error { get; private set; }
    [BindProperty(SupportsGet = true)] public Guid? ProcessId { get; set; }
    public bool CanManage { get; private set; }
    private async Task<SessionSnapshot?> Session(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(ct);
        CanManage = session?.RoleCode == CanonicalRole.Direction;
        return session;
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var session = await Session(ct); if (session == null) return Redirect("/acceso");
        try { Items = await catalog.ItemsAsync(session.UserId, ct); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostSaveAsync(Guid id, long revision, string name, int minutes, bool active, CancellationToken ct)
    {
        var session = await Session(ct); if (!CanManage || session == null) return StatusCode(403);
        try {
            if (!ModelState.IsValid) throw new DirectionPlanException("PROCESS_ITEM_INVALID", 422);
            await catalog.SaveItemAsync(session.UserId, id, revision, name, minutes, active, ct);
            return RedirectToPage(new { ProcessId, saved = true });
        } catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; Error = IndexModel.Message(e.Code);
            Items = await catalog.ItemsAsync(session.UserId, ct); return Page();
        }
    }
}
