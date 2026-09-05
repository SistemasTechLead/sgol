using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ObligationQueryTests
{
    private static readonly DateTimeOffset QueriedAt =
        new(2026, 9, 5, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_ReturnsApprovedEnvelopeAndForwardsAllServerBoundFilters()
    {
        var obligation = Item();
        var reader = new RecordingReader
        {
            ListResult = new ObligationPage([obligation], "next", QueriedAt),
        };
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString(
            $"?periodId={obligation.Period.PeriodId:D}&taskCode=TAR-0005&executionStatus=PENDIENTE" +
            $"&condition=VENCIDA&responsiblePersonId={obligation.CurrentAssignment!.Responsible.PersonId:D}" +
            "&cursor=opaque&limit=1");

        var result = await ObligationQueryApiEndpoints.HandleListAsync(
            context, reader, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, reader.ListRequest?.Limit);
        Assert.Equal(ObligationConditions.Overdue, reader.ListRequest?.Condition);
        Assert.Equal(obligation.Period.PeriodId, reader.ListRequest?.PeriodId);
        var json = JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value,
            JsonSerializerOptions.Web);
        Assert.Contains("\"nextCursor\":\"next\"", json, StringComparison.Ordinal);
        Assert.Contains("\"count\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"queriedAt\":\"2026-09-05T18:00:00+00:00\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("taskPayload", json, StringComparison.Ordinal);
        Assert.DoesNotContain("inputPayload", json, StringComparison.Ordinal);
        Assert.DoesNotContain("explanation", json, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("?unknown=value")]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=1&limit=2")]
    [InlineData("?periodId=not-a-guid")]
    [InlineData("?periodId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?taskCode=TAR-9999")]
    [InlineData("?executionStatus=pendiente")]
    [InlineData("?condition=ATRASADA")]
    [InlineData("?responsiblePersonId=")]
    [InlineData("?cursor=")]
    public async Task List_RejectsUnknownDuplicateOrInvalidFiltersWithoutCallingReader(string query)
    {
        var reader = new RecordingReader();
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString(query);

        var result = await ObligationQueryApiEndpoints.HandleListAsync(
            context, reader, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.ListRequest);
        AssertProblem(result, "FILTRO_OBLIGACIONES_INVALIDO");
    }

    [Fact]
    public async Task List_RequiresSessionAndMapsPermissionFailureWithoutCallingOrLeakingData()
    {
        var reader = new RecordingReader();
        var unauthenticated = await ObligationQueryApiEndpoints.HandleListAsync(
            new DefaultHttpContext(), reader, CancellationToken.None);
        Assert.Equal(401, Assert.IsAssignableFrom<IStatusCodeHttpResult>(unauthenticated).StatusCode);
        Assert.Null(reader.ListRequest);

        reader.Exception = new ObligationQueryAccessDeniedException();
        var forbidden = await ObligationQueryApiEndpoints.HandleListAsync(
            AuthenticatedContext(), reader, CancellationToken.None);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(forbidden).StatusCode);
        AssertProblem(forbidden, "ACCESO_DENEGADO");
    }

    [Fact]
    public async Task ListAndDetail_UseDefaultTwentyFiveAndAcceptMaximumOneHundred()
    {
        var item = Item();
        var detail = new ObligationDetail(
            item.ObligationId,
            item.Task,
            item.Origin,
            item.Period,
            item.Dates,
            item.ExecutionStatus,
            item.Condition,
            item.CurrentAssignment,
            item.Links,
            new ObligationGenerationRequest(
                item.Origin.GenerationRequestId, "ACEPTADA", item.Origin.RequestedAt, null),
            []);
        var reader = new RecordingReader
        {
            DetailResult = new ObligationDetailPage(detail, null, QueriedAt),
        };

        _ = await ObligationQueryApiEndpoints.HandleListAsync(
            AuthenticatedContext(), reader, CancellationToken.None);
        Assert.Equal(25, reader.ListRequest?.Limit);

        var listMaximum = AuthenticatedContext();
        listMaximum.Request.QueryString = new QueryString("?limit=100");
        _ = await ObligationQueryApiEndpoints.HandleListAsync(
            listMaximum, reader, CancellationToken.None);
        Assert.Equal(100, reader.ListRequest?.Limit);

        _ = await ObligationQueryApiEndpoints.HandleDetailAsync(
            AuthenticatedContext(), item.ObligationId.ToString("D"), reader, CancellationToken.None);
        Assert.Equal(25, reader.DetailRequest?.HistoryLimit);

        var historyMaximum = AuthenticatedContext();
        historyMaximum.Request.QueryString = new QueryString("?historyLimit=100");
        _ = await ObligationQueryApiEndpoints.HandleDetailAsync(
            historyMaximum,
            item.ObligationId.ToString("D"),
            reader,
            CancellationToken.None);
        Assert.Equal(100, reader.DetailRequest?.HistoryLimit);
    }

    [Fact]
    public async Task Detail_ReturnsExactHistoryMetadataAndSanitizedContract()
    {
        var item = Item();
        var history = new ObligationHistoryEvent(
            item.Origin.GenerationRequestId,
            ObligationHistoryEventTypes.GenerationRequested,
            item.Origin.RequestedAt,
            ObligationHistoryActorTypes.Human,
            Guid.CreateVersion7(),
            null,
            null,
            null);
        var detail = new ObligationDetail(
            item.ObligationId,
            item.Task,
            item.Origin,
            item.Period,
            item.Dates,
            item.ExecutionStatus,
            item.Condition,
            item.CurrentAssignment,
            item.Links,
            new ObligationGenerationRequest(
                item.Origin.GenerationRequestId,
                "ACEPTADA",
                item.Origin.RequestedAt,
                history.ActorUserId),
            [history]);
        var reader = new RecordingReader
        {
            DetailResult = new ObligationDetailPage(detail, "history-next", QueriedAt),
        };
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString("?historyLimit=1");

        var result = await ObligationQueryApiEndpoints.HandleDetailAsync(
            context,
            item.ObligationId.ToString("D"),
            reader,
            CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, reader.DetailRequest?.HistoryLimit);
        var json = JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value,
            JsonSerializerOptions.Web);
        Assert.Contains("\"historyNextCursor\":\"history-next\"", json, StringComparison.Ordinal);
        Assert.Contains("\"historyCount\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"generationRequest\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("audit", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-guid", "", "OBLIGACION_ID_INVALIDO")]
    [InlineData("019d9300-0000-7000-8000-000000000001", "?unknown=value", "FILTRO_HISTORIA_INVALIDO")]
    [InlineData("019d9300-0000-7000-8000-000000000001", "?historyLimit=0", "FILTRO_HISTORIA_INVALIDO")]
    [InlineData("019D9300-0000-7000-8000-000000000001", "", "OBLIGACION_ID_INVALIDO")]
    public async Task Detail_RejectsInvalidRouteAndHistoryParameters(
        string id,
        string query,
        string code)
    {
        var reader = new RecordingReader();
        var context = AuthenticatedContext();
        context.Request.QueryString = new QueryString(query);

        var result = await ObligationQueryApiEndpoints.HandleDetailAsync(
            context, id, reader, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.DetailRequest);
        AssertProblem(result, code);
    }

    [Fact]
    public async Task Detail_MapsMissingAndOutOfScopeResourcesToTheSameNotFoundContract()
    {
        var reader = new RecordingReader { Exception = new ObligationQueryNotFoundException() };
        var result = await ObligationQueryApiEndpoints.HandleDetailAsync(
            AuthenticatedContext(),
            Guid.CreateVersion7().ToString("D"),
            reader,
            CancellationToken.None);

        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        AssertProblem(result, "OBLIGACION_NO_ENCONTRADA");
    }

    private static ObligationListItem Item()
    {
        var obligationId = Guid.CreateVersion7();
        var responsibleId = Guid.CreateVersion7();
        return new ObligationListItem(
            obligationId,
            new ObligationTask(
                Guid.CreateVersion7(),
                "TAR-0005",
                "Tarea sintética",
                new ObligationTaskVersion(
                    Guid.CreateVersion7(), 2, "VIGENTE", QueriedAt.AddDays(-1), null, 1)),
            new ObligationOrigin(
                ObligationOriginKinds.Recurring,
                "WORKING_DAY_WINDOW_V1",
                "LOR-001|2026-09-05|12:00",
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                QueriedAt.AddHours(-6)),
            new ObligationPeriod(
                Guid.CreateVersion7(), 2026, 36,
                new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 6),
                "America/Mexico_City"),
            new ObligationDates(
                QueriedAt.AddMinutes(-1), new DateOnly(2026, 9, 5), null),
            "PENDIENTE",
            ObligationConditions.Overdue,
            new ObligationAssignmentSummary(
                Guid.CreateVersion7(),
                new ObligationPerson(responsibleId, "SYN-001", "Persona sintética"),
                "AUTOMATICA",
                QueriedAt.AddHours(-5)),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["self"] = $"/api/v1/obligations/{obligationId:D}",
            });
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))],
            "synthetic"));
        return context;
    }

    private static void AssertProblem(IResult result, string code)
    {
        Assert.Equal(
            "application/problem+json",
            Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Contains(code, json, StringComparison.Ordinal);
        Assert.Contains("correlationId", json, StringComparison.Ordinal);
    }

    private sealed class RecordingReader : IObligationQueryReader
    {
        public ObligationPage ListResult { get; init; } = new([], null, QueriedAt);
        public ObligationDetailPage? DetailResult { get; init; }
        public ObligationListRequest? ListRequest { get; private set; }
        public ObligationDetailRequest? DetailRequest { get; private set; }
        public Exception? Exception { get; set; }

        public Task<ObligationPage> ListAsync(
            ObligationListRequest request,
            CancellationToken cancellationToken = default)
        {
            ListRequest = request;
            return Exception is null
                ? Task.FromResult(ListResult)
                : Task.FromException<ObligationPage>(Exception);
        }

        public Task<ObligationDetailPage> GetAsync(
            ObligationDetailRequest request,
            CancellationToken cancellationToken = default)
        {
            DetailRequest = request;
            return Exception is null
                ? Task.FromResult(DetailResult ?? throw new InvalidOperationException())
                : Task.FromException<ObligationDetailPage>(Exception);
        }
    }
}
