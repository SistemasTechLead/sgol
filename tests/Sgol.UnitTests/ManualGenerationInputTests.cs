using System.Text.Json;
using Sgol.Generation.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ManualGenerationInputTests
{
    public static TheoryData<string, string> ApprovedForms => new()
    {
        { """{"taskCode":"TAR-0007","reservationReference":"R1","merchandiseReference":"M1","startedAt":"2026-09-01T12:00:00Z","expiresAt":"2026-09-02T12:00:00Z","sourceReference":"D1"}""", """[2,"TAR-0007","R1","2026-09-02T12:00:00.000000Z"]""" },
        { """{"taskCode":"TAR-0008","operationReference":"O1","detectedAt":"2026-09-01T12:00:00Z","claimantReferences":["B","A"]}""", """[2,"TAR-0008","O1"]""" },
        { """{"taskCode":"TAR-0011","caseReference":"C1","authorizationReference":"A1","productReference":"P1","solutionType":"CAMBIO","authorizedAt":"2026-09-01T12:00:00Z"}""", """[2,"TAR-0011","A1"]""" },
        { """{"taskCode":"TAR-0018","eventReference":"E1","zoneReference":"Z1","planogramReference":"P1"}""", """[2,"TAR-0018","E1","P1"]""" },
        { """{"taskCode":"TAR-0092","receiptReference":"R1","supplierReference":"S1","documentReference":"D1","startedAt":"2026-09-01T12:00:00Z","merchandiseReference":"M1"}""", """[2,"TAR-0092","R1"]""" },
        { """{"taskCode":"TAR-0093","parentObligationId":"019d3a10-0000-7000-8000-000000000001","incidentReference":"I1","incidentType":"DANO","description":"Daño sintético","occurredAt":"2026-09-01T12:00:00Z"}""", """[2,"TAR-0093","019d3a10-0000-7000-8000-000000000001","I1"]""" }
    };

    [Theory, MemberData(nameof(ApprovedForms))]
    public void ClosedFormsHaveStableIdentityAndRejectEveryMissingField(string json, string expectedIdentity)
    {
        using var source = JsonDocument.Parse(json);
        var normalized = ManualGenerationInput.Normalize(source.RootElement);
        Assert.Equal(expectedIdentity, ManualGenerationInput.Identity(normalized).ToJsonString());
        var expectedHash = ManualGenerationInput.TaskCode(normalized) switch
        {
            "TAR-0007" => "e8fae0fd4c77ee49770cd18c7c9ecd04f3da0b10b4de8f9652b71988b131fd62",
            "TAR-0008" => "a7791c1af3312c4fe70d318c58110d18001b13d40405e8388335308dcab91615",
            "TAR-0011" => "40b27c0dfdd93fbe7e226b50d1391b0011fcf106524db047ac42f72c12e87887",
            "TAR-0018" => "a09e536d2c81e3d3dfa1566fa2f11b1014fceadd8995b4f6229210e122e2ae96",
            "TAR-0092" => "0a26e14101eb20bfd5ae2e5304bfe7956867168bbc7bb7c01f45493b473b66de",
            _ => "d79b23358dede0d1be16a4e594ea47ec428abcc05565c442ea3929349a73f480"
        };
        var canonical = Sgol.Web.Infrastructure.Persistence.Idempotency.IdempotencyProtocol.Canonicalize(ManualGenerationInput.Identity(normalized));
        Assert.Equal(expectedHash, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))));
        ManualGenerationInput.ValidateTimes(normalized, DateTimeOffset.Parse("2026-09-03T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var field in ManualGenerationInput.Fields(ManualGenerationInput.TaskCode(normalized)))
        {
            var changed = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            changed.AsObject().Remove(field);
            var error = Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.Normalize(JsonSerializer.SerializeToElement(changed)));
            Assert.Equal("ORIGEN_INVALIDO", error.Code);
            Assert.Contains(error.FieldErrors, e => e.Path == "inputPayload." + field && e.Code == "REQUIRED");
        }
    }

    [Theory]
    [InlineData("https://example.invalid/document")]
    [InlineData("\n")]
    [InlineData("a\tb")]
    public void InvalidExternalReferencesDoNotAcceptUrlsOrControls(string text) =>
        Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.Reference(text));

    [Fact]
    public void ReferencesNormalizeNfcAndTrimWithoutLosingCaseOrInternalSpaces()
    {
        Assert.Equal("Á  b", ManualGenerationInput.Reference(" A\u0301  b "));
        Assert.Equal(new string('a', 120), ManualGenerationInput.Reference(new string('a', 120)));
        Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.Reference(new string('a', 121)));
        Assert.Equal(string.Concat(Enumerable.Repeat("😀", 120)), ManualGenerationInput.Reference(string.Concat(Enumerable.Repeat("😀", 120))));
    }

    [Theory]
    [InlineData("2026-09-01T12:00:00-06:00")]
    [InlineData("2026-09-01T12:00:00.1234567Z")]
    [InlineData("2026-02-30T12:00:00Z")]
    public void OnlyUtcMicrosecondInstantsAreAccepted(string value) =>
        Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.Instant(value, "startedAt"));

    [Fact]
    public void DuplicateClaimantsUnknownFieldsAndDuplicateJsonKeysAreRejected()
    {
        foreach (var json in new[]
        {
            """{"taskCode":"TAR-0008","operationReference":"O1","detectedAt":"2026-09-01T12:00:00Z","claimantReferences":[" A ","A"]}""",
            """{"taskCode":"TAR-0018","eventReference":"E1","zoneReference":"Z1","planogramReference":"P1","responsibleId":"x"}""",
            """{"taskCode":"TAR-0018","eventReference":"E1","zoneReference":"Z1","planogramReference":"P1","eventReference":"E2"}"""
        })
        {
            using var source = JsonDocument.Parse(json);
            Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.Normalize(source.RootElement));
        }
    }

    [Fact]
    public void ClaimantOrderIsCanonicalAndFutureOriginIsRejected()
    {
        using var body = JsonDocument.Parse("""{"taskCode":"TAR-0008","operationReference":"O1","detectedAt":"2026-09-01T12:00:00Z","claimantReferences":["B","A"]}""");
        var normalized = ManualGenerationInput.Normalize(body.RootElement);
        Assert.Equal("A", normalized.GetProperty("claimantReferences")[0].GetString());
        Assert.Throws<ManualGenerationException>(() => ManualGenerationInput.ValidateTimes(normalized, DateTimeOffset.Parse("2026-09-01T11:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));
    }
}
