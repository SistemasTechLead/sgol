using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Configuration;

public sealed partial class IndexModel
{
    public EvidencePolicyHistoryDetails? EvidencePolicy { get; private set; }
    public ValidationPolicyHistoryDetails? ValidationPolicy { get; private set; }
    public string? EvidenceReadEtag { get; private set; }
    public string? ValidationReadEtag { get; private set; }
    public bool CanManageEvidence { get; private set; }
    public bool CanManageValidation { get; private set; }
    public bool EvidenceValidationUnauthorized { get; private set; }
    public bool EvidenceConflict { get; private set; }
    public bool ValidationConflict { get; private set; }
    public ProblemDetailsPresentation? EvidenceError { get; private set; }
    public ProblemDetailsPresentation? ValidationError { get; private set; }
    public string? EvidenceSuccess { get; private set; }
    public string? ValidationSuccess { get; private set; }
    public Guid EvidenceIntentKey { get; private set; } = Guid.CreateVersion7();
    public Guid ValidationIntentKey { get; private set; } = Guid.CreateVersion7();
    public Guid? EvidenceErrorReleaseId { get; private set; }
    public Guid? ValidationErrorReleaseId { get; private set; }

    private async Task LoadEvidenceValidationAsync(CancellationToken cancellationToken)
    {
        if (SelectedTask is null) return;
        var session = await sessionState.GetAsync(cancellationToken);
        CanManageEvidence = session?.Permissions?.Contains(EvidencePolicyAuthorization.Administer,
            StringComparer.Ordinal) == true;
        CanManageValidation = session?.Permissions?.Contains(ValidationPolicyAuthorization.Administer,
            StringComparer.Ordinal) == true;
        if (CanManageEvidence)
        {
            try
            {
                var response = await apiClient.SendAsync<EvidencePolicyHistoryDetails>(
                    new(HttpMethod.Get, $"{TaskDefinitionsPath}/{SelectedTask.TaskCode}/evidence-policy",
                        ApiResponseShape.Item), cancellationToken);
                if (response.Status == StatusCodes.Status401Unauthorized)
                {
                    sessionState.Invalidate();
                    EvidenceValidationUnauthorized = true;
                    return;
                }
                if (response.IsSuccess && response.Data?.TaskCode == SelectedTask.TaskCode &&
                    (response.Data.Current is null && response.ETag is null ||
                     response.Data.Current is { } current &&
                     response.ETag == VersionEtag.Format(current.RowVersion)))
                {
                    EvidencePolicy = response.Data;
                    EvidenceReadEtag = response.ETag;
                }
                else
                {
                    EvidenceError = response.Status == StatusCodes.Status403Forbidden
                        ? new("No tienes permiso para administrar esta política",
                            "La sección no está disponible para tu sesión.", response.CorrelationId)
                        : response.Error ?? PolicySafeError(response.CorrelationId);
                    CanManageEvidence = false;
                }
            }
            catch (ApiProtocolException) { EvidenceError = PolicySafeError(null); CanManageEvidence = false; }
        }
        if (CanManageValidation)
        {
            try
            {
                var response = await apiClient.SendAsync<ValidationPolicyHistoryDetails>(
                    new(HttpMethod.Get, $"{TaskDefinitionsPath}/{SelectedTask.TaskCode}/validation-policy",
                        ApiResponseShape.Item), cancellationToken);
                if (response.Status == StatusCodes.Status401Unauthorized)
                {
                    sessionState.Invalidate();
                    EvidenceValidationUnauthorized = true;
                    return;
                }
                if (response.IsSuccess && response.Data?.TaskCode == SelectedTask.TaskCode &&
                    (response.Data.Current is null && response.ETag is null ||
                     response.Data.Current is { } current &&
                     response.ETag == VersionEtag.Format(current.RowVersion)))
                {
                    ValidationPolicy = response.Data;
                    ValidationReadEtag = response.ETag;
                }
                else
                {
                    ValidationError = response.Status == StatusCodes.Status403Forbidden
                        ? new("No tienes permiso para administrar esta política",
                            "La sección no está disponible para tu sesión.", response.CorrelationId)
                        : response.Error ?? PolicySafeError(response.CorrelationId);
                    CanManageValidation = false;
                }
            }
            catch (ApiProtocolException) { ValidationError = PolicySafeError(null); CanManageValidation = false; }
        }
    }

    public Task<IActionResult> OnPostEvidencePolicyAsync(CancellationToken cancellationToken) =>
        PutEvidenceValidationAsync(evidence: true, cancellationToken);

    public Task<IActionResult> OnPostValidationPolicyAsync(CancellationToken cancellationToken) =>
        PutEvidenceValidationAsync(evidence: false, cancellationToken);

    private async Task<IActionResult> PutEvidenceValidationAsync(bool evidence, CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        var permission = evidence ? EvidencePolicyAuthorization.Administer : ValidationPolicyAuthorization.Administer;
        if (session.Permissions?.Contains(permission, StringComparer.Ordinal) != true)
            return StatusCode(StatusCodes.Status403Forbidden);
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SelectedTaskCode = form["taskCode"].ToString();
        if (!TaskCodeAllowed(SelectedTaskCode) ||
            !Guid.TryParseExact(form["releaseId"].ToString(), "D", out var releaseId) || releaseId == Guid.Empty ||
            !Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await EvidenceValidationFormErrorAsync(evidence, "Revisa la TAR y la release", cancellationToken);
        if (evidence) { EvidenceIntentKey = key; EvidenceErrorReleaseId = releaseId; }
        else { ValidationIntentKey = key; ValidationErrorReleaseId = releaseId; }
        var etag = form["policyEtag"].ToString();
        if (!string.IsNullOrEmpty(etag) && (!etag.StartsWith('"') || !etag.EndsWith('"')))
            return await EvidenceValidationConflictAsync(evidence, cancellationToken);

        try
        {
            object body;
            if (evidence)
            {
                body = new
                {
                    releaseId,
                    requirements = EvidencePolicyCatalog.Require(SelectedTaskCode!).Select(item => new
                    {
                        code = item.Code,
                        kind = item.Kind,
                        condition = item.ConditionCode == EvidenceConditionCodes.Always
                            ? null : new { code = item.ConditionCode },
                    }).ToArray(),
                };
            }
            else
            {
                var matrix = ValidationPolicyCatalog.Require(SelectedTaskCode!);
                body = new
                {
                    releaseId,
                    isRequired = true,
                    executorRole = matrix.ExecutorRole,
                    validatorRelation = ValidationPolicyValues.ImmediateSuperior,
                    validatorRole = matrix.ValidatorRole,
                    allowedResults = ValidationPolicyValues.AllowedResults,
                };
            }
            var path = $"{TaskDefinitionsPath}/{SelectedTaskCode}/{(evidence ? "evidence-policy" : "validation-policy")}";
            var response = await antiforgery.SendValidatedAsync<JsonElement>(HttpContext,
                new ApiRequest(HttpMethod.Put, path, ApiResponseShape.Item, body,
                    string.IsNullOrEmpty(etag) ? null : etag, ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await EvidenceValidationFormErrorAsync(evidence, "No se pudo verificar la solicitud", cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess)
            {
                var message = response.Replayed
                    ? "La operación ya se había procesado; consulta la historia antes de otra intención"
                    : "Política guardada en borrador; aún no genera trabajo";
                if (evidence) { EvidenceSuccess = message; EvidenceIntentKey = Guid.CreateVersion7(); EvidenceErrorReleaseId = null; }
                else { ValidationSuccess = message; ValidationIntentKey = Guid.CreateVersion7(); ValidationErrorReleaseId = null; }
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
                        "POLITICA_EVIDENCIA_INVALIDA" => new("Los requisitos no coinciden con el catálogo",
                            "Revisa código, orden, tipo y condición.", response.CorrelationId),
                        "POLITICA_VALIDACION_INVALIDA" => new("La autoridad no coincide con la matriz",
                            "Revisa ejecutor, superior inmediato, validador y resultados.", response.CorrelationId),
                        "CONFIGURACION_BORRADOR_REQUERIDA" or "CONFIGURACION_SOLAPADA" =>
                            new("Revisa la release y la historia", "Recarga antes de decidir otra intención.", response.CorrelationId),
                        "VERSION_TAR_REQUERIDA" =>
                            new("Selecciona la versión TAR aplicable", "Recarga el detalle antes de continuar.", response.CorrelationId),
                        _ => response.Error ?? PolicySafeError(response.CorrelationId),
                    };
                if (evidence) { EvidenceError = error; EvidenceConflict = conflict; }
                else { ValidationError = error; ValidationConflict = conflict; }
                if (response.Status is >= 400 and < 500)
                {
                    if (evidence) EvidenceIntentKey = Guid.CreateVersion7();
                    else ValidationIntentKey = Guid.CreateVersion7();
                }
            }
        }
        catch (ApiProtocolException)
        {
            if (evidence) EvidenceError = PolicySafeError(null); else ValidationError = PolicySafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadAsync(cancellationToken);
    }

    private Task<IActionResult> EvidenceValidationConflictAsync(bool evidence, CancellationToken cancellationToken)
    {
        if (evidence) EvidenceConflict = true; else ValidationConflict = true;
        return EvidenceValidationFormErrorAsync(evidence, "Esta política cambió mientras la editabas", cancellationToken);
    }

    private async Task<IActionResult> EvidenceValidationFormErrorAsync(
        bool evidence, string title, CancellationToken cancellationToken)
    {
        var error = new ProblemDetailsPresentation(title, "Corrige los campos señalados antes de continuar.", null);
        if (evidence) { EvidenceError = error; EvidenceErrorReleaseId ??= Guid.Empty; }
        else { ValidationError = error; ValidationErrorReleaseId ??= Guid.Empty; }
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return await LoadAsync(cancellationToken);
    }
}
