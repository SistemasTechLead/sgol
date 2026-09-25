using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Configuration;

[IgnoreAntiforgeryToken] // The shared bridge validates the Razor token and forwards its CSRF pair.
public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    RazorAntiforgeryBridge antiforgery) : PageModel
{
    private const string ReleasesPath = "/api/v1/configuration/releases";
    private static readonly TimeZoneInfo OperationalZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");

    public IReadOnlyList<ConfigurationReleaseDetails> Releases { get; private set; } = [];
    public bool CanManage { get; private set; }
    public bool CanShow { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public string? Success { get; private set; }
    public Guid? ConflictReleaseId { get; private set; }
    public Guid? PublicationErrorReleaseId { get; private set; }
    public string? EffectiveFromInput { get; private set; }
    public string? ReasonInput { get; private set; }
    public string? EffectiveFromError { get; private set; }
    public string? ReasonError { get; private set; }
    public Guid CreateIntentKey { get; private set; } = Guid.CreateVersion7();
    public Guid PublishIntentKey { get; private set; } = Guid.CreateVersion7();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        return await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await FormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.",
                StatusCodes.Status400BadRequest, cancellationToken);
        CreateIntentKey = key;
        try
        {
            var response = await antiforgery.SendValidatedAsync<ConfigurationReleaseDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, ReleasesPath, ApiResponseShape.Item,
                    Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await FormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.",
                    StatusCodes.Status400BadRequest, cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess && response.Data is { Id: var id } && id != Guid.Empty)
            {
                Success = response.Replayed || response.Data.Status != VersionStatuses.Draft
                    ? "La operación ya se había procesado; consulta la historia"
                    : "Borrador disponible; todavía no está vigente";
                CreateIntentKey = Guid.CreateVersion7();
            }
            else
            {
                Error = response.Error ?? SafeError(response.CorrelationId);
                Response.StatusCode = response.Status;
                if (response.Status is >= 400 and < 500) CreateIntentKey = Guid.CreateVersion7();
            }
        }
        catch (ApiProtocolException)
        {
            Error = SafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParseExact(form["releaseId"].ToString(), "D", out var releaseId) || releaseId == Guid.Empty ||
            !Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await FormErrorAsync("No se pudo verificar la solicitud", "Recarga las releases antes de continuar.",
                StatusCodes.Status400BadRequest, cancellationToken);

        PublishIntentKey = key;
        EffectiveFromInput = form["effectiveFrom"].ToString();
        ReasonInput = form["reason"].ToString().Trim();
        var etag = form["releaseEtag"].ToString();
        if (!TryParseEffectiveFrom(EffectiveFromInput, out var effectiveFrom))
            EffectiveFromError = "Ingresa una fecha y hora válidas de Ciudad de México.";
        if (string.IsNullOrWhiteSpace(ReasonInput)) ReasonError = "Ingresa el motivo antes de continuar.";
        if (EffectiveFromError is not null || ReasonError is not null)
        {
            PublicationErrorReleaseId = releaseId;
            Error = new("Revisa la fecha y el motivo", "Corrige los campos señalados antes de publicar.", null);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await LoadAsync(cancellationToken);
        }
        if (string.IsNullOrEmpty(etag))
        {
            ConflictReleaseId = releaseId;
            Error = new("Este borrador cambió mientras lo editabas", "Recarga las releases antes de publicar.", null);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await LoadAsync(cancellationToken);
        }

        try
        {
            var response = await antiforgery.SendValidatedAsync<ConfigurationReleaseDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, $"{ReleasesPath}/{releaseId:D}/publish", ApiResponseShape.Item,
                    new { effectiveFrom, reason = ReasonInput }, etag,
                    ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await FormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.",
                    StatusCodes.Status400BadRequest, cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.IsSuccess && response.Data is { Status: VersionStatuses.Current })
                Success = response.Replayed ? "La operación ya se había procesado" :
                    "Release publicada; la versión anterior permanece en el historial";
            else
            {
                Response.StatusCode = response.Status;
                (Error, ConflictReleaseId) = MapPublishError(response, releaseId);
                PublicationErrorReleaseId = ConflictReleaseId is null ? releaseId : null;
                if (response.Status is >= 400 and < 500) PublishIntentKey = Guid.CreateVersion7();
            }
        }
        catch (ApiProtocolException)
        {
            Error = SafeError(null);
            PublicationErrorReleaseId = releaseId;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadAsync(cancellationToken);
    }

    public string Etag(ConfigurationReleaseDetails release) => VersionEtag.Format(release.RowVersion);

    public string LocalInstant(DateTimeOffset? instant) => instant is null ? "—" :
        TimeZoneInfo.ConvertTime(instant.Value, OperationalZone).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-MX"));

    private async Task<IActionResult> LoadAsync(CancellationToken cancellationToken)
    {
        var session = await sessionState.GetAsync(cancellationToken);
        CanManage = session?.Permissions?.Contains(ConfigurationAuthorization.Administer, StringComparer.Ordinal) == true;
        try
        {
            var response = await apiClient.SendAsync<ConfigurationReleaseDetails>(
                new ApiRequest(HttpMethod.Get, ReleasesPath, ApiResponseShape.Collection), cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.Status == StatusCodes.Status403Forbidden)
            {
                Error = new("No tienes permiso para administrar releases", "La sección no está disponible para tu sesión.",
                    response.CorrelationId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Page();
            }
            if (!response.IsSuccess)
            {
                Error = response.Error ?? SafeError(response.CorrelationId);
                Response.StatusCode = response.Status;
                return Page();
            }
            Releases = response.Items ?? [];
            CanShow = true;
            return Page();
        }
        catch (ApiProtocolException)
        {
            Error = SafeError(null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
    }

    private async Task<IActionResult> FormErrorAsync(string title, string message, int status,
        CancellationToken cancellationToken)
    {
        Error = new(title, message, null);
        Response.StatusCode = status;
        return await LoadAsync(cancellationToken);
    }

    private static bool TryParseEffectiveFrom(string? value, out DateTimeOffset instant)
    {
        instant = default;
        if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) return false;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (OperationalZone.IsInvalidTime(local) || OperationalZone.IsAmbiguousTime(local)) return false;
        instant = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, OperationalZone), TimeSpan.Zero);
        return true;
    }

    private static (ProblemDetailsPresentation Error, Guid? ConflictReleaseId) MapPublishError(
        ApiResponse<ConfigurationReleaseDetails> response, Guid releaseId) => response.ErrorCode switch
        {
            "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO" =>
                (new("Este borrador cambió mientras lo editabas", "Recarga las releases antes de publicar.",
                    response.CorrelationId), releaseId),
            "VIGENCIA_SOLAPADA" =>
                (new("La vigencia se solapa con otra versión publicada", "Consulta la historia y elige otra fecha.",
                    response.CorrelationId), null),
            "PUBLICACION_INVALIDA" or "POLITICA_ELEGIBILIDAD_INCOMPLETA" or
                "POLITICA_EVIDENCIA_INCOMPLETA" or "POLITICA_VALIDACION_INCOMPLETA" =>
                (new("No se puede publicar esta release", "Revisa la configuración vigente antes de continuar.",
                    response.CorrelationId), null),
            "ACCESO_DENEGADO" =>
                (new("No tienes permiso para administrar releases", "La sección no está disponible para tu sesión.",
                    response.CorrelationId), null),
            _ => (response.Error ?? SafeError(response.CorrelationId), null),
        };

    private static ProblemDetailsPresentation SafeError(string? correlationId) =>
        new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", correlationId);

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
