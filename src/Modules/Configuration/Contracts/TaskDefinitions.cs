using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class TaskDefinitionAuthorization
{
    public const string Administer = "PER-DEFINICION-ADMIN";
}

public static class TaskDefinitionStatuses
{
    public const string InactiveForNew = "INACTIVA_NUEVAS";
}

public static class TaskDefinitionCatalog
{
    public const int SchemaVersion = 1;

    private static readonly IReadOnlyDictionary<string, TaskDefinitionSeed> Definitions =
        new Dictionary<string, TaskDefinitionSeed>(StringComparer.Ordinal)
        {
            ["TAR-0005"] = new(Guid.Parse("019d3a10-0005-7000-8000-000000000005"), "TAR-0005", "Monitorear el avance de ventas y activar acciones correctivas"),
            ["TAR-0007"] = new(Guid.Parse("019d3a10-0007-7000-8000-000000000007"), "TAR-0007", "Liberar la mercancía al vencer un Separado de Cortesía"),
            ["TAR-0008"] = new(Guid.Parse("019d3a10-0008-7000-8000-000000000008"), "TAR-0008", "Resolver una controversia de asignación de venta con base en evidencia"),
            ["TAR-0011"] = new(Guid.Parse("019d3a10-0011-7000-8000-000000000011"), "TAR-0011", "Gestionar la reparación o el cambio de una garantía autorizada después de 90 días"),
            ["TAR-0018"] = new(Guid.Parse("019d3a10-0018-7000-8000-000000000018"), "TAR-0018", "Montar o actualizar exhibiciones conforme al planograma y la zonificación"),
            ["TAR-0026"] = new(Guid.Parse("019d3a10-0026-7000-8000-000000000026"), "TAR-0026", "Programar y realizar el pago de servicios básicos"),
            ["TAR-0092"] = new(Guid.Parse("019d3a10-0092-7000-8000-000000000092"), "TAR-0092", "Coordinar la recepción de mercancía contra la nota de envío"),
            ["TAR-0093"] = new(Guid.Parse("019d3a10-0093-7000-8000-000000000093"), "TAR-0093", "Documentar y notificar incidencia de recepción de mercancía"),
        };

    public static IReadOnlyCollection<TaskDefinitionSeed> All { get; } = Definitions.Values.ToArray();

    public static TaskDefinitionSeed Require(string taskCode)
    {
        if (string.IsNullOrWhiteSpace(taskCode) || !Definitions.TryGetValue(taskCode, out var definition))
        {
            throw new TaskDefinitionNotMvpException();
        }

        return definition;
    }

    public static JsonDocument ValidatePayload(int schemaVersion, JsonElement payload)
    {
        if (schemaVersion != SchemaVersion || payload.ValueKind != JsonValueKind.Object ||
            payload.EnumerateObject().Any())
        {
            throw new TaskDefinitionValidationException(
                "schemaVersion debe ser 1 y taskPayload debe ser un objeto vacío para HU-011.");
        }

        return JsonDocument.Parse("{}");
    }
}

public sealed record TaskDefinitionSeed(Guid Id, string TaskCode, string Name);

public sealed class TaskDefinition
{
    private TaskDefinition()
    {
    }

    public TaskDefinition(Guid id, string taskCode, string name)
    {
        var canonical = TaskDefinitionCatalog.Require(taskCode);
        if (canonical.Id != id || !string.Equals(canonical.Name, name, StringComparison.Ordinal))
        {
            throw new TaskDefinitionValidationException("La identidad TAR debe conservar su UUID, código y nombre canónicos.");
        }

        Id = id;
        TaskCode = taskCode;
        Name = name;
    }

    public Guid Id { get; private init; }

    public string TaskCode { get; private init; } = null!;

    public string Name { get; private init; } = null!;
}

public sealed class TaskDefinitionVersion : IVersionedEntity
{
    private TaskDefinitionVersion()
    {
    }

    public TaskDefinitionVersion(
        Guid id,
        Guid taskDefinitionId,
        int versionNo,
        int schemaVersion,
        JsonDocument taskPayload,
        Guid releaseId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        ArgumentNullException.ThrowIfNull(taskPayload);
        var validated = TaskDefinitionCatalog.ValidatePayload(schemaVersion, taskPayload.RootElement);
        Id = id;
        TaskDefinitionId = taskDefinitionId;
        VersionNo = versionNo;
        SchemaVersion = schemaVersion;
        TaskPayload = validated;
        ReleaseId = releaseId;
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }

    public Guid TaskDefinitionId { get; private init; }

    public int VersionNo { get; private init; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public int SchemaVersion { get; private init; }

    public JsonDocument TaskPayload { get; private init; } = null!;

    public Guid ReleaseId { get; private init; }

    public string? Reason { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public long RowVersion { get; private set; }

    public VersionRecord ToVersionRecord() => new(
        Id,
        Status,
        EffectiveFrom,
        EffectiveTo,
        Reason,
        SupersedesId,
        RowVersion);

    public void ApplyPublished(VersionRecord version, bool activeForNew)
    {
        if (version.Id != Id || version.Status != VersionStatuses.Current)
        {
            throw new VersioningStateException("The publication plan does not match this task definition version.");
        }

        Apply(version with
        {
            Status = activeForNew ? VersionStatuses.Current : TaskDefinitionStatuses.InactiveForNew,
        });
    }

    public void ApplySuperseded(VersionRecord version)
    {
        if (version.Id != Id || version.Status != VersionStatuses.Superseded)
        {
            throw new VersioningStateException("The substitution plan does not match this task definition version.");
        }

        Apply(version);
    }

    private void Apply(VersionRecord version)
    {
        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record TaskDefinitionVersionDetails(
    Guid Id,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SchemaVersion,
    JsonElement TaskPayload,
    Guid ReleaseId,
    string? Reason,
    Guid? SupersedesId,
    long RowVersion);

public sealed record TaskDefinitionDetails(
    Guid Id,
    string TaskCode,
    string Name,
    TaskDefinitionVersionDetails? Current,
    IReadOnlyList<TaskDefinitionVersionDetails> History);

public sealed record CreateTaskDefinitionVersionCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid ReleaseId,
    int SchemaVersion,
    JsonElement TaskPayload);

public sealed record PublishTaskDefinitionVersionCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid VersionId,
    long ExpectedRowVersion,
    DateTimeOffset EffectiveFrom,
    string Reason);

public sealed record DeactivateTaskDefinitionCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid ReleaseId,
    long ExpectedRowVersion,
    DateTimeOffset EffectiveFrom,
    string Reason);

public sealed record TaskPublicationDirective(
    string TaskCode,
    Guid? VersionId,
    long ExpectedRowVersion,
    bool ActiveForNew,
    bool CreateDraft);

public interface ITaskDefinitionService
{
    Task<IReadOnlyList<TaskDefinitionDetails>> ListAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken = default);

    Task<TaskDefinitionDetails> GetAsync(Guid actorUserId, Guid correlationId, string taskCode, CancellationToken cancellationToken = default);

    Task<TaskDefinitionVersionDetails> CreateVersionAsync(CreateTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default);

    Task<TaskDefinitionVersionDetails> PublishVersionAsync(PublishTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default);

    Task<TaskDefinitionVersionDetails> DeactivateNewAsync(DeactivateTaskDefinitionCommand command, CancellationToken cancellationToken = default);
}

public sealed class TaskDefinitionNotMvpException() : Exception("DEFINICION_NO_MVP");

public sealed class TaskDefinitionAccessDeniedException()
    : Exception($"{TaskDefinitionAuthorization.Administer} is required for LOR-001.");

public sealed class TaskDefinitionNotFoundException() : Exception("The task definition version does not exist.");

public sealed class TaskDefinitionReleaseNotDraftException()
    : Exception("The configuration release must exist and remain BORRADOR for LOR-001.");

public sealed class TaskDefinitionIdempotencyConflictException()
    : Exception("The idempotency key was already used with different content.");

public sealed class TaskDefinitionValidationException(string message) : Exception(message);
