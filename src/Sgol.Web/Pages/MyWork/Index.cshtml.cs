using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.MyWork;

[IgnoreAntiforgeryToken] // The shared bridge validates this Razor form and the API validates the same pair again.
public sealed class IndexModel(
    IRazorSessionState sessionState,
    RazorAntiforgeryBridge antiforgery,
    AccessNotice accessNotice) : PageModel
{
    public string? ErrorTitle { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var hadSession = Request.Cookies.ContainsKey("__Host-SGOL-Session");
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is not null) return Page();
        if (hadSession && sessionState.IsInvalid) return Access(AccessNoticeKind.Ended);
        if (!hadSession) return Redirect("/acceso");
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ErrorTitle = "Sesión no disponible";
        ErrorMessage = "No se pudo consultar tu sesión. Vuelve a intentarlo.";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null)
            return sessionState.IsInvalid ? Access(AccessNoticeKind.Ended) : Redirect("/acceso");

        ApiResponse<object>? response;
        try
        {
            response = await antiforgery.SendValidatedAsync<object>(HttpContext,
                new ApiRequest(HttpMethod.Post, "/api/v1/auth/logout", ApiResponseShape.NoContent),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            ApiCookieBridge.Clear(HttpContext);
            sessionState.Invalidate();
            return Access(AccessNoticeKind.Unconfirmed);
        }

        if (response is null || response.ErrorCode is "CSRF_INVALID" or "CSRF_INVALIDO")
        {
            ErrorTitle = "No se pudo verificar la solicitud";
            ErrorMessage = "Recarga la página antes de volver a enviarla.";
            return Page();
        }
        if (response.Status == StatusCodes.Status204NoContent)
        {
            ApiCookieBridge.Clear(HttpContext);
            sessionState.Invalidate();
            return Access(AccessNoticeKind.Closed);
        }
        if (response.Status == StatusCodes.Status401Unauthorized)
            return Access(AccessNoticeKind.Ended);

        ApiCookieBridge.Clear(HttpContext);
        sessionState.Invalidate();
        return Access(AccessNoticeKind.Unconfirmed);
    }

    private RedirectResult Access(AccessNoticeKind kind) =>
        Redirect("/acceso?notice=" + Uri.EscapeDataString(accessNotice.Protect(kind)));

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
