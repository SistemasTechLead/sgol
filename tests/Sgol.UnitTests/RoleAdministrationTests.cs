using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class RoleAdministrationTests
{
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000701");
    private static readonly Guid TargetUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000702");

    [Fact]
    public async Task FirstAssignment_ForwardsCanonicalRoleWithoutInventingActionOrVersion()
    {
        var service = new RecordingRoleService(CreateDetails(CanonicalRole.Administration));
        var context = CreateContext();
        var key = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = key.ToString("D");

        var result = await RoleApiEndpoints.HandleChangeAsync(
            TargetUserId,
            new ChangeRoleAssignmentRequest(CanonicalRole.Administration, "Asignación sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(ActorUserId, service.Command?.ActorUserId);
        Assert.Equal(TargetUserId, service.Command?.UserId);
        Assert.Equal(key, service.Command?.IdempotencyKey);
        Assert.Equal(CanonicalRole.Administration, service.Command?.RoleCode);
        Assert.Null(service.Command?.ExpectedRowVersion);
    }

    [Fact]
    public async Task ChangeAndRevoke_UseIfMatchAndNullRoleContract()
    {
        var service = new RecordingRoleService(CreateDetails(CanonicalRole.Subcoordination, rowVersion: 2));
        var context = CreateContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"1\"";

        var result = await RoleApiEndpoints.HandleChangeAsync(
            TargetUserId,
            new ChangeRoleAssignmentRequest(null, "Revocación sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command?.RoleCode);
        Assert.Equal(1, service.Command?.ExpectedRowVersion);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
    }

    [Fact]
    public async Task InvalidIfMatch_IsRejectedWithoutCallingService()
    {
        var service = new RecordingRoleService(CreateDetails(CanonicalRole.Administration));
        var context = CreateContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "not-a-version";

        var result = await RoleApiEndpoints.HandleChangeAsync(
            TargetUserId,
            new ChangeRoleAssignmentRequest(CanonicalRole.Administration, "Motivo sintético"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    [Fact]
    public async Task GetRoleHistory_UsesExistingDetailsAndActiveEtag()
    {
        var service = new RecordingRoleService(CreateDetails(CanonicalRole.Administration, rowVersion: 3));
        var context = CreateContext();

        var result = await RoleApiEndpoints.HandleGetAsync(TargetUserId, context, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"3\"", context.Response.Headers.ETag);
        Assert.Equal(TargetUserId, service.ReadUserId);
    }

    [Fact]
    public async Task GetRoleHistory_WithoutActiveRoleHasNoEtag()
    {
        var service = new RecordingRoleService(new RoleAssignmentDetails(TargetUserId,
            BranchScope.LorettaId, []));
        var context = CreateContext();

        var result = await RoleApiEndpoints.HandleGetAsync(TargetUserId, context, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag));
    }

    [Theory]
    [InlineData(CanonicalRole.Administration, CanonicalRole.Administration, false, false)]
    [InlineData(CanonicalRole.Administration, CanonicalRole.Subcoordination, false, true)]
    [InlineData(CanonicalRole.Administration, CanonicalRole.SalesFloor, false, true)]
    [InlineData(CanonicalRole.Administration, CanonicalRole.Direction, false, false)]
    [InlineData(CanonicalRole.Subcoordination, CanonicalRole.Administration, false, false)]
    [InlineData(CanonicalRole.Direction, CanonicalRole.Direction, false, true)]
    [InlineData(CanonicalRole.Direction, CanonicalRole.SalesFloor, false, true)]
    [InlineData(CanonicalRole.SalesFloor, CanonicalRole.SalesFloor, true, true)]
    public void Hierarchy_AllowsOwnAndLowerButRejectsPeerAndSuperior(
        string actorRole,
        string targetRole,
        bool sameUser,
        bool expected)
    {
        Assert.Equal(expected, RoleHierarchy.CanAccess(actorRole, targetRole, sameUser));
    }

    [Fact]
    public void ResponseContract_ExcludesCredentialsEmploymentAndSecurityStamp()
    {
        var properties = typeof(RoleAssignmentSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Password", properties);
        Assert.DoesNotContain("PasswordHash", properties);
        Assert.DoesNotContain("SecurityStamp", properties);
        Assert.DoesNotContain("TotpSecret", properties);
        Assert.DoesNotContain("RecoveryCodes", properties);
        Assert.DoesNotContain("PositionText", properties);
        Assert.DoesNotContain("ShiftText", properties);
        Assert.DoesNotContain("Person", properties);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = Guid.CreateVersion7().ToString("D"),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
                "synthetic")),
        };
        return context;
    }

    private static RoleAssignmentDetails CreateDetails(string roleCode, long rowVersion = 1) => new(
        TargetUserId,
        BranchScope.LorettaId,
        [new RoleAssignmentSnapshot(
            Guid.CreateVersion7(),
            roleCode,
            RoleAssignmentStatus.Active,
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
            null,
            null,
            rowVersion)]);

    private sealed class RecordingRoleService(RoleAssignmentDetails details) : IRoleAssignmentService
    {
        public ChangeRoleAssignmentCommand? Command { get; private set; }

        public Guid? ReadUserId { get; private set; }

        public Task<RoleAssignmentDetails> GetAsync(Guid actorUserId, Guid correlationId,
            Guid userId, CancellationToken cancellationToken = default)
        {
            ReadUserId = userId;
            return Task.FromResult(details);
        }

        public Task<RoleAssignmentMutationResult> ChangeAsync(
            ChangeRoleAssignmentCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(new RoleAssignmentMutationResult(details, Replayed: false));
        }
    }
}
