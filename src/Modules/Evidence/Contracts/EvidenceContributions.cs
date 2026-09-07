namespace Sgol.Evidence.Contracts;

public static class EvidenceAuthorization
{
    public const string Contribute = "PER-EVIDENCIA-APORTAR";
    public const string Replace = "PER-EVIDENCIA-SUSTITUIR";
}

public static class EvidenceFileStatuses
{
    public const string Pending = "PENDIENTE";
    public const string Clean = "LIMPIO";
    public const string Infected = "INFECTADO";
    public const string Invalid = "INVALIDO";
    public const string ScanError = "ERROR_ESCANEO";
}

public static class EvidenceBucketClasses
{
    public const string Quarantine = "QUARANTINE";
    public const string Clean = "CLEAN";
}

public static class EvidenceVersionStatuses
{
    public const string Current = "VIGENTE";
    public const string Superseded = "SUSTITUIDA";
}

public sealed class FileObject
{
    private FileObject() { }

    public FileObject(
        Guid id,
        Guid branchId,
        Guid obligationId,
        Guid evidencePolicyVersionId,
        Guid requirementVersionId,
        string requirementCode,
        string requirementKind,
        string? documentSubtype,
        string objectKey,
        string originalName,
        string declaredMediaType,
        long sizeBytes,
        string sha256,
        Guid uploadedBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        BranchId = branchId;
        ObligationId = obligationId;
        EvidencePolicyVersionId = evidencePolicyVersionId;
        RequirementVersionId = requirementVersionId;
        RequirementCode = requirementCode;
        RequirementKind = requirementKind;
        DocumentSubtype = documentSubtype;
        BucketClass = EvidenceBucketClasses.Quarantine;
        ObjectKey = objectKey;
        OriginalName = objectName(originalName);
        DeclaredMediaType = declaredMediaType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        ScanStatus = EvidenceFileStatuses.Pending;
        UploadedBy = uploadedBy;
        CreatedAt = createdAt;
        UploadExpiresAt = createdAt.AddMinutes(10);
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid BranchId { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid EvidencePolicyVersionId { get; private init; }
    public Guid RequirementVersionId { get; private init; }
    public string RequirementCode { get; private init; } = null!;
    public string RequirementKind { get; private init; } = null!;
    public string? DocumentSubtype { get; private init; }
    public string BucketClass { get; private set; } = null!;
    public string ObjectKey { get; private init; } = null!;
    public string OriginalName { get; private init; } = null!;
    public string DeclaredMediaType { get; private init; } = null!;
    public string? DetectedMediaType { get; private set; }
    public long SizeBytes { get; private init; }
    public string Sha256 { get; private init; } = null!;
    public string ScanStatus { get; private set; } = null!;
    public string? ScanEngine { get; private set; }
    public string? ScanErrorCode { get; private set; }
    public DateTimeOffset? ScannedAt { get; private set; }
    public Guid UploadedBy { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UploadExpiresAt { get; private init; }
    public DateTimeOffset? UploadCompletedAt { get; private set; }
    public DateTimeOffset? LinkExpiresAt { get; private set; }
    public Guid? LinkedEvidenceItemId { get; private set; }
    public DateTimeOffset? ReplicatedAt { get; private init; }
    public long RowVersion { get; private set; }

    public void ConfirmUpload(DateTimeOffset at)
    {
        if (ScanStatus != EvidenceFileStatuses.Pending || UploadCompletedAt is not null || at > UploadExpiresAt)
        {
            throw new EvidenceFileStateException();
        }

        UploadCompletedAt = at;
        RowVersion++;
    }

    public void MarkClean(string detectedMediaType, string? engine, DateTimeOffset at)
    {
        EnsurePendingConfirmed();
        DetectedMediaType = detectedMediaType;
        ScanStatus = EvidenceFileStatuses.Clean;
        BucketClass = EvidenceBucketClasses.Clean;
        ScanEngine = engine;
        ScannedAt = at;
        LinkExpiresAt = at.AddHours(24);
        RowVersion++;
    }

    public void MarkTerminal(string status, string? detectedMediaType, string? engine, string errorCode, DateTimeOffset at)
    {
        if (status is not (EvidenceFileStatuses.Infected or EvidenceFileStatuses.Invalid or EvidenceFileStatuses.ScanError))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (ScanStatus != EvidenceFileStatuses.Pending)
        {
            throw new EvidenceFileStateException();
        }

        DetectedMediaType = detectedMediaType;
        ScanStatus = status;
        ScanEngine = engine;
        ScanErrorCode = errorCode;
        ScannedAt = at;
        RowVersion++;
    }

    public void Link(Guid evidenceItemId, DateTimeOffset at)
    {
        if (ScanStatus != EvidenceFileStatuses.Clean || BucketClass != EvidenceBucketClasses.Clean ||
            LinkedEvidenceItemId is not null || LinkExpiresAt is null || at > LinkExpiresAt)
        {
            throw new EvidenceFileNotCleanException();
        }

        LinkedEvidenceItemId = evidenceItemId;
        RowVersion++;
    }

    public void ExpireUnlinked(string errorCode, DateTimeOffset at)
    {
        if (LinkedEvidenceItemId is not null ||
            !((ScanStatus == EvidenceFileStatuses.Pending && UploadCompletedAt is null && UploadExpiresAt <= at) ||
              (ScanStatus == EvidenceFileStatuses.Clean && LinkExpiresAt <= at)))
        {
            throw new EvidenceFileStateException();
        }

        ScanStatus = EvidenceFileStatuses.Invalid;
        BucketClass = EvidenceBucketClasses.Quarantine;
        ScanErrorCode = errorCode;
        ScannedAt ??= at;
        LinkExpiresAt = null;
        RowVersion++;
    }

    private void EnsurePendingConfirmed()
    {
        if (ScanStatus != EvidenceFileStatuses.Pending || UploadCompletedAt is null)
        {
            throw new EvidenceFileStateException();
        }
    }

    private static string objectName(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Original name is required.", nameof(value)) : value;
}

public sealed class EvidenceItem
{
    private EvidenceItem() { }

    public EvidenceItem(Guid id, Guid obligationId, Guid policyVersionId, Guid requirementVersionId,
        string requirementCode, string requirementKind, DateTimeOffset createdAt)
    {
        Id = id;
        ObligationId = obligationId;
        EvidencePolicyVersionId = policyVersionId;
        RequirementVersionId = requirementVersionId;
        RequirementCode = requirementCode;
        RequirementKind = requirementKind;
        CreatedAt = createdAt;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid EvidencePolicyVersionId { get; private init; }
    public Guid RequirementVersionId { get; private init; }
    public string RequirementCode { get; private init; } = null!;
    public string RequirementKind { get; private init; } = null!;
    public DateTimeOffset CreatedAt { get; private init; }
    public long RowVersion { get; private set; }

    public void Advance(long expected)
    {
        if (RowVersion != expected) throw new EvidenceVersionConflictException();
        RowVersion++;
    }
}

public sealed class EvidenceVersion
{
    private EvidenceVersion() { }

    public EvidenceVersion(Guid id, Guid itemId, int versionNo, Guid fileObjectId, Guid submittedBy,
        DateTimeOffset submittedAt, string? reason = null, Guid? supersedesId = null)
    {
        if (versionNo < 1 || (versionNo == 1) != (supersedesId is null)) throw new ArgumentException("Invalid evidence chain.");
        Id = id;
        EvidenceItemId = itemId;
        VersionNo = versionNo;
        FileObjectId = fileObjectId;
        Status = EvidenceVersionStatuses.Current;
        SubmittedBy = submittedBy;
        SubmittedAt = submittedAt;
        Reason = reason;
        SupersedesId = supersedesId;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid EvidenceItemId { get; private init; }
    public int VersionNo { get; private init; }
    public Guid FileObjectId { get; private init; }
    public System.Text.Json.JsonDocument? StructuredPayload { get; private init; }
    public string Status { get; private set; } = null!;
    public Guid SubmittedBy { get; private init; }
    public DateTimeOffset SubmittedAt { get; private init; }
    public string? Reason { get; private init; }
    public Guid? SupersedesId { get; private init; }
    public long RowVersion { get; private set; }

    public void Supersede()
    {
        if (Status != EvidenceVersionStatuses.Current) throw new EvidenceFileStateException();
        Status = EvidenceVersionStatuses.Superseded;
        RowVersion++;
    }
}

public sealed record EvidenceUploadHeaders(string ContentType, string ContentLength, string IfNoneMatch,
    string Sha256, string MediaType, string SizeBytes);
public sealed record EvidenceUploadAuthorization(Uri Url, DateTimeOffset ExpiresAt, EvidenceUploadHeaders Headers);
public sealed record CreateEvidenceUploadCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId,
    Guid ObligationId, string RequirementCode, string OriginalFileName, string DeclaredMediaType,
    long SizeBytes, string Sha256, string? DocumentSubtype);
public sealed record EvidenceUploadIntentResult(Guid FileId, string Status, EvidenceUploadAuthorization Upload, bool Replayed);
public sealed record CompleteEvidenceUploadCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId, Guid FileId);
public sealed record EvidenceFileStatusDetails(Guid FileId, string Status, string OriginalFileName,
    string DeclaredMediaType, string? DetectedMediaType, long SizeBytes, string Sha256,
    DateTimeOffset CreatedAt, DateTimeOffset UploadExpiresAt, DateTimeOffset? UploadedAt,
    DateTimeOffset? ScannedAt, string? FailureCode, Guid? LinkedEvidenceItemId);
public sealed record ContributeEvidenceCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId,
    Guid ObligationId, string RequirementCode, Guid FileId);
public sealed record ReplaceEvidenceCommand(Guid ActorUserId, Guid IdempotencyKey, Guid CorrelationId,
    Guid ObligationId, Guid EvidenceItemId, Guid FileId, string? Reason, long ExpectedRowVersion);
public sealed record EvidenceRequirementDetails(Guid RequirementVersionId, string RequirementCode, string Kind);
public sealed record EvidenceVersionDetails(Guid EvidenceVersionId, int VersionNo, string Status,
    Guid SubmittedByUserId, DateTimeOffset SubmittedAt, string? Reason, Guid? SupersedesEvidenceVersionId);
public sealed record EvidenceFileDetails(Guid FileId, string OriginalFileName, string MediaType,
    long SizeBytes, string Sha256, string? DocumentSubtype);
public sealed record EvidenceDetails(Guid EvidenceItemId, long ItemRowVersion, EvidenceRequirementDetails Requirement,
    EvidenceVersionDetails Version, EvidenceFileDetails File);
public sealed record EvidencePage(IReadOnlyList<EvidenceDetails> Items, string? NextCursor);
public sealed record EvidenceQuery(Guid ActorUserId, Guid ObligationId, string? RequirementCode,
    string? Status, string? Cursor, int Limit);

public interface IEvidenceContributionService
{
    Task<EvidenceUploadIntentResult> CreateUploadIntentAsync(CreateEvidenceUploadCommand command, CancellationToken cancellationToken = default);
    Task<EvidenceFileStatusDetails> CompleteUploadAsync(CompleteEvidenceUploadCommand command, CancellationToken cancellationToken = default);
    Task<EvidenceFileStatusDetails> GetFileStatusAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default);
    Task<EvidenceDetails> ContributeAsync(ContributeEvidenceCommand command, CancellationToken cancellationToken = default);
    Task<EvidenceDetails> ReplaceAsync(ReplaceEvidenceCommand command, CancellationToken cancellationToken = default);
    Task<EvidencePage> ListAsync(EvidenceQuery query, CancellationToken cancellationToken = default);
}

public sealed class EvidenceAccessDeniedException() : Exception;
public sealed class EvidenceObligationNotFoundException() : Exception;
public sealed class EvidenceFileNotFoundException() : Exception;
public sealed class EvidenceItemNotFoundException() : Exception;
public sealed class EvidenceRequestInvalidException() : Exception;
public sealed class EvidenceRequirementInvalidException() : Exception;
public sealed class EvidenceConditionalRequirementException() : Exception;
public sealed class EvidenceTypeNotImplementedException() : Exception;
public sealed class EvidenceUnsupportedMediaTypeException() : Exception;
public sealed class EvidenceFileTooLargeException() : Exception;
public sealed class EvidenceUploadExpiredException() : Exception;
public sealed class EvidenceUploadMissingException() : Exception;
public sealed class EvidenceFileNotCleanException() : Exception;
public sealed class EvidenceFileStateException() : Exception;
public sealed class EvidenceAlreadyExistsException() : Exception;
public sealed class EvidenceFileAlreadyLinkedException() : Exception;
public sealed class EvidenceVersionConflictException() : Exception;
public sealed class EvidenceReasonRequiredException() : Exception;
public sealed class EvidenceReplacementNotAllowedException() : Exception;
public sealed class EvidenceIdempotencyConflictException() : Exception;
public sealed class EvidenceRateLimitException() : Exception;
