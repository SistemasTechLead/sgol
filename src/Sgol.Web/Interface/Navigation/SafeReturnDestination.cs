using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.Navigation;

public sealed class SafeReturnDestination(IDataProtectionProvider provider)
{
    private readonly ITimeLimitedDataProtector protector = provider
        .CreateProtector("SGOL.Frontend.ReturnDestination.v1")
        .ToTimeLimitedDataProtector();

    // FRONT-002 implements only the shell host, without any UI-E child route.
    private static readonly IReadOnlySet<string> ImplementedRoutes = new HashSet<string>(["/mi-trabajo"], StringComparer.Ordinal);

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

    private static bool IsAllowed(string? destination, IReadOnlySet<string> routes) =>
        destination is { Length: > 1 and < 256 } &&
        destination[0] == '/' && !destination.StartsWith("//", StringComparison.Ordinal) &&
        !destination.Any(char.IsControl) &&
        destination.IndexOfAny(['\\', '%', '?', '#']) < 0 &&
        routes.Contains(destination);
}
