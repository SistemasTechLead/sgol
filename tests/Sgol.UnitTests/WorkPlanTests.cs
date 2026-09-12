using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class WorkPlanTests
{
    [Fact]
    public void NewPlanHasStableIdentityDraftStatusAndInitialVersion()
    {
        var id = Guid.CreateVersion7();
        var periodId = Guid.CreateVersion7();

        var plan = new WorkPlan(id, BranchScope.LorettaId, periodId);

        Assert.Equal(id, plan.Id);
        Assert.Equal(BranchScope.LorettaId, plan.BranchId);
        Assert.Equal(periodId, plan.PeriodId);
        Assert.Equal(WorkPlanStatuses.Draft, plan.Status);
        Assert.Equal(1, plan.RowVersion);
    }

    [Fact]
    public async Task EndpointRequiresSessionAndIdempotencyWithoutCallingService()
    {
        var service = new RecordingService();
        var anonymous = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        AssertProblem(await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", null, anonymous, service, CancellationToken.None),
            401, "AUTENTICACION_REQUERIDA");

        var missingKey = AuthenticatedContext(withKey: false);
        AssertProblem(await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", null, missingKey, service, CancellationToken.None),
            400, "IDEMPOTENCY_KEY_REQUERIDA");

        Assert.Null(service.Command);
    }

    [Fact]
    public async Task EndpointAcceptsAbsentOrEmptyBodyAndReturnsApprovedEtag()
    {
        var service = new RecordingService();
        var absentBody = AuthenticatedContext();

        var created = await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", null, absentBody, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(created).StatusCode);
        Assert.Equal("\"1\"", absentBody.Response.Headers.ETag);
        Assert.Equal(BranchScope.LorettaId, service.Command?.BranchId);
        Assert.Equal(2026, service.Command?.IsoYear);
        Assert.Equal(36, service.Command?.IsoWeek);

        using var emptyDocument = JsonDocument.Parse("{}");
        service.ResultName = WorkPlanResults.Recovered;
        var emptyBody = AuthenticatedContext();
        var recovered = await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", emptyDocument.RootElement, emptyBody, service, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(recovered).StatusCode);
        Assert.Equal("\"1\"", emptyBody.Response.Headers.ETag);
    }

    [Fact]
    public async Task EndpointRejectsAdditionalBodyFieldsAndNonDecimalRoute()
    {
        var service = new RecordingService();
        using var body = JsonDocument.Parse("{\"branchId\":\"LOR-001\"}");

        AssertProblem(await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", body.RootElement, AuthenticatedContext(), service, CancellationToken.None),
            400, "SOLICITUD_PLAN_INVALIDA");
        AssertProblem(await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "+36", null, AuthenticatedContext(), service, CancellationToken.None),
            400, "SOLICITUD_PLAN_INVALIDA");
        Assert.Null(service.Command);
    }

    [Theory]
    [InlineData(typeof(WorkPlanAccessDeniedException), 403, "ACCESO_DENEGADO")]
    [InlineData(typeof(WorkPlanPeriodNotFoundException), 404, "PERIODO_NO_ENCONTRADO")]
    [InlineData(typeof(WorkPlanIsoWeekInvalidException), 422, "SEMANA_ISO_INVALIDA")]
    [InlineData(typeof(WorkPlanIdempotencyConflictException), 409, "IDEMPOTENCY_CONFLICT")]
    public async Task EndpointMapsApprovedErrors(Type exceptionType, int status, string code)
    {
        var service = new RecordingService
        {
            Exception = (WorkPlanException)Activator.CreateInstance(exceptionType)!,
        };

        var result = await WorkPlanApiEndpoints.HandleEnsureAsync(
            "2026", "36", null, AuthenticatedContext(), service, CancellationToken.None);

        AssertProblem(result, status, code);
    }

    private static DefaultHttpContext AuthenticatedContext(bool withKey = true)
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))],
            "synthetic"));
        if (withKey)
        {
            context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
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

    private sealed class RecordingService : IWorkPlanService
    {
        public EnsureWorkPlanCommand? Command { get; private set; }

        public string ResultName { get; set; } = WorkPlanResults.Created;

        public WorkPlanException? Exception { get; init; }

        public Task<WorkPlanEnsureResult> EnsureAsync(
            EnsureWorkPlanCommand command,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Command = command;
            return Task.FromResult(new WorkPlanEnsureResult(
                ResultName,
                Guid.CreateVersion7(),
                command.BranchId,
                BranchScope.LorettaCode,
                Guid.CreateVersion7(),
                command.IsoYear,
                command.IsoWeek,
                WorkPlanStatuses.Draft,
                1));
        }
    }
}
