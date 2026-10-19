using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Occasional;

public sealed class IndexModel(IRazorSessionState sessions, OperationalTaskService tasks) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty] public OccasionalTaskForm Input { get; set; } = new("", "NINGUNO", 30, 24);
    public IReadOnlyList<OperationalTask> Items { get; private set; } = [];
    public string? Error { get; private set; }

    private async Task<Guid?> ActorAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(token);
        return session?.RoleCode is CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination ? session.UserId : null;
    }

    private async Task LoadAsync(Guid actor, CancellationToken token)
    {
        var all = await tasks.OccasionalTasksAsync(actor, token);
        Items = all.Where(t => string.IsNullOrWhiteSpace(Search) || t.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)
            || t.TaskCode.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return StatusCode(403);
        try { await LoadAsync(actor.Value, token); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return StatusCode(403);
        try {
            if (!ModelState.IsValid) throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422);
            var id = await tasks.CreateOccasionalTaskAsync(actor.Value, Input, token);
            return Redirect($"/asignar-tarea/asignar?taskId={id:D}");
        } catch (DirectionPlanException e) {
            if (e.Status is 403 or 404) return StatusCode(e.Status);
            Error = Tasks.IndexModel.Message(e.Code); Response.StatusCode = e.Status;
            await LoadAsync(actor.Value, token); return Page();
        }
    }
}
