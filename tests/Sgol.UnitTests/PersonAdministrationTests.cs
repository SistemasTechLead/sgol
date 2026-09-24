using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class PersonAdministrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000101");
    private static readonly Guid PersonId = Guid.Parse("019d2d67-2c00-7000-8000-000000000102");

    [Fact]
    public async Task EmptyList_UsesCollectionEnvelopeWithCountForTheSharedClient()
    {
        var result = await PersonApiEndpoints.HandleListAsync(CreateContext(authenticated: true),
            new RecordingPersonService(CreateDetails()), CancellationToken.None);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        using var document = JsonSerializer.SerializeToDocument(value, JsonOptions);
        Assert.Empty(document.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(0, document.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
    }

    [Fact]
    public void EmploymentCorrection_ClosesPredecessorAndCreatesSuccessor()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var changedAt = startedAt.AddHours(1);
        var active = new EmploymentVersion(
            Guid.CreateVersion7(),
            PersonId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            startedAt,
            positionText: "Piso",
            shiftText: "Matutino");

        var inactive = active.CreateSuccessor(Guid.CreateVersion7(), EmploymentStatus.Inactive, changedAt);

        Assert.Equal(changedAt, active.ValidTo);
        Assert.Equal(2, active.RowVersion);
        Assert.Equal(active.Id, inactive.SupersedesId);
        Assert.Equal(EmploymentStatus.Inactive, inactive.Status);
        Assert.Equal("Piso", inactive.PositionText);
        Assert.Equal("Matutino", inactive.ShiftText);
        Assert.Equal(2, inactive.RowVersion);
        Assert.Throws<InvalidOperationException>(() =>
            active.CreateSuccessor(Guid.CreateVersion7(), EmploymentStatus.Active, changedAt.AddMinutes(1)));
    }

    [Fact]
    public void LaborDataCorrection_VersionsPositionAndShiftWithoutAuthorityData()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var changedAt = startedAt.AddHours(1);
        var active = new EmploymentVersion(
            Guid.CreateVersion7(),
            PersonId,
            BranchScope.LorettaId,
            EmploymentStatus.Active,
            startedAt,
            positionText: "Piso",
            shiftText: "Matutino");

        var corrected = active.CreateSuccessor(
            Guid.CreateVersion7(),
            EmploymentStatus.Active,
            "Director",
            "Vespertino",
            changedAt);

        Assert.Equal(changedAt, active.ValidTo);
        Assert.Equal("Piso", active.PositionText);
        Assert.Equal("Matutino", active.ShiftText);
        Assert.Equal("Director", corrected.PositionText);
        Assert.Equal("Vespertino", corrected.ShiftText);
        Assert.Equal(active.Id, corrected.SupersedesId);
        Assert.Equal(2, corrected.RowVersion);
    }

    [Fact]
    public async Task Create_RequiresAuthenticatedUuidActor()
    {
        var service = new RecordingPersonService(CreateDetails());
        var context = CreateContext(authenticated: false);

        var result = await PersonApiEndpoints.HandleCreateAsync(
            new CreatePersonRequest("PER-001", "Persona sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.CreateCommand);
    }

    [Fact]
    public async Task Create_ForwardsIdempotencyAndReturnsCreatedWithETag()
    {
        var service = new RecordingPersonService(CreateDetails());
        var context = CreateContext(authenticated: true);
        var idempotencyKey = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = idempotencyKey.ToString("D");

        var result = await PersonApiEndpoints.HandleCreateAsync(
            new CreatePersonRequest("PER-001", "Persona sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
        Assert.Equal(ActorUserId, service.CreateCommand?.ActorUserId);
        Assert.Equal(idempotencyKey, service.CreateCommand?.IdempotencyKey);
        Assert.Equal("PER-001", service.CreateCommand?.StableCode);
    }

    [Fact]
    public async Task Deactivate_RequiresETagAndIdempotencyKey()
    {
        var service = new RecordingPersonService(CreateDetails());
        var context = CreateContext(authenticated: true);
        context.Request.Headers.IfMatch = "\"1\"";
        var idempotencyKey = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = idempotencyKey.ToString("D");

        var result = await PersonApiEndpoints.HandleDeactivateAsync(
            PersonId,
            new EmploymentReasonRequest("Baja sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(EmploymentStatus.Inactive, service.ChangeCommand?.Status);
        Assert.Equal(1, service.ChangeCommand?.ExpectedRowVersion);
        Assert.Equal(idempotencyKey, service.ChangeCommand?.IdempotencyKey);
    }

    [Fact]
    public async Task EmploymentChange_MapsStaleETagWithoutExposingImplementationDetails()
    {
        var service = new RecordingPersonService(CreateDetails())
        {
            ChangeException = new PersonVersionConflictException(),
        };
        var context = CreateContext(authenticated: true);
        context.Request.Headers.IfMatch = "\"1\"";
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");

        var result = await PersonApiEndpoints.HandlePatchEmploymentAsync(
            PersonId,
            new ChangeEmploymentRequest(EmploymentStatus.Inactive, "Corrección sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status412PreconditionFailed, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task EmploymentChange_ForwardsPositionAndShiftAsLaborData()
    {
        var service = new RecordingPersonService(CreateDetails());
        var context = CreateContext(authenticated: true);
        context.Request.Headers.IfMatch = "\"1\"";
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");

        var result = await PersonApiEndpoints.HandlePatchEmploymentAsync(
            PersonId,
            new ChangeEmploymentRequest(
                EmploymentStatus.Active,
                "Corrección sintética",
                "Director",
                "Vespertino"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("Director", service.ChangeCommand?.PositionText);
        Assert.Equal("Vespertino", service.ChangeCommand?.ShiftText);
        Assert.Equal(EmploymentStatus.Active, service.ChangeCommand?.Status);
    }

    private static DefaultHttpContext CreateContext(bool authenticated)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = Guid.CreateVersion7().ToString("D"),
        };
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
                "synthetic"));
        }

        return context;
    }

    private static PersonDetails CreateDetails() => new(
        PersonId,
        "PER-001",
        "Persona sintética",
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        [new EmploymentVersionSnapshot(
            Guid.CreateVersion7(),
            EmploymentStatus.Active,
            new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            null,
            null,
            1)]);

    private sealed class RecordingPersonService(PersonDetails person) : IPersonAdministrationService
    {
        public CreatePersonCommand? CreateCommand { get; private set; }

        public ChangeEmploymentCommand? ChangeCommand { get; private set; }

        public Exception? ChangeException { get; init; }

        public Task<IReadOnlyList<PersonSummary>> ListAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PersonSummary>>([]);

        public Task<PersonDetails?> FindAsync(
            Guid actorUserId,
            Guid correlationId,
            Guid personId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonDetails?>(person);

        public Task<PersonMutationResult> CreateAsync(
            CreatePersonCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCommand = command;
            return Task.FromResult(new PersonMutationResult(person, Replayed: false));
        }

        public Task<PersonMutationResult> ChangeEmploymentAsync(
            ChangeEmploymentCommand command,
            CancellationToken cancellationToken = default)
        {
            ChangeCommand = command;
            if (ChangeException is not null)
            {
                return Task.FromException<PersonMutationResult>(ChangeException);
            }

            return Task.FromResult(new PersonMutationResult(person, Replayed: false));
        }
    }
}
