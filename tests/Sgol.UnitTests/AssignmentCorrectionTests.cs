using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Assignment.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class AssignmentCorrectionTests
{
    [Fact]
    public void ReasonNormalizationIsCanonicalAndBounded()
    {
        Assert.Equal("Motivo válido", AssignmentCorrectionReason.Normalize("  Motivo\t válido  "));
        Assert.Throws<AssignmentCorrectionReasonInvalidException>(() => AssignmentCorrectionReason.Normalize(null));
        Assert.Throws<AssignmentCorrectionReasonInvalidException>(() => AssignmentCorrectionReason.Normalize("   "));
        Assert.Throws<AssignmentCorrectionReasonInvalidException>(() => AssignmentCorrectionReason.Normalize("breve"));
        Assert.Throws<AssignmentCorrectionReasonInvalidException>(() =>
            AssignmentCorrectionReason.Normalize(new string('x', AssignmentCorrectionReason.MaximumLength + 1)));
    }

    [Theory]
    [InlineData(CanonicalRole.Direction, CanonicalRole.Administration, true)]
    [InlineData(CanonicalRole.Direction, CanonicalRole.SalesFloor, true)]
    [InlineData(CanonicalRole.Administration, CanonicalRole.Subcoordination, true)]
    [InlineData(CanonicalRole.Subcoordination, CanonicalRole.SalesFloor, true)]
    [InlineData(CanonicalRole.Subcoordination, CanonicalRole.Subcoordination, false)]
    [InlineData(CanonicalRole.SalesFloor, CanonicalRole.Subcoordination, false)]
    public void CorrectionHierarchyIsStrictAndCanonical(string actor, string target, bool expected)
    {
        Assert.Equal(expected, RoleHierarchy.IsStrictlySuperior(actor, target));
        Assert.Equal(actor != CanonicalRole.SalesFloor, RoleHierarchy.GrantsAssignmentCorrection(actor));
        Assert.False(RoleHierarchy.IsStrictlySuperior("Subcoordinador", target));
    }

    [Fact]
    public void AssignmentSupersessionPreservesImmediateChainAndCorrectionMetadata()
    {
        var obligation = Guid.CreateVersion7();
        var original = new AssignmentVersion(
            Guid.CreateVersion7(), obligation, Guid.CreateVersion7(), AssignmentVersionStatuses.Current,
            AssignmentTypes.Automatic, JsonDocument.Parse("{}"), DateTimeOffset.UtcNow.AddMinutes(-1));
        original.Supersede();
        var successor = new AssignmentVersion(
            Guid.CreateVersion7(), obligation, Guid.CreateVersion7(), AssignmentVersionStatuses.Current,
            AssignmentTypes.Correction, JsonDocument.Parse("{}"), DateTimeOffset.UtcNow,
            "Motivo válido", Guid.CreateVersion7(), original.Id);

        Assert.Equal(AssignmentVersionStatuses.Superseded, original.Status);
        Assert.Equal(original.Id, successor.SupersedesId);
        Assert.Throws<InvalidOperationException>(original.Supersede);
    }

    [Fact]
    public async Task EndpointRequiresAuthenticationIdempotencyAndIfMatchBeforeCallingService()
    {
        var service = new RecordingService();
        using var body = ValidBody();
        var anonymous = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        AssertProblem(await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            Guid.CreateVersion7(), body.RootElement, anonymous, service, CancellationToken.None), 401,
            "AUTENTICACION_REQUERIDA");

        var missingKey = AuthenticatedContext();
        AssertProblem(await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            Guid.CreateVersion7(), body.RootElement, missingKey, service, CancellationToken.None), 400,
            "IDEMPOTENCY_KEY_REQUERIDA");

        var missingEtag = AuthenticatedContext();
        missingEtag.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        AssertProblem(await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            Guid.CreateVersion7(), body.RootElement, missingEtag, service, CancellationToken.None), 400,
            "IF_MATCH_REQUERIDO");
        Assert.Null(service.Command);
    }

    [Fact]
    public async Task EndpointUsesStrictBodyAndReturnsCreatedOrRecoveredWithEtag()
    {
        var service = new RecordingService();
        var context = ValidContext();
        var obligationId = Guid.CreateVersion7();
        using var body = ValidBody();

        var createdResult = await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            obligationId, body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(createdResult).StatusCode);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
        Assert.Equal($"/api/v1/obligations/{obligationId:D}", context.Response.Headers.Location);
        Assert.Equal(1, service.Command?.ExpectedRowVersion);
        Assert.Equal("Motivo de corrección", service.Command?.Reason);

        service.ResultName = AssignmentCorrectionResults.Recovered;
        var replayContext = ValidContext();
        var recovered = await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            obligationId, body.RootElement, replayContext, service, CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(recovered).StatusCode);

        using var extra = JsonDocument.Parse(
            $$"""{"newResponsiblePersonId":"{{Guid.CreateVersion7():D}}","eligibilityEvaluationId":"{{Guid.CreateVersion7():D}}","reason":"Motivo de corrección","extra":true}""");
        var invalid = await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            obligationId, extra.RootElement, ValidContext(), service, CancellationToken.None);
        AssertProblem(invalid, 400, "SOLICITUD_CORRECCION_INVALIDA");
    }

    [Theory]
    [InlineData(typeof(AssignmentCorrectionAccessDeniedException), 403, AssignmentCorrectionErrors.AccessDenied)]
    [InlineData(typeof(AssignmentCorrectionVersionConflictException), 412, AssignmentCorrectionErrors.VersionConflict)]
    [InlineData(typeof(AssignmentCorrectionResponsibleIneligibleException), 422, AssignmentCorrectionErrors.ResponsibleIneligible)]
    [InlineData(typeof(AssignmentCorrectionIdempotencyConflictException), 409, AssignmentCorrectionErrors.IdempotencyConflict)]
    public async Task EndpointMapsApprovedFailures(Type exceptionType, int status, string code)
    {
        var service = new RecordingService
        {
            Exception = (Exception)Activator.CreateInstance(exceptionType)!,
        };
        using var body = ValidBody();
        var result = await AssignmentCorrectionApiEndpoints.HandlePostAsync(
            Guid.CreateVersion7(), body.RootElement, ValidContext(), service, CancellationToken.None);
        AssertProblem(result, status, code);
    }

    private static DefaultHttpContext ValidContext()
    {
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"1\"";
        return context;
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private static JsonDocument ValidBody() => JsonDocument.Parse(
        $$"""{"newResponsiblePersonId":"{{Guid.CreateVersion7():D}}","eligibilityEvaluationId":"{{Guid.CreateVersion7():D}}","reason":"  Motivo   de corrección  "}""");

    private static void AssertProblem(IResult result, int status, string code)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
        Assert.Equal(status, problem.StatusCode);
        Assert.True(problem.ProblemDetails.Extensions.ContainsKey("correlationId"));
    }

    private sealed class RecordingService : IAssignmentCorrectionService
    {
        public CorrectAssignmentCommand? Command { get; private set; }
        public string ResultName { get; set; } = AssignmentCorrectionResults.Created;
        public Exception? Exception { get; init; }

        public Task<AssignmentCorrectionResult> CorrectAsync(
            CorrectAssignmentCommand command,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Command = command;
            return Task.FromResult(new AssignmentCorrectionResult(
                ResultName,
                Guid.CreateVersion7(),
                command.ObligationId,
                Guid.CreateVersion7(),
                command.NewResponsiblePersonId,
                AssignmentVersionStatuses.Current,
                AssignmentTypes.Correction,
                command.Reason,
                command.ActorUserId,
                DateTimeOffset.UtcNow,
                Guid.CreateVersion7(),
                2));
        }
    }
}
