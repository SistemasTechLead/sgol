using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class AccountAdministrationTests
{
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000201");
    private static readonly Guid PersonId = Guid.Parse("019d2d67-2c00-7000-8000-000000000202");
    private static readonly Guid UserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000203");
    private const string SyntheticPassword = "synthetic-only-password";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task List_ProvidesCollectionCountRequiredBySharedClient()
    {
        var context = CreateContext(authenticated: true);
        var result = await AccountApiEndpoints.HandleListAsync(
            context, new RecordingAccountService(CreateAccount()), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        Assert.Equal(1, json.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
        Assert.Equal(context.TraceIdentifier, json.RootElement.GetProperty("meta").GetProperty("correlationId").GetString());
        Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());

        var empty = await AccountApiEndpoints.HandleListAsync(
            CreateContext(authenticated: true), new RecordingAccountService(CreateAccount(), emptyList: true),
            CancellationToken.None);
        using var emptyJson = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(empty).Value, JsonOptions));
        Assert.Equal(0, emptyJson.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
        Assert.Empty(emptyJson.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task GeneratedActivationUsesOneTimeResponseWithoutChangingLegacyContract()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var result = await AccountApiEndpoints.HandleCreateAsync(
            new CreateAccountRequest { PersonId = PersonId, UserName = "person.synthetic" },
            context, service, CancellationToken.None);

        Assert.Null(service.CreateCommand?.TemporaryPassword);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(UserId.ToString("D"), data.GetProperty("account").GetProperty("id").GetString());
        Assert.Equal(SyntheticPassword.Length, data.GetProperty("temporaryPassword").GetString()?.Length);
        Assert.False(value?.ToString()?.Contains(SyntheticPassword, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Create_RequiresAuthenticatedUuidActor()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: false);

        var result = await AccountApiEndpoints.HandleCreateAsync(
            CreateRequest(),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.CreateCommand);
    }

    [Fact]
    public async Task Create_ForwardsIdempotencyAndTemporaryCredentialWithoutReturningIt()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);
        var idempotencyKey = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = idempotencyKey.ToString("D");

        var result = await AccountApiEndpoints.HandleCreateAsync(
            CreateRequest(),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(ActorUserId, service.CreateCommand?.ActorUserId);
        Assert.Equal(idempotencyKey, service.CreateCommand?.IdempotencyKey);
        Assert.Equal(PersonId, service.CreateCommand?.PersonId);
        Assert.Equal(SyntheticPassword, service.CreateCommand?.TemporaryPassword);
        Assert.DoesNotContain(SyntheticPassword, service.CreateCommand?.ToString(), StringComparison.Ordinal);
        AssertSafeResponseContract();
    }

    [Fact]
    public async Task Deactivate_ForwardsReasonAndIdempotencyKey()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);
        var idempotencyKey = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = idempotencyKey.ToString("D");

        var result = await AccountApiEndpoints.HandleDeactivateAsync(
            UserId,
            new AccountReasonRequest("Baja sintética"),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(idempotencyKey, service.StatusCommand?.IdempotencyKey);
        Assert.Equal("Baja sintética", service.StatusCommand?.Reason);
        Assert.False(service.Reactivated);
    }

    [Fact]
    public async Task Reactivate_RedactsTemporaryCredentialFromRequestAndCommandText()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var request = new ReactivateAccountRequest
        {
            Reason = "Reactivación sintética",
            TemporaryPassword = SyntheticPassword,
        };

        var result = await AccountApiEndpoints.HandleReactivateAsync(
            UserId,
            request,
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.True(service.Reactivated);
        Assert.DoesNotContain(SyntheticPassword, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticPassword, service.StatusCommand?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingIdempotencyKey_IsRejectedWithoutCallingService()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);

        var result = await AccountApiEndpoints.HandleCreateAsync(
            CreateRequest(),
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.CreateCommand);
    }

    [Fact]
    public async Task ResetMfa_ForwardsReasonAndRedactsTemporaryCredential()
    {
        var service = new RecordingAccountService(CreateAccount());
        var context = CreateContext(authenticated: true);
        var idempotencyKey = Guid.CreateVersion7();
        context.Request.Headers["Idempotency-Key"] = idempotencyKey.ToString("D");
        var request = new ResetMfaRequest
        {
            Reason = "Recuperación sintética",
            TemporaryPassword = SyntheticPassword,
        };

        var result = await AccountApiEndpoints.HandleMfaResetAsync(
            UserId,
            request,
            context,
            service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(idempotencyKey, service.ResetCommand?.IdempotencyKey);
        Assert.Equal("Recuperación sintética", service.ResetCommand?.Reason);
        Assert.DoesNotContain(SyntheticPassword, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SyntheticPassword, service.ResetCommand?.ToString(), StringComparison.Ordinal);
    }

    private static void AssertSafeResponseContract()
    {
        var propertyNames = typeof(AccountSummary)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Password", propertyNames);
        Assert.DoesNotContain("PasswordHash", propertyNames);
        Assert.DoesNotContain("SecurityStamp", propertyNames);
        Assert.DoesNotContain("TotpSecret", propertyNames);
        Assert.DoesNotContain("RecoveryCodes", propertyNames);
        Assert.DoesNotContain("Role", propertyNames);
        Assert.DoesNotContain("Position", propertyNames);
        Assert.DoesNotContain("Shift", propertyNames);
    }

    private static CreateAccountRequest CreateRequest() => new()
    {
        PersonId = PersonId,
        UserName = "person.synthetic",
        TemporaryPassword = SyntheticPassword,
    };

    private static AccountSummary CreateAccount() => new(
        UserId,
        PersonId,
        "person.synthetic",
        AccountStatus.Active,
        MustChangePassword: true,
        MfaEnrolledAt: null);

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

    private sealed class RecordingAccountService(AccountSummary account, bool emptyList = false) : IAccountAdministrationService
    {
        public CreateAccountCommand? CreateCommand { get; private set; }

        public ChangeAccountStatusCommand? StatusCommand { get; private set; }

        public ResetMfaCommand? ResetCommand { get; private set; }

        public bool Reactivated { get; private set; }

        public Task<IReadOnlyList<AccountSummary>> ListAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountSummary>>(emptyList ? [] : [account]);

        public Task<AccountMutationResult> CreateAsync(
            CreateAccountCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCommand = command;
            return Task.FromResult(new AccountMutationResult(account, Replayed: false,
                command.TemporaryPassword is null ? SyntheticPassword : null));
        }

        public Task<AccountMutationResult> DeactivateAsync(
            ChangeAccountStatusCommand command,
            CancellationToken cancellationToken = default)
        {
            StatusCommand = command;
            Reactivated = false;
            return Task.FromResult(new AccountMutationResult(account, Replayed: false));
        }

        public Task<AccountMutationResult> ReactivateAsync(
            ChangeAccountStatusCommand command,
            CancellationToken cancellationToken = default)
        {
            StatusCommand = command;
            Reactivated = true;
            return Task.FromResult(new AccountMutationResult(account, Replayed: false));
        }

        public Task<AccountMutationResult> ResetMfaAsync(
            ResetMfaCommand command,
            CancellationToken cancellationToken = default)
        {
            ResetCommand = command;
            return Task.FromResult(new AccountMutationResult(account, Replayed: false));
        }
    }
}
