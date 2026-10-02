using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Reporting.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.Reporting;

namespace Sgol.Web.Pages.Indicators;

public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection) : PageModel
{
    public IndicatorQuery Query { get; private set; } = new();
    public Dictionary<string, IndicatorSnapshot> Data { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, DateTimeOffset?> Queried { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, CursorPaginationViewModel> Pagination { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ProblemDetailsPresentation> Errors { get; } = new(StringComparer.Ordinal);
    public bool CanOperation { get; private set; }
    public bool CanDirection { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        CanOperation = session.Permissions.Contains("PER-INDICADOR-VER");
        CanDirection = session.RoleCode == "DIRECCION" && session.Permissions.Contains("PER-DIRECCION-VER");
        if (!CanOperation && !CanDirection) { Response.StatusCode = 403; Errors["operation"] = IndicatorPresentation.Error(403, null, null); return Page(); }
        if (!IndicatorQuery.TryRead(Request.Query, out var query))
        { Response.StatusCode = 400; Errors["operation"] = IndicatorPresentation.Error(400, "FILTRO_INDICADORES_INVALIDO", null); Errors["direction"] = Errors["operation"]; return Page(); }
        Query = query;
        foreach (var section in new[] { "operation", "direction" })
        {
            if (!(section == "operation" ? CanOperation : CanDirection) || !Query.Selected(section)) continue;
            try
            {
                var cursor = new MyWorkCursor(protection, session.UserId, "front020-" + section, Query.Hash(section));
                if (!cursor.Read(Query.Value(section, "cursor"))) throw new ApiProtocolException();
                var path = section == "operation" ? "/api/v1/indicators" : "/api/v1/direction/overview";
                var response = await apiClient.SendAsync<IndicatorSnapshot>(new(HttpMethod.Get, QueryHelpers.AddQueryString(path, Query.Api(section, cursor.Cursor)), ApiResponseShape.Item), cancellationToken);
                if (!response.IsSuccess)
                {
                    if (response.Status == 401) { sessionState.Invalidate(); return Redirect("/acceso"); }
                    Response.StatusCode = response.Status; Errors[section] = IndicatorPresentation.Error(response.Status, response.ErrorCode, response.CorrelationId); continue;
                }
                IndicatorPresentation.Validate(response.Data!);
                Data[section] = response.Data!; Queried[section] = response.QueriedAt;
                Pagination[section] = cursor.Links(response.NextCursor, c => Query.Href(section, c));
            }
            catch (ApiProtocolException) { Response.StatusCode = 503; Errors[section] = IndicatorPresentation.Error(503, null, null); }
        }
        return Page();
    }
}
