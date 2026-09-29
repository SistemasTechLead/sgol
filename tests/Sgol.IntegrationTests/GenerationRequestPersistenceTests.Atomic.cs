using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class GenerationRequestPersistenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task EveryAtomicWriteStageAndCommitFailureLeaveNoPartialGeneration(int stage)
    {
        var scenario = await ResetAndSeedAsync(CanonicalRole.Direction);
        var fault = new ManualWriteFault(stage);
        var options = new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(fault, new ManualCommitFault(stage == 4)).Options;
        await using (var failing = new SgolDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(failing).CreateAsync(Command(scenario, Guid.CreateVersion7())));
        await using var verify = CreateContext();
        Assert.Empty(await verify.GenerationRequests.ToListAsync());
        Assert.Empty(await verify.WorkObligations.ToListAsync());
        Assert.Empty(await verify.IdempotencyRecords.ToListAsync());
        Assert.Empty(await verify.AuditEvents.ToListAsync());
    }

    private sealed class ManualWriteFault(int stage) : SaveChangesInterceptor
    {
        private int saves;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++saves == stage) throw new InvalidOperationException("Synthetic atomic-write fault.");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class ManualCommitFault(bool enabled) : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (enabled) throw new InvalidOperationException("Synthetic pre-commit fault.");
            return ValueTask.FromResult(result);
        }
    }
}
