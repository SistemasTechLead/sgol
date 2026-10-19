using System.Globalization;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class ActivationPolicyAuthorization
{
    public const string Administer = "PER-ACTIVACION-ADMIN";
}

public static class ActivationModes
{
    public const string Manual = "MANUAL";
    public const string Recurring = "RECURRENTE";
}

public static class ActivationOriginSchemas
{
    public const string ManualReference = "MANUAL_REFERENCE_V1";
    public const string WorkingDayWindow = "WORKING_DAY_WINDOW_V1";
    public const string ServiceDueDateReference = "SERVICE_DUE_DATE_REFERENCE_V1";

    public static IReadOnlyList<string> ServiceDueDateReferenceParts { get; } =
        ["serviceKey", "dueDate", "reference"];
}

public static class ActivationScheduleKinds
{
    public const string WorkingDayWindows = "WORKING_DAY_WINDOWS";
    public const string BusinessDaysBeforeDueDate = "BUSINESS_DAYS_BEFORE_DUE_DATE";
}

public static class ActivationPolicyCatalog
{
    public const string LorettaTimeZone = "America/Mexico_City";

    private static readonly Dictionary<string, ActivationPolicyDefinition> Definitions =
        new Dictionary<string, ActivationPolicyDefinition>(StringComparer.Ordinal)
        {
            ["TAR-0005"] = new(ActivationModes.Recurring, ActivationOriginSchemas.WorkingDayWindow),
            ["TAR-0007"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            ["TAR-0008"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            ["TAR-0011"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            ["TAR-0018"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            ["TAR-0026"] = new(ActivationModes.Recurring, ActivationOriginSchemas.ServiceDueDateReference),
            ["TAR-0092"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            ["TAR-0093"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
        };

    private static readonly string[] SalesSchedulePropertyNames =
        ["kind", "workingDaysOnly", "localTimes", "timeZone"];

    private static readonly string[] ServiceSchedulePropertyNames =
        ["kind", "businessDaysBefore", "localTime", "timeZone", "adjustDueDateToPreviousBusinessDay"];

    private static readonly string[] SalesLocalTimes = ["12:00", "17:00"];

    public static IReadOnlyDictionary<string, ActivationPolicyDefinition> All => Definitions;

    public static ActivationPolicyDefinition Require(string taskCode)
    {
        _ = TaskDefinitionCatalog.Require(taskCode);
        return Definitions[taskCode];
    }

    public static JsonDocument Validate(
        string taskCode,
        string mode,
        JsonElement schedule,
        string originKeySchema)
    {
        var approved = Require(taskCode);
        if (!string.Equals(mode, approved.Mode, StringComparison.Ordinal))
        {
            throw new ActivationPolicyValidationException(
                $"{taskCode} sólo admite el mecanismo {approved.Mode}.");
        }

        if (!string.Equals(originKeySchema, approved.OriginKeySchema, StringComparison.Ordinal))
        {
            throw new ActivationPolicyValidationException(
                $"originKeySchema no coincide con el esquema cerrado aprobado para {taskCode}.");
        }

        if (mode == ActivationModes.Manual)
        {
            if (schedule.ValueKind != JsonValueKind.Null)
            {
                throw new ActivationPolicyValidationException("Una política MANUAL no admite schedule.");
            }

            return JsonDocument.Parse("null");
        }

        if (taskCode == "TAR-0005")
        {
            return ValidateSalesWindows(schedule);
        }

        return ValidateServiceDueDate(schedule);
    }

    private static JsonDocument ValidateSalesWindows(JsonElement schedule)
    {
        var properties = RequireExactObject(schedule, SalesSchedulePropertyNames);
        if (!IsString(properties["kind"], ActivationScheduleKinds.WorkingDayWindows) ||
            properties["workingDaysOnly"].ValueKind != JsonValueKind.True ||
            !IsString(properties["timeZone"], LorettaTimeZone) ||
            properties["localTimes"].ValueKind != JsonValueKind.Array)
        {
            throw new ActivationPolicyValidationException(
                "TAR-0005 requiere ventanas laborables locales a las 12:00 y 17:00.");
        }

        var times = properties["localTimes"].EnumerateArray().ToArray();
        if (times.Length != 2 || !IsString(times[0], "12:00") || !IsString(times[1], "17:00"))
        {
            throw new ActivationPolicyValidationException(
                "TAR-0005 requiere exactamente las ventanas 12:00 y 17:00, en ese orden.");
        }

        return JsonSerializer.SerializeToDocument(new
        {
            kind = ActivationScheduleKinds.WorkingDayWindows,
            workingDaysOnly = true,
            localTimes = SalesLocalTimes,
            timeZone = LorettaTimeZone,
        });
    }

    private static JsonDocument ValidateServiceDueDate(JsonElement schedule)
    {
        var properties = RequireExactObject(schedule, ServiceSchedulePropertyNames);
        if (!IsString(properties["kind"], ActivationScheduleKinds.BusinessDaysBeforeDueDate) ||
            properties["businessDaysBefore"].ValueKind != JsonValueKind.Number ||
            !properties["businessDaysBefore"].TryGetInt32(out var offset) || offset != 3 ||
            properties["localTime"].ValueKind != JsonValueKind.String ||
            !TimeOnly.TryParseExact(
                properties["localTime"].GetString(),
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _) ||
            !IsString(properties["timeZone"], LorettaTimeZone) ||
            properties["adjustDueDateToPreviousBusinessDay"].ValueKind != JsonValueKind.True)
        {
            throw new ActivationPolicyValidationException(
                "TAR-0026 requiere tres días hábiles antes, ajuste previo del vencimiento y una hora local HH:mm.");
        }

        return JsonSerializer.SerializeToDocument(new
        {
            kind = ActivationScheduleKinds.BusinessDaysBeforeDueDate,
            businessDaysBefore = 3,
            localTime = properties["localTime"].GetString(),
            timeZone = LorettaTimeZone,
            adjustDueDateToPreviousBusinessDay = true,
        });
    }

    private static Dictionary<string, JsonElement> RequireExactObject(
        JsonElement value,
        params string[] expectedNames)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ActivationPolicyValidationException("schedule debe ser un objeto JSON cerrado.");
        }

        var properties = value.EnumerateObject().ToArray();
        if (properties.Length != expectedNames.Length ||
            properties.Any(property => !expectedNames.Contains(property.Name, StringComparer.Ordinal)))
        {
            throw new ActivationPolicyValidationException("schedule contiene campos faltantes o no documentados.");
        }

        return properties.ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
    }

    private static bool IsString(JsonElement value, string expected) =>
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);
}

public sealed record ActivationPolicyDefinition(string Mode, string OriginKeySchema);

public sealed class ActivationRuleVersion : IVersionedEntity
{
    private ActivationRuleVersion()
    {
    }

    public ActivationRuleVersion(
        Guid id,
        Guid taskDefinitionId,
        Guid taskDefinitionVersionId,
        Guid releaseId,
        Guid? basedOnId,
        int versionNo,
        string mode,
        JsonElement schedule,
        string originKeySchema)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        var taskCode = TaskDefinitionCatalog.All.Single(item => item.Id == taskDefinitionId).TaskCode;
        Id = id;
        TaskDefinitionId = taskDefinitionId;
        TaskDefinitionVersionId = taskDefinitionVersionId;
        ReleaseId = releaseId;
        BasedOnId = basedOnId;
        VersionNo = versionNo;
        Mode = mode;
        Schedule = ActivationPolicyCatalog.Validate(taskCode, mode, schedule, originKeySchema);
        OriginKeySchema = originKeySchema;
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid TaskDefinitionId { get; private init; }
    public Guid TaskDefinitionVersionId { get; private init; }
    public Guid ReleaseId { get; private init; }
    public Guid? BasedOnId { get; private init; }
    public int VersionNo { get; private init; }
    public string Mode { get; private init; } = null!;
    public JsonDocument Schedule { get; private init; } = null!;
    public string OriginKeySchema { get; private init; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset? EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string? Reason { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public long RowVersion { get; private set; }

    public VersionRecord ToVersionRecord() =>
        new(Id, Status, EffectiveFrom, EffectiveTo, Reason, SupersedesId, RowVersion);

    public void ApplyPublished(VersionRecord version) => Apply(version, VersionStatuses.Current);
    public void ApplySuperseded(VersionRecord version) => Apply(version, VersionStatuses.Superseded);

    private void Apply(VersionRecord version, string requiredStatus)
    {
        if (version.Id != Id || version.Status != requiredStatus)
        {
            throw new VersioningStateException("The publication plan does not match this activation rule version.");
        }

        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record ActivationRuleVersionDetails(
    Guid Id,
    string TaskCode,
    Guid TaskDefinitionVersionId,
    Guid ReleaseId,
    int VersionNo,
    string Mode,
    JsonElement Schedule,
    string OriginKeySchema,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? SupersedesId,
    long RowVersion)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Replayed { get; init; }
}

public sealed record ActivationPolicyHistoryDetails(
    string TaskCode,
    ActivationRuleVersionDetails? Current,
    IReadOnlyList<ActivationRuleVersionDetails> History);

public sealed record PutActivationPolicyCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid TaskDefinitionVersionId,
    Guid ReleaseId,
    string Mode,
    JsonElement Schedule,
    string OriginKeySchema,
    long? ExpectedRowVersion);

public interface IActivationPolicyService
{
    Task<ActivationPolicyHistoryDetails> GetAsync(
        Guid actorUserId, Guid correlationId, string taskCode,
        CancellationToken cancellationToken = default);
    Task<ActivationRuleVersionDetails> PutAsync(
        PutActivationPolicyCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ActivationPolicyAccessDeniedException()
    : Exception($"{ActivationPolicyAuthorization.Administer} is required for LOR-001.");
public sealed class ActivationPolicyReleaseNotDraftException() : Exception("The configuration release must remain BORRADOR for LOR-001.");
public sealed class ActivationPolicyDefinitionPreconditionException() : Exception("The release must target the exact requested active TAR version or its draft successor.");
public sealed class ActivationPolicyCoverageException() : Exception("A configuration publication must leave exactly eight activation policies linked to the applicable TAR versions.");
public sealed class ActivationPolicyIfMatchRequiredException() : Exception("If-Match is required to supersede the current activation policy.");
public sealed class ActivationPolicyIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");
public sealed class ActivationPolicyValidationException(string message) : Exception(message);
