using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EligibilityPolicyTests
{
    [Fact]
    public void SessionProjectsEligibilityAdministrationOnlyForDirection()
    {
        Assert.Contains(EligibilityPolicyAuthorization.Administer, RolePermissionProjection.ForRole(CanonicalRole.Direction));
        foreach (var role in new[] { CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor })
            Assert.DoesNotContain(EligibilityPolicyAuthorization.Administer, RolePermissionProjection.ForRole(role));
    }

    [Fact]
    public async Task GetEndpoint_RequiresSessionAndReturnsHistoryEnvelope()
    {
        var service = new RecordingService();
        var denied = await EligibilityPolicyApiEndpoints.HandleGetAsync("TAR-0007", new DefaultHttpContext(), service, CancellationToken.None);
        Assert.Equal(401, Assert.IsAssignableFrom<IStatusCodeHttpResult>(denied).StatusCode);
        var context = AuthenticatedContext();
        var result = await EligibilityPolicyApiEndpoints.HandleGetAsync("TAR-0007", context, service, CancellationToken.None);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"3\"", context.Response.Headers.ETag.ToString());
    }
    [Fact]
    public void Catalog_ContainsExactlyTheEightApprovedTaskRolePairs()
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["TAR-0005"] = "SUBCOORDINACION",
                ["TAR-0007"] = "PISO_VENTAS",
                ["TAR-0008"] = "SUBCOORDINACION",
                ["TAR-0011"] = "SUBCOORDINACION",
                ["TAR-0018"] = "PISO_VENTAS",
                ["TAR-0026"] = "ADMINISTRACION",
                ["TAR-0092"] = "SUBCOORDINACION",
                ["TAR-0093"] = "SUBCOORDINACION",
            },
            EligibilityPolicyCatalog.All);
    }

    [Fact]
    public void Policy_RequiresApprovedRoleAvailabilityAndNullShift()
    {
        var task = TaskDefinitionCatalog.Require("TAR-0005");
        var accepted = new EligibilityPolicyVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            "SUBCOORDINACION", true, null);
        Assert.True(accepted.RequiresAvailability);
        Assert.Null(accepted.RequiredShift);

        Assert.Throws<EligibilityPolicyValidationException>(() => new EligibilityPolicyVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            "Director", true, null));
        Assert.Throws<EligibilityPolicyValidationException>(() => new EligibilityPolicyVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            "DIRECCION", true, null));
        Assert.Throws<EligibilityPolicyValidationException>(() => new EligibilityPolicyVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            "SUBCOORDINACION", false, null));
        Assert.Throws<EligibilityPolicyValidationException>(() => new EligibilityPolicyVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            "SUBCOORDINACION", true, "MATUTINO"));
    }

    [Fact]
    public async Task PutEndpoint_UsesStrictCamelCaseBodyAndForwardsConcurrencyHeaders()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"3\"";
        var releaseId = Guid.CreateVersion7();
        using var body = JsonDocument.Parse(
            $$"""{"releaseId":"{{releaseId:D}}","requiredRole":"SUBCOORDINACION","requiresAvailability":true,"requiredShift":null}""");

        var result = await EligibilityPolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.NotNull(service.Command);
        Assert.Equal(3, service.Command.ExpectedRowVersion);
        Assert.Equal("SUBCOORDINACION", service.Command.RequiredRole);
        Assert.True(service.Command.RequiresAvailability);
        Assert.Null(service.Command.RequiredShift);
    }

    [Fact]
    public async Task PutEndpoint_RejectsMissingOrAdditionalFieldsBeforeCallingService()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var body = JsonDocument.Parse(
            $$"""{"releaseId":"{{Guid.CreateVersion7():D}}","requiredRole":"SUBCOORDINACION","requiresAvailability":true,"requiredShift":null,"position":"Subcoordinador"}""");

        var result = await EligibilityPolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingService : IEligibilityPolicyService
    {
        public Task<EligibilityPolicyHistoryDetails> GetAsync(Guid actorUserId, Guid correlationId,
            string taskCode, CancellationToken cancellationToken = default)
        {
            var current = new EligibilityPolicyVersionDetails(Guid.CreateVersion7(), taskCode, Guid.CreateVersion7(),
                Guid.CreateVersion7(), 1, EligibilityPolicyCatalog.RequireRole(taskCode), true, null,
                "VIGENTE", null, null, null, null, 3);
            return Task.FromResult(new EligibilityPolicyHistoryDetails(taskCode, current, [current]));
        }
        public PutEligibilityPolicyCommand? Command { get; private set; }

        public Task<IReadOnlyList<EligibilityPolicyVersionDetails>> ListAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EligibilityPolicyVersionDetails>>([]);

        public Task<EligibilityPolicyVersionDetails> PutAsync(
            PutEligibilityPolicyCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(new EligibilityPolicyVersionDetails(
                Guid.CreateVersion7(), command.TaskCode, Guid.CreateVersion7(), command.ReleaseId, 1,
                command.RequiredRole, command.RequiresAvailability, command.RequiredShift,
                "BORRADOR", null, null, null, null, 1));
        }
    }
}
