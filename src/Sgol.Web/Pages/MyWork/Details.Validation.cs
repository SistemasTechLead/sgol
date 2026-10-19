using Microsoft.AspNetCore.Mvc;
using Sgol.Validation.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.Validation;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public ValidationHistoryData? Validations { get; private set; }
    public string? ValidationETag { get; private set; }
    public ProblemDetailsPresentation? ValidationError { get; private set; }
    public string? ValidationSuccess { get; private set; }
    public bool ValidationConflict { get; private set; }
    public string? PreparedValidation { get; private set; }
    public ValidationIntention? ValidationIntention { get; private set; }
    public Dictionary<string, string> ValidationValues { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ValidationFields { get; } = new(StringComparer.Ordinal);
    public string ValidationValue(string field) => ValidationValues.GetValueOrDefault(field, "");
    private async Task LoadValidationsAsync(CancellationToken token)
    {
        Validations = null; ValidationETag = null;
        if (Detail is null) return;
        try
        {
            var response = await apiClient.SendAsync<ValidationHistoryData>(new(HttpMethod.Get, $"/api/v1/obligations/{Detail.ObligationId:D}/validations", ApiResponseShape.Item), token);
            if (!response.IsSuccess)
            {
                if (response.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); }
                ValidationError = ValidationPresentation.Error(response.Status, response.ErrorCode, response.CorrelationId); return;
            }
            if (response.Data is null) throw new ApiProtocolException();
            ValidationPresentation.Validate(response.Data, Detail.ObligationId, response.ETag);
            Validations = response.Data; ValidationETag = response.ETag;
        }
        catch (ApiProtocolException) { ValidationError = ValidationPresentation.Error(503, null, null); }
    }
    public async Task<IActionResult> OnPostPrepareValidationAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "mode" or "result" or "foundation" or "reason")) || form.Any(p => p.Value.Count != 1))
        { Response.StatusCode = 400; ValidationError = ValidationPresentation.Error(400, "SOLICITUD_VALIDACION_INVALIDA", null); return Page(); }
        if (Validations is null || ValidationError is not null) return Page();
        var replace = form["mode"] == "replace";
        if (form["mode"] != "issue" && !replace || ValidationETag is null ||
            (replace ? Validations.ValidationActions?.CanReplace != true : Validations.ValidationActions?.IssueAuthority is null))
        { Response.StatusCode = 404; ValidationError = ValidationPresentation.Error(404, null, null); return Page(); }
        foreach (var field in new[] { "result", "foundation", "reason" }) ValidationValues[field] = form[field].ToString();
        var result = form["result"].ToString();
        if (!ValidationResults.IsDefined(result)) ValidationFields["result"] = "Selecciona un resultado";
        string foundation = ""; string? reason = null;
        try { foundation = ValidationText.Foundation(form["foundation"].ToString()); }
        catch (ValidationDecisionException) { ValidationFields["foundation"] = "Ingresa un fundamento válido de 1 a 1000 caracteres, sin datos prohibidos"; }
        var escalated = !replace && Validations.ValidationActions?.IssueAuthority == ValidationAuthorityTypes.Escalation;
        if (replace || escalated)
        {
            try { reason = ValidationText.Reason(form["reason"].ToString()); }
            catch (ValidationDecisionException) { ValidationFields["reason"] = "Ingresa un motivo válido de 1 a 500 caracteres, sin datos prohibidos"; }
        }
        else if (!string.IsNullOrEmpty(form["reason"])) ValidationFields["reason"] = "Revisa este campo";
        if (ValidationFields.Count > 0) { Response.StatusCode = 422; ValidationError = new("No se pudo preparar la validación. Revisa los datos y recarga la tarea si el problema continúa.", "", null); return Page(); }
        ValidationIntention = new((await sessionState.GetAsync(cancellationToken))!.UserId, Detail.ObligationId,
            replace ? Validations.Decisions.Single(d => d.Status == "VIGENTE").DecisionVersionId : null, Guid.NewGuid(), ValidationETag,
            result, foundation, replace ? reason : null, escalated ? reason : null, DateTimeOffset.UtcNow.AddHours(8));
        PreparedValidation = new ValidationIntentionProtector(protection).Protect(ValidationIntention);
        return Page();
    }
    public async Task<IActionResult> OnPostSendValidationAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intention" or "recovery")) || form.Any(p => p.Value.Count != 1))
        { Response.StatusCode = 400; ValidationError = ValidationPresentation.Error(400, "CSRF_INVALIDO", null); return Page(); }
        var intention = new ValidationIntentionProtector(protection).Read(form["intention"].ToString(), session.UserId, Detail.ObligationId, DateTimeOffset.UtcNow);
        if (intention is null)
        { Response.StatusCode = 400; ValidationConflict = true; ValidationError = new("La intención venció y no se pudo confirmar su resultado. Consulta el historial antes de preparar otra decisión.", "", null); return Page(); }
        PreparedValidation = form["intention"].ToString(); ValidationIntention = intention;
        try
        {
            // The API reauthorizes replay against the original intent; current presentation may no longer allow a new decision.
            var response = await apiClient.SendAsync<ValidationMutationData>(new(HttpMethod.Post, intention.Path, ApiResponseShape.Item, intention.Body,
                intention.ETag, ApiMutationIntent.FromKey(intention.Key), form["__RequestVerificationToken"].ToString()), cancellationToken);
            if (!response.IsSuccess)
            {
                if (response.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                Response.StatusCode = response.Status; ValidationError = ValidationPresentation.Error(response.Status, response.ErrorCode, response.CorrelationId);
                if (response.Status is 403 or 404 or 409 or 412 || response.ErrorCode is "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO")
                { PreparedValidation = null; ValidationIntention = null; ValidationConflict = true; }
                if (response.ErrorCode is "FUNDAMENTO_INVALIDO" or "MOTIVO_REQUERIDO" or "RESULTADO_VALIDACION_INVALIDO") { PreparedValidation = null; ValidationIntention = null; }
                return Page();
            }
            var data = response.Data;
            if (response.Status != 201 || data is null || data.Decision is null || data.ValidationRequirement is null || data.ObligationId != intention.Obligation || data.ExecutionStatus != "CONCLUIDA" ||
                data.Decision.Result != intention.Result || data.Decision.Foundation != intention.Foundation || data.Decision.Reason != (intention.Reason ?? intention.EscalationReason) ||
                data.Decision.ValidatorUserId != session.UserId || data.Decision.SupersedesDecisionVersionId != intention.Decision ||
                data.ValidationRequirement.Status != "RESUELTA" || response.ETag != $"\"{data.ValidationRequirement.RowVersion}\"") throw new ApiProtocolException();
            ValidationPresentation.Validate(data.Decision);
            PreparedValidation = null; ValidationIntention = null; ValidationError = null;
            await LoadValidationsAsync(cancellationToken);
            ValidationSuccess = form["recovery"] == "true" ? "Se confirmó el resultado de la misma solicitud. Consulta el historial antes de preparar otra decisión." :
                intention.Decision is null ? "Validación registrada. La ejecución permanece concluida." : "Validación sustituida. La decisión anterior permanece en el historial.";
        }
        catch (ApiProtocolException) { ValidationError = new("No se pudo confirmar el resultado de la solicitud. Recupera la misma intención antes de preparar otra decisión.", "", null); }
        return Page();
    }
}
