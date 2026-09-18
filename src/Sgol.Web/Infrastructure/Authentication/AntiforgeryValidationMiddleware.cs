using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Sgol.Web.Infrastructure.Http;
using System.Text.Json;

namespace Sgol.Web.Infrastructure.Authentication;

public sealed class AntiforgeryValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (IsAuthenticationSurface(context.Request.Path))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Pragma = "no-cache";
                return Task.CompletedTask;
            });
        }

        if (RequiresValidation(context))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "La protección CSRF es inválida",
                        Instance = context.Request.Path,
                        Extensions =
                        {
                            ["code"] = "CSRF_INVALID",
                            ["correlationId"] = context.GetCorrelationId(),
                        },
                    });
                return;
            }

            if (!await HasApprovedJsonShapeAsync(context.Request, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Los datos de autenticación no son válidos",
                        Instance = context.Request.Path,
                        Extensions =
                        {
                            ["code"] = "DATOS_AUTENTICACION_INVALIDOS",
                            ["correlationId"] = context.GetCorrelationId(),
                        },
                    },
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) ||
            HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            HttpMethods.IsTrace(request.Method))
        {
            return false;
        }

        return ExpectedProperties(request.Path) is not null ||
            context.User.Identity?.IsAuthenticated == true;
    }

    private static async Task<bool> HasApprovedJsonShapeAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var expected = ExpectedProperties(request.Path);
        if (expected is null)
        {
            return true;
        }

        if (request.ContentLength == 0 && expected.Count == 0)
        {
            return true;
        }

        request.EnableBuffering();
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String ||
                    !names.Add(property.Name) ||
                    !expected.Contains(property.Name))
                {
                    return false;
                }
            }

            return string.Equals(request.Path.Value, "/api/v1/auth/mfa/verify", StringComparison.OrdinalIgnoreCase)
                ? names.Count == 1 && expected.IsSupersetOf(names)
                : names.SetEquals(expected);
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static HashSet<string>? ExpectedProperties(PathString path)
    {
        var value = path.Value;
        if (string.Equals(value, "/api/v1/auth/login", StringComparison.OrdinalIgnoreCase))
            return ["userName", "password"];
        if (string.Equals(value, "/api/v1/auth/password/change", StringComparison.OrdinalIgnoreCase))
            return ["currentPassword", "newPassword"];
        if (string.Equals(value, "/api/v1/auth/mfa/enroll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase))
            return [];
        if (string.Equals(value, "/api/v1/auth/mfa/confirm", StringComparison.OrdinalIgnoreCase))
            return ["totpCode"];
        if (string.Equals(value, "/api/v1/auth/mfa/verify", StringComparison.OrdinalIgnoreCase))
            return ["totpCode", "recoveryCode"];
        if (string.Equals(value, "/api/v1/auth/recovery-codes/regenerate", StringComparison.OrdinalIgnoreCase))
            return ["currentPassword"];
        if (value is not null && value.StartsWith("/api/v1/users/", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith("/mfa-reset", StringComparison.OrdinalIgnoreCase))
            return ["reason", "temporaryPassword"];
        return null;
    }

    private static bool IsAuthenticationSurface(PathString path) =>
        path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase) ||
        path.Value is { } value &&
        value.StartsWith("/api/v1/users/", StringComparison.OrdinalIgnoreCase) &&
        value.EndsWith("/mfa-reset", StringComparison.OrdinalIgnoreCase);
}
