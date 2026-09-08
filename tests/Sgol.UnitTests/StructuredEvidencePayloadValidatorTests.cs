using System.Text.Json;
using Sgol.Evidence.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class StructuredEvidencePayloadValidatorTests
{
    public static TheoryData<string, string, string, string> ApprovedPayloads => new()
    {
        { "TAR-0005", "CALCULO_AVANCE", "REGISTRO_DIGITAL", """{"schemaVersion":1,"expectedTarget":100.00,"actualSales":90,"sourceReference":"VENTAS-01"}""" },
        { "TAR-0005", "ACCION_O_CONFORMIDAD", "REGISTRO_DIGITAL", """{"schemaVersion":1,"outcome":"CONFORMIDAD","actionDescription":null,"responsiblePersonId":null,"startsAt":null}""" },
        { "TAR-0007", "LIBERACION", "REGISTRO_DIGITAL", """{"schemaVersion":1,"releasedAt":"2026-09-07T20:00:00Z","releaseReference":"LIB-01"}""" },
        { "TAR-0007", "MERCANCIA", "DATO_ESTRUCTURADO", """{"schemaVersion":1,"merchandiseReference":"MER-01"}""" },
        { "TAR-0007", "FECHA_HORA", "DATO_ESTRUCTURADO", """{"schemaVersion":1,"occurredAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0007", "RETORNO_EXHIBICION", "REGISTRO_DIGITAL", """{"schemaVersion":1,"returnedAt":"2026-09-07T20:00:00Z","returnReference":"RET-01"}""" },
        { "TAR-0008", "SECUENCIA", "REGISTRO_DIGITAL", """{"schemaVersion":1,"sequenceSummary":"Secuencia sintética"}""" },
        { "TAR-0008", "DECISION", "REGISTRO_DIGITAL", """{"schemaVersion":1,"decisionSummary":"Decisión sintética","decidedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0008", "FUNDAMENTO", "DATO_ESTRUCTURADO", """{"schemaVersion":1,"foundationSummary":"Fundamento sintético"}""" },
        { "TAR-0008", "AVISO_INTERNO", "REGISTRO_DIGITAL", """{"schemaVersion":1,"noticeReference":"AVI-01","notifiedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0011", "EVALUACION", "REGISTRO_DIGITAL", """{"schemaVersion":1,"assessmentSummary":"Evaluación sintética","assessedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0011", "REPARACION_O_CAMBIO", "REGISTRO_DIGITAL", """{"schemaVersion":1,"solutionType":"REPARACION","solutionReference":"SOL-01","completedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0011", "ENTREGA", "REGISTRO_DIGITAL", """{"schemaVersion":1,"deliveryReference":"ENT-01","deliveredAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0018", "CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", """{"schemaVersion":1,"productCorrect":true,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}""" },
        { "TAR-0026", "FORM_ADM_02", "FORMULARIO_REFERENCIADO", """{"schemaVersion":1,"formCode":"FORM-ADM-02","formReference":"ADM-01","completedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0092", "F_ENT_001", "FORMULARIO_REFERENCIADO", """{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false}""" },
        { "TAR-0093", "ANOTACION_F_ENT_001", "FORMULARIO_REFERENCIADO", """{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","annotationReference":"ANO-01","recordedAt":"2026-09-07T20:00:00Z"}""" },
        { "TAR-0093", "CONSTANCIA_AVISO_INTERNO", "REGISTRO_DIGITAL", """{"schemaVersion":1,"noticeReference":"AVI-01","notifiedAt":"2026-09-07T20:00:00Z"}""" },
    };

    [Theory]
    [MemberData(nameof(ApprovedPayloads))]
    public void ApprovedClosedSchemasAreCanonicalized(string taskCode, string requirementCode, string kind, string json)
    {
        using var input = JsonDocument.Parse(json);
        using var canonical = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(taskCode, requirementCode, kind, input.RootElement);

        Assert.Equal(1, canonical.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(requirementCode == "F_ENT_001", StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(canonical.RootElement, out _));
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":false,"hasDamage":false,"extra":true}""")]
    [InlineData("""{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"https://example.test","completedAt":"2026-09-07T20:00:00Z","hasDifference":false,"hasDamage":false}""")]
    [InlineData("""{"schemaVersion":2,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":false,"hasDamage":false}""")]
    public void UnknownUnsafeOrWrongVersionPayloadIsRejected(string json)
    {
        using var input = JsonDocument.Parse(json);
        Assert.Throws<EvidenceRequestInvalidException>(() => StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0092", "F_ENT_001", "FORMULARIO_REFERENCIADO", input.RootElement));
    }

    [Fact]
    public void DifferenceOrDamageDistinguishesFalseFromUnresolved()
    {
        using var valid = JsonDocument.Parse("""{"schemaVersion":1,"hasDifference":false,"hasDamage":false}""");
        using var invalid = JsonDocument.Parse("""{"schemaVersion":1,"hasDifference":false}""");

        Assert.True(StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(valid.RootElement, out var applies));
        Assert.False(applies);
        Assert.False(StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(invalid.RootElement, out _));
    }

    [Fact]
    public void CanonicalizationMakesPropertyOrderAndTrimmableTextReproducible()
    {
        using var first = JsonDocument.Parse("""{"schemaVersion":1,"merchandiseReference":"  MER-01  "}""");
        using var second = JsonDocument.Parse("""{"merchandiseReference":"MER-01","schemaVersion":1}""");
        using var firstCanonical = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0007", "MERCANCIA", "DATO_ESTRUCTURADO", first.RootElement);
        using var secondCanonical = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0007", "MERCANCIA", "DATO_ESTRUCTURADO", second.RootElement);

        Assert.Equal(firstCanonical.RootElement.GetRawText(), secondCanonical.RootElement.GetRawText());
    }

    [Fact]
    public void ValidChecklistMayRecordFalseWithoutBecomingInvalidEvidence()
    {
        using var input = JsonDocument.Parse("""{"schemaVersion":1,"productCorrect":false,"zoneAndFamilyCorrect":true,"stableFormation":true,"labelsVisible":true,"alignmentConsistent":true,"occupancyJustified":true,"clean":true,"intact":true,"signageCorrect":true,"matchesPlanogramOrList":true}""");
        using var canonical = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0018", "CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", input.RootElement);

        Assert.False(canonical.RootElement.GetProperty("productCorrect").GetBoolean());
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"outcome":"ACCION","actionDescription":null,"responsiblePersonId":null,"startsAt":null}""")]
    [InlineData("""{"schemaVersion":1,"outcome":"CONFORMIDAD","actionDescription":"No corresponde","responsiblePersonId":null,"startsAt":null}""")]
    public void ActionOutcomeRejectsIncoherentConditionalFields(string json)
    {
        using var input = JsonDocument.Parse(json);
        Assert.Throws<EvidenceRequestInvalidException>(() => StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0005", "ACCION_O_CONFORMIDAD", "REGISTRO_DIGITAL", input.RootElement));
    }
}
