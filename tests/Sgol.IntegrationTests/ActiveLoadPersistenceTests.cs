using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class ActiveLoadPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task ActiveLoad_CountsOnlyPendingCurrentAssignmentsAndPreservesAllReadState()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var before = await CountsAsync(context);

        var result = await Reader(context).ListAsync(new(
            scenario.Direction.UserId, null, 100, null));

        Assert.Equal(3, Item(result, scenario.Subcoordination.PersonId).ActiveLoad);
        Assert.Equal(1, Item(result, scenario.Sales.PersonId).ActiveLoad);
        Assert.Equal(0, Item(result, scenario.ZeroLoad.PersonId).ActiveLoad);
        Assert.DoesNotContain(result.Items, item => item.Person.Id == scenario.Inactive.PersonId);
        Assert.All(result.Items, item => Assert.Equal(Now, item.CalculatedAt));
        Assert.Equal(before, await CountsAsync(context));
        Assert.All(await context.EligibilityCandidates.AsNoTracking().ToListAsync(), candidate =>
        {
            Assert.Null(candidate.ActiveLoad);
            Assert.Null(candidate.LastAutoAssignmentAt);
            Assert.Null(candidate.Rank);
        });
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "WorkPlan");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "PlanVersion");
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.ClrType.Name == "PlanVersionObligation");
        Assert.Empty(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.PlanVersionObligations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ActiveLoad_AppliesCanonicalHierarchyAndCannotBeExpandedByPersonFilterOrPositionText()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var reader = Reader(context);

        var direction = await reader.ListAsync(new(scenario.Direction.UserId, null, 100, null));
        Assert.Contains(direction.Items, item => item.Person.Id == scenario.AdministrationPeer.PersonId);

        var administration = await reader.ListAsync(new(scenario.Administration.UserId, null, 100, null));
        Assert.Contains(administration.Items, item => item.Person.Id == scenario.Administration.PersonId);
        Assert.Contains(administration.Items, item => item.Person.Id == scenario.Subcoordination.PersonId);
        Assert.Contains(administration.Items, item => item.Person.Id == scenario.Sales.PersonId);
        Assert.DoesNotContain(administration.Items, item => item.Person.Id == scenario.Direction.PersonId);
        Assert.DoesNotContain(administration.Items, item => item.Person.Id == scenario.AdministrationPeer.PersonId);

        var subcoordination = await reader.ListAsync(new(scenario.Subcoordination.UserId, null, 100, null));
        Assert.Contains(subcoordination.Items, item => item.Person.Id == scenario.Subcoordination.PersonId);
        Assert.Contains(subcoordination.Items, item => item.Person.Id == scenario.Sales.PersonId);
        Assert.DoesNotContain(subcoordination.Items, item => item.Person.Id == scenario.SubcoordinationPeer.PersonId);
        Assert.DoesNotContain(subcoordination.Items, item => item.Person.Id == scenario.Administration.PersonId);

        var sales = await reader.ListAsync(new(scenario.Sales.UserId, null, 100, null));
        Assert.Equal(scenario.Sales.PersonId, Assert.Single(sales.Items).Person.Id);
        var similarPosition = await reader.ListAsync(new(scenario.SimilarPosition.UserId, null, 100, null));
        Assert.Equal(scenario.SimilarPosition.PersonId, Assert.Single(similarPosition.Items).Person.Id);

        var hidden = await reader.ListAsync(new(
            scenario.Administration.UserId, null, 100, scenario.Direction.PersonId));
        Assert.Empty(hidden.Items);
        Assert.Null(hidden.NextCursor);
    }

    [Fact]
    public async Task ActiveLoad_RejectsActorWithoutCurrentCanonicalRoleAndUsesOpaquePagination()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();
        var reader = Reader(context);

        await Assert.ThrowsAsync<ActiveLoadAccessDeniedException>(() => reader.ListAsync(new(
            scenario.WithoutRole.UserId, null, 25, null)));
        await Assert.ThrowsAsync<ActiveLoadFilterInvalidException>(() => reader.ListAsync(new(
            scenario.Direction.UserId, "not-a-cursor", 25, null)));

        var first = await reader.ListAsync(new(scenario.Direction.UserId, null, 2, null));
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        var second = await reader.ListAsync(new(scenario.Direction.UserId, first.NextCursor, 2, null));
        Assert.NotEmpty(second.Items);
        Assert.DoesNotContain(second.Items, item => first.Items.Any(previous => previous.Person.Id == item.Person.Id));
    }

    [Fact]
    public async Task PostgreSql_EnforcesAssignmentHistoryConstraintsAndRestrictRelations()
    {
        var scenario = await ResetAndSeedAsync();
        await using var context = CreateContext();

        var invalidStatus = "INVALIDA";
        var emptyExplanation = "{\"schemaVersion\":1}";
        var invalidStatusException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO assignment_version (id, obligation_id, person_id, status, assignment_type, explanation, assigned_at) VALUES ({Guid.CreateVersion7()}, {scenario.PendingObligationId}, {scenario.ZeroLoad.PersonId}, {invalidStatus}, {AssignmentTypes.Automatic}, CAST({emptyExplanation} AS jsonb), {Now})"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidStatusException.SqlState);

        using var explanation = JsonDocument.Parse("{}");
        context.AssignmentVersions.Add(new AssignmentVersion(
            Guid.CreateVersion7(), scenario.PendingObligationId, scenario.ZeroLoad.PersonId,
            AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, explanation, Now));
        var duplicateCurrent = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(
            AssignmentVersionConfiguration.CurrentObligationIndex,
            (duplicateCurrent.InnerException as PostgresException)?.ConstraintName);
        context.ChangeTracker.Clear();

        var restrictDelete = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM person WHERE id = {scenario.Subcoordination.PersonId}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, restrictDelete.SqlState);
    }

    private async Task<Scenario> ResetAndSeedAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        var direction = AddPerson(context, "LOAD-001", CanonicalRole.Direction);
        var administration = AddPerson(context, "LOAD-010", CanonicalRole.Administration);
        var administrationPeer = AddPerson(context, "LOAD-011", CanonicalRole.Administration);
        var subcoordination = AddPerson(context, "LOAD-020", CanonicalRole.Subcoordination);
        var subcoordinationPeer = AddPerson(context, "LOAD-021", CanonicalRole.Subcoordination);
        var sales = AddPerson(context, "LOAD-030", CanonicalRole.SalesFloor);
        var zeroLoad = AddPerson(context, "LOAD-031", CanonicalRole.SalesFloor);
        var similarPosition = AddPerson(context, "LOAD-032", CanonicalRole.SalesFloor, "Director");
        var inactive = AddPerson(context, "LOAD-040", CanonicalRole.SalesFloor, status: EmploymentStatus.Inactive);
        var withoutRole = AddPerson(context, "LOAD-050", roleCode: null);
        await context.SaveChangesAsync();

        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, direction.UserId, Now.AddHours(-6));
        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = Guid.CreateVersion7();
        using var payload = JsonDocument.Parse("{}");
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1, payload, releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), activeForNew: true);
        var policyId = Guid.CreateVersion7();
        var policy = new EligibilityPolicyVersion(
            policyId, task.Id, taskVersionId, releaseId, null, 1,
            CanonicalRole.Subcoordination, true, null);
        policy.ApplyPublished(Published(policyId));
        var ruleId = Guid.CreateVersion7();
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            ruleId, task.Id, taskVersionId, releaseId, null, 1,
            ActivationModes.Manual, schedule.RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(ruleId));
        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(
            Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 36,
            week.StartsOn, week.EndsOn, WeekContract.Current);
        context.ConfigurationReleases.Add(release);
        context.TaskDefinitionVersions.Add(taskVersion);
        context.EligibilityPolicyVersions.Add(policy);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();

        var pending1 = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-pending-1");
        var pending2 = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-pending-2");
        var transferred = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-transferred");
        var concluded1 = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-concluded-1");
        var concluded2 = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-concluded-2");
        var salesPending = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-sales");
        var inactivePending = await AddObligationAsync(context, taskVersionId, ruleId, period.Id, direction.UserId, "load-inactive");

        AddAutomatic(context, pending1, subcoordination.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-5));
        AddAutomatic(context, pending2, subcoordination.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-4));
        var originalId = AddAutomatic(context, transferred, sales.PersonId, AssignmentVersionStatuses.Superseded, Now.AddHours(-3));
        var firstCorrectionId = AddCorrection(
            context, transferred, sales.PersonId, AssignmentVersionStatuses.Superseded,
            Now.AddHours(-2), direction.UserId, originalId);
        _ = AddCorrection(
            context, transferred, subcoordination.PersonId, AssignmentVersionStatuses.Current,
            Now.AddHours(-1), direction.UserId, firstCorrectionId);
        AddAutomatic(context, concluded1, subcoordination.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-5));
        AddAutomatic(context, concluded2, subcoordination.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-5));
        AddAutomatic(context, salesPending, sales.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-2));
        AddAutomatic(context, inactivePending, inactive.PersonId, AssignmentVersionStatuses.Current, Now.AddHours(-2));
        await ObligationConclusionTestData.ConcludeAsync(context, concluded1, Now.AddMinutes(-1));
        await ObligationConclusionTestData.ConcludeAsync(context, concluded2, Now.AddMinutes(-1));

        using var snapshot = JsonDocument.Parse("{}");
        var evaluation = new EligibilityEvaluation(
            Guid.CreateVersion7(), Guid.CreateVersion7(), pending1, Now.AddHours(-1),
            new DateOnly(2026, 9, 4), EligibilityDateSources.ManualRequest,
            policyId, snapshot, EligibilityResults.NoEligibleCandidate);
        using var reasons = JsonDocument.Parse("[\"DISPONIBILIDAD_AUSENTE\"]");
        context.EligibilityEvaluations.Add(evaluation);
        context.EligibilityCandidates.Add(new EligibilityCandidate(
            evaluation.Id, subcoordination.PersonId, subcoordination.StableCode, false, reasons));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Scenario(
            direction, administration, administrationPeer, subcoordination,
            subcoordinationPeer, sales, zeroLoad, similarPosition, inactive,
            withoutRole, pending1);
    }

    private static SeededPerson AddPerson(
        SgolDbContext context,
        string stableCode,
        string? roleCode,
        string? positionText = null,
        string status = EmploymentStatus.Active)
    {
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = stableCode,
            DisplayName = $"Persona {stableCode}",
            CreatedAt = Now.AddDays(-1),
        });
        context.EmploymentVersions.Add(new EmploymentVersion(
            Guid.CreateVersion7(), personId, BranchScope.LorettaId, status,
            Now.AddDays(-1), positionText: positionText));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-1),
            SecurityStamp = $"synthetic-{userId:N}",
        });
        if (roleCode is not null)
        {
            context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                BranchId = BranchScope.LorettaId,
                RoleCode = roleCode,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = Now.AddDays(-1),
            });
        }

        return new SeededPerson(personId, userId, stableCode);
    }

    private static async Task<Guid> AddObligationAsync(
        SgolDbContext context,
        Guid taskVersionId,
        Guid ruleId,
        Guid periodId,
        Guid requestedBy,
        string origin)
    {
        var requestId = Guid.CreateVersion7();
        var request = new GenerationRequest(
            requestId, Guid.CreateVersion7(), Hash(origin), ruleId,
            BranchScope.LorettaId, periodId, ActivationOriginSchemas.ManualReference,
            origin, requestedBy, Now.AddHours(-6));
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        var evidencePolicyId = await ObligationConclusionTestData.EnsurePolicyAsync(
            context, taskVersionId, Now.AddDays(-1));
        var obligation = new WorkObligation(
            Guid.CreateVersion7(), taskVersionId, BranchScope.LorettaId,
            periodId, requestId, origin, evidencePolicyId);
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
        string status,
        DateTimeOffset assignedAt)
    {
        var id = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            id, obligationId, personId, status, AssignmentTypes.Automatic,
            JsonDocument.Parse("{}"), assignedAt));
        return id;
    }

    private static Guid AddCorrection(
        SgolDbContext context,
        Guid obligationId,
        Guid personId,
        string status,
        DateTimeOffset assignedAt,
        Guid assignedBy,
        Guid supersedesId)
    {
        var id = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            id, obligationId, personId, status, AssignmentTypes.Correction,
            JsonDocument.Parse("{}"), assignedAt, "Corrección sintética",
            assignedBy, supersedesId));
        return id;
    }

    private static ActiveLoadItem Item(ActiveLoadPage page, Guid personId) =>
        Assert.Single(page.Items, item => item.Person.Id == personId);

    private static async Task<DatabaseCounts> CountsAsync(SgolDbContext context) => new(
        await context.WorkObligations.CountAsync(),
        await context.AssignmentVersions.CountAsync(),
        await context.EligibilityEvaluations.CountAsync(),
        await context.EligibilityCandidates.CountAsync(),
        await context.People.CountAsync(),
        await context.EmploymentVersions.CountAsync(),
        await context.RoleAssignmentVersions.CountAsync(),
        await context.AuditEvents.CountAsync());

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static EfActiveLoadReader Reader(SgolDbContext context) =>
        new(context, new FixedClock(Now));

    private static VersionRecord Published(Guid id) => new(
        id, VersionStatuses.Current, Now.AddHours(-6), null, "Configuración sintética", null, 2);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record SeededPerson(Guid PersonId, Guid UserId, string StableCode);
    private sealed record Scenario(
        SeededPerson Direction,
        SeededPerson Administration,
        SeededPerson AdministrationPeer,
        SeededPerson Subcoordination,
        SeededPerson SubcoordinationPeer,
        SeededPerson Sales,
        SeededPerson ZeroLoad,
        SeededPerson SimilarPosition,
        SeededPerson Inactive,
        SeededPerson WithoutRole,
        Guid PendingObligationId);
    private sealed record DatabaseCounts(
        int Obligations,
        int Assignments,
        int Evaluations,
        int Candidates,
        int People,
        int Employments,
        int Roles,
        int Audits);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
