using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

internal sealed class UnavailablePrivateObjectStorage : IPrivateObjectStorage
{
    public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(EvidenceObjectMetadata metadata, DateTimeOffset expiresAt, CancellationToken cancellationToken) => Fail<EvidenceUploadAuthorization>();
    public Task PutQuarantineAsync(EvidenceObjectMetadata metadata, Stream content, CancellationToken cancellationToken) => Fail();
    public Task<EvidenceObjectMetadata> GetMetadataAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => Fail<EvidenceObjectMetadata>();
    public Task<Stream> OpenReadAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => Fail<Stream>();
    public Task PromoteToCleanAsync(EvidenceObjectMetadata expected, CancellationToken cancellationToken) => Fail();
    public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => Fail();
    public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => Fail();
    private static Task Fail() => Task.FromException(new EvidenceStorageUnavailableException());
    private static Task<T> Fail<T>() => Task.FromException<T>(new EvidenceStorageUnavailableException());
}

internal sealed class UnavailableMalwareScanner : IFileMalwareScanner
{
    public Task<EvidenceScanOutcome> ScanAsync(Stream validatedContent, long sizeBytes, CancellationToken cancellationToken) =>
        Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.ErrorEscaneo, ErrorCode: "UNAVAILABLE"));
    public Task CheckAvailabilityAsync(CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException("Evidence scanner configuration is unavailable."));
}
