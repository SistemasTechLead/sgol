using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

[IgnoreAntiforgeryToken] // The shared bridge validates the Razor token before forwarding the same pair to the API.
public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    RazorAntiforgeryBridge antiforgery) : PageModel
{
    public IReadOnlyList<PersonSummary> People { get; private set; } = [];
    public ProblemDetailsPresentation? Error { get; private set; }
    public string? CodeError { get; private set; }
    public string? StableCode { get; private set; }
    public string? DisplayName { get; private set; }
    public Guid IntentKey { get; private set; } = Guid.CreateVersion7();
    public bool IsDenied { get; private set; }
    public bool CanShowPeople { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        return await LoadPeopleAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        StableCode = form["stableCode"].ToString();
        DisplayName = form["displayName"].ToString();
        if (!Guid.TryParseExact(form["intentKey"].ToString(), "D", out var key) || key == Guid.Empty)
        {
            Error = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await LoadPeopleAsync(cancellationToken);
        }
        IntentKey = key;
        ApiResponse<PersonDetails>? created;
        try
        {
            created = await antiforgery.SendValidatedAsync<PersonDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, "/api/v1/people", ApiResponseShape.Item,
                    new { stableCode = StableCode, displayName = DisplayName },
                    Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
        }
        catch (ApiProtocolException)
        {
            Error = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await LoadPeopleAsync(cancellationToken);
        }
        if (created is null)
        {
            Error = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
            Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        else if (created.Status == StatusCodes.Status401Unauthorized)
        {
            return Redirect("/acceso");
        }
        else if (created.IsSuccess && created.Data is { Id: var id } && id != Guid.Empty)
        {
            return Redirect($"/personas-y-accesos/personas/{id:D}");
        }
        else
        {
            Response.StatusCode = created.Status;
            if (created.Status == StatusCodes.Status409Conflict && created.ErrorCode == "CODIGO_PERSONA_DUPLICADO")
            {
                CodeError = "El código de persona ya está registrado";
                Error = new(CodeError, "Usa otro código para registrar a la persona.", created.CorrelationId);
                IntentKey = Guid.CreateVersion7();
            }
            else Error = created.Error;
        }
        return await LoadPeopleAsync(cancellationToken);
    }

    private async Task<IActionResult> LoadPeopleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.SendAsync<PersonSummary>(
                new ApiRequest(HttpMethod.Get, "/api/v1/people", ApiResponseShape.Collection), cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
            if (response.Status == StatusCodes.Status403Forbidden)
            {
                IsDenied = true;
                Error = new("No tienes permiso para ver personas y accesos", "La sección no está disponible para tu rol actual.", response.CorrelationId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return Page();
            }
            if (!response.IsSuccess)
            {
                Error = response.Error;
                Response.StatusCode = response.Status;
                return Page();
            }
            People = response.Items ?? [];
            CanShowPeople = true;
            return Page();
        }
        catch (ApiProtocolException)
        {
            Error = new("No se pudo cargar la lista", "Vuelve a consultar más tarde.", null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
