using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationConclusionPersistenceTests
{
    private static EfValidationDecisionService Front019Service(SgolDbContext db) => new(db,
        new EfEvidenceConclusionReviewService(db, NewUuidGenerator()), new FixedClock(Now.AddMinutes(20)), NewUuidGenerator());
    private static async Task<string> Front019Rows(SgolDbContext db)
    {
        var output = new List<string>();
        foreach (var table in new[] { "work_obligation", "assignment_version", "execution_result", "evidence_version", "evidence_review_snapshot", "validation_requirement", "validation_decision_version", "audit_event", "idempotency_record", "internal_notice", "outbox_event" })
        {
            var sql = "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM \"" + table + "\" t";
            output.Add(await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return string.Join('\n', output);
    }
    [Fact]
    public async Task Front019HistoryProjectsOrdinaryEscalatedAndReplacementAuthorityWithoutEffects()
    {
        var fixture = await ResetAndCreateObligationAsync(true, taskCode: "TAR-0008");
        await using (var db = CreateContext()) await ConclusionService(db, new FixedClock(Now)).ConcludeAsync(new(fixture.ActorId, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1));
        var admin = await SeedActorAsync("FR019-ADMIN", CanonicalRole.Administration);
        var direction = await SeedActorAsync("FR019-DIRECTION", CanonicalRole.Direction);
        await using var read = CreateContext(); var before = await Front019Rows(read);
        var history = await Front019Service(read).GetAsync(new(admin, Guid.NewGuid(), fixture.ObligationId));
        Assert.Equal("ORDINARIA", history.ValidationActions?.IssueAuthority); Assert.False(history.ValidationActions?.CanReplace);
        Assert.Equal("ESCALAMIENTO", (await Front019Service(read).GetAsync(new(direction, Guid.NewGuid(), fixture.ObligationId))).ValidationActions?.IssueAuthority);
        Assert.Null((await Front019Service(read).GetAsync(new(fixture.ActorId, Guid.NewGuid(), fixture.ObligationId))).ValidationActions?.IssueAuthority);
        Assert.Equal(before, await Front019Rows(read)); Assert.Empty(read.ChangeTracker.Entries());
        await using (var write = CreateContext()) await Front019Service(write).IssueAsync(new(admin, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1, "INCOMPLETA", "Revisión sintética incompleta", null));
        history = await Front019Service(read).GetAsync(new(admin, Guid.NewGuid(), fixture.ObligationId));
        Assert.Null(history.ValidationActions?.IssueAuthority); Assert.True(history.ValidationActions?.CanReplace); Assert.Single(history.Decisions);
        Assert.True((await Front019Service(read).GetAsync(new(direction, Guid.NewGuid(), fixture.ObligationId))).ValidationActions?.CanReplace);
    }
    [Fact]
    public async Task Front019ConcurrentInitialDecisionsLeaveOneCurrentAndLoserConflict()
    {
        var fixture = await ResetAndCreateObligationAsync(true, taskCode: "TAR-0008");
        await using (var db = CreateContext()) await ConclusionService(db, new FixedClock(Now)).ConcludeAsync(new(fixture.ActorId, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1));
        var admin = await SeedActorAsync("FR019-RACE-ADMIN", CanonicalRole.Administration);
        var direction = await SeedActorAsync("FR019-RACE-DIRECTION", CanonicalRole.Direction);
        async Task<string> Issue(Guid actor, string? escalation)
        {
            await using var db = CreateContext();
            try { await Front019Service(db).IssueAsync(new(actor, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1, "CUMPLIDA", "Revisión sintética", escalation)); return "SUCCESS"; }
            catch (ValidationDecisionException e) { return e.Code; }
        }
        var results = await Task.WhenAll(Issue(admin, null), Issue(direction, "Intervención del nivel posterior"));
        Assert.Single(results, r => r == "SUCCESS"); Assert.Single(results, r => r == "VERSION_CONFLICT");
        await using var verify = CreateContext(); Assert.Single(await verify.ValidationDecisionVersions.ToListAsync());
        Assert.Equal("CONCLUIDA", (await verify.WorkObligations.SingleAsync(o => o.Id == fixture.ObligationId)).ExecutionStatus);
    }
    [Fact]
    public async Task Front019HistoryDuringReplacementKeepsOneConsistentChainAndEtag()
    {
        var fixture = await ResetAndCreateObligationAsync(true, taskCode: "TAR-0008");
        await using (var db = CreateContext()) await ConclusionService(db, new FixedClock(Now)).ConcludeAsync(new(fixture.ActorId, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1));
        var admin = await SeedActorAsync("FR019-HISTORY", CanonicalRole.Administration);
        await using var write = CreateContext();
        var first = await Front019Service(write).IssueAsync(new(admin, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1, "CUMPLIDA", "Revisión sintética inicial", null));
        async Task<ValidationHistoryDetails> Read()
        {
            await using var db = CreateContext();
            return await Front019Service(db).GetAsync(new(admin, Guid.NewGuid(), fixture.ObligationId));
        }
        var reads = Enumerable.Range(0, 4).Select(_ => Read()).ToArray();
        await Front019Service(write).ReplaceAsync(new(admin, Guid.NewGuid(), Guid.NewGuid(), first.Decision.DecisionVersionId, 1, "INCOMPLETA", "Revisión sintética posterior", "Sustitución sintética motivada"));
        foreach (var history in await Task.WhenAll(reads))
        {
            Assert.Equal(history.ValidationRequirement!.RowVersion, history.Decisions.Count);
            Assert.Equal(history.RowVersion, history.Decisions[0].VersionNo);
            Assert.Single(history.Decisions, d => d.Status == "VIGENTE");
            Assert.Equal("RESUELTA", history.ValidationRequirement.Status);
        }
        var before = await Front019Rows(write); await Read(); Assert.Equal(before, await Front019Rows(write));
    }
    [Fact]
    public async Task Front019LostAuthorityCannotRecoverPriorDecisionAndReadsRemainPure()
    {
        var fixture = await ResetAndCreateObligationAsync(true, taskCode: "TAR-0008");
        await using (var db = CreateContext()) await ConclusionService(db, new FixedClock(Now)).ConcludeAsync(new(fixture.ActorId, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1));
        var admin = await SeedActorAsync("FR019-LOST", CanonicalRole.Administration);
        await using var write = CreateContext();
        var command = new IssueValidationDecisionCommand(admin, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1, "CUMPLIDA", "Revisión sintética fundada", null);
        await Front019Service(write).IssueAsync(command);
        await write.Database.ExecuteSqlInterpolatedAsync($"UPDATE role_assignment_version SET role_code = 'SUBCOORDINACION' WHERE user_id = {admin}");
        var before = await Front019Rows(write);
        await Assert.ThrowsAsync<ValidationObligationNotFoundException>(() => Front019Service(write).IssueAsync(command));
        await Assert.ThrowsAsync<ValidationObligationNotFoundException>(() => Front019Service(write).GetAsync(new(admin, Guid.NewGuid(), fixture.ObligationId)));
        Assert.Equal(before, await Front019Rows(write));
    }
    [Fact]
    public async Task Front019AuditFailureRollsBackAndSameIntentionCanRecover()
    {
        var fixture = await ResetAndCreateObligationAsync(true, taskCode: "TAR-0008");
        await using (var db = CreateContext()) await ConclusionService(db, new FixedClock(Now)).ConcludeAsync(new(fixture.ActorId, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1));
        var admin = await SeedActorAsync("FR019-AUDIT", CanonicalRole.Administration);
        await using var write = CreateContext(); var before = await Front019Rows(write);
        await write.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT front019_reject_audit CHECK (action <> 'VALIDATION_DECISION_ISSUED') NOT VALID");
        var command = new IssueValidationDecisionCommand(admin, Guid.NewGuid(), Guid.NewGuid(), fixture.ObligationId, 1, "NO_CUMPLIDA", "Revisión sintética desfavorable", null);
        await Assert.ThrowsAnyAsync<Exception>(() => Front019Service(write).IssueAsync(command));
        Assert.Equal(before, await Front019Rows(write));
        await write.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT front019_reject_audit");
        var first = await Front019Service(write).IssueAsync(command); var committed = await Front019Rows(write);
        var replay = await Front019Service(write).IssueAsync(command);
        Assert.Equal(first.Decision.DecisionVersionId, replay.Decision.DecisionVersionId); Assert.True(replay.Replayed);
        Assert.Equal(committed, await Front019Rows(write));
    }
}
