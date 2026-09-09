namespace Sgol.Notifications.Contracts;

public static class InboxAuthorization
{
    public const string ViewOwn = "PER-BANDEJA-PROPIA";
}

public static class InternalNoticeTypes
{
    public const string ObligationAssigned = "OBLIGATION_ASSIGNED";
}

public static class InternalNoticeResourceTypes
{
    public const string AssignmentVersion = "ASSIGNMENT_VERSION";
}

public static class InternalNoticeStatuses
{
    public const string Unread = "UNREAD";
    public const string Read = "READ";
}

public static class InternalNoticeReadResults
{
    public const string MarkedRead = "MARKED_READ";
    public const string AlreadyRead = "ALREADY_READ";
}

public static class InboxTaskStates
{
    public const string Future = "FUTURA";
    public const string Available = "DISPONIBLE";
    public const string Overdue = "VENCIDA";
    public const string Concluded = "CONCLUIDA";

    public static string Classify(
        bool concluded,
        DateTimeOffset? dueAt,
        DateOnly periodStartsOn,
        DateOnly localToday,
        DateTimeOffset queriedAt) => concluded ? Concluded :
        dueAt is { } due && due < queriedAt ? Overdue :
        periodStartsOn > localToday ? Future : Available;

    public static int Rank(string state) => state switch
    {
        Overdue => 0,
        Available => 1,
        Future => 2,
        Concluded => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    public static string FromRank(int rank) => rank switch
    {
        0 => Overdue,
        1 => Available,
        2 => Future,
        3 => Concluded,
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };
}

public static class InboxActionCodes
{
    public const string ViewTask = "VIEW_TASK";
    public const string ContributeEvidence = "CONTRIBUTE_EVIDENCE";
    public const string ConcludeTask = "CONCLUDE_TASK";
    public const string MarkNoticeRead = "MARK_NOTICE_READ";
}

public sealed class InternalNotice
{
    private InternalNotice() { }

    public InternalNotice(Guid id, Guid recipientUserId, Guid assignmentVersionId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || recipientUserId == Guid.Empty || assignmentVersionId == Guid.Empty)
        {
            throw new ArgumentException("Internal-notice identifiers are required.");
        }

        Id = id;
        RecipientUserId = recipientUserId;
        NoticeType = InternalNoticeTypes.ObligationAssigned;
        ResourceType = InternalNoticeResourceTypes.AssignmentVersion;
        ResourceId = assignmentVersionId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }
    public Guid RecipientUserId { get; private init; }
    public string NoticeType { get; private init; } = null!;
    public string ResourceType { get; private init; } = null!;
    public Guid ResourceId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool MarkRead(DateTimeOffset readAt)
    {
        if (ReadAt is not null) return false;
        ArgumentOutOfRangeException.ThrowIfLessThan(readAt, CreatedAt);
        ReadAt = readAt;
        return true;
    }
}

public sealed record InboxPeriod(Guid PeriodId, int IsoYear, int IsoWeek, DateOnly StartsOn, DateOnly EndsOn, string TimeZone);
public sealed record InboxTaskDefinition(Guid TaskDefinitionId, string TaskCode, string Name);
public sealed record InboxTaskDates(DateTimeOffset? DueAt, DateOnly? DueLocalDate, DateTimeOffset? ConcludedAt);
public sealed record InboxMissingRequirement(Guid RequirementVersionId, string RequirementCode, string Kind, string ConditionCode, short Ordinal, string MissingReason);
public sealed record InboxEvidence(string Result, IReadOnlyList<InboxMissingRequirement> MissingRequirements);
public sealed record InboxTask(Guid ObligationId, InboxTaskDefinition Task, InboxPeriod Period, string OriginKind, bool Scheduled, InboxTaskDates Dates, string ExecutionStatus, string TaskState, InboxEvidence Evidence, IReadOnlyList<string> AllowedActions);
public sealed record InboxNoticeResource(string ResourceType, Guid ResourceId, bool Available, Guid? ObligationId, string? TaskCode, string? TaskName);
public sealed record InboxNotice(Guid NoticeId, string NoticeType, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, InboxNoticeResource Resource, IReadOnlyList<string> AllowedActions);
public sealed record InboxSection<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record InboxPage(InboxPeriod? Period, InboxSection<InboxTask> Tasks, InboxSection<InboxNotice> Notices, DateTimeOffset QueriedAt);
public sealed record InboxQuery(Guid ActorUserId, Guid? PeriodId, string? TaskState, string? TaskCursor, int TaskLimit, string NoticeStatus, string? NoticeCursor, int NoticeLimit);

public interface IInboxReader
{
    Task<InboxPage> ReadAsync(InboxQuery query, CancellationToken cancellationToken = default);
}

public interface IInternalNoticeWriter
{
    Task AddAssignmentNoticeAsync(Guid assignmentVersionId, Guid personId, DateTimeOffset createdAt, CancellationToken cancellationToken = default);
    void RecordAssignmentNoticeCommitted();
}

public sealed record ReadInternalNoticeCommand(Guid ActorUserId, Guid NoticeId, Guid CorrelationId);
public sealed record ReadInternalNoticeResult(Guid NoticeId, string Status, DateTimeOffset ReadAt, string Result);

public interface IInternalNoticeService
{
    Task<ReadInternalNoticeResult> MarkReadAsync(ReadInternalNoticeCommand command, CancellationToken cancellationToken = default);
}

public abstract class InboxException(string code, Exception? innerException = null) : Exception(code, innerException)
{
    public string Code { get; } = code;
}
public sealed class InboxAccessDeniedException() : InboxException("ACCESO_DENEGADO");
public sealed class InboxFilterInvalidException() : InboxException("FILTRO_BANDEJA_INVALIDO");
public sealed class InboxInconsistentException(Exception? innerException = null) : InboxException("BANDEJA_INCONSISTENTE", innerException);
public sealed class InternalNoticeNotFoundException() : InboxException("AVISO_NO_ENCONTRADO");
public sealed class InternalNoticeConcurrencyException() : InboxException("LECTURA_AVISO_CONCURRENCIA_CONFLICTO");
