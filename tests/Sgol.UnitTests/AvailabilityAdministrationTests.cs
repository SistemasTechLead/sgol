using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class AvailabilityAdministrationTests
{
    [Theory]
    [InlineData(CanonicalRole.Direction, true)]
    [InlineData(CanonicalRole.Administration, false)]
    [InlineData(CanonicalRole.Subcoordination, false)]
    [InlineData(CanonicalRole.SalesFloor, false)]
    public void SessionProjectsAvailabilityPermissionOnlyForDirection(string role, bool expected) =>
        Assert.Equal(expected, RolePermissionProjection.ForRole(role).Contains(AvailabilityAuthorization.Administer));

    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000301");
    private static readonly Guid PersonId = Guid.Parse("019d2d67-2c00-7000-8000-000000000302");
    private static readonly DateOnly LocalDate = new(2026, 9, 2);

    [Fact]
    public void Correction_PreservesBinaryPredecessorAndCreatesSingleCurrentSuccessor()
    {
        var original = new AvailabilityDayVersion(
            Guid.CreateVersion7(),
            PersonId,
            BranchScope.LorettaId,
            LocalDate,
            isAvailable: true,
            ActorUserId);

        var corrected = original.CreateSuccessor(Guid.CreateVersion7(), isAvailable: false, ActorUserId);

        Assert.Equal(AvailabilityVersionStatus.Historical, original.Status);
        Assert.True(original.IsAvailable);
        Assert.Equal(2, original.RowVersion);
        Assert.Equal(AvailabilityVersionStatus.Current, corrected.Status);
        Assert.False(corrected.IsAvailable);
        Assert.Equal(original.Id, corrected.SupersedesId);
        Assert.Equal(2, corrected.RowVersion);
        Assert.Throws<InvalidOperationException>(() =>
            original.CreateSuccessor(Guid.CreateVersion7(), isAvailable: true, ActorUserId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Put_AcceptsOnlyBooleanAndReturnsVersionETag(bool isAvailable)
    {
        var service = new RecordingAvailabilityService(CreateSnapshot(isAvailable, rowVersion: 1));
        var context = CreateContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");

        var result = await PersonApiEndpoints.HandlePutAvailabilityAsync(
            PersonId,
            "2026-09-02",
            Json($$"""{"isAvailable":{{isAvailable.ToString().ToLowerInvariant()}}}"""),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(isAvailable, service.PutCommand?.IsAvailable);
        Assert.Equal(LocalDate, service.PutCommand?.LocalDate);
        Assert.Null(service.PutCommand?.ExpectedRowVersion);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"isAvailable\":null}")]
    [InlineData("{\"isAvailable\":50}")]
    [InlineData("{\"isAvailable\":0.5}")]
    [InlineData("{\"isAvailable\":\"09:00-17:00\"}")]
    [InlineData("{\"isAvailable\":true,\"percentage\":1}")]
    public async Task Put_RejectsEmptyPartialNumericIntervalAndAdditionalFields(string json)
    {
        var service = new RecordingAvailabilityService(CreateSnapshot(true, rowVersion: 1));
        var context = CreateContext();

        var result = await PersonApiEndpoints.HandlePutAvailabilityAsync(
            PersonId,
            "2026-09-02",
            Json(json),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.PutCommand);
    }

    [Fact]
    public async Task Put_RejectsInvalidDateWithoutCallingService()
    {
        var service = new RecordingAvailabilityService(CreateSnapshot(true, rowVersion: 1));
        var context = CreateContext();

        var result = await PersonApiEndpoints.HandlePutAvailabilityAsync(
            PersonId,
            "2026-02-30",
            Json("""{"isAvailable":true}"""),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.PutCommand);
    }

    [Fact]
    public async Task Put_CorrectionForwardsIfMatchWithoutUtcDateConversion()
    {
        var service = new RecordingAvailabilityService(CreateSnapshot(false, rowVersion: 2));
        var context = CreateContext();
        context.Request.Headers.IfMatch = "\"1\"";
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");

        var result = await PersonApiEndpoints.HandlePutAvailabilityAsync(
            PersonId,
            "2026-09-01",
            Json("""{"isAvailable":false}"""),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new DateOnly(2026, 9, 1), service.PutCommand?.LocalDate);
        Assert.Equal(1, service.PutCommand?.ExpectedRowVersion);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
    }

    [Fact]
    public async Task Get_ForwardsInclusiveRangeAndPreservesAbsenceAsNoValue()
    {
        var service = new RecordingAvailabilityService(CreateSnapshot(true, rowVersion: 1))
        {
            QueryResult = [],
        };
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?fromDate=2026-09-01&toDate=2026-09-03");

        var result = await PersonApiEndpoints.HandleGetAvailabilityAsync(
            PersonId,
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new DateOnly(2026, 9, 1), service.FromDate);
        Assert.Equal(new DateOnly(2026, 9, 3), service.ToDate);
        Assert.Empty(service.QueryResult);
        var body = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        using var envelope = JsonDocument.Parse(JsonSerializer.Serialize(body));
        Assert.Equal(0, envelope.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
    }

    [Fact]
    public void ResponseContract_ExcludesIdentityAuthorityAndSecrets()
    {
        var properties = typeof(AvailabilityDaySnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Id", "IsAvailable", "LocalDate", "PersonId", "RowVersion"], properties.Order());
        Assert.DoesNotContain("Role", properties);
        Assert.DoesNotContain("Permission", properties);
        Assert.DoesNotContain("PositionText", properties);
        Assert.DoesNotContain("ShiftText", properties);
        Assert.DoesNotContain("SecurityStamp", properties);
        Assert.DoesNotContain("PasswordHash", properties);
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static DefaultHttpContext CreateContext() => new()
    {
        TraceIdentifier = Guid.CreateVersion7().ToString("D"),
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
            "synthetic")),
    };

    private static AvailabilityDaySnapshot CreateSnapshot(bool isAvailable, long rowVersion) => new(
        Guid.CreateVersion7(),
        PersonId,
        LocalDate,
        isAvailable,
        rowVersion);

    private sealed class RecordingAvailabilityService(AvailabilityDaySnapshot putResult)
        : IAvailabilityAdministrationService
    {
        public PutAvailabilityCommand? PutCommand { get; private set; }

        public DateOnly? FromDate { get; private set; }

        public DateOnly? ToDate { get; private set; }

        public IReadOnlyList<AvailabilityDaySnapshot> QueryResult { get; init; } = [putResult];

        public Task<IReadOnlyList<AvailabilityDaySnapshot>> GetAsync(
            Guid actorUserId,
            Guid correlationId,
            Guid personId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            FromDate = fromDate;
            ToDate = toDate;
            return Task.FromResult(QueryResult);
        }

        public Task<AvailabilityDaySnapshot> PutAsync(
            PutAvailabilityCommand command,
            CancellationToken cancellationToken = default)
        {
            PutCommand = command;
            return Task.FromResult(putResult);
        }
    }
}
