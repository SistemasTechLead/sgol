using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Validation;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front019PresentationTests
{
    [Theory]
    [InlineData("https://synthetic.invalid/review")]
    [InlineData("www.synthetic.invalid")]
    [InlineData("mailto:synthetic@example.invalid")]
    [InlineData("evidence.pdf")]
    [InlineData("image.JPG")]
    [InlineData("image.png")]
    [InlineData("C:\\synthetic\\file")]
    [InlineData("/private/object")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ProhibitedTransportAndBinaryReferencesCannotBecomeDecisionText(string text)
    {
        Assert.Equal("FUNDAMENTO_INVALIDO", Assert.Throws<ValidationDecisionException>(() => ValidationText.Foundation(text)).Code);
        Assert.Equal("MOTIVO_REQUERIDO", Assert.Throws<ValidationDecisionException>(() => ValidationText.Reason(text)).Code);
    }
    [Fact]
    public void TextBoundariesAndNormalizationRemainContractual()
    {
        Assert.Equal("é", ValidationText.Foundation(" e\u0301 "));
        Assert.Equal("A\tB", ValidationText.Foundation("A\tB"));
        Assert.Equal(1000, ValidationText.Foundation(new string('x', 1000)).Length);
        Assert.Equal(500, ValidationText.Reason(new string('x', 500)).Length);
        Assert.Throws<ValidationDecisionException>(() => ValidationText.Foundation(new string('x', 1001)));
        Assert.Throws<ValidationDecisionException>(() => ValidationText.Reason(new string('x', 501)));
    }
    [Theory]
    [InlineData("DIRECCION", "SUBCOORDINACION", "SUBCOORDINACION", "ADMINISTRACION", false, "ESCALAMIENTO")]
    [InlineData("ADMINISTRACION", "SUBCOORDINACION", "SUBCOORDINACION", "ADMINISTRACION", false, "ORDINARIA")]
    [InlineData("ADMINISTRACION", "ADMINISTRACION", "SUBCOORDINACION", "ADMINISTRACION", false, null)]
    [InlineData("SUBCOORDINACION", "SUBCOORDINACION", "SUBCOORDINACION", "ADMINISTRACION", true, null)]
    [InlineData("DIRECCION", "DIRECCION", "SUBCOORDINACION", "ADMINISTRACION", true, "AUTOVALIDACION_DIRECCION")]
    public void PresentationAndMutationShareStrictAuthority(string actor, string responsible, string executor, string validator, bool same, string? authority) =>
        Assert.Equal(authority, ValidationDecisionAuthorization.Issue(actor, responsible, executor, validator, same));
    [Fact]
    public void OriginalReplacementDoesNotSurviveLostRoleOrBecomePeerValidation()
    {
        Assert.Null(ValidationDecisionAuthorization.Replace("SUBCOORDINACION", "PISO_VENTAS", "ADMINISTRACION", true, false));
        Assert.Null(ValidationDecisionAuthorization.Replace("ADMINISTRACION", "ADMINISTRACION", "ADMINISTRACION", true, false));
        Assert.Equal("SUSTITUCION_SUPERIOR", ValidationDecisionAuthorization.Replace("DIRECCION", "SUBCOORDINACION", "ADMINISTRACION", false, false));
    }
    [Fact]
    public void QueryDoesNotAdmitOtherCapabilitiesDuplicateFiltersOrPartialWeek()
    {
        bool Parse(Dictionary<string, StringValues> values) => ValidationQuery.TryRead(new QueryCollection(values), out _);
        Assert.False(Parse(new() { ["pendingexecutionStatus"] = "PENDIENTE" }));
        Assert.False(Parse(new() { ["pendingresult"] = "CUMPLIDA" }));
        Assert.False(Parse(new() { ["pendingisoYear"] = "2026" }));
        Assert.False(Parse(new() { ["pendinglimit"] = "101" }));
        Assert.False(Parse(new() { ["pendinglevel"] = new StringValues(["ADMINISTRACION", "PISO_VENTAS"]) }));
        Assert.True(Parse(new() { ["pendingisoYear"] = "2026", ["pendingisoWeek"] = "40", ["supervisionexecutionStatus"] = "CONCLUIDA" }));
    }
    [Fact]
    public void IntentionBindsActorResourceVersionBodyAndExpiryWithoutDisplayingIt()
    {
        var protection = new EphemeralDataProtectionProvider(); var actor = Guid.NewGuid(); var resource = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var protector = new ValidationIntentionProtector(protection);
        var value = new ValidationIntention(actor, resource, null, Guid.NewGuid(), "\"1\"", "CUMPLIDA", "Fundamento sintético", null, null, now.AddHours(8));
        var token = protector.Protect(value);
        Assert.Equal(value, protector.Read(token, actor, resource, now));
        Assert.Null(protector.Read(token, Guid.NewGuid(), resource, now)); Assert.Null(protector.Read(token, actor, Guid.NewGuid(), now));
        Assert.Null(protector.Read(token, actor, resource, now.AddHours(9))); Assert.Null(protector.Read(token + "alterado", actor, resource, now));
        Assert.DoesNotContain(value.Foundation, token); Assert.Equal("ValidationIntention", value.ToString());
    }
    [Fact]
    public void ReturnContextPreservesItsCollectionAndRejectsAnotherActor()
    {
        var protection = new EphemeralDataProtectionProvider(); var actor = Guid.NewGuid(); var id = Guid.NewGuid();
        Assert.True(ValidationQuery.TryRead(new QueryCollection(new Dictionary<string, StringValues> { ["supervisionexecutionStatus"] = "CONCLUIDA" }), out var query));
        var context = new ValidationReturnContext(protection, actor); var token = context.Protect(query, "supervision", id);
        Assert.Equal("/validaciones?supervisionexecutionStatus=CONCLUIDA#supervision-" + id.ToString("D"), context.Read(token));
        Assert.Equal("/validaciones", new ValidationReturnContext(protection, Guid.NewGuid()).Read(token));
    }
    [Theory]
    [InlineData("DIRECCION", true)]
    [InlineData("ADMINISTRACION", true)]
    [InlineData("SUBCOORDINACION", true)]
    [InlineData("PISO_VENTAS", false)]
    public void ExistingEscalationAndReplacementPermissionsAreProjectedOnlyToSuperiors(string role, bool expected)
    {
        Assert.Equal(expected, RolePermissionProjection.ForRole(role).Contains(ValidationAuthorization.Escalate));
        Assert.Equal(expected, RolePermissionProjection.ForRole(role).Contains(ValidationAuthorization.Replace));
    }
    [Fact]
    public void UnknownProjectionAndFalseHistoryFailClosed()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ApiProtocolException>(() => ValidationPresentation.Validate(new(id, "CONCLUIDA", null, [], new("ANY", true)), id, "\"1\""));
        Assert.Throws<ApiProtocolException>(() => ValidationPresentation.Validate(new(id, "PENDIENTE", null, [], new("ORDINARIA", false)), id, "\"1\""));
        ValidationPresentation.Validate(new(id, "CONCLUIDA", null, [], null), id, "\"1\"");
        Assert.Throws<ApiProtocolException>(() => ValidationPresentation.Result("VALIDADA"));
    }
}
