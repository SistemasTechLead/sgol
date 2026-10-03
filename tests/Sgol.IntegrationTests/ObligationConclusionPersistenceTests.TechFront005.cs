using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationConclusionPersistenceTests
{
    private static readonly string[] TechFront005Claimants = ["TF005-A", "TF005-B"];
    [Theory]
    [InlineData("TAR-0008")]
    [InlineData("TAR-0011")]
    public async Task TechFront005LateCat003And004KeepExecutionAndValidationSeparate(string taskCode)
    {
        var due = taskCode == "TAR-0008" ? Now.AddMinutes(30) : Now.AddDays(8);
        using var input = taskCode == "TAR-0008" ? JsonSerializer.SerializeToDocument(new
        { taskCode, operationReference = "TF005-LATE", detectedAt = Now.UtcDateTime, claimantReferences = TechFront005Claimants }) :
            JsonSerializer.SerializeToDocument(new
            {
                taskCode,
                caseReference = "TF005-LATE",
                authorizationReference = "TF005-AUTH",
                productReference = "TF005-P",
                solutionType = "CAMBIO",
                authorizedAt = Now.UtcDateTime
            });
        var identity = ManualGenerationInput.Identity(input.RootElement);
        var originKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToJsonString())));
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 2,
            input = input.RootElement,
            originIdentity = identity,
            calendarDayVersionIds = Array.Empty<Guid>()
        });
        var snapshot = new ManualObligationSnapshot(taskCode, originKey, null, payload, due);
        var fixture = await ResetAndCreateObligationAsync(true, taskCode, manualSnapshot: snapshot);
        await using var db = CreateContext();
        var late = due.AddMinutes(1);
        var conclusion = await ConclusionService(db, new FixedClock(late)).ConcludeAsync(new(
            fixture.ActorId, Guid.CreateVersion7(), Guid.CreateVersion7(), fixture.ObligationId, 1));
        Assert.Equal(WorkObligationStatuses.Concluded, conclusion.ExecutionStatus);
        var obligation = await db.WorkObligations.AsNoTracking().SingleAsync(o => o.Id == fixture.ObligationId);
        Assert.Equal(due, obligation.DueAt);
        Assert.True(obligation.ConcludedAt > obligation.DueAt);
        Assert.Empty(await db.ValidationDecisionVersions.ToArrayAsync());
        var requirement = await db.ValidationRequirements.SingleAsync(r => r.ObligationId == fixture.ObligationId);
        Assert.Equal(ValidationStatuses.Pending, requirement.Status);
        var validator = await SeedActorAsync("TF005-LATE-ADMIN", CanonicalRole.Administration);
        var service = new EfValidationDecisionService(db, new EfEvidenceConclusionReviewService(db, NewUuidGenerator()),
            new FixedClock(late), NewUuidGenerator());
        var issued = await service.IssueAsync(new(validator, Guid.CreateVersion7(), Guid.CreateVersion7(),
            fixture.ObligationId, 1, ValidationResults.NotFulfilled, "Expediente completo posterior al objetivo temporal.", null));
        Assert.Equal(ValidationResults.NotFulfilled, issued.Decision.Result);
        Assert.Equal(WorkObligationStatuses.Concluded, issued.History.ExecutionStatus);
        Assert.Single(await db.ExecutionResults.Where(e => e.ObligationId == fixture.ObligationId).ToArrayAsync());
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "OBLIGATION_CONCLUDED" && a.ResourceId == fixture.ObligationId));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "VALIDATION_DECISION_ISSUED" && a.ResourceId == requirement.Id));
    }
}
