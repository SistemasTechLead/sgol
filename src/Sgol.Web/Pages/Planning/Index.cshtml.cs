using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Planning;

[IgnoreAntiforgeryToken] // Each current planning form validates through the shared bridge.
public sealed partial class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    RazorAntiforgeryBridge antiforgery, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider protection,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery formAntiforgery) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken token)
    {
        NoStore();
        if (Request.Path.Value != "/planificacion") return Redirect("/planificacion" + Request.QueryString);
        // Existing bookmarks for the retired screen resolve to the current planning list.
        string[] retiredFilters = ["isoYear", "isoWeek", "from", "to", "day", "releaseId", "taskCode",
            "generationRequestId", "obligationId", "loadPersonId", "loadCursor", "assignmentHistoryCursor"];
        if (Request.Query.Any(q => retiredFilters.Contains(q.Key))) return Redirect("/planificacion");
        return await LoadDirectionPlansAsync(token);
    }

    public IActionResult OnPost() => NotFound();

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
