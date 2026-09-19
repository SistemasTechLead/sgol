using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Sgol.Cv04Demo;

internal sealed class Cv04Infrastructure : IAsyncDisposable
{
    private readonly List<HttpClient> clients = [];
    private readonly string databaseName = $"{DemoContract.DatabasePrefix}{Guid.CreateVersion7():N}";
    private PostgreSqlContainer? postgres;
    private Process? web;
    private Task? stdoutDrain;
    private Task? stderrDrain;
    private X509Certificate2? certificate;
    private string? certificatePath;
    private bool cleaned;

    public string RepositoryRoot { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
    public Uri BaseAddress { get; private set; } = null!;
    public string DirectionUserName { get; } = $"dir.cv04.{Guid.CreateVersion7():N}";
    public string DirectionTemporaryPassword { get; } = NewPassword();
    public string DirectionNewPassword { get; } = NewPassword();
    public Guid DirectionUserId { get; private set; }
    public Guid DirectionPersonId { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var phase = "PREFLIGHT";
        try
        {
            RepositoryRoot = FindRepositoryRoot();
            DemoSafety.RejectExternalConfiguration(Environment.GetEnvironmentVariable);
            DemoSafety.ValidatePreconditions(RepositoryRoot);

            phase = "POSTGRESQL";
            var username = $"cv04_{Guid.CreateVersion7():N}";
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            postgres = new PostgreSqlBuilder(DemoContract.PostgreSqlImage)
                .WithDatabase(databaseName)
                .WithUsername(username)
                .WithPassword(password)
                .Build();
            try
            {
                await postgres.StartAsync(cancellationToken);
            }
            catch
            {
                throw new DemoFailureException("POSTGRESQL", "NONE", "CV04_POSTGRESQL_START_FAILED");
            }

            ConnectionString = postgres.GetConnectionString();
            var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
            DemoSafety.ValidateDisposableDatabase(builder.Host!, builder.Database!, databaseName, builder.Port);

            await using (var context = CreateContext())
            {
                await context.Database.MigrateAsync(cancellationToken);
                var migrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
                if (migrations.LastOrDefault() != DemoContract.LatestMigration)
                {
                    throw new DemoFailureException("POSTGRESQL", "NONE", "CV04_DATABASE_CONTRACT_FAILED");
                }
            }

            phase = "SEED";
            await RunDirectionBootstrapAsync(cancellationToken);
            await using (var context = CreateContext())
            {
                var normalizedUserName = DirectionUserName.ToUpperInvariant();
#pragma warning disable CA1309
                var direction = await (
                    from credential in context.IdentityCredentials.AsNoTracking()
                    join user in context.AppUsers.AsNoTracking() on credential.UserId equals user.Id
                    where string.Equals(credential.NormalizedUserName, normalizedUserName)
                    select new { credential.UserId, user.PersonId })
                    .SingleAsync(cancellationToken);
#pragma warning restore CA1309
                DirectionUserId = direction.UserId;
                DirectionPersonId = direction.PersonId;
            }

            phase = "HTTPS";
            await StartWebAsync(cancellationToken);
        }
        catch (DemoFailureException)
        {
            throw;
        }
        catch
        {
            var code = phase switch
            {
                "POSTGRESQL" => "CV04_DATABASE_CONTRACT_FAILED",
                "HTTPS" => "CV04_HTTPS_START_FAILED",
                _ => "CV04_PRECONDITION_FAILED",
            };
            throw new DemoFailureException(phase, "NONE", code);
        }
    }

    public SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(ConnectionString).Options);

    public HttpClient CreateClient()
    {
        if (certificate is null)
        {
            throw new InvalidOperationException("HTTPS infrastructure has not started.");
        }

        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies, UseCookies = true };
        var expected = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        handler.ServerCertificateCustomValidationCallback = (_, presented, _, _) =>
            presented is not null && string.Equals(
                presented.GetCertHashString(HashAlgorithmName.SHA256), expected, StringComparison.Ordinal);
        var client = new HttpClient(handler)
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30),
        };
        clients.Add(client);
        return client;
    }

    public async Task<bool> CleanupAsync()
    {
        if (cleaned)
        {
            return true;
        }

        cleaned = true;
        var success = true;
        foreach (var client in clients)
        {
            client.Dispose();
        }
        clients.Clear();

        try
        {
            if (web is { HasExited: false })
            {
                web.Kill(entireProcessTree: true);
                await web.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            if (web is not null && web.HasExited)
            {
                _ = DemoSafety.CaptureExit(web);
            }
            if (stdoutDrain is not null)
            {
                await stdoutDrain.WaitAsync(TimeSpan.FromSeconds(5));
            }
            if (stderrDrain is not null)
            {
                await stderrDrain.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch
        {
            success = false;
        }
        finally
        {
            web?.Dispose();
            web = null;
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
            success = false;
        }
        finally
        {
            postgres = null;
        }

        try
        {
            certificate?.Dispose();
            certificate = null;
            if (certificatePath is not null && File.Exists(certificatePath))
            {
                File.Delete(certificatePath);
            }
            if (certificatePath is not null && File.Exists(certificatePath))
            {
                success = false;
            }
        }
        catch
        {
            success = false;
        }

        return success;
    }

    public async ValueTask DisposeAsync() => _ = await CleanupAsync();

    public static string NewPassword() => $"S!{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}a";

    private async Task RunDirectionBootstrapAsync(CancellationToken cancellationToken)
    {
        var adminAssembly = Path.Combine(RepositoryRoot, "src", "Sgol.Admin", "bin", "Release", "net10.0", "Sgol.Admin.dll");
        var startInfo = BaseProcess(adminAssembly);
        startInfo.Environment["ConnectionStrings__Sgol"] = ConnectionString;
        startInfo.Environment["SGOL_BOOTSTRAP_PERSON_CODE"] = "DIR-CV04";
        startInfo.Environment["SGOL_BOOTSTRAP_PERSON_DISPLAY_NAME"] = "Dirección sintética CV-04";
        startInfo.Environment["SGOL_BOOTSTRAP_USER_NAME"] = DirectionUserName;
        startInfo.Environment["SGOL_BOOTSTRAP_INITIAL_PASSWORD"] = DirectionTemporaryPassword;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_PRECONDITION_FAILED");
        }
        var output = DrainAsync(process.StandardOutput);
        var errors = DrainAsync(process.StandardError);
        await process.WaitForExitAsync(cancellationToken);
        var exit = DemoSafety.CaptureExit(process);
        await Task.WhenAll(output, errors);
        if (exit != 0)
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_DATABASE_CONTRACT_FAILED", exit);
        }
    }

    private async Task StartWebAsync(CancellationToken cancellationToken)
    {
        var port = ReserveTcpPort();
        BaseAddress = new Uri($"https://{DemoContract.LoopbackAddress}:{port}");
        var certificatePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        certificate = CreateCertificate();
        certificatePath = Path.Combine(Path.GetTempPath(), $"sgol-cv04-{Guid.CreateVersion7():N}.pfx");
        await File.WriteAllBytesAsync(
            certificatePath,
            certificate.Export(X509ContentType.Pfx, certificatePassword),
            cancellationToken);

        var webAssembly = Path.Combine(RepositoryRoot, "src", "Sgol.Web", "bin", "Release", "net10.0", "Sgol.Web.dll");
        var startInfo = BaseProcess(webAssembly);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "IntegrationTests";
        startInfo.Environment["ASPNETCORE_URLS"] = BaseAddress.AbsoluteUri;
        startInfo.Environment["ConnectionStrings__Sgol"] = ConnectionString;
        startInfo.Environment["Kestrel__Certificates__Default__Path"] = certificatePath;
        startInfo.Environment["Kestrel__Certificates__Default__Password"] = certificatePassword;
        web = new Process { StartInfo = startInfo };
        if (!web.Start())
        {
            throw new DemoFailureException("HTTPS", "NONE", "CV04_HTTPS_START_FAILED");
        }
        stdoutDrain = DrainAsync(web.StandardOutput);
        stderrDrain = DrainAsync(web.StandardError);
        await WaitUntilReadyAsync(cancellationToken);
    }

    private ProcessStartInfo BaseProcess(string assembly)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = RepositoryRoot,
        };
        startInfo.ArgumentList.Add(assembly);
        return startInfo;
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (web is { HasExited: true })
            {
                _ = DemoSafety.CaptureExit(web);
                throw new DemoFailureException("HTTPS", "NONE", "CV04_HTTPS_START_FAILED");
            }
            try
            {
                using var response = await client.GetAsync("/health/live", cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TimeoutException)
            {
            }
            await Task.Delay(250, cancellationToken);
        }
        throw new DemoFailureException("HTTPS", "NONE", "CV04_HTTPS_START_FAILED");
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
        return directory?.FullName ?? throw new DemoFailureException("PREFLIGHT", "NONE", "CV04_PRECONDITION_FAILED");
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is not null)
        {
        }
    }
}
