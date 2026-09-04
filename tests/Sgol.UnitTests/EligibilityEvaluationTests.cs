using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Assignment.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EligibilityEvaluationTests
{
    [Fact]
    public void ExactRolePositiveAvailabilityAndUnrestrictedShift_IsEligible()
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput(shift: "MATUTINO"),
            "SUBCOORDINACION",
            requiredShift: null);

        Assert.Empty(reasons);
    }

    [Fact]
    public void InactivePerson_IsExcluded()
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput() with { IsPersonActive = false },
            "SUBCOORDINACION",
            null);

        Assert.Equal([EligibilityExclusionReasons.InactivePerson], reasons);
    }

    [Theory]
    [InlineData(false, true, "EMPLEO_NO_VIGENTE")]
    [InlineData(true, false, "SUCURSAL_NO_COINCIDE")]
    public void InvalidEmployment_IsExcluded(
        bool hasCurrentEmployment,
        bool branchMatches,
        string expectedReason)
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput() with
            {
                HasCurrentEmployment = hasCurrentEmployment,
                BranchMatches = branchMatches,
            },
            "SUBCOORDINACION",
            null);

        Assert.Contains(expectedReason, reasons);
    }

    [Fact]
    public void MissingActiveRole_IsExcluded()
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput() with { HasActiveRole = false, ActiveRoleCode = null },
            "SUBCOORDINACION",
            null);

        Assert.Contains(EligibilityExclusionReasons.ActiveRoleMissing, reasons);
    }

    [Theory]
    [InlineData("DIRECCION")]
    [InlineData("ADMINISTRACION")]
    [InlineData("PISO_VENTAS")]
    [InlineData("SUBCOORDINADOR")]
    public void SuperiorInferiorOrSimilarRole_DoesNotReplaceExactRole(string actualRole)
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput() with { ActiveRoleCode = actualRole },
            "SUBCOORDINACION",
            null);

        Assert.Contains(EligibilityExclusionReasons.RequiredRoleMismatch, reasons);
    }

    [Fact]
    public void PositionText_DoesNotParticipateInEligibility()
    {
        var withoutRole = EligibleInput() with
        {
            HasActiveRole = false,
            ActiveRoleCode = null,
        };

        var reasons = EligibilityEvaluator.Explain(withoutRole, "SUBCOORDINACION", null);

        Assert.Contains(EligibilityExclusionReasons.ActiveRoleMissing, reasons);
    }

    [Theory]
    [InlineData(false, "DISPONIBILIDAD_NO_POSITIVA")]
    [InlineData(null, "DISPONIBILIDAD_AUSENTE")]
    public void AvailabilityMustBePositive(bool? isAvailable, string expectedReason)
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput() with { IsAvailable = isAvailable },
            "SUBCOORDINACION",
            null);

        Assert.Contains(expectedReason, reasons);
    }

    [Fact]
    public void RestrictedShift_MustMatchExactly()
    {
        var reasons = EligibilityEvaluator.Explain(
            EligibleInput(shift: "VESPERTINO"),
            "SUBCOORDINACION",
            "MATUTINO");

        Assert.Contains(EligibilityExclusionReasons.ShiftMismatch, reasons);
    }

    [Fact]
    public void MultipleFailures_PreserveEveryApprovedReasonInStableOrder()
    {
        var reasons = EligibilityEvaluator.Explain(
            new EligibilityPersonInput(
                HasCurrentEmployment: true,
                IsPersonActive: false,
                BranchMatches: false,
                HasActiveRole: true,
                ActiveRoleCode: "DIRECCION",
                IsAvailable: false,
                ShiftText: "VESPERTINO"),
            "SUBCOORDINACION",
            "MATUTINO");

        Assert.Equal(
            [
                EligibilityExclusionReasons.InactivePerson,
                EligibilityExclusionReasons.BranchMismatch,
                EligibilityExclusionReasons.RequiredRoleMismatch,
                EligibilityExclusionReasons.AvailabilityNotPositive,
                EligibilityExclusionReasons.ShiftMismatch,
            ],
            reasons);
    }

    [Fact]
    public void Hu016Snapshot_CannotContainRankingOrWinner()
    {
        using var snapshot = JsonDocument.Parse("""{"schemaVersion":1}""");
        using var reasons = JsonDocument.Parse("[]");
        var evaluation = new EligibilityEvaluation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            new DateOnly(2026, 9, 3),
            EligibilityDateSources.ManualRequest,
            Guid.CreateVersion7(),
            snapshot,
            EligibilityResults.NoEligibleCandidate);
        var candidate = new EligibilityCandidate(
            evaluation.Id,
            Guid.CreateVersion7(),
            "PER-001",
            false,
            reasons);

        Assert.Null(evaluation.WinnerPersonId);
        Assert.Null(candidate.ActiveLoad);
        Assert.Null(candidate.LastAutoAssignmentAt);
        Assert.Null(candidate.Rank);
    }

    [Fact]
    public async Task ExplanationGet_IsReadOnlyAndReturnsApprovedContract()
    {
        var service = new RecordingEligibilityService();
        var context = AuthenticatedContext();
        var obligationId = Guid.CreateVersion7();

        var result = await EligibilityEvaluationApiEndpoints.HandleGetAsync(
            obligationId,
            context,
            service,
            CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(obligationId, service.RequestedObligationId);
        Assert.False(service.EvaluateCalled);
        var json = JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value,
            JsonSerializerOptions.Web);
        Assert.Contains("\"eligibilityDate\"", json, StringComparison.Ordinal);
        Assert.Contains("\"policyVersionId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"candidates\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(true, 404)]
    public async Task ExplanationGet_HidesUnavailableResourceWithoutEvaluation(
        bool authenticated,
        int expectedStatus)
    {
        var service = new RecordingEligibilityService
        {
            Exception = new EligibilityEvaluationNotFoundException(),
        };
        var context = authenticated ? AuthenticatedContext() : new DefaultHttpContext();

        var result = await EligibilityEvaluationApiEndpoints.HandleGetAsync(
            Guid.CreateVersion7(),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.False(service.EvaluateCalled);
    }

    private static EligibilityPersonInput EligibleInput(string? shift = null) => new(
        HasCurrentEmployment: true,
        IsPersonActive: true,
        BranchMatches: true,
        HasActiveRole: true,
        ActiveRoleCode: "SUBCOORDINACION",
        IsAvailable: true,
        ShiftText: shift);

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingEligibilityService : IEligibilityEvaluationService
    {
        public Guid? RequestedObligationId { get; private set; }
        public bool EvaluateCalled { get; private set; }
        public Exception? Exception { get; init; }

        public Task<EligibilityEvaluationDetails> EvaluateAsync(
            EvaluateEligibilityCommand command,
            CancellationToken cancellationToken = default)
        {
            EvaluateCalled = true;
            throw new NotSupportedException();
        }

        public Task<EligibilityEvaluationDetails> GetLatestAsync(
            Guid actorUserId,
            Guid correlationId,
            Guid obligationId,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            RequestedObligationId = obligationId;
            return Task.FromResult(new EligibilityEvaluationDetails(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                obligationId,
                DateTimeOffset.UtcNow,
                new DateOnly(2026, 9, 3),
                EligibilityDateSources.ManualRequest,
                Guid.CreateVersion7(),
                "SUBCOORDINACION",
                null,
                EligibilityResults.EligibleCandidates,
                null,
                [],
                false));
        }
    }
}
