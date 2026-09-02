using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ConfigurationReleaseTests
{
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000801");
    private static readonly Guid ReleaseId = Guid.Parse("019d2d67-2c00-7000-8000-000000000802");
    private static readonly DateTimeOffset September = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Release_ConsumesVersioningCoreForInitialAndSuccessorPublication()
    {
        var first = new ConfigurationRelease(ReleaseId, Guid.CreateVersion7());
        var firstPlan = VersioningRules.PlanPublication(
            first.ToVersionRecord(),
            current: null,
            publishedHistory: [],
            expectedRowVersion: 1,
            effectiveFrom: September,
            reason: "Inicial");
        first.ApplyPublished(firstPlan.Published, 1, ActorUserId, September);

        var successor = new ConfigurationRelease(Guid.CreateVersion7(), first.BranchId);
        var successorPlan = VersioningRules.PlanPublication(
            successor.ToVersionRecord(),
            first.ToVersionRecord(),
            [first.ToVersionRecord()],
            expectedRowVersion: 1,
            effectiveFrom: September.AddDays(7),
            reason: "Sucesora");
        first.ApplySuperseded(successorPlan.Superseded!);
        successor.ApplyPublished(successorPlan.Published, 2, ActorUserId, September.AddDays(1));

        Assert.Equal(VersionStatuses.Superseded, first.Status);
        Assert.Equal(September.AddDays(7), first.EffectiveTo);
        Assert.Equal(VersionStatuses.Current, successor.Status);
        Assert.Equal(first.Id, successor.SupersedesId);
        Assert.Equal(2, successor.VersionNo);
    }

    [Fact]
    public async Task CreateDraft_RequiresIdempotencyKeyAndReturnsDraftEtag()
    {
        var service = new RecordingService(CreateDetails(VersionStatuses.Draft, rowVersion: 1));
        var invalidContext = CreateContext();

        var invalid = await ConfigurationApiEndpoints.HandleCreateDraftAsync(
            invalidContext,
            service,
            CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode);
        Assert.Null(service.CreateCommand);

        var context = CreateContext();
        var key = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = key.ToString("D");
        var result = await ConfigurationApiEndpoints.HandleCreateDraftAsync(
            context,
            service,
            CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(key, service.CreateCommand?.IdempotencyKey);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
    }

    [Fact]
    public async Task Publish_RequiresQuotedIfMatchAndMapsObsoleteVersionTo412()
    {
        var request = new PublishConfigurationReleaseRequest(September, "Publicación");
        var service = new RecordingService(CreateDetails(VersionStatuses.Current, rowVersion: 2));
        var missingContext = CreateContext();
        missingContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");

        var missing = await ConfigurationApiEndpoints.HandlePublishAsync(
            ReleaseId,
            request,
            missingContext,
            service,
            CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);
        Assert.Null(service.PublishCommand);

        var validContext = CreateContext();
        validContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        validContext.Request.Headers.IfMatch = "\"1\"";
        var valid = await ConfigurationApiEndpoints.HandlePublishAsync(
            ReleaseId,
            request,
            validContext,
            service,
            CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(valid).StatusCode);
        Assert.Equal(1, service.PublishCommand?.ExpectedRowVersion);
        Assert.Equal("\"2\"", validContext.Response.Headers.ETag);

        service.ThrowVersionConflict = true;
        var staleContext = CreateContext();
        staleContext.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        staleContext.Request.Headers.IfMatch = "\"1\"";
        var stale = await ConfigurationApiEndpoints.HandlePublishAsync(
            ReleaseId,
            request,
            staleContext,
            service,
            CancellationToken.None);
        Assert.Equal(412, Assert.IsAssignableFrom<IStatusCodeHttpResult>(stale).StatusCode);
    }

    [Fact]
    public void ResponseContract_ContainsNoCredentialOrSecretMaterial()
    {
        var properties = typeof(ConfigurationReleaseDetails)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Password", properties);
        Assert.DoesNotContain("PasswordHash", properties);
        Assert.DoesNotContain("SecurityStamp", properties);
        Assert.DoesNotContain("TotpSecret", properties);
        Assert.DoesNotContain("RecoveryCodes", properties);
        Assert.DoesNotContain("ConnectionString", properties);
    }

    private static DefaultHttpContext CreateContext() => new()
    {
        TraceIdentifier = Guid.CreateVersion7().ToString("D"),
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
            "synthetic")),
    };

    private static ConfigurationReleaseDetails CreateDetails(string status, long rowVersion) => new(
        ReleaseId,
        status == VersionStatuses.Draft ? null : 1,
        status,
        status == VersionStatuses.Draft ? null : September,
        null,
        status == VersionStatuses.Draft ? null : "Publicación",
        status == VersionStatuses.Draft ? null : ActorUserId,
        status == VersionStatuses.Draft ? null : September,
        null,
        rowVersion);

    private sealed class RecordingService(ConfigurationReleaseDetails details) : IConfigurationReleaseService
    {
        public CreateConfigurationReleaseCommand? CreateCommand { get; private set; }

        public PublishConfigurationReleaseCommand? PublishCommand { get; private set; }

        public bool ThrowVersionConflict { get; set; }

        public Task<IReadOnlyList<ConfigurationReleaseDetails>> ListAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConfigurationReleaseDetails>>([details]);

        public Task<ConfigurationReleaseDetails> CreateDraftAsync(
            CreateConfigurationReleaseCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCommand = command;
            return Task.FromResult(details);
        }

        public Task<ConfigurationReleaseDetails> PublishAsync(
            PublishConfigurationReleaseCommand command,
            CancellationToken cancellationToken = default)
        {
            PublishCommand = command;
            if (ThrowVersionConflict)
            {
                throw new VersionConflictException();
            }

            return Task.FromResult(details);
        }
    }
}
