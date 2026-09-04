using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Assignment.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ActiveLoadTests
{
    private static readonly DateTimeOffset CalculatedAt =
        new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AssignmentVersion_RequiresTheApprovedCorrectionMetadata()
    {
        using var explanation = JsonDocument.Parse("{}");
        var automatic = new AssignmentVersion(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            AssignmentVersionStatuses.Current, AssignmentTypes.Automatic,
            explanation, CalculatedAt);

        Assert.Equal(AssignmentTypes.Automatic, automatic.AssignmentType);
        Assert.Throws<ArgumentException>(() => new AssignmentVersion(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            AssignmentVersionStatuses.Current, AssignmentTypes.Correction,
            explanation, CalculatedAt));
    }

    [Fact]
    public async Task Get_ReturnsApprovedEnvelopeAndForwardsServerBoundFilters()
    {
        var personId = Guid.CreateVersion7();
        var reader = new RecordingReader(new ActiveLoadPage(
            [new(new(personId, "EMP-001", "Persona sintética"), 3, CalculatedAt)],
            "next"));
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString($"?limit=1&personId={personId:D}");

        var result = await ActiveLoadApiEndpoints.HandleGetAsync(
            context, reader, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(personId, reader.Request?.PersonId);
        Assert.Equal(1, reader.Request?.Limit);
        var json = JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value,
            JsonSerializerOptions.Web);
        Assert.Contains("\"activeLoad\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"calculatedAt\":\"2026-09-04T18:00:00+00:00\"", json, StringComparison.Ordinal);
        Assert.Contains("\"count\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"nextCursor\":\"next\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?unknown=value")]
    [InlineData("?limit=0")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?personId=not-a-guid")]
    [InlineData("?cursor=")]
    public async Task Get_RejectsManipulatedOrInvalidFiltersWithoutCallingReader(string query)
    {
        var reader = new RecordingReader(new ActiveLoadPage([], null));
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString(query);

        var result = await ActiveLoadApiEndpoints.HandleGetAsync(
            context, reader, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
        Assert.Contains(
            "FILTRO_CARGA_INVALIDO",
            JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RequiresAuthenticationAndMapsMissingPermissionToForbidden()
    {
        var reader = new RecordingReader(new ActiveLoadPage([], null));
        var unauthenticated = await ActiveLoadApiEndpoints.HandleGetAsync(
            new DefaultHttpContext(), reader, CancellationToken.None);
        Assert.Equal(401, Assert.IsAssignableFrom<IStatusCodeHttpResult>(unauthenticated).StatusCode);

        reader.Exception = new ActiveLoadAccessDeniedException();
        var forbidden = await ActiveLoadApiEndpoints.HandleGetAsync(
            AuthenticatedContext(), reader, CancellationToken.None);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(forbidden).StatusCode);
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))],
            "synthetic"));
        return context;
    }

    private sealed class RecordingReader(ActiveLoadPage result) : IActiveLoadReader
    {
        public ActiveLoadRequest? Request { get; private set; }
        public Exception? Exception { get; set; }

        public Task<ActiveLoadPage> ListAsync(
            ActiveLoadRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Exception is null
                ? Task.FromResult(result)
                : Task.FromException<ActiveLoadPage>(Exception);
        }
    }
}
