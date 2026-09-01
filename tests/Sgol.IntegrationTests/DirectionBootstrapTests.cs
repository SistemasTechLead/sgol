using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class DirectionBootstrapTests : IAsyncLifetime
{
    private static readonly string SyntheticSecret = $"synthetic-bootstrap-{new string('x', 14)}";
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task InitialExecution_CreatesActiveDirectionIdentityAndAuditInOneTransaction()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await ResetDatabaseAsync(context);

        var result = await ExecuteAsync(scope.ServiceProvider);

        var person = await context.People.AsNoTracking().SingleAsync();
        var employment = await context.EmploymentVersions.AsNoTracking().SingleAsync();
        var user = await context.AppUsers.AsNoTracking().SingleAsync();
        var credential = await context.IdentityCredentials.AsNoTracking().SingleAsync();
        var role = await context.RoleAssignmentVersions.AsNoTracking().SingleAsync();
        var audit = await context.AuditEvents.AsNoTracking().SingleAsync();

        Assert.Equal(result.PersonId, person.Id);
        Assert.Equal(BootstrapContract.ActivePersonStatus, employment.Status);
        Assert.Equal(BranchScope.LorettaId, employment.BranchId);
        Assert.Equal(result.UserId, user.Id);
        Assert.Equal(BootstrapContract.ActiveAccountStatus, user.Status);
        Assert.True(user.MustChangePassword);
        Assert.Null(user.MfaEnrolledAt);
        Assert.True(user.RequiresFirstAccessSetup);
        Assert.Equal(result.RoleAssignmentId, role.Id);
        Assert.Equal(BootstrapContract.DirectionRoleCode, role.RoleCode);
        Assert.Equal(BootstrapContract.ActiveRoleStatus, role.Status);
        Assert.Equal("TECHNICAL_OPERATOR", audit.ActorType);
        Assert.Null(audit.ActorUserId);
        Assert.Equal("DIRECTION_BOOTSTRAP_COMPLETED", audit.Action);
        Assert.Equal("APP_USER", audit.ResourceType);
        Assert.Equal(user.Id, audit.ResourceId);
        Assert.Equal("SUCCESS", audit.Outcome);
        Assert.True(await context.DirectionBootstrapMarkers.AsNoTracking().AnyAsync());

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(user, credential.PasswordHash, SyntheticSecret));
    }

    [Fact]
    public async Task SecondExecution_IsPermanentlyRejectedWithoutAdditionalEffects()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await ResetDatabaseAsync(context);
        await ExecuteAsync(scope.ServiceProvider);

        var error = await Assert.ThrowsAsync<DirectionBootstrapAlreadyCompletedException>(
            () => ExecuteAsync(scope.ServiceProvider));

        Assert.DoesNotContain(SyntheticSecret, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, await context.DirectionBootstrapMarkers.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.People.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.EmploymentVersions.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.AppUsers.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.IdentityCredentials.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.RoleAssignmentVersions.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.AuditEvents.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData("person")]
    [InlineData("employment_version")]
    [InlineData("app_user")]
    [InlineData("identity_credential")]
    [InlineData("role_assignment_version")]
    [InlineData("audit_event")]
    public async Task FailureInAnyCreationOrAuditStep_RollsBackEverything(string rejectedTable)
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await ResetDatabaseAsync(context);
        await InstallRejectingTriggerAsync(context, rejectedTable);

        await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(scope.ServiceProvider));

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(await context.DirectionBootstrapMarkers.AsNoTracking().AnyAsync());
        Assert.False(await context.People.AsNoTracking().AnyAsync());
        Assert.False(await context.EmploymentVersions.AsNoTracking().AnyAsync());
        Assert.False(await context.AppUsers.AsNoTracking().AnyAsync());
        Assert.False(await context.IdentityCredentials.AsNoTracking().AnyAsync());
        Assert.False(await context.RoleAssignmentVersions.AsNoTracking().AnyAsync());
        Assert.False(await context.AuditEvents.AsNoTracking().AnyAsync());
    }

    [Fact]
    public async Task Secret_IsAbsentFromErrorsAuditAndPersistedPlainText()
    {
        var logs = new ConcurrentQueue<string>();
        await using var factory = CreateFactory(new CollectingLoggerProvider(logs));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await ResetDatabaseAsync(context);
        var input = CreateInput();

        Assert.DoesNotContain(SyntheticSecret, input.ToString(), StringComparison.Ordinal);
        await ExecuteAsync(scope.ServiceProvider);
        var user = await context.AppUsers.AsNoTracking().SingleAsync();
        var credential = await context.IdentityCredentials.AsNoTracking().SingleAsync();
        var audit = await context.AuditEvents.AsNoTracking().SingleAsync();

        Assert.NotEqual(SyntheticSecret, credential.PasswordHash);
        Assert.DoesNotContain(SyntheticSecret, credential.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticSecret, audit.AfterData?.RootElement.GetRawText() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticSecret, audit.BeforeData?.RootElement.GetRawText() ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticSecret, audit.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(logs, message => message.Contains(SyntheticSecret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Migration_DoesNotPreloadAnAccountRoleOrFunctionalData()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await ResetDatabaseAsync(context);

        Assert.Equal(
            BranchScope.LorettaCode,
            await context.Branches.Select(branch => branch.Code).SingleAsync());
        Assert.False(await context.DirectionBootstrapMarkers.AnyAsync());
        Assert.False(await context.People.AnyAsync());
        Assert.False(await context.EmploymentVersions.AnyAsync());
        Assert.False(await context.AppUsers.AnyAsync());
        Assert.False(await context.IdentityCredentials.AnyAsync());
        Assert.False(await context.RoleAssignmentVersions.AnyAsync());
        Assert.False(await context.AuditEvents.AnyAsync());
    }

    private static Task<DirectionBootstrapResult> ExecuteAsync(
        IServiceProvider services,
        string personCode = "DIR-001",
        string userName = "direction.initial") =>
        services.GetRequiredService<DirectionBootstrapService>().ExecuteAsync(
            CreateInput(personCode, userName));

    private static DirectionBootstrapInput CreateInput(
        string personCode = "DIR-001",
        string userName = "direction.initial") => new()
        {
            PersonStableCode = personCode,
            PersonDisplayName = "Synthetic Direction",
            UserName = userName,
            InitialPassword = SyntheticSecret,
        };

    private WebApplicationFactory<Program> CreateFactory(ILoggerProvider? loggerProvider = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
                    }));
                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    if (loggerProvider is not null)
                    {
                        logging.AddProvider(loggerProvider);
                    }
                });
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });

    private static async Task ResetDatabaseAsync(SgolDbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
    }

    private static Task<int> InstallRejectingTriggerAsync(SgolDbContext context, string table)
    {
        var validatedTable = table switch
        {
            "person" => "person",
            "employment_version" => "employment_version",
            "app_user" => "app_user",
            "identity_credential" => "identity_credential",
            "role_assignment_version" => "role_assignment_version",
            "audit_event" => "audit_event",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };

#pragma warning disable EF1002 // validatedTable is selected exclusively from the fixed allowlist above.
        return context.Database.ExecuteSqlRawAsync(
            $"""
            CREATE OR REPLACE FUNCTION reject_bootstrap_test_insert()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'synthetic bootstrap failure';
            END;
            $function$;

            CREATE TRIGGER reject_bootstrap_test_insert
            BEFORE INSERT ON {validatedTable}
            FOR EACH ROW
            EXECUTE FUNCTION reject_bootstrap_test_insert();
            """);
#pragma warning restore EF1002
    }

    private sealed class CollectingLoggerProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class CollectingLogger(ConcurrentQueue<string> messages) : ILogger
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
            messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                messages.Enqueue(exception.ToString());
            }
        }
    }
}
