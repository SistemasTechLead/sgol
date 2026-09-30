using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed class DetailsModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    IDataProtectionProvider protection, AccessNotice accessNotice) : PageModel
{
    public ObligationDetail? Detail { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public CursorPaginationViewModel? Pagination { get; private set; }
    public string BackHref { get; private set; } = "/mi-trabajo";
    public string ReloadHref => QueryHelpers.AddQueryString(Request.Path,
        new Dictionary<string, string?> { ["returnToken"] = Request.Query["returnToken"].Count == 1 ? Request.Query["returnToken"].ToString() : null });
    public async Task<IActionResult> OnGetAsync(string obligationId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        BackHref = new MyWorkReturnContext(protection, session.UserId).Read(Request.Query["returnToken"].Count == 1 ? Request.Query["returnToken"].ToString() : null);
        if (!session.Permissions.Contains(ObligationQueryAuthorization.View))
        { Response.StatusCode = 403; Error = MyWorkPresentation.Message(403, "ACCESO_DENEGADO", null, false); return Page(); }
        if (!MyWorkQuery.CanonicalId(obligationId, out var id))
        { Response.StatusCode = 400; Error = MyWorkPresentation.Message(400, "OBLIGACION_ID_INVALIDO", null, false); return Page(); }
        var trail = new MyWorkCursor(protection, session.UserId, "history", obligationId);
        if (Request.Query.Keys.Any(k => k is not ("historyCursor" or "returnToken")) || Request.Query["returnToken"].Count > 1 || Request.Query["historyCursor"].Count > 1 ||
            Request.Query.ContainsKey("historyCursor") && string.IsNullOrEmpty(Request.Query["historyCursor"]) ||
            !trail.Read(Request.Query.ContainsKey("historyCursor") ? Request.Query["historyCursor"].ToString() : null))
        { Response.StatusCode = 400; Error = MyWorkPresentation.Message(400, "FILTRO_HISTORIA_INVALIDO", null, false); return Page(); }
        try
        {
            var path = QueryHelpers.AddQueryString($"/api/v1/obligations/{id:D}", new Dictionary<string, string?> { ["historyCursor"] = trail.Cursor });
            var response = await apiClient.SendAsync<ObligationDetail>(new(HttpMethod.Get, path, ApiResponseShape.Item), cancellationToken);
            if (response.Status == 401)
            {
                sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext);
                return Redirect("/acceso?notice=" + Uri.EscapeDataString(accessNotice.Protect(AccessNoticeKind.Ended)));
            }
            if (!response.IsSuccess) { Response.StatusCode = response.Status; Error = MyWorkPresentation.Message(response, false); return Page(); }
            if (response.Data is null || response.Data.ObligationId != id) throw new ApiProtocolException();
            MyWorkPresentation.Validate(response.Data);
            Pagination = trail.Links(response.HistoryNextCursor, value =>
                QueryHelpers.AddQueryString(ReloadHref, new Dictionary<string, string?> { ["historyCursor"] = value }) + "#historia");
            Detail = response.Data;
            return Page();
        }
        catch (ApiProtocolException)
        { Response.StatusCode = 503; Error = MyWorkPresentation.Message(503, null, null, false); return Page(); }
    }
}
