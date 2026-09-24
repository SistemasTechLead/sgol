using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

[IgnoreAntiforgeryToken] // The shared bridge validates Razor antiforgery before forwarding CSRF.
public sealed class DetailsModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    RazorAntiforgeryBridge antiforgery) : PageModel
{
    private static readonly TimeZoneInfo MexicoCity = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");
    public PersonDetails? Person { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }
    public string? ETag { get; private set; }
    public string? PositionText { get; private set; }
    public string? ShiftText { get; private set; }
    public string? Success { get; private set; }
    public bool IsConflict { get; private set; }
    public Guid EmploymentIntent { get; private set; } = Guid.CreateVersion7();
    public Guid DeactivateIntent { get; private set; } = Guid.CreateVersion7();
    public Guid ReactivateIntent { get; private set; } = Guid.CreateVersion7();

    public async Task<IActionResult> OnGetAsync(Guid personId, CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        return await LoadAsync(personId, cancellationToken);
    }

    public Task<IActionResult> OnPostEmploymentAsync(Guid personId, CancellationToken cancellationToken) =>
        MutateAsync(personId, "employment", cancellationToken);

    public Task<IActionResult> OnPostDeactivateAsync(Guid personId, CancellationToken cancellationToken) =>
        MutateAsync(personId, "deactivate", cancellationToken);

    public Task<IActionResult> OnPostReactivateAsync(Guid personId, CancellationToken cancellationToken) =>
        MutateAsync(personId, "reactivate", cancellationToken);

    private async Task<IActionResult> MutateAsync(Guid personId, string action, CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        var reason = form["reason"].ToString().Trim();
        var status = form["status"].ToString();
        var etag = form["etag"].ToString();
        PositionText = form["positionText"].ToString();
        ShiftText = form["shiftText"].ToString();
        if (!Guid.TryParseExact(form["intentKey"].ToString(), "D", out var intent) || intent == Guid.Empty ||
            reason.Length == 0 || etag.Length == 0 ||
            action == "employment" && (status is not ("ACTIVA" or "INACTIVA") ||
                string.IsNullOrWhiteSpace(PositionText) && string.IsNullOrWhiteSpace(ShiftText)))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Error = new("Revisa los datos ingresados", "El motivo y al menos un dato laboral son obligatorios.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        var path = $"/api/v1/people/{personId:D}/{action}";
        var method = action == "employment" ? HttpMethod.Patch : HttpMethod.Post;
        object body = action == "employment"
            ? new
            {
                status,
                reason,
                positionText = string.IsNullOrWhiteSpace(PositionText) ? null : PositionText,
                shiftText = string.IsNullOrWhiteSpace(ShiftText) ? null : ShiftText
            }
            : new { reason };
        ApiResponse<PersonDetails>? changed;
        try
        {
            changed = await antiforgery.SendValidatedAsync<PersonDetails>(HttpContext,
                new ApiRequest(method, path, ApiResponseShape.Item, body, etag,
                    ApiMutationIntent.FromKey(intent)), cancellationToken);
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Error = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        if (changed is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            Error = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        if (changed.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
        if (changed.Status is StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Error = new("No existe o no está disponible en tu alcance", "Vuelve a la lista de personas.", changed.CorrelationId);
            return Page();
        }
        if (changed.Status == StatusCodes.Status412PreconditionFailed)
        {
            Response.StatusCode = changed.Status;
            Error = changed.Error;
            IsConflict = true;
            return Page();
        }
        if (!changed.IsSuccess || changed.Data is null || changed.ETag is null)
        {
            Response.StatusCode = changed.Status;
            Error = changed.Error ?? new("No se pudo completar la operación", "Recarga el detalle y vuelve a intentarlo.", changed.CorrelationId);
            if (changed.ErrorCode == "DATOS_LABORALES_SIN_CAMBIO")
                Error = new("Puesto y turno ya son los vigentes", "Modifica al menos un dato laboral antes de guardar.", changed.CorrelationId);
            if (changed.ErrorCode == "VIGENCIA_SIN_CAMBIO")
                Error = new("La vigencia solicitada ya es la vigente", "Recarga el detalle antes de continuar.", changed.CorrelationId);
            return await LoadAsync(personId, cancellationToken);
        }
        Person = changed.Data;
        ETag = changed.ETag;
        Success = action switch
        {
            "deactivate" => "Persona dada de baja",
            "reactivate" => "Persona reactivada",
            _ => "Empleo actualizado",
        };
        return Page();
    }

    private async Task<IActionResult> LoadAsync(Guid personId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.SendAsync<PersonDetails>(new ApiRequest(HttpMethod.Get,
                $"/api/v1/people/{personId:D}", ApiResponseShape.Item), cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.Status is StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                Error = new("No existe o no está disponible en tu alcance", "Vuelve a la lista de personas.", response.CorrelationId);
                return Page();
            }
            if (!response.IsSuccess || response.Data is null || response.ETag is null)
            {
                Response.StatusCode = response.Status;
                Error ??= response.Error;
                return Page();
            }
            Person = response.Data;
            ETag = response.ETag;
            return Page();
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Error ??= new("No se pudo cargar el detalle", "Vuelve a consultar más tarde.", null);
            return Page();
        }
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public static string LocalDateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, MexicoCity).ToString("dd/MM/yyyy HH:mm", SpanishMexico);
}
