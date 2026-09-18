using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Xunit;

namespace Sgol.UnitTests;

public sealed class HostedAuthenticationTests
{
    private static readonly Guid UserId = Guid.Parse("0199f189-2a16-7191-81c7-8c40f74ff001");
    private static readonly Guid PersonId = Guid.Parse("0199f189-2a16-7191-81c7-8c40f74ff002");

    [Fact]
    public void TotpAcceptsRfc6238Sha1VectorOnce()
    {
        const string rfc6238VectorHalf = "GEZDGNBVGY3TQOJQ";
        var secret = string.Concat(rfc6238VectorHalf, rfc6238VectorHalf);
        var instant = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.True(TotpCodes.TryValidate(secret, "287082", instant, null, out var acceptedStep));
        Assert.False(TotpCodes.TryValidate(secret, "287082", instant, acceptedStep, out _));
    }

    [Fact]
    public async Task HostedFlowIssuesHardenedCookieAndMaintainsSession()
    {
        await using var factory = CreateFactory(new RecordingAuthenticationService());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        var csrf = await GetCsrfAsync(client);
        using var login = await PostAsync(client, "/api/v1/auth/login", new
        {
            userName = "direction.synthetic",
            password = "Temporary-Password-01!",
        }, csrf);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(HostedAuthenticationDefaults.PreAuthenticationCookie, StringComparison.Ordinal));

        using var confirm = await PostAsync(client, "/api/v1/auth/mfa/confirm", new
        {
            totpCode = "287082",
        }, csrf);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var sessionCookie = Assert.Single(confirm.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(HostedAuthenticationDefaults.SessionCookie, StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", sessionCookie, StringComparison.OrdinalIgnoreCase);

        using var session = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        var authenticatedCsrf = await GetCsrfAsync(client);
        using var logout = await PostAsync(client, "/api/v1/auth/logout", new { }, authenticatedCsrf);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var afterLogout = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task LoginWithoutCsrfIsRejectedBeforeCredentialsAreEvaluated()
    {
        var service = new RecordingAuthenticationService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userName = "direction.synthetic",
            password = "Temporary-Password-01!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, service.LoginCalls);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CSRF_INVALID", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthenticationBodyRejectsUnknownPropertiesAndLogoutIsIdempotent()
    {
        var service = new RecordingAuthenticationService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        var csrf = await GetCsrfAsync(client);

        using var invalid = await PostAsync(client, "/api/v1/auth/login", new
        {
            userName = "direction.synthetic",
            password = "not-logged",
            unexpected = true,
        }, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, service.LoginCalls);

        using var logout = await PostAsync(client, "/api/v1/auth/logout", new { }, csrf);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("no-store", logout.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task LoginRateLimitRejectsEleventhRequestWithoutCallingService()
    {
        var service = new RecordingAuthenticationService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        var csrf = await GetCsrfAsync(client);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var accepted = await PostAsync(client, "/api/v1/auth/login", new
            {
                userName = "direction.synthetic",
                password = "not-logged",
            }, csrf);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using var rejected = await PostAsync(client, "/api/v1/auth/login", new
        {
            userName = "direction.synthetic",
            password = "not-logged",
        }, csrf);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal(10, service.LoginCalls);
    }

    private static WebApplicationFactory<Program> CreateFactory(IHostedAuthenticationService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseTestServer()
                .ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHostedAuthenticationService>();
                    services.AddSingleton(service);
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                }));

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("requestToken").GetString()!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string uri, object body, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return client.SendAsync(request);
    }

    private sealed class RecordingAuthenticationService : IHostedAuthenticationService
    {
        private readonly AuthenticatedSession session = new(
            UserId,
            PersonId,
            "direction.synthetic",
            "Dirección Sintética",
            "DIRECCION",
            "security-stamp",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(8),
            ["PER-USUARIO-ADMIN"]);

        public int LoginCalls { get; private set; }

        public Task<AuthenticationFlowResult> LoginAsync(
            string userName,
            string password,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            LoginCalls++;
            return Task.FromResult(new AuthenticationFlowResult(
                Guid.Parse("0199f189-2a16-7191-81c7-8c40f74ff003"),
                AuthenticationNextStep.EnrollMfa,
                DateTimeOffset.UtcNow.AddMinutes(10)));
        }

        public Task<MfaCompletionResult> ConfirmMfaEnrollmentAsync(
            Guid challengeId,
            string totpCode,
            Guid correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MfaCompletionResult(
                session,
                ["AAAA-BBBB-CCCC-DDDD-EEEE"],
                AuthenticationNextStep.RecoveryCodes));

        public Task<SessionSnapshot> GetSessionAsync(
            Guid userId,
            string securityStamp,
            DateTimeOffset mfaAuthenticatedAt,
            DateTimeOffset absoluteExpiresAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionSnapshot(
                session.UserId,
                session.PersonId,
                session.UserName,
                session.DisplayName,
                "LOR-001",
                session.RoleCode,
                session.Permissions,
                mfaAuthenticatedAt,
                DateTimeOffset.UtcNow.AddMinutes(30),
                absoluteExpiresAt));

        public Task<AuthenticatedSession?> ValidateSessionAsync(
            Guid userId,
            string securityStamp,
            DateTimeOffset mfaAuthenticatedAt,
            DateTimeOffset absoluteExpiresAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AuthenticatedSession?>(
                userId == session.UserId && securityStamp == session.SecurityStamp ? session : null);

        public Task<AuthenticationFlowResult> ChangePasswordAsync(
            Guid? challengeId,
            Guid? authenticatedUserId,
            string currentPassword,
            string newPassword,
            Guid correlationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MfaEnrollmentResult> BeginMfaEnrollmentAsync(
            Guid challengeId,
            Guid correlationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MfaCompletionResult> VerifyMfaAsync(
            Guid challengeId,
            string? totpCode,
            string? recoveryCode,
            Guid correlationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MfaCompletionResult> RegenerateRecoveryCodesAsync(
            Guid? challengeId,
            Guid? authenticatedUserId,
            string currentPassword,
            Guid correlationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
