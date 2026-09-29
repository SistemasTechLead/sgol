using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class GenerationRequestPersistenceTests
{
    private static async Task<Scenario> AddManualTaskAsync(SgolDbContext db, Scenario scenario, string code)
    {
        var release = await db.ConfigurationReleases.Select(r => r.Id).SingleAsync();
        var task = TaskDefinitionCatalog.Require(code);
        var version = new TaskDefinitionVersion(Guid.CreateVersion7(), task.Id, 1, 1, JsonDocument.Parse("{}"), release);
        version.ApplyPublished(Published(version.Id), true);
        var eligibility = new EligibilityPolicyVersion(Guid.CreateVersion7(), task.Id, version.Id, release, null, 1,
            EligibilityPolicyCatalog.RequireRole(code), true, null);
        eligibility.ApplyPublished(Published(eligibility.Id));
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(Guid.CreateVersion7(), task.Id, version.Id, release, null, 1,
            ActivationModes.Manual, schedule.RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(rule.Id));
        var evidence = new EvidencePolicyVersion(Guid.CreateVersion7(), task.Id, version.Id, release, null, 1);
        evidence.ApplyPublished(Published(evidence.Id));
        var matrix = ValidationPolicyCatalog.Require(code);
        var validation = new ValidationPolicyVersion(Guid.CreateVersion7(), task.Id, version.Id, release, null, 1,
            true, matrix.ExecutorRole, ValidationPolicyValues.ImmediateSuperior, matrix.ValidatorRole, ValidationPolicyValues.AllowedResults);
        validation.ApplyPublished(Published(validation.Id));
        db.AddRange(version, eligibility, rule, evidence, validation);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return scenario with { RuleVersionId = rule.Id, TaskDefinitionVersionId = version.Id };
    }

    private static JsonElement CatInput(string code, string reference = "SYN", Guid? parent = null)
    {
        var instant = Now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return code switch
        {
            "TAR-0008" => JsonSerializer.SerializeToElement(new { taskCode = code, operationReference = reference, detectedAt = instant, claimantReferences = (string[])["A", "B"] }),
            "TAR-0011" => JsonSerializer.SerializeToElement(new { taskCode = code, caseReference = reference, authorizationReference = reference, productReference = "P", solutionType = "REPARACION", authorizedAt = instant }),
            "TAR-0018" => JsonSerializer.SerializeToElement(new { taskCode = code, eventReference = reference, zoneReference = "Z", planogramReference = "P" }),
            "TAR-0092" => JsonSerializer.SerializeToElement(new { taskCode = code, receiptReference = reference, supplierReference = "S", documentReference = "D", startedAt = instant, merchandiseReference = "M" }),
            "TAR-0093" => JsonSerializer.SerializeToElement(new { taskCode = code, parentObligationId = parent, incidentReference = reference, incidentType = "DIFERENCIA_Y_DANO", description = "Incidencia sintética", occurredAt = instant }),
            _ => throw new InvalidOperationException()
        };
    }

    [Fact]
    public async Task SixCatFormsSnapshotDeadlinesAndParentWithoutAssignmentsOrEvidence()
    {
        var initial = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var db = CreateContext();
        var service = CreateService(db);
        var first = await service.CreateAsync(Command(initial, Guid.CreateVersion7()));
        Assert.Equal(Now.AddHours(24), first.DueAt);
        Guid? parent = null;
        foreach (var code in new[] { "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0092", "TAR-0093" })
        {
            var scenario = await AddManualTaskAsync(db, initial, code);
            if (code == "TAR-0011")
            {
                var command = Command(scenario, Guid.CreateVersion7()) with { InputPayload = CatInput(code) };
                var missing = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(command));
                Assert.Equal("CALENDARIO_GENERACION_INCOMPLETO", missing.Code);
                var release = await db.ConfigurationReleases.Select(r => r.Id).SingleAsync();
                for (var index = 1; index <= 8; index++)
                {
                    var day = new CalendarDayVersion(Guid.CreateVersion7(), BranchScope.LorettaId, new DateOnly(2026, 9, 3).AddDays(index),
                        index == 2 ? CalendarContract.Holiday : CalendarContract.WorkingDay, index != 2, release, "Sintético");
                    day.ApplyPublished(Published(day.Id));
                    db.CalendarDayVersions.Add(day);
                }
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
            var created = await service.CreateAsync(Command(scenario, Guid.CreateVersion7()) with { InputPayload = CatInput(code, parent: parent) });
            if (code == "TAR-0092") parent = created.ObligationId;
            Assert.NotNull(created.ObligationId);
            Assert.NotNull(created.EvidencePolicyVersionId);
            Assert.NotNull(created.ValidationPolicyVersionId);
            Assert.Equal(code == "TAR-0008" ? Now.AddMinutes(30) : code == "TAR-0011" ? Now.AddDays(8) : (DateTimeOffset?)null, created.DueAt);
            var obligation = await db.WorkObligations.AsNoTracking().SingleAsync(o => o.Id == created.ObligationId);
            Assert.Equal(code == "TAR-0093" ? parent : null, obligation.ParentObligationId);
            Assert.Equal(code == "TAR-0011" ? 8 : 0, obligation.InputPayload!.RootElement.GetProperty("calendarDayVersionIds").GetArrayLength());
        }
        Assert.Equal(6, await db.GenerationRequests.CountAsync());
        Assert.Equal(6, await db.WorkObligations.CountAsync());
        Assert.Empty(await db.AssignmentVersions.ToListAsync());
        Assert.Empty(await db.PlanVersionObligations.ToListAsync());
        Assert.Equal(6, await db.AuditEvents.CountAsync(a => a.Action == "WORK_OBLIGATION_CREATED"));
        Assert.DoesNotContain(await db.AuditEvents.Select(a => a.AfterData).ToListAsync(),
            json => json is not null && json.RootElement.GetRawText().Contains("claimantReferences", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReceiptPaginationBindsActorFilterAndPageSizeAndHidesConcludedParents()
    {
        var initial = await ResetAndSeedAsync(CanonicalRole.Subcoordination);
        await using var db = CreateContext();
        var receipt = await AddManualTaskAsync(db, initial, "TAR-0092");
        var incident = await AddManualTaskAsync(db, initial, "TAR-0093");
        var service = CreateService(db, protection: new EphemeralDataProtectionProvider());
        for (var i = 0; i < 3; i++)
            await service.CreateAsync(Command(receipt, Guid.CreateVersion7()) with { InputPayload = CatInput("TAR-0092", "REC-" + i.ToString(CultureInfo.InvariantCulture)) });
        var page = await service.GetReceiptOriginsAsync(initial.ActorUserId, null, null, 2);
        Assert.Equal(2, page.Items.Count);
        Assert.NotNull(page.NextCursor);
        var next = await service.GetReceiptOriginsAsync(initial.ActorUserId, null, page.NextCursor, 2);
        Assert.Single(next.Items);
        Assert.Null(next.NextCursor);
        Assert.Empty(page.Items.Select(r => r.ObligationId).Intersect(next.Items.Select(r => r.ObligationId)));
        await Assert.ThrowsAsync<ManualGenerationException>(() => service.GetReceiptOriginsAsync(initial.ActorUserId, null, page.NextCursor, 1));
        var peer = await SeedActorAsync("SYN-PEER", CanonicalRole.Subcoordination);
        await Assert.ThrowsAsync<ManualGenerationException>(() => service.GetReceiptOriginsAsync(peer, null, page.NextCursor, 2));
        Assert.Empty((await service.GetReceiptOriginsAsync(peer, null, null, 2)).Items);
        var selected = await db.WorkObligations.SingleAsync(o => o.Id == page.Items[0].ObligationId);
        await ObligationConclusionTestData.EnsurePolicyAsync(db, selected.TaskDefinitionVersionId, Now);
        var responsible = await db.AppUsers.Where(u => u.Id == initial.ActorUserId).Select(u => u.PersonId).SingleAsync();
        await ObligationConclusionTestData.ConcludeAsync(db, selected.Id, Now, responsible, receiptDifference: true);
        db.ChangeTracker.Clear();
        var denied = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(Command(incident, Guid.CreateVersion7()) with
        { InputPayload = CatInput("TAR-0093", parent: selected.Id) }));
        Assert.Equal("ORIGEN_PADRE_NO_DISPONIBLE", denied.Code);
        Assert.DoesNotContain((await service.GetReceiptOriginsAsync(initial.ActorUserId, null, null, 10)).Items, r => r.ObligationId == selected.Id);
        Assert.Equal(3, await db.GenerationRequests.CountAsync());
    }
    [Fact]
    public async Task AcceptedSnapshotAndReplaySurviveTaskDeactivation()
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var db = CreateContext();
        var service = CreateService(db);
        var command = Command(scenario, Guid.CreateVersion7());
        var accepted = await service.CreateAsync(command);
        var before = await service.GetAsync(scenario.ActorUserId, Guid.CreateVersion7(), accepted.GenerationRequestId);
        var version = await db.TaskDefinitionVersions.SingleAsync(v => v.Id == scenario.TaskDefinitionVersionId);
        version.ApplyPublished(Published(version.Id), false);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var replay = await service.CreateAsync(command);
        var after = await service.GetAsync(scenario.ActorUserId, Guid.CreateVersion7(), accepted.GenerationRequestId);
        Assert.Equal(accepted.ObligationId, replay.ObligationId);
        Assert.Equal(before.InputPayload!.Value.GetRawText(), after.InputPayload!.Value.GetRawText());
        Assert.Equal(before.DueAt, after.DueAt);
        Assert.Equal(before.EvidencePolicyVersionId, after.EvidencePolicyVersionId);
        Assert.Empty(await service.GetOptionsAsync(scenario.ActorUserId));
        await Assert.ThrowsAsync<GenerationRequestTaskInactiveException>(() => service.CreateAsync(command with { IdempotencyKey = Guid.CreateVersion7() }));
    }

    [Theory]
    [InlineData("TAR-0007", false)]
    [InlineData("TAR-0008", true)]
    [InlineData("TAR-0092", true)]
    public async Task CatUniquenessIsPermanentOrPendingAndRn010StillRecoversSamePeriod(string code, bool activeOnly)
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction, taskCode: code);
        await using var db = CreateContext();
        var service = CreateService(db);
        var command = Command(scenario, Guid.CreateVersion7());
        if (code != "TAR-0007") command = command with { InputPayload = CatInput(code) };
        var accepted = await service.CreateAsync(command);
        await ObligationConclusionTestData.EnsurePolicyAsync(db, scenario.TaskDefinitionVersionId, Now);
        var person = await db.AppUsers.Where(u => u.Id == scenario.ActorUserId).Select(u => u.PersonId).SingleAsync();
        await ObligationConclusionTestData.ConcludeAsync(db, accepted.ObligationId!.Value, Now, person, receiptDifference: code == "TAR-0092");
        var same = await service.CreateAsync(command with { IdempotencyKey = Guid.CreateVersion7() });
        Assert.Equal(accepted.ObligationId, same.ObligationId);
        var range = Sgol.Planning.Contracts.WeekContract.Calculate(2026, 37);
        var next = new Sgol.Planning.Contracts.WeekPeriod(Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 37,
            range.StartsOn, range.EndsOn, Sgol.Planning.Contracts.WeekContract.Current);
        db.WeekPeriods.Add(next);
        await db.SaveChangesAsync();
        var later = command with { IdempotencyKey = Guid.CreateVersion7(), PeriodId = next.Id };
        if (activeOnly)
        {
            var created = await service.CreateAsync(later);
            Assert.NotEqual(accepted.ObligationId, created.ObligationId);
        }
        else
        {
            var conflict = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(later));
            Assert.Equal("ORIGEN_YA_REGISTRADO", conflict.Code);
        }
        Assert.Equal(activeOnly ? 2 : 1, await db.WorkObligations.CountAsync());
    }
    [Fact]
    public async Task IncidentRejectsHiddenMissingWrongPeriodAndEarlierParentThenReplaysAfterConclusion()
    {
        var initial = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var db = CreateContext();
        var receipt = await AddManualTaskAsync(db, initial, "TAR-0092");
        var incident = await AddManualTaskAsync(db, initial, "TAR-0093");
        var service = CreateService(db);
        var parent = await service.CreateAsync(Command(receipt, Guid.CreateVersion7()) with { InputPayload = CatInput("TAR-0092") });
        var command = Command(incident, Guid.CreateVersion7()) with { InputPayload = CatInput("TAR-0093", parent: parent.ObligationId) };
        var peer = await SeedActorAsync("SYN-OUTSIDE-PARENT", CanonicalRole.Subcoordination);
        await Assert.ThrowsAsync<GenerationRequestNotFoundException>(() => service.CreateAsync(command with { ActorUserId = peer }));
        await Assert.ThrowsAsync<GenerationRequestNotFoundException>(() => service.CreateAsync(command with { InputPayload = CatInput("TAR-0093", parent: Guid.CreateVersion7()) }));
        var range = Sgol.Planning.Contracts.WeekContract.Calculate(2026, 37);
        var otherPeriod = new Sgol.Planning.Contracts.WeekPeriod(Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 37,
            range.StartsOn, range.EndsOn, Sgol.Planning.Contracts.WeekContract.Current);
        db.WeekPeriods.Add(otherPeriod);
        await db.SaveChangesAsync();
        var mismatch = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(command with { PeriodId = otherPeriod.Id }));
        Assert.Equal("PERIODO_INVALIDO", mismatch.Code);
        var earlier = System.Text.Json.Nodes.JsonNode.Parse(command.InputPayload!.Value.GetRawText())!;
        earlier["occurredAt"] = Now.AddMinutes(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var invalid = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(command with { InputPayload = JsonSerializer.SerializeToElement(earlier) }));
        Assert.Equal("ORIGEN_INVALIDO", invalid.Code);
        Assert.Equal(1, await db.WorkObligations.CountAsync());
        var accepted = await service.CreateAsync(command);
        var person = await db.AppUsers.Where(u => u.Id == initial.ActorUserId).Select(u => u.PersonId).SingleAsync();
        await ObligationConclusionTestData.EnsurePolicyAsync(db, receipt.TaskDefinitionVersionId, Now);
        await ObligationConclusionTestData.ConcludeAsync(db, parent.ObligationId!.Value, Now, person, receiptDifference: true);
        var replay = await service.CreateAsync(command);
        Assert.Equal(accepted.GenerationRequestId, replay.GenerationRequestId);
        Assert.Equal(accepted.ObligationId, replay.ObligationId);
        Assert.Equal(2, await db.WorkObligations.CountAsync());
    }

    [Fact]
    public async Task MatchingDigestWithDifferentStoredTupleFailsClosedWithoutNewResources()
    {
        var initial = await ResetAndSeedAsync(CanonicalRole.Direction);
        await using var db = CreateContext();
        var service = CreateService(db);
        var command = Command(initial, Guid.CreateVersion7());
        var accepted = await service.CreateAsync(command);
        // Synthetic collision: preserve the digest but replace its independently stored tuple.
        var replacement = JsonSerializer.Serialize(new object[] { 2, "TAR-0007", "OTHER-TUPLE" });
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_obligation SET input_payload = jsonb_set(input_payload, '{{originIdentity}}', {replacement}::jsonb) WHERE id = {accepted.ObligationId}");
        var rejected = await Assert.ThrowsAsync<ManualGenerationException>(() => service.CreateAsync(command with { IdempotencyKey = Guid.CreateVersion7() }));
        Assert.Equal("ORIGEN_IDENTIDAD_INCONSISTENTE", rejected.Code);
        Assert.Equal(1, await db.GenerationRequests.CountAsync());
        Assert.Equal(1, await db.WorkObligations.CountAsync());
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync());
    }
}
