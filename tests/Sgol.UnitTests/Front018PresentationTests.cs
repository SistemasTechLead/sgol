using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.MyWork;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front018PresentationTests
{
    [Theory]
    [InlineData("DIRECCION", true)]
    [InlineData("ADMINISTRACION", true)]
    [InlineData("SUBCOORDINACION", true)]
    [InlineData("PISO_VENTAS", false)]
    public void ProjectionUsesOnlyExistingCanonicalPermissions(string role, bool replace)
    {
        var permissions = RolePermissionProjection.ForRole(role);
        Assert.Contains(ObligationConclusionAuthorization.Execute, permissions);
        Assert.Equal(replace, permissions.Contains(EvidenceAuthorization.Replace));
    }

    [Theory]
    [InlineData("PENDIENTE", true, false, true)]
    [InlineData("PENDIENTE", false, true, false)]
    [InlineData("CONCLUIDA", true, false, false)]
    [InlineData("CONCLUIDA", false, true, true)]
    [InlineData("CONCLUIDA", false, false, false)]
    [InlineData("UNKNOWN", true, true, false)]
    public void ReplacementAuthorityKeepsStateAndStrictHierarchy(string status, bool own, bool superior, bool expected) =>
        Assert.Equal(expected, EvidenceAuthorization.CanReplace(status, own, superior));

    [Theory]
    [InlineData("PENDIENTE", true, true, true)]
    [InlineData("PENDIENTE", false, true, false)]
    [InlineData("PENDIENTE", true, false, false)]
    [InlineData("CONCLUIDA", true, true, false)]
    public void ConclusionRequiresOwnPendingAndExecutionPermission(string status, bool own, bool permission, bool expected) =>
        Assert.Equal(expected, ObligationConclusionAuthorization.CanConclude(status, own, permission));

    [Fact]
    public void BinaryRoundTripTreatsJsonNullAsAbsentPayloadAndRejectsMixedOrWrongKindRepresentations()
    {
        var requirement = new ObligationEvidenceRequirement(Guid.NewGuid(), "EXPEDIENTE", "DOCUMENTO_REFERENCIADO", "SIEMPRE", 1);
        var detail = new ObligationDetail(Guid.NewGuid(), new(Guid.NewGuid(), "TAR-0008", "Sintética", new(Guid.NewGuid(), 1, "VIGENTE", null, null, 1)),
            null!, null!, null!, "PENDIENTE", "DISPONIBLE", null, new Dictionary<string, string>(), null!, [], new(Guid.NewGuid(), [requirement]));
        var item = new EvidenceDetails(Guid.NewGuid(), 1, new(requirement.RequirementVersionId, requirement.RequirementCode, requirement.Kind),
            new(Guid.NewGuid(), 1, "VIGENTE", Guid.NewGuid(), DateTimeOffset.UtcNow, null, null), new(Guid.NewGuid(), "sintetico.pdf", "application/pdf", 128, new string('a', 64), null), null);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var received = JsonSerializer.Deserialize<EvidenceDetails>(JsonSerializer.Serialize(item, options), options)!;
        EvidenceReviewPresentation.Validate(received, detail);
        using var mixed = JsonDocument.Parse("{\"schemaVersion\":1,\"sequenceSummary\":\"Sintética\"}");
        Assert.Throws<Sgol.Web.Presentation.ApiClient.ApiProtocolException>(() => EvidenceReviewPresentation.Validate(received with { StructuredPayload = mixed }, detail));
        Assert.Throws<Sgol.Web.Presentation.ApiClient.ApiProtocolException>(() => EvidenceReviewPresentation.Validate(received with { File = received.File! with { MediaType = "image/png" } }, detail));
        Assert.Throws<Sgol.Web.Presentation.ApiClient.ApiProtocolException>(() => EvidenceReviewPresentation.Validate(received with { File = null }, detail));
        received.StructuredPayload?.Dispose();
    }

    [Fact]
    public void ConclusionAndReplacementProtectionPreservesOriginalIntentAndRejectsOtherActorResourceTamperingAndExpiry()
    {
        var provider = new EphemeralDataProtectionProvider(); var now = DateTimeOffset.UtcNow;
        var conclusion = new ConclusionIntention(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "\"3\"", now.AddMinutes(5));
        var protector = new ConclusionIntentionProtector(provider); var token = protector.Protect(conclusion);
        Assert.Equal(conclusion, protector.Read(token, conclusion.Actor, conclusion.Obligation, now));
        Assert.Null(protector.Read(token, Guid.NewGuid(), conclusion.Obligation, now));
        Assert.Null(protector.Read(token, conclusion.Actor, Guid.NewGuid(), now));
        Assert.Null(protector.Read(token, conclusion.Actor, conclusion.Obligation, now.AddMinutes(5)));
        Assert.Null(protector.Read(token[..^8] + "tampered", conclusion.Actor, conclusion.Obligation, now));
        Assert.Equal("ConclusionIntention", conclusion.ToString());
        var replacement = new EvidenceIntention(conclusion.Actor, conclusion.Obligation, "MERCANCIA", "replacement",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), conclusion.ExpiresAt, JsonSerializer.SerializeToElement(new { reason = "Sintético", structuredPayload = new { schemaVersion = 1, merchandiseReference = "MER-02" } }),
            ReplacementItem: Guid.NewGuid(), ReplacementRowVersion: 7, ReplacementVersionNo: 2, Reason: "Sintético");
        var evidence = new EvidenceIntentionProtector(provider); var encrypted = evidence.Protect(replacement);
        Assert.DoesNotContain("MER-02", encrypted);
        var recovered = evidence.Read(encrypted, replacement.Actor, replacement.Obligation, "replacement", now)!;
        Assert.Equal(replacement.Key, recovered.Key); Assert.Equal(7, recovered.ReplacementRowVersion);
        Assert.Equal(replacement.ReplacementItem, recovered.ReplacementItem); Assert.Equal(replacement.Body.GetRawText(), recovered.Body.GetRawText());
        Assert.Null(evidence.Read(encrypted, replacement.Actor, replacement.Obligation, "structured", now));
        Assert.Null(evidence.Read(encrypted, Guid.NewGuid(), replacement.Obligation, "replacement", now));
    }
}
