using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.Navigation;

public sealed class SafeReturnDestination(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector protector = provider
        .CreateProtector("SGOL.Frontend.ReturnDestination.v1")
        .ToTimeLimitedDataProtector();

    // FRONT-002 implements only the shell host, without any UI-E child route.
    private static readonly IReadOnlySet<string> ImplementedRoutes = new HashSet<string>(["/mi-trabajo", "/validaciones", "/indicadores", "/auditoria", "/continuidad"], StringComparer.Ordinal);

    public string? Protect(string? destination, IReadOnlySet<string>? implementedRoutes = null)
    {
        if (!IsAllowed(destination, implementedRoutes ?? ImplementedRoutes)) return null;
        return protector.Protect(destination!, TimeSpan.FromMinutes(30));
    }

    public string? Read(string? protectedValue, IReadOnlySet<string>? implementedRoutes = null,
        Func<string, bool>? authorizedForCurrentPrincipal = null)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return null;
        try
        {
            var destination = protector.Unprotect(protectedValue);
            return IsAllowed(destination, implementedRoutes ?? ImplementedRoutes) &&
                authorizedForCurrentPrincipal?.Invoke(destination) == true ? destination : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
        catch (Exception exception) when (exception.GetType().Name == "PayloadExpiredException")
        {
            return null;
        }
    }

    private static bool IsAllowed(string? destination, IReadOnlySet<string> routes)
    {
        if (destination is not { Length: > 1 and < 60000 } || destination[0] != '/' || destination.StartsWith("//", StringComparison.Ordinal) ||
            destination.Any(char.IsControl) || destination.IndexOfAny(['\\', '#']) >= 0) return false;
        var parts = destination.Split('?', 2); var path = parts[0];
        if (path.Contains('%', StringComparison.Ordinal)) return false;
        if (routes.Contains(path))
        {
            if (parts.Length == 1) return true;
            var query = new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parts[1]));
            return path switch
            {
                "/indicadores" => Sgol.Web.Presentation.Reporting.IndicatorQuery.TryRead(query, out _),
                "/auditoria" => Sgol.Web.Presentation.Auditing.AuditQuery.TryRead(query, out _),
                _ => false
            };
        }
        if (parts.Length != 1) return false;
        foreach (var prefix in new[] { "/auditoria/eventos/", "/continuidad/reconciliaciones/" })
            if (routes.Contains(prefix.StartsWith("/auditoria", StringComparison.Ordinal) ? "/auditoria" : "/continuidad") && path.StartsWith(prefix, StringComparison.Ordinal))
                return Sgol.Web.Presentation.Reporting.IndicatorQuery.CanonicalId(path[prefix.Length..]);
        return false;
    }
}
