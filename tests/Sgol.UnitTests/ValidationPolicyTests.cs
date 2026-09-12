using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ValidationPolicyTests
{
    [Theory]
    [InlineData("TAR-0005", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0007", "PISO_VENTAS", "SUBCOORDINACION")]
    [InlineData("TAR-0008", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0011", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0018", "PISO_VENTAS", "SUBCOORDINACION")]
    [InlineData("TAR-0026", "ADMINISTRACION", "DIRECCION")]
    [InlineData("TAR-0092", "SUBCOORDINACION", "ADMINISTRACION")]
    [InlineData("TAR-0093", "SUBCOORDINACION", "ADMINISTRACION")]
    public void Catalog_ContainsTheExactApprovedImmediateSuperior(string taskCode, string executor, string validator)
    {
        var policy = ValidationPolicyCatalog.Require(taskCode);
        Assert.Equal(executor, policy.ExecutorRole);
        Assert.Equal(validator, policy.ValidatorRole);
        ValidationPolicyCatalog.Validate(
            taskCode,
            true,
            executor,
            ValidationPolicyValues.ImmediateSuperior,
            validator,
            ValidationPolicyValues.AllowedResults.Reverse().ToArray());
    }

    [Fact]
    public void Validation_RejectsNonCanonicalAuthorityAndIncompleteOrUnknownResults()
    {
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", false, "SUBCOORDINACION", "ADMINISTRACION", Results()));
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", true, "GERENTE", "ADMINISTRACION", Results()));
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", true, "SUBCOORDINACION", "SUBCOORDINACION", Results()));
        Assert.Throws<ValidationPolicyValidationException>(() => ValidationPolicyCatalog.Validate(
            "TAR-0005", true, "SUBCOORDINACION", "PAR", "ADMINISTRACION", Results()));
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", true, "SUBCOORDINACION", "PISO_VENTAS", Results()));
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", true, "SUBCOORDINACION", "ADMINISTRACION", ["CUMPLIDA", "INCOMPLETA"]));
        Assert.Throws<ValidationPolicyValidationException>(() => Validate("TAR-0005", true, "SUBCOORDINACION", "ADMINISTRACION", ["CUMPLIDA", "INCOMPLETA", "DESCONOCIDA"]));
        Assert.Throws<TaskDefinitionNotMvpException>(() => ValidationPolicyCatalog.Require("TAR-9999"));
    }

    [Fact]
    public async Task PutEndpoint_UsesStrictBodyNormalizesResultsAndForwardsEtag()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"7\"";
        var releaseId = Guid.CreateVersion7();
        using var body = Request("TAR-0005", releaseId, ValidationPolicyValues.AllowedResults.Reverse());

        var result = await ValidationPolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
        Assert.NotNull(service.Command);
        Assert.Equal(7, service.Command.ExpectedRowVersion);
        Assert.Equal(releaseId, service.Command.ReleaseId);
        Assert.Equal(ValidationPolicyValues.AllowedResults.Reverse(), service.Command.AllowedResults);
    }

    [Fact]
    public async Task PutEndpoint_RejectsUnknownFieldsAndMapsNonMvpWithoutEffect()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var invalid = JsonSerializer.SerializeToDocument(new
        {
            releaseId = Guid.CreateVersion7(),
            isRequired = true,
            executorRole = "SUBCOORDINACION",
            validatorRelation = "SUPERIOR_INMEDIATO",
            validatorRole = "ADMINISTRACION",
            allowedResults = Results(),
            authorityByPosition = "GERENTE",
        });

        var malformed = await ValidationPolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", invalid.RootElement, context, service, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(malformed).StatusCode);
        Assert.Null(service.Command);
        Assert.Equal(1, service.RejectionCount);

        service.Exception = new TaskDefinitionNotMvpException();
        using var valid = Request("TAR-0005", Guid.CreateVersion7(), Results());
        var missing = await ValidationPolicyApiEndpoints.HandlePutAsync(
            "TAR-9999", valid.RootElement, context, service, CancellationToken.None);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);
    }

    [Fact]
    public async Task PutEndpoint_DoesNotLetMalformedInputBypassAuthorization()
    {
        var service = new RecordingService { RejectionException = new ValidationPolicyAccessDeniedException() };
        var context = AuthenticatedContext();
        using var body = JsonSerializer.SerializeToDocument(new { });

        var result = await ValidationPolicyApiEndpoints.HandlePutAsync(
            "TAR-0005", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    private static void Validate(
        string taskCode,
        bool isRequired,
        string executor,
        string validator,
        IReadOnlyCollection<string> results) =>
        ValidationPolicyCatalog.Validate(
            taskCode,
            isRequired,
            executor,
            ValidationPolicyValues.ImmediateSuperior,
            validator,
            results);

    private static string[] Results() => ["CUMPLIDA", "INCOMPLETA", "NO_CUMPLIDA"];

    private static JsonDocument Request(string taskCode, Guid releaseId, IEnumerable<string> results)
    {
        var policy = ValidationPolicyCatalog.Require(taskCode);
        return JsonSerializer.SerializeToDocument(new
        {
            releaseId,
            isRequired = true,
            executorRole = policy.ExecutorRole,
            validatorRelation = ValidationPolicyValues.ImmediateSuperior,
            validatorRole = policy.ValidatorRole,
            allowedResults = results,
        });
    }

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingService : IValidationPolicyService
    {
        public PutValidationPolicyCommand? Command { get; private set; }
        public Exception? Exception { get; set; }
        public Exception? RejectionException { get; set; }
        public int RejectionCount { get; private set; }

        public Task<ValidationPolicyVersionDetails> PutAsync(
            PutValidationPolicyCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new ValidationPolicyVersionDetails(
                Guid.CreateVersion7(),
                command.TaskCode,
                Guid.CreateVersion7(),
                command.ReleaseId,
                1,
                command.IsRequired,
                command.ExecutorRole,
                command.ValidatorRelation,
                command.ValidatorRole,
                ValidationPolicyValues.AllowedResults,
                "BORRADOR",
                null,
                null,
                null,
                null,
                null,
                1));
        }

        public Task RecordRejectionAsync(
            Guid actorUserId,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            if (RejectionException is not null)
            {
                throw RejectionException;
            }

            RejectionCount++;
            return Task.CompletedTask;
        }
    }
}
