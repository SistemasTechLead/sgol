using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Sgol.Generation.Contracts;

public sealed record GenerationFieldError(string Path, string Code);
public sealed class ManualGenerationException(string code, int status = 422, params GenerationFieldError[] fields)
    : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public IReadOnlyList<GenerationFieldError> FieldErrors { get; } = fields;
}

public sealed record ManualGenerationOption(string TaskCode, string Name, Guid TaskDefinitionVersionId,
    Guid RuleVersionId, Guid BranchId, string OriginType);
public sealed record ReceiptOrigin(Guid ObligationId, Guid GenerationRequestId, string ReceiptReference,
    DateTimeOffset StartedAt, Guid PeriodId);
public sealed record ReceiptOriginPage(IReadOnlyList<ReceiptOrigin> Items, string? NextCursor);
public interface IManualGenerationReader
{
    Task<IReadOnlyList<ManualGenerationOption>> GetOptionsAsync(Guid actor, CancellationToken token = default);
    Task<ReceiptOriginPage> GetReceiptOriginsAsync(Guid actor, string? receiptReference, string? cursor,
        int pageSize, CancellationToken token = default);
}

/// <summary>The six closed CAT input schemas approved in Adenda 51. No persistence or authorization.</summary>
public static partial class ManualGenerationInput
{
    private static readonly IReadOnlyDictionary<string, string[]> Schemas = new Dictionary<string, string[]>
    {
        ["TAR-0007"] = ["reservationReference", "merchandiseReference", "startedAt", "expiresAt", "sourceReference"],
        ["TAR-0008"] = ["operationReference", "detectedAt", "claimantReferences"],
        ["TAR-0011"] = ["caseReference", "authorizationReference", "productReference", "solutionType", "authorizedAt"],
        ["TAR-0018"] = ["eventReference", "zoneReference", "planogramReference"],
        ["TAR-0092"] = ["receiptReference", "supplierReference", "documentReference", "startedAt", "merchandiseReference"],
        ["TAR-0093"] = ["parentObligationId", "incidentReference", "incidentType", "description", "occurredAt"]
    };
    public static IReadOnlyCollection<string> TaskCodes => Schemas.Keys.ToArray();
    public static IReadOnlyList<string> Fields(string taskCode) => Schemas.TryGetValue(taskCode, out var fields)
        ? fields : throw Invalid("taskCode");

    // Normalization must also work during terminal replay; time-dependent checks are separate.
    public static JsonElement Normalize(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) throw Syntax();
        var props = input.EnumerateObject().ToArray();
        if (props.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != props.Length) throw Syntax();
        if (!input.TryGetProperty("taskCode", out var code) || code.ValueKind != JsonValueKind.String) throw Syntax();
        var task = code.GetString()!;
        if (task is "TAR-0005" or "TAR-0026") throw new ManualGenerationException("REGLA_MANUAL_REQUERIDA", 409);
        var fields = Fields(task);
        if (props.Any(p => p.Name != "taskCode" && !fields.Contains(p.Name))) throw Syntax();
        var result = new JsonObject { ["taskCode"] = task };
        foreach (var field in fields)
        {
            if (!input.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
                throw Invalid(field, "REQUIRED");
            if (field == "claimantReferences")
            {
                if (value.ValueKind != JsonValueKind.Array) throw Syntax();
                var claims = value.EnumerateArray().Select(v => Reference(ReadString(v), field: field)).Order(StringComparer.Ordinal).ToArray();
                if (claims.Length is < 2 or > 20) throw Invalid(field, "OUT_OF_RANGE");
                if (claims.Distinct(StringComparer.Ordinal).Count() != claims.Length) throw Invalid(field, "DUPLICATE");
                result[field] = new JsonArray(claims.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
                continue;
            }
            var text = ReadString(value);
            if (field.EndsWith("At", StringComparison.Ordinal))
                result[field] = Instant(text, field).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
            else if (field == "parentObligationId")
            {
                if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty) throw Invalid(field);
                result[field] = id.ToString("D");
            }
            else if (field == "solutionType")
                result[field] = text is "REPARACION" or "CAMBIO" ? text : throw Invalid(field);
            else if (field == "incidentType")
                result[field] = text is "DIFERENCIA" or "DANO" or "DIFERENCIA_Y_DANO" ? text : throw Invalid(field);
            else result[field] = Reference(text, field == "description" ? 500 : 120, field);
        }
        return JsonSerializer.SerializeToElement(result);
    }

    public static string Reference(string text, int max = 120, string field = "receiptReference")
    {
        var value = text.Trim().Normalize(NormalizationForm.FormC);
        if (value.Length == 0 || value.EnumerateRunes().Count() > max ||
            value.Any(char.IsControl) || Uri.TryCreate(value, UriKind.Absolute, out _)) throw Invalid(field);
        return value;
    }
    public static DateTimeOffset Instant(string text, string field)
    {
        if (!UtcInstant().IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date)) throw Invalid(field);
        return date;
    }
    public static void ValidateTimes(JsonElement input, DateTimeOffset now)
    {
        foreach (var field in new[] { "startedAt", "detectedAt", "authorizedAt", "occurredAt" })
            if (input.TryGetProperty(field, out var value) && Instant(value.GetString()!, field) > now)
                throw Invalid(field, "OUT_OF_RANGE");
        if (TaskCode(input) == "TAR-0007" && Date(input, "expiresAt") <= Date(input, "startedAt"))
            throw Invalid("expiresAt", "OUT_OF_RANGE");
    }
    public static string TaskCode(JsonElement input) => input.GetProperty("taskCode").GetString()!;
    public static DateTimeOffset Date(JsonElement input, string field) => Instant(input.GetProperty(field).GetString()!, field);
    public static JsonArray Identity(JsonElement input)
    {
        var code = TaskCode(input);
        string[] fields = code switch
        {
            "TAR-0007" => ["reservationReference", "expiresAt"],
            "TAR-0008" => ["operationReference"],
            "TAR-0011" => ["authorizationReference"],
            "TAR-0018" => ["eventReference", "planogramReference"],
            "TAR-0092" => ["receiptReference"],
            "TAR-0093" => ["parentObligationId", "incidentReference"],
            _ => throw Invalid("taskCode")
        };
        var tuple = new JsonArray(2, code);
        foreach (var field in fields) tuple.Add(input.GetProperty(field).GetString());
        return tuple;
    }
    public static ManualGenerationException Invalid(string field, string code = "INVALID") =>
        new("ORIGEN_INVALIDO", 422, new GenerationFieldError("inputPayload." + field, code));
    private static string ReadString(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Syntax();
    private static ManualGenerationException Syntax() => new("GENERATION_REQUEST_INVALIDA", 400);
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,6})?Z$", RegexOptions.CultureInvariant)]
    private static partial Regex UtcInstant();
}
