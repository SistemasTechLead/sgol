using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Auditing.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Auditing;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.Reporting;

namespace Sgol.Web.Pages.Audit;

public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection) : PageModel
{
    public AuditQuery Query { get; private set; } = new();
    public IReadOnlyList<AuditEventDetails> Items { get; private set; } = [];
    public AuditCompleteness? Completeness { get; private set; }
    public AuditSnapshot? Snapshot { get; private set; }
    public DateTimeOffset? QueriedAt { get; private set; }
    public CursorPaginationViewModel? Pagination { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public bool Allowed { get; private set; }
    private Guid actor;
    public string DetailHref(Guid id) => QueryHelpers.AddQueryString($"/auditoria/eventos/{id:D}", "returnToken", new AuditReturnContext(protection, actor).Protect(Query, id));
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        actor = session.UserId; Allowed = session.Permissions.Contains("PER-AUDITORIA-VER");
        if (!Allowed) { Response.StatusCode = 403; Error = IndicatorPresentation.Error(403, null, null); return Page(); }
        if (!AuditQuery.TryRead(Request.Query, out var query)) { Response.StatusCode = 400; Error = IndicatorPresentation.Error(400, "AUDIT_FILTER_INVALID", null); return Page(); }
        Query = query;
        if (!Query.Selected) return Page();
        try
        {
            var cursor = new MyWorkCursor(protection, actor, "front020-audit", Query.Hash);
            if (!cursor.Read(Query.Value("cursor"))) { Response.StatusCode = 400; Error = IndicatorPresentation.Error(400, "AUDIT_CURSOR_INVALID", null); return Page(); }
            var values = Query.Values.Where(p => p.Key is not ("cursor" or "mode")).Append(new("cursor", cursor.Cursor)).ToDictionary(p => p.Key, p => p.Value);
            var result = await apiClient.SendAsync<AuditEventDetails>(new(HttpMethod.Get, QueryHelpers.AddQueryString("/api/v1/audit-events", values), ApiResponseShape.Collection), cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); return Redirect("/acceso"); }
                Response.StatusCode = result.Status; Error = IndicatorPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId); return Page();
            }
            foreach (var item in result.Items ?? []) AuditPresentation.Validate(item);
            if (Query.Value("traceObligationId") is not null && result.AuditCompleteness is null) throw new ApiProtocolException();
            Items = result.Items ?? []; Completeness = result.AuditCompleteness; Snapshot = result.AuditSnapshot; QueriedAt = result.QueriedAt;
            Pagination = cursor.Links(result.NextCursor, c => Query.Href(c));
        }
        catch (ApiProtocolException) { Response.StatusCode = 503; Error = IndicatorPresentation.Error(503, null, null); }
        return Page();
    }
}
