using System.Diagnostics.CodeAnalysis;

namespace Sgol.Evidence.Contracts;

public static class EvidenceFileLimits
{
    public const long MinimumBytes = 1;
    public const long MaximumBytes = 15 * 1024 * 1024;
    public const int BufferBytes = 64 * 1024;
}

public enum EvidenceMediaType
{
    Jpeg,
    Png,
    Pdf
}

public enum EvidenceScanResult
{
    Limpio,
    Infectado,
    Invalido,
    ErrorEscaneo
}

public enum EvidenceStorageArea
{
    Quarantine,
    Clean
}

public sealed record EvidenceObjectKey
{
    private const int TokenLength = 64;

    private EvidenceObjectKey(string value) => Value = value;

    public string Value { get; }

    public static EvidenceObjectKey Parse(string value)
    {
        if (!TryParse(value, out var key))
        {
            throw new ArgumentException("The evidence object key is invalid.", nameof(value));
        }

        return key;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out EvidenceObjectKey? key)
    {
        key = null;
        if (value is null || value.Length != 3 + 2 + 1 + 2 + 1 + TokenLength)
        {
            return false;
        }

        var token = value.AsSpan(9);
        if (!value.StartsWith("v1/", StringComparison.Ordinal) ||
            value[5] != '/' ||
            value[8] != '/' ||
            !value.AsSpan(3, 2).SequenceEqual(token[..2]) ||
            !value.AsSpan(6, 2).SequenceEqual(token.Slice(2, 2)) ||
            !token.ContainsOnlyLowercaseHex())
        {
            return false;
        }

        key = new EvidenceObjectKey(value);
        return true;
    }

    public override string ToString() => "EvidenceObjectKey";
}

public sealed record EvidenceObjectMetadata(
    EvidenceObjectKey Key,
    long SizeBytes,
    string Sha256,
    EvidenceMediaType MediaType);

public sealed record EvidenceScanOutcome(
    EvidenceScanResult Result,
    string? EngineVersion = null,
    string? ErrorCode = null);

public sealed class ValidatedEvidenceFile : IAsyncDisposable
{
    internal ValidatedEvidenceFile(
        FileStream content,
        EvidenceMediaType mediaType,
        long sizeBytes,
        string sha256)
    {
        Content = content;
        MediaType = mediaType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    public FileStream Content { get; }

    public EvidenceMediaType MediaType { get; }

    public long SizeBytes { get; }

    public string Sha256 { get; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed record FileTechnicalValidationResult(
    bool IsValid,
    ValidatedEvidenceFile? File,
    string? ErrorCode)
{
    public static FileTechnicalValidationResult Invalid(string errorCode) => new(false, null, errorCode);

    public static FileTechnicalValidationResult Valid(ValidatedEvidenceFile file) => new(true, file, null);
}

public interface IEvidenceObjectKeyFactory
{
    EvidenceObjectKey Create();
}

public interface IPrivateObjectStorage
{
    Task PutQuarantineAsync(
        EvidenceObjectMetadata metadata,
        Stream content,
        CancellationToken cancellationToken);

    Task<EvidenceObjectMetadata> GetMetadataAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken);

    Task PromoteToCleanAsync(
        EvidenceObjectMetadata expected,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        EvidenceStorageArea area,
        EvidenceObjectKey key,
        CancellationToken cancellationToken);

    Task CheckAvailabilityAsync(CancellationToken cancellationToken);
}

public interface IFileMalwareScanner
{
    Task<EvidenceScanOutcome> ScanAsync(
        Stream validatedContent,
        long sizeBytes,
        CancellationToken cancellationToken);

    Task CheckAvailabilityAsync(CancellationToken cancellationToken);
}

public interface IFileTechnicalValidator
{
    Task<FileTechnicalValidationResult> ValidateAsync(
        Stream content,
        string declaredMediaType,
        string originalFileName,
        CancellationToken cancellationToken);
}

public static class EvidenceContractExtensions
{
    public static string ToMediaType(this EvidenceMediaType mediaType) => mediaType switch
    {
        EvidenceMediaType.Jpeg => "image/jpeg",
        EvidenceMediaType.Png => "image/png",
        EvidenceMediaType.Pdf => "application/pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(mediaType))
    };

    internal static bool ContainsOnlyLowercaseHex(this ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
