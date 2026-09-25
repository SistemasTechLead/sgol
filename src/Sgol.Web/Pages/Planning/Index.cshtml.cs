using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Configuration.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Planning;

[IgnoreAntiforgeryToken] // The shared bridge validates Razor antiforgery before forwarding CSRF.
public sealed class IndexModel(IRazorSessionState sessionState, ISgolApiClient apiClient,
    RazorAntiforgeryBridge antiforgery) : PageModel
{
    private static readonly TimeZoneInfo MexicoCity = TimeZoneInfo.FindSystemTimeZoneById(CalendarContract.TimeZone);

    public string IsoYear { get; private set; } = string.Empty;
    public string IsoWeek { get; private set; } = string.Empty;
    public string From { get; private set; } = string.Empty;
    public string To { get; private set; } = string.Empty;
    public string Day { get; private set; } = string.Empty;
    public Guid? SelectedReleaseId { get; private set; }
    public WeekPeriodDetails? Week { get; private set; }
    public IReadOnlyList<CalendarDayDetails> PublishedDays { get; private set; } = [];
    public IReadOnlyList<CalendarDayDetails> DraftDays { get; private set; } = [];
    public IReadOnlyList<ConfigurationReleaseDetails> DraftReleases { get; private set; } = [];
    public ProblemDetailsPresentation? WeekError { get; private set; }
    public ProblemDetailsPresentation? CalendarError { get; private set; }
    public ProblemDetailsPresentation? DraftError { get; private set; }
    public string? Success { get; private set; }
    public bool CanViewWeek { get; private set; }
    public bool CanEditCalendar { get; private set; }
    public bool IsConflict { get; private set; }
    public Guid IntentKey { get; private set; } = Guid.CreateVersion7();
    public string? SelectedEtag => DraftDays.FirstOrDefault(item => Iso(item.LocalDate) == Day) is { } value
        ? $"\"{value.RowVersion.ToString(CultureInfo.InvariantCulture)}\"" : null;

    public async Task<IActionResult> OnGetAsync(string? isoYear, string? isoWeek,
        string? from, string? to, string? day, Guid? releaseId, CancellationToken cancellationToken)
    {
        NoStore();
        SetFilters(isoYear, isoWeek, from, to, day, releaseId);
        return await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        if (!session.Permissions.Contains(CalendarAuthorization.Administer))
            return StatusCode(StatusCodes.Status404NotFound);
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        SetFilters(form["isoYear"], form["isoWeek"], form["from"], form["to"],
            form["day"], Guid.TryParse(form["releaseId"], out var selected) ? selected : null);
        var dayType = form["dayType"].ToString();
        var reason = form["reason"].ToString().Trim();
        var etag = form["etag"].ToString();
        if (!SelectedReleaseId.HasValue || !TryDate(Day, out var localDate) ||
            !TryDate(From, out var fromDate) || !TryDate(To, out var toDate) ||
            toDate < fromDate || localDate < fromDate || localDate > toDate ||
            dayType is not (CalendarContract.WorkingDay or CalendarContract.Holiday or CalendarContract.ExtraordinaryClosure) ||
            reason.Length == 0 || !Guid.TryParseExact(form["intentKey"], "D", out var intent) ||
            intent == Guid.Empty)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            DraftError = new("Revisa el día y el motivo", "Selecciona una fecha, un tipo y un motivo válidos.", null);
            return await LoadAsync(cancellationToken);
        }
        if (etag.Length > 0 && !ValidEtag(etag))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            IsConflict = true;
            DraftError = new("El borrador cambió; recárgalo antes de continuar",
                "No se guardaron tus cambios.", null);
            return await LoadAsync(cancellationToken);
        }

        ApiResponse<CalendarDayDetails>? changed;
        try
        {
            changed = await antiforgery.SendValidatedAsync<CalendarDayDetails>(HttpContext,
                new ApiRequest(HttpMethod.Put, $"/api/v1/calendar/{Day}", ApiResponseShape.Item,
                    new
                    {
                        releaseId = SelectedReleaseId.Value,
                        dayType,
                        isWorkingDay = dayType == CalendarContract.WorkingDay,
                        reason
                    },
                    string.IsNullOrEmpty(etag) ? null : etag, ApiMutationIntent.FromKey(intent)),
                cancellationToken);
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            DraftError = new("No se pudo completar la operación", "Consulta el borrador antes de volver a enviarla.", null);
            return await LoadAsync(cancellationToken);
        }
        if (changed is null)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            DraftError = new("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", null);
        }
        else if (changed.Status == StatusCodes.Status401Unauthorized) return Redirect("/acceso");
        else if (changed.Status == StatusCodes.Status412PreconditionFailed ||
            changed.Status == StatusCodes.Status400BadRequest &&
            changed.ErrorCode is "IF_MATCH_INVALIDO" or "IF_MATCH_REQUERIDO" ||
            changed.Status == StatusCodes.Status409Conflict && changed.ErrorCode == "CONFIGURACION_BORRADOR_REQUERIDA")
        {
            Response.StatusCode = changed.Status;
            IsConflict = true;
            DraftError = new("El borrador cambió; recárgalo antes de continuar",
                "No se guardaron tus cambios.", changed.CorrelationId);
        }
        else if (!changed.IsSuccess || changed.Data is null)
        {
            Response.StatusCode = changed.Status;
            DraftError = changed.Status == StatusCodes.Status403Forbidden
                ? new("No tienes permiso para editar el calendario", "No se guardó el día.", changed.CorrelationId)
                : changed.Error;
        }
        else
        {
            Success = string.IsNullOrEmpty(etag) ? "Día agregado al borrador" : "Día corregido en el borrador";
        }
        return await LoadAsync(cancellationToken);
    }

    private async Task<IActionResult> LoadAsync(CancellationToken cancellationToken)
    {
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return Redirect("/acceso");
        CanViewWeek = session.Permissions.Contains(WeekAuthorization.View);
        CanEditCalendar = session.Permissions.Contains(CalendarAuthorization.Administer);

        try
        {
            if (CanViewWeek)
            {
                if (int.TryParse(IsoYear, NumberStyles.None, CultureInfo.InvariantCulture, out var year) &&
                    int.TryParse(IsoWeek, NumberStyles.None, CultureInfo.InvariantCulture, out var week))
                {
                    var result = await apiClient.SendAsync<WeekPeriodDetails>(new ApiRequest(HttpMethod.Get,
                        $"/api/v1/weeks/{year}/{week}", ApiResponseShape.Item), cancellationToken);
                    if (result.Status == 401) return Redirect("/acceso");
                    if (result.IsSuccess && result.Data is { BranchCode: "LOR-001" }) Week = result.Data;
                    else
                    {
                        WeekError = result.Status == 403
                            ? new("No tienes permiso para consultar el período semanal", "No se muestran datos de la semana.", result.CorrelationId)
                            : new("Revisa el año y la semana ISO", "Selecciona una semana ISO válida.", result.CorrelationId);
                        Response.StatusCode = result.Status;
                    }
                }
                else
                {
                    WeekError = new("Revisa el año y la semana ISO", "Selecciona una semana ISO válida.", null);
                    Response.StatusCode = 400;
                }
            }

            if (!TryDate(From, out var fromDate) || !TryDate(To, out var toDate) || toDate < fromDate)
            {
                CalendarError = new("Revisa el rango de calendario", "Selecciona fechas válidas.", null);
                Response.StatusCode = 400;
            }
            else
            {
                var result = await apiClient.SendAsync<CalendarDayDetails>(new ApiRequest(HttpMethod.Get,
                    $"/api/v1/calendar?from={From}&to={To}", ApiResponseShape.Collection), cancellationToken);
                if (result.Status == 401) return Redirect("/acceso");
                if (result.IsSuccess) PublishedDays = result.Items ?? [];
                else
                {
                    CalendarError = result.Status == 403
                        ? new("No tienes permiso para consultar el calendario", "No se muestran días.", result.CorrelationId)
                        : result.Error;
                    Response.StatusCode = result.Status;
                }
            }

            if (CanEditCalendar)
            {
                var releases = await apiClient.SendAsync<ConfigurationReleaseDetails>(new ApiRequest(
                    HttpMethod.Get, "/api/v1/configuration/releases", ApiResponseShape.Collection), cancellationToken);
                if (releases.Status == 401) return Redirect("/acceso");
                if (releases.IsSuccess) DraftReleases = (releases.Items ?? [])
                    .Where(item => item.Status == "BORRADOR").ToArray();
                else
                {
                    DraftError ??= releases.Error;
                    Response.StatusCode = releases.Status;
                }
                if (SelectedReleaseId is null && DraftReleases.Count == 1)
                    SelectedReleaseId = DraftReleases[0].Id;
                if (SelectedReleaseId is { } releaseId &&
                    DraftReleases.Any(item => item.Id == releaseId) && CalendarError is null)
                {
                    var draft = await apiClient.SendAsync<CalendarDayDetails>(new ApiRequest(HttpMethod.Get,
                        $"/api/v1/calendar/drafts/{releaseId:D}?from={From}&to={To}", ApiResponseShape.Collection),
                        cancellationToken);
                    if (draft.Status == 401) return Redirect("/acceso");
                    if (draft.IsSuccess) DraftDays = draft.Items ?? [];
                    else
                    {
                        DraftError ??= draft.Error;
                        Response.StatusCode = draft.Status;
                    }
                }
            }
            return Page();
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            CalendarError ??= new("No se pudo cargar la planificación", "Vuelve a consultar más tarde.", null);
            return Page();
        }
    }

    private void SetFilters(string? yearText, string? weekText, string? fromText,
        string? toText, string? dayText, Guid? releaseId)
    {
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, MexicoCity).DateTime);
        var monday = localToday.AddDays(-(((int)localToday.DayOfWeek + 6) % 7));
        IsoYear = yearText ?? ISOWeek.GetYear(localToday.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture);
        IsoWeek = weekText ?? ISOWeek.GetWeekOfYear(localToday.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture);
        From = fromText ?? Iso(monday);
        To = toText ?? Iso(monday.AddDays(6));
        Day = dayText ?? Iso(localToday);
        SelectedReleaseId = releaseId;
    }

    private static bool TryDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    private static string Iso(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static bool ValidEtag(string value) => value.Length >= 3 &&
        value[0] == '"' && value[^1] == '"' &&
        long.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.None,
            CultureInfo.InvariantCulture, out var version) && version > 0;
    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
