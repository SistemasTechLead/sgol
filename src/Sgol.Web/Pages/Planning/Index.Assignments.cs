using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Assignment.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    private bool correctionDenied;
    public string? AssignmentQuery { get; private set; }
    public string? LoadPersonQuery { get; private set; }
    public string? LoadCursor { get; private set; }
    public string? AssignmentHistoryCursor { get; private set; }
    private string? currentLoadCursor;
    private string? currentHistoryCursor;
    private string?[] previousLoadCursors = [];
    private string?[] previousHistoryCursors = [];
    private Guid cursorActor;
    public string? PreviousLoadHref => previousLoadCursors.Length == 0 ? null :
        LoadsHref(PreviousCursor("loads", LoadPersonQuery ?? "", previousLoadCursors));
    public string? PreviousHistoryHref => previousHistoryCursors.Length == 0 ? null :
        AssignmentHref(PreviousCursor("history", AssignmentQuery ?? "", previousHistoryCursors));
    public bool CanViewAssignments { get; private set; }
    public bool CanExplainAssignment { get; private set; }
    public bool CanViewLoads { get; private set; }
    public bool CanCorrectAssignment { get; private set; }
    public ObligationDetail? AssignmentObligation { get; private set; }
    public EligibilityEvaluationDetails? AssignmentEligibility { get; private set; }
    public IReadOnlyList<ActiveLoadItem> ActiveLoads { get; private set; } = [];
    public string? NextLoadCursor { get; private set; }
    public string? NextAssignmentHistoryCursor { get; private set; }
    public string? AssignmentEtag { get; private set; }
    public ProblemDetailsPresentation? AssignmentError { get; private set; }
    public ProblemDetailsPresentation? EligibilityError { get; private set; }
    public ProblemDetailsPresentation? LoadError { get; private set; }
    public ProblemDetailsPresentation? CorrectionError { get; private set; }
    public string? CorrectionFieldError { get; private set; }
    public string? CorrectionIntentToken { get; private set; }
    public AssignmentCorrectionResult? CorrectionResult { get; private set; }
    public bool CorrectionConflict { get; private set; }
    public bool CorrectionUncertain { get; private set; }
    public bool OpenCorrectionConfirmation { get; private set; }
    public string CorrectionReason { get; private set; } = "";
    public Guid? SelectedResponsible { get; private set; }
    public CorrectionBody? PreparedCorrection { get; private set; }
    public IEnumerable<EligibilityCandidateExplanation> CorrectionCandidates =>
        AssignmentEligibility?.Candidates.Where(c => c.IsEligible &&
            c.PersonId != AssignmentObligation?.CurrentAssignment?.Responsible.PersonId) ?? [];
    public bool CanPrepareCorrection => CanCorrectAssignment && AssignmentError is null && EligibilityError is null &&
        AssignmentObligation is { ExecutionStatus: "PENDIENTE", CurrentAssignment: not null } &&
        AssignmentEligibility is not null && AssignmentEtag is not null && CorrectionCandidates.Any() &&
        !CorrectionConflict && PreparedCorrection is null;

    public sealed record CorrectionBody(Guid NewResponsiblePersonId, Guid EligibilityEvaluationId, string Reason);
    private sealed record CorrectionIntention(Guid Actor, Guid ObligationId, Guid Key,
        DateTimeOffset ExpiresAt, string Etag, CorrectionBody Body);
    private IDataProtector CorrectionProtector => protection.CreateProtector("SGOL.FRONT-014.correction-intention.v1");

    private async Task<bool> LoadAssignmentsAsync(CancellationToken token)
    {
        var session = await sessionState.GetAsync(token);
        if (session is null) return false;
        cursorActor = session.UserId;
        CanViewAssignments = session.Permissions.Contains(ObligationQueryAuthorization.View);
        CanExplainAssignment = session.Permissions.Contains(EligibilityAuthorization.Explain);
        CanViewLoads = session.Permissions.Contains(ActiveLoadAuthorization.View);
        CanCorrectAssignment = !correctionDenied && session.Permissions.Contains(AssignmentCorrectionAuthorization.Correct);
        if (CanViewLoads)
        {
            try
            {
                if (Request.Query["loadPersonId"].Count > 1 || Request.Query["loadCursor"].Count > 1 ||
                    !string.IsNullOrEmpty(LoadPersonQuery) && !CanonicalId(LoadPersonQuery, out _) ||
                    !ReadCursor("loads", LoadPersonQuery ?? "", LoadCursor, out currentLoadCursor, out previousLoadCursors))
                {
                    LoadError = new("Revisa el filtro de persona", "", null);
                    Response.StatusCode = 400;
                }
                else
                {
                    var query = new List<string>();
                    if (!string.IsNullOrEmpty(LoadPersonQuery)) query.Add("personId=" + Uri.EscapeDataString(LoadPersonQuery));
                    if (currentLoadCursor is not null) query.Add("cursor=" + Uri.EscapeDataString(currentLoadCursor));
                    var loads = await apiClient.SendAsync<ActiveLoadItem>(new(HttpMethod.Get,
                        "/api/v1/loads" + (query.Count == 0 ? "" : "?" + string.Join('&', query)), ApiResponseShape.Collection), token);
                    if (loads.Status == 401) return false;
                    if (loads.IsSuccess) { ActiveLoads = loads.Items ?? []; NextLoadCursor = loads.NextCursor; }
                    else
                    {
                        LoadError = loads.Status == 403 ? new("No tienes permiso para consultar carga activa", "", loads.CorrelationId)
                            : loads.ErrorCode == "FILTRO_CARGA_INVALIDO" ? new("Revisa el filtro de persona", "", loads.CorrelationId) : loads.Error;
                        Response.StatusCode = loads.Status;
                        if (loads.Status == 403) CanViewLoads = false;
                    }
                }
            }
            catch (ApiProtocolException) { LoadError = ReadFailure(); Response.StatusCode = 503; }
        }
        if (string.IsNullOrEmpty(AssignmentQuery)) return true;
        if (!CanViewAssignments || Request.Query["obligationId"].Count > 1 ||
            Request.Query["assignmentHistoryCursor"].Count > 1 || !CanonicalId(AssignmentQuery, out var id) ||
            !ReadCursor("history", AssignmentQuery, AssignmentHistoryCursor, out currentHistoryCursor, out previousHistoryCursors))
        {
            AssignmentError = HiddenAssignment(); Response.StatusCode = 404; return true;
        }
        try
        {
            var suffix = currentHistoryCursor is null ? "" : "?historyCursor=" + Uri.EscapeDataString(currentHistoryCursor);
            var obligation = await apiClient.SendAsync<ObligationDetail>(new(HttpMethod.Get,
                $"/api/v1/obligations/{id:D}" + suffix, ApiResponseShape.Item), token);
            if (obligation.Status == 401) return false;
            if (!obligation.IsSuccess)
            {
                AssignmentError = obligation.Status is 403 or 404 ? HiddenAssignment() : obligation.Error;
                Response.StatusCode = obligation.Status;
                CanCorrectAssignment = false;
                return true;
            }
            if (obligation.Data is null || obligation.Data.ObligationId != id || obligation.ETag is null || !ValidEtag(obligation.ETag)) throw new ApiProtocolException();
            AssignmentObligation = obligation.Data;
            AssignmentEtag = obligation.ETag is { } etag && ValidEtag(etag) ? etag : null;
            NextAssignmentHistoryCursor = obligation.HistoryNextCursor;
        }
        catch (ApiProtocolException) { AssignmentError = ReadFailure(); Response.StatusCode = 503; return true; }
        if (!CanExplainAssignment) return true;
        try
        {
            var eligibility = await apiClient.SendAsync<EligibilityEvaluationDetails>(new(HttpMethod.Get,
                $"/api/v1/obligations/{id:D}/eligibility", ApiResponseShape.Item), token);
            if (eligibility.Status == 401) return false;
            if (!eligibility.IsSuccess)
            {
                EligibilityError = eligibility.Status == 403 ? new("No tienes permiso para consultar esta elegibilidad", "", eligibility.CorrelationId)
                    : eligibility.Status == 404 ? HiddenAssignment() : eligibility.Error;
                Response.StatusCode = eligibility.Status;
                if (eligibility.Status == 403) CanExplainAssignment = false;
            }
            else
            {
                if (eligibility.Data is null || eligibility.Data.ObligationId != id ||
                    eligibility.Data.Result is not ("CANDIDATOS_ELEGIBLES" or "SIN_CANDIDATO_ELEGIBLE")) throw new ApiProtocolException();
                AssignmentEligibility = eligibility.Data;
            }
        }
        catch (ApiProtocolException) { EligibilityError = ReadFailure(); Response.StatusCode = 503; }
        return true;
    }

    private async Task<IFormCollection?> ReadCorrectionFormAsync(CancellationToken token)
    {
        NoStore();
        if (!Request.HasFormContentType) return null;
        try { await formAntiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return null; }
        var form = await Request.ReadFormAsync(token);
        SetFilters(null, null, null, null, null, null);
        return form;
    }

    public async Task<IActionResult> OnPostPrepareCorrectionAsync(CancellationToken cancellationToken)
    {
        var form = await ReadCorrectionFormAsync(cancellationToken);
        if (form is null) return BadRequest();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(AssignmentCorrectionAuthorization.Correct)) return StatusCode(403);
        AssignmentQuery = form["obligationId"].ToString();
        CorrectionReason = form["reason"].ToString();
        var page = await LoadAsync(cancellationToken);
        if (page is RedirectResult) return page;
        if (!CanPrepareCorrection || !CanonicalId(form["newResponsiblePersonId"], out var responsible) ||
            !CorrectionCandidates.Any(c => c.PersonId == responsible))
        {
            CorrectionError = new("Revisa el responsable y el motivo", "", null); Response.StatusCode = 400; return Page();
        }
        SelectedResponsible = responsible;
        try { CorrectionReason = AssignmentCorrectionReason.Normalize(CorrectionReason); }
        catch (AssignmentCorrectionReasonInvalidException)
        {
            CorrectionFieldError = "Escribe un motivo de 10 a 500 caracteres después de normalizar espacios";
            CorrectionError = new(CorrectionFieldError, "", null); Response.StatusCode = 400; return Page();
        }
        // Bind the version shown in the form; a preparation never silently adopts a newer version.
        if (form["etag"].Count != 1 || form["etag"].ToString() != AssignmentEtag ||
            !CanonicalId(form["evaluationId"], out var evaluation) || evaluation != AssignmentEligibility!.EvaluationId)
        {
            CorrectionConflict = true;
            CorrectionError = new("Esta obligación cambió mientras preparabas la corrección", "Recarga antes de preparar otra corrección.", null);
            Response.StatusCode = 412; return Page();
        }
        PreparedCorrection = new(responsible, evaluation, CorrectionReason);
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        if (expires > session.AbsoluteExpiresAt) expires = session.AbsoluteExpiresAt;
        CorrectionIntentToken = CorrectionProtector.Protect(JsonSerializer.Serialize(new CorrectionIntention(
            session.UserId, AssignmentObligation!.ObligationId, Guid.CreateVersion7(), expires, AssignmentEtag!, PreparedCorrection)));
        OpenCorrectionConfirmation = true;
        return Page();
    }

    public async Task<IActionResult> OnPostCorrectAssignmentAsync(CancellationToken cancellationToken)
    {
        var form = await ReadCorrectionFormAsync(cancellationToken);
        if (form is null) return BadRequest();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(AssignmentCorrectionAuthorization.Correct)) return StatusCode(403);
        CorrectionIntention? intent;
        try
        {
            if (form["correctionIntent"].Count != 1) throw new CryptographicException();
            intent = JsonSerializer.Deserialize<CorrectionIntention>(CorrectionProtector.Unprotect(form["correctionIntent"]!));
            if (intent is null || intent.Actor != session.UserId || intent.ExpiresAt <= DateTimeOffset.UtcNow ||
                intent.ObligationId == Guid.Empty || intent.Key == Guid.Empty || !ValidEtag(intent.Etag)) throw new CryptographicException();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            CorrectionError = new("La intención venció o no se pudo verificar; consulta la asignación antes de preparar otra corrección", "", null);
            Response.StatusCode = 400; return await LoadAsync(cancellationToken);
        }
        AssignmentQuery = intent.ObligationId.ToString("D");
        PreparedCorrection = intent.Body;
        CorrectionIntentToken = form["correctionIntent"];
        SelectedResponsible = intent.Body.NewResponsiblePersonId;
        CorrectionReason = intent.Body.Reason;
        try
        {
            var response = await antiforgery.SendValidatedAsync<AssignmentCorrectionResult>(HttpContext,
                new(HttpMethod.Post, $"/api/v1/obligations/{intent.ObligationId:D}/assignment-corrections", ApiResponseShape.Item,
                    intent.Body, intent.Etag, ApiMutationIntent.FromKey(intent.Key)), cancellationToken);
            if (response is null) { CorrectionError = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null); Response.StatusCode = 400; }
            else if (response.Status == 401) return Redirect("/acceso");
            else if (!response.IsSuccess)
            {
                CorrectionConflict = response.Status == 412 || response.ErrorCode is "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO";
                CorrectionError = new(CorrectionMessage(response.Status, response.ErrorCode) ?? response.Error!.Title,
                    response.Error?.Message ?? "", response.CorrelationId);
                Response.StatusCode = response.Status;
                if (response.Status == 403) { correctionDenied = true; PreparedCorrection = null; CorrectionIntentToken = null; }
            }
            else
            {
                if (response.Data is null || response.Data.ObligationId != intent.ObligationId ||
                    response.Data.NewResponsiblePersonId != intent.Body.NewResponsiblePersonId ||
                    response.Data.Status != "VIGENTE" || response.Data.AssignmentType != "CORRECCION" ||
                    (response.Status, response.Data.Result) is not ((201, "CREADA") or (200, "RECUPERADA")) ||
                    response.ETag != $"\"{response.Data.RowVersion.ToString(CultureInfo.InvariantCulture)}\"") throw new ApiProtocolException();
                CorrectionResult = response.Data;
            }
        }
        catch (ApiProtocolException)
        {
            CorrectionUncertain = true;
            CorrectionError = new("No se pudo confirmar la corrección", "Consulta la asignación antes de decidir otro intento.", null);
            Response.StatusCode = 503;
        }
        var status = Response.StatusCode;
        var result = await LoadAsync(cancellationToken);
        if (status >= 400) Response.StatusCode = status;
        return result;
    }

    private sealed record CursorTrail(Guid Actor, string Filter, string? Cursor, string?[] Previous);
    private IDataProtector CursorProtector(string section) => protection.CreateProtector("SGOL.FRONT-014.cursor.v1", section);
    private bool ReadCursor(string section, string filter, string? token, out string? cursor, out string?[] previous)
    {
        cursor = null; previous = [];
        if (string.IsNullOrEmpty(token)) return true;
        try
        {
            var trail = JsonSerializer.Deserialize<CursorTrail>(CursorProtector(section).Unprotect(token));
            if (trail is null || trail.Actor != cursorActor || trail.Filter != filter || trail.Previous.Length > 100) return false;
            cursor = trail.Cursor; previous = trail.Previous; return true;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException) { return false; }
    }
    private string ProtectCursor(string section, string filter, string? cursor, string?[] previous) =>
        CursorProtector(section).Protect(JsonSerializer.Serialize(new CursorTrail(cursorActor, filter, cursor, previous)));
    private string? PreviousCursor(string section, string filter, string?[] previous) => previous.Length == 1 && previous[0] is null
        ? null : ProtectCursor(section, filter, previous[^1], previous[..^1]);
    public string NextHistoryHref => AssignmentHref(ProtectCursor("history", AssignmentQuery ?? "", NextAssignmentHistoryCursor, [.. previousHistoryCursors, currentHistoryCursor]));
    public string NextLoadHref => LoadsHref(ProtectCursor("loads", LoadPersonQuery ?? "", NextLoadCursor, [.. previousLoadCursors, currentLoadCursor]));
    public string AssignmentHref(string? historyCursor = null) => "/planificacion?obligationId=" +
        Uri.EscapeDataString(AssignmentQuery ?? "") + (historyCursor is null ? "" : "&assignmentHistoryCursor=" + Uri.EscapeDataString(historyCursor)) + "#asignaciones";
    public string LoadsHref(string? cursor = null) => "/planificacion?loadPersonId=" + Uri.EscapeDataString(LoadPersonQuery ?? "") +
        (cursor is null ? "" : "&loadCursor=" + Uri.EscapeDataString(cursor)) +
        (string.IsNullOrEmpty(AssignmentQuery) ? "" : "&obligationId=" + Uri.EscapeDataString(AssignmentQuery)) + "#carga-activa";
    public static string AssignmentTime(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, MexicoCity).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-MX"));
    private static bool CanonicalId(string? text, out Guid value) => Guid.TryParseExact(text, "D", out value) && value != Guid.Empty;
    private static ProblemDetailsPresentation HiddenAssignment() => new("No existe o no está disponible en tu alcance", "", null);
    private static ProblemDetailsPresentation ReadFailure() => new("No se pudo completar la operación", "Vuelve a consultar más tarde.", null);
    public static string ExclusionText(string reason) => reason switch
    {
        "PERSONA_INACTIVA" => "Persona inactiva",
        "EMPLEO_NO_VIGENTE" => "Sin empleo vigente",
        "SUCURSAL_NO_COINCIDE" => "La sucursal no coincide",
        "ROL_ACTIVO_AUSENTE" => "Sin rol activo",
        "ROL_REQUERIDO_NO_COINCIDE" => "El rol no coincide con el requerido",
        "DISPONIBILIDAD_AUSENTE" => "Sin disponibilidad registrada",
        "DISPONIBILIDAD_NO_POSITIVA" => "No disponible para la fecha evaluada",
        "TURNO_NO_COINCIDE" => "El turno no coincide",
        _ => "Razón no reconocida"
    };
    public static string? CorrectionMessage(int status, string? code) => (status, code) switch
    {
        (403, "ACCESO_DENEGADO") => "No tienes permiso para corregir esta asignación",
        (404, _) => "No existe o no está disponible en tu alcance",
        (412, "VERSION_CONFLICT") or (400, "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO") => "Esta obligación cambió mientras preparabas la corrección",
        (400, "MOTIVO_INVALIDO") => "Escribe un motivo de 10 a 500 caracteres después de normalizar espacios",
        (400, "SOLICITUD_CORRECCION_INVALIDA") => "Revisa el responsable y el motivo",
        (400, "IDEMPOTENCY_KEY_INVALIDA") => "No se pudo verificar la intención de corrección",
        (409, "IDEMPOTENCY_CONFLICT") => "Esta intención ya se envió con otros datos",
        (409, "ASIGNACION_VIGENTE_NO_ENCONTRADA") => "No hay una asignación vigente para corregir",
        (409, "EVALUACION_ELEGIBILIDAD_DESACTUALIZADA") => "Hay una evaluación más reciente; recarga antes de preparar otra corrección",
        (409 or 422, "EVALUACION_ELEGIBILIDAD_INCOMPATIBLE") => "La evaluación no permite esta corrección",
        (422, "RESPONSABLE_INELEGIBLE") => "El responsable seleccionado no es elegible para esta corrección",
        (422, "ASIGNACION_SIN_CAMBIO") => "La persona seleccionada ya es responsable vigente",
        (422, "OBLIGACION_NO_CORREGIBLE") => "Esta obligación no admite corrección",
        (409, "ASSIGNMENT_CORRECTION_CONCURRENCY_CONFLICT" or "ASSIGNMENT_CORRECTION_CONFLICT") => "No se pudo confirmar la corrección",
        _ => null
    };
}
