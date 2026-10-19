using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

public sealed partial class DetailsModel
{
    public sealed record AvailabilityDay(DateOnly Date, bool? IsAvailable);

    public bool CanManageAvailability { get; private set; }
    public bool IsAvailabilityConflict { get; private set; }
    public IReadOnlyList<AvailabilityDay> AvailabilityDays { get; private set; } = [];
    public ProblemDetailsPresentation? AvailabilityError { get; private set; }
    public string? AvailabilitySuccess { get; private set; }
    public string FromDate { get; private set; } = string.Empty;
    public string ToDate { get; private set; } = string.Empty;
    public string SelectedDay { get; private set; } = string.Empty;
    public bool? SelectedValue { get; private set; }
    public string? SelectedEtag { get; private set; }
    public Guid AvailabilityIntent { get; private set; } = Guid.CreateVersion7();

    public async Task<IActionResult> OnPostAvailabilityAsync(Guid personId, CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(AvailabilityAuthorization.Administer))
            return StatusCode(StatusCodes.Status404NotFound);
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SetAvailabilityRange(form["fromDate"], form["toDate"], form["day"]);
        var value = form["isAvailable"].ToString();
        if (AvailabilityError is not null || value is not ("true" or "false") ||
            !Guid.TryParseExact(form["intentKey"], "D", out var intent) || intent == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            AvailabilityError ??= new("Revisa el rango y el día", "Selecciona una fecha y Disponible o No disponible.", null);
            return await LoadAsync(personId, cancellationToken);
        }

        var date = DateOnly.ParseExact(SelectedDay, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var etag = form["etag"].ToString();
        if (etag.Length > 0 && !ValidAvailabilityEtag(etag))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            IsAvailabilityConflict = true;
            AvailabilityError = new("La versión del registro no es válida", "Recarga la disponibilidad antes de guardar.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        ApiResponse<AvailabilityDaySnapshot>? changed;
        try
        {
            changed = await antiforgery.SendValidatedAsync<AvailabilityDaySnapshot>(HttpContext,
                new ApiRequest(HttpMethod.Put, $"/api/v1/people/{personId:D}/availability/{SelectedDay}",
                    ApiResponseShape.Item, new { isAvailable = value == "true" },
                    string.IsNullOrEmpty(etag) ? null : etag, ApiMutationIntent.FromKey(intent)), cancellationToken);
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            AvailabilityError = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        if (changed is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            AvailabilityError = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
            return await LoadAsync(personId, cancellationToken);
        }
        if (changed.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
        if (changed.Status is StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            Error = new("No existe o no está disponible en tu alcance", "Vuelve a la lista de personas.", changed.CorrelationId);
            return Page();
        }
        if (changed.Status == StatusCodes.Status412PreconditionFailed ||
            changed.Status == StatusCodes.Status400BadRequest && changed.ErrorCode == "IF_MATCH_INVALIDO")
        {
            Response.StatusCode = changed.Status;
            IsAvailabilityConflict = true;
            AvailabilityError = changed.Error;
            return await LoadAsync(personId, cancellationToken);
        }
        if (!changed.IsSuccess || changed.Data is null || changed.ETag is null)
        {
            Response.StatusCode = changed.Status;
            AvailabilityError = changed.Error;
            return await LoadAsync(personId, cancellationToken);
        }
        AvailabilitySuccess = string.IsNullOrEmpty(etag) ? "Disponibilidad registrada" : "Disponibilidad corregida";
        return await LoadAsync(personId, cancellationToken);
    }

    private void SetAvailabilityRange(string? fromText, string? toText, string? dayText)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, MexicoCity).DateTime);
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        FromDate = fromText ?? Iso(start);
        ToDate = toText ?? Iso(start.AddDays(6));
        SelectedDay = dayText ?? Iso(today);
        if (!TryDate(FromDate, out var from) || !TryDate(ToDate, out var to) ||
            !TryDate(SelectedDay, out var selected) || to < from ||
            to.DayNumber - from.DayNumber >= 100 || selected < from || selected > to)
            AvailabilityError = new("Revisa el rango y el día", "Selecciona fechas válidas dentro del rango permitido.", null);
    }

    private async Task LoadAvailabilityAsync(Guid personId, CancellationToken cancellationToken)
    {
        if (AvailabilityError is not null && !TryDate(FromDate, out _)) return;
        if (!TryDate(FromDate, out var from) || !TryDate(ToDate, out var to) ||
            !TryDate(SelectedDay, out var selected) || to < from ||
            to.DayNumber - from.DayNumber >= 100 || selected < from || selected > to) return;
        try
        {
            var response = await apiClient.SendAsync<AvailabilityDaySnapshot>(new ApiRequest(HttpMethod.Get,
                $"/api/v1/people/{personId:D}/availability?fromDate={FromDate}&toDate={ToDate}",
                ApiResponseShape.Collection), cancellationToken);
            if (!response.IsSuccess || response.Items is null)
            {
                AvailabilityError ??= response.Error;
                return;
            }
            var registered = response.Items.ToDictionary(item => item.LocalDate);
            AvailabilityDays = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset =>
                {
                    var date = from.AddDays(offset);
                    return new AvailabilityDay(date, registered.TryGetValue(date, out var item) ? item.IsAvailable : null);
                }).ToArray();
            if (registered.TryGetValue(selected, out var current))
            {
                SelectedValue = current.IsAvailable;
                SelectedEtag = $"\"{current.RowVersion.ToString(CultureInfo.InvariantCulture)}\"";
            }
        }
        catch (ApiProtocolException)
        {
            AvailabilityError ??= new("No se pudo cargar la disponibilidad", "Consulta el rango más tarde.", null);
        }
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);

    private static string Iso(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool ValidAvailabilityEtag(string value) =>
        value.Length >= 3 && value[0] == '"' && value[^1] == '"' &&
        long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None,
            CultureInfo.InvariantCulture, out var version) && version > 0;
}
