using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

public sealed class DetailsModel(IRazorSessionState sessionState, ISgolApiClient apiClient) : PageModel
{
    private static readonly TimeZoneInfo MexicoCity = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");
    public PersonDetails? Person { get; private set; }
    public ProblemDetailsPresentation? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid personId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
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
            if (!response.IsSuccess || response.Data is null)
            {
                Response.StatusCode = response.Status;
                Error = response.Error;
                return Page();
            }
            Person = response.Data;
            return Page();
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            Error = new("No se pudo cargar el detalle", "Vuelve a consultar más tarde.", null);
            return Page();
        }
    }

    public static string LocalDateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, MexicoCity).ToString("dd/MM/yyyy HH:mm", SpanishMexico);
}
