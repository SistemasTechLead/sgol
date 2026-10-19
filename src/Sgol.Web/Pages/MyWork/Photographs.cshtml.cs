using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.MyWork;
public sealed class PhotographsModel(IRazorSessionState sessions, OperationalTaskService tasks) : PageModel
{
    public Guid AssignmentId { get; private set; }
    public IReadOnlyList<OperationalPhotographDetails> Photos { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessions.GetAsync(token); if (session is null) return Redirect("/acceso");
        try
        {
            AssignmentId = id; Photos = await tasks.PhotographsAsync(session.UserId, id, token);
            return Photos.Count == 0 ? NotFound() : Page();
        }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
}
