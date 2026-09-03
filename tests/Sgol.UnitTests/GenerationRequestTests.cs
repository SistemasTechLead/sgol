using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Generation.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class GenerationRequestTests
{
    [Fact]
    public async Task Post_RequiresUuidIdempotencyKeyWithoutCallingService()
    {
        var service = new RecordingService();
        var missing = AuthenticatedContext();
        using var body = ValidBody();

        var missingResult = await GenerationRequestApiEndpoints.HandlePostAsync(
            body.RootElement, missing, service, CancellationToken.None);
        AssertProblem(missingResult, 400, "IDEMPOTENCY_KEY_INVALIDA", missing.TraceIdentifier);

        var invalid = AuthenticatedContext();
        invalid.Request.Headers["Idempotency-Key"] = "not-a-uuid";
        var invalidResult = await GenerationRequestApiEndpoints.HandlePostAsync(
            body.RootElement, invalid, service, CancellationToken.None);
        AssertProblem(invalidResult, 400, "IDEMPOTENCY_KEY_INVALIDA", invalid.TraceIdentifier);
        Assert.Null(service.CreateCommand);
    }

    [Fact]
    public async Task Post_UsesStrictCamelCaseContractAndReturnsAcceptedEnvelope()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        var key = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = key.ToString("D");
        using var body = ValidBody();

        var result = await GenerationRequestApiEndpoints.HandlePostAsync(
            body.RootElement, context, service, CancellationToken.None);

        var created = Assert.IsType<Created<object>>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.NotNull(service.CreateCommand);
        Assert.Equal(key, service.CreateCommand.IdempotencyKey);
        Assert.Equal("MANUAL_REFERENCE_V1", service.CreateCommand.OriginType);
        var json = JsonSerializer.Serialize(created.Value, JsonSerializerOptions.Web);
        Assert.Contains("\"generationRequestId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerationRequestId", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_ReplayIsOkAndConflictIsProblemJsonWithCorrelationId()
    {
        var service = new RecordingService { Result = GenerationRequestResults.Recovered };
        var recoveredContext = AuthenticatedContext();
        recoveredContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var body = ValidBody();
        var recovered = await GenerationRequestApiEndpoints.HandlePostAsync(
            body.RootElement, recoveredContext, service, CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(recovered).StatusCode);

        service.Exception = new GenerationRequestIdempotencyConflictException();
        var conflictContext = AuthenticatedContext();
        conflictContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var conflict = await GenerationRequestApiEndpoints.HandlePostAsync(
            body.RootElement, conflictContext, service, CancellationToken.None);
        AssertProblem(conflict, 409, "IDEMPOTENCY_CONFLICT", conflictContext.TraceIdentifier);
    }

    [Fact]
    public async Task Get_ForwardsActorAndMapsHorizontalDenialToHiddenNotFound()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        var id = Guid.CreateVersion7();
        var result = await GenerationRequestApiEndpoints.HandleGetAsync(
            id, context, service, CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(id, service.GetId);

        service.Exception = new GenerationRequestNotFoundException();
        var denied = await GenerationRequestApiEndpoints.HandleGetAsync(
            id, context, service, CancellationToken.None);
        AssertProblem(denied, 404, "GENERATION_REQUEST_NO_ENCONTRADA", context.TraceIdentifier);
    }

    [Fact]
    public void RoleHierarchy_AllowsOwnAndLowerLevelsForGeneration()
    {
        Assert.True(Sgol.Identity.Contracts.RoleHierarchy.CanAccessLevel("DIRECCION", "PISO_VENTAS"));
        Assert.True(Sgol.Identity.Contracts.RoleHierarchy.CanAccessLevel("SUBCOORDINACION", "SUBCOORDINACION"));
        Assert.False(Sgol.Identity.Contracts.RoleHierarchy.CanAccessLevel("SUBCOORDINACION", "ADMINISTRACION"));
    }

    private static void AssertProblem(IResult result, int status, string code, string correlationId)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(status, problem.StatusCode);
        Assert.Equal("application/problem+json", problem.ContentType);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
        Assert.Equal(correlationId, problem.ProblemDetails.Extensions["correlationId"]);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private static JsonDocument ValidBody() => JsonDocument.Parse(
        $$"""{"ruleVersionId":"{{Guid.CreateVersion7():D}}","branchId":"019d3a10-0100-7000-8000-000000000001","periodId":"{{Guid.CreateVersion7():D}}","originType":"MANUAL_REFERENCE_V1","originReference":"synthetic-reference"}""");

    private sealed class RecordingService : IGenerationRequestService
    {
        public CreateGenerationRequestCommand? CreateCommand { get; private set; }
        public Guid? GetId { get; private set; }
        public string Result { get; init; } = GenerationRequestResults.Accepted;
        public Exception? Exception { get; set; }

        public Task<GenerationRequestDetails> CreateAsync(
            CreateGenerationRequestCommand command,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            CreateCommand = command;
            return Task.FromResult(Details(Guid.CreateVersion7(), command, Result));
        }

        public Task<GenerationRequestDetails> GetAsync(
            Guid actorUserId,
            Guid correlationId,
            Guid generationRequestId,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            GetId = generationRequestId;
            return Task.FromResult(new GenerationRequestDetails(
                generationRequestId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "MANUAL_REFERENCE_V1",
                "synthetic-reference",
                GenerationRequestResults.Accepted,
                actorUserId,
                DateTimeOffset.UtcNow,
                null,
                null));
        }

        private static GenerationRequestDetails Details(
            Guid id,
            CreateGenerationRequestCommand command,
            string result) => new(
                id,
                command.RuleVersionId,
                command.BranchId,
                command.PeriodId,
                command.OriginType,
                command.OriginReference,
                result,
                command.ActorUserId,
                DateTimeOffset.UtcNow,
                null,
                null);
    }
}
