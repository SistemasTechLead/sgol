using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationQueryPersistenceTests
{
    [Fact]
    public async Task Front017DetailUsesCapturedPolicyAndOrderedRequirementsWithoutEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var id = scenario.HistoricalSalesObligationId;
        var capturedId = await db.WorkObligations.Where(o => o.Id == id).Select(o => o.EvidencePolicyVersionId).SingleAsync();
        var expected = await db.EvidenceRequirementVersions.AsNoTracking().Where(r => r.PolicyVersionId == capturedId).OrderBy(r => r.Ordinal).ToListAsync();
        // A superseded captured version remains the obligation's contract.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE evidence_policy_version SET status = 'SUSTITUIDA' WHERE id = {capturedId}");
        var before = await Front016NoEffectSnapshot.ReadAsync(db);
        var detail = (await Reader(db).GetAsync(new(scenario.Direction.UserId, id, null, 25))).Detail;
        Assert.Equal(capturedId, detail.EvidencePolicy!.EvidencePolicyVersionId);
        Assert.Equal(expected.Select(r => r.Id), detail.EvidencePolicy.Requirements.Select(r => r.RequirementVersionId));
        var canonical = EvidencePolicyCatalog.Require(detail.Task.TaskCode);
        Assert.Equal(canonical.Select(r => r.Code), detail.EvidencePolicy.Requirements.Select(r => r.RequirementCode));
        Assert.Equal(canonical.Select(r => r.ConditionCode), detail.EvidencePolicy.Requirements.Select(r => r.ConditionCode));
        Assert.Equal(before, await Front016NoEffectSnapshot.ReadAsync(db));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Front017MismatchedCapturedPolicyFailsClosedWithoutEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var id = scenario.HistoricalSalesObligationId;
        var capturedId = await db.WorkObligations.Where(o => o.Id == id).Select(o => o.EvidencePolicyVersionId).SingleAsync();
        var before = await Front016NoEffectSnapshot.ReadAsync(db);
        await Assert.ThrowsAsync<EvidencePolicyValidationException>(() => new EfCapturedEvidencePolicyReader(db)
            .ReadAsync(capturedId!.Value, Guid.NewGuid(), "TAR-0008"));
        Assert.Equal(before, await Front016NoEffectSnapshot.ReadAsync(db));
    }
}
