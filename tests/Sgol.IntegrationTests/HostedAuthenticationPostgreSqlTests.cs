using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class HostedAuthenticationPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => postgres.StartAsync();

    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task RealHostCompletesFourRoleFirstAccessRecoveryAndSessionInvalidation()
    {
        var accounts = CreateSyntheticAccounts();
        await using var factory = CreateFactory();
        await SeedAsync(factory, accounts);

        using (var anonymous = CreateClient(factory))
        {
            var csrf = await GetCsrfAsync(anonymous);
            var known = await LoginAsync(anonymous, accounts[0].UserName, NewPassword(), csrf);
            var unknown = await LoginAsync(anonymous, "unknown.synthetic", NewPassword(), csrf);
            Assert.Equal(HttpStatusCode.Unauthorized, known.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
            Assert.Equal("AUTHENTICATION_FAILED", await ProblemCodeAsync(known));
            Assert.Equal("AUTHENTICATION_FAILED", await ProblemCodeAsync(unknown));
        }

        var sessions = new List<AuthenticatedClient>();
        foreach (var account in accounts)
        {
            sessions.Add(await CompleteFirstAccessAsync(factory, account));
        }

        foreach (var authenticated in sessions)
        {
            using var sessionResponse = await authenticated.Client.GetAsync("/api/v1/auth/session");
            Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
            using var branchResponse = await authenticated.Client.GetAsync("/api/v1/branches/LOR-001");
            Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);

            using var usersResponse = await authenticated.Client.GetAsync("/api/v1/users");
            Assert.Equal(
                authenticated.Account.RoleCode == CanonicalRole.Direction
                    ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden,
                usersResponse.StatusCode);

            using var continuity = await PostAsync(
                authenticated.Client,
                "/api/v1/continuity/reconciliations",
                new { reason = "Smoke sintético TECH-AUTH-001" },
                authenticated.Csrf,
                new Dictionary<string, string> { ["Idempotency-Key"] = Guid.CreateVersion7().ToString("D") });
            Assert.Equal(
                authenticated.Account.RoleCode == CanonicalRole.Direction
                    ? HttpStatusCode.Created
                    : HttpStatusCode.Forbidden,
                continuity.StatusCode);
        }

        var direction = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Direction);
        using (var logout = await PostAsync(direction.Client, "/api/v1/auth/logout", new { }, direction.Csrf))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
        using (var afterLogout = await direction.Client.GetAsync("/api/v1/auth/session"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        }

        var anonymousCsrf = await GetCsrfAsync(direction.Client);
        using (var login = await LoginAsync(
            direction.Client,
            direction.Account.UserName,
            direction.Account.NewPassword,
            anonymousCsrf))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        using (var recovered = await PostAsync(
            direction.Client,
            "/api/v1/auth/mfa/verify",
            new { recoveryCode = direction.RecoveryCode },
            anonymousCsrf))
        {
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            Assert.Equal(AuthenticationNextStep.RegenerateRecoveryCodes, await NextStepAsync(recovered));
        }
        using (var regenerated = await PostAsync(
            direction.Client,
            "/api/v1/auth/recovery-codes/regenerate",
            new { currentPassword = direction.Account.NewPassword },
            anonymousCsrf))
        {
            Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        }

        await InvalidatePersistentIdentityAsync(factory, sessions);
        foreach (var authenticated in sessions)
        {
            using var invalidated = await authenticated.Client.GetAsync("/api/v1/auth/session");
            Assert.Equal(HttpStatusCode.Unauthorized, invalidated.StatusCode);
        }

        await using var lockoutFactory = CreateFactory();
        using var lockoutClient = CreateClient(lockoutFactory);
        var lockoutCsrf = await GetCsrfAsync(lockoutClient);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await LoginAsync(
                lockoutClient,
                direction.Account.UserName,
                NewPassword(),
                lockoutCsrf);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        using var locked = await LoginAsync(
            lockoutClient,
            direction.Account.UserName,
            direction.Account.NewPassword,
            lockoutCsrf);
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        Assert.Equal("ACCOUNT_LOCKED", await ProblemCodeAsync(locked));

        foreach (var item in sessions)
        {
            item.Client.Dispose();
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sgol"] = postgres.GetConnectionString(),
                    }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    private static IReadOnlyList<SyntheticAccount> CreateSyntheticAccounts() =>
    [
        NewAccount(CanonicalRole.Direction),
        NewAccount(CanonicalRole.Administration),
        NewAccount(CanonicalRole.Subcoordination),
        NewAccount(CanonicalRole.SalesFloor),
    ];

    private static SyntheticAccount NewAccount(string roleCode) => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        $"{roleCode.ToLowerInvariant()}.synthetic",
        roleCode,
        NewPassword(),
        NewPassword());

    private static string NewPassword() => $"S!{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}a";

    private static async Task SeedAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyList<SyntheticAccount> accounts)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();
        var hasher = new PasswordHasher<AppUser>();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        foreach (var account in accounts)
        {
            context.People.Add(new Person
            {
                Id = account.PersonId,
                StableCode = $"AUTH-{account.PersonId:N}",
                DisplayName = $"Persona {account.RoleCode}",
                CreatedAt = now,
            });
            context.EmploymentVersions.Add(new EmploymentVersion(
                Guid.CreateVersion7(),
                account.PersonId,
                BranchScope.LorettaId,
                EmploymentStatus.Active,
                now));
            var user = new AppUser
            {
                Id = account.UserId,
                PersonId = account.PersonId,
                Status = AccountStatus.Active,
                MustChangePassword = true,
                MfaEnrolledAt = null,
                SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            };
            context.AppUsers.Add(user);
            context.IdentityCredentials.Add(new IdentityCredential
            {
                UserId = user.Id,
                UserName = account.UserName,
                NormalizedUserName = account.UserName.ToUpperInvariant(),
                PasswordHash = hasher.HashPassword(user, account.TemporaryPassword),
            });
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                BranchId = BranchScope.LorettaId,
                RoleCode = account.RoleCode,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = now,
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<AuthenticatedClient> CompleteFirstAccessAsync(
        WebApplicationFactory<Program> factory,
        SyntheticAccount account)
    {
        var client = CreateClient(factory);
        var csrf = await GetCsrfAsync(client);
        using (var login = await LoginAsync(client, account.UserName, account.TemporaryPassword, csrf))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal(AuthenticationNextStep.ChangePassword, await NextStepAsync(login));
        }
        using (var changed = await PostAsync(client, "/api/v1/auth/password/change", new
        {
            currentPassword = account.TemporaryPassword,
            newPassword = account.NewPassword,
        }, csrf))
        {
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.Equal(AuthenticationNextStep.EnrollMfa, await NextStepAsync(changed));
        }
        string manualKey;
        using (var enrollment = await PostAsync(client, "/api/v1/auth/mfa/enroll", new { }, csrf))
        {
            Assert.Equal(HttpStatusCode.OK, enrollment.StatusCode);
            manualKey = await DataStringAsync(enrollment, "manualKey");
        }
        string recoveryCode;
        using (var confirmed = await PostAsync(client, "/api/v1/auth/mfa/confirm", new
        {
            totpCode = ComputeTotp(manualKey, DateTimeOffset.UtcNow),
        }, csrf))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            using var document = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync());
            recoveryCode = document.RootElement.GetProperty("data").GetProperty("recoveryCodes")[0].GetString()!;
        }

        var authenticatedCsrf = await GetCsrfAsync(client);
        return new AuthenticatedClient(account, client, authenticatedCsrf, recoveryCode);
    }

    private static async Task InvalidatePersistentIdentityAsync(
        WebApplicationFactory<Program> factory,
        IReadOnlyList<AuthenticatedClient> sessions)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var now = DateTimeOffset.UtcNow;

        var direction = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Direction).Account;
        var directionUser = await context.AppUsers.SingleAsync(item => item.Id == direction.UserId);
        directionUser.SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var administration = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Administration).Account;
        var administrationRole = await context.RoleAssignmentVersions.SingleAsync(item =>
            item.UserId == administration.UserId && item.ValidTo == null);
        administrationRole.Status = RoleAssignmentStatus.Superseded;
        administrationRole.ValidTo = now;
        administrationRole.RowVersion++;

        var subcoordination = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Subcoordination).Account;
        var employment = await context.EmploymentVersions.SingleAsync(item =>
            item.PersonId == subcoordination.PersonId && item.ValidTo == null);
        context.EmploymentVersions.Add(employment.CreateSuccessor(
            Guid.CreateVersion7(),
            EmploymentStatus.Inactive,
            now));

        var salesFloor = sessions.Single(item => item.Account.RoleCode == CanonicalRole.SalesFloor).Account;
        var salesFloorUser = await context.AppUsers.SingleAsync(item => item.Id == salesFloor.UserId);
        salesFloorUser.Status = AccountStatus.Inactive;
        await context.SaveChangesAsync();
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        return await DataStringAsync(response, "requestToken");
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userName,
        string password,
        string csrf) =>
        PostAsync(client, "/api/v1/auth/login", new { userName, password }, csrf);

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object body,
        string csrf,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
        return client.SendAsync(request);
    }

    private static async Task<string> NextStepAsync(HttpResponseMessage response) =>
        await DataStringAsync(response, "nextStep");

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString()!;
    }

    private static async Task<string> DataStringAsync(HttpResponseMessage response, string property)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty(property).GetString()!;
    }

    private static string ComputeTotp(string base32Secret, DateTimeOffset instant)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in base32Secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, instant.ToUnixTimeSeconds() / 30);
#pragma warning disable CA5350 // The tested RFC 6238 profile intentionally uses HMAC-SHA1.
        var hash = HMACSHA1.HashData(output.ToArray(), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record SyntheticAccount(
        Guid UserId,
        Guid PersonId,
        string UserName,
        string RoleCode,
        string TemporaryPassword,
        string NewPassword);

    private sealed record AuthenticatedClient(
        SyntheticAccount Account,
        HttpClient Client,
        string Csrf,
        string RecoveryCode);
}
