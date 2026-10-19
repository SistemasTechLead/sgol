using Microsoft.AspNetCore.Authentication;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence.Identity;

namespace Sgol.Web.Presentation.Endpoints;

public static class ScreenDeviceEndpoints
{
    public static void MapScreenDeviceApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/screens/link", async (LinkInput input, ScreenDeviceService devices, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                var request = await devices.RequestLinkAsync(input.Code, input.CredentialHash, input.DeviceName, context.RequestAborted);
                return Results.Ok(new { request.ScreenId, request.ApprovalCode, request.DeviceName, request.ExpiresAt });
            }
            catch (ScreenDeviceException ex) { return Error(ex); }
        }).RequireRateLimiting(ScreenAuthentication.RateLimit);

        app.MapPost("/api/v1/screens/ticket", async (HttpContext context, ScreenDeviceService devices) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var header = context.Request.Headers.Authorization.ToString();
            var parts = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..].Split('.', 2) : [];
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var id)) return Results.Unauthorized();
            try
            {
                var ticket = await devices.IssueTicketAsync(id, parts[1], context.RequestAborted);
                return ticket is null ? Results.StatusCode(202) : Results.Ok(new { ticket });
            }
            catch (ScreenDeviceException ex) { return Error(ex); }
        }).RequireRateLimiting(ScreenAuthentication.RateLimit);

        // The one-use ticket is the login proof here. This endpoint does not use
        // person cookies or the normal login form's antiforgery contract.
        app.MapPost("/pantalla/entrar", async (HttpContext context, ScreenDeviceService devices) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (context.Request.Headers.Origin != "http://127.0.0.1:17841" ||
                !context.Request.HasFormContentType || context.Request.ContentLength is > 2048)
                return Results.StatusCode(403);
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            try
            {
                var account = await devices.RedeemAsync(form["ticket"].ToString(), context.RequestAborted);
                var expires = DateTimeOffset.UtcNow.Add(ScreenAuthentication.Lifetime);
                await context.SignOutAsync(HostedAuthenticationDefaults.Scheme);
                await context.SignInAsync(ScreenAuthentication.Scheme, ScreenAuthentication.Principal(account, expires),
                    new AuthenticationProperties { IsPersistent = false, IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = expires });
                return Results.Redirect("/tablero");
            }
            catch (ScreenDeviceException) { return Results.Redirect("/pantalla"); }
        }).RequireRateLimiting(ScreenAuthentication.RateLimit);
    }

    private static IResult Error(ScreenDeviceException ex) => Results.Json(new { code = ex.Message }, statusCode: ex.Status);
    public sealed record LinkInput(string Code, string CredentialHash, string DeviceName);
}
