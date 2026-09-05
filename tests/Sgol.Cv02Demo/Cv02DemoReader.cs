using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.Cv02Demo;

internal sealed record Cv02Snapshot(
    int Releases,
    int TaskVersions,
    int ActivationRules,
    int EligibilityPolicies,
    int CalendarDays,
    int GenerationRequests,
    int Obligations,
    int Evaluations,
    int EligibleCandidates,
    int Assignments,
    int Plans,
    int PlanVersions,
    int PlanItems,
    int Audits,
    int ScheduledRuns,
    IReadOnlyList<string> RequestResults,
    IReadOnlyList<string> AssignmentStatuses,
    IReadOnlyList<string> PlanStatuses);

internal sealed class Cv02DemoReader(Cv02Database database)
{
    public async Task<Cv02Snapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(new DemoReadOnlyCommandInterceptor())
            .Options;
        await using var context = new SgolDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);

        var snapshot = new Cv02Snapshot(
            await context.ConfigurationReleases.AsNoTracking().CountAsync(cancellationToken),
            await context.TaskDefinitionVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.ActivationRuleVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.EligibilityPolicyVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.CalendarDayVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.GenerationRequests.AsNoTracking().CountAsync(cancellationToken),
            await context.WorkObligations.AsNoTracking().CountAsync(cancellationToken),
            await context.EligibilityEvaluations.AsNoTracking().CountAsync(cancellationToken),
            await context.EligibilityCandidates.AsNoTracking().CountAsync(item => item.IsEligible, cancellationToken),
            await context.AssignmentVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.WorkPlans.AsNoTracking().CountAsync(cancellationToken),
            await context.PlanVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.PlanVersionObligations.AsNoTracking().CountAsync(cancellationToken),
            await context.AuditEvents.AsNoTracking().CountAsync(cancellationToken),
            await context.ScheduledJobRuns.AsNoTracking().CountAsync(cancellationToken),
            await context.GenerationRequests.AsNoTracking().OrderBy(item => item.RequestedAt)
                .Select(item => item.Result).ToArrayAsync(cancellationToken),
            await context.AssignmentVersions.AsNoTracking().OrderBy(item => item.AssignedAt)
                .Select(item => item.Status).ToArrayAsync(cancellationToken),
            await context.WorkPlans.AsNoTracking().OrderBy(item => item.PeriodId)
                .Select(item => item.Status).ToArrayAsync(cancellationToken));

        await transaction.RollbackAsync(cancellationToken);
        if (context.ChangeTracker.Entries().Any())
        {
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        }

        return snapshot;
    }

    internal static string Fingerprint(Cv02Snapshot snapshot) =>
        Cv02Seed.Hash(JsonSerializer.Serialize(snapshot));
}
