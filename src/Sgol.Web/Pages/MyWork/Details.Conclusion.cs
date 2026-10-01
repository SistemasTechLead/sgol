using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public string? PreparedConclusion { get; private set; }
    public ProblemDetailsPresentation? ConclusionError { get; private set; }
    public string? ConclusionSuccess { get; private set; }
    public IReadOnlyList<string> ConclusionMissing { get; private set; } = [];
    public bool ConclusionConflict { get; private set; }
    public async Task<IActionResult> OnPostPrepareConclusionAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "review"))) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        var protectedReview = new ConclusionIntentionProtector(protection).ReadReview(form["review"].ToString(), session.UserId, Detail.ObligationId, DateTimeOffset.UtcNow);
        if (!session.Permissions.Contains(ObligationConclusionAuthorization.Execute) || Detail.EvidenceActions?.CanConclude != true ||
            protectedReview?.Review.Result != "COMPLETA" || ObligationETag is null)
        { Response.StatusCode = 422; ConclusionError = new("Consulta una revisión completa de la evidencia antes de concluir", "", null); return Page(); }
        try { EvidenceReviewPresentation.Validate(protectedReview.Review, Detail); }
        catch (ApiProtocolException) { Response.StatusCode = 400; ConclusionError = EvidenceReviewPresentation.Error(400, null, null); return Page(); }
        Review = protectedReview.Review; ReviewToken = form["review"].ToString();
        PreparedConclusion = new ConclusionIntentionProtector(protection).Protect(new ConclusionIntention(session.UserId, Detail.ObligationId,
            Guid.NewGuid(), ObligationETag, protectedReview.ExpiresAt));
        return Page();
    }
    public async Task<IActionResult> OnPostConcludeAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (form is null || form.Keys.Any(k => k is not ("__RequestVerificationToken" or "intention")) || !MyWorkQuery.CanonicalId(obligationId, out var id)) return EvidenceFailure(400, "CSRF_INVALIDO");
        var intention = new ConclusionIntentionProtector(protection).Read(form["intention"].ToString(), session.UserId, id, DateTimeOffset.UtcNow);
        if (intention is null)
        {
            var invalidPage = await OnGetAsync(obligationId, cancellationToken);
            if (invalidPage is RedirectResult || Detail is null || Error is not null) return invalidPage;
            Response.StatusCode = 400; ConclusionError = EvidenceReviewPresentation.Error(400, "SOLICITUD_CONCLUSION_INVALIDA", null);
            ConclusionConflict = true; return Page();
        }
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (page is RedirectResult || Detail is null || Error is not null) return page;
        // Recovery reauthorizes ownership but does not require PENDIENTE; the API checks the original intent before current state/version.
        if (!session.Permissions.Contains(ObligationConclusionAuthorization.Execute) || Detail.CurrentAssignment?.Responsible.PersonId != session.PersonId)
        { Response.StatusCode = 404; ConclusionError = EvidenceReviewPresentation.Error(404, null, null); return Page(); }
        PreparedConclusion = form["intention"].ToString();
        try
        {
            var result = await apiClient.SendAsync<JsonElement>(new(HttpMethod.Post, $"/api/v1/obligations/{id:D}/conclusion",
                ApiResponseShape.Item, Body: null, IfMatch: intention.ETag, Intent: ApiMutationIntent.FromKey(intention.Key),
                CsrfToken: form["__RequestVerificationToken"].ToString()), cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                ConclusionError = EvidenceReviewPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId); Response.StatusCode = result.Status;
                if (result.Status is 403 or 404 or 412 || result.ErrorCode is "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO") { PreparedConclusion = null; ConclusionConflict = true; }
                if (result.ErrorCode == "EVIDENCIA_FALTANTE")
                {
                    var allowed = Detail.EvidencePolicy?.Requirements.Select(r => r.RequirementCode).ToHashSet(StringComparer.Ordinal) ?? [];
                    ConclusionMissing = (result.FieldErrors ?? []).Where(e => e.Path == "evidence" && e.Code == "MISSING" && e.Reference is not null && allowed.Contains(e.Reference))
                        .Select(e => e.Reference!).Distinct().ToArray();
                    PreparedConclusion = null;
                }
                return Page();
            }
            if (result.Data.GetProperty("obligationId").GetGuid() != id || result.Data.GetProperty("executionStatus").GetString() != "CONCLUIDA" ||
                result.Data.GetProperty("concludedBy").GetGuid() != session.UserId || result.ETag is null) throw new ApiProtocolException();
            PreparedConclusion = null;
            await OnGetAsync(obligationId, cancellationToken);
            if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
            ConclusionSuccess = "Conclusión confirmada. La tarea está concluida; esto no significa que esté validada";
        }
        catch (Exception e) when (e is ApiProtocolException or KeyNotFoundException or InvalidOperationException or FormatException)
        { ConclusionError = new("No se pudo confirmar el resultado. Puedes recuperar la solicitud original sin crear otra", "", null); }
        return Page();
    }
}
