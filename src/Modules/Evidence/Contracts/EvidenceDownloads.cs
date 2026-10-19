namespace Sgol.Evidence.Contracts;

public interface IEvidenceDownloadService
{
    Task RequireActorAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<EvidenceDownloadDetails> AuthorizeAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default);
}

public sealed class EvidenceDownloadAuthorization(Uri url, DateTimeOffset expiresAt)
{
    public Uri Url { get; } = url;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => nameof(EvidenceDownloadAuthorization);
}

public sealed class EvidenceDownloadDetails(Guid fileId, EvidenceDownloadAuthorization download)
{
    public Guid FileId { get; } = fileId;
    public EvidenceDownloadAuthorization Download { get; } = download;
    public override string ToString() => nameof(EvidenceDownloadDetails);
}

public sealed class EvidenceDownloadException(int statusCode, string code) : Exception(code)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
