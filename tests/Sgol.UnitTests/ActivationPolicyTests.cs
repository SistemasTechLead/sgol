using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ActivationPolicyTests
{
    private static readonly string[] ServiceDueDateReferenceParts = ["serviceKey", "dueDate", "reference"];

    [Fact]
    public void Catalog_ContainsExactlyTheEightApprovedTaskMechanisms()
    {
        Assert.Equal(
            new Dictionary<string, ActivationPolicyDefinition>
            {
                ["TAR-0005"] = new(ActivationModes.Recurring, ActivationOriginSchemas.WorkingDayWindow),
                ["TAR-0007"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
                ["TAR-0008"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
                ["TAR-0011"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
                ["TAR-0018"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
                ["TAR-0026"] = new(ActivationModes.Recurring, ActivationOriginSchemas.ServiceDueDateReference),
                ["TAR-0092"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
                ["TAR-0093"] = new(ActivationModes.Manual, ActivationOriginSchemas.ManualReference),
            },
            ActivationPolicyCatalog.All);
    }

    [Theory]
    [InlineData("TAR-0007")]
    [InlineData("TAR-0008")]
    [InlineData("TAR-0011")]
    [InlineData("TAR-0018")]
    [InlineData("TAR-0092")]
    [InlineData("TAR-0093")]
    public void ManualPolicies_RequireNullSchedule(string taskCode)
    {
        using var nullSchedule = JsonDocument.Parse("null");
        var task = TaskDefinitionCatalog.Require(taskCode);
        var accepted = new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Manual, nullSchedule.RootElement, ActivationOriginSchemas.ManualReference);
        Assert.Equal(JsonValueKind.Null, accepted.Schedule.RootElement.ValueKind);

        using var objectSchedule = JsonDocument.Parse("{}");
        Assert.Throws<ActivationPolicyValidationException>(() => new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Manual, objectSchedule.RootElement, ActivationOriginSchemas.ManualReference));
    }

    [Fact]
    public void Tar0005_RequiresWorkingDaysAndExactLocalWindows()
    {
        using var acceptedSchedule = SalesSchedule();
        var task = TaskDefinitionCatalog.Require("TAR-0005");
        var accepted = new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Recurring, acceptedSchedule.RootElement, ActivationOriginSchemas.WorkingDayWindow);
        Assert.Equal("12:00", accepted.Schedule.RootElement.GetProperty("localTimes")[0].GetString());
        Assert.Equal("17:00", accepted.Schedule.RootElement.GetProperty("localTimes")[1].GetString());

        using var invalidSchedule = JsonDocument.Parse(
            """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","18:00"],"timeZone":"America/Mexico_City"}""");
        Assert.Throws<ActivationPolicyValidationException>(() => new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Recurring, invalidSchedule.RootElement, ActivationOriginSchemas.WorkingDayWindow));
    }

    [Fact]
    public void Tar0026_RequiresApprovedDueDateStrategyAndConfigurableLocalTime()
    {
        Assert.Equal(
            ServiceDueDateReferenceParts,
            ActivationOriginSchemas.ServiceDueDateReferenceParts);
        using var acceptedSchedule = ServiceSchedule("08:30");
        var task = TaskDefinitionCatalog.Require("TAR-0026");
        var accepted = new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Recurring, acceptedSchedule.RootElement, ActivationOriginSchemas.ServiceDueDateReference);
        Assert.Equal("08:30", accepted.Schedule.RootElement.GetProperty("localTime").GetString());
        Assert.True(accepted.Schedule.RootElement.GetProperty("adjustDueDateToPreviousBusinessDay").GetBoolean());

        using var invalidSchedule = ServiceSchedule("24:00");
        Assert.Throws<ActivationPolicyValidationException>(() => new ActivationRuleVersion(
            Guid.CreateVersion7(), task.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1,
            ActivationModes.Recurring, invalidSchedule.RootElement, ActivationOriginSchemas.ServiceDueDateReference));
    }

    [Theory]
    [InlineData("EVENTO")]
    [InlineData("CONDICION")]
    [InlineData("INTEGRACION_EXTERNA")]
    [InlineData("DESCONOCIDO")]
    public void UnsupportedMechanisms_AreRejected(string mode)
    {
        using var schedule = JsonDocument.Parse("null");
        Assert.Throws<ActivationPolicyValidationException>(() => ActivationPolicyCatalog.Validate(
            "TAR-0007", mode, schedule.RootElement, ActivationOriginSchemas.ManualReference));
    }

    [Fact]
    public async Task PutEndpoint_UsesStrictCamelCaseBodyAndForwardsConcurrencyHeaders()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = "\"3\"";
        var releaseId = Guid.CreateVersion7();
        var taskDefinitionVersionId = Guid.CreateVersion7();
        using var body = JsonDocument.Parse(
            $$"""{"taskDefinitionVersionId":"{{taskDefinitionVersionId:D}}","releaseId":"{{releaseId:D}}","mode":"MANUAL","schedule":null,"originKeySchema":"MANUAL_REFERENCE_V1"}""");

        var result = await ActivationPolicyApiEndpoints.HandlePutAsync(
            "TAR-0007", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.NotNull(service.Command);
        Assert.Equal(taskDefinitionVersionId, service.Command.TaskDefinitionVersionId);
        Assert.Equal(3, service.Command.ExpectedRowVersion);
        Assert.Equal(ActivationModes.Manual, service.Command.Mode);
        Assert.Equal(JsonValueKind.Null, service.Command.Schedule.ValueKind);
    }

    [Fact]
    public async Task PutEndpoint_RejectsUnknownFieldsAsProblemJsonBeforeCallingService()
    {
        var service = new RecordingService();
        var context = AuthenticatedContext();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        using var body = JsonDocument.Parse(
            $$"""{"taskDefinitionVersionId":"{{Guid.CreateVersion7():D}}","releaseId":"{{Guid.CreateVersion7():D}}","mode":"MANUAL","schedule":null,"originKeySchema":"MANUAL_REFERENCE_V1","externalSystem":"ERP"}""");

        var result = await ActivationPolicyApiEndpoints.HandlePutAsync(
            "TAR-0007", body.RootElement, context, service, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
        Assert.Null(service.Command);
    }

    private static JsonDocument SalesSchedule() => JsonDocument.Parse(
        """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""");

    private static JsonDocument ServiceSchedule(string localTime) => JsonDocument.Parse(
        $$"""{"kind":"BUSINESS_DAYS_BEFORE_DUE_DATE","businessDaysBefore":3,"localTime":"{{localTime}}","timeZone":"America/Mexico_City","adjustDueDateToPreviousBusinessDay":true}""");

    private static DefaultHttpContext AuthenticatedContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString("D"))], "synthetic"));
        return context;
    }

    private sealed class RecordingService : IActivationPolicyService
    {
        public PutActivationPolicyCommand? Command { get; private set; }

        public Task<ActivationRuleVersionDetails> PutAsync(
            PutActivationPolicyCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(new ActivationRuleVersionDetails(
                Guid.CreateVersion7(), command.TaskCode, command.TaskDefinitionVersionId, command.ReleaseId, 1,
                command.Mode, command.Schedule, command.OriginKeySchema, "BORRADOR", null, null, null, null, 1));
        }
    }
}
