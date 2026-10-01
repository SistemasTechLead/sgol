using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Continuity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Continuity;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.Reporting;

namespace Sgol.Web.Pages.Continuity;
public class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient, IDataProtectionProvider protection) : PageModel
{
    public bool Allowed { get; private set; }
    public RecoveryReconciliationDetails? Data { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public string? Success { get; private set; }
    public string? Prepared { get; private set; }
    public string? ApprovalToken { get; private set; }
    public ContinuityIntention? Intention { get; private set; }
    public bool Uncertain { get; private set; }
    public bool Conflict { get; private set; }
    public string ReasonValue { get; private set; } = "";
    public bool ReasonError { get; private set; }
    public bool Cancelled { get; private set; }
    public string DetailHref => Data is null ? "/continuidad" : $"/continuidad/reconciliaciones/{Data.ReconciliationId:D}";
    private async Task<Sgol.Identity.Contracts.SessionSnapshot?> AuthorizeAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "private, no-store";
        var session = await sessionState.GetAsync(token);
        Allowed = session is { RoleCode: "DIRECCION" } && session.Permissions.Contains("PER-CONTINUIDAD-VER");
        if (session is not null && !Allowed) { Response.StatusCode = 403; Error = IndicatorPresentation.Error(403, null, null, true); }
        return session;
    }
    public async Task<IActionResult> OnGetAsync(Guid? reconciliationId, CancellationToken cancellationToken)
    {
        var session = await AuthorizeAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!Allowed) return Page();
        if (Request.Query.Count > 0)
        {
            if (Request.Path == "/continuidad" && Request.Query.Count == 1 && Request.Query["reconciliationId"].Count == 1 && IndicatorQuery.CanonicalId(Request.Query["reconciliationId"].ToString()))
                return Redirect($"/continuidad/reconciliaciones/{Request.Query["reconciliationId"]}");
            Response.StatusCode = 400; Error = IndicatorPresentation.Error(400, "SOLICITUD_RECONCILIACION_INVALIDA", null, true); return Page();
        }
        if (reconciliationId is null) return Page();
        try
        {
            var response = await apiClient.SendAsync<RecoveryReconciliationDetails>(new(HttpMethod.Get, $"/api/v1/continuity/reconciliations/{reconciliationId:D}", ApiResponseShape.Item), cancellationToken);
            if (!response.IsSuccess) return Failure(response.Status, response.ErrorCode, response.CorrelationId);
            ContinuityPresentation.Validate(response.Data!, reconciliationId, response.ETag);
            Data = response.Data;
            if (ContinuityPresentation.CanApprove(Data!)) ApprovalToken = new ContinuityIntentionProtector(protection).Protect(
                new(session.UserId, reconciliationId, Guid.NewGuid(), response.ETag, "", DateTimeOffset.UtcNow.AddHours(8), Data));
        }
        catch (ApiProtocolException) { Response.StatusCode = 503; Error = IndicatorPresentation.Error(503, null, null, true); }
        return Page();
    }
    public async Task<IActionResult> OnPostPrepareAsync(Guid? reconciliationId, CancellationToken cancellationToken)
    {
        var session = await AuthorizeAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!Allowed) return Page();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Keys.Any(k => k is not ("__RequestVerificationToken" or "reason" or "approvalToken")) || form.Any(p => p.Value.Count != 1)) return Failure(400, "SOLICITUD_RECONCILIACION_INVALIDA", null);
        ReasonValue = form["reason"].ToString();
        var reason = ContinuityPresentation.Reason(ReasonValue);
        var baseline = reconciliationId is null ? null : new ContinuityIntentionProtector(protection).Read(form["approvalToken"], session.UserId, reconciliationId, DateTimeOffset.UtcNow);
        if (reconciliationId is not null && (baseline?.Report is null || !ContinuityPresentation.CanApprove(baseline.Report))) return Failure(409, "RECONCILIACION_NO_APROBABLE", null);
        Data = baseline?.Report; ApprovalToken = form["approvalToken"];
        if (reason is null) { ReasonError = true; return Failure(400, "MOTIVO_INVALIDO", null); }
        Intention = new(session.UserId, reconciliationId, baseline?.Key ?? Guid.NewGuid(), baseline?.ETag, reason, DateTimeOffset.UtcNow.AddHours(8), Data);
        Prepared = new ContinuityIntentionProtector(protection).Protect(Intention);
        return Page();
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid? reconciliationId, CancellationToken cancellationToken)
    {
        var session = await AuthorizeAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!Allowed) return Page();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intention")) || form.Any(p => p.Value.Count != 1)) return Failure(400, "CSRF_INVALID", null);
        var intent = new ContinuityIntentionProtector(protection).Read(form["intention"], session.UserId, reconciliationId, DateTimeOffset.UtcNow);
        Data = intent?.Report; ReasonValue = intent?.Reason ?? ""; Cancelled = true;
        if (intent?.Report is not null) ApprovalToken = new ContinuityIntentionProtector(protection).Protect(intent with { Reason = "" });
        return Page();
    }
    public async Task<IActionResult> OnPostSendAsync(Guid? reconciliationId, CancellationToken cancellationToken)
    {
        var session = await AuthorizeAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!Allowed) return Page();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intention" or "recovery")) || form.Any(p => p.Value.Count != 1)) return Failure(400, "CSRF_INVALID", null);
        var intent = new ContinuityIntentionProtector(protection).Read(form["intention"], session.UserId, reconciliationId, DateTimeOffset.UtcNow);
        if (intent is null) { Conflict = true; Error = new("La intención venció y no se pudo confirmar su resultado.", "", null); Response.StatusCode = 400; return Page(); }
        Prepared = form["intention"]; Intention = intent; Data = null;
        try
        {
            var response = await apiClient.SendAsync<RecoveryReconciliationDetails>(new(HttpMethod.Post, intent.Path, ApiResponseShape.Item,
                new { reason = intent.Reason }, intent.ETag, ApiMutationIntent.FromKey(intent.Key), form["__RequestVerificationToken"]), cancellationToken);
            if (!response.IsSuccess)
            {
                Prepared = null; Intention = null; Conflict = response.Status is 409 or 412 or 428;
                return Failure(response.Status, response.ErrorCode, response.CorrelationId);
            }
            ContinuityPresentation.Validate(response.Data!, intent.Resource, response.ETag);
            if (response.Status != (intent.Resource is null ? 201 : 200) || intent.Resource is not null && response.Data!.Status != "APPROVED") throw new ApiProtocolException();
            Data = response.Data; Prepared = null; Intention = null;
            Success = response.Replayed ? "Se recuperó la misma operación; no se registró otra solicitud o aprobación." : intent.Resource is null ?
                "Solicitud de reconciliación registrada. La recuperación aún no está confirmada." : "Aceptación de la reconciliación registrada.";
        }
        catch (ApiProtocolException) { Uncertain = true; Error = new("No se pudo confirmar el resultado de la operación.", "", null); }
        return Page();
    }
    private IActionResult Failure(int status, string? code, string? correlation)
    {
        if (status == 401) { sessionState.Invalidate(); return Redirect("/acceso"); }
        Response.StatusCode = status; Error = IndicatorPresentation.Error(status, code, correlation, true);
        if (status is 403 or 404) { Data = null; Prepared = null; ApprovalToken = null; }
        return Page();
    }
}
