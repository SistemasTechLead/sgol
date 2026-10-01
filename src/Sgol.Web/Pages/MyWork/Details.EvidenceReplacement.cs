using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Evidence.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public EvidenceReplacementIntention? Replacement { get; private set; }
    public string? ReplacementToken { get; private set; }
    public string ReplacementReason { get; private set; } = "";
    public bool ReplacementReasonInvalid { get; private set; }
    public string? PreparedReplacement { get; private set; }
    public ProblemDetailsPresentation? ReplacementError { get; private set; }
    public string? ReplacementSuccess { get; private set; }
    public bool ReplacementConflict { get; private set; }
    public bool CanReplace => Detail?.EvidenceActions?.CanReplace == true;
    public bool ReplacementNeedsReason => Detail?.ExecutionStatus == "CONCLUIDA";
    private EvidenceReplacementIntentionProtector Replacements => new(protection);

    public async Task<IActionResult> OnPostSelectReplacementAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "itemId" or "requirementCode"))) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        if (!CanReplace || !Guid.TryParseExact(form["itemId"], "D", out var itemId))
        { ReplacementError = EvidenceReviewPresentation.Error(404, null, null); Response.StatusCode = 404; return Page(); }
        var requirement = form["requirementCode"].ToString();
        if (Detail.EvidencePolicy?.Requirements.Any(r => r.RequirementCode == requirement) != true) return EvidenceFailure(400, "REQUISITO_EVIDENCIA_INVALIDO");
        try
        {
            var result = await apiClient.SendAsync<EvidenceDetails>(new(HttpMethod.Get, QueryHelpers.AddQueryString(
                $"/api/v1/obligations/{Detail.ObligationId:D}/evidence", new Dictionary<string, string?> { ["requirementCode"] = requirement, ["status"] = "VIGENTE" }), ApiResponseShape.Collection), cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                ReplacementError = EvidenceReviewPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId); Response.StatusCode = result.Status; return Page();
            }
            if (result.Items?.Count != 1 || result.NextCursor is not null || result.Items[0].EvidenceItemId != itemId) throw new ApiProtocolException();
            var item = result.Items[0]; EvidenceReviewPresentation.Validate(item, Detail);
            if (item.Version.Status != "VIGENTE") throw new ApiProtocolException();
            Replacement = new(session.UserId, Detail.ObligationId, itemId, requirement, item.ItemRowVersion, item.Version.VersionNo,
                session.IdleExpiresAt < session.AbsoluteExpiresAt ? session.IdleExpiresAt : session.AbsoluteExpiresAt);
            ReplacementToken = Replacements.Protect(Replacement);
            SelectedRequirement = Detail.EvidencePolicy.Requirements.Single(r => r.RequirementCode == requirement);
        }
        catch (ApiProtocolException) { ReplacementError = new("No hay una versión vigente disponible para sustituir", "", null); Response.StatusCode = 409; }
        return Page();
    }
    private bool RestoreReplacement(IFormCollection form, Guid actor, Guid obligation)
    {
        Replacement = Replacements.Read(form["replacement"].ToString(), actor, obligation, DateTimeOffset.UtcNow);
        if (Replacement is null || !CanReplace) return false;
        SelectedRequirement = Detail?.EvidencePolicy?.Requirements.SingleOrDefault(r => r.RequirementCode == Replacement.Requirement);
        ReplacementToken = form["replacement"].ToString();
        return SelectedRequirement is not null;
    }
    public async Task<IActionResult> OnPostPrepareReplacementAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        if (!RestoreReplacement(form, session.UserId, Detail.ObligationId) || IsBinary)
        { Replacement = null; Response.StatusCode = 404; ReplacementError = EvidenceReviewPresentation.Error(404, null, null); ReplacementConflict = true; return Page(); }
        ReplacementReason = form["reason"].ToString();
        string? reason;
        try { reason = NormalizeReplacementReason(ReplacementReason, ReplacementNeedsReason); }
        catch (EvidenceRequestInvalidException)
        { ReplacementReasonInvalid = true; ReplacementError = EvidenceReviewPresentation.Error(422, "MOTIVO_REQUERIDO", null); Response.StatusCode = 422; return Page(); }
        foreach (var f in EvidenceContributionPresentation.Inputs(SelectedRequirement!.RequirementCode)) EvidenceValues[f.Name] = form[f.Name].ToString();
        try
        {
            var fields = EvidenceContributionPresentation.Inputs(SelectedRequirement.RequirementCode);
            if (form["requirementCode"].ToString() != Replacement!.Requirement) throw new EvidenceRequestInvalidException();
            if (form.Keys.Any(k => k is not ("__RequestVerificationToken" or "replacement" or "requirementCode" or "reason") && !fields.Any(f => f.Name == k))) throw new EvidenceRequestInvalidException();
            using var payload = EvidenceContributionPresentation.Payload(Detail.Task.TaskCode, SelectedRequirement.RequirementCode, SelectedRequirement.Kind, EvidenceValues, InvalidEvidenceFields);
            var body = JsonSerializer.SerializeToElement(new { structuredPayload = payload.RootElement, reason });
            PreparedReplacement = Intentions.Protect(NewIntention(session, Detail.ObligationId, Replacement!.Requirement, "replacement", body) with
            { ReplacementItem = Replacement.Item, ReplacementRowVersion = Replacement.RowVersion, ReplacementVersionNo = Replacement.VersionNo, Reason = reason });
        }
        catch (EvidenceRequestInvalidException)
        { EvidenceError = new("Revisa los datos del requisito seleccionado.", "", null); Response.StatusCode = 422; }
        return Page();
    }
    public async Task<IActionResult> OnPostSendReplacementAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intention")) || !MyWorkQuery.CanonicalId(obligationId, out var id)) return EvidenceFailure(400, "CSRF_INVALIDO");
        var intention = Intentions.Read(form["intention"].ToString(), session.UserId, id, "replacement", DateTimeOffset.UtcNow);
        if (!ValidReplacement(intention))
        {
            var invalidPage = await OnGetAsync(obligationId, cancellationToken);
            if (invalidPage is RedirectResult || Detail is null || Error is not null) return invalidPage;
            Response.StatusCode = 400; ReplacementError = EvidenceReviewPresentation.Error(400, "SOLICITUD_EVIDENCIA_INVALIDA", null);
            ReplacementConflict = true; return Page();
        }
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        PreparedReplacement = form["intention"].ToString();
        Replacement = new(session.UserId, id, intention!.ReplacementItem!.Value, intention.Requirement,
            intention.ReplacementRowVersion!.Value, intention.ReplacementVersionNo!.Value, intention.ExpiresAt);
        ReplacementReason = intention.Reason ?? "";
        try
        {
            var result = await apiClient.SendAsync<JsonElement>(new(HttpMethod.Post, ReplacementPath(id, intention!), ApiResponseShape.Item,
                intention!.Body, IfMatch: ReplacementETag(intention), Intent: ApiMutationIntent.FromKey(intention.Key),
                CsrfToken: form["__RequestVerificationToken"].ToString()), cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                ReplacementError = EvidenceReviewPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId); Response.StatusCode = result.Status;
                if (result.Status is 403 or 404 or 412 || result.ErrorCode is "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO")
                { PreparedReplacement = null; Replacement = null; ReplacementConflict = true; }
                return Page();
            }
            ConfirmReplacement(result.Data, intention);
            PreparedReplacement = null; Replacement = null;
            await OnGetAsync(obligationId, cancellationToken);
            if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
            ReplacementSuccess = "Sustitución confirmada. La versión anterior permanece en historia";
        }
        catch (Exception e) when (e is ApiProtocolException or KeyNotFoundException or InvalidOperationException or FormatException)
        { ReplacementError = new("No se pudo confirmar el resultado. Puedes recuperar la solicitud original sin crear otra", "", null); }
        return Page();
    }
    private static bool ValidReplacement(EvidenceIntention? intention) => intention is { ReplacementItem: not null, ReplacementRowVersion: > 0, ReplacementVersionNo: > 0 } && intention.ReplacementItem != Guid.Empty;
    private static string ReplacementPath(Guid id, EvidenceIntention intention) => $"/api/v1/obligations/{id:D}/evidence/{intention.ReplacementItem:D}/replacements";
    private static string ReplacementETag(EvidenceIntention intention) => $"\"{intention.ReplacementRowVersion!.Value.ToString(CultureInfo.InvariantCulture)}\"";
    private static void ConfirmReplacement(JsonElement data, EvidenceIntention intention)
    {
        if (data.GetProperty("evidenceItemId").GetGuid() != intention.ReplacementItem || data.GetProperty("itemRowVersion").GetInt64() != intention.ReplacementRowVersion + 1 ||
            data.GetProperty("requirement").GetProperty("requirementCode").GetString() != intention.Requirement ||
            data.GetProperty("version").GetProperty("versionNo").GetInt32() != intention.ReplacementVersionNo + 1 || data.GetProperty("version").GetProperty("status").GetString() != "VIGENTE") throw new ApiProtocolException();
    }
    private static string? NormalizeReplacementReason(string value, bool required)
    {
        var text = value.Normalize(System.Text.NormalizationForm.FormC).Trim();
        if (text.Length == 0 && !required) return null;
        if (text.Length is < 1 or > 500 || text.Any(c => char.IsControl(c) || c is '<' or '>')) throw new EvidenceRequestInvalidException();
        return text;
    }
}
