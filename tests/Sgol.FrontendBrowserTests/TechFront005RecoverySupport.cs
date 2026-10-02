using Microsoft.Extensions.Options;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05Infrastructure
{
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
