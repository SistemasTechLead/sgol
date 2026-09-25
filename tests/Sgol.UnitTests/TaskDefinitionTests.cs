using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class TaskDefinitionTests
{
    private static readonly Guid ActorId = Guid.Parse("019d3a20-0000-7000-8000-000000000001");
    private static readonly Guid ReleaseId = Guid.Parse("019d3a20-0000-7000-8000-000000000002");
    private static readonly DateTimeOffset Effective = new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Catalog_ContainsExactlyTheEightCanonicalDefinitions()
    {
        Assert.Equal(
            ["TAR-0005", "TAR-0007", "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0026", "TAR-0092", "TAR-0093"],
            TaskDefinitionCatalog.All.Select(item => item.TaskCode));
        Assert.Equal(8, TaskDefinitionCatalog.All.Select(item => item.Id).Distinct().Count());
        Assert.All(TaskDefinitionCatalog.All, item =>
            Assert.Equal(7, item.Id.ToByteArray()[7] >> 4));
    }

    [Fact]
    public void SessionProjectsDefinitionAdministrationOnlyForDirection()
    {
        Assert.Contains(TaskDefinitionAuthorization.Administer,
            RolePermissionProjection.ForRole(CanonicalRole.Direction));
        foreach (var role in new[] { CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
            Assert.DoesNotContain(TaskDefinitionAuthorization.Administer, RolePermissionProjection.ForRole(role));
    }

    [Theory]
    [InlineData("TAR-0001")]
    [InlineData("TAR-0195")]
    [InlineData("TAR-0196")]
    [InlineData("TAR-0202")]
    [InlineData("T221")]
    [InlineData("TAR-9999")]
    [InlineData("tar-0005")]
    public void Catalog_RejectsOptionalExcludedUnknownAndMalformedCodes(string taskCode) =>
        Assert.Throws<TaskDefinitionNotMvpException>(() => TaskDefinitionCatalog.Require(taskCode));

    [Fact]
    public void PayloadV1_AcceptsOnlyAnEmptyObject()
    {
        using var empty = JsonDocument.Parse("{}");
        using var accepted = TaskDefinitionCatalog.ValidatePayload(1, empty.RootElement);
        Assert.Equal(JsonValueKind.Object, accepted.RootElement.ValueKind);

        using var futurePolicy = JsonDocument.Parse("{\"requiredRole\":\"DIRECCION\"}");
        Assert.Throws<TaskDefinitionValidationException>(() =>
            TaskDefinitionCatalog.ValidatePayload(1, futurePolicy.RootElement));
        Assert.Throws<TaskDefinitionValidationException>(() =>
            TaskDefinitionCatalog.ValidatePayload(2, empty.RootElement));
    }

    [Fact]
    public void Versioning_PreservesV1PublishesV2AndCreatesInactiveSuccessor()
    {
        var definition = TaskDefinitionCatalog.Require("TAR-0005");
        using var payload = JsonDocument.Parse("{}");
        var v1 = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 1, 1, payload, ReleaseId);
        var v1Plan = VersioningRules.PlanPublication(
            v1.ToVersionRecord(), null, [], 1, Effective, "Publicación V1");
        v1.ApplyPublished(v1Plan.Published, activeForNew: true);

        var v2 = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 2, 1, payload, Guid.CreateVersion7());
        var v2Plan = VersioningRules.PlanPublication(
            v2.ToVersionRecord(), v1.ToVersionRecord(), [v1.ToVersionRecord()], 1, Effective.AddDays(1), "Publicación V2");
        v1.ApplySuperseded(v2Plan.Superseded!);
        v2.ApplyPublished(v2Plan.Published, activeForNew: true);

        var inactive = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 3, 1, payload, Guid.CreateVersion7());
        var inactivePlan = VersioningRules.PlanPublication(
            inactive.ToVersionRecord(), v2.ToVersionRecord(), [v1.ToVersionRecord()], 1, Effective.AddDays(2), "Desactivación");
        v2.ApplySuperseded(inactivePlan.Superseded!);
        inactive.ApplyPublished(inactivePlan.Published, activeForNew: false);

        Assert.Equal(VersionStatuses.Superseded, v1.Status);
        Assert.Equal(VersionStatuses.Superseded, v2.Status);
        Assert.Equal(TaskDefinitionStatuses.InactiveForNew, inactive.Status);
        Assert.Equal(v2.Id, inactive.SupersedesId);
        Assert.Equal(3, inactive.VersionNo);
    }

    [Fact]
    public void ObligationCreatedFromV1KeepsItsSnapshotWhenV2ReplacesTheDefinition()
    {
        var definition = TaskDefinitionCatalog.Require("TAR-0005");
        using var payload = JsonDocument.Parse("{}");
        var v1 = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 1, 1, payload, ReleaseId);
        var v1Plan = VersioningRules.PlanPublication(v1.ToVersionRecord(), null, [], 1, Effective, "V1");
        v1.ApplyPublished(v1Plan.Published, activeForNew: true);
        var obligation = new WorkObligation(Guid.CreateVersion7(), v1.Id, Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), "synthetic-origin");

        var v2 = new TaskDefinitionVersion(Guid.CreateVersion7(), definition.Id, 2, 1, payload, Guid.CreateVersion7());
        var v2Plan = VersioningRules.PlanPublication(v2.ToVersionRecord(), v1.ToVersionRecord(),
            [v1.ToVersionRecord()], 1, Effective.AddDays(1), "V2");
        v1.ApplySuperseded(v2Plan.Superseded!);
        v2.ApplyPublished(v2Plan.Published, activeForNew: true);

        Assert.Equal(v1.Id, obligation.TaskDefinitionVersionId);
        Assert.NotEqual(v2.Id, obligation.TaskDefinitionVersionId);
        Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus);
        Assert.Equal(1, obligation.RowVersion);
        Assert.Equal(VersionStatuses.Superseded, v1.Status);
    }

    [Fact]
    public async Task CreateEndpoint_RejectsAdditionalPolicyFieldsBeforeCallingService()
    {
        var service = new RecordingTaskDefinitionService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var body = JsonDocument.Parse(
            $$"""{"releaseId":"{{ReleaseId:D}}","schemaVersion":1,"taskPayload":{},"requiredRole":"DIRECCION"}""");

        var result = await TaskDefinitionApiEndpoints.HandleCreateVersionAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.CreateCommand);
    }

    [Fact]
    public async Task ListEndpoint_UsesCollectionEnvelopeRequiredBySharedClient()
    {
        var context = AuthenticatedContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var result = await TaskDefinitionApiEndpoints.HandleListAsync(
            context, new RecordingTaskDefinitionService(), CancellationToken.None);

        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(0, response.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
        Assert.Empty(response.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task PublishEndpoint_RequiresIdempotencyKeyAndIfMatch()
    {
        var service = new RecordingTaskDefinitionService();
        var context = AuthenticatedContext();
        using var body = JsonDocument.Parse("{\"effectiveFrom\":\"2026-09-04T00:00:00Z\",\"reason\":\"Publicación\"}");

        var result = await TaskDefinitionApiEndpoints.HandlePublishVersionAsync(
            "TAR-0005", Guid.CreateVersion7(), body.RootElement, context, service, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        result = await TaskDefinitionApiEndpoints.HandlePublishVersionAsync(
            "TAR-0005", Guid.CreateVersion7(), body.RootElement, context, service, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.PublishCommand);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingTaskDefinitionService : ITaskDefinitionService
    {
        public CreateTaskDefinitionVersionCommand? CreateCommand { get; private set; }

        public PublishTaskDefinitionVersionCommand? PublishCommand { get; private set; }

        public Task<IReadOnlyList<TaskDefinitionDetails>> ListAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskDefinitionDetails>>([]);

        public Task<TaskDefinitionDetails> GetAsync(Guid actorUserId, Guid correlationId, string taskCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TaskDefinitionVersionDetails> CreateVersionAsync(CreateTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default)
        {
            CreateCommand = command;
            throw new NotSupportedException();
        }

        public Task<TaskDefinitionVersionDetails> PublishVersionAsync(PublishTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default)
        {
            PublishCommand = command;
            throw new NotSupportedException();
        }

        public Task<TaskDefinitionVersionDetails> DeactivateNewAsync(DeactivateTaskDefinitionCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
