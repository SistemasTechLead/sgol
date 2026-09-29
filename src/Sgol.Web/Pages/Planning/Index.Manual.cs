using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    private bool manualDenied;
    public bool CanCreateManual { get; private set; }
    public IReadOnlyList<ManualGenerationOption> ManualOptions { get; private set; } = [];
    public string? ManualTaskCode { get; private set; }
    public string? ManualResultQuery { get; private set; }
    public ManualGenerationOption? ManualOption => ManualOptions.FirstOrDefault(o => o.TaskCode == ManualTaskCode);
    public BranchCatalogItem? ManualBranch { get; private set; }
    public ProblemDetailsPresentation? ManualError { get; private set; }
    public IReadOnlyList<ApiFieldError> ManualFields { get; private set; } = [];
    public Dictionary<string, string> ManualValues { get; } = new(StringComparer.Ordinal);
    public List<string> ManualClaimants { get; } = ["", ""];
    public IReadOnlyList<ReceiptOrigin> ManualReceipts { get; private set; } = [];
    public string? ReceiptFilter { get; private set; }
    public string? ReceiptCursor { get; private set; }
    public string? NextReceiptCursor { get; private set; }
    public string? ManualIntentToken { get; private set; }
    public ManualBody? PreparedManual { get; private set; }
    public GenerationRequestDetails? ManualResult { get; private set; }
    public bool ManualConflict { get; private set; }
    public bool ManualExpired { get; private set; }
    public bool OpenManualConfirmation { get; private set; }
    public string ManualValue(string field) => ManualValues.GetValueOrDefault(field, "");
    public bool ManualInvalid(string field) => ManualFields.Any(e => e.Path == "inputPayload." + field || e.Path == field);

    public sealed record ManualBody(int SchemaVersion, Guid RuleVersionId, Guid BranchId, Guid PeriodId,
        string OriginType, JsonElement InputPayload);
    private sealed record ManualIntention(Guid Actor, Guid Key, DateTimeOffset ExpiresAt, ManualBody Body);
    private IDataProtector ManualProtector => protection.CreateProtector("SGOL.FRONT-013.manual-intention.v2");

    private async Task LoadManualAsync(CancellationToken token)
    {
        var session = await sessionState.GetAsync(token);
        CanCreateManual = !manualDenied && session?.Permissions.Contains(GenerationRequestAuthorization.Create) == true;
        if (!CanCreateManual)
        {
            if (ManualTaskCode is not null || ManualResultQuery is not null)
            {
                Response.StatusCode = 404;
                ManualError = new("No existe o no está disponible en tu alcance", "", null);
            }
            return;
        }
        try
        {
            var options = await apiClient.SendAsync<ManualGenerationOption>(new(HttpMethod.Get,
                "/api/v1/generation-requests/options", ApiResponseShape.Collection), token);
            if (!options.IsSuccess)
            {
                ManualError = ManualProblem(options);
                Response.StatusCode = options.Status;
                if (options.Status is 401 or 403) CanCreateManual = false;
                return;
            }
            ManualOptions = options.Items ?? [];
            var branch = await apiClient.SendAsync<BranchCatalogItem>(new(HttpMethod.Get,
                "/api/v1/branches/LOR-001", ApiResponseShape.Item), token);
            if (branch.IsSuccess) ManualBranch = branch.Data;
            else { ManualError = branch.Error; Response.StatusCode = branch.Status; }
            if (ManualTaskCode is not null && !ManualGenerationInput.TaskCodes.Contains(ManualTaskCode))
            {
                ManualError = new("No existe o no está disponible en tu alcance", "", null);
                Response.StatusCode = 404;
                ManualTaskCode = null;
            }
            if (ManualTaskCode == "TAR-0093" && ManualOption is not null) await LoadReceiptsAsync(token);
            if (ManualResultQuery is not null)
            {
                if (!Guid.TryParseExact(ManualResultQuery, "D", out var id) || id == Guid.Empty)
                {
                    ManualError = new("No existe o no está disponible en tu alcance", "", null);
                    Response.StatusCode = 404;
                    return;
                }
                var result = await apiClient.SendAsync<GenerationRequestDetails>(new(HttpMethod.Get,
                    $"/api/v1/generation-requests/{id:D}", ApiResponseShape.Item), token);
                if (result.IsSuccess) ManualResult = result.Data;
                else { ManualError = ManualProblem(result); Response.StatusCode = result.Status; }
            }
        }
        catch (ApiProtocolException)
        {
            ManualError = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            Response.StatusCode = 503;
        }
    }

    private async Task LoadReceiptsAsync(CancellationToken token)
    {
        var query = new List<string>();
        if (ReceiptFilter is not null) query.Add("receiptReference=" + Uri.EscapeDataString(ReceiptFilter));
        if (ReceiptCursor is not null) query.Add("cursor=" + Uri.EscapeDataString(ReceiptCursor));
        var response = await apiClient.SendAsync<ReceiptOrigin>(new(HttpMethod.Get,
            "/api/v1/generation-requests/receipt-origins" + (query.Count == 0 ? "" : "?" + string.Join('&', query)), ApiResponseShape.Collection), token);
        if (response.IsSuccess) { ManualReceipts = response.Items ?? []; NextReceiptCursor = response.NextCursor; }
        else { ManualError = ManualProblem(response); Response.StatusCode = response.Status; }
    }

    private async Task<IFormCollection?> ReadManualFormAsync(CancellationToken token)
    {
        NoStore();
        if (!Request.HasFormContentType) return null;
        try { await formAntiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return null; }
        var form = await Request.ReadFormAsync(token);
        SetFilters(form["isoYear"], form["isoWeek"], form["from"], form["to"], null, null);
        ManualTaskCode = form["taskCode"].ToString();
        ReceiptFilter = string.IsNullOrWhiteSpace(form["receiptFilter"]) ? null : form["receiptFilter"].ToString();
        ReceiptCursor = string.IsNullOrEmpty(form["receiptCursor"]) ? null : form["receiptCursor"].ToString();
        return form;
    }

    public async Task<IActionResult> OnPostReceiptSearchAsync(CancellationToken cancellationToken)
    {
        if (await ReadManualFormAsync(cancellationToken) is null) return BadRequest();
        return await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPrepareManualAsync(CancellationToken cancellationToken)
    {
        var form = await ReadManualFormAsync(cancellationToken);
        if (form is null) return BadRequest();
        var page = await LoadAsync(cancellationToken);
        if (!CanCreateManual) return page;
        if (ManualOption is null || ManualBranch is null || Week is null)
        {
            ManualError = new("No se pudo crear la solicitud", "Selecciona un período válido y una tarea disponible.", null);
            Response.StatusCode = 422;
            return Page();
        }
        try
        {
            var input = new JsonObject { ["taskCode"] = ManualTaskCode };
            foreach (var field in ManualGenerationInput.Fields(ManualTaskCode!).Where(f => f != "claimantReferences"))
                ManualValues[field] = form[field].ToString();
            foreach (var field in ManualGenerationInput.Fields(ManualTaskCode!))
            {
                if (field == "claimantReferences")
                {
                    ManualClaimants.Clear();
                    ManualClaimants.AddRange(form[field].Select(s => s ?? ""));
                    input[field] = new JsonArray(ManualClaimants.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
                    continue;
                }
                var text = form[field].ToString();
                ManualValues[field] = text;
                input[field] = field.EndsWith("At", StringComparison.Ordinal) ? ToUtcInput(text, field) : text;
            }
            var normalized = ManualGenerationInput.Normalize(JsonSerializer.SerializeToElement(input));
            ManualGenerationInput.ValidateTimes(normalized, DateTimeOffset.UtcNow);
            var period = Week.Id;
            if (ManualTaskCode == "TAR-0093")
            {
                var parentId = normalized.GetProperty("parentObligationId").GetGuid();
                var parent = ManualReceipts.SingleOrDefault(r => r.ObligationId == parentId)
                    ?? throw ManualGenerationInput.Invalid("parentObligationId");
                period = parent.PeriodId;
            }
            PreparedManual = new(2, ManualOption.RuleVersionId, ManualOption.BranchId, period, ManualOption.OriginType, normalized);
            var session = await sessionState.GetAsync(cancellationToken);
            ManualIntentToken = ManualProtector.Protect(JsonSerializer.Serialize(new ManualIntention(session!.UserId,
                Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddHours(8), PreparedManual)));
            OpenManualConfirmation = true;
        }
        catch (ManualGenerationException e)
        {
            ManualError = new("No se pudo crear la solicitud", "Revisa los campos marcados.", null);
            ManualFields = e.FieldErrors.Select(f => new ApiFieldError(f.Path, f.Code)).ToArray();
            Response.StatusCode = e.Status;
        }
        return Page();
    }

    private static string ToUtcInput(string text, string field)
    {
        string[] formats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFF"];
        if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ||
            MexicoCity.IsInvalidTime(local) || MexicoCity.IsAmbiguousTime(local)) throw ManualGenerationInput.Invalid(field);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), MexicoCity)
            .ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
    }
    public async Task<IActionResult> OnPostCreateManualAsync(CancellationToken cancellationToken)
    {
        var form = await ReadManualFormAsync(cancellationToken);
        if (form is null) return BadRequest();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(GenerationRequestAuthorization.Create)) return StatusCode(404);
        ManualIntentToken = form["manualIntent"].ToString();
        ManualIntention? intent;
        try
        {
            intent = JsonSerializer.Deserialize<ManualIntention>(ManualProtector.Unprotect(ManualIntentToken));
            if (intent is null || intent.Actor != session.UserId) throw new CryptographicException();
            if (intent.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                if (Guid.TryParseExact(form["knownGenerationRequestId"], "D", out var known) && known != Guid.Empty)
                {
                    ManualResultQuery = known.ToString("D");
                    ManualIntentToken = null;
                    await LoadAsync(cancellationToken);
                    return Page();
                }
                ManualExpired = true;
                ManualIntentToken = null;
                ManualError = new("La intención venció y no se pudo confirmar su resultado", "", null);
                Response.StatusCode = 409;
                await LoadAsync(cancellationToken);
                return Page();
            }
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException)
        {
            ManualIntentToken = null;
            ManualError = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
            Response.StatusCode = 400;
            await LoadAsync(cancellationToken);
            return Page();
        }
        PreparedManual = intent.Body;
        ManualTaskCode = ManualGenerationInput.TaskCode(intent.Body.InputPayload);
        try
        {
            var response = await antiforgery.SendValidatedAsync<GenerationRequestDetails>(HttpContext,
                new(HttpMethod.Post, "/api/v1/generation-requests", ApiResponseShape.Item, intent.Body,
                    Intent: ApiMutationIntent.FromKey(intent.Key)), cancellationToken);
            if (response is null)
            {
                ManualError = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
                Response.StatusCode = 400;
            }
            else if (response.Status == 401) return Redirect("/acceso");
            else if (!response.IsSuccess)
            {
                Response.StatusCode = response.Status;
                ManualFields = response.FieldErrors ?? [];
                ManualConflict = response.ErrorCode == "IDEMPOTENCY_CONFLICT";
                ManualError = ManualConflict
                    ? new("Esta solicitud ya se envió con otros datos", "", response.CorrelationId)
                    : ManualProblem(response);
                if (response.Status == 403)
                {
                    manualDenied = true;
                    PreparedManual = null;
                    ManualIntentToken = null;
                    ManualTaskCode = null;
                }
            }
            else ManualResult = response.Data;
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = 503;
            ManualError = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
        }
        await LoadAsync(cancellationToken);
        return Page();
    }

    private static ProblemDetailsPresentation ManualProblem<T>(ApiResponse<T> response) =>
        new("No se pudo crear la solicitud", ManualMessage(response.Status, response.ErrorCode) ?? response.Error?.Message ?? "", response.CorrelationId);

    internal static string? ManualMessage(int status, string? code) => (status, code) switch
    {
        (400, "GENERATION_REQUEST_INVALIDA") => "Revisa los datos de la solicitud",
        (400, "CONSULTA_ORIGEN_INVALIDA") => "Revisa la búsqueda de recepciones",
        (403, "ACCESO_DENEGADO") => "No tienes permiso para crear esta solicitud",
        (404, "GENERATION_REQUEST_NO_ENCONTRADA") => "No existe o no está disponible en tu alcance",
        (409, "REGLA_MANUAL_REQUERIDA") => "La regla vigente no permite alta manual",
        (409, "TAR_INACTIVA") => "Esta tarea no admite nuevas generaciones",
        (409, "CONFIGURACION_GENERACION_INCOMPLETA") => "La configuración de esta tarea está incompleta",
        (409, "ORIGEN_YA_REGISTRADO") => "Este origen ya tiene una solicitud incompatible con los datos ingresados",
        (409, "ORIGEN_IDENTIDAD_INCONSISTENTE") => "No se pudo verificar la identidad del origen",
        (409, "ORIGEN_PADRE_NO_DISPONIBLE") => "La recepción ya no admite esta incidencia",
        (409, "GENERACION_LEGACY_REQUIERE_REVISION") => "Hay orígenes históricos que requieren revisión antes de nuevas altas",
        (409, "IDEMPOTENCY_REPLAY_NO_DISPONIBLE") => "No se puede recuperar esta solicitud de forma segura",
        (422, "PERIODO_INVALIDO") => "Selecciona un período válido",
        (422, "ORIGEN_INVALIDO") => "Revisa los campos de origen",
        (422, "CALENDARIO_GENERACION_INCOMPLETO") => "El calendario publicado no permite calcular el plazo",
        (422, "GENERATION_REQUEST_SCHEMA_REQUERIDO") => "Recarga el formulario para usar el contrato de alta vigente",
        (413, "GENERATION_REQUEST_DEMASIADO_GRANDE") => "La solicitud excede el tamaño permitido",
        _ => null
    };
    public static string ManualLabel(string field) => field switch
    {
        "reservationReference" => "Referencia del separado",
        "merchandiseReference" => "Referencia de mercancía",
        "startedAt" => "Inicio",
        "expiresAt" => "Vencimiento del separado",
        "sourceReference" => "Referencia documental del plazo",
        "operationReference" => "Referencia de operación",
        "detectedAt" => "Detección de la controversia",
        "claimantReferences" => "Reclamantes",
        "caseReference" => "Referencia del caso",
        "authorizationReference" => "Referencia de autorización previa",
        "productReference" => "Referencia del producto",
        "solutionType" => "Solución autorizada",
        "authorizedAt" => "Fecha de autorización",
        "eventReference" => "Referencia del evento",
        "zoneReference" => "Referencia de zona",
        "planogramReference" => "Referencia del planograma",
        "receiptReference" => "Referencia de recepción",
        "supplierReference" => "Referencia del proveedor",
        "documentReference" => "Referencia de nota de envío",
        "parentObligationId" => "Recepción de origen",
        "incidentReference" => "Referencia de incidencia",
        "incidentType" => "Tipo de incidencia",
        "description" => "Descripción de la incidencia",
        "occurredAt" => "Fecha de incidencia",
        _ => field
    };
}
