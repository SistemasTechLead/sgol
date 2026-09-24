using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.Navigation;

public enum AccessNoticeKind { Closed, Ended, Unconfirmed }

public sealed class AccessNotice(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector protector = provider
        .CreateProtector("SGOL.Frontend.AccessNotice.v1")
        .ToTimeLimitedDataProtector();

    public string Protect(AccessNoticeKind kind) => protector.Protect(kind.ToString(), TimeSpan.FromMinutes(2));

    public AccessNoticeKind? Read(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) return null;
        try
        {
            return protector.Unprotect(value) switch
            {
                nameof(AccessNoticeKind.Closed) => AccessNoticeKind.Closed,
                nameof(AccessNoticeKind.Ended) => AccessNoticeKind.Ended,
                nameof(AccessNoticeKind.Unconfirmed) => AccessNoticeKind.Unconfirmed,
                _ => null,
            };
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
        catch (Exception exception) when (exception.GetType().Name == "PayloadExpiredException") { return null; }
    }

    public static string Message(AccessNoticeKind kind) => kind switch
    {
        AccessNoticeKind.Closed => "Sesión cerrada.",
        AccessNoticeKind.Ended => "Tu sesión terminó. Inicia sesión nuevamente.",
        AccessNoticeKind.Unconfirmed => "La sesión se cerró en este dispositivo, pero no se pudo confirmar el cierre en el servidor.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
