using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class BranchCatalogTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task CanonicalSeed_IsReturnedByApiAndAccessibleUi()
    {
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory.Services);
        using var client = factory.CreateClient();

        using var apiResponse = await client.GetAsync("/api/v1/branches/LOR-001");
        using var payload = JsonDocument.Parse(await apiResponse.Content.ReadAsStringAsync());
        using var pageResponse = await client.GetAsync("/branches/LOR-001");
        var page = await pageResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
        Assert.Equal("application/json", apiResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(BranchScope.LorettaCode, payload.RootElement.GetProperty("data").GetProperty("code").GetString());
        Assert.Equal(BranchScope.LorettaName, payload.RootElement.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(BranchScope.ActiveStatus, payload.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.True(payload.RootElement.GetProperty("meta").TryGetProperty("correlationId", out _));
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Contains("LOR-001", page, StringComparison.Ordinal);
        Assert.Contains("Loretta", page, StringComparison.Ordinal);
        Assert.Contains("Activa", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalQueryScope_ReturnsOnlyLoretta()
    {
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IBranchCatalogReader>();

        var branches = await reader.ListForScopeAsync(BranchScope.GlobalQueryScope);

        var branch = Assert.Single(branches);
        Assert.Equal(BranchScope.LorettaCode, branch.Code);
        Assert.Equal(BranchScope.LorettaName, branch.Name);
    }

    [Theory]
    [InlineData("TODAS")]
    [InlineData("LOR-002")]
    public async Task UnsupportedCode_IsRejectedWithoutChangingCatalog(string branchCode)
    {
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory.Services);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/branches/{branchCode}");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ALCANCE_INVALIDO", payload.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, await context.Branches.AsNoTracking().CountAsync());
        Assert.Equal(BranchScope.LorettaCode, await context.Branches.Select(branch => branch.Code).SingleAsync());
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsAnotherBranchWithoutPersistingIt()
    {
        await using var factory = CreateFactory();
        await ResetDatabaseAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        context.Branches.Add(new Branch
        {
            Id = Guid.CreateVersion7(),
            Code = "LOR-002",
            Name = "Synthetic branch",
            Status = BranchScope.ActiveStatus,
            TimeZone = BranchScope.TimeZone,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.Branches.AsNoTracking().CountAsync());
        Assert.Equal(BranchScope.LorettaCode, await context.Branches.Select(branch => branch.Code).SingleAsync());
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
                    }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    services.AddSingleton<IStartupFilter, AuthenticatedDirectionStartupFilter>();
                });
            });

    private static async Task ResetDatabaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private sealed class AuthenticatedDirectionStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "synthetic-direction"),
                    new Claim(ClaimTypes.Role, "DIRECCION"),
                ],
                "synthetic"));
                await nextMiddleware();
            });
            next(app);
        };
    }
}
