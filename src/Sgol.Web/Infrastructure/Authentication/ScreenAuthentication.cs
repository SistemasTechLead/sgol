using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Identity;

namespace Sgol.Web.Infrastructure.Authentication;

public static class ScreenAuthentication
{
    public const string Scheme = "SGOL.Screen";
    public const string Cookie = "__Host-SGOL-Screen";
    public const string Role = "PANTALLA";
    public const string RateLimit = "screen-device";
    public const string Stamp = "sgol:screen_stamp";
    public const string Expires = "sgol:screen_expires";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);
    public static bool IsScreen(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && user.Identity.AuthenticationType == Scheme;
    public static bool IsBoard(PathString path) => path.Equals("/tablero", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/tablero/rol-de-tareas", StringComparison.OrdinalIgnoreCase);

    public static ClaimsPrincipal Principal(ScreenAccount account, DateTimeOffset expires) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, account.Id.ToString("D")),
        new Claim(ClaimTypes.Name, account.DisplayName),
        new Claim("sgol:screen_code", account.Code),
        new Claim(Stamp, account.SessionStamp!.Value.ToString("D")),
        new Claim(Expires, expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
    }, Scheme));

    public static SessionSnapshot Snapshot(ClaimsPrincipal principal)
    {
        var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirstValue(Expires)!, CultureInfo.InvariantCulture));
        return new(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), Guid.Empty,
            principal.FindFirstValue("sgol:screen_code")!, principal.Identity!.Name!, BranchScope.LorettaCode,
            Role, ["PER-TABLERO-VER"], expires - Lifetime, expires, expires);
    }
}

public sealed class ScreenCookieEvents(ScreenDeviceService devices) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!ScreenAuthentication.IsScreen(context.Principal!) ||
            !Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !Guid.TryParse(context.Principal?.FindFirstValue(ScreenAuthentication.Stamp), out var stamp) ||
            await devices.ValidateAsync(id, stamp, context.HttpContext.RequestAborted) is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(ScreenAuthentication.Scheme);
        }
    }
}

// Enforce the screen's read-only boundary for every dynamic URL, including APIs,
// alternate Razor routes and POST handlers. Normal user authentication is untouched.
public sealed class ScreenAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Cookies.ContainsKey(ScreenAuthentication.Cookie)) { await next(context); return; }
        var auth = await context.AuthenticateAsync(ScreenAuthentication.Scheme);
        var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        var board = ScreenAuthentication.IsBoard(context.Request.Path);
        var entry = context.Request.Path.Equals("/pantalla", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Equals("/pantalla/entrar", StringComparison.OrdinalIgnoreCase);
        if (!entry && (!read || !board))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (auth.Succeeded) context.User = auth.Principal!;
        else if (board)
        {
            context.Response.Redirect("/pantalla");
            return;
        }
        else context.User = new ClaimsPrincipal(new ClaimsIdentity());
        context.Response.Headers.CacheControl = "no-store";
        await next(context);
    }
}
