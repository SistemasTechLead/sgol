using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Sgol.Web.Presentation.ApiClient;

internal static class ApiCookieBridge
{
    private const string Session = "__Host-SGOL-Session";
    private const string PreAuth = "__Host-SGOL-PreAuth";
    private const string Csrf = "__Host-SGOL-CSRF";

    internal static string? RequestCookie(HttpContext context, HttpMethod method, string path, bool mutation)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        if (!IsLogin(path) && path != "/api/v1/auth/csrf" &&
            path is not ("/api/v1/auth/mfa/enroll" or "/api/v1/auth/mfa/confirm" or "/api/v1/auth/mfa/verify"))
            allowed.Add(Session);
        if (method == HttpMethod.Post && PreAuthPath(path)) allowed.Add(PreAuth);
        if (mutation) allowed.Add(Csrf);

        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in context.Request.Headers.Cookie.ToString().Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) throw new ApiProtocolException();
            var name = part[..separator];
            if (!IsKnown(name)) throw new ApiProtocolException();
            if (!seen.Add(name) || part[(separator + 1)..].Length == 0 || part.Any(char.IsControl))
                throw new ApiProtocolException();
            if (allowed.Contains(name)) selected.Add(part);
        }
        if (mutation && !seen.Contains(Csrf)) throw new ApiProtocolException();
        return selected.Count == 0 ? null : string.Join("; ", selected);
    }

    internal static IReadOnlyList<string> ValidateResponseCookies(HttpResponseMessage response, HttpMethod method, string path)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers)) return [];
        var validated = new List<string>();
        var names = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            var parts = header.Split(';', StringSplitOptions.TrimEntries);
            var pair = parts[0];
            var separator = pair.IndexOf('=');
            if (separator <= 0 || header.Any(character => character is '\r' or '\n') || pair.Any(char.IsControl))
                throw new ApiProtocolException();
            var name = pair[..separator];
            if (!IsKnown(name) || !CanSet(name, method, path)) throw new ApiProtocolException();
            var attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in parts.Skip(1))
            {
                var equals = attribute.IndexOf('=');
                var key = equals < 0 ? attribute : attribute[..equals];
                var value = equals < 0 ? null : attribute[(equals + 1)..];
                if (key.Length == 0 || !attributes.TryAdd(key, value) ||
                    !key.Equals("Secure", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("HttpOnly", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("SameSite", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("Path", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("Expires", StringComparison.OrdinalIgnoreCase) &&
                    !key.Equals("Max-Age", StringComparison.OrdinalIgnoreCase)) throw new ApiProtocolException();
            }
            if (!attributes.TryGetValue("Secure", out var secure) || secure is not null ||
                !attributes.TryGetValue("HttpOnly", out var httpOnly) || httpOnly is not null ||
                !attributes.TryGetValue("SameSite", out var sameSite) || !string.Equals(sameSite, "Strict", StringComparison.OrdinalIgnoreCase) ||
                !attributes.TryGetValue("Path", out var cookiePath) || cookiePath != "/" ||
                attributes.ContainsKey("Domain")) throw new ApiProtocolException();
            if (attributes.TryGetValue("Expires", out var expires) &&
                !DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                throw new ApiProtocolException();
            if (attributes.TryGetValue("Max-Age", out var maxAge) &&
                (!long.TryParse(maxAge, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
                 seconds is < 0 or > 28800)) throw new ApiProtocolException();
            var deleted = pair[(separator + 1)..].Length == 0 &&
                (attributes.TryGetValue("Max-Age", out var age) && age == "0" ||
                 attributes.TryGetValue("Expires", out var date) &&
                 DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiration) &&
                 expiration <= DateTimeOffset.UtcNow);
            if (names.TryGetValue(name, out var previousDeleted))
            {
                // The hosted login deliberately deletes an old preauth cookie before issuing its replacement.
                if (name != PreAuth || !IsLogin(path) || !previousDeleted || deleted)
                    throw new ApiProtocolException();
                names[name] = false;
            }
            else names.Add(name, deleted);
            validated.Add(header);
        }
        return validated;
    }

    internal static void ApplyResponseCookies(HttpContext context, IReadOnlyList<string> validated)
    {
        foreach (var header in validated) context.Response.Headers.Append("Set-Cookie", header);
    }

    internal static void Clear(HttpContext context)
    {
        foreach (var name in new[] { Session, PreAuth, Csrf })
            context.Response.Cookies.Delete(name, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
    }

    internal static void ClearCsrf(HttpContext context) =>
        context.Response.Cookies.Delete(Csrf,
            new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });

    private static bool IsKnown(string name) => name is Session or PreAuth or Csrf;
    private static bool IsLogin(string path) => path == "/api/v1/auth/login";
    private static bool PreAuthPath(string path) => path is "/api/v1/auth/password/change" or
        "/api/v1/auth/mfa/enroll" or "/api/v1/auth/mfa/confirm" or "/api/v1/auth/mfa/verify" or
        "/api/v1/auth/recovery-codes/regenerate";
    private static bool CanSet(string name, HttpMethod method, string path) => name switch
    {
        Session => path != "/api/v1/auth/csrf",
        PreAuth => method == HttpMethod.Post && (IsLogin(path) || PreAuthPath(path) || path == "/api/v1/auth/logout"),
        Csrf => method == HttpMethod.Get && path == "/api/v1/auth/csrf",
        _ => false,
    };
}
