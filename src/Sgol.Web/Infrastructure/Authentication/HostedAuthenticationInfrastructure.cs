using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Infrastructure.Authentication;

public static class HostedAuthenticationDefaults
{
    public const string Scheme = "SGOL.Cookie";
    public const string SessionCookie = "__Host-SGOL-Session";
    public const string PreAuthenticationCookie = "__Host-SGOL-PreAuth";
    public const string LoginRateLimitPolicy = "authentication-login";
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);
    internal const string RejectedSessionItem = "SGOL.Authentication.RejectedSession";
}

public static class SgolClaimTypes
{
    public const string SecurityStamp = "sgol:security_stamp";
    public const string RoleCode = "sgol:role";
    public const string MfaAuthenticatedAt = "sgol:mfa_at";
    public const string AbsoluteExpiresAt = "sgol:absolute_expires_at";
    public const string AuthenticationMethod = "amr";
}

public sealed class PreAuthenticationCookieService(IDataProtectionProvider dataProtectionProvider)
{
    private readonly ITimeLimitedDataProtector protector = dataProtectionProvider
        .CreateProtector("SGOL.Authentication.PreAuth.v1")
        .ToTimeLimitedDataProtector();

    public void Write(HttpContext context, Guid challengeId, DateTimeOffset expiresAt)
    {
        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            throw new AuthenticationChallengeInvalidException();
        }

        var value = protector.Protect(challengeId.ToString("D", CultureInfo.InvariantCulture), lifetime);
        context.Response.Cookies.Append(
            HostedAuthenticationDefaults.PreAuthenticationCookie,
            value,
            BuildOptions(expiresAt));
    }

    public Guid? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(HostedAuthenticationDefaults.PreAuthenticationCookie, out var value))
        {
            return null;
        }

        try
        {
            return Guid.TryParse(protector.Unprotect(value), out var challengeId)
                ? challengeId
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }

        catch (Exception exception) when (exception.GetType().Name == "PayloadExpiredException")
        {
            return null;
        }
    }

    public static void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(
            HostedAuthenticationDefaults.PreAuthenticationCookie,
            BuildOptions(expiresAt: null));

    private static CookieOptions BuildOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAt,
        IsEssential = true,
    };
}

internal sealed class HostedCookieEvents(
    IHostedAuthenticationService authenticationService,
    AuthenticationTelemetry telemetry)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!AuthenticationPrincipalFactory.TryReadSessionClaims(
                context.Principal,
                out var userId,
                out var securityStamp,
                out var mfaAuthenticatedAt,
                out var absoluteExpiresAt))
        {
            await RejectAsync(context);
            return;
        }

        var session = await authenticationService.ValidateSessionAsync(
            userId,
            securityStamp,
            mfaAuthenticatedAt,
            absoluteExpiresAt,
            context.HttpContext.RequestAborted);
        if (session is null)
        {
            await RejectAsync(context);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        context.HttpContext.Items.ContainsKey(HostedAuthenticationDefaults.RejectedSessionItem)
            ? WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "SESSION_INVALID", "La sesión ya no es válida")
            : WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "ACCESO_DENEGADO", "La sesión no autoriza esta operación");

    private async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        telemetry.SessionRejected();
        context.HttpContext.Items[HostedAuthenticationDefaults.RejectedSessionItem] = true;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(HostedAuthenticationDefaults.Scheme);
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = context.Request.Path,
                Extensions =
                {
                    ["code"] = code,
                    ["correlationId"] = context.GetCorrelationId(),
                },
            });
    }
}

public static class AuthenticationPrincipalFactory
{
    public static ClaimsPrincipal Create(AuthenticatedSession session)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString("D", CultureInfo.InvariantCulture)),
            new Claim(SgolClaimTypes.SecurityStamp, session.SecurityStamp),
            new Claim(SgolClaimTypes.RoleCode, session.RoleCode),
            new Claim(SgolClaimTypes.AuthenticationMethod, "mfa"),
            new Claim(SgolClaimTypes.MfaAuthenticatedAt, session.MfaAuthenticatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim(SgolClaimTypes.AbsoluteExpiresAt, session.AbsoluteExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, HostedAuthenticationDefaults.Scheme));
    }

    public static bool TryReadSessionClaims(
        ClaimsPrincipal? principal,
        out Guid userId,
        out string securityStamp,
        out DateTimeOffset mfaAuthenticatedAt,
        out DateTimeOffset absoluteExpiresAt)
    {
        userId = default;
        securityStamp = principal?.FindFirstValue(SgolClaimTypes.SecurityStamp) ?? string.Empty;
        mfaAuthenticatedAt = default;
        absoluteExpiresAt = default;
        return principal?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId) &&
            !string.IsNullOrWhiteSpace(securityStamp) &&
            TryReadUnixTime(principal.FindFirstValue(SgolClaimTypes.MfaAuthenticatedAt), out mfaAuthenticatedAt) &&
            TryReadUnixTime(principal.FindFirstValue(SgolClaimTypes.AbsoluteExpiresAt), out absoluteExpiresAt);
    }

    private static bool TryReadUnixTime(string? value, out DateTimeOffset instant)
    {
        instant = default;
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        try
        {
            instant = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

public static class HostedAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddSgolHostedAuthentication(this IServiceCollection services)
    {
        services.AddScoped<HostedCookieEvents>();
        services.AddSingleton<PreAuthenticationCookieService>();
        services.AddAuthentication(HostedAuthenticationDefaults.Scheme)
            .AddCookie(HostedAuthenticationDefaults.Scheme, options =>
            {
                options.Cookie.Name = HostedAuthenticationDefaults.SessionCookie;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = HostedAuthenticationDefaults.IdleLifetime;
                options.SlidingExpiration = true;
                options.EventsType = typeof(HostedCookieEvents);
            });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(HostedAuthenticationDefaults.LoginRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.RequestServices.GetRequiredService<AuthenticationTelemetry>().RateLimited();
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.Headers.Pragma = "no-cache";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (long)retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Se excedió el límite temporal de autenticación",
                        Instance = context.HttpContext.Request.Path,
                        Extensions =
                        {
                            ["code"] = "RATE_LIMITED",
                            ["correlationId"] = context.HttpContext.GetCorrelationId(),
                        },
                    },
                    cancellationToken);
            };
        });
        return services;
    }
}
