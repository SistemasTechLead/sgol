using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sgol.Evidence.Contracts;

public static partial class StructuredEvidencePayloadValidator
{
    private enum FieldKind { SchemaVersion, Text, Reference, Timestamp, Boolean, Decimal, Identifier, Literal, Enum }
    private sealed record Field(string Name, FieldKind Kind, bool Nullable = false, string? Fixed = null);

    private static readonly Dictionary<string, Field[]> Schemas =
        new Dictionary<string, Field[]>(StringComparer.Ordinal)
        {
            ["CALCULO_AVANCE"] = [Schema(), Decimal("expectedTarget"), Decimal("actualSales"), Reference("sourceReference")],
            ["ACCION_O_CONFORMIDAD"] = [Schema(), Enum("outcome", "ACCION|CONFORMIDAD"), Text("actionDescription", true), Identifier("responsiblePersonId", true), Timestamp("startsAt", true)],
            ["LIBERACION"] = [Schema(), Timestamp("releasedAt"), Reference("releaseReference")],
            ["MERCANCIA"] = [Schema(), Reference("merchandiseReference")],
            ["FECHA_HORA"] = [Schema(), Timestamp("occurredAt")],
            ["RETORNO_EXHIBICION"] = [Schema(), Timestamp("returnedAt"), Reference("returnReference")],
            ["SECUENCIA"] = [Schema(), Text("sequenceSummary")],
            ["DECISION"] = [Schema(), Text("decisionSummary"), Timestamp("decidedAt")],
            ["FUNDAMENTO"] = [Schema(), Text("foundationSummary")],
            ["AVISO_INTERNO"] = [Schema(), Reference("noticeReference"), Timestamp("notifiedAt")],
            ["EVALUACION"] = [Schema(), Text("assessmentSummary"), Timestamp("assessedAt")],
            ["REPARACION_O_CAMBIO"] = [Schema(), Enum("solutionType", "REPARACION|CAMBIO"), Reference("solutionReference"), Timestamp("completedAt")],
            ["ENTREGA"] = [Schema(), Reference("deliveryReference"), Timestamp("deliveredAt")],
            ["CHECKLIST_COMPLETO"] = [Schema(), Boolean("productCorrect"), Boolean("zoneAndFamilyCorrect"), Boolean("stableFormation"), Boolean("labelsVisible"), Boolean("alignmentConsistent"), Boolean("occupancyJustified"), Boolean("clean"), Boolean("intact"), Boolean("signageCorrect"), Boolean("matchesPlanogramOrList")],
            ["FORM_ADM_02"] = [Schema(), Literal("formCode", "FORM-ADM-02"), Reference("formReference"), Timestamp("completedAt")],
            ["F_ENT_001"] = [Schema(), Literal("formCode", "F-ENT-001"), Reference("formReference"), Timestamp("completedAt"), Boolean("hasDifference"), Boolean("hasDamage")],
            ["ANOTACION_F_ENT_001"] = [Schema(), Literal("formCode", "F-ENT-001"), Reference("formReference"), Reference("annotationReference"), Timestamp("recordedAt")],
            ["CONSTANCIA_AVISO_INTERNO"] = [Schema(), Reference("noticeReference"), Timestamp("notifiedAt")],
        };

    private static readonly Dictionary<string, Dictionary<string, string>> Requirements =
        new(StringComparer.Ordinal)
        {
            ["TAR-0005"] = Kinds(("CALCULO_AVANCE", "REGISTRO_DIGITAL"), ("ACCION_O_CONFORMIDAD", "REGISTRO_DIGITAL")),
            ["TAR-0007"] = Kinds(("LIBERACION", "REGISTRO_DIGITAL"), ("MERCANCIA", "DATO_ESTRUCTURADO"), ("FECHA_HORA", "DATO_ESTRUCTURADO"), ("RETORNO_EXHIBICION", "REGISTRO_DIGITAL")),
            ["TAR-0008"] = Kinds(("SECUENCIA", "REGISTRO_DIGITAL"), ("DECISION", "REGISTRO_DIGITAL"), ("FUNDAMENTO", "DATO_ESTRUCTURADO"), ("AVISO_INTERNO", "REGISTRO_DIGITAL")),
            ["TAR-0011"] = Kinds(("EVALUACION", "REGISTRO_DIGITAL"), ("REPARACION_O_CAMBIO", "REGISTRO_DIGITAL"), ("ENTREGA", "REGISTRO_DIGITAL")),
            ["TAR-0018"] = Kinds(("CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO")),
            ["TAR-0026"] = Kinds(("FORM_ADM_02", "FORMULARIO_REFERENCIADO")),
            ["TAR-0092"] = Kinds(("F_ENT_001", "FORMULARIO_REFERENCIADO")),
            ["TAR-0093"] = Kinds(("ANOTACION_F_ENT_001", "FORMULARIO_REFERENCIADO"), ("CONSTANCIA_AVISO_INTERNO", "REGISTRO_DIGITAL")),
        };

    public static JsonDocument ValidateAndCanonicalize(string taskCode, string requirementCode, string requirementKind, JsonElement payload)
    {
        if (!Requirements.TryGetValue(taskCode, out var taskRequirements) ||
            !taskRequirements.TryGetValue(requirementCode, out var expectedKind) || expectedKind != requirementKind ||
            !Schemas.TryGetValue(requirementCode, out var fields) || payload.ValueKind != JsonValueKind.Object)
        {
            throw new EvidenceRequestInvalidException();
        }

        var properties = payload.EnumerateObject().ToArray();
        if (properties.Length != fields.Length || properties.GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() != 1) ||
            fields.Any(field => properties.All(property => property.Name != field.Name)))
        {
            throw new EvidenceRequestInvalidException();
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var value = payload.GetProperty(field.Name);
            normalized[field.Name] = Normalize(field, value);
        }

        ValidateRelations(requirementCode, normalized);
        return JsonSerializer.SerializeToDocument(normalized);
    }

    public static bool TryResolveDifferenceOrDamage(JsonElement payload, out bool applies)
    {
        applies = false;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("schemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1 ||
            !payload.TryGetProperty("hasDifference", out var difference) || difference.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !payload.TryGetProperty("hasDamage", out var damage) || damage.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        applies = difference.GetBoolean() || damage.GetBoolean();
        return true;
    }

    private static object? Normalize(Field field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null && field.Nullable) return null;
        return field.Kind switch
        {
            FieldKind.SchemaVersion when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var version) && version == 1 => 1,
            FieldKind.Literal => NormalizeString(value, 120, field.Fixed),
            FieldKind.Enum => NormalizeEnum(value, field.Fixed!),
            FieldKind.Text => NormalizeString(value, 500),
            FieldKind.Reference => NormalizeReference(value),
            FieldKind.Timestamp => NormalizeTimestamp(value),
            FieldKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            FieldKind.Decimal => NormalizeDecimal(value),
            FieldKind.Identifier => NormalizeIdentifier(value),
            _ => throw new EvidenceRequestInvalidException()
        };
    }

    private static string NormalizeString(JsonElement value, int maximum, string? fixedValue = null)
    {
        if (value.ValueKind != JsonValueKind.String) throw new EvidenceRequestInvalidException();
        var original = value.GetString() ?? throw new EvidenceRequestInvalidException();
        var normalized = original.Normalize(NormalizationForm.FormC).Trim();
        if (normalized.Length is < 1 || normalized.Length > maximum || normalized.Any(IsForbiddenCharacter) ||
            fixedValue is not null && normalized != fixedValue)
        {
            throw new EvidenceRequestInvalidException();
        }

        return normalized;
    }

    private static string NormalizeReference(JsonElement value)
    {
        var text = NormalizeString(value, 120);
        if (Uri.TryCreate(text, UriKind.Absolute, out _) || text.Contains('/') || text.Contains('\\'))
            throw new EvidenceRequestInvalidException();
        return text;
    }

    private static string NormalizeEnum(JsonElement value, string allowed)
    {
        var text = NormalizeString(value, 120);
        if (!allowed.Split('|').Contains(text, StringComparer.Ordinal)) throw new EvidenceRequestInvalidException();
        return text;
    }

    private static string NormalizeTimestamp(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String) throw new EvidenceRequestInvalidException();
        var text = value.GetString()!;
        if (!UtcTimestampPattern().IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            throw new EvidenceRequestInvalidException();
        }

        return timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    private static decimal NormalizeDecimal(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)) throw new EvidenceRequestInvalidException();
        var bits = decimal.GetBits(number);
        var scale = (bits[3] >> 16) & 0x7F;
        var absolute = decimal.Abs(number);
        if (scale > 2 || absolute >= 1_000_000_000_000_000m) throw new EvidenceRequestInvalidException();
        return number;
    }

    private static string NormalizeIdentifier(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !Guid.TryParseExact(value.GetString(), "D", out var id) || id == Guid.Empty ||
            value.GetString() != id.ToString("D"))
        {
            throw new EvidenceRequestInvalidException();
        }
        return id.ToString("D");
    }

    private static void ValidateRelations(string requirementCode, Dictionary<string, object?> values)
    {
        if (requirementCode == "CALCULO_AVANCE" &&
            ((decimal)values["expectedTarget"]! <= 0 || (decimal)values["actualSales"]! < 0))
        {
            throw new EvidenceRequestInvalidException();
        }

        if (requirementCode == "ACCION_O_CONFORMIDAD")
        {
            var action = string.Equals(values["outcome"] as string, "ACCION", StringComparison.Ordinal);
            var related = new[] { values["actionDescription"], values["responsiblePersonId"], values["startsAt"] };
            if (action ? related.Any(x => x is null) : related.Any(x => x is not null)) throw new EvidenceRequestInvalidException();
        }
    }

    private static bool IsForbiddenCharacter(char value) => char.IsControl(value) || value is '<' or '>' || value is >= '\u007f' and <= '\u009f';
    private static Field Schema() => new("schemaVersion", FieldKind.SchemaVersion);
    private static Field Text(string name, bool nullable = false) => new(name, FieldKind.Text, nullable);
    private static Field Reference(string name) => new(name, FieldKind.Reference);
    private static Field Timestamp(string name, bool nullable = false) => new(name, FieldKind.Timestamp, nullable);
    private static Field Boolean(string name) => new(name, FieldKind.Boolean);
    private static Field Decimal(string name) => new(name, FieldKind.Decimal);
    private static Field Identifier(string name, bool nullable = false) => new(name, FieldKind.Identifier, nullable);
    private static Field Literal(string name, string value) => new(name, FieldKind.Literal, Fixed: value);
    private static Field Enum(string name, string values) => new(name, FieldKind.Enum, Fixed: values);
    private static Dictionary<string, string> Kinds(params (string Code, string Kind)[] values) =>
        values.ToDictionary(x => x.Code, x => x.Kind, StringComparer.Ordinal);

    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z$", RegexOptions.CultureInvariant)]
    private static partial Regex UtcTimestampPattern();
}
