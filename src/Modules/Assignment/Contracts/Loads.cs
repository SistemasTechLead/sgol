using System.Text.Json;

namespace Sgol.Assignment.Contracts;

public static class ActiveLoadAuthorization
{
    public const string View = "PER-CARGA-VER";
}

public static class AssignmentVersionStatuses
{
    public const string Current = "VIGENTE";
    public const string Superseded = "SUSTITUIDA";
}

public static class AssignmentTypes
{
    public const string Automatic = "AUTOMATICA";
    public const string Correction = "CORRECCION";
}

public sealed class AssignmentVersion
{
    private AssignmentVersion()
    {
    }

    public AssignmentVersion(
        Guid id,
        Guid obligationId,
        Guid personId,
        string status,
        string assignmentType,
        JsonDocument explanation,
        DateTimeOffset assignedAt,
        string? reason = null,
        Guid? assignedBy = null,
        Guid? supersedesId = null)
    {
        ArgumentNullException.ThrowIfNull(explanation);
        if (status is not AssignmentVersionStatuses.Current and not AssignmentVersionStatuses.Superseded)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (assignmentType is not AssignmentTypes.Automatic and not AssignmentTypes.Correction)
        {
            throw new ArgumentOutOfRangeException(nameof(assignmentType));
        }

        if (explanation.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Assignment explanation must be a JSON object.", nameof(explanation));
        }

        var isCorrection = assignmentType == AssignmentTypes.Correction;
        if (isCorrection != (!string.IsNullOrWhiteSpace(reason) && assignedBy is not null && supersedesId is not null))
        {
            throw new ArgumentException("Correction metadata must match the assignment type.");
        }

        if (!isCorrection && (reason is not null || assignedBy is not null || supersedesId is not null))
        {
            throw new ArgumentException("Automatic assignments cannot contain correction metadata.");
        }

        if (id == supersedesId)
        {
            throw new ArgumentException("An assignment version cannot supersede itself.", nameof(supersedesId));
        }

        Id = id;
        ObligationId = obligationId;
        PersonId = personId;
        Status = status;
        AssignmentType = assignmentType;
        Explanation = explanation;
        Reason = reason;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
        SupersedesId = supersedesId;
    }

    public Guid Id { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid PersonId { get; private init; }
    public string Status { get; private init; } = null!;
    public string AssignmentType { get; private init; } = null!;
    public JsonDocument Explanation { get; private init; } = null!;
    public string? Reason { get; private init; }
    public Guid? AssignedBy { get; private init; }
    public DateTimeOffset AssignedAt { get; private init; }
    public Guid? SupersedesId { get; private init; }
}

public sealed record ActiveLoadPerson(Guid Id, string StableCode, string DisplayName);

public sealed record ActiveLoadItem(
    ActiveLoadPerson Person,
    int ActiveLoad,
    DateTimeOffset CalculatedAt);

public sealed record ActiveLoadPage(
    IReadOnlyList<ActiveLoadItem> Items,
    string? NextCursor);

public sealed record ActiveLoadRequest(
    Guid ActorUserId,
    string? Cursor,
    int Limit,
    Guid? PersonId);

public interface IActiveLoadReader
{
    Task<ActiveLoadPage> ListAsync(
        ActiveLoadRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ActiveLoadAccessDeniedException()
    : Exception($"{ActiveLoadAuthorization.View} is required for LOR-001.");

public sealed class ActiveLoadFilterInvalidException()
    : Exception("The active-load query parameters are invalid.");
