using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class ObligationQueryPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task HierarchyUsesCurrentResponsibilityAndHidesMissingAndOutOfScopeIdentically()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var reader = Reader(context);

        var direction = await reader.ListAsync(new(
            scenario.Direction.UserId, null, null, null, null, null, null, 100));
        Assert.Equal(scenario.ObligationIds.Count, direction.Items.Count);
        Assert.Contains(direction.Items, item => item.ObligationId == scenario.UnassignedObligationId);

        var administration = await reader.ListAsync(new(
            scenario.Administration.UserId, null, null, null, null, null, null, 100));
        Assert.Contains(administration.Items, item => item.ObligationId == scenario.AdministrationObligationId);
        Assert.Contains(administration.Items, item => item.ObligationId == scenario.SubcoordinationObligationId);
        Assert.Contains(administration.Items, item => item.ObligationId == scenario.SalesObligationId);
        Assert.DoesNotContain(administration.Items, item => item.ObligationId == scenario.DirectionObligationId);
        Assert.DoesNotContain(administration.Items, item => item.ObligationId == scenario.AdministrationPeerObligationId);
        Assert.DoesNotContain(administration.Items, item => item.ObligationId == scenario.UnassignedObligationId);

        var subcoordination = await reader.ListAsync(new(
            scenario.Subcoordination.UserId, null, null, null, null, null, null, 100));
        Assert.Contains(subcoordination.Items, item => item.ObligationId == scenario.SubcoordinationObligationId);
        Assert.Contains(subcoordination.Items, item => item.ObligationId == scenario.SalesObligationId);
        Assert.DoesNotContain(subcoordination.Items, item => item.ObligationId == scenario.AdministrationObligationId);

        var sales = await reader.ListAsync(new(
            scenario.Sales.UserId, null, null, null, null, null, null, 100));
        Assert.All(sales.Items, item =>
            Assert.Equal(scenario.Sales.PersonId, item.CurrentAssignment?.Responsible.PersonId));
        Assert.DoesNotContain(sales.Items, item => item.ObligationId == scenario.HistoricalSalesObligationId);

        var ownDetail = await reader.GetAsync(new(
            scenario.Sales.UserId, scenario.SalesObligationId, null, 25));
        Assert.Equal(scenario.SalesObligationId, ownDetail.Detail.ObligationId);
        var lowerDetail = await reader.GetAsync(new(
            scenario.Administration.UserId, scenario.SalesObligationId, null, 25));
        Assert.Equal(scenario.SalesObligationId, lowerDetail.Detail.ObligationId);

        await Assert.ThrowsAsync<ObligationQueryNotFoundException>(() => reader.GetAsync(new(
            scenario.Sales.UserId, scenario.DirectionObligationId, null, 25)));
        await Assert.ThrowsAsync<ObligationQueryNotFoundException>(() => reader.GetAsync(new(
            scenario.Administration.UserId, scenario.AdministrationPeerObligationId, null, 25)));
        await Assert.ThrowsAsync<ObligationQueryNotFoundException>(() => reader.GetAsync(new(
            scenario.Sales.UserId, Guid.CreateVersion7(), null, 25)));
        await Assert.ThrowsAsync<ObligationQueryAccessDeniedException>(() => reader.ListAsync(new(
            Guid.CreateVersion7(), null, null, null, null, null, null, 25)));

        var hiddenFilter = await reader.ListAsync(new(
            scenario.Administration.UserId,
            null,
            null,
            null,
            null,
            scenario.Direction.PersonId,
            null,
            25));
        Assert.Empty(hiddenFilter.Items);
        Assert.Null(hiddenFilter.NextCursor);
    }

    [Fact]
    public async Task DetailProjectsAllowedOriginsDatesAssignmentsAndPublicationsWithoutWriteEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var before = await CountsAsync(context);
        var reader = Reader(context);

        var manual = await reader.GetAsync(new(
            scenario.Direction.UserId, scenario.HistoricalSalesObligationId, null, 2));
        Assert.Equal(ObligationOriginKinds.Manual, manual.Detail.Origin.Kind);
        Assert.Equal(ObligationConditions.Overdue, manual.Detail.Condition);
        Assert.Equal(new DateOnly(2026, 9, 5), manual.Detail.Dates.DueLocalDate);
        Assert.Equal(2, manual.Detail.History.Count);
        Assert.NotNull(manual.HistoryNextCursor);
        var rest = await reader.GetAsync(new(
            scenario.Direction.UserId,
            scenario.HistoricalSalesObligationId,
            manual.HistoryNextCursor,
            100));
        var completeHistory = manual.Detail.History.Concat(rest.Detail.History).ToArray();
        Assert.Equal(
            [
                ObligationHistoryEventTypes.GenerationRequested,
                ObligationHistoryEventTypes.AutomaticAssignment,
                ObligationHistoryEventTypes.CorrectedAssignment,
                ObligationHistoryEventTypes.IncludedInPublication,
                ObligationHistoryEventTypes.IncludedInPublication,
            ],
            completeHistory.Select(item => item.EventType));
        Assert.Equal("Corrección sintética autorizada", completeHistory[2].Reason);
        Assert.NotNull(completeHistory[1].Assignment);
        Assert.NotNull(completeHistory[3].Publication);
        Assert.DoesNotContain(manual.Detail.Links.Keys, key => key.Contains("evidence", StringComparison.OrdinalIgnoreCase));

        var recurrent = await reader.GetAsync(new(
            scenario.Direction.UserId, scenario.RecurringObligationId, null, 25));
        Assert.Equal(ObligationOriginKinds.Recurring, recurrent.Detail.Origin.Kind);
        Assert.Equal("LOR-001|2026-09-05|12:00", recurrent.Detail.Origin.Reference);
        Assert.Equal(ObligationConditions.NotOverdue, recurrent.Detail.Condition);

        var concluded = await reader.GetAsync(new(
            scenario.Direction.UserId, scenario.DirectionObligationId, null, 25));
        Assert.Equal(WorkObligationStatuses.Concluded, concluded.Detail.ExecutionStatus);
        Assert.Equal(ObligationConditions.NotOverdue, concluded.Detail.Condition);
        Assert.NotNull(concluded.Detail.Dates.ConcludedAt);

        Assert.Equal(before, await CountsAsync(context));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FiltersAndKeysetCursorReturnStableAuthorizedPagesWithoutDuplicates()
    {
        var scenario = await ResetAndSeedAsync(additionalSalesObligations: 24);
        await using var context = CreateContext();
        var reader = Reader(context);

        var first = await reader.ListAsync(new(
            scenario.Direction.UserId, null, null, null, null, null, null, 25));
        Assert.Equal(25, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        var second = await reader.ListAsync(new(
            scenario.Direction.UserId, null, null, null, null, null, first.NextCursor, 25));
        Assert.NotEmpty(second.Items);
        Assert.Empty(first.Items.Select(item => item.ObligationId)
            .Intersect(second.Items.Select(item => item.ObligationId)));
        Assert.Equal(
            scenario.ObligationIds.Count,
            first.Items.Concat(second.Items).Select(item => item.ObligationId).Distinct().Count());

        var overdue = await reader.ListAsync(new(
            scenario.Direction.UserId,
            scenario.PeriodId,
            "TAR-0008",
            WorkObligationStatuses.Pending,
            ObligationConditions.Overdue,
            scenario.AdministrationPeer.PersonId,
            null,
            100));
        var overdueItem = Assert.Single(overdue.Items);
        Assert.Equal(scenario.HistoricalSalesObligationId, overdueItem.ObligationId);
        Assert.Equal(ObligationConditions.Overdue, overdueItem.Condition);

        var concluded = await reader.ListAsync(new(
            scenario.Direction.UserId,
            scenario.PeriodId,
            "TAR-0008",
            WorkObligationStatuses.Concluded,
            ObligationConditions.NotOverdue,
            scenario.Direction.PersonId,
            null,
            100));
        Assert.Equal(scenario.DirectionObligationId, Assert.Single(concluded.Items).ObligationId);

        await Assert.ThrowsAsync<ObligationQueryFilterInvalidException>(() => reader.ListAsync(new(
            scenario.Direction.UserId, null, "TAR-9999", null, null, null, null, 25)));
        await Assert.ThrowsAsync<ObligationQueryFilterInvalidException>(() => reader.ListAsync(new(
            scenario.Direction.UserId, null, null, null, null, null, "not-a-cursor", 25)));
    }

    private async Task<Scenario> ResetAndSeedAsync(int additionalSalesObligations = 0)
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var direction = AddPerson(context, "QUERY-DIR", CanonicalRole.Direction);
        var administration = AddPerson(context, "QUERY-ADM", CanonicalRole.Administration);
        var administrationPeer = AddPerson(context, "QUERY-ADM-PEER", CanonicalRole.Administration);
        var subcoordination = AddPerson(context, "QUERY-SUB", CanonicalRole.Subcoordination);
        var sales = AddPerson(context, "QUERY-SALES", CanonicalRole.SalesFloor);
        var salesPeer = AddPerson(context, "QUERY-SALES-PEER", CanonicalRole.SalesFloor);
        await context.SaveChangesAsync();

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, direction.UserId, Now.AddDays(-2));
        context.ConfigurationReleases.Add(release);

        var manualTask = TaskDefinitionCatalog.Require("TAR-0008");
        var manualVersionId = Guid.CreateVersion7();
        using var manualPayload = JsonDocument.Parse("{}");
        var manualVersion = new TaskDefinitionVersion(
            manualVersionId, manualTask.Id, 1, 1, manualPayload, releaseId);
        manualVersion.ApplyPublished(Published(manualVersionId), activeForNew: true);
        var manualRuleId = Guid.CreateVersion7();
        using var manualSchedule = JsonDocument.Parse("null");
        var manualRule = new ActivationRuleVersion(
            manualRuleId,
            manualTask.Id,
            manualVersionId,
            releaseId,
            null,
            1,
            ActivationModes.Manual,
            manualSchedule.RootElement,
            ActivationOriginSchemas.ManualReference);
        manualRule.ApplyPublished(Published(manualRuleId));

        var recurringTask = TaskDefinitionCatalog.Require("TAR-0005");
        var recurringVersionId = Guid.CreateVersion7();
        using var recurringPayload = JsonDocument.Parse("{}");
        var recurringVersion = new TaskDefinitionVersion(
            recurringVersionId, recurringTask.Id, 1, 1, recurringPayload, releaseId);
        recurringVersion.ApplyPublished(Published(recurringVersionId), activeForNew: true);
        var recurringRuleId = Guid.CreateVersion7();
        using var recurringSchedule = JsonDocument.Parse(
            "{\"kind\":\"WORKING_DAY_WINDOWS\",\"workingDaysOnly\":true," +
            "\"localTimes\":[\"12:00\",\"17:00\"],\"timeZone\":\"America/Mexico_City\"}");
        var recurringRule = new ActivationRuleVersion(
            recurringRuleId,
            recurringTask.Id,
            recurringVersionId,
            releaseId,
            null,
            1,
            ActivationModes.Recurring,
            recurringSchedule.RootElement,
            ActivationOriginSchemas.WorkingDayWindow);
        recurringRule.ApplyPublished(Published(recurringRuleId));

        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(),
            BranchScope.LorettaId,
            2026,
            36,
            week.StartsOn,
            week.EndsOn,
            WeekContract.Current);
        context.TaskDefinitionVersions.AddRange(manualVersion, recurringVersion);
        context.ActivationRuleVersions.AddRange(manualRule, recurringRule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();

        var obligationIds = new List<Guid>();
        var directionObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Dirección");
        var administrationObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Administración");
        var administrationPeerObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Par administración");
        var subcoordinationObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Subcoordinación");
        var salesObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Ventas");
        var salesPeerObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Par ventas");
        var unassignedObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Sin asignar");
        var historicalSalesObligationId = await AddManualObligationAsync(context, manualVersionId, manualRuleId, period.Id, direction.UserId, "Historia corregida");
        var recurringObligationId = await AddRecurringObligationAsync(context, recurringVersionId, recurringRuleId, period.Id);
        obligationIds.AddRange([
            directionObligationId,
            administrationObligationId,
            administrationPeerObligationId,
            subcoordinationObligationId,
            salesObligationId,
            salesPeerObligationId,
            unassignedObligationId,
            historicalSalesObligationId,
            recurringObligationId,
        ]);

        var directionAssignment = AddAutomatic(context, directionObligationId, direction.PersonId, Now.AddHours(-8));
        _ = AddAutomatic(context, administrationObligationId, administration.PersonId, Now.AddHours(-7));
        _ = AddAutomatic(context, administrationPeerObligationId, administrationPeer.PersonId, Now.AddHours(-7));
        _ = AddAutomatic(context, subcoordinationObligationId, subcoordination.PersonId, Now.AddHours(-6));
        _ = AddAutomatic(context, salesObligationId, sales.PersonId, Now.AddHours(-5));
        _ = AddAutomatic(context, salesPeerObligationId, salesPeer.PersonId, Now.AddHours(-5));
        _ = AddAutomatic(context, recurringObligationId, sales.PersonId, Now.AddHours(-4));
        var historicalAssignment = AddAutomatic(
            context,
            historicalSalesObligationId,
            sales.PersonId,
            Now.AddHours(-4),
            AssignmentVersionStatuses.Superseded);
        var correctedAssignment = AddCorrection(
            context,
            historicalSalesObligationId,
            administrationPeer.PersonId,
            Now.AddHours(-3),
            direction.UserId,
            historicalAssignment);
        _ = directionAssignment;

        for (var index = 0; index < additionalSalesObligations; index++)
        {
            var id = await AddManualObligationAsync(
                context,
                manualVersionId,
                manualRuleId,
                period.Id,
                direction.UserId,
                $"Página {index:00}");
            obligationIds.Add(id);
            _ = AddAutomatic(context, id, sales.PersonId, Now.AddMinutes(index));
        }

        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_obligation SET due_at = {Now.AddMinutes(-1)} WHERE id = {historicalSalesObligationId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_obligation SET due_at = {Now.AddMinutes(1)} WHERE id = {recurringObligationId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_obligation SET due_at = {Now.AddMinutes(-2)} WHERE id = {directionObligationId}");
        await ObligationConclusionTestData.ConcludeAsync(context, directionObligationId, Now.AddMinutes(-1));

        var plan = new WorkPlan(Guid.CreateVersion7(), BranchScope.LorettaId, period.Id);
        plan.ApplyPublication();
        var publication1 = new PlanVersion(
            Guid.CreateVersion7(), plan.Id, 1, CanonicalRole.Direction,
            direction.UserId, Now.AddHours(-2), null, plan.RowVersion);
        publication1.Supersede();
        plan.ApplyPublication();
        var publication2 = new PlanVersion(
            Guid.CreateVersion7(), plan.Id, 2, CanonicalRole.Direction,
            direction.UserId, Now.AddHours(-1), publication1.Id, plan.RowVersion);
        context.WorkPlans.Add(plan);
        context.PlanVersions.AddRange(publication1, publication2);
        context.PlanVersionObligations.AddRange(
            new PlanVersionObligation(publication1.Id, historicalSalesObligationId, correctedAssignment),
            new PlanVersionObligation(publication2.Id, historicalSalesObligationId, correctedAssignment));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Scenario(
            direction,
            administration,
            administrationPeer,
            subcoordination,
            sales,
            directionObligationId,
            administrationObligationId,
            administrationPeerObligationId,
            subcoordinationObligationId,
            salesObligationId,
            unassignedObligationId,
            historicalSalesObligationId,
            recurringObligationId,
            period.Id,
            obligationIds);
    }

    private static SeededPerson AddPerson(SgolDbContext context, string stableCode, string roleCode)
    {
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = $"Persona {stableCode}",
            CreatedAt = Now.AddDays(-3),
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(), personId, BranchScope.LorettaId,
            EmploymentStatus.Active, Now.AddDays(-3)));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-3),
            SecurityStamp = $"query-{userId:N}",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = roleCode,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now.AddDays(-3),
        });
        return new SeededPerson(personId, userId);
    }

    private static async Task<Guid> AddManualObligationAsync(
        SgolDbContext context,
        Guid taskVersionId,
        Guid ruleId,
        Guid periodId,
        Guid requestedBy,
        string originReference)
    {
        return await AddObligationAsync(
            context,
            taskVersionId,
            ruleId,
            periodId,
            ActivationOriginSchemas.ManualReference,
            originReference,
            requestedBy,
            Now.AddHours(-10));
    }

    private static async Task<Guid> AddRecurringObligationAsync(
        SgolDbContext context,
        Guid taskVersionId,
        Guid ruleId,
        Guid periodId)
    {
        return await AddObligationAsync(
            context,
            taskVersionId,
            ruleId,
            periodId,
            ActivationOriginSchemas.WorkingDayWindow,
            "LOR-001|2026-09-05|12:00",
            null,
            Now.AddHours(-9));
    }

    private static async Task<Guid> AddObligationAsync(
        SgolDbContext context,
        Guid taskVersionId,
        Guid ruleId,
        Guid periodId,
        string originType,
        string originReference,
        Guid? requestedBy,
        DateTimeOffset requestedAt)
    {
        var request = new GenerationRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Hash(originReference),
            ruleId,
            BranchScope.LorettaId,
            periodId,
            originType,
            originReference,
            requestedBy,
            requestedAt);
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        var obligation = new WorkObligation(
            Guid.CreateVersion7(),
            taskVersionId,
            BranchScope.LorettaId,
            periodId,
            request.Id,
            originReference);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        request.LinkObligation(obligation.Id);
        await context.SaveChangesAsync();
        return obligation.Id;
    }

    private static Guid AddAutomatic(
        SgolDbContext context,
        Guid obligationId,
        Guid personId,
        DateTimeOffset assignedAt,
        string status = AssignmentVersionStatuses.Current)
    {
        var id = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            id,
            obligationId,
            personId,
            status,
            AssignmentTypes.Automatic,
            JsonDocument.Parse("{}"),
            assignedAt));
        return id;
    }

    private static Guid AddCorrection(
        SgolDbContext context,
        Guid obligationId,
        Guid personId,
        DateTimeOffset assignedAt,
        Guid assignedBy,
        Guid supersedesId)
    {
        var id = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            id,
            obligationId,
            personId,
            AssignmentVersionStatuses.Current,
            AssignmentTypes.Correction,
            JsonDocument.Parse("{}"),
            assignedAt,
            "Corrección sintética autorizada",
            assignedBy,
            supersedesId));
        return id;
    }

    private static async Task<DatabaseCounts> CountsAsync(SgolDbContext context) => new(
        await context.GenerationRequests.CountAsync(),
        await context.WorkObligations.CountAsync(),
        await context.AssignmentVersions.CountAsync(),
        await context.WorkPlans.CountAsync(),
        await context.PlanVersions.CountAsync(),
        await context.PlanVersionObligations.CountAsync(),
        await context.AuditEvents.CountAsync(),
        await context.IdempotencyRecords.CountAsync(),
        await context.OutboxEvents.CountAsync(),
        await context.ScheduledJobRuns.CountAsync());

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfObligationQueryReader Reader(SgolDbContext context) =>
        new(context, new FixedClock(Now));

    private static VersionRecord Published(Guid id) => new(
        id,
        VersionStatuses.Current,
        Now.AddDays(-2),
        null,
        "Configuración sintética",
        null,
        2);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record SeededPerson(Guid PersonId, Guid UserId);

    private sealed record Scenario(
        SeededPerson Direction,
        SeededPerson Administration,
        SeededPerson AdministrationPeer,
        SeededPerson Subcoordination,
        SeededPerson Sales,
        Guid DirectionObligationId,
        Guid AdministrationObligationId,
        Guid AdministrationPeerObligationId,
        Guid SubcoordinationObligationId,
        Guid SalesObligationId,
        Guid UnassignedObligationId,
        Guid HistoricalSalesObligationId,
        Guid RecurringObligationId,
        Guid PeriodId,
        IReadOnlyList<Guid> ObligationIds);

    private sealed record DatabaseCounts(
        int GenerationRequests,
        int Obligations,
        int Assignments,
        int Plans,
        int PlanVersions,
        int PlanMemberships,
        int Audits,
        int IdempotencyRecords,
        int OutboxEvents,
        int ScheduledRuns);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
