using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages;

public sealed class EntryModel(IRazorSessionState sessionState, AccessNotice accessNotice) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var hadSession = Request.Cookies.ContainsKey("__Host-SGOL-Session");
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is not null) return Redirect("/mi-trabajo");
        if (hadSession && sessionState.IsInvalid)
            return Redirect("/acceso?notice=" + Uri.EscapeDataString(accessNotice.Protect(AccessNoticeKind.Ended)));
        if (!hadSession) return Redirect("/acceso");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ViewData["Title"] = "Sesión no disponible";
        return Page();
    }
}
