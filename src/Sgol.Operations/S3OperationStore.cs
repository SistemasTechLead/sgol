using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace Sgol.Operations;

public sealed class S3OperationStore(IAmazonS3 client)
{
    internal bool CaptureReplicaWriteOperation { get; init; }
    internal string ReplicaWriteOperation { get; private set; } = "UNKNOWN";

    private void MarkReplicaWriteOperation(string operation)
    {
        if (CaptureReplicaWriteOperation)
            ReplicaWriteOperation = operation;
    }

    public async Task PutFileVerifiedAsync(
        string bucket,
        string key,
        string path,
        string sha256,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await PutStreamOnceAsync(bucket, key, input, sha256, input.Length, contentType, null, cancellationToken);
        await VerifyAsync(bucket, key, sha256, input.Length, cancellationToken);
    }

    public async Task PutBytesVerifiedAsync(
        string bucket,
        string key,
        byte[] content,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
        await using var input = new MemoryStream(content, writable: false);
        await PutStreamOnceAsync(bucket, key, input, sha256, content.LongLength, contentType, metadata, cancellationToken);
        await VerifyAsync(bucket, key, sha256, content.LongLength, cancellationToken);
    }

    public async Task PutStreamVerifiedAsync(
        string bucket,
        string key,
        Stream content,
        long size,
        string sha256,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        await PutStreamOnceAsync(bucket, key, content, sha256, size, contentType, metadata, cancellationToken);
        await VerifyAsync(bucket, key, sha256, size, cancellationToken);
    }

    public async Task<(byte[] Content, string Sha256)> ReadAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken);
        await using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        var content = memory.ToArray();
        return (content, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)));
    }

    public async Task<(byte[] Content, string Sha256)?> TryReadAsync(
        string bucket,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ReadAsync(bucket, key, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DownloadVerifiedAsync(
        string bucket,
        string key,
        string path,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken);
        await using var output = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long size = 0;
        int read;
        while ((read = await response.ResponseStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer.AsSpan(0, read));
            size += read;
        }

        await output.FlushAsync(cancellationToken);
        var actualHash = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (size != expectedSize || !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new OperationsIntegrityException("BACKUP_HASH_MISMATCH");
        }
    }

    public async Task VerifyObjectAsync(
        string bucket,
        string key,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        MarkReplicaWriteOperation("GET_DESTINATION_VERIFY");
        using var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = bucket, Key = key }, cancellationToken);
        var (actualHash, actualSize) = await OperationManifestSerializer.HashAsync(
            response.ResponseStream, cancellationToken);
        MarkReplicaWriteOperation("UNKNOWN");
        if (actualSize != expectedSize || !string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new OperationsIntegrityException("S3_OBJECT_VERIFICATION_FAILED");
        }
    }

    public async Task<bool> ExistsWithHashAsync(
        string bucket,
        string key,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        try
        {
            MarkReplicaWriteOperation("HEAD_DESTINATION_METADATA");
            var metadata = await client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = bucket, Key = key }, cancellationToken);
            MarkReplicaWriteOperation("UNKNOWN");
            return metadata.ContentLength == expectedSize &&
                string.Equals(metadata.Metadata["x-amz-meta-sha256"], expectedHash, StringComparison.Ordinal);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            MarkReplicaWriteOperation("UNKNOWN");
            return false;
        }
    }

    private async Task PutStreamOnceAsync(
        string bucket,
        string key,
        Stream input,
        string sha256,
        long size,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = input,
                AutoCloseStream = false,
                ContentType = contentType,
                IfNoneMatch = "*"
            };
            request.Metadata["sha256"] = sha256;
            if (metadata is not null)
            {
                foreach (var item in metadata)
                {
                    request.Metadata[item.Key] = item.Value;
                }
            }
            request.Headers.ContentLength = size;
            MarkReplicaWriteOperation("PUT_DESTINATION");
            await client.PutObjectAsync(request, cancellationToken);
            MarkReplicaWriteOperation("UNKNOWN");
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            if (!await ExistsWithHashAsync(bucket, key, sha256, size, cancellationToken))
            {
                throw new OperationsIntegrityException("IMMUTABLE_OBJECT_CONFLICT");
            }
        }
    }

    private async Task VerifyAsync(
        string bucket,
        string key,
        string expectedHash,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        await VerifyObjectAsync(bucket, key, expectedHash, expectedSize, cancellationToken);
        if (!await ExistsWithHashAsync(bucket, key, expectedHash, expectedSize, cancellationToken))
        {
            throw new OperationsIntegrityException("S3_WRITE_VERIFICATION_FAILED");
        }
    }
}
