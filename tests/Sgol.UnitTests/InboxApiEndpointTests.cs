using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Notifications.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class InboxApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetUsesApprovedDefaultsAndReturnsIndependentEmptySections()
    {
        var actor = Guid.CreateVersion7();
        var queriedAt = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
        var context = Context(actor, HttpMethods.Get, "/api/v1/me/inbox");
        var reader = new RecordingReader(new(null, new([], null), new([], null), queriedAt));

        var result = await InboxApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl);
        Assert.Equal(new InboxQuery(actor, null, null, null, 25, "ALL", null, 25), reader.Query);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.Contains("\"tasks\":{\"items\":[],\"nextCursor\":null,\"count\":0,\"isEmpty\":true}", json, StringComparison.Ordinal);
        Assert.Contains("\"notices\":{\"items\":[],\"nextCursor\":null,\"count\":0,\"isEmpty\":true}", json, StringComparison.Ordinal);
        Assert.Contains("\"isEmpty\":true", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?unknown=1")]
    [InlineData("?taskLimit=0")]
    [InlineData("?taskLimit=101")]
    [InlineData("?taskState=PROGRAMADA")]
    [InlineData("?noticeStatus=UNKNOWN")]
    [InlineData("?taskLimit=10&taskLimit=20")]
    public async Task InvalidGetFiltersAreRejectedBeforeReader(string queryString)
    {
        var context = Context(Guid.CreateVersion7(), HttpMethods.Get, "/api/v1/me/inbox");
        context.Request.QueryString = new QueryString(queryString);
        var reader = new RecordingReader(null);

        var result = await InboxApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Query);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    [Fact]
    public async Task UnauthenticatedGetIsRejectedBeforeReader()
    {
        var context = Context(null, HttpMethods.Get, "/api/v1/me/inbox");
        var reader = new RecordingReader(null);

        var result = await InboxApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Query);
    }

    [Theory]
    [InlineData("Idempotency-Key")]
    [InlineData("If-Match")]
    public async Task GetRejectsUnsupportedFunctionalHeaders(string header)
    {
        var context = Context(Guid.CreateVersion7(), HttpMethods.Get, "/api/v1/me/inbox");
        context.Request.Headers[header] = "unsupported";
        var reader = new RecordingReader(null);

        var result = await InboxApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Query);
    }

    [Fact]
    public async Task MarkReadReturnsApprovedProjectionWithoutIdempotencyOrEtag()
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var notice = Guid.CreateVersion7();
        var readAt = new DateTimeOffset(2026, 9, 9, 18, 5, 0, TimeSpan.Zero);
        var context = Context(actor, HttpMethods.Post, $"/api/v1/me/notices/{notice:D}/read", services);
        AddValidCsrf(services, context, actor, notice);
        var service = new RecordingNoticeService(new(notice, InternalNoticeStatuses.Read, readAt, InternalNoticeReadResults.MarkedRead));

        var result = await InboxApiEndpoints.MarkReadAsync(context, notice.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(actor, service.Command!.ActorUserId);
        Assert.Equal(notice, service.Command.NoticeId);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.Contains("\"result\":\"MARKED_READ\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("etag", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("body")]
    [InlineData("query")]
    [InlineData("idempotency")]
    [InlineData("if-match")]
    public async Task UnsupportedMarkReadTransportIsRejectedBeforeCsrfAndBusiness(string defect)
    {
        using var services = Services();
        var notice = Guid.CreateVersion7();
        var context = Context(Guid.CreateVersion7(), HttpMethods.Post, $"/api/v1/me/notices/{notice:D}/read", services);
        switch (defect)
        {
            case "body": context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}")); context.Request.ContentLength = 2; break;
            case "query": context.Request.QueryString = new QueryString("?x=1"); break;
            case "idempotency": context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D"); break;
            case "if-match": context.Request.Headers.IfMatch = "\"1\""; break;
        }
        var service = new RecordingNoticeService(null);

        var result = await InboxApiEndpoints.MarkReadAsync(context, notice.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    [Fact]
    public async Task MissingCsrfRejectsBeforeBusiness()
    {
        using var services = Services();
        var notice = Guid.CreateVersion7();
        var context = Context(Guid.CreateVersion7(), HttpMethods.Post, $"/api/v1/me/notices/{notice:D}/read", services);
        var service = new RecordingNoticeService(null);

        var result = await InboxApiEndpoints.MarkReadAsync(context, notice.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    private static DefaultHttpContext Context(Guid? actor, string method, string path, IServiceProvider? services = null)
    {
        var context = new DefaultHttpContext { RequestServices = services ?? new ServiceCollection().BuildServiceProvider() };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Body = new MemoryStream();
        if (actor.HasValue)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString("D"))], "test"));
        return context;
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        return services.BuildServiceProvider();
    }

    private static void AddValidCsrf(IServiceProvider services, DefaultHttpContext context, Guid actor, Guid notice)
    {
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var issuance = Context(actor, HttpMethods.Post, $"/api/v1/me/notices/{notice:D}/read", services);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
    }

    private sealed class RecordingReader(InboxPage? page) : IInboxReader
    {
        public InboxQuery? Query { get; private set; }
        public Task<InboxPage> ReadAsync(InboxQuery query, CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(page!);
        }
    }

    private sealed class RecordingNoticeService(ReadInternalNoticeResult? result) : IInternalNoticeService
    {
        public ReadInternalNoticeCommand? Command { get; private set; }
        public Task<ReadInternalNoticeResult> MarkReadAsync(ReadInternalNoticeCommand command, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(result!);
        }
    }
}
