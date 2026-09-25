using Microsoft.AspNetCore.Mvc;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Configuration;

public sealed partial class IndexModel
{
    private const string TaskDefinitionsPath = "/api/v1/task-definitions";

    public IReadOnlyList<TaskDefinitionDetails> TaskDefinitions { get; private set; } = [];
    public TaskDefinitionDetails? SelectedTask { get; private set; }
    public string? SelectedTaskCode { get; private set; }
    public bool CanShowTaskDefinitions { get; private set; }
    public bool CanManageTaskDefinitions { get; private set; }
    public bool TaskDefinitionsUnauthorized { get; private set; }
    public bool TaskConflict { get; private set; }
    public ProblemDetailsPresentation? TaskDefinitionsError { get; private set; }
    public string? TaskDefinitionsSuccess { get; private set; }
    public string? TaskActionError { get; private set; }
    public Guid? TaskErrorVersionId { get; private set; }
    public string? TaskEffectiveFromInput { get; private set; }
    public string? TaskReasonInput { get; private set; }
    public string? TaskEffectiveFromError { get; private set; }
    public string? TaskReasonError { get; private set; }
    public Guid TaskCreateIntentKey { get; private set; } = Guid.CreateVersion7();
    public Guid TaskMutationIntentKey { get; private set; } = Guid.CreateVersion7();

    public string TaskEtag(TaskDefinitionVersionDetails version) => VersionEtag.Format(version.RowVersion);

    public string TaskStatusText(string status) => status switch
    {
        VersionStatuses.Draft => "Borrador",
        VersionStatuses.Current => "Vigente",
        VersionStatuses.Superseded => "Sustituida",
        TaskDefinitionStatuses.InactiveForNew => "Inactiva para nuevas generaciones",
        _ => "Estado no disponible",
    };

    public string TaskBadgeClass(string status) => status switch
    {
        VersionStatuses.Draft => "badge--neutro",
        VersionStatuses.Current => "badge--exito",
        VersionStatuses.Superseded => "badge--info",
        TaskDefinitionStatuses.InactiveForNew => "badge--advertencia",
        _ => "badge--neutro",
    };

    public string TaskBadgeIcon(string status) => status switch
    {
        VersionStatuses.Draft => "✎",
        VersionStatuses.Current => "✓",
        VersionStatuses.Superseded => "◷",
        TaskDefinitionStatuses.InactiveForNew => "Ⅱ",
        _ => "?",
    };

    private async Task LoadTaskDefinitionsAsync(CancellationToken cancellationToken)
    {
        var session = await sessionState.GetAsync(cancellationToken);
        CanManageTaskDefinitions = session?.Permissions?.Contains(
            TaskDefinitionAuthorization.Administer, StringComparer.Ordinal) == true;
        if (string.IsNullOrEmpty(SelectedTaskCode))
            SelectedTaskCode = Request.Query["taskCode"].ToString();
        try
        {
            var response = await apiClient.SendAsync<TaskDefinitionDetails>(
                new ApiRequest(HttpMethod.Get, TaskDefinitionsPath, ApiResponseShape.Collection), cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized)
            {
                TaskDefinitionsUnauthorized = true;
                return;
            }
            if (!response.IsSuccess || response.Items is null)
            {
                TaskDefinitionsError = response.Error ?? TaskSafeError(response.CorrelationId);
                if (Response.StatusCode == 200) Response.StatusCode = response.Status;
                return;
            }
            var canonical = TaskDefinitionCatalog.All.Select(item => item.TaskCode).ToHashSet(StringComparer.Ordinal);
            if (response.Items.Count != canonical.Count ||
                response.Items.Select(item => item.TaskCode).ToHashSet(StringComparer.Ordinal).Count != canonical.Count ||
                response.Items.Any(item => !canonical.Contains(item.TaskCode)))
                throw new ApiProtocolException();
            TaskDefinitions = response.Items;
            CanShowTaskDefinitions = true;
            if (string.IsNullOrEmpty(SelectedTaskCode)) return;
            if (!canonical.Contains(SelectedTaskCode))
            {
                TaskDefinitionsError = new("Esta TAR no pertenece al catálogo MVP",
                    "Selecciona una de las ocho definiciones aprobadas.", response.CorrelationId);
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var detail = await apiClient.SendAsync<TaskDefinitionDetails>(
                new ApiRequest(HttpMethod.Get, $"{TaskDefinitionsPath}/{SelectedTaskCode}", ApiResponseShape.Item),
                cancellationToken);
            if (detail.Status == StatusCodes.Status401Unauthorized)
            {
                TaskDefinitionsUnauthorized = true;
                return;
            }
            if (!detail.IsSuccess || detail.Data is null || detail.Data.TaskCode != SelectedTaskCode)
            {
                TaskDefinitionsError = detail.Error ?? TaskSafeError(detail.CorrelationId);
                Response.StatusCode = detail.IsSuccess ? StatusCodes.Status503ServiceUnavailable : detail.Status;
                return;
            }
            SelectedTask = detail.Data;
        }
        catch (ApiProtocolException)
        {
            TaskDefinitionsError = TaskSafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }

    public async Task<IActionResult> OnPostCreateTaskVersionAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SelectedTaskCode = form["taskCode"].ToString();
        if (!TaskCodeAllowed(SelectedTaskCode) ||
            !Guid.TryParseExact(form["releaseId"].ToString(), "D", out var releaseId) || releaseId == Guid.Empty ||
            !Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await TaskFormErrorAsync("Revisa la TAR y la release", cancellationToken);
        TaskCreateIntentKey = key;
        try
        {
            var response = await antiforgery.SendValidatedAsync<TaskDefinitionVersionDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, $"{TaskDefinitionsPath}/{SelectedTaskCode}/versions", ApiResponseShape.Item,
                    new { releaseId, schemaVersion = TaskDefinitionCatalog.SchemaVersion, taskPayload = new { } },
                    Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null) return await TaskFormErrorAsync("No se pudo verificar la solicitud", cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess && response.Data is { Status: VersionStatuses.Draft })
            {
                TaskDefinitionsSuccess = "Borrador de TAR disponible; todavía no genera trabajo";
                TaskCreateIntentKey = Guid.CreateVersion7();
            }
            else SetTaskError(response);
        }
        catch (ApiProtocolException)
        {
            TaskDefinitionsError = TaskSafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadAsync(cancellationToken);
    }

    public Task<IActionResult> OnPostPublishTaskVersionAsync(CancellationToken cancellationToken) =>
        MutateTaskVersionAsync(deactivate: false, cancellationToken);

    public Task<IActionResult> OnPostDeactivateTaskAsync(CancellationToken cancellationToken) =>
        MutateTaskVersionAsync(deactivate: true, cancellationToken);

    private async Task<IActionResult> MutateTaskVersionAsync(bool deactivate, CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SelectedTaskCode = form["taskCode"].ToString();
        TaskActionError = deactivate ? "deactivate" : "publish";
        if (Guid.TryParseExact(form["versionId"].ToString(), "D", out var errorVersionId))
            TaskErrorVersionId = errorVersionId;
        TaskEffectiveFromInput = form["effectiveFrom"].ToString();
        TaskReasonInput = form["reason"].ToString().Trim();
        if (!TaskCodeAllowed(SelectedTaskCode) ||
            !Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty ||
            !Guid.TryParseExact(form[deactivate ? "releaseId" : "versionId"].ToString(), "D", out var resourceId) ||
            resourceId == Guid.Empty)
            return await TaskFormErrorAsync("Revisa la TAR y la versión", cancellationToken);
        TaskMutationIntentKey = key;
        if (!TryParseEffectiveFrom(TaskEffectiveFromInput, out var effectiveFrom))
            TaskEffectiveFromError = "Ingresa una fecha y hora válidas de Ciudad de México.";
        if (string.IsNullOrWhiteSpace(TaskReasonInput))
            TaskReasonError = "Ingresa el motivo antes de continuar.";
        if (TaskEffectiveFromError is not null || TaskReasonError is not null)
            return await TaskFormErrorAsync("Revisa la fecha y el motivo", cancellationToken);
        var etag = form["versionEtag"].ToString();
        if (string.IsNullOrEmpty(etag))
        {
            TaskConflict = true;
            return await TaskFormErrorAsync("Esta versión cambió mientras la editabas", cancellationToken);
        }
        try
        {
            var path = deactivate
                ? $"{TaskDefinitionsPath}/{SelectedTaskCode}/deactivate-new"
                : $"{TaskDefinitionsPath}/{SelectedTaskCode}/versions/{resourceId:D}/publish";
            var body = deactivate ? (object)new { releaseId = resourceId, effectiveFrom, reason = TaskReasonInput }
                : new { effectiveFrom, reason = TaskReasonInput };
            var response = await antiforgery.SendValidatedAsync<TaskDefinitionVersionDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, path, ApiResponseShape.Item, body, etag,
                    ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null) return await TaskFormErrorAsync("No se pudo verificar la solicitud", cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess && response.Data is not null)
            {
                TaskDefinitionsSuccess = deactivate
                    ? "Se detuvieron las nuevas generaciones; las obligaciones existentes permanecen intactas"
                    : "Versión de TAR publicada; las obligaciones anteriores permanecen intactas";
                TaskMutationIntentKey = Guid.CreateVersion7();
                TaskActionError = null;
            }
            else SetTaskError(response);
        }
        catch (ApiProtocolException)
        {
            TaskDefinitionsError = TaskSafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadAsync(cancellationToken);
    }

    private void SetTaskError(ApiResponse<TaskDefinitionVersionDetails> response)
    {
        Response.StatusCode = response.Status;
        TaskConflict = response.ErrorCode is "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO";
        TaskDefinitionsError = response.ErrorCode switch
        {
            "DEFINICION_NO_MVP" => new("Esta TAR no pertenece al catálogo MVP",
                "Selecciona una de las ocho definiciones aprobadas.", response.CorrelationId),
            "DEFINICION_INVALIDA" => new("La definición no cumple el esquema aprobado",
                "Revisa la selección y vuelve a consultar.", response.CorrelationId),
            "CONFIGURACION_BORRADOR_REQUERIDA" => new("Selecciona una release que siga en borrador",
                "Recarga las releases antes de continuar.", response.CorrelationId),
            "CONFIGURACION_SOLAPADA" => new("La vigencia se solapa con otra versión publicada",
                "Consulta la historia y elige otra fecha.", response.CorrelationId),
            "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO" =>
                new("Esta versión cambió mientras la editabas", "Recarga las definiciones antes de continuar.",
                    response.CorrelationId),
            "ACCESO_DENEGADO" => new("No tienes permiso para administrar definiciones TAR",
                "La acción no está disponible para tu sesión.", response.CorrelationId),
            _ => response.Error ?? TaskSafeError(response.CorrelationId),
        };
        if (response.Status is >= 400 and < 500)
        {
            TaskMutationIntentKey = Guid.CreateVersion7();
            TaskCreateIntentKey = Guid.CreateVersion7();
        }
    }

    private async Task<IActionResult> TaskFormErrorAsync(string title, CancellationToken cancellationToken)
    {
        TaskDefinitionsError = new(title, "Corrige los campos señalados antes de continuar.", null);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return await LoadAsync(cancellationToken);
    }

    private static bool TaskCodeAllowed(string? taskCode) =>
        TaskDefinitionCatalog.All.Any(item => item.TaskCode == taskCode);

    private static ProblemDetailsPresentation TaskSafeError(string? correlationId) =>
        new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", correlationId);
}
