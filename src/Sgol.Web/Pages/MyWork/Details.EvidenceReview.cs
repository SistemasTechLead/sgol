using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Evidence.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class DetailsModel
{
    public IReadOnlyList<EvidenceDetails> Versions { get; private set; } = [];
    public CursorPaginationViewModel? EvidencePagination { get; private set; }
    public ProblemDetailsPresentation? VersionsError { get; private set; }
    public ProblemDetailsPresentation? ReviewError { get; private set; }
    public EvidenceReviewDetails? Review { get; private set; }
    public string? ReviewToken { get; private set; }
    public string? ObligationETag { get; private set; }
    public string? EvidenceRequirement => Request.Query["evidenceRequirement"].ToString() is { Length: > 0 } value ? value : null;
    public string? EvidenceStatus => Request.Query["evidenceStatus"].ToString() is { Length: > 0 } value ? value : null;
    private async Task LoadVersionsAsync(CancellationToken token)
    {
        if (Detail is null) return;
        if (Request.Query["evidenceRequirement"].Count > 1 || Request.Query["evidenceStatus"].Count > 1 || Request.Query["evidenceCursor"].Count > 1 ||
            EvidenceRequirement is not null && Detail.EvidencePolicy?.Requirements.Any(r => r.RequirementCode == EvidenceRequirement) != true ||
            EvidenceStatus is not (null or "VIGENTE" or "SUSTITUIDA"))
        { VersionsError = EvidenceReviewPresentation.Error(400, "FILTRO_EVIDENCIA_INVALIDO", null); Response.StatusCode = 400; return; }
        var session = (await sessionState.GetAsync(token))!;
        var cursor = new MyWorkCursor(protection, session.UserId, "evidence", $"{Detail.ObligationId:D}:{EvidenceRequirement}:{EvidenceStatus}");
        if (!cursor.Read(Request.Query.ContainsKey("evidenceCursor") ? Request.Query["evidenceCursor"].ToString() : null))
        { VersionsError = EvidenceReviewPresentation.Error(400, "FILTRO_EVIDENCIA_INVALIDO", null); Response.StatusCode = 400; return; }
        try
        {
            var path = QueryHelpers.AddQueryString($"/api/v1/obligations/{Detail.ObligationId:D}/evidence", new Dictionary<string, string?>
            { ["requirementCode"] = EvidenceRequirement, ["status"] = EvidenceStatus, ["cursor"] = cursor.Cursor });
            var result = await apiClient.SendAsync<EvidenceDetails>(new(HttpMethod.Get, path, ApiResponseShape.Collection), token);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); }
                VersionsError = EvidenceReviewPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId); return;
            }
            var rows = result.Items ?? throw new ApiProtocolException();
            if (rows.Count > 100 || rows.Select(r => r.Version?.EvidenceVersionId).Distinct().Count() != rows.Count) throw new ApiProtocolException();
            foreach (var item in rows)
            {
                EvidenceReviewPresentation.Validate(item, Detail);
                if (EvidenceRequirement is not null && item.Requirement.RequirementCode != EvidenceRequirement ||
                    EvidenceStatus is not null && item.Version.Status != EvidenceStatus) throw new ApiProtocolException();
            }
            Versions = rows;
            EvidencePagination = cursor.Links(result.NextCursor, value => QueryHelpers.AddQueryString(ReloadHref, new Dictionary<string, string?>
            { ["evidenceRequirement"] = EvidenceRequirement, ["evidenceStatus"] = EvidenceStatus, ["evidenceCursor"] = value,
                ["historyCursor"] = Request.Query["historyCursor"].Count == 1 ? Request.Query["historyCursor"].ToString() : null }) + "#versiones-evidencia");
        }
        catch (ApiProtocolException) { VersionsError = EvidenceReviewPresentation.Error(503, null, null); }
    }
    public async Task<IActionResult> OnPostReviewEvidenceAsync(string obligationId, CancellationToken cancellationToken)
    {
        var form = await EvidenceFormAsync(cancellationToken);
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (form is null || form.Keys.Any(k => k != "__RequestVerificationToken")) return EvidenceFailure(400, "CSRF_INVALIDO");
        var page = await OnGetAsync(obligationId, cancellationToken);
        if (Detail is null || Error is not null || page is RedirectResult) return page;
        try
        {
            var result = await apiClient.SendAsync<EvidenceReviewDetails>(new(HttpMethod.Get,
                $"/api/v1/obligations/{Detail.ObligationId:D}/evidence-review", ApiResponseShape.Item), cancellationToken);
            if (!result.IsSuccess)
            {
                if (result.Status == 401) { sessionState.Invalidate(); ApiCookieBridge.Clear(HttpContext); return Redirect("/acceso"); }
                ReviewError = EvidenceReviewPresentation.Error(result.Status, result.ErrorCode, result.CorrelationId);
                Response.StatusCode = result.Status; return Page();
            }
            Review = result.Data ?? throw new ApiProtocolException();
            EvidenceReviewPresentation.Validate(Review, Detail);
            var session = (await sessionState.GetAsync(cancellationToken))!;
            ReviewToken = new ConclusionIntentionProtector(protection).Protect(new ProtectedEvidenceReview(session.UserId,
                session.IdleExpiresAt < session.AbsoluteExpiresAt ? session.IdleExpiresAt : session.AbsoluteExpiresAt, Review));
        }
        catch (ApiProtocolException) { Review = null; ReviewError = EvidenceReviewPresentation.Error(503, null, null); Response.StatusCode = 503; }
        return Page();
    }
}
