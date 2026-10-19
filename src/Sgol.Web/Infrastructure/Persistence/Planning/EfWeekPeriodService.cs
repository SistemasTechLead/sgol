using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class EfWeekPeriodService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IWeekPeriodService
{
    public async Task<WeekPeriodDetails> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        int isoYear,
        int isoWeek,
        CancellationToken cancellationToken = default)
    {
        var range = WeekContract.Calculate(isoYear, isoWeek);
        await EnsureAuthorizedAsync(actorUserId, correlationId, cancellationToken);

        var status = WeekContract.DeriveStatus(range.EndsOn, WeekContract.LocalToday(clock.UtcNow));
        var period = await dbContext.WeekPeriods
            .SingleOrDefaultAsync(
                item => item.BranchId == BranchScope.LorettaId &&
                    item.IsoYear == isoYear &&
                    item.IsoWeek == isoWeek,
                cancellationToken);

        if (period is null)
        {
            period = await MaterializeAsync(
                actorUserId,
                correlationId,
                isoYear,
                isoWeek,
                range,
                status,
                cancellationToken);
        }
        else if (period.DerivedStatus != status)
        {
            await RefreshStatusAsync(period, actorUserId, correlationId, status, cancellationToken);
        }

        var result = ToDetails(period);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<WeekPeriod> MaterializeAsync(
        Guid actorUserId,
        Guid correlationId,
        int isoYear,
        int isoWeek,
        WeekRange range,
        string status,
        CancellationToken cancellationToken)
    {
        var period = new WeekPeriod(
            uuidGenerator.NewUuid(),
            BranchScope.LorettaId,
            isoYear,
            isoWeek,
            range.StartsOn,
            range.EndsOn,
            status);

        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    dbContext.WeekPeriods.Add(period);
                    await Task.CompletedTask;
                    return NewAuditEvent(
                        actorUserId,
                        correlationId,
                        period.Id,
                        "WEEK_PERIOD_MATERIALIZED",
                        afterData: SerializeAuditValue(period));
                },
                cancellationToken);
            return period;
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) == WeekPeriodConfiguration.UniqueIndex)
        {
            dbContext.ChangeTracker.Clear();
            return await dbContext.WeekPeriods
                .AsNoTracking()
                .SingleAsync(
                    item => item.BranchId == BranchScope.LorettaId &&
                        item.IsoYear == isoYear &&
                        item.IsoWeek == isoWeek,
                    cancellationToken);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task RefreshStatusAsync(
        WeekPeriod period,
        Guid actorUserId,
        Guid correlationId,
        string status,
        CancellationToken cancellationToken)
    {
        var beforeData = SerializeAuditValue(period);
        period.RefreshDerivedStatus(status);
        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                period.Id,
                "WEEK_PERIOD_DERIVED_STATUS_REFRESHED",
                beforeData,
                SerializeAuditValue(period)),
            _ => Task.CompletedTask,
            cancellationToken);
    }

    private async Task EnsureAuthorizedAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await PlanningAuthorizationQuery.HasPlanViewAsync(
                dbContext,
                actorUserId,
                cancellationToken))
        {
            return;
        }

        await auditTransaction.ExecuteAsync(
            NewAuditEvent(
                actorUserId,
                correlationId,
                resourceId: null,
                action: "WEEK_PERIOD_ACCESS_DENIED",
                outcome: "DENIED"),
            _ => Task.CompletedTask,
            cancellationToken);
        dbContext.ChangeTracker.Clear();
        throw new WeekAccessDeniedException();
    }

    private AuditEvent NewAuditEvent(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        string action,
        JsonDocument? beforeData = null,
        JsonDocument? afterData = null,
        string outcome = "SUCCESS") => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorUserId = actorUserId,
            ActorType = "APP_USER",
            Action = action,
            ResourceType = "WEEK_PERIOD",
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            BeforeData = beforeData,
            AfterData = afterData,
            Outcome = outcome,
        };

    private static JsonDocument SerializeAuditValue(WeekPeriod period) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            weekPeriodId = period.Id,
            branchId = period.BranchId,
            isoYear = period.IsoYear,
            isoWeek = period.IsoWeek,
            startsOn = period.StartsOn,
            endsOn = period.EndsOn,
            derivedStatus = period.DerivedStatus,
        });

    private static WeekPeriodDetails ToDetails(WeekPeriod period) => new(
        period.Id,
        BranchScope.LorettaCode,
        period.IsoYear,
        period.IsoWeek,
        period.StartsOn,
        period.EndsOn,
        period.DerivedStatus);

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
}
