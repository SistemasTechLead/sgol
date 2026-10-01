using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Evidence.Contracts;
using Sgol.Web.Presentation.MyWork;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front017PresentationTests
{
    [Theory]
    [InlineData("DIRECCION")]
    [InlineData("ADMINISTRACION")]
    [InlineData("SUBCOORDINACION")]
    [InlineData("PISO_VENTAS")]
    public void AllCanonicalRolesExposeTheExistingContributionCapability(string role) =>
        Assert.Contains(EvidenceAuthorization.Contribute, Sgol.Identity.Contracts.RolePermissionProjection.ForRole(role));

    public static TheoryData<string, string, string, string> Payloads => StructuredEvidencePayloadValidatorTests.ApprovedPayloads;

    [Theory]
    [MemberData(nameof(Payloads))]
    public void AllEighteenFormsProduceTheApprovedClosedPayload(string task, string requirement, string kind, string json)
    {
        using var expected = JsonDocument.Parse(json);
        var values = new Dictionary<string, string>();
        foreach (var field in EvidenceContributionPresentation.Inputs(requirement))
        {
            var value = expected.RootElement.GetProperty(field.Name);
            values[field.Name] = field.Type == "datetime-local" ? "2026-09-07T14:00" :
                value.ValueKind switch { JsonValueKind.True => "true", JsonValueKind.False => "false", JsonValueKind.Null => "", _ => value.ToString() };
        }
        var invalid = new List<string>();
        using var actual = EvidenceContributionPresentation.Payload(task, requirement, kind, values, invalid);
        using var canonical = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(task, requirement, kind, expected.RootElement);
        Assert.Equal(canonical.RootElement.GetRawText(), actual.RootElement.GetRawText());
        Assert.Empty(invalid);
    }

    [Theory]
    [InlineData("hasDifference", "")]
    [InlineData("hasDamage", "yes")]
    [InlineData("completedAt", "2026-09-07T20:00:00Z")]
    [InlineData("formReference", "https://example.test")]
    public void MissingBooleanAmbiguousTimeAndUnsafeReferenceAreRejected(string field, string invalidValue)
    {
        var values = new Dictionary<string, string> { ["formReference"] = "SINTETICA-01", ["completedAt"] = "2026-09-07T14:00", ["hasDifference"] = "false", ["hasDamage"] = "true" };
        values[field] = invalidValue;
        var errors = new List<string>();
        Assert.Throws<EvidenceRequestInvalidException>(() => EvidenceContributionPresentation.Payload("TAR-0092", "F_ENT_001", "FORMULARIO_REFERENCIADO", values, errors));
        Assert.Contains(field, errors);
    }

    [Fact]
    public void ChecklistFalseRemainsFalseAndActionConvertsToUtc()
    {
        var values = EvidenceContributionPresentation.Inputs("CHECKLIST_COMPLETO").ToDictionary(f => f.Name, _ => "false");
        using var checklist = EvidenceContributionPresentation.Payload("TAR-0018", "CHECKLIST_COMPLETO", "CHECKLIST_ESTRUCTURADO", values, new List<string>());
        Assert.False(checklist.RootElement.GetProperty("clean").GetBoolean());
        values = new() { ["outcome"] = "ACCION", ["actionDescription"] = "Acción sintética", ["responsiblePersonId"] = Guid.NewGuid().ToString("D"), ["startsAt"] = "2026-09-07T14:00" };
        using var action = EvidenceContributionPresentation.Payload("TAR-0005", "ACCION_O_CONFORMIDAD", "REGISTRO_DIGITAL", values, new List<string>());
        Assert.Equal(DateTimeOffset.Parse("2026-09-07T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            action.RootElement.GetProperty("startsAt").GetDateTimeOffset());
    }

    [Fact]
    public void ProtectedIntentsBindActorResourceOperationExpiryAndStableKeys()
    {
        var protector = new EvidenceIntentionProtector(new EphemeralDataProtectionProvider());
        var now = DateTimeOffset.UtcNow;
        var intention = new EvidenceIntention(Guid.NewGuid(), Guid.NewGuid(), "SECUENCIA", "structured", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(10), JsonSerializer.SerializeToElement(new { secretSynthetic = "SINTETICA-PRIVADA" }));
        var token = protector.Protect(intention);
        Assert.DoesNotContain("SINTETICA-PRIVADA", token);
        Assert.Equal("EvidenceIntention", intention.ToString());
        Assert.Equal(intention.Key, protector.Read(token, intention.Actor, intention.Obligation, "structured", now)!.Key);
        Assert.Equal(intention.ContributionKey, protector.Read(token, intention.Actor, intention.Obligation, "structured", now)!.ContributionKey);
        Assert.Null(protector.Read(token, Guid.NewGuid(), intention.Obligation, "structured", now));
        Assert.Null(protector.Read(token, intention.Actor, Guid.NewGuid(), "structured", now));
        Assert.Null(protector.Read(token, intention.Actor, intention.Obligation, "file", now));
        Assert.Null(protector.Read(token, intention.Actor, intention.Obligation, "structured", now.AddMinutes(10)));
        Assert.Null(protector.Read(token[..^8] + "tampered", intention.Actor, intention.Obligation, "structured", now));
    }

    [Theory]
    [InlineData("INFECTADO")]
    [InlineData("INVALIDO")]
    [InlineData("ERROR_ESCANEO")]
    public void TerminalStatesExplicitlyStateThatNoFileWasLinked(string state) =>
        Assert.Contains("vincul", EvidenceContributionPresentation.Scan(state));
}
