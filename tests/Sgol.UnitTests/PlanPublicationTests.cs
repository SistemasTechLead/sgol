using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class PlanPublicationTests
{
    [Fact]
    public void PublicationChangesPlanOnceAndPreservesHistoricalVersion()
    {
        var plan = new WorkPlan(Guid.CreateVersion7(), BranchScope.LorettaId, Guid.CreateVersion7());
        plan.ApplyPublication();
        Assert.Equal(WorkPlanStatuses.Published, plan.Status);
        Assert.Equal(2, plan.RowVersion);

        var first = new PlanVersion(
            Guid.CreateVersion7(), plan.Id, 1, CanonicalRole.Subcoordination,
            Guid.CreateVersion7(), DateTimeOffset.UtcNow, null, plan.RowVersion);
        first.Supersede();

        Assert.Equal(PlanVersionStatuses.Superseded, first.Status);
        Assert.Throws<InvalidOperationException>(first.Supersede);
    }

    [Fact]
    public async Task EndpointRequiresSessionKeyAndIfMatchWithoutCallingService()
    {
        var service = new RecordingService(Guid.CreateVersion7());
        var anonymous = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        AssertProblem(await PlanPublicationApiEndpoints.HandlePublishAsync(
            Guid.CreateVersion7().ToString("D"), null, anonymous, service, CancellationToken.None),
            401, "AUTENTICACION_REQUERIDA");

        var missingKey = AuthenticatedContext(withKey: false);
        AssertProblem(await PlanPublicationApiEndpoints.HandlePublishAsync(
            Guid.CreateVersion7().ToString("D"), null, missingKey, service, CancellationToken.None),
            400, "IDEMPOTENCY_KEY_INVALIDA");

        var missingIfMatch = AuthenticatedContext(withIfMatch: false);
        AssertProblem(await PlanPublicationApiEndpoints.HandlePublishAsync(
            Guid.CreateVersion7().ToString("D"), null, missingIfMatch, service, CancellationToken.None),
            400, "IF_MATCH_REQUERIDO");

        Assert.Null(service.Command);
    }

    [Fact]
    public async Task EndpointAcceptsOnlyEmptyBodyAndReturnsPublicationEtag()
    {
        var planId = Guid.CreateVersion7();
        var service = new RecordingService(planId);
        var context = AuthenticatedContext();

        var created = await PlanPublicationApiEndpoints.HandlePublishAsync(
            planId.ToString("D"), null, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(created).StatusCode);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
        Assert.Equal(planId, service.Command?.PlanId);
        Assert.Equal(1, service.Command?.ExpectedRowVersion);

        using var invalid = JsonDocument.Parse("{\"scopeRole\":\"PISO_VENTAS\"}");
        AssertProblem(await PlanPublicationApiEndpoints.HandlePublishAsync(
            planId.ToString("D"), invalid.RootElement, AuthenticatedContext(), service, CancellationToken.None),
            400, "SOLICITUD_PUBLICACION_INVALIDA");
    }

    [Fact]
    public async Task EndpointReturnsRecoveredPublicationAndOriginalEtag()
    {
        var planId = Guid.CreateVersion7();
        var service = new RecordingService(planId) { ResultName = PlanPublicationResults.Recovered };
        var context = AuthenticatedContext();

        var recovered = await PlanPublicationApiEndpoints.HandlePublishAsync(
            planId.ToString("D"), null, context, service, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(recovered).StatusCode);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
    }

    [Theory]
    [InlineData(typeof(PlanPublicationAccessDeniedException), 403, "ACCESO_DENEGADO")]
    [InlineData(typeof(PlanPublicationNotFoundException), 404, "PLAN_NO_ENCONTRADO")]
    [InlineData(typeof(PlanPublicationIdempotencyConflictException), 409, "IDEMPOTENCY_CONFLICT")]
    [InlineData(typeof(PlanPublicationVersionConflictException), 412, "VERSION_CONFLICT")]
    [InlineData(typeof(PlanPublicationUnassignedObligationException), 422, "OBLIGACION_SIN_ASIGNACION")]
    [InlineData(typeof(PlanPublicationNoChangesException), 422, "SIN_NOVEDADES_PUBLICABLES")]
    public async Task EndpointMapsApprovedErrors(Type exceptionType, int status, string code)
    {
        var service = new RecordingService(Guid.CreateVersion7())
        {
            Exception = (PlanPublicationException)Activator.CreateInstance(exceptionType)!,
        };
        var result = await PlanPublicationApiEndpoints.HandlePublishAsync(
            service.PlanId.ToString("D"), null, AuthenticatedContext(), service, CancellationToken.None);

        AssertProblem(result, status, code);
    }

    private static DefaultHttpContext AuthenticatedContext(bool withKey = true, bool withIfMatch = true)
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))],
            "synthetic"));
        if (withKey)
        {
            context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        }

        if (withIfMatch)
        {
            context.Request.Headers.IfMatch = "\"1\"";
        }

        return context;
    }

    private static void AssertProblem(IResult result, int status, string code)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(status, problem.StatusCode);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
        Assert.True(problem.ProblemDetails.Extensions.ContainsKey("correlationId"));
    }

    private sealed class RecordingService(Guid planId) : IPlanPublicationService
    {
        public Guid PlanId { get; } = planId;
        public PublishWorkPlanCommand? Command { get; private set; }
        public string ResultName { get; set; } = PlanPublicationResults.Initial;
        public PlanPublicationException? Exception { get; init; }

        public Task<PlanPublicationResult> PublishAsync(
            PublishWorkPlanCommand command,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Command = command;
            var item = new PlanPublicationItem(Guid.CreateVersion7(), Guid.CreateVersion7());
            return Task.FromResult(new PlanPublicationResult(
                ResultName,
                command.PlanId,
                WorkPlanStatuses.Published,
                Guid.CreateVersion7(),
                1,
                PlanVersionStatuses.Current,
                CanonicalRole.Subcoordination,
                command.ActorUserId,
                DateTimeOffset.UtcNow,
                [item],
                [item],
                2));
        }
    }
}
