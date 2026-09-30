using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    public bool CanViewPlan { get; private set; }
    public bool CanPublishPlan { get; private set; }
    public WorkPlanDetails? WeeklyPlan { get; private set; }
    public string? PlanEtag { get; private set; }
    public IReadOnlyList<ObligationListItem> PlanObligations { get; private set; } = [];
    public IReadOnlyList<PlanVersionDetails> PlanVersions { get; private set; } = [];
    public PlanSnapshotDetails? SelectedPlanPublication { get; private set; }
    public ProblemDetailsPresentation? PlanError { get; private set; }
    public ProblemDetailsPresentation? PlanItemsError { get; private set; }
    public ProblemDetailsPresentation? PlanHistoryError { get; private set; }
    public ProblemDetailsPresentation? PlanOperationError { get; private set; }
    public string? PlanSuccess { get; private set; }
    public WorkPlanEnsureResult? EnsuredPlan { get; private set; }
    public PlanPublicationResult? PublishedPlan { get; private set; }
    public string? EnsurePlanToken { get; private set; }
    public string? PublishPlanToken { get; private set; }
    public bool PlanConflict { get; private set; }
    public bool PlanUncertain { get; private set; }
    public bool PlanUncertainPublication { get; private set; }
    public bool PlanConfirmation { get; private set; }
    public string PlanScope { get; private set; } = "";
    public string? NextPlanItemsHref { get; private set; }
    public string? PreviousPlanItemsHref { get; private set; }
    public string? NextPlanVersionsHref { get; private set; }
    public string? PreviousPlanVersionsHref { get; private set; }
    public string? NextPlanSnapshotHref { get; private set; }
    public string? PreviousPlanSnapshotHref { get; private set; }
    private bool planDenied;
    private sealed record PlanIntention(Guid Actor, Guid Key, int Year, int Week, Guid? PlanId,
        string? Etag, DateTimeOffset ExpiresAt);
    private IDataProtector PlanIntentionProtector => protection.CreateProtector("SGOL.FRONT-015.plan-intention.v1");

    public string PlanHref(string? publicationId = null, string? itemsCursor = null, string? versionsCursor = null,
        string? snapshotCursor = null) => "/planificacion?isoYear=" + Uri.EscapeDataString(IsoYear) +
        "&isoWeek=" + Uri.EscapeDataString(IsoWeek) +
        (publicationId is null ? "" : "&publicationId=" + Uri.EscapeDataString(publicationId)) +
        (itemsCursor is null ? "" : "&planItemsCursor=" + Uri.EscapeDataString(itemsCursor)) +
        (versionsCursor is null ? "" : "&planVersionsCursor=" + Uri.EscapeDataString(versionsCursor)) +
        (snapshotCursor is null ? "" : "&planSnapshotCursor=" + Uri.EscapeDataString(snapshotCursor)) + "#plan-semanal";

    private async Task<bool> LoadPlansAsync(CancellationToken token)
    {
        var session = await sessionState.GetAsync(token);
        if (session is null) return false;
        cursorActor = session.UserId;
        CanViewPlan = !planDenied && session.Permissions.Contains(WeekAuthorization.View);
        CanPublishPlan = !planDenied && session.Permissions.Contains(PlanPublicationAuthorization.Publish);
        PlanScope = session.RoleCode switch
        {
            CanonicalRole.Direction => "Dirección, Administración, Subcoordinación y Piso",
            CanonicalRole.Administration => "Administración, Subcoordinación y Piso",
            CanonicalRole.Subcoordination => "Subcoordinación y Piso", _ => ""
        };
        if (!CanViewPlan) return true;
        if (Week is null)
        { PlanError = WeekError ?? new("Revisa el año, la semana y los datos de consulta", "", null); return true; }
        try
        {
            if (Request.Query["isoYear"].Count > 1 || Request.Query["isoWeek"].Count > 1)
            { PlanError = new("Revisa el año, la semana y los datos de consulta", "", null); return true; }
            var result = await apiClient.SendAsync<WorkPlanDetails>(new(HttpMethod.Get,
                $"/api/v1/plans/{IsoYear}/{IsoWeek}", ApiResponseShape.Item), token);
            if (result.Status == 401) return false;
            if (result.IsSuccess && result.Data is { BranchCode: "LOR-001" } plan && plan.PeriodId == Week.Id &&
                plan.IsoYear == Week.IsoYear && plan.IsoWeek == Week.IsoWeek && ValidEtag(result.ETag ?? ""))
            { WeeklyPlan = plan; PlanEtag = result.ETag; }
            else if (result.ErrorCode != "PLAN_NO_ENCONTRADO")
            {
                PlanError = PlanMessage(result, false);
                if (result.Status == 403)
                {
                    planDenied = true; CanViewPlan = CanPublishPlan = false;
                    PublishedPlan = null; EnsuredPlan = null; PlanSuccess = null;
                }
                return true;
            }
            if (!PlanUncertain && EnsurePlanToken is null)
                EnsurePlanToken = ProtectPlanIntent(session, null, null);
            if (WeeklyPlan is null) return true;
            await LoadPlanItemsAsync(token);
            if (sessionState.IsInvalid) return false;
            await LoadPlanVersionsAsync(token);
            return !sessionState.IsInvalid;
        }
        catch (ApiProtocolException) { PlanError = new("No se pudo cargar la planificación", "Vuelve a consultar más tarde.", null); return true; }
    }

    private async Task LoadPlanItemsAsync(CancellationToken token)
    {
        var filter = WeeklyPlan!.PeriodId.ToString("D");
        if (!PlanCursor("plan-items", filter, "planItemsCursor", out var cursor, out var previous))
        { PlanItemsError = new("La consulta cambió", "Vuelve a consultar desde el inicio.", null); return; }
        var result = await apiClient.SendAsync<ObligationListItem>(new(HttpMethod.Get,
            "/api/v1/obligations?periodId=" + filter + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), ApiResponseShape.Collection), token);
        if (result.Status == 401) { sessionState.Invalidate(); return; }
        if (!result.IsSuccess) { PlanItemsError = PlanMessage(result, false); return; }
        PlanObligations = result.Items ?? [];
        if (result.NextCursor is not null) NextPlanItemsHref = PlanHref(itemsCursor:
            ProtectCursor("plan-items", filter, result.NextCursor, [.. previous, cursor]));
        if (previous.Length > 0) PreviousPlanItemsHref = PlanHref(itemsCursor: PreviousCursor("plan-items", filter, previous));
    }

    private async Task LoadPlanVersionsAsync(CancellationToken token)
    {
        var filter = WeeklyPlan!.PlanId.ToString("D");
        if (!PlanCursor("plan-versions", filter, "planVersionsCursor", out var cursor, out var previous))
        { PlanHistoryError = new("La consulta cambió", "Vuelve a consultar desde el inicio.", null); return; }
        var path = "/api/v1/plans/" + filter + "/versions";
        var result = await apiClient.SendAsync<PlanVersionDetails>(new(HttpMethod.Get,
            path + (cursor is null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), ApiResponseShape.Collection), token);
        if (result.Status == 401) { sessionState.Invalidate(); return; }
        if (!result.IsSuccess) { PlanHistoryError = PlanMessage(result, false); return; }
        PlanVersions = result.Items ?? [];
        if (result.NextCursor is not null) NextPlanVersionsHref = PlanHref(versionsCursor:
            ProtectCursor("plan-versions", filter, result.NextCursor, [.. previous, cursor]));
        if (previous.Length > 0) PreviousPlanVersionsHref = PlanHref(versionsCursor: PreviousCursor("plan-versions", filter, previous));
        var selected = Request.Query["publicationId"];
        if (selected.Count == 0) return;
        if (selected.Count != 1 || !CanonicalId(selected, out var publicationId) ||
            !PlanCursor("plan-snapshot", filter + ":" + selected, "planSnapshotCursor", out cursor, out previous))
        { PlanHistoryError = new("Revisa el año, la semana y los datos de consulta", "", null); return; }
        var snapshot = await apiClient.SendAsync<PlanSnapshotDetails>(new(HttpMethod.Get,
            path + "?publicationId=" + publicationId.ToString("D") +
            (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), ApiResponseShape.Item), token);
        if (snapshot.Status == 401) { sessionState.Invalidate(); return; }
        if (!snapshot.IsSuccess) { PlanHistoryError = PlanMessage(snapshot, false); return; }
        SelectedPlanPublication = snapshot.Data;
        var snapshotFilter = filter + ":" + selected;
        if (snapshot.NextCursor is not null) NextPlanSnapshotHref = PlanHref(selected, snapshotCursor:
            ProtectCursor("plan-snapshot", snapshotFilter, snapshot.NextCursor, [.. previous, cursor]));
        if (previous.Length > 0) PreviousPlanSnapshotHref = PlanHref(selected,
            snapshotCursor: PreviousCursor("plan-snapshot", snapshotFilter, previous));
    }

    private bool PlanCursor(string section, string filter, string key, out string? cursor, out string?[] previous) =>
        ReadCursor(section, filter, Request.Query[key].Count <= 1 ? Request.Query[key].ToString() : "invalid", out cursor, out previous);

    private string ProtectPlanIntent(SessionSnapshot session, Guid? planId, string? etag)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        if (expires > session.AbsoluteExpiresAt) expires = session.AbsoluteExpiresAt;
        return PlanIntentionProtector.Protect(JsonSerializer.Serialize(new PlanIntention(session.UserId,
            Guid.CreateVersion7(), Week!.IsoYear, Week.IsoWeek, planId, etag, expires)));
    }

    private async Task<IFormCollection?> PlanFormAsync(CancellationToken token)
    {
        NoStore();
        if (!Request.HasFormContentType) return null;
        try { await formAntiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return null; }
        return await Request.ReadFormAsync(token);
    }

    public async Task<IActionResult> OnPostPreparePlanPublicationAsync(CancellationToken cancellationToken)
    {
        var form = await PlanFormAsync(cancellationToken);
        if (form is null) return BadRequest();
        SetFilters(form["isoYear"], form["isoWeek"], null, null, null, null);
        var page = await LoadAsync(cancellationToken);
        if (page is RedirectResult) return page;
        if (!CanPublishPlan) return StatusCode(403);
        if (WeeklyPlan is null || !CanonicalId(form["planId"], out var id) || id != WeeklyPlan.PlanId ||
            form["etag"].Count != 1 || form["etag"].ToString() != PlanEtag)
        { PlanConflict = true; PlanOperationError = new("Este registro cambió mientras lo editabas", "Recarga el plan antes de publicar.", null); Response.StatusCode = 412; return Page(); }
        var session = await sessionState.GetAsync(cancellationToken);
        PublishPlanToken = ProtectPlanIntent(session!, id, PlanEtag);
        PlanConfirmation = true;
        return Page();
    }

    public Task<IActionResult> OnPostEnsurePlanAsync(CancellationToken cancellationToken) => SubmitPlanAsync(false, cancellationToken);
    public Task<IActionResult> OnPostPublishPlanAsync(CancellationToken cancellationToken) => SubmitPlanAsync(true, cancellationToken);

    private async Task<IActionResult> SubmitPlanAsync(bool publish, CancellationToken token)
    {
        var form = await PlanFormAsync(token);
        if (form is null) return BadRequest();
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(publish ? PlanPublicationAuthorization.Publish : WeekAuthorization.View)) return StatusCode(403);
        PlanIntention? intent;
        try
        {
            if (form["planIntent"].Count != 1 || form["planIntent"].ToString().Length > 8192) return BadRequest();
            intent = JsonSerializer.Deserialize<PlanIntention>(PlanIntentionProtector.Unprotect(form["planIntent"].ToString()));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException) { return BadRequest(); }
        if (intent is null || intent.Actor != session.UserId || intent.Key == Guid.Empty || intent.ExpiresAt <= DateTimeOffset.UtcNow ||
            (publish ? intent.PlanId is null || intent.PlanId == Guid.Empty || !ValidEtag(intent.Etag ?? "") : intent.PlanId is not null || intent.Etag is not null)) return BadRequest();
        SetFilters(intent.Year.ToString(CultureInfo.InvariantCulture), intent.Week.ToString(CultureInfo.InvariantCulture), null, null, null, null);
        if (publish) PublishPlanToken = form["planIntent"]; else EnsurePlanToken = form["planIntent"];
        try
        {
            if (publish)
            {
                var result = await antiforgery.SendValidatedAsync<PlanPublicationResult>(HttpContext,
                    new(HttpMethod.Post, $"/api/v1/plans/{intent.PlanId:D}/publications", ApiResponseShape.Item,
                        new object(), intent.Etag, ApiMutationIntent.FromKey(intent.Key)), token);
                if (result is null) return BadRequest();
                if (result.Status == 401) return Redirect("/acceso");
                if (result.IsSuccess && result.Data is { } publication)
                {
                    if (publication.PlanId != intent.PlanId || publication.PublicationId == Guid.Empty ||
                        publication.Result is not ("PUBLICADA_INICIAL" or "PUBLICADA_INCREMENTAL" or "RECUPERADA"))
                        throw new ApiProtocolException();
                    PublishedPlan = publication;
                    PlanSuccess = publication.Result switch
                    {
                        "PUBLICADA_INICIAL" => "Primera publicación de tu alcance confirmada",
                        "PUBLICADA_INCREMENTAL" => "Nueva versión de tu alcance publicada",
                        "RECUPERADA" => "Se recuperó la publicación ya confirmada", _ => null
                    };
                }
                else OperationFailure(result, true);
            }
            else
            {
                // The approved week GET materializes its own period; plan GET never does.
                var period = await apiClient.SendAsync<WeekPeriodDetails>(new(HttpMethod.Get,
                    $"/api/v1/weeks/{intent.Year}/{intent.Week}", ApiResponseShape.Item), token);
                if (period.Status == 401) return Redirect("/acceso");
                if (!period.IsSuccess) { OperationFailure(period, false); return await LoadAsync(token); }
                var result = await antiforgery.SendValidatedAsync<WorkPlanEnsureResult>(HttpContext,
                    new(HttpMethod.Post, $"/api/v1/plans/{intent.Year}/{intent.Week}/ensure", ApiResponseShape.Item,
                        new object(), Intent: ApiMutationIntent.FromKey(intent.Key)), token);
                if (result is null) return BadRequest();
                if (result.Status == 401) return Redirect("/acceso");
                if (result.IsSuccess && result.Data is { } ensured)
                {
                    if (ensured.PlanId == Guid.Empty || ensured.BranchCode != "LOR-001" ||
                        ensured.IsoYear != intent.Year || ensured.IsoWeek != intent.Week ||
                        ensured.Result is not ("CREADA" or "RECUPERADA")) throw new ApiProtocolException();
                    EnsuredPlan = ensured; PlanSuccess = ensured.Result == "RECUPERADA" ? "Se recuperó el plan semanal existente" : "Plan semanal creado";
                }
                else OperationFailure(result, false);
            }
        }
        catch (ApiProtocolException)
        {
            PlanUncertain = true; PlanUncertainPublication = publish;
            PlanOperationError = new("No se pudo confirmar el resultado", "Puedes reenviar la misma solicitud.", null);
            Response.StatusCode = 503;
        }
        return await LoadAsync(token);
    }

    private void OperationFailure<T>(ApiResponse<T> response, bool publish)
    {
        PlanOperationError = PlanMessage(response, publish); Response.StatusCode = response.Status;
        PlanConflict = response.Status == 412 || response.ErrorCode is "IF_MATCH_REQUERIDO" or "IF_MATCH_INVALIDO";
        if (response.Status == 403) planDenied = true;
    }

    private static ProblemDetailsPresentation PlanMessage<T>(ApiResponse<T> response, bool publish)
    {
        var message = (response.Status, response.ErrorCode) switch
        {
            (403, _) => publish ? "No tienes permiso para publicar este plan" : "No tienes permiso para consultar este plan",
            (404, "PLAN_NO_ENCONTRADO") => "El plan no existe o no está disponible",
            (404, "PUBLICACION_NO_ENCONTRADA") => "La publicación no existe o no está disponible en tu alcance",
            (404, "PERIODO_NO_ENCONTRADO") => "Consulta primero el período semanal",
            (400, "SOLICITUD_PLAN_INVALIDA" or "CONSULTA_PLAN_INVALIDA") or (422, "SEMANA_ISO_INVALIDA") => "Revisa el año, la semana y los datos de consulta",
            (400, "CURSOR_INVALIDO") => "La consulta cambió. Vuelve a consultar desde el inicio",
            (400, "SOLICITUD_PUBLICACION_INVALIDA") => "No se pudo verificar la publicación. Recarga el plan antes de preparar otra solicitud",
            (400, "IDEMPOTENCY_KEY_INVALIDA") => "No se pudo verificar la solicitud. Recarga antes de continuar",
            (422, "SIN_OBLIGACIONES_PUBLICABLES") => "No hay obligaciones publicables en tu alcance",
            (422, "SIN_NOVEDADES_PUBLICABLES") => "No hay nuevas obligaciones publicables en tu alcance",
            (422, "OBLIGACION_SIN_ASIGNACION") => "Hay obligaciones de tu alcance que necesitan asignación antes de publicar",
            (422, "ASIGNACION_NO_PUBLICABLE") => "Una asignación de tu alcance dejó de ser publicable. Revisa las asignaciones antes de preparar otra publicación",
            (409, "PERIODO_INCOMPATIBLE" or "CONTENIDO_PLAN_INCOMPATIBLE" or "ESTADO_PLAN_INCOMPATIBLE") => "Los datos del plan no permiten continuar. Vuelve a consultarlo",
            (409, "IDEMPOTENCY_CONFLICT") => "Esta solicitud ya se usó con otros datos. Consulta el plan antes de preparar otra intención",
            (409, "WORK_PLAN_CONCURRENCY_CONFLICT" or "WORK_PLAN_CONFLICT" or "PLAN_PUBLICATION_CONCURRENCY_CONFLICT" or "PLAN_PUBLICATION_CONFLICT") => "No se pudo confirmar la operación. Consulta el plan antes de decidir otro intento",
            _ => null
        };
        return message is null ? response.Error ?? new("No se pudo cargar la planificación", "Vuelve a consultar más tarde.", response.CorrelationId)
            : new(message, "", response.CorrelationId);
    }
}
