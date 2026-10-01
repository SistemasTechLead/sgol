using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Auditing.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Auditing;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.Reporting;

namespace Sgol.Web.Pages.Audit;
public sealed class DetailsModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection) : PageModel
{
    public AuditEventDetails? Data { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public string ReturnHref { get; private set; } = "/auditoria";
    public async Task<IActionResult> OnGetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (Request.Query.Keys.Any(k => k != "returnToken") || Request.Query.Any(p => p.Value.Count != 1)) { Response.StatusCode = 400; Error = IndicatorPresentation.Error(400, "AUDIT_FILTER_INVALID", null); return Page(); }
        ReturnHref = new AuditReturnContext(protection, session.UserId).Read(Request.Query["returnToken"], eventId);
        try
        {
            var response = await apiClient.SendAsync<AuditEventDetails>(new(HttpMethod.Get, $"/api/v1/audit-events/{eventId:D}", ApiResponseShape.Item), cancellationToken);
            if (!response.IsSuccess)
            {
                if (response.Status == 401) { sessionState.Invalidate(); return Redirect("/acceso"); }
                Response.StatusCode = response.Status; Error = IndicatorPresentation.Error(response.Status, response.ErrorCode, response.CorrelationId); return Page();
            }
            AuditPresentation.Validate(response.Data!);
            if (response.Data!.Id != eventId) throw new ApiProtocolException();
            Data = response.Data;
        }
        catch (ApiProtocolException) { Response.StatusCode = 503; Error = IndicatorPresentation.Error(503, null, null); }
        return Page();
    }
}
