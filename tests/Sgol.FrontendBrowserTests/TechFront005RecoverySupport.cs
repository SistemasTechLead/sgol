using Microsoft.Extensions.Options;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.FrontendBrowserTests;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05Infrastructure
{
    internal Func<Task<bool>> FrontendResourceAbsenceCheck(Action<string> report)
    {
        var containerIds = new[] { postgres, restorePostgres, scanner, sourceStore, destinationStore }
            .Where(container => container is not null).Select(container => container!.Id)
            .Where(id => !string.IsNullOrEmpty(id)).ToArray();
        var transientNames = transientContainers.ToArray();
        var ownedDirectory = privateDirectory;
        var ownedCertificatePath = certificatePath;
        var process = web;
        var processId = process is null ? (int?)null : process.Id;
        var processStarted = process is null ? (DateTime?)null : process.StartTime;
        return async () =>
        {
            var absent = !Directory.Exists(ownedDirectory) && (ownedCertificatePath is null || !File.Exists(ownedCertificatePath));
            foreach (var id in containerIds)
            {
                var result = await NativeProcess.RunAsync("docker", ["container", "ls", "--all", "--filter", "id=" + id,
                    "--format", "{{.ID}}"], RepositoryRoot, TimeSpan.FromSeconds(30), CancellationToken.None);
                absent &= result.Exit == 0 && string.IsNullOrWhiteSpace(result.Stdout);
            }
            var networks = await NativeProcess.RunAsync("docker", ["network", "ls", "--filter", "name=" + networkName,
                "--format", "{{.Name}}"], RepositoryRoot, TimeSpan.FromSeconds(30), CancellationToken.None);
            absent &= networks.Exit == 0 && string.IsNullOrWhiteSpace(networks.Stdout);
            foreach (var name in transientNames)
            {
                var result = await NativeProcess.RunAsync("docker", ["container", "ls", "--all", "--filter", "name=" + name,
                    "--format", "{{.Names}}"], RepositoryRoot, TimeSpan.FromSeconds(30), CancellationToken.None);
                absent &= result.Exit == 0 && string.IsNullOrWhiteSpace(result.Stdout);
            }
            if (processId is { } idValue)
            {
                try
                {
                    using var remaining = System.Diagnostics.Process.GetProcessById(idValue);
                    absent &= remaining.StartTime != processStarted;
                }
                catch (ArgumentException) { }
            }
            report("TECH_FRONT005 CLEANUP_ABSENCE " + absent + " containers=" + containerIds.Length);
            return absent;
        };
    }

    internal async Task ConfigureFrontendCorsAsync()
    {
        using var client = CreateSourceS3();
        await client.PutCORSConfigurationAsync(new Amazon.S3.Model.PutCORSConfigurationRequest
        {
            BucketName = DemoContract.SourceQuarantineBucket,
            Configuration = new Amazon.S3.Model.CORSConfiguration
            {
                Rules = [new Amazon.S3.Model.CORSRule { Id = "sgol-evidence-upload", AllowedOrigins = [BaseAddress.AbsoluteUri.TrimEnd('/')],
                    AllowedMethods = ["PUT"], AllowedHeaders = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"], MaxAgeSeconds = 600 }]
            }
        });
    }

    private bool frontendCertificateTrusted;

    internal async Task VerifyFrontendTlsAsync(Action<string> report)
    {
        using var handler = new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, _, chain, errors) =>
            {
                report("TECH_FRONT005 TLS_POLICY " + errors);
                foreach (var status in chain?.ChainStatus ?? [])
                    report("TECH_FRONT005 TLS_CHAIN " + status.Status);
                return errors == System.Net.Security.SslPolicyErrors.None;
            }
        };
        using var client = new HttpClient(handler) { BaseAddress = BaseAddress };
        using var response = await client.GetAsync("/api/v1/auth/session");
        if ((int)response.StatusCode != 401) throw new InvalidOperationException("Unexpected anonymous session contract.");
    }

    internal void TrustFrontendCertificate()
    {
        if (certificate is null) throw new InvalidOperationException("Owned HTTPS certificate unavailable.");
        OwnedBrowserCertificateTrust.Add(certificate);
        frontendCertificateTrusted = true;
    }

    internal bool CleanupFrontendCertificate()
    {
        if (!frontendCertificateTrusted) return true;
        if (certificate is null) return false;
        var removed = OwnedBrowserCertificateTrust.Remove(certificate);
        if (removed) frontendCertificateTrusted = false;
        return removed;
    }

    internal EvidenceInspectionPipeline FrontendInspectionPipeline(Amazon.S3.IAmazonS3 client) => new(new FileTechnicalValidator(),
        new CryptographicEvidenceObjectKeyFactory(), new S3PrivateObjectStorage(client,
            Options.Create(new EvidenceStorageOptions
            {
                Endpoint = SourceHostEndpoint,
                Region = "us-east-1",
                QuarantineBucket = DemoContract.SourceQuarantineBucket,
                CleanBucket = DemoContract.SourceCleanBucket,
                AccessKey = sourceAccessKey,
                SecretKey = sourceSecretKey,
                AllowInsecureTransport = true,
                AllowedUploadOrigins = [BaseAddress.AbsoluteUri.TrimEnd('/')]
            })),
        new ClamAvScanner(Options.Create(new EvidenceScannerOptions
        {
            Host = "127.0.0.1",
            Port = scanner!.GetMappedPublicPort(3310)
        })));
}
