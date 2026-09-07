using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class S3PrivateObjectStorage(
    IAmazonS3 client,
    IOptions<EvidenceStorageOptions> options) : IPrivateObjectStorage
{
    private const string HashMetadata = "x-amz-meta-sgol-sha256";
    private const string MediaTypeMetadata = "x-amz-meta-sgol-media-type";
    private const string SizeMetadata = "x-amz-meta-sgol-size-bytes";
    private static readonly string[] UploadCorsHeaders =
    [
        "Content-Type", "Content-Length", "If-None-Match",
        HashMetadata, MediaTypeMetadata, SizeMetadata
    ];
    private readonly EvidenceStorageOptions configuration = options.Value;
    private readonly object corsVerificationLock = new();
    private Task? corsVerification;

    public async Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(
        EvidenceObjectMetadata metadata,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureQuarantineCorsAsync(cancellationToken);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = configuration.QuarantineBucket,
            Key = metadata.Key.Value,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = metadata.MediaType.ToMediaType(),
            Protocol = new Uri(configuration.Endpoint).Scheme == Uri.UriSchemeHttp
                ? Protocol.HTTP
                : Protocol.HTTPS,
        };
        request.Headers["Content-Length"] = metadata.SizeBytes.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        request.Headers["If-None-Match"] = "*";
        request.Metadata[HashMetadata] = metadata.Sha256;
        request.Metadata[MediaTypeMetadata] = metadata.MediaType.ToString();
        request.Metadata[SizeMetadata] = metadata.SizeBytes.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        var url = client.GetPreSignedURL(request);
        return new EvidenceUploadAuthorization(
            new Uri(url, UriKind.Absolute),
            expiresAt,
            new EvidenceUploadHeaders(
                metadata.MediaType.ToMediaType(),
                metadata.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "*",
                metadata.Sha256,
                metadata.MediaType.ToString(),
                metadata.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private async Task EnsureQuarantineCorsAsync(CancellationToken cancellationToken)
    {
        Task verification;
        lock (corsVerificationLock)
        {
            verification = corsVerification ??= VerifyQuarantineCorsAsync();
        }

        try
        {
            await verification.WaitAsync(cancellationToken);
        }
        catch when (verification.IsFaulted)
        {
            lock (corsVerificationLock)
            {
                if (ReferenceEquals(corsVerification, verification)) corsVerification = null;
            }
            throw;
        }
    }

    private async Task VerifyQuarantineCorsAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var response = await client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest
            {
                BucketName = configuration.QuarantineBucket
            }, timeout.Token);
            var rules = response.Configuration?.Rules;
            if (rules is null || rules.Count != 1)
            {
                throw new EvidenceStorageUnavailableException();
            }

            var rule = rules[0];
            if (rule.Id != "sgol-evidence-upload" || rule.MaxAgeSeconds != 600 ||
                !ExactSet(rule.AllowedOrigins, configuration.AllowedUploadOrigins, StringComparer.Ordinal) ||
                !ExactSet(rule.AllowedMethods, ["PUT"], StringComparer.Ordinal) ||
                !ExactSet(rule.AllowedHeaders, UploadCorsHeaders, StringComparer.OrdinalIgnoreCase) ||
                rule.ExposeHeaders is { Count: > 0 })
            {
                throw new EvidenceStorageUnavailableException();
            }
        }
        catch (Exception exception) when (exception is AmazonS3Exception or OperationCanceledException)
        {
            throw new EvidenceStorageUnavailableException();
        }
    }

    private static bool ExactSet(
        List<string> actual,
        string[] expected,
        StringComparer comparer) =>
        actual.Count == expected.Length && new HashSet<string>(actual, comparer).SetEquals(expected);

    public async Task PutQuarantineAsync(
        EvidenceObjectMetadata metadata,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(30), cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = configuration.QuarantineBucket,
                Key = metadata.Key.Value,
                InputStream = content,
                AutoCloseStream = false,
                ContentType = metadata.MediaType.ToMediaType(),
                IfNoneMatch = "*"
            };
            request.Metadata[HashMetadata] = metadata.Sha256;
            request.Metadata[MediaTypeMetadata] = metadata.MediaType.ToString();
            request.Metadata[SizeMetadata] = metadata.SizeBytes.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            request.Headers.ContentLength = metadata.SizeBytes;

            await client.PutObjectAsync(request, timeout.Token);
            EvidenceTelemetry.QuarantineEntered.Add(1, tag: new("operation", "put"));
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            throw new EvidenceObjectAlreadyExistsException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EvidenceStorageUnavailableException();
        }
        catch (AmazonS3Exception)
        {
            throw new EvidenceStorageUnavailableException();
        }
        finally
        {
            EvidenceTelemetry.StorageDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                tag: new("operation", "put"));
        }
    }

    public async Task<EvidenceObjectMetadata> GetMetadataAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(10), cancellationToken);
        try
        {
            var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = GetBucket(area),
                Key = key.Value
            }, timeout.Token);

            if (!TryReadMetadata(response.Metadata, key, response.ContentLength, out var metadata))
            {
                throw new EvidenceObjectIntegrityException();
            }

            return metadata;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new EvidenceObjectNotFoundException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EvidenceStorageUnavailableException();
        }
        catch (AmazonS3Exception)
        {
            throw new EvidenceStorageUnavailableException();
        }
    }

    public async Task<Stream> OpenReadAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken)
    {
        var timeout = CreateTimeout(TimeSpan.FromSeconds(30), cancellationToken);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = GetBucket(area),
                Key = key.Value
            }, timeout.Token);
            return new ResponseDisposingStream(response, timeout);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            timeout.Dispose();
            throw new EvidenceObjectNotFoundException();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timeout.Dispose();
            throw new EvidenceStorageUnavailableException();
        }
        catch (AmazonS3Exception)
        {
            timeout.Dispose();
            throw new EvidenceStorageUnavailableException();
        }
        catch
        {
            timeout.Dispose();
            throw;
        }
    }

    public async Task PromoteToCleanAsync(
        EvidenceObjectMetadata expected,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(30), cancellationToken);
        try
        {
            await client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = configuration.QuarantineBucket,
                SourceKey = expected.Key.Value,
                DestinationBucket = configuration.CleanBucket,
                DestinationKey = expected.Key.Value,
                MetadataDirective = S3MetadataDirective.COPY
            }, timeout.Token);

            var metadata = await GetMetadataAsync(EvidenceStorageArea.Clean, expected.Key, cancellationToken);
            if (metadata != expected || !await HasExpectedHashAsync(expected, cancellationToken))
            {
                throw new EvidenceObjectIntegrityException();
            }

            await DeleteAsync(EvidenceStorageArea.Quarantine, expected.Key, cancellationToken);
            EvidenceTelemetry.QuarantineExited.Add(1, tag: new("operation", "promote"));
            EvidenceTelemetry.Promotions.Add(1, tag: new("result", "LIMPIO"));
        }
        catch
        {
            await TryDeleteCleanCopyAsync(expected.Key);
            throw;
        }
    }

    public async Task DeleteAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(10), cancellationToken);
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = GetBucket(area),
                Key = key.Value
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EvidenceStorageUnavailableException();
        }
        catch (AmazonS3Exception)
        {
            throw new EvidenceStorageUnavailableException();
        }
    }

    public async Task CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(TimeSpan.FromSeconds(10), cancellationToken);
        try
        {
            await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = configuration.QuarantineBucket,
                MaxKeys = 1
            }, timeout.Token);
            await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = configuration.CleanBucket,
                MaxKeys = 1
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EvidenceStorageUnavailableException();
        }
        catch (AmazonS3Exception)
        {
            throw new EvidenceStorageUnavailableException();
        }
    }

    private async Task<bool> HasExpectedHashAsync(
        EvidenceObjectMetadata expected,
        CancellationToken cancellationToken)
    {
        await using var stream = await OpenReadAsync(EvidenceStorageArea.Clean, expected.Key, cancellationToken);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[EvidenceFileLimits.BufferBytes];
        long size = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            size += read;
            if (size > EvidenceFileLimits.MaximumBytes)
            {
                return false;
            }

            hash.AppendData(buffer.AsSpan(0, read));
        }

        return size == expected.SizeBytes &&
               Convert.ToHexStringLower(hash.GetHashAndReset()) == expected.Sha256;
    }

    private static bool TryReadMetadata(
        MetadataCollection collection,
        EvidenceObjectKey key,
        long contentLength,
        out EvidenceObjectMetadata metadata)
    {
        metadata = default!;
        var hash = collection[HashMetadata];
        var media = collection[MediaTypeMetadata];
        var sizeText = collection[SizeMetadata];
        if (hash is null || hash.Length != 64 || hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            !Enum.TryParse<EvidenceMediaType>(media, out var mediaType) ||
            !long.TryParse(sizeText, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var size) ||
            size != contentLength || size is < EvidenceFileLimits.MinimumBytes or > EvidenceFileLimits.MaximumBytes)
        {
            return false;
        }

        metadata = new EvidenceObjectMetadata(key, size, hash, mediaType);
        return true;
    }

    private async Task TryDeleteCleanCopyAsync(EvidenceObjectKey key)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.DeleteObjectAsync(configuration.CleanBucket, key.Value, timeout.Token);
        }
        catch (Exception exception) when (exception is AmazonS3Exception or OperationCanceledException)
        {
            // Best effort is intentionally silent; quarantine remains the authority.
        }
    }

    private string GetBucket(EvidenceStorageArea area) => area switch
    {
        EvidenceStorageArea.Quarantine => configuration.QuarantineBucket,
        EvidenceStorageArea.Clean => configuration.CleanBucket,
        _ => throw new ArgumentOutOfRangeException(nameof(area))
    };

    private static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private sealed class ResponseDisposingStream(
        GetObjectResponse response,
        CancellationTokenSource timeout) : Stream
    {
        private readonly Stream inner = response.ResponseStream;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return inner.ReadAsync(buffer, timeout.Token);
            }

            return ReadWithLinkedCancellationAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timeout.Dispose();
                response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            timeout.Dispose();
            response.Dispose();
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        private async ValueTask<int> ReadWithLinkedCancellationAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                timeout.Token,
                cancellationToken);
            return await inner.ReadAsync(buffer, linked.Token);
        }
    }
}
