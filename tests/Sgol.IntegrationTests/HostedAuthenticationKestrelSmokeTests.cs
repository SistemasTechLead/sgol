using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Sdk;

namespace Sgol.IntegrationTests;

public sealed class HostedAuthenticationKestrelSmokeTests
{
    [Fact]
    public async Task RealKestrelHttpsCompletesHostedAuthenticationContract()
    {
        var stage = HostedAuthenticationSmokeStage.CSRF;
        var failureStage = stage;
        var scenario = HostedAuthenticationSmokeScenario.FIRST_ACCESS;
        var error = HostedAuthenticationSmokeError.UNEXPECTED_FAILURE;
        PostgreSqlContainer? postgres = null;
        Process? web = null;
        Task? stdoutDrain = null;
        Task? stderrDrain = null;
        WebApplicationFactory<Program>? seedFactory = null;
        string? certificatePath = null;
        var clients = new List<HttpClient>();
        var cleanupFailed = false;

        try
        {
            postgres = PostgreSqlPersistenceTests.CreateContainerForTests();
            await postgres.StartAsync();
            var accounts = CreateAccounts();
            seedFactory = CreateSeedFactory(postgres.GetConnectionString());
            await SeedAsync(seedFactory, accounts);

            var repositoryRoot = FindRepositoryRoot();
            var webAssembly = Path.Combine(repositoryRoot, "src", "Sgol.Web", "bin", "Release", "net10.0", "Sgol.Web.dll");
            if (!File.Exists(webAssembly))
            {
                error = HostedAuthenticationSmokeError.PRECONDITION_FAILED;
                throw new SmokeFailureException();
            }

            var port = ReserveTcpPort();
            var baseAddress = new Uri($"https://127.0.0.1:{port}");
            var certificatePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            using var certificate = CreateCertificate();
            certificatePath = Path.Combine(Path.GetTempPath(), $"sgol-auth-{Guid.CreateVersion7():N}.pfx");
            await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, certificatePassword));

            var startInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = repositoryRoot,
            };
            startInfo.ArgumentList.Add(webAssembly);
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests";
            startInfo.Environment["ASPNETCORE_URLS"] = baseAddress.AbsoluteUri;
            startInfo.Environment["ConnectionStrings__Sgol"] = postgres.GetConnectionString();
            startInfo.Environment["Kestrel__Certificates__Default__Path"] = certificatePath;
            startInfo.Environment["Kestrel__Certificates__Default__Password"] = certificatePassword;
            web = new Process { StartInfo = startInfo };
            if (!web.Start())
            {
                error = HostedAuthenticationSmokeError.HOST_START_FAILED;
                throw new SmokeFailureException();
            }
            stdoutDrain = DrainAsync(web.StandardOutput);
            stderrDrain = DrainAsync(web.StandardError);
            error = HostedAuthenticationSmokeError.HOST_NOT_READY;
            await WaitUntilReadyAsync(web, baseAddress, certificate);
            error = HostedAuthenticationSmokeError.UNEXPECTED_FAILURE;

            scenario = HostedAuthenticationSmokeScenario.FOUR_ROLES;
            var sessions = new List<SmokeSession>();
            foreach (var account in accounts)
            {
                var client = CreateClient(baseAddress, certificate);
                clients.Add(client);
                sessions.Add(await CompleteFirstAccessAsync(client, account, currentStage => stage = currentStage));
            }

            stage = HostedAuthenticationSmokeStage.SESSION_VALIDATE;
            foreach (var session in sessions)
            {
                using var state = await session.Client.GetAsync("/api/v1/auth/session");
                Require(state.StatusCode == HttpStatusCode.OK);
                using var branch = await session.Client.GetAsync("/api/v1/branches/LOR-001");
                Require(branch.StatusCode == HttpStatusCode.OK);
                using var users = await session.Client.GetAsync("/api/v1/users");
                Require(users.StatusCode == (session.Account.RoleCode == CanonicalRole.Direction
                    ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden));
            }

            scenario = HostedAuthenticationSmokeScenario.ADMINISTRATIVE_RESET;
            stage = HostedAuthenticationSmokeStage.MFA_RESET;
            var direction = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Direction);
            foreach (var denied in sessions.Where(item => item.Account.RoleCode != CanonicalRole.Direction))
            {
                using var response = await PostAsync(
                    denied.Client,
                    $"/api/v1/users/{direction.Account.UserId:D}/mfa-reset",
                    new { reason = "Smoke TECH-AUTH-001", temporaryPassword = NewPassword() },
                    denied.Csrf,
                    idempotencyKey: Guid.CreateVersion7());
                Require(response.StatusCode == HttpStatusCode.Forbidden);
            }
            var sales = sessions.Single(item => item.Account.RoleCode == CanonicalRole.SalesFloor);
            using (var reset = await PostAsync(
                direction.Client,
                $"/api/v1/users/{sales.Account.UserId:D}/mfa-reset",
                new { reason = "Smoke TECH-AUTH-001", temporaryPassword = NewPassword() },
                direction.Csrf,
                idempotencyKey: Guid.CreateVersion7()))
            {
                Require(reset.StatusCode == HttpStatusCode.OK);
            }
            using (var resetSession = await sales.Client.GetAsync("/api/v1/auth/session"))
            {
                Require(resetSession.StatusCode == HttpStatusCode.Unauthorized);
            }
            await using (var scope = seedFactory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
                var audits = await context.AuditEvents.AsNoTracking().ToListAsync();
                Require(audits.Any(item => item.Action == "AUTH_MFA_RESET"));
                Require(audits.Any(item => item.Action == "AUTH_SESSION_REJECTED"));
                var auditText = string.Join('|', audits.Select(item =>
                    $"{item.Action}:{item.Reason}:{item.BeforeData?.RootElement.GetRawText()}:{item.AfterData?.RootElement.GetRawText()}"));
                foreach (var secret in accounts.SelectMany(item => new[] { item.TemporaryPassword, item.NewPassword })
                    .Concat(sessions.Select(item => item.RecoveryCode)))
                {
                    Require(!auditText.Contains(secret, StringComparison.Ordinal));
                }
            }

            scenario = HostedAuthenticationSmokeScenario.RECOVERY_CODE;
            stage = HostedAuthenticationSmokeStage.LOGOUT;
            using (var logout = await PostAsync(direction.Client, "/api/v1/auth/logout", new { }, direction.Csrf))
            {
                Require(logout.StatusCode == HttpStatusCode.NoContent);
            }
            var anonymousCsrf = await GetCsrfAsync(direction.Client);
            stage = HostedAuthenticationSmokeStage.LOGIN;
            using (var login = await LoginAsync(direction.Client, direction.Account.UserName, direction.Account.NewPassword, anonymousCsrf))
            {
                Require(login.StatusCode == HttpStatusCode.OK);
            }
            stage = HostedAuthenticationSmokeStage.MFA_VERIFY;
            using (var recovery = await PostAsync(
                direction.Client,
                "/api/v1/auth/mfa/verify",
                new { recoveryCode = direction.RecoveryCode },
                anonymousCsrf))
            {
                Require(recovery.StatusCode == HttpStatusCode.OK);
                Require(await NextStepAsync(recovery) == AuthenticationNextStep.RegenerateRecoveryCodes);
            }
            stage = HostedAuthenticationSmokeStage.RECOVERY_REGENERATE;
            using (var regenerated = await PostAsync(
                direction.Client,
                "/api/v1/auth/recovery-codes/regenerate",
                new { currentPassword = direction.Account.NewPassword },
                anonymousCsrf))
            {
                Require(regenerated.StatusCode == HttpStatusCode.OK);
            }

            scenario = HostedAuthenticationSmokeScenario.LOCKOUT;
            stage = HostedAuthenticationSmokeStage.LOGIN;
            var administration = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Administration).Account;
            var attempts = new List<Task<HttpResponseMessage>>();
            for (var index = 0; index < 5; index++)
            {
                var client = CreateClient(baseAddress, certificate);
                clients.Add(client);
                var csrf = await GetCsrfAsync(client);
                attempts.Add(LoginAsync(client, administration.UserName, NewPassword(), csrf));
            }
            var failures = await Task.WhenAll(attempts);
            try
            {
                Require(failures.All(item => item.StatusCode == HttpStatusCode.Unauthorized));
            }
            finally
            {
                foreach (var response in failures)
                {
                    response.Dispose();
                }
            }
            await using (var scope = seedFactory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
                var lockedUser = await context.AppUsers.AsNoTracking()
                    .SingleAsync(item => item.Id == administration.UserId);
                Require(lockedUser.AccessFailedCount == 0);
                Require(lockedUser.LockoutLevel == 1);
                Require(lockedUser.LockoutEndUtc > DateTimeOffset.UtcNow);
            }

            scenario = HostedAuthenticationSmokeScenario.INVALIDATION;
            stage = HostedAuthenticationSmokeStage.SESSION_VALIDATE;
            await InvalidateAsync(seedFactory, sessions);
            foreach (var session in sessions)
            {
                using var invalidated = await session.Client.GetAsync("/api/v1/auth/session");
                Require(invalidated.StatusCode == HttpStatusCode.Unauthorized);
            }

            error = HostedAuthenticationSmokeError.NONE;
        }
        catch (SmokeFailureException)
        {
            failureStage = stage;
            error = error == HostedAuthenticationSmokeError.UNEXPECTED_FAILURE
                ? HostedAuthenticationSmokeError.HTTP_CONTRACT_FAILED
                : error;
        }
        catch
        {
            failureStage = stage;
            error = web is { HasExited: true }
                ? HostedAuthenticationSmokeError.PROCESS_EXIT_FAILED
                : HostedAuthenticationSmokeError.UNEXPECTED_FAILURE;
        }
        finally
        {
            stage = HostedAuthenticationSmokeStage.CLEANUP;
            foreach (var client in clients)
            {
                client.Dispose();
            }
            try
            {
                if (web is { HasExited: false })
                {
                    web.Kill(entireProcessTree: true);
                    await web.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                }
                if (stdoutDrain is not null)
                {
                    await stdoutDrain.WaitAsync(TimeSpan.FromSeconds(5));
                }
                if (stderrDrain is not null)
                {
                    await stderrDrain.WaitAsync(TimeSpan.FromSeconds(5));
                }
                web?.Dispose();
            }
            catch
            {
                cleanupFailed = true;
            }
            try
            {
                if (seedFactory is not null)
                {
                    await seedFactory.DisposeAsync();
                }
            }
            catch
            {
                cleanupFailed = true;
            }
            try
            {
                if (postgres is not null)
                {
                    await postgres.DisposeAsync();
                }
            }
            catch
            {
                cleanupFailed = true;
            }
            try
            {
                if (certificatePath is not null && File.Exists(certificatePath))
                {
                    File.Delete(certificatePath);
                }
            }
            catch
            {
                cleanupFailed = true;
            }
        }

        var evidence = cleanupFailed
            ? HostedAuthenticationSmokeEvidence.Failure(
                HostedAuthenticationSmokeStage.CLEANUP,
                scenario,
                HostedAuthenticationSmokeError.CLEANUP_FAILED)
            : error == HostedAuthenticationSmokeError.NONE
                ? HostedAuthenticationSmokeEvidence.Success(stage, scenario)
                : HostedAuthenticationSmokeEvidence.Failure(failureStage, scenario, error);
        Console.WriteLine(evidence.ToJson());
        if (evidence.Exit != 0)
        {
            throw new XunitException(evidence.ToJson());
        }
    }

    private static WebApplicationFactory<Program> CreateSeedFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("IntegrationTests");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sgol"] = connectionString,
                }));
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });

    private static IReadOnlyList<SmokeAccount> CreateAccounts() =>
    [
        NewAccount(CanonicalRole.Direction),
        NewAccount(CanonicalRole.Administration),
        NewAccount(CanonicalRole.Subcoordination),
        NewAccount(CanonicalRole.SalesFloor),
    ];

    private static SmokeAccount NewAccount(string role) => new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), $"{role.ToLowerInvariant()}.{Guid.CreateVersion7():N}", role, NewPassword(), NewPassword());

    private static string NewPassword() => $"S!{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}a";

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, IReadOnlyList<SmokeAccount> accounts)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();
        var hasher = new PasswordHasher<AppUser>();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        foreach (var account in accounts)
        {
            context.People.Add(new Person { Id = account.PersonId, StableCode = $"AUTH-{account.PersonId:N}", DisplayName = $"Persona {account.RoleCode}", CreatedAt = now });
            context.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), account.PersonId, BranchScope.LorettaId, EmploymentStatus.Active, now));
            var user = new AppUser { Id = account.UserId, PersonId = account.PersonId, Status = AccountStatus.Active, MustChangePassword = true, SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) };
            context.AppUsers.Add(user);
            context.IdentityCredentials.Add(new IdentityCredential { UserId = user.Id, UserName = account.UserName, NormalizedUserName = account.UserName.ToUpperInvariant(), PasswordHash = hasher.HashPassword(user, account.TemporaryPassword) });
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion { Id = Guid.CreateVersion7(), UserId = user.Id, BranchId = BranchScope.LorettaId, RoleCode = account.RoleCode, Status = RoleAssignmentStatus.Active, ValidFrom = now });
        }
        await context.SaveChangesAsync();
    }

    private static async Task<SmokeSession> CompleteFirstAccessAsync(HttpClient client, SmokeAccount account, Action<HostedAuthenticationSmokeStage> setStage)
    {
        setStage(HostedAuthenticationSmokeStage.CSRF);
        var csrf = await GetCsrfAsync(client);
        setStage(HostedAuthenticationSmokeStage.LOGIN);
        using (var login = await LoginAsync(client, account.UserName, account.TemporaryPassword, csrf))
        {
            Require(login.StatusCode == HttpStatusCode.OK && await NextStepAsync(login) == AuthenticationNextStep.ChangePassword);
        }
        setStage(HostedAuthenticationSmokeStage.PASSWORD_CHANGE);
        using (var changed = await PostAsync(client, "/api/v1/auth/password/change", new { currentPassword = account.TemporaryPassword, newPassword = account.NewPassword }, csrf))
        {
            Require(changed.StatusCode == HttpStatusCode.OK && await NextStepAsync(changed) == AuthenticationNextStep.EnrollMfa);
        }
        setStage(HostedAuthenticationSmokeStage.MFA_ENROLL);
        string manualKey;
        using (var enrollment = await PostAsync(client, "/api/v1/auth/mfa/enroll", new { }, csrf))
        {
            Require(enrollment.StatusCode == HttpStatusCode.OK);
            manualKey = await DataStringAsync(enrollment, "manualKey");
        }
        setStage(HostedAuthenticationSmokeStage.MFA_CONFIRM);
        string recoveryCode;
        using (var confirmed = await PostAsync(client, "/api/v1/auth/mfa/confirm", new { totpCode = ComputeTotp(manualKey, DateTimeOffset.UtcNow) }, csrf))
        {
            Require(confirmed.StatusCode == HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync());
            recoveryCode = document.RootElement.GetProperty("data").GetProperty("recoveryCodes")[0].GetString()!;
        }
        return new SmokeSession(account, client, await GetCsrfAsync(client), recoveryCode);
    }

    private static async Task InvalidateAsync(WebApplicationFactory<Program> factory, IReadOnlyList<SmokeSession> sessions)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var now = DateTimeOffset.UtcNow;
        var direction = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Direction).Account;
        (await context.AppUsers.SingleAsync(item => item.Id == direction.UserId)).SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var administration = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Administration).Account;
        var role = await context.RoleAssignmentVersions.SingleAsync(item => item.UserId == administration.UserId && item.ValidTo == null);
        role.Status = RoleAssignmentStatus.Superseded;
        role.ValidTo = now;
        role.RowVersion++;
        var subcoordination = sessions.Single(item => item.Account.RoleCode == CanonicalRole.Subcoordination).Account;
        var employment = await context.EmploymentVersions.SingleAsync(item => item.PersonId == subcoordination.PersonId && item.ValidTo == null);
        context.EmploymentVersions.Add(employment.CreateSuccessor(Guid.CreateVersion7(), EmploymentStatus.Inactive, now));
        var sales = sessions.Single(item => item.Account.RoleCode == CanonicalRole.SalesFloor).Account;
        (await context.AppUsers.SingleAsync(item => item.Id == sales.UserId)).Status = AccountStatus.Inactive;
        await context.SaveChangesAsync();
    }

    private static HttpClient CreateClient(Uri baseAddress, X509Certificate2 certificate)
    {
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies, UseCookies = true };
        var expected = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        handler.ServerCertificateCustomValidationCallback = (_, presented, _, _) =>
            presented is not null && string.Equals(presented.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.Ordinal);
        return new HttpClient(handler) { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
    }

    private static async Task WaitUntilReadyAsync(Process process, Uri baseAddress, X509Certificate2 certificate)
    {
        using var client = CreateClient(baseAddress, certificate);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new SmokeFailureException();
            }
            try
            {
                using var response = await client.GetAsync("/health/live");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            await Task.Delay(250);
        }
        throw new SmokeFailureException();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    }

    private static int ReserveTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new SmokeFailureException();
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is not null)
        {
        }
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Require(response.StatusCode == HttpStatusCode.OK);
        return await DataStringAsync(response, "requestToken");
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password, string csrf) =>
        PostAsync(client, "/api/v1/auth/login", new { userName, password }, csrf);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string csrf, Guid? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (idempotencyKey is Guid key)
        {
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
        }
        return client.SendAsync(request);
    }

    private static async Task<string> DataStringAsync(HttpResponseMessage response, string property)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty(property).GetString()!;
    }

    private static Task<string> NextStepAsync(HttpResponseMessage response) => DataStringAsync(response, "nextStep");

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
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(output.ToArray(), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new SmokeFailureException();
        }
    }

    private sealed class SmokeFailureException : Exception;
    private sealed record SmokeAccount(Guid UserId, Guid PersonId, string UserName, string RoleCode, string TemporaryPassword, string NewPassword);
    private sealed record SmokeSession(SmokeAccount Account, HttpClient Client, string Csrf, string RecoveryCode);
}
