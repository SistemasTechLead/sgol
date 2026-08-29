using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sgol.Web.Infrastructure.Http;
using Xunit;

namespace Sgol.UnitTests;

public sealed class HttpPrimitiveTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpPrimitiveTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
    }

    [Fact]
    public async Task ValidUuid7CorrelationId_IsPropagatedInHeaderAndBody()
    {
        using var client = _factory.CreateClient();
        var expected = Guid.CreateVersion7(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, expected.ToString("D"));

        using var response = await client.SendAsync(request);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            expected.ToString("D"),
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        Assert.Equal(
            expected.ToString("D"),
            payload.RootElement.GetProperty("meta").GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvalidCorrelationId_IsReplacedByUuid7()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "caller-controlled-value");

        using var response = await client.SendAsync(request);
        var actual = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));

        Assert.True(Guid.TryParse(actual, out var parsed));
        Assert.Equal(7, parsed.Version);
        Assert.NotEqual("caller-controlled-value", actual);
    }

    [Fact]
    public async Task MissingRoute_ReturnsProblemDetailsWithCorrelationId()
    {
        using var client = _factory.CreateClient();
        var expected = Guid.CreateVersion7();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/not-found");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, expected.ToString("D"));

        using var response = await client.SendAsync(request);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, payload.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/not-found", payload.RootElement.GetProperty("instance").GetString());
        Assert.Equal(
            expected.ToString("D"),
            payload.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task UnhandledError_DoesNotExposeSecretInProblemDetailsOrLogs()
    {
        const string sensitiveValue = "private-input-marker";
        var entries = new ConcurrentQueue<string>();
        using var provider = new CapturingLoggerProvider(entries);
        using var host = await Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(provider);
            })
            .ConfigureWebHost(webHost =>
            {
                webHost.UseEnvironment("Production");
                webHost.UseTestServer();
                webHost.ConfigureServices(services => services.AddSgolHttpPrimitives());
                webHost.Configure(app =>
                {
                    app.UseSgolHttpPrimitives();
                    app.Run(_ => throw new InvalidOperationException($"Failure containing {sensitiveValue}"));
                });
            })
            .StartAsync();

        using var response = await host.GetTestClient().GetAsync($"/failure?credential={sensitiveValue}");
        var body = await response.Content.ReadAsStringAsync();
        using var payload = JsonDocument.Parse(body);
        var correlationId = payload.RootElement.GetProperty("correlationId").GetString();
        var logs = string.Join(Environment.NewLine, entries);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(body.Contains(sensitiveValue, StringComparison.Ordinal));
        Assert.False(logs.Contains(sensitiveValue, StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Contains(correlationId, logs, StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var rendered = formatter(state, exception);
            if (exception is not null)
            {
                rendered += Environment.NewLine + exception;
            }

            entries.Enqueue(rendered);
        }
    }
}
