using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05ScenarioEngine
{
    private async Task VerifyHostedAuthenticationFailuresAsync(CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var before = await context.GenerationRequests.AsNoTracking().CountAsync(token);
        var body = GenerationBody("CV05-AUTH-NEGATIVE");

        using (var noCookie = infrastructure.CreateClient())
        using (var denied = await HostedAuthenticationClient.PostAsync(noCookie,
            "/api/v1/generation-requests", body, await HostedAuthenticationClient.GetCsrfAsync(noCookie, token),
            token, Guid.CreateVersion7()))
            Require(denied.StatusCode == HttpStatusCode.Unauthorized);

        using (var tampered = infrastructure.CreateClient())
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/generation-requests")
            {
                Content = System.Net.Http.Json.JsonContent.Create(body),
            };
            request.Headers.Add("Cookie", "__Host-SGOL=invalid.synthetic.cookie");
            request.Headers.Add("X-CSRF-TOKEN", await HostedAuthenticationClient.GetCsrfAsync(tampered, token));
            request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
            using var denied = await tampered.SendAsync(request, token);
            Require(denied.StatusCode == HttpStatusCode.Unauthorized);
        }

        using (var missingCsrf = await HostedAuthenticationClient.PostAsync(administration.Client,
            "/api/v1/generation-requests", body, null, token, Guid.CreateVersion7()))
        {
            Require(missingCsrf.StatusCode == HttpStatusCode.BadRequest);
            Require(await HostedAuthenticationClient.ProblemCodeAsync(missingCsrf, token) == "CSRF_INVALID");
        }
        using (var invalidCsrf = await HostedAuthenticationClient.PostAsync(administration.Client,
            "/api/v1/generation-requests", body, "invalid-cv05", token, Guid.CreateVersion7()))
        {
            Require(invalidCsrf.StatusCode == HttpStatusCode.BadRequest);
            Require(await HostedAuthenticationClient.ProblemCodeAsync(invalidCsrf, token) == "CSRF_INVALID");
        }

        var temporaryPassword = Cv05Infrastructure.NewPassword();
        using (var reset = await HostedAuthenticationClient.PostAsync(direction.Client,
            $"/api/v1/users/{administration.Account.UserId:D}/mfa-reset",
            new { reason = "CV05 sesión invalidada sintética", temporaryPassword }, direction.Csrf,
            token, Guid.CreateVersion7()))
            Require(reset.StatusCode == HttpStatusCode.OK);

        using (var revoked = await HostedAuthenticationClient.PostAsync(administration.Client,
            "/api/v1/generation-requests", body, administration.Csrf, token, Guid.CreateVersion7()))
            Require(revoked.StatusCode == HttpStatusCode.Unauthorized);

        using (var pending = infrastructure.CreateClient())
        {
            var csrf = await HostedAuthenticationClient.GetCsrfAsync(pending, token);
            using (var login = await HostedAuthenticationClient.PostAsync(pending, "/api/v1/auth/login",
                new { userName = administration.Account.UserName, password = temporaryPassword }, csrf, token))
                Require(login.StatusCode == HttpStatusCode.OK);
            using (var change = await HostedAuthenticationClient.PostAsync(pending,
                "/api/v1/auth/password/change",
                new { currentPassword = temporaryPassword, newPassword = Cv05Infrastructure.NewPassword() },
                csrf, token))
                Require(change.StatusCode == HttpStatusCode.OK);
            using var mfaPending = await HostedAuthenticationClient.PostAsync(pending,
                "/api/v1/generation-requests", body, csrf, token, Guid.CreateVersion7());
            Require(mfaPending.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        }
        Require(await context.GenerationRequests.AsNoTracking().CountAsync(token) == before);
    }
}
