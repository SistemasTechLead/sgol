using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Configuration;

public sealed class IndexModel(IRazorSessionState sessionState) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        return Page();
    }
}
