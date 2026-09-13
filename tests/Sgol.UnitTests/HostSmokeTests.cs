using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HostSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    services.RemoveAll<ITaskDefinitionService>();
                    services.AddSingleton<ITaskDefinitionService, UnusedTaskDefinitionService>();
                    services.RemoveAll<IActivationPolicyService>();
                    services.AddSingleton<IActivationPolicyService, UnusedActivationPolicyService>();
                    services.RemoveAll<IEvidencePolicyService>();
                    services.AddSingleton<IEvidencePolicyService, UnusedEvidencePolicyService>();
                    services.RemoveAll<IValidationPolicyService>();
                    services.AddSingleton<IValidationPolicyService, UnusedValidationPolicyService>();
                    services.RemoveAll<IEvidenceContributionService>();
                    services.AddSingleton<IEvidenceContributionService, UnusedEvidenceContributionService>();
                    services.RemoveAll<IEvidenceReviewService>();
                    services.AddSingleton<IEvidenceReviewService, UnusedEvidenceReviewService>();
                });
            })
            .CreateClient();
    }

    [Fact]
    public async Task LiveEndpoint_ReturnsSuccessWithoutBusinessData()
    {
        var response = await _client.GetAsync("/health/live");
        var payload = await response.Content.ReadFromJsonAsync<LiveResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alive", payload?.Status);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(payload?.Meta.CorrelationId, Assert.Single(values));
    }

    [Fact]
    public async Task ReadyEndpoint_FailsClosedWithoutPostgreSqlOrObjectStorageDetails()
    {
        using var response = await _client.GetAsync("/health/ready");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected readiness 503 but received {(int)response.StatusCode}: {payload}");
        Assert.Contains("unavailable", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Host=", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/css/tokens.css", "--color-acento")]
    [InlineData("/css/components.css", ".navegacion-lateral__item")]
    public async Task SharedInterfaceStyles_AreServed(string path, string expectedContract)
    {
        var response = await _client.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedContract, content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/bootstrap")]
    [InlineData("/api/v1/bootstrap")]
    [InlineData("/api/v1/auth/bootstrap")]
    public async Task PublicBootstrapEndpoint_DoesNotExist(string path)
    {
        using var response = await _client.PostAsync(path, content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BranchApi_RequiresAnAuthenticatedSession()
    {
        using var response = await _client.GetAsync("/api/v1/branches/LOR-001");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task WeekApi_RequiresAnAuthenticatedSessionWithProblemDetails()
    {
        using var response = await _client.GetAsync("/api/v1/weeks/2026/37");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Theory]
    [InlineData("/api/v1/weeks/2026/37/close")]
    [InlineData("/api/v1/weeks/2026/37/reopen")]
    public async Task FormalWeekCloseAndReopenEndpoints_DoNotExist(string path)
    {
        using var response = await _client.PostAsync(path, content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkPlanEnsureRequiresAuthenticationAndGetRemainsOutsideHu020()
    {
        using var ensure = await _client.PostAsync("/api/v1/plans/2026/36/ensure", content: null);
        using var get = await _client.GetAsync("/api/v1/plans/2026/36");

        Assert.Equal(HttpStatusCode.Unauthorized, ensure.StatusCode);
        Assert.Equal("application/problem+json", ensure.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/task-definitions")]
    [InlineData("/api/v1/task-definitions/TAR-0005")]
    public async Task TaskDefinitionQueries_RequireAuthentication(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EvidencePolicyEndpoint_RequiresAuthentication()
    {
        using var response = await _client.PutAsync(
            "/api/v1/task-definitions/TAR-0005/evidence-policy",
            JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ValidationPolicyEndpoint_RequiresAuthentication()
    {
        using var response = await _client.PutAsync(
            "/api/v1/task-definitions/TAR-0005/validation-policy",
            JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("POST", "/api/v1/files/upload-intents")]
    [InlineData("POST", "/api/v1/files/019d2d67-2c00-7000-8000-000000000100/complete")]
    [InlineData("GET", "/api/v1/files/019d2d67-2c00-7000-8000-000000000100/status")]
    [InlineData("POST", "/api/v1/obligations/019d2d67-2c00-7000-8000-000000000101/evidence")]
    [InlineData("POST", "/api/v1/obligations/019d2d67-2c00-7000-8000-000000000101/evidence/019d2d67-2c00-7000-8000-000000000102/replacements")]
    [InlineData("GET", "/api/v1/obligations/019d2d67-2c00-7000-8000-000000000101/evidence")]
    public async Task Hu025EndpointsRequireAuthentication(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { });
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Hu026EndpointRequiresAuthentication()
    {
        using var response = await _client.GetAsync(
            "/api/v1/obligations/019d2d67-2c00-7000-8000-000000000101/evidence-review");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EvidenceDownloadRemainsOutsideHu025()
    {
        using var response = await _client.GetAsync("/api/v1/files/019d2d67-2c00-7000-8000-000000000100/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ActivationPolicyEndpoint_RequiresAuthentication()
    {
        using var response = await _client.PutAsync(
            "/api/v1/task-definitions/TAR-0005/activation-policy",
            JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TaskDefinitionDeletionEndpoint_DoesNotExist()
    {
        using var response = await _client.DeleteAsync("/api/v1/task-definitions/TAR-0005");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private sealed record LiveResponse(string Status, ResponseMeta Meta);

    private sealed record ResponseMeta(string CorrelationId);

    private sealed class UnusedTaskDefinitionService : ITaskDefinitionService
    {
        public Task<IReadOnlyList<TaskDefinitionDetails>> ListAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TaskDefinitionDetails> GetAsync(Guid actorUserId, Guid correlationId, string taskCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TaskDefinitionVersionDetails> CreateVersionAsync(CreateTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TaskDefinitionVersionDetails> PublishVersionAsync(PublishTaskDefinitionVersionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TaskDefinitionVersionDetails> DeactivateNewAsync(DeactivateTaskDefinitionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedActivationPolicyService : IActivationPolicyService
    {
        public Task<ActivationRuleVersionDetails> PutAsync(
            PutActivationPolicyCommand command,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedEvidencePolicyService : IEvidencePolicyService
    {
        public Task<EvidencePolicyVersionDetails> PutAsync(
            PutEvidencePolicyCommand command,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordRejectionAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UnusedValidationPolicyService : IValidationPolicyService
    {
        public Task<ValidationPolicyVersionDetails> PutAsync(
            PutValidationPolicyCommand command,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordRejectionAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UnusedEvidenceContributionService : IEvidenceContributionService
    {
        public Task<EvidenceUploadIntentResult> CreateUploadIntentAsync(CreateEvidenceUploadCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceFileStatusDetails> CompleteUploadAsync(CompleteEvidenceUploadCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceFileStatusDetails> GetFileStatusAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceDetails> ContributeAsync(ContributeEvidenceCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceDetails> ReplaceAsync(ReplaceEvidenceCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidencePage> ListAsync(EvidenceQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedEvidenceReviewService : IEvidenceReviewService
    {
        public Task<EvidenceReviewDetails> ReviewAsync(
            EvidenceReviewQuery query,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
