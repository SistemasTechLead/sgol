using Microsoft.EntityFrameworkCore;
using Sgol.Execution.Contracts;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationQueryPersistenceTests
{
    [Fact]
    public async Task Front018ActionProjectionUsesServerAuthorityAndReadHasNoEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var reader = Reader(db);
        await db.AppUsers.ExecuteUpdateAsync(set => set.SetProperty(u => u.MfaEnrolledAt, (DateTimeOffset?)null));
        var disabled = (await reader.GetAsync(new(scenario.Sales.UserId, scenario.SalesObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(false, false), disabled.EvidenceActions);
        await db.AppUsers.ExecuteUpdateAsync(set => set.SetProperty(u => u.MfaEnrolledAt, Now.AddDays(-1)));
        var before = await Front016NoEffectSnapshot.ReadAsync(db);
        var own = (await reader.GetAsync(new(scenario.Sales.UserId, scenario.SalesObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(true, true), own.EvidenceActions);
        var superior = (await reader.GetAsync(new(scenario.Direction.UserId, scenario.SalesObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(false, false), superior.EvidenceActions);
        var unassigned = (await reader.GetAsync(new(scenario.Direction.UserId, scenario.UnassignedObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(false, false), unassigned.EvidenceActions);
        Assert.Equal(before, await Front016NoEffectSnapshot.ReadAsync(db));
        await ObligationConclusionTestData.ConcludeAsync(db, scenario.SalesObligationId, Now);
        db.ChangeTracker.Clear(); before = await Front016NoEffectSnapshot.ReadAsync(db);
        own = (await reader.GetAsync(new(scenario.Sales.UserId, scenario.SalesObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(false, false), own.EvidenceActions);
        superior = (await reader.GetAsync(new(scenario.Direction.UserId, scenario.SalesObligationId, null, 25))).Detail;
        Assert.Equal(new ObligationEvidenceActions(true, false), superior.EvidenceActions);
        Assert.Equal(before, await Front016NoEffectSnapshot.ReadAsync(db)); Assert.Empty(db.ChangeTracker.Entries());
    }
}
