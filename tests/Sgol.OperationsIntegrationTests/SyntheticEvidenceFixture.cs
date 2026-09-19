using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;

namespace Sgol.OperationsIntegrationTests;

internal static class SyntheticEvidenceFixture
{
    public const string ObjectKey =
        "v1/e4/e9/e4e9899d4e81ad7741a456fb87b202bdecfa81d32258d088f20ef3cab2a7a0af";
    public const string OriginalName = "hu-035.pdf";
    public const string ContentType = "application/pdf";
    public const string MetadataMediaType = "Document";
    public const string FirstSequencePayload =
        "{\"schemaVersion\":1,\"sequenceSummary\":\"primera\"}";
    public const string SecondSequencePayload =
        "{\"schemaVersion\":1,\"sequenceSummary\":\"segunda\"}";

    private const string Document =
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n" +
        "% TECH-OPS-001 synthetic evidence object\n" +
        "trailer\n<< /Root 1 0 R >>\n%%EOF\n";

    public static byte[] Content => Encoding.ASCII.GetBytes(Document);

    public static string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Content));

    public static void AssertContract()
    {
        _ = EvidenceObjectKey.Parse(ObjectKey);
        if (Content.Length is < 1 or > 15_728_640 || Sha256.Length != 64)
        {
            throw new InvalidOperationException("The synthetic evidence fixture violates its persistence contract.");
        }

        ValidateSequencePayload(FirstSequencePayload);
        ValidateSequencePayload(SecondSequencePayload);
    }

    private static void ValidateSequencePayload(string json)
    {
        using var payload = JsonDocument.Parse(json);
        _ = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            "TAR-0008", "SECUENCIA", EvidenceRequirementKinds.DigitalRecord, payload.RootElement);
    }
}
