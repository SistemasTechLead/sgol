using System.Globalization;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Navigation;

public static class SessionPresentation
{
    private static readonly TimeZoneInfo OperatingZone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

    public static string? RoleName(string roleCode) => roleCode switch
    {
        "DIRECCION" => "Dirección",
        "ADMINISTRACION" => "Administración",
        "SUBCOORDINACION" => "Subcoordinación",
        "PISO_VENTAS" => "Piso de ventas",
        _ => null,
    };

    public static DateTimeOffset EffectiveExpiration(SessionSnapshot session) =>
        session.IdleExpiresAt <= session.AbsoluteExpiresAt ? session.IdleExpiresAt : session.AbsoluteExpiresAt;

    public static string ExpirationText(SessionSnapshot session, DateTimeOffset now)
    {
        var localExpiration = TimeZoneInfo.ConvertTime(EffectiveExpiration(session), OperatingZone);
        var localToday = TimeZoneInfo.ConvertTime(now, OperatingZone);
        return localExpiration.Date == localToday.Date
            ? localExpiration.ToString("HH:mm", SpanishMexico)
            : localExpiration.ToString("dd MMM yyyy, HH:mm", SpanishMexico);
    }
}
