using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05Infrastructure
{
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

    internal void TrustFrontendCertificate()
    {
        if (certificate is null) throw new InvalidOperationException("Owned HTTPS certificate unavailable.");
        using var roots = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        roots.Open(OpenFlags.ReadWrite);
        if (roots.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count != 0)
            throw new InvalidOperationException("Owned certificate already present.");
        roots.Add(certificate);
        frontendCertificateTrusted = true;
    }

    internal bool CleanupFrontendCertificate()
    {
        if (!frontendCertificateTrusted) return true;
        if (certificate is null) return false;
        using var roots = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        roots.Open(OpenFlags.ReadWrite);
        roots.Remove(certificate);
        var removed = roots.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count == 0;
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
