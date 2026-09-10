using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ValidationDecisionTests
{
    [Theory]
    [InlineData("CUMPLIDA")]
    [InlineData("INCOMPLETA")]
    [InlineData("NO_CUMPLIDA")]
    public void OnlyApprovedResultsAreDefined(string result) => Assert.True(ValidationResults.IsDefined(result));

    [Theory]
    [InlineData("")]
    [InlineData("DESCONOCIDA")]
    [InlineData("cumplida")]
    public void UnknownResultsAreRejected(string result) => Assert.False(ValidationResults.IsDefined(result));

    [Theory]
    [InlineData("DIRECCION", true)]
    [InlineData("ADMINISTRACION", true)]
    [InlineData("SUBCOORDINACION", true)]
    [InlineData("PISO_VENTAS", false)]
    public void ValidationPermissionsFollowTheApprovedRoleMatrix(string role, bool expected)
    {
        Assert.Equal(expected, RoleHierarchy.GrantsValidationIssue(role));
        Assert.Equal(expected, RoleHierarchy.GrantsValidationEscalation(role));
        Assert.Equal(expected, RoleHierarchy.GrantsValidationReplacement(role));
    }

    [Theory]
    [InlineData("TAR-0005", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0007", "PISO_VENTAS", "SUBCOORDINACION")]
    [InlineData("TAR-0008", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0011", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0018", "PISO_VENTAS", "SUBCOORDINACION")]
    [InlineData("TAR-0026", "ADMINISTRACION", "DIRECCION")]
    [InlineData("TAR-0092", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0093", "SUBCOORDINACION", "ADMINISTRACION")]
    public void AllEightTarAuthorityPairsPermitOnlyTheExactOrdinaryValidator(string taskCode, string executor, string validator)
    {
        var policy = ValidationPolicyCatalog.Require(taskCode);
        Assert.Equal(executor, policy.ExecutorRole);
        Assert.Equal(validator, policy.ValidatorRole);
        Assert.True(RoleHierarchy.CanIssueValidationOrdinarily(validator, executor, executor, validator, false));
        Assert.False(RoleHierarchy.CanIssueValidationOrdinarily(executor, executor, executor, validator, false));
        if (validator != CanonicalRole.Direction)
            Assert.False(RoleHierarchy.CanIssueValidationOrdinarily("DIRECCION", executor, executor, validator, false));
    }

    [Fact]
    public void EscalationSelfValidationAndReplacementRequireTheirExactRelationships()
    {
        Assert.True(RoleHierarchy.CanEscalateValidation("DIRECCION", "SUBCOORDINACION", "ADMINISTRACION", false));
        Assert.False(RoleHierarchy.CanEscalateValidation("ADMINISTRACION", "SUBCOORDINACION", "ADMINISTRACION", false));
        Assert.True(RoleHierarchy.CanSelfValidateAsDirection("DIRECCION", true));
        Assert.False(RoleHierarchy.CanSelfValidateAsDirection("ADMINISTRACION", true));
        Assert.True(RoleHierarchy.CanReplaceValidationAsOriginal("ADMINISTRACION", "SUBCOORDINACION", "ADMINISTRACION", true, false));
        Assert.False(RoleHierarchy.CanReplaceValidationAsOriginal("ADMINISTRACION", "ADMINISTRACION", "ADMINISTRACION", true, false));
        Assert.True(RoleHierarchy.CanReplaceValidationAsSuperior("DIRECCION", "SUBCOORDINACION", "ADMINISTRACION", false));
        Assert.False(RoleHierarchy.CanReplaceValidationAsSuperior("ADMINISTRACION", "SUBCOORDINACION", "ADMINISTRACION", false));
    }

    [Fact]
    public void RequirementAndDecisionPreserveAOneCurrentVersionChain()
    {
        var now = new DateTimeOffset(2026, 9, 10, 20, 0, 0, TimeSpan.Zero);
        var requirement = new ValidationRequirement(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now);
        var first = Decision(requirement.Id, 1, null, ValidationAuthorityTypes.Ordinary, null, now);

        requirement.Resolve(now, 1);
        first.Supersede();
        var second = Decision(requirement.Id, 2, first.Id, ValidationAuthorityTypes.OriginalReplacement,
            "Corrección fundada de la decisión.", now.AddMinutes(1));
        requirement.Advance(now.AddMinutes(1), 1);

        Assert.Equal(ValidationStatuses.Superseded, first.Status);
        Assert.Equal(ValidationStatuses.Current, second.Status);
        Assert.Equal(first.Id, second.SupersedesId);
        Assert.Equal(2, requirement.RowVersion);
        Assert.Equal(ValidationStatuses.Resolved, requirement.Status);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("<script>")]
    [InlineData("línea\notra")]
    public void InvalidFoundationIsRejectedWithoutAnEntity(string value) =>
        Assert.Equal("FUNDAMENTO_INVALIDO", Assert.Throws<ValidationDecisionException>(() => ValidationText.Foundation(value)).Code);

    private static ValidationDecisionVersion Decision(Guid requirementId, int version, Guid? supersedes,
        string authority, string? reason, DateTimeOffset at) => new(Guid.CreateVersion7(), requirementId, version,
        ValidationResults.Fulfilled, "La evidencia vigente acredita el criterio.", authority, Guid.CreateVersion7(),
        Guid.CreateVersion7(), "ADMINISTRACION", Guid.CreateVersion7(), Guid.CreateVersion7(), at, reason,
        supersedes, Guid.CreateVersion7());
}
