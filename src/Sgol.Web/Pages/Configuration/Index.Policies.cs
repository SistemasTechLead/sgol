using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Configuration;

public sealed partial class IndexModel
{
    private static readonly string[] SalesLocalTimes = ["12:00", "17:00"];
    public ActivationPolicyHistoryDetails? ActivationPolicy { get; private set; }
    public EligibilityPolicyHistoryDetails? EligibilityPolicy { get; private set; }
    public string? ActivationReadEtag { get; private set; }
    public string? EligibilityReadEtag { get; private set; }
    public bool CanManageActivation { get; private set; }
    public bool CanManageEligibility { get; private set; }
    public bool PoliciesUnauthorized { get; private set; }
    public bool ActivationConflict { get; private set; }
    public bool EligibilityConflict { get; private set; }
    public ProblemDetailsPresentation? ActivationError { get; private set; }
    public ProblemDetailsPresentation? EligibilityError { get; private set; }
    public string? ActivationSuccess { get; private set; }
    public string? EligibilitySuccess { get; private set; }
    public string? ActivationTimeError { get; private set; }
    public string? ActivationTimeInput { get; private set; }
    public Guid? ActivationErrorReleaseId { get; private set; }
    public Guid ActivationIntentKey { get; private set; } = Guid.CreateVersion7();
    public Guid EligibilityIntentKey { get; private set; } = Guid.CreateVersion7();

    private async Task LoadPoliciesAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null) return;
        var session = await sessionState.GetAsync(cancellationToken);
        CanManageActivation = session?.Permissions?.Contains(ActivationPolicyAuthorization.Administer,
            StringComparer.Ordinal) == true;
        CanManageEligibility = session?.Permissions?.Contains(EligibilityPolicyAuthorization.Administer,
            StringComparer.Ordinal) == true;
        if (CanManageActivation)
        {
            try
            {
                var response = await apiClient.SendAsync<ActivationPolicyHistoryDetails>(
                    new(HttpMethod.Get, $"{TaskDefinitionsPath}/{SelectedTask.TaskCode}/activation-policy",
                        ApiResponseShape.Item), cancellationToken);
                if (response.Status == StatusCodes.Status401Unauthorized)
                {
                    sessionState.Invalidate();
                    PoliciesUnauthorized = true;
                    return;
                }
                if (response.IsSuccess && response.Data?.TaskCode == SelectedTask.TaskCode &&
                    (response.Data.Current is null && response.ETag is null ||
                     response.Data.Current is { } current && response.ETag == VersionEtag.Format(current.RowVersion)))
                {
                    ActivationPolicy = response.Data;
                    ActivationReadEtag = response.ETag;
                }
                else
                {
                    ActivationError = response.Status == StatusCodes.Status403Forbidden
                        ? new("No tienes permiso para administrar esta política", "La sección no está disponible para tu sesión.", response.CorrelationId)
                        : response.Error ?? PolicySafeError(response.CorrelationId);
                    CanManageActivation = false;
                }
            }
            catch (ApiProtocolException) { ActivationError = PolicySafeError(null); CanManageActivation = false; }
        }
        if (CanManageEligibility)
        {
            try
            {
                var response = await apiClient.SendAsync<EligibilityPolicyHistoryDetails>(
                    new(HttpMethod.Get, $"{TaskDefinitionsPath}/{SelectedTask.TaskCode}/eligibility-policy",
                        ApiResponseShape.Item), cancellationToken);
                if (response.Status == StatusCodes.Status401Unauthorized)
                {
                    sessionState.Invalidate();
                    PoliciesUnauthorized = true;
                    return;
                }
                if (response.IsSuccess && response.Data?.TaskCode == SelectedTask.TaskCode &&
                    (response.Data.Current is null && response.ETag is null ||
                     response.Data.Current is { } current && response.ETag == VersionEtag.Format(current.RowVersion)))
                {
                    EligibilityPolicy = response.Data;
                    EligibilityReadEtag = response.ETag;
                }
                else
                {
                    EligibilityError = response.Status == StatusCodes.Status403Forbidden
                        ? new("No tienes permiso para administrar esta política", "La sección no está disponible para tu sesión.", response.CorrelationId)
                        : response.Error ?? PolicySafeError(response.CorrelationId);
                    CanManageEligibility = false;
                }
            }
            catch (ApiProtocolException) { EligibilityError = PolicySafeError(null); CanManageEligibility = false; }
        }
    }

    public Task<IActionResult> OnPostActivationPolicyAsync(CancellationToken cancellationToken) =>
        PutPolicyAsync(activation: true, cancellationToken);

    public Task<IActionResult> OnPostEligibilityPolicyAsync(CancellationToken cancellationToken) =>
        PutPolicyAsync(activation: false, cancellationToken);

    private async Task<IActionResult> PutPolicyAsync(bool activation, CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SelectedTaskCode = form["taskCode"].ToString();
        if (!TaskCodeAllowed(SelectedTaskCode) ||
            !Guid.TryParseExact(form["releaseId"].ToString(), "D", out var releaseId) || releaseId == Guid.Empty ||
            !Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty ||
            activation && (!Guid.TryParseExact(form["taskDefinitionVersionId"].ToString(), "D", out var versionId) || versionId == Guid.Empty))
            return await PolicyFormErrorAsync(activation, "Revisa la TAR y la release", cancellationToken);

        if (activation) ActivationIntentKey = key; else EligibilityIntentKey = key;
        if (activation)
        {
            ActivationErrorReleaseId = releaseId;
            ActivationTimeInput = form["localTime"].ToString();
        }
        var etag = form["policyEtag"].ToString();
        if (!string.IsNullOrEmpty(etag) && (!etag.StartsWith('"') || !etag.EndsWith('"')))
            return await PolicyConflictAsync(activation, cancellationToken);

        JsonDocument? schedule = null;
        try
        {
            object body;
            if (activation)
            {
                var definition = ActivationPolicyCatalog.Require(SelectedTaskCode!);
                if (SelectedTaskCode == "TAR-0026")
                {
                    var localTime = ActivationTimeInput!;
                    if (!TimeOnly.TryParseExact(localTime, "HH:mm", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out _))
                    {
                        ActivationTimeError = "Ingresa una hora local válida HH:mm.";
                        return await PolicyFormErrorAsync(true, "Revisa la hora local", cancellationToken);
                    }
                    schedule = JsonSerializer.SerializeToDocument(new
                    {
                        kind = ActivationScheduleKinds.BusinessDaysBeforeDueDate,
                        businessDaysBefore = 3,
                        localTime,
                        timeZone = ActivationPolicyCatalog.LorettaTimeZone,
                        adjustDueDateToPreviousBusinessDay = true,
                    });
                }
                else if (SelectedTaskCode == "TAR-0005")
                    schedule = JsonSerializer.SerializeToDocument(new
                    {
                        kind = ActivationScheduleKinds.WorkingDayWindows,
                        workingDaysOnly = true,
                        localTimes = SalesLocalTimes,
                        timeZone = ActivationPolicyCatalog.LorettaTimeZone,
                    });
                else schedule = JsonDocument.Parse("null");
                body = new
                {
                    taskDefinitionVersionId = Guid.Parse(form["taskDefinitionVersionId"].ToString()),
                    releaseId,
                    mode = definition.Mode,
                    schedule = schedule.RootElement,
                    originKeySchema = definition.OriginKeySchema,
                };
            }
            else body = new
            {
                releaseId,
                requiredRole = EligibilityPolicyCatalog.RequireRole(SelectedTaskCode!),
                requiresAvailability = true,
                requiredShift = (string?)null,
            };
            var path = $"{TaskDefinitionsPath}/{SelectedTaskCode}/{(activation ? "activation-policy" : "eligibility-policy")}";
            var response = await antiforgery.SendValidatedAsync<JsonElement>(HttpContext,
                new ApiRequest(HttpMethod.Put, path, ApiResponseShape.Item, body,
                    string.IsNullOrEmpty(etag) ? null : etag, ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await PolicyFormErrorAsync(activation, "No se pudo verificar la solicitud", cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess)
            {
                var message = response.Replayed
                    ? "La operación ya se había procesado; consulta la historia antes de otra intención"
                    : "Política guardada en borrador; aún no genera trabajo";
                if (activation) { ActivationSuccess = message; ActivationIntentKey = Guid.CreateVersion7(); ActivationErrorReleaseId = null; }
                else { EligibilitySuccess = message; EligibilityIntentKey = Guid.CreateVersion7(); }
            }
            else
            {
                Response.StatusCode = response.Status;
                var conflict = response.ErrorCode is "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO";
                var error = conflict
                    ? new ProblemDetailsPresentation("Esta política cambió mientras la editabas",
                        "Recarga la política antes de continuar.", response.CorrelationId)
                    : response.ErrorCode switch
                    {
                        "CONFIGURACION_BORRADOR_REQUERIDA" => new("Selecciona una release que siga en borrador",
                            "Recarga las releases antes de continuar.", response.CorrelationId),
                        "VERSION_TAR_REQUERIDA" => new("Selecciona la versión TAR aplicable",
                            "Recarga el detalle antes de continuar.", response.CorrelationId),
                        "POLITICA_ACTIVACION_INVALIDA" or "POLITICA_ELEGIBILIDAD_INVALIDA" =>
                            new("La política no cumple el esquema aprobado", "Revisa los campos señalados.", response.CorrelationId),
                        _ => response.Error ?? PolicySafeError(response.CorrelationId),
                    };
                if (activation) { ActivationError = error; ActivationConflict = conflict; if (conflict) ActivationErrorReleaseId = null; }
                else { EligibilityError = error; EligibilityConflict = conflict; }
                if (response.Status is >= 400 and < 500)
                {
                    if (activation) ActivationIntentKey = Guid.CreateVersion7();
                    else EligibilityIntentKey = Guid.CreateVersion7();
                }
            }
        }
        catch (ApiProtocolException)
        {
            if (activation) ActivationError = PolicySafeError(null); else EligibilityError = PolicySafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        finally { schedule?.Dispose(); }
        return await LoadAsync(cancellationToken);
    }

    private Task<IActionResult> PolicyConflictAsync(bool activation, CancellationToken cancellationToken)
    {
        if (activation) ActivationConflict = true; else EligibilityConflict = true;
        return PolicyFormErrorAsync(activation, "Esta política cambió mientras la editabas", cancellationToken);
    }

    private async Task<IActionResult> PolicyFormErrorAsync(bool activation, string title,
        CancellationToken cancellationToken)
    {
        var error = new ProblemDetailsPresentation(title, "Corrige los campos señalados antes de continuar.", null);
        if (activation) ActivationError = error; else EligibilityError = error;
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return await LoadAsync(cancellationToken);
    }

    private static ProblemDetailsPresentation PolicySafeError(string? correlationId) =>
        new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", correlationId);
}
