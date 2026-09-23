using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Sgol.Web.Presentation.ApiClient;
using Xunit;

namespace Sgol.UnitTests;

public sealed class SgolApiClientTests
{
    private static readonly string Correlation = Guid.CreateVersion7().ToString("D");

    [Fact]
    public async Task SingleEnvelope_PreservesSessionCookieAndCorrelation()
    {
        string? observedCookie = null;
        Uri? observedUri = null;
        using var handler = new StubHandler(request =>
        {
            observedCookie = request.Headers.GetValues("Cookie").Single();
            observedUri = request.RequestUri;
            return Json(HttpStatusCode.OK, $"{{\"data\":{{\"id\":1}},\"meta\":{{\"correlationId\":\"{Correlation}\"}}}}");
        });
        var client = CreateClient(handler);

        var result = await client.SendAsync<Item>(new(HttpMethod.Get, "/api/v1/branches/LOR-001", ApiResponseShape.Item));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Id);
        Assert.Equal(Correlation, result.CorrelationId);
        Assert.Equal("__Host-SGOL-Session=opaque", observedCookie);
        Assert.Equal("https://sgol.example/api/v1/branches/LOR-001", observedUri!.ToString());
        Assert.DoesNotContain("opaque", result.ToString());
        Assert.DoesNotContain("opaque", new ApiRequest(HttpMethod.Get, "/api/v1/loads?token=opaque", ApiResponseShape.Item).ToString());
    }

    [Fact]
    public async Task Collection_PreservesCursorCountAndCorrelation()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"data\":[{{\"id\":1}},{{\"id\":2}}],\"meta\":{{\"nextCursor\":\"opaque-next\",\"count\":2,\"correlationId\":\"{Correlation}\"}}}}"));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Collection));
        Assert.Equal([1, 2], result.Items!.Select(item => item.Id));
        Assert.Equal("opaque-next", result.NextCursor);
        Assert.Equal(2, result.Count);
        Assert.Equal(Correlation, result.CorrelationId);
    }

    [Fact]
    public async Task ETagIfMatchAndIntent_AreExplicitAndStable_AndReplayIsNotNewSuccess()
    {
        var requests = new List<(string? IfMatch, string? Key, string? Csrf)>();
        using var handler = new StubHandler(request =>
        {
            requests.Add((request.Headers.TryGetValues("If-Match", out var values) ? values.Single() : null,
                request.Headers.TryGetValues("Idempotency-Key", out values) ? values.Single() : null,
                request.Headers.TryGetValues("X-CSRF-TOKEN", out values) ? values.Single() : null));
            var response = Json(HttpStatusCode.OK,
                $"{{\"data\":{{\"id\":7,\"result\":\"{(requests.Count == 1 ? "ACEPTADA" : "RECUPERADA")}\"}},\"meta\":{{\"correlationId\":\"{Correlation}\"}}}}");
            response.Headers.ETag = new("\"4\"");
            return response;
        });
        var client = CreateClient(handler);
        var intent = ApiMutationIntent.New();
        var request = new ApiRequest(HttpMethod.Post, "/api/v1/obligations/1/assignment-corrections",
            ApiResponseShape.Item, new { reason = "synthetic" }, "\"3\"", intent, "csrf-synthetic");

        var first = await client.SendAsync<Item>(request);
        var replay = await client.SendAsync<Item>(request);

        Assert.Equal("\"4\"", first.ETag);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Data!.Id, replay.Data!.Id);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, captured =>
        {
            Assert.Equal("\"3\"", captured.IfMatch);
            Assert.Equal(intent.Key.ToString("D"), captured.Key);
            Assert.Equal("csrf-synthetic", captured.Csrf);
        });
    }

    [Fact]
    public async Task SameIntentWithDifferentBody_FailsBeforeSecondMutation()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"data\":{{\"id\":1}},\"meta\":{{\"correlationId\":\"{Correlation}\"}}}}"));
        var client = CreateClient(handler);
        var intent = ApiMutationIntent.New();
        await client.SendAsync<Item>(new(HttpMethod.Post, "/api/v1/people", ApiResponseShape.Item,
            new { name = "one" }, Intent: intent, CsrfToken: "synthetic"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync<Item>(new(HttpMethod.Post,
            "/api/v1/people", ApiResponseShape.Item, new { name = "two" }, Intent: intent, CsrfToken: "synthetic")));
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData(400, "SOLICITUD_INVALIDA")]
    [InlineData(401, "AUTENTICACION_REQUERIDA")]
    [InlineData(403, "ACCESO_DENEGADO")]
    [InlineData(404, "OBLIGACION_NO_ENCONTRADA")]
    [InlineData(409, "IDEMPOTENCY_CONFLICT")]
    [InlineData(412, "VERSION_CONFLICT")]
    [InlineData(413, "FILE_TOO_LARGE")]
    [InlineData(415, "FILE_TYPE_NOT_ALLOWED")]
    [InlineData(422, "EVIDENCIA_FALTANTE")]
    [InlineData(423, "ACCOUNT_LOCKED")]
    [InlineData(428, "IF_MATCH_REQUERIDO")]
    [InlineData(429, "RATE_LIMITED")]
    [InlineData(500, "RECONCILIACION_FALLO")]
    [InlineData(503, "DEPENDENCY_UNAVAILABLE")]
    public async Task ContractedErrorStatuses_RemainErrorsWithSafePresentation(int status, string code)
    {
        using var handler = new StubHandler(_ => Problem(status, code));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.False(result.IsSuccess);
        Assert.Equal(status, result.Status);
        Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData("CSRF_INVALID")]
    [InlineData("CSRF_INVALIDO")]
    public async Task CsrfAliases_UseOneApprovedMessage(string code)
    {
        using var handler = new StubHandler(_ => Problem(400, code));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.Equal("No se pudo verificar la solicitud", result.Error!.Title);
        Assert.Equal("Recarga la página antes de volver a enviarla.", result.Error.Message);
    }

    [Theory]
    [InlineData(400, "IF_MATCH_REQUERIDO", "Recarga el registro antes de guardar los cambios.")]
    [InlineData(400, "IF_MATCH_INVALIDO", "Recárgalo antes de guardar.")]
    [InlineData(428, "IF_MATCH_REQUERIDO", "Recarga la reconciliación antes de aprobarla.")]
    [InlineData(412, "VERSION_CONFLICT", "Probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar, o tus cambios podrían sobrescribir los de la otra persona.")]
    public async Task IfMatchMatrix_UsesStatusAndCode(int status, string code, string message)
    {
        using var handler = new StubHandler(_ => Problem(status, code));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.Equal(message, result.Error!.Message);
    }

    [Fact]
    public async Task StaleVersion_DoesNotRetryOrTriggerSecondMutation()
    {
        using var handler = new StubHandler(_ => Problem(412, "VERSION_CONFLICT"));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Post, "/api/v1/obligations/1/conclusion",
            ApiResponseShape.Item, IfMatch: "\"3\"", Intent: ApiMutationIntent.New(), CsrfToken: "synthetic"));
        Assert.False(result.IsSuccess);
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    public async Task InvalidBody_FailsClosed(string body)
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));
        await Assert.ThrowsAsync<ApiProtocolException>(() => CreateClient(handler).SendAsync<Item>(
            new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item)));
    }

    [Fact]
    public async Task WrongContentTypeOrMissingCorrelation_FailsClosed()
    {
        using var wrongType = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>error</html>", Encoding.UTF8, "text/html")
        });
        await Assert.ThrowsAsync<ApiProtocolException>(() => CreateClient(wrongType).SendAsync<Item>(
            new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item)));
        using var missingCorrelation = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"data\":{\"id\":1},\"meta\":{}}"));
        await Assert.ThrowsAsync<ApiProtocolException>(() => CreateClient(missingCorrelation).SendAsync<Item>(
            new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item)));
        using var invalidCorrelation = new StubHandler(_ => Json(HttpStatusCode.OK,
            "{\"data\":{\"id\":1},\"meta\":{\"correlationId\":\"not-a-guid\"}}"));
        await Assert.ThrowsAsync<ApiProtocolException>(() => CreateClient(invalidCorrelation).SendAsync<Item>(
            new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item)));
    }

    [Fact]
    public async Task NoContent_UsesHeaderCorrelation_AndMetaReplayIsRecognized()
    {
        using var emptyHandler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            response.Headers.TryAddWithoutValidation("X-Correlation-ID", Correlation);
            return response;
        });
        var empty = await CreateClient(emptyHandler).SendAsync<Item>(new(HttpMethod.Post, "/api/v1/auth/logout",
            ApiResponseShape.NoContent, CsrfToken: "synthetic"));
        Assert.True(empty.IsSuccess);
        Assert.Equal(Correlation, empty.CorrelationId);
        using var replayHandler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"data\":{{\"id\":7}},\"meta\":{{\"replayed\":true,\"correlationId\":\"{Correlation}\"}}}}"));
        var replay = await CreateClient(replayHandler).SendAsync<Item>(new(HttpMethod.Get,
            "/api/v1/continuity/reconciliations/1", ApiResponseShape.Item));
        Assert.True(replay.Replayed);
    }

    [Fact]
    public async Task UnknownCodeAndInternalDetails_NeverReachPresentation()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError,
            $"{{\"status\":500,\"code\":\"UNKNOWN_FAILURE\",\"title\":\"stack SQL password cookie\",\"detail\":\"C:\\\\private\\\\evidence\",\"correlationId\":\"{Correlation}\"}}",
            "application/problem+json"));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.Equal("No se pudo completar la operación", result.Error!.Title);
        Assert.DoesNotContain("stack", result.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", result.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", result.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generic500WithoutCode_UsesSafeMessage()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError,
            $"{{\"status\":500,\"title\":\"SQL stack\",\"correlationId\":\"{Correlation}\"}}",
            "application/problem+json"));
        var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.False(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.DoesNotContain("SQL", result.Error!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidProblemBodies_FailClosed()
    {
        foreach (var body in new[] { "", "{", $"{{\"status\":403,\"code\":\"ACCESO_DENEGADO\",\"correlationId\":\"invalid\"}}" })
        {
            using var handler = new StubHandler(_ => Json(HttpStatusCode.Forbidden, body, "application/problem+json"));
            await Assert.ThrowsAsync<ApiProtocolException>(() => CreateClient(handler).SendAsync<Item>(
                new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item)));
        }
    }

    [Fact]
    public async Task UnauthorizedForbiddenAndHiddenResource_HaveDistinctSafeTitles()
    {
        var titles = new List<string>();
        foreach (var (status, code) in new[] { (401, "AUTENTICACION_REQUERIDA"), (403, "ACCESO_DENEGADO"), (404, "OBLIGACION_NO_ENCONTRADA") })
        {
            using var handler = new StubHandler(_ => Problem(status, code));
            var result = await CreateClient(handler).SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
            Assert.False(result.IsSuccess);
            titles.Add(result.Error!.Title);
        }
        Assert.Equal(3, titles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task PathCannotLeaveApiOrigin_AndErrorResponseIsDisposed()
    {
        HttpResponseMessage? response = null;
        using var handler = new StubHandler(_ => response = Problem(403, "ACCESO_DENEGADO"));
        var client = CreateClient(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync<Item>(new(HttpMethod.Get,
            "https://elsewhere.example/api/v1/loads", ApiResponseShape.Item)));
        Assert.Equal(0, handler.Count);
        await client.SendAsync<Item>(new(HttpMethod.Get, "/api/v1/loads", ApiResponseShape.Item));
        Assert.NotNull(response);
        Assert.Throws<ObjectDisposedException>(() => response!.Content.ReadAsStringAsync().GetAwaiter().GetResult());
    }

    private static SgolApiClient CreateClient(StubHandler handler)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("sgol.example");
        context.Request.Headers.Cookie = "__Host-SGOL-Session=opaque; unrelated=private";
        return new(new HttpClient(handler), new HttpContextAccessor { HttpContext = context });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json")
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        response.Headers.TryAddWithoutValidation("X-Correlation-ID", Correlation);
        return response;
    }

    private static HttpResponseMessage Problem(int status, string code) => Json((HttpStatusCode)status,
        $"{{\"status\":{status},\"code\":\"{code}\",\"correlationId\":\"{Correlation}\"}}", "application/problem+json");

    private sealed record Item(int Id, string? Result = null);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return Task.FromResult(responder(request));
        }
    }
}
