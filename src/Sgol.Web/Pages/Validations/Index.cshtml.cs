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
using Sgol.Web.Presentation.Validation;

namespace Sgol.Web.Pages.Validations;

public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection) : PageModel
{
    public ValidationQuery Query { get; private set; } = new();
    public IReadOnlyList<PendingValidationItem> Pending { get; private set; } = [];
    public IReadOnlyList<SupervisionObligationItem> Supervision { get; private set; } = [];
    public CursorPaginationViewModel? PendingPagination { get; private set; }
    public CursorPaginationViewModel? SupervisionPagination { get; private set; }
    public ProblemDetailsPresentation? PendingError { get; private set; }
    public ProblemDetailsPresentation? SupervisionError { get; private set; }
    public DateTimeOffset? PendingQueriedAt { get; private set; }
    public DateTimeOffset? SupervisionQueriedAt { get; private set; }
    public bool CanPending { get; private set; }
    public bool CanSupervision { get; private set; }
    public string[] Levels { get; private set; } = [];
    private Guid actor;
    public string TaskHref(Guid id, string section) => QueryHelpers.AddQueryString($"/mi-trabajo/tareas/{id:D}", "returnToken", new ValidationReturnContext(protection, actor).Protect(Query, section, id));
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        actor = session.UserId;
        Levels = session.RoleCode switch { "DIRECCION" => ["ADMINISTRACION", "SUBCOORDINACION", "PISO_VENTAS"], "ADMINISTRACION" => ["SUBCOORDINACION", "PISO_VENTAS"], "SUBCOORDINACION" => ["PISO_VENTAS"], _ => [] };
        CanPending = Levels.Length > 0 && session.Permissions.Any(p => p is "PER-VALIDACION-EMITIR" or "PER-VALIDACION-ESCALAR");
        CanSupervision = Levels.Length > 0 && session.Permissions.Contains("PER-SUPERVISION-VER");
        if (!CanPending && !CanSupervision) { Response.StatusCode = 403; PendingError = ValidationPresentation.Error(403, null, null, "pending"); return Page(); }
        if (!ValidationQuery.TryRead(Request.Query, out var query))
        { Response.StatusCode = 400; PendingError = ValidationPresentation.Error(400, "FILTRO_VALIDACIONES_PENDIENTES_INVALIDO", null); SupervisionError = ValidationPresentation.Error(400, "FILTRO_SUPERVISION_INVALIDO", null); return Page(); }
        Query = query;
        if (CanPending) await LoadAsync("pending", cancellationToken);
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (CanSupervision) await LoadAsync("supervision", cancellationToken);
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        return Page();
    }
    private async Task LoadAsync(string section, CancellationToken token)
    {
        try
        {
            var cursor = new MyWorkCursor(protection, actor, "front019-" + section, Query.Hash(section));
            if (!cursor.Read(Query.Value(section, "cursor"))) throw new ApiProtocolException();
            var path = section == "pending" ? "/api/v1/validations/pending" : "/api/v1/supervision/obligations";
            path = QueryHelpers.AddQueryString(path, Query.ApiValues(section, cursor.Cursor));
            if (section == "pending")
            {
                var response = await apiClient.SendAsync<PendingValidationItem>(new(HttpMethod.Get, path, ApiResponseShape.Collection), token);
                if (!response.IsSuccess) { Failure(response.Status, response.ErrorCode, response.CorrelationId, section); return; }
                foreach (var item in response.Items ?? [])
                {
                    if (item is null || item.Obligation is null) throw new ApiProtocolException();
                    MyWorkPresentation.Validate(item.Obligation with { Links = item.Links });
                    if (item.Obligation is null || item.Obligation.ObligationId == Guid.Empty || item.Obligation.ExecutionStatus != "CONCLUIDA" || !Levels.Contains(item.ResponsibleLevel) ||
                        item.AvailableAuthority is null || item.AvailableAuthority.AuthorityType is not ("ORDINARIA" or "ESCALAMIENTO") ||
                        item.AvailableAuthority.EscalationReasonRequired != (item.AvailableAuthority.AuthorityType == "ESCALAMIENTO") || !ValidationPresentation.StrongEtag(item.DecisionEtag) ||
                        item.MaterializationStatus is not ("MATERIALIZED" or "DERIVED") || (item.MaterializationStatus == "MATERIALIZED") != (item.ValidationRequirement is not null)) throw new ApiProtocolException();
                }
                Pending = response.Items ?? []; PendingQueriedAt = response.QueriedAt;
                PendingPagination = cursor.Links(response.NextCursor, c => Query.Href(section, c));
            }
            else
            {
                var response = await apiClient.SendAsync<SupervisionObligationItem>(new(HttpMethod.Get, path, ApiResponseShape.Collection), token);
                if (!response.IsSuccess) { Failure(response.Status, response.ErrorCode, response.CorrelationId, section); return; }
                foreach (var item in response.Items ?? [])
                {
                    if (item is null || item.Obligation is null) throw new ApiProtocolException();
                    MyWorkPresentation.Validate(item.Obligation with { Links = item.Links });
                    if (item.Obligation is null || item.Obligation.ObligationId == Guid.Empty || !Levels.Contains(item.ResponsibleLevel) || item.CurrentEvidence is null || item.Validation is null ||
                        item.Obligation.ExecutionStatus is not ("PENDIENTE" or "CONCLUIDA") || item.CurrentEvidence.Any(e => e is null || e.Requirement is null || e.Version is null || e.Version.Status != "VIGENTE" || e.SourceKind is not ("FILE" or "STRUCTURED"))) throw new ApiProtocolException();
                    if (item.Validation.Requirement is { } requirement && requirement.Status is not ("PENDIENTE" or "RESUELTA")) throw new ApiProtocolException();
                    if (item.Validation.CurrentDecision is { } decision) { ValidationPresentation.Validate(decision); if (decision.Status != "VIGENTE") throw new ApiProtocolException(); }
                }
                Supervision = response.Items ?? []; SupervisionQueriedAt = response.QueriedAt;
                SupervisionPagination = cursor.Links(response.NextCursor, c => Query.Href(section, c));
            }
        }
        catch (ApiProtocolException) { Failure(503, null, null, section); }
    }
    private void Failure(int status, string? code, string? correlation, string section)
    {
        if (status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); }
        Response.StatusCode = status;
        if (section == "pending") { Pending = []; PendingError = ValidationPresentation.Error(status, code, correlation, section); }
        else { Supervision = []; SupervisionError = ValidationPresentation.Error(status, code, correlation, section); }
    }
}
