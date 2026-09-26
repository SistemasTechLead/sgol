using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Testcontainers.PostgreSql;

namespace Sgol.FrontendBrowserTests;

internal sealed record BrowserAccount(Guid UserId, Guid PersonId, string UserName, string Role,
    string TemporaryPassword, string NewPassword);

internal sealed class BrowserFixture : IAsyncDisposable
{
    private static readonly string[] RejectedEnvironmentVariables =
        ["ConnectionStrings__Sgol", "DATABASE_URL", "PGHOST", "PGDATABASE", "PGUSER", "PGPASSWORD"];
    private readonly string databaseName = $"sgol_front004_{Guid.CreateVersion7():N}";
    private readonly List<HttpClient> clients = [];
    private PostgreSqlContainer? database;
    private string? connectionString;
    private Process? web;
    private Task? stdoutDrain;
    private Task? stderrDrain;
    private X509Certificate2? certificate;
    private string? certificatePath;
    private bool trustedCertificateInstalled;
    private bool disposed;
    public bool CleanupComplete { get; private set; }

    public Uri BaseAddress { get; private set; } = null!;
    public IReadOnlyList<BrowserAccount> Accounts { get; } =
    [
        NewAccount(CanonicalRole.Direction),
        NewAccount(CanonicalRole.Administration),
        NewAccount(CanonicalRole.Subcoordination),
        NewAccount(CanonicalRole.SalesFloor),
    ];

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    public async Task StartAsync()
    {
        // The test owns a disposable local database. Never accept an external connection string.
        if (RejectedEnvironmentVariables.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))))
            throw new InvalidOperationException("External database configuration is not allowed.");

        database = new PostgreSqlBuilder("postgres:18.6-alpine3.23")
            .WithDatabase(databaseName)
            .WithUsername($"front004_{Guid.CreateVersion7():N}")
            .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
            .Build();
        await database.StartAsync();
        var connection = database.GetConnectionString();
        connectionString = connection;
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host is not ("localhost" or "127.0.0.1") || parsed.Database != databaseName || parsed.Port <= 0)
            throw new InvalidOperationException("Disposable database contract failed.");

        await using (var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options))
        {
            await context.Database.MigrateAsync();
            var hasher = new PasswordHasher<AppUser>();
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            foreach (var account in Accounts)
            {
                context.People.Add(new Person
                {
                    Id = account.PersonId,
                    StableCode = $"FRONT004-{account.PersonId:N}",
                    DisplayName = $"Persona sintética {account.Role}",
                    CreatedAt = now
                });
                context.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), account.PersonId,
                    BranchScope.LorettaId, EmploymentStatus.Active, now));
                var user = new AppUser
                {
                    Id = account.UserId,
                    PersonId = account.PersonId,
                    Status = AccountStatus.Active,
                    MustChangePassword = true,
                    SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
                };
                context.AppUsers.Add(user);
                context.IdentityCredentials.Add(new IdentityCredential
                {
                    UserId = user.Id,
                    UserName = account.UserName,
                    NormalizedUserName = account.UserName.ToUpperInvariant(),
                    PasswordHash = hasher.HashPassword(user, account.TemporaryPassword)
                });
                context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
                {
                    Id = Guid.CreateVersion7(),
                    UserId = user.Id,
                    BranchId = BranchScope.LorettaId,
                    RoleCode = account.Role,
                    Status = RoleAssignmentStatus.Active,
                    ValidFrom = now
                });
            }
            await context.SaveChangesAsync();
        }

        var port = ReservePort();
        BaseAddress = new Uri($"https://127.0.0.1:{port}");
        certificate = NewCertificate();
        using (var roots = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
        {
            roots.Open(OpenFlags.ReadWrite);
            if (roots.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false).Count != 0)
                throw new InvalidOperationException("Fixture certificate already exists in trust store.");
            roots.Add(certificate);
            trustedCertificateInstalled = true;
        }
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        certificatePath = Path.Combine(Path.GetTempPath(), $"sgol-front004-{Guid.CreateVersion7():N}.pfx");
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        var assembly = Path.Combine(RepositoryRoot(), "src", "Sgol.Web", "bin", "Release", "net10.0", "Sgol.Web.dll");
        if (!File.Exists(assembly)) throw new InvalidOperationException("Build Release before browser smoke.");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.Combine(RepositoryRoot(), "src", "Sgol.Web")
        };
        start.ArgumentList.Add(assembly);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests";
        start.Environment["ASPNETCORE_URLS"] = BaseAddress.AbsoluteUri;
        start.Environment["ConnectionStrings__Sgol"] = connection;
        start.Environment["Kestrel__Certificates__Default__Path"] = certificatePath;
        start.Environment["Kestrel__Certificates__Default__Password"] = password;
        web = new Process { StartInfo = start };
        if (!web.Start()) throw new InvalidOperationException("Kestrel did not start.");
        stdoutDrain = DrainAsync(web.StandardOutput);
        stderrDrain = DrainAsync(web.StandardError);
        await WaitUntilReadyAsync();
    }

    public HttpClient NewClient(out CookieContainer cookies)
    {
        if (certificate is null) throw new InvalidOperationException("HTTPS fixture is not ready.");
        cookies = new CookieContainer();
        var expected = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var handler = new HttpClientHandler
        {
            CookieContainer = cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented is not null &&
                string.Equals(presented.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.Ordinal)
        };
        var client = new HttpClient(handler) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
        clients.Add(client);
        return client;
    }

    public async Task InvalidateAsync(BrowserAccount account)
    {
        if (connectionString is null) throw new InvalidOperationException("Disposable database is unavailable.");
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        var user = await context.AppUsers.SingleAsync(item => item.Id == account.UserId);
        user.SecurityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await context.SaveChangesAsync();
    }

    public async Task<Guid> SeedUnlinkedPersonAsync(string code, bool active)
    {
        if (connectionString is null) throw new InvalidOperationException("Disposable database is unavailable.");
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        var personId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = code,
            DisplayName = $"Persona sintética {code}",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        context.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), personId,
            BranchScope.LorettaId, active ? EmploymentStatus.Active : EmploymentStatus.Inactive,
            DateTimeOffset.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();
        return personId;
    }

    public async Task<Guid> SeedCalendarScenarioAsync(Guid directionUserId, DateOnly publishedDate)
    {
        if (connectionString is null) throw new InvalidOperationException("Disposable database is unavailable.");
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        var clock = new BrowserFixedClock(DateTimeOffset.UtcNow.AddMinutes(-2));
        var uuids = new Uuid7Generator(clock);
        var audit = new AuditTransaction(context);
        var releases = new EfConfigurationReleaseService(context, audit,
            new VersioningTransaction(context, audit), clock, uuids);
        var calendar = new EfCalendarService(context, audit, clock, uuids);
        var published = await releases.CreateDraftAsync(new CreateConfigurationReleaseCommand(
            directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7()));
        await calendar.PutAsync(new PutCalendarDayCommand(directionUserId, Guid.CreateVersion7(),
            Guid.CreateVersion7(), publishedDate, published.Id, CalendarContract.Holiday,
            false, "Festivo sintético FRONT-008", null));
        await releases.PublishAsync(new PublishConfigurationReleaseCommand(directionUserId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), published.Id, published.RowVersion,
            clock.UtcNow, "Publicación sintética FRONT-008"));
        var draft = await releases.CreateDraftAsync(new CreateConfigurationReleaseCommand(
            directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7()));
        return draft.Id;
    }

    public async Task SeedPublishedPoliciesAsync(Guid directionUserId)
    {
        if (connectionString is null) throw new InvalidOperationException("Disposable database is unavailable.");
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(connectionString).Options);
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var clock = new BrowserFixedClock(now);
        var uuids = new Uuid7Generator(clock);
        var audit = new AuditTransaction(context);
        var releases = new EfConfigurationReleaseService(context, audit,
            new VersioningTransaction(context, audit), clock, uuids);
        var definitions = new EfTaskDefinitionService(context, audit, releases, clock, uuids);
        var activation = new EfActivationPolicyService(context, audit, clock, uuids);
        var eligibility = new EfEligibilityPolicyService(context, audit, clock, uuids);
        using var empty = JsonDocument.Parse("{}");
        var definitionRelease = await releases.CreateDraftAsync(new CreateConfigurationReleaseCommand(
            directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7()));
        foreach (var task in TaskDefinitionCatalog.All)
            await definitions.CreateVersionAsync(new CreateTaskDefinitionVersionCommand(
                directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(), task.TaskCode,
                definitionRelease.Id, 1, empty.RootElement));
        await releases.PublishAsync(new PublishConfigurationReleaseCommand(directionUserId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), definitionRelease.Id, definitionRelease.RowVersion,
            now, "Definiciones sintéticas FRONT-011"));
        var currentDefinitions = await context.TaskDefinitionVersions.AsNoTracking()
            .Where(item => item.Status == Sgol.BuildingBlocks.Versioning.VersionStatuses.Current)
            .ToDictionaryAsync(item => item.TaskDefinitionId, item => item.Id);
        var policyRelease = await releases.CreateDraftAsync(new CreateConfigurationReleaseCommand(
            directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7()));
        foreach (var task in TaskDefinitionCatalog.All)
        {
            var contract = ActivationPolicyCatalog.Require(task.TaskCode);
            using var schedule = task.TaskCode switch
            {
                "TAR-0005" => JsonDocument.Parse("""{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}"""),
                "TAR-0026" => JsonDocument.Parse("""{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"08:30","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}"""),
                _ => JsonDocument.Parse("null"),
            };
            await activation.PutAsync(new PutActivationPolicyCommand(directionUserId, Guid.CreateVersion7(),
                Guid.CreateVersion7(), task.TaskCode, currentDefinitions[task.Id], policyRelease.Id,
                contract.Mode, schedule.RootElement, contract.OriginKeySchema, null));
            await eligibility.PutAsync(new PutEligibilityPolicyCommand(directionUserId, Guid.CreateVersion7(),
                Guid.CreateVersion7(), task.TaskCode, policyRelease.Id,
                EligibilityPolicyCatalog.RequireRole(task.TaskCode), true, null, null));
        }
        await releases.PublishAsync(new PublishConfigurationReleaseCommand(directionUserId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), policyRelease.Id, policyRelease.RowVersion,
            now.AddMinutes(1), "Políticas sintéticas FRONT-011"));
    }

    private sealed class BrowserFixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    public async Task<string> AuthenticateAsync(BrowserAccount account)
    {
        using var client = NewClient(out var cookies);
        var csrf = await DataAsync(await client.GetAsync("/api/v1/auth/csrf"), "requestToken");
        await ExpectNextAsync(await PostAsync(client, "/api/v1/auth/login",
            new { userName = account.UserName, password = account.TemporaryPassword }, csrf),
            AuthenticationNextStep.ChangePassword);
        await ExpectNextAsync(await PostAsync(client, "/api/v1/auth/password/change",
            new { currentPassword = account.TemporaryPassword, newPassword = account.NewPassword }, csrf),
            AuthenticationNextStep.EnrollMfa);
        var key = await DataAsync(await PostAsync(client, "/api/v1/auth/mfa/enroll", new { }, csrf), "manualKey");
        using (var confirmation = await PostAsync(client, "/api/v1/auth/mfa/confirm",
            new { totpCode = Totp(key, DateTimeOffset.UtcNow) }, csrf))
        {
            if (confirmation.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("MFA confirmation failed.");
        }
        using (var session = await client.GetAsync("/api/v1/auth/session"))
        {
            if (session.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Full session unavailable.");
            using var body = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
            if (body.RootElement.GetProperty("data").GetProperty("roleCode").GetString() != account.Role)
                throw new InvalidOperationException("Session role mismatch.");
        }
        var cookie = cookies.GetCookies(BaseAddress).Cast<Cookie>()
            .Single(item => item.Name == "__Host-SGOL-Session");
        return cookie.Value;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<string> DataAsync(HttpResponseMessage response, string key)
    {
        using (response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Authentication step failed: HTTP {(int)response.StatusCode}.");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("data").GetProperty(key).GetString()!;
        }
    }

    private static async Task ExpectNextAsync(HttpResponseMessage response, string expected)
    {
        var actual = await DataAsync(response, "nextStep");
        if (actual != expected) throw new InvalidOperationException("Unexpected authentication step.");
    }

    private async Task WaitUntilReadyAsync()
    {
        using var client = NewClient(out _);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (web?.HasExited == true) throw new InvalidOperationException("Kestrel exited early.");
            try
            {
                using var response = await client.GetAsync("/health/live");
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new InvalidOperationException("Kestrel health check failed.");
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var success = true;
        foreach (var client in clients) client.Dispose();
        clients.Clear();
        try
        {
            if (web is { HasExited: false })
            {
                web.Kill(entireProcessTree: true);
                await web.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            if (stdoutDrain is not null) await stdoutDrain.WaitAsync(TimeSpan.FromSeconds(5));
            if (stderrDrain is not null) await stderrDrain.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch { success = false; }
        finally { web?.Dispose(); }
        try { if (database is not null) await database.DisposeAsync(); }
        catch { success = false; }
        try
        {
            if (trustedCertificateInstalled && certificate is not null)
            {
                using var roots = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                roots.Open(OpenFlags.ReadWrite);
                roots.Remove(certificate);
                success &= roots.Certificates.Find(X509FindType.FindByThumbprint,
                    certificate.Thumbprint, validOnly: false).Count == 0;
            }
        }
        catch { success = false; }
        certificate?.Dispose();
        try
        {
            if (certificatePath is not null && File.Exists(certificatePath)) File.Delete(certificatePath);
            success &= certificatePath is null || !File.Exists(certificatePath);
        }
        catch { success = false; }
        CleanupComplete = success;
        if (!success) throw new InvalidOperationException("Browser fixture cleanup failed.");
    }

    private static BrowserAccount NewAccount(string role) => new(Guid.CreateVersion7(), Guid.CreateVersion7(),
        $"{role.ToLowerInvariant()}.{Guid.CreateVersion7():N}", role, NewPassword(), NewPassword());
    private static string NewPassword() => $"S!{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}a";

    internal static string Totp(string secret, DateTimeOffset instant)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, instant.ToUnixTimeSeconds() / 30);
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter); // RFC 6238 requires HMAC-SHA1.
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static X509Certificate2 NewCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is not null) { }
    }
}
