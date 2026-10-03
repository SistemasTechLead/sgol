using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidenceDownloadTests
{
    [Theory]
    [InlineData("invalid", null)]
    [InlineData("00000000-0000-0000-0000-000000000000", null)]
    [InlineData("valid", "query")]
    [InlineData("valid", "body")]
    [InlineData("valid", "chunked")]
    [InlineData("valid", "Idempotency-Key")]
    [InlineData("valid", "If-Match")]
    public async Task ClosedSyntaxReauthorizesActorButNeverResolvesFile(string id, string? invalid)
    {
        using var services = Services();
        var context = Context(services);
        var service = new RecordingDownloadService();
        if (invalid == "query") context.Request.QueryString = new("?ttl=1");
        else if (invalid == "body") context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        else if (invalid == "chunked") context.Request.Headers.TransferEncoding = "chunked";
        else if (invalid is not null) context.Request.Headers[invalid] = "synthetic";
        var result = await EvidenceApiEndpoints.DownloadAsync(context,
            id == "valid" ? Guid.CreateVersion7().ToString("D") : id, service, CancellationToken.None);
        await result.ExecuteAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal(1, service.ActorChecks);
        Assert.Equal(0, service.Authorizations);
        AssertHeaders(context);
        using var json = JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal("SOLICITUD_DESCARGA_INVALIDA", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("application/problem+json", context.Response.ContentType!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(403, "ACCESO_DENEGADO")]
    [InlineData(404, "ARCHIVO_NO_ENCONTRADO")]
    [InlineData(422, "ARCHIVO_NO_LIMPIO")]
    [InlineData(500, "CADENA_EVIDENCIA_INCONSISTENTE")]
    [InlineData(503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE")]
    public async Task ClosedProblemsNeverReturnPartialAuthorization(int status, string code)
    {
        using var services = Services();
        var context = Context(services);
        var service = new RecordingDownloadService { Failure = new EvidenceDownloadException(status, code) };
        var result = await EvidenceApiEndpoints.DownloadAsync(context, Guid.CreateVersion7().ToString("D"), service, CancellationToken.None);
        await result.ExecuteAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        AssertHeaders(context);
        using var json = JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("correlationId", out _));
        Assert.False(json.RootElement.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task SuccessIsSingularClosedNoCacheEvenWithConditionalHeaders()
    {
        using var services = Services();
        var context = Context(services);
        context.Request.Headers.IfNoneMatch = "\"synthetic\"";
        var service = new RecordingDownloadService();
        var result = await EvidenceApiEndpoints.DownloadAsync(context, Guid.CreateVersion7().ToString("D"), service, CancellationToken.None);
        await result.ExecuteAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
        AssertHeaders(context);
        Assert.False(context.Response.Headers.ContainsKey("ETag"));
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        using var json = JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal(["data", "meta"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["fileId", "download"], json.RootElement.GetProperty("data").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["url", "expiresAt"], json.RootElement.GetProperty("data").GetProperty("download").EnumerateObject().Select(p => p.Name));
        Assert.EndsWith("Z", json.RootElement.GetProperty("data").GetProperty("download").GetProperty("expiresAt").GetString());
        Assert.Equal(["correlationId"], json.RootElement.GetProperty("meta").EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, service.ActorChecks);
        Assert.Equal(1, service.Authorizations);
    }

    [Fact]
    public async Task MissingSessionIs401BeforeAnyServiceAndKeepsNoStore()
    {
        using var services = Services();
        var context = Context(services);
        context.User = new ClaimsPrincipal();
        var service = new RecordingDownloadService();
        var result = await EvidenceApiEndpoints.DownloadAsync(context, "invalid", service, CancellationToken.None);
        await result.ExecuteAsync(context);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal(0, service.ActorChecks);
        AssertHeaders(context);
    }

    [Theory]
    [InlineData(EvidenceMediaType.Jpeg, "jpg")]
    [InlineData(EvidenceMediaType.Png, "png")]
    [InlineData(EvidenceMediaType.Pdf, "pdf")]
    public async Task SignerUsesGetCleanExactHeadersAndExpiry(EvidenceMediaType type, string extension)
    {
        using var client = new AmazonS3Client(new BasicAWSCredentials("synthetic-key", "synthetic-secret"), new AmazonS3Config
        { ServiceURL = "https://storage.example.test", AuthenticationRegion = "us-east-1", ForcePathStyle = true });
        var storage = new S3PrivateObjectStorage(client, Options.Create(new EvidenceStorageOptions
        { Endpoint = "https://storage.example.test", CleanBucket = "clean", QuarantineBucket = "quarantine" }));
        var metadata = new EvidenceObjectMetadata(EvidenceObjectKey.Parse("v1/aa/aa/" + new string('a', 64)), 128, new string('b', 64), type);
        var id = Guid.CreateVersion7();
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddSeconds(60);
        var result = await storage.CreateCleanDownloadAuthorizationAsync(metadata, id, expiresAt, CancellationToken.None);
        Assert.Equal(expiresAt, result.ExpiresAt);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(result.Url.Query);
        Assert.Equal(type.ToMediaType(), query["response-content-type"].ToString());
        Assert.Equal($"attachment; filename=\"evidence-{id:D}.{extension}\"", query["response-content-disposition"].ToString());
        Assert.Equal("private, no-store", query["response-cache-control"].ToString());
        Assert.Equal(nameof(EvidenceDownloadAuthorization), result.ToString());
    }

    private static ServiceProvider Services() => new ServiceCollection().AddLogging().BuildServiceProvider();
    private static DefaultHttpContext Context(IServiceProvider services) => new()
    {
        RequestServices = services,
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "test")),
        Response = { Body = new MemoryStream() },
        Request = { Method = HttpMethods.Get, Scheme = "https", Body = new MemoryStream() }
    };
    private static void AssertHeaders(HttpContext context)
    {
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
    }
    private sealed class RecordingDownloadService : IEvidenceDownloadService
    {
        public int ActorChecks { get; private set; }
        public int Authorizations { get; private set; }
        public EvidenceDownloadException? Failure { get; init; }
        public Task RequireActorAsync(Guid actorUserId, CancellationToken cancellationToken = default)
        {
            ActorChecks++;
            if (Failure?.StatusCode == 403) throw Failure;
            return Task.CompletedTask;
        }
        public Task<EvidenceDownloadDetails> AuthorizeAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default)
        {
            Authorizations++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(new EvidenceDownloadDetails(fileId,
                new(new Uri("https://storage.example.test/synthetic-object"), new DateTimeOffset(2026, 10, 3, 18, 5, 0, TimeSpan.Zero))));
        }
    }
}
