using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class AuthenticationApiEndpoints
{
    private static readonly TimeSpan RecentMfaLifetime = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapAuthenticationApi(this IEndpointRouteBuilder endpoints)
    {
        var authentication = endpoints.MapGroup("/api/v1/auth");
        authentication.MapGet("/csrf", GetCsrf);
        authentication.MapPost("/login", LoginAsync)
            .RequireRateLimiting(HostedAuthenticationDefaults.LoginRateLimitPolicy);
        authentication.MapPost("/password/change", ChangePasswordAsync);
        authentication.MapPost("/mfa/enroll", BeginMfaEnrollmentAsync);
        authentication.MapPost("/mfa/confirm", ConfirmMfaEnrollmentAsync);
        authentication.MapPost("/mfa/verify", VerifyMfaAsync);
        authentication.MapPost("/recovery-codes/regenerate", RegenerateRecoveryCodesAsync);
        authentication.MapGet("/session", GetSessionAsync).RequireAuthorization();
        authentication.MapPost("/logout", LogoutAsync);
        return endpoints;
    }

    private static IResult GetCsrf(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(Envelope(context, new
        {
            requestToken = tokens.RequestToken,
        }));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SignOutAsync(HostedAuthenticationDefaults.Scheme);
            PreAuthenticationCookieService.Delete(context);
            var result = await service.LoginAsync(
                request.UserName,
                request.Password,
                GetCorrelationId(context),
                cancellationToken);
            preAuthenticationCookie.Write(context, result.ChallengeId, result.ChallengeExpiresAt);
            return Results.Ok(Envelope(context, new
            {
                result.NextStep,
                result.ChallengeExpiresAt,
                nextPath = NextPath(result.NextStep),
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var authenticatedUserId = GetRecentlyAuthenticatedUserId(context);
            var result = await service.ChangePasswordAsync(
                authenticatedUserId is null ? preAuthenticationCookie.Read(context) : null,
                authenticatedUserId,
                request.CurrentPassword,
                request.NewPassword,
                GetCorrelationId(context),
                cancellationToken);
            await context.SignOutAsync(HostedAuthenticationDefaults.Scheme);
            preAuthenticationCookie.Write(context, result.ChallengeId, result.ChallengeExpiresAt);
            return Results.Ok(Envelope(context, new
            {
                result.NextStep,
                result.ChallengeExpiresAt,
                nextPath = NextPath(result.NextStep),
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> BeginMfaEnrollmentAsync(
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var challengeId = RequireChallenge(preAuthenticationCookie.Read(context));
            var result = await service.BeginMfaEnrollmentAsync(
                challengeId,
                GetCorrelationId(context),
                cancellationToken);
            return Results.Ok(Envelope(context, new
            {
                result.ManualKey,
                result.OtpAuthUri,
                result.ExpiresAt,
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> ConfirmMfaEnrollmentAsync(
        TotpRequest request,
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ConfirmMfaEnrollmentAsync(
                RequireChallenge(preAuthenticationCookie.Read(context)),
                request.TotpCode,
                GetCorrelationId(context),
                cancellationToken);
            await IssueSessionAsync(context, result.Session);
            PreAuthenticationCookieService.Delete(context);
            return Results.Ok(Envelope(context, new
            {
                result.NextStep,
                recoveryCodes = result.RecoveryCodes,
                session = ToSessionData(result.Session),
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> VerifyMfaAsync(
        VerifyMfaRequest request,
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.VerifyMfaAsync(
                RequireChallenge(preAuthenticationCookie.Read(context)),
                request.TotpCode,
                request.RecoveryCode,
                GetCorrelationId(context),
                cancellationToken);
            if (result.NextStep == AuthenticationNextStep.Authenticated)
            {
                await IssueSessionAsync(context, result.Session);
                PreAuthenticationCookieService.Delete(context);
            }

            return Results.Ok(Envelope(context, new
            {
                result.NextStep,
                session = result.NextStep == AuthenticationNextStep.Authenticated
                    ? ToSessionData(result.Session)
                    : null,
                nextPath = NextPath(result.NextStep),
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(
        RegenerateRecoveryCodesRequest request,
        HttpContext context,
        IHostedAuthenticationService service,
        PreAuthenticationCookieService preAuthenticationCookie,
        CancellationToken cancellationToken)
    {
        try
        {
            var authenticatedUserId = GetRecentlyAuthenticatedUserId(context);
            var result = await service.RegenerateRecoveryCodesAsync(
                authenticatedUserId is null ? preAuthenticationCookie.Read(context) : null,
                authenticatedUserId,
                request.CurrentPassword,
                GetCorrelationId(context),
                cancellationToken);
            await IssueSessionAsync(context, result.Session);
            PreAuthenticationCookieService.Delete(context);
            return Results.Ok(Envelope(context, new
            {
                result.NextStep,
                recoveryCodes = result.RecoveryCodes,
                session = ToSessionData(result.Session),
            }));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> GetSessionAsync(
        HttpContext context,
        IHostedAuthenticationService service,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationPrincipalFactory.TryReadSessionClaims(
                context.User,
                out var userId,
                out var securityStamp,
                out var mfaAuthenticatedAt,
                out var absoluteExpiresAt))
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
        }

        try
        {
            var session = await service.GetSessionAsync(
                userId,
                securityStamp,
                mfaAuthenticatedAt,
                absoluteExpiresAt,
                cancellationToken);
            var authentication = await context.AuthenticateAsync(HostedAuthenticationDefaults.Scheme);
            if (authentication.Properties?.ExpiresUtc is DateTimeOffset ticketExpiresAt)
            {
                session = session with
                {
                    IdleExpiresAt = Minimum(ticketExpiresAt, session.AbsoluteExpiresAt),
                };
            }
            return Results.Ok(Envelope(context, session));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IHostedAuthenticationService service,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = AuthenticationPrincipalFactory.TryReadSessionClaims(
            context.User,
            out var authenticatedUserId,
            out _,
            out _,
            out _)
            ? authenticatedUserId
            : (Guid?)null;
        await service.RecordLogoutAsync(userId, GetCorrelationId(context), cancellationToken);
        await context.SignOutAsync(HostedAuthenticationDefaults.Scheme);
        PreAuthenticationCookieService.Delete(context);
        return Results.NoContent();
    }

    private static async Task IssueSessionAsync(HttpContext context, AuthenticatedSession session)
    {
        var idleExpiration = DateTimeOffset.UtcNow.Add(HostedAuthenticationDefaults.IdleLifetime);
        if (idleExpiration > session.AbsoluteExpiresAt)
        {
            idleExpiration = session.AbsoluteExpiresAt;
        }

        await context.SignInAsync(
            HostedAuthenticationDefaults.Scheme,
            AuthenticationPrincipalFactory.Create(session),
            new AuthenticationProperties
            {
                AllowRefresh = true,
                ExpiresUtc = idleExpiration,
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow,
            });
    }

    private static Guid RequireChallenge(Guid? challengeId) =>
        challengeId ?? throw new AuthenticationChallengeInvalidException();

    private static Guid? GetRecentlyAuthenticatedUserId(HttpContext context)
    {
        if (!AuthenticationPrincipalFactory.TryReadSessionClaims(
                context.User,
                out var userId,
                out _,
                out var mfaAuthenticatedAt,
                out _))
        {
            return null;
        }

        return DateTimeOffset.UtcNow - mfaAuthenticatedAt <= RecentMfaLifetime
            ? userId
            : null;
    }

    private static object ToSessionData(AuthenticatedSession session) => new
    {
        session.UserId,
        session.PersonId,
        session.UserName,
        session.DisplayName,
        branchCode = "LOR-001",
        session.RoleCode,
        session.Permissions,
        session.MfaAuthenticatedAt,
        idleExpiresAt = Minimum(
            DateTimeOffset.UtcNow.Add(HostedAuthenticationDefaults.IdleLifetime),
            session.AbsoluteExpiresAt),
        session.AbsoluteExpiresAt,
    };

    private static DateTimeOffset Minimum(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;

    private static string? NextPath(string nextStep) => nextStep switch
    {
        AuthenticationNextStep.ChangePassword => "/api/v1/auth/password/change",
        AuthenticationNextStep.EnrollMfa => "/api/v1/auth/mfa/enroll",
        AuthenticationNextStep.VerifyMfa => "/api/v1/auth/mfa/verify",
        AuthenticationNextStep.RegenerateRecoveryCodes => "/api/v1/auth/recovery-codes/regenerate",
        AuthenticationNextStep.Authenticated => "/api/v1/auth/session",
        _ => null,
    };

    private static IResult MapException(HttpContext context, Exception exception)
    {
        if (exception is AuthenticationAccountLockedException locked)
        {
            context.Response.Headers.RetryAfter = Math.Max(1, (long)(locked.RetryAt - DateTimeOffset.UtcNow).TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
            return Problem(context, 423, "ACCOUNT_LOCKED", "La cuenta está bloqueada temporalmente", new Dictionary<string, object?>
            {
                ["retryAt"] = locked.RetryAt,
            });
        }

        return exception switch
        {
            AuthenticationFailedException => Problem(context, 401, "AUTHENTICATION_FAILED", "No fue posible autenticar la solicitud"),
            AuthenticationChallengeInvalidException => Problem(context, 401, "DESAFIO_INVALIDO", "El desafío de autenticación es inválido o expiró"),
            AuthenticationMfaCodeInvalidException => Problem(context, 401, "CODIGO_MFA_INVALIDO", "El código de autenticación no es válido"),
            AuthenticationMfaStateInconsistentException => Problem(context, 409, "MFA_STATE_INCONSISTENT", "El estado MFA requiere recuperación administrada"),
            AuthenticationPasswordPolicyException => Problem(context, 400, "PASSWORD_NO_CUMPLE_POLITICA", exception.Message),
            AuthenticationValidationException => Problem(context, 400, "DATOS_AUTENTICACION_INVALIDOS", exception.Message),
            AuthenticationSessionInvalidException => Problem(context, 401, "SESSION_INVALID", "La sesión ya no es válida"),
            _ => throw exception,
        };
    }

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static object Envelope(HttpContext context, object data) => new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId() },
    };

    private static IResult Problem(
        HttpContext context,
        int status,
        string code,
        string title,
        IDictionary<string, object?>? additionalExtensions = null)
    {
        var extensions = additionalExtensions is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(additionalExtensions);
        extensions["code"] = code;
        extensions["correlationId"] = context.GetCorrelationId();
        return Results.Problem(statusCode: status, title: title, extensions: extensions);
    }
}

public sealed class LoginRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public override string ToString() => $"{nameof(LoginRequest)} {{ UserName = {UserName}, Password = [REDACTED] }}";
}

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
    public override string ToString() => $"{nameof(ChangePasswordRequest)} {{ CurrentPassword = [REDACTED], NewPassword = [REDACTED] }}";
}

public sealed class TotpRequest
{
    public required string TotpCode { get; init; }
    public override string ToString() => $"{nameof(TotpRequest)} {{ TotpCode = [REDACTED] }}";
}

public sealed class VerifyMfaRequest
{
    public string? TotpCode { get; init; }
    public string? RecoveryCode { get; init; }
    public override string ToString() => $"{nameof(VerifyMfaRequest)} {{ TotpCode = [REDACTED], RecoveryCode = [REDACTED] }}";
}

public sealed class RegenerateRecoveryCodesRequest
{
    public required string CurrentPassword { get; init; }
    public override string ToString() => $"{nameof(RegenerateRecoveryCodesRequest)} {{ CurrentPassword = [REDACTED] }}";
}
